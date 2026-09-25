import concurrent.futures
import gzip
import hashlib
import io
import json
import os
import stat
import sys
import tempfile
import threading
import time
import unittest
import urllib.error
import urllib.parse
import urllib.request
import warnings
import zipfile
from pathlib import Path
from dataclasses import replace
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import server


class PublisherTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.token = "local-test-token-not-a-production-credential"
        self.service = server.PublisherServer(("127.0.0.1", 0),
            server.Config(Path(self.directory.name), "http://127.0.0.1", self.token))
        self.base = f"http://127.0.0.1:{self.service.server_port}"
        self.service.store.config = server.Config(Path(self.directory.name), self.base, self.token)
        self.thread = threading.Thread(target=lambda: self.service.serve_forever(poll_interval=0.01), daemon=True)
        self.thread.start()

    def tearDown(self):
        self.service.shutdown()
        self.service.server_close()
        self.thread.join(timeout=2)
        self.directory.cleanup()

    def manifest(self, data=b"real downloadable bundle bytes", revision="revision-1"):
        return {"schemaVersion": 1, "sdkVersion": "0.1.0", "unityVersion": "2022.3.16f1",
                "platform": "Android", "renderPipeline": "BuiltIn", "worldId": "sample-world",
                "revisionId": revision, "entryScene": "Assets/Scenes/World.unity",
                "scenes": ["Assets/Scenes/World.unity"], "requiredTypes": [],
                "bundles": [{"name": "world-scenes", "fileName": "world-scenes", "sizeBytes": len(data),
                             "sha256": hashlib.sha256(data).hexdigest(), "crc": 123,
                             "unityHash": "a" * 32, "dependencies": []}]}

    def archive(self, manifest=None, data=b"real downloadable bundle bytes", extra=None, raw=None):
        output = io.BytesIO()
        with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("world.json", raw if raw is not None else json.dumps(manifest or self.manifest(data)).encode())
            archive.writestr("world-scenes", data)
            if extra:
                for name, content in extra:
                    archive.writestr(name, content)
        return output.getvalue()

    def request(self, path, data=None, authenticated=True, headers=None):
        values = {} if data is None else {"Content-Type": "application/zip"}
        if authenticated:
            values["Authorization"] = "Bearer " + self.token
        values.update(headers or {})
        request = urllib.request.Request(self.base + path, data=data, headers=values)
        try:
            response = urllib.request.urlopen(request, timeout=5)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.status, response.read(), response.headers

    def publish(self, archive=None):
        status, body, _ = self.request("/api/publish", archive or self.archive())
        return status, json.loads(body)

    def assert_no_publication(self):
        self.assertEqual([], list(self.service.store.worlds.glob("*/*/world.json")))
        for _ in range(100):
            if not list(self.service.store.staging.iterdir()):
                break
            time.sleep(0.01)
        self.assertEqual([], list(self.service.store.staging.iterdir()))

    def test_publish_download_launch_and_real_qr(self):
        raw = json.dumps(self.manifest(), indent=2).encode()
        status, result = self.publish(self.archive(raw=raw))
        self.assertEqual(201, status)
        self.assertEqual(hashlib.sha256(raw).hexdigest(), result["manifestSha256"])
        launch = urllib.parse.urlsplit(result["launchUrl"])
        self.assertEqual(("kimchily", "world"), (launch.scheme, launch.netloc))
        query = urllib.parse.parse_qs(launch.query)
        self.assertEqual([result["manifestUrl"]], query["manifest"])
        self.assertEqual([result["manifestSha256"]], query["sha256"])
        code, downloaded, headers = self.request(urllib.parse.urlsplit(result["manifestUrl"]).path, authenticated=False)
        self.assertEqual((200, raw), (code, downloaded))
        self.assertIn("immutable", headers["Cache-Control"])
        code, bundle, _ = self.request("/worlds/sample-world/revision-1/world-scenes", authenticated=False)
        self.assertEqual((200, b"real downloadable bundle bytes"), (code, bundle))
        code, png, headers = self.request(urllib.parse.urlsplit(result["qrUrl"]).path, authenticated=False)
        self.assertEqual((200, "image/png"), (code, headers["Content-Type"]))
        self.assertTrue(png.startswith(b"\x89PNG\r\n\x1a\n"))
        from PIL import Image
        with Image.open(io.BytesIO(png)) as image:
            self.assertGreaterEqual(image.width, 200)
            self.assertEqual(image.width, image.height)
            self.assertEqual((0, 255), image.convert("L").getextrema())
        code, page, _ = self.request("/w/sample-world/revision-1", authenticated=False)
        self.assertEqual(200, code)
        self.assertIn(b"kimchily://world?manifest=", page)
        self.assertIn(result["qrUrl"].encode(), page)

    def test_revision_is_immutable(self):
        self.assertEqual(201, self.publish()[0])
        status, body = self.publish(self.archive(data=b"different data", manifest=self.manifest(b"different data")))
        self.assertEqual((409, "REVISION_EXISTS"), (status, body["code"]))
        self.assertEqual(b"real downloadable bundle bytes", self.request("/worlds/sample-world/revision-1/world-scenes")[1])

    def test_concurrent_same_revision_has_one_atomic_winner(self):
        with concurrent.futures.ThreadPoolExecutor(2) as pool:
            results = list(pool.map(lambda _: self.publish()[0], range(2)))
        self.assertEqual([201, 409], sorted(results))
        self.assertEqual(1, len(list(self.service.store.worlds.glob("*/*/world.json"))))

    def test_authentication_precedes_upload_and_health_has_no_token(self):
        for headers in ({}, {"Authorization": "Bearer wrong"}):
            code, body, _ = self.request("/api/publish", self.archive(), authenticated=False, headers=headers)
            self.assertEqual((401, "UNAUTHORIZED"), (code, json.loads(body)["code"]))
        self.assert_no_publication()
        code, body, _ = self.request("/health", authenticated=False)
        self.assertEqual(200, code)
        self.assertNotIn(self.token.encode(), body)

    def test_zip_traversal_and_extra_files_are_rejected(self):
        for name in ("../escaped", "/absolute", "nested/file", "nested\\file", "extra.txt", "C:bad"):
            with self.subTest(name=name):
                self.assertEqual(400, self.publish(self.archive(extra=[(name, b"x")]))[0])
                self.assert_no_publication()

    def test_zip_duplicates_case_collisions_and_symlinks_are_rejected(self):
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            for name in ("world-scenes", "WORLD-SCENES"):
                self.assertEqual(400, self.publish(self.archive(extra=[(name, b"x")]))[0])
        link = zipfile.ZipInfo("symlink")
        link.create_system = 3
        link.external_attr = (stat.S_IFLNK | 0o777) << 16
        self.assertEqual(400, self.publish(self.archive(extra=[(link, b"../token")]))[0])
        self.assert_no_publication()

    def test_hash_size_and_missing_files_fail_without_partial_revision(self):
        for field, value in (("sha256", "0" * 64), ("sizeBytes", 999), ("fileName", "missing-bundle")):
            manifest = self.manifest()
            manifest["bundles"][0][field] = value
            self.assertEqual(400, self.publish(self.archive(manifest=manifest))[0])
            self.assert_no_publication()

    def test_identifiers_and_reserved_paths_are_rejected(self):
        for key, value in (("worldId", "../escape"), ("revisionId", "a" * 81), ("worldId", "CON"),
                           ("revisionId", "a/b"), ("worldId", "")):
            manifest = self.manifest()
            manifest[key] = value
            self.assertEqual(400, self.publish(self.archive(manifest=manifest))[0])
        self.assert_no_publication()

    def test_malformed_manifest_and_zip_are_rejected(self):
        for raw in (b"not json", b'{"schemaVersion":1,"schemaVersion":1}', b"[]", b"\xff"):
            self.assertEqual(400, self.publish(self.archive(raw=raw))[0])
        self.assertEqual(400, self.publish(b"not a zip file")[0])
        self.assert_no_publication()

    def test_http_and_expanded_size_limits(self):
        code, _, _ = self.request("/api/publish", b"x", headers={"Content-Length": str(server.MAX_UPLOAD + 1)})
        self.assertEqual(413, code)
        original = self.service.store.config
        self.service.store.config = server.Config(original.data_dir, original.base_url, original.token, max_expanded=1024)
        self.assertEqual(413, self.publish(self.archive(data=b"x" * 2048))[0])
        self.assert_no_publication()

    def test_file_reads_are_only_for_declared_public_files(self):
        self.publish()
        for path in ("/worlds/sample-world/revision-1/../token", "/worlds/sample-world/revision-1/%2e%2e%2ftoken",
                     "/worlds/sample-world/revision-1/token", "/worlds/sample-world/revision-1/world.json?file=token",
                     "/worlds/sample-world/revision-1/CON", "/.local/token"):
            self.assertEqual(404, self.request(path, authenticated=False)[0], path)

    def test_dependency_cycles_wrong_platform_and_missing_entry_are_rejected(self):
        manifests = []
        first = self.manifest(); first["bundles"][0]["dependencies"] = ["world-scenes"]; manifests.append(first)
        second = self.manifest(); second["platform"] = "StandaloneWindows64"; manifests.append(second)
        third = self.manifest(); third["entryScene"] = "not-declared"; manifests.append(third)
        for manifest in manifests:
            self.assertEqual(400, self.publish(self.archive(manifest=manifest))[0])
        self.assert_no_publication()

    def test_token_persists_without_overwriting_and_public_origin_validation(self):
        path = Path(self.directory.name) / "private-token"
        self.assertEqual(server.load_or_create_token(path), server.load_or_create_token(path))
        for value in ("file:///tmp", "http://user:secret@localhost", "http://localhost/path", "http://localhost?token=x"):
            with self.assertRaises(ValueError):
                server.public_base(value)

    def web_manifest(self, data=b"real downloadable bundle bytes", revision="webgl-revision-1"):
        manifest = self.manifest(data, revision)
        manifest["platform"] = "WebGL"
        manifest["unityVersion"] = "6000.0.65f1"
        return manifest

    def player_files(self, files=None):
        root = Path(self.directory.name) / "web-player"
        root.mkdir(exist_ok=True)
        (root / "index.html").write_bytes(b"<!doctype html><title>Test WebGL player</title>")
        for name, data in (files or {}).items():
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        self.service.store.config = replace(self.service.store.config, webplayer_dir=root)
        return root

    def web_publish(self, **kwargs):
        return self.publish(self.archive(manifest=self.web_manifest(**kwargs), data=kwargs.get("data", b"real downloadable bundle bytes")))

    def test_webgl_publish_launch_and_qr_preserve_exact_manifest_digest(self):
        self.player_files()
        raw = json.dumps(self.web_manifest(), indent=3).encode("utf-8")
        status, result = self.publish(self.archive(raw=raw))
        self.assertEqual(201, status)
        self.assertEqual(hashlib.sha256(raw).hexdigest(), result["manifestSha256"])
        launch = urllib.parse.urlsplit(result["launchUrl"])
        self.assertEqual(self.base + "/player/", urllib.parse.urlunsplit((launch.scheme, launch.netloc, launch.path, "", "")))
        self.assertEqual({"manifest": [result["manifestUrl"]], "sha256": [result["manifestSha256"]]},
                         urllib.parse.parse_qs(launch.query))
        code, page, headers = self.request(launch.path + "?" + launch.query, authenticated=False)
        self.assertEqual(200, code)
        self.assertIn(b"Test WebGL player", page)
        self.assertEqual("no-store", headers["Cache-Control"])
        code, downloaded, headers = self.request(urllib.parse.urlsplit(result["manifestUrl"]).path, authenticated=False)
        self.assertEqual((200, raw), (code, downloaded))
        self.assertIn("immutable", headers["Cache-Control"])
        import qrcode
        actual_add = qrcode.QRCode.add_data
        encoded = []
        def record_payload(qr, data, *args, **kwargs):
            encoded.append(data)
            return actual_add(qr, data, *args, **kwargs)
        with mock.patch.object(qrcode.QRCode, "add_data", record_payload):
            code, png, _ = self.request(urllib.parse.urlsplit(result["qrUrl"]).path, authenticated=False)
        self.assertEqual(200, code)
        self.assertTrue(png.startswith(b"\x89PNG\r\n\x1a\n"))
        self.assertEqual([result["launchUrl"]], encoded)
        code, landing, _ = self.request(urllib.parse.urlsplit(result["publishUrl"]).path, authenticated=False)
        self.assertEqual(200, code)
        self.assertIn("브라우저에서 월드 열기".encode(), landing)
        self.assertNotIn(b"kimchily://", landing)

    def test_webgl_revisions_remain_immutable_and_do_not_replace_android(self):
        self.assertEqual(201, self.publish()[0])
        self.assertEqual(201, self.web_publish()[0])
        code, error = self.web_publish(data=b"new WebGL bundle")
        self.assertEqual((409, "REVISION_EXISTS"), (code, error["code"]))
        # Platform is part of the immutable content, never an overwrite permission.
        code, error = self.web_publish(revision="revision-1")
        self.assertEqual((409, "REVISION_EXISTS"), (code, error["code"]))
        for revision, platform in (("revision-1", "Android"), ("webgl-revision-1", "WebGL")):
            code, raw, _ = self.request(f"/worlds/sample-world/{revision}/world.json")
            self.assertEqual(200, code)
            self.assertEqual(platform, json.loads(raw)["platform"])

    def test_webgl_bundle_hash_validation_is_not_bypassed(self):
        manifest = self.web_manifest()
        manifest["bundles"][0]["sha256"] = "0" * 64
        code, result = self.publish(self.archive(manifest=manifest))
        self.assertEqual((400, "HASH_MISMATCH"), (code, result["code"]))
        self.assert_no_publication()

    def test_web_player_serves_mime_compression_and_mutable_cache_policy(self):
        wasm = b"\x00asm\x01\x00\x00\x00"
        zipped = gzip.compress(wasm)
        brotli_fixture = b"\x0b\x01\x80wasm\x03"
        data = {"Build/game.wasm": wasm, "Build/game.wasm.gz": zipped,
                "Build/game.wasm.br": brotli_fixture, "Build/game.data": b"assets",
                "Build/game.loader.js": b"window.fixture = true;", "player.css": b"body{}",
                "host.js": b"// bridge", "Build/game.symbols.json": b"{}",
                "Build/game.wasm.unityweb": b"fallback-compressed-data"}
        self.player_files(data)
        expected = {".wasm": "application/wasm", ".gz": "application/wasm", ".br": "application/wasm",
                    ".data": "application/octet-stream", ".js": "application/javascript; charset=utf-8",
                    ".css": "text/css; charset=utf-8", ".json": "application/json; charset=utf-8",
                    ".unityweb": "application/octet-stream"}
        for name, content in data.items():
            with self.subTest(name=name):
                code, body, headers = self.request("/player/" + name, authenticated=False)
                self.assertEqual((200, content), (code, body))
                self.assertEqual(expected[Path(name).suffix], headers["Content-Type"])
                self.assertEqual(str(len(content)), headers["Content-Length"])
                self.assertEqual("no-store", headers["Cache-Control"])
                self.assertEqual("nosniff", headers["X-Content-Type-Options"])
                self.assertEqual("gzip" if name.endswith(".gz") else "br" if name.endswith(".br") else None,
                                 headers.get("Content-Encoding"))
        self.assertEqual(wasm, gzip.decompress(self.request("/player/Build/game.wasm.gz")[1]))
        self.assertEqual(200, self.request("/player", authenticated=False)[0])

    def test_web_player_rejects_paths_sources_and_directory_listing(self):
        self.player_files({"Build/game.wasm": b"wasm", "Secret.cs": b"source", "Config.asset": b"asset",
                           "Build/game.wasm.meta": b"metadata"})
        for path in ("/player/../token", "/player/%2e%2e/token", "/player/Build/../../token",
                     "/player/Build\\game.wasm", "/player//Build/game.wasm", "/player/Build/",
                     "/player/Secret.cs", "/player/Config.asset", "/player/Build/game.wasm.meta",
                     "/player/Build/game.wasm?manifest=bad", "/player/CON", "/player/missing.js"):
            with self.subTest(path=path):
                self.assertEqual(404, self.request(path, authenticated=False)[0])

    def test_web_launch_rejects_external_origin_bad_sha_duplicates_and_android(self):
        self.player_files()
        _, web = self.web_publish()
        _, android = self.publish()
        cases = [({"manifest": "https://example.invalid/worlds/a/b/world.json", "sha256": web["manifestSha256"]}, "INVALID_WEB_LAUNCH"),
                 ({"manifest": "http://[invalid", "sha256": web["manifestSha256"]}, "INVALID_WEB_LAUNCH"),
                 ({"manifest": web["manifestUrl"], "sha256": "0" * 64}, "HASH_MISMATCH"),
                 ({"manifest": web["manifestUrl"], "sha256": "bad"}, "INVALID_WEB_LAUNCH"),
                 ({"manifest": android["manifestUrl"], "sha256": android["manifestSha256"]}, "WRONG_PLATFORM"),
                 ({"manifest": web["manifestUrl"]}, "INVALID_WEB_LAUNCH")]
        for values, expected in cases:
            code, raw, _ = self.request("/player/?" + urllib.parse.urlencode(values), authenticated=False)
            self.assertEqual((400, expected), (code, json.loads(raw)["code"]))
        for suffix in ("&sha256=" + web["manifestSha256"], "&extra=x"):
            url = urllib.parse.urlsplit(web["launchUrl"])
            code, raw, _ = self.request(url.path + "?" + url.query + suffix, authenticated=False)
            self.assertEqual((400, "INVALID_WEB_LAUNCH"), (code, json.loads(raw)["code"]))

    def test_missing_web_player_is_explicit_without_disabling_publication(self):
        self.service.store.config = replace(self.service.store.config, webplayer_dir=Path(self.directory.name) / "missing-build")
        self.assertEqual(201, self.web_publish()[0])
        code, body, headers = self.request("/player/", authenticated=False)
        self.assertEqual((503, "WEB_PLAYER_MISSING"), (code, json.loads(body)["code"]))
        self.assertEqual("no-store", headers["Cache-Control"])

    def app_files(self):
        root = Path(self.directory.name) / "app"
        root.mkdir()
        for name in ("index.html", "manifest.webmanifest", "sw.js", *server.WEBAPP_FILES):
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(("fixture:" + name).encode())
        self.service.store.config = replace(self.service.store.config, webapp_dir=root)
        return root

    def test_pwa_shell_manifest_service_worker_and_explicit_assets_are_public(self):
        self.app_files()
        routes = {"/": ("index.html", "text/html; charset=utf-8"),
                  "/manifest.webmanifest": ("manifest.webmanifest", "application/manifest+json; charset=utf-8"),
                  "/sw.js": ("sw.js", "application/javascript; charset=utf-8")}
        routes.update({"/app/" + name: (name, mime) for name, mime in server.WEBAPP_FILES.items()})
        for route, (name, mime) in routes.items():
            with self.subTest(route=route):
                code, body, headers = self.request(route, authenticated=False)
                self.assertEqual((200, ("fixture:" + name).encode()), (code, body))
                self.assertEqual(mime, headers["Content-Type"])
                self.assertEqual("no-store", headers["Cache-Control"])
                self.assertEqual("/" if route == "/sw.js" else None, headers.get("Service-Worker-Allowed"))

    def test_pwa_never_serves_development_files_or_arbitrary_javascript(self):
        root = self.app_files()
        for name in ("README.md", "secrets.json", "private.js", "tests/app.test.js", "package.json"):
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("private data")
        for route in ("/app/README.md", "/app/secrets.json", "/app/private.js", "/app/tests/app.test.js",
                      "/app/package.json", "/app/../server.py", "/app/%2e%2e/server.py", "/app/",
                      "/app/icons/", "/app//app.js", "/app/app.js?file=private.js", "/sw.js?file=private.js"):
            self.assertEqual(404, self.request(route, authenticated=False)[0], route)

    def write_catalog_record(self, world, revision, timestamp, platform="WebGL", raw=None):
        manifest = self.web_manifest(revision=revision)
        manifest["worldId"] = world
        manifest["platform"] = platform
        path = self.service.store.worlds / world / revision / "world.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw if raw is not None else json.dumps(manifest).encode())
        os.utime(path, ns=(timestamp, timestamp))
        return path

    def test_catalog_uses_publication_time_skips_corrupt_and_android_and_returns_one_per_world(self):
        self.write_catalog_record("garden", "zz-older", 1_000_000_000)
        expected = self.write_catalog_record("garden", "aa-newer", 3_000_000_000)
        self.write_catalog_record("garden", "bad-newest", 5_000_000_000, raw=b"corrupt")
        self.write_catalog_record("garden", "android-newest", 6_000_000_000, platform="Android")
        self.write_catalog_record("beach", "revision", 4_000_000_000)
        self.write_catalog_record("android-only", "revision", 7_000_000_000, platform="Android")
        self.write_catalog_record("corrupt-only", "revision", 8_000_000_000, raw=b"[]")
        self.write_catalog_record("mismatched", "revision", 9_000_000_000,
                                  raw=json.dumps(self.web_manifest()).encode())
        code, body, headers = self.request("/api/worlds", authenticated=False)
        self.assertEqual(200, code)
        catalog = json.loads(body)
        self.assertEqual([self.base], catalog["linkOrigins"])
        worlds = catalog["worlds"]
        self.assertEqual(["beach", "garden"], [item["worldId"] for item in worlds])
        self.assertEqual("aa-newer", worlds[1]["revisionId"])
        self.assertEqual(hashlib.sha256(expected.read_bytes()).hexdigest(), worlds[1]["manifestSha256"])
        self.assertEqual("garden", worlds[1]["title"])
        self.assertEqual("no-store", headers["Cache-Control"])

    def test_catalog_limit_defaults_to_20_caps_at_100_and_rejects_ambiguous_input(self):
        for index in range(103):
            self.write_catalog_record(f"world-{index}", "revision", (index + 1) * 1_000_000_000)
        for query, count in (("", 20), ("?limit=2", 2), ("?limit=999", 100)):
            code, body, _ = self.request("/api/worlds" + query, authenticated=False)
            self.assertEqual(200, code)
            self.assertEqual(count, len(json.loads(body)["worlds"]))
            self.assertEqual("world-102", json.loads(body)["worlds"][0]["worldId"])
        for query in ("?limit=0", "?limit=-1", "?limit=x", "?limit=2&limit=3", "?other=1"):
            self.assertEqual(400, self.request("/api/worlds" + query, authenticated=False)[0])

    def resolve_link(self, url, headers=None):
        code, body, _ = self.request("/api/resolve?" + urllib.parse.urlencode({"url": url}),
                                     authenticated=False, headers=headers)
        return code, json.loads(body)

    def test_resolver_accepts_player_landing_and_legacy_webgl_links(self):
        _, published = self.web_publish()
        parsed = urllib.parse.urlsplit(published["launchUrl"])
        links = (published["launchUrl"], published["publishUrl"], parsed.path + "?" + parsed.query,
                 "kimchily://world?" + parsed.query)
        for link in links:
            with self.subTest(link=link):
                code, result = self.resolve_link(link)
                self.assertEqual(200, code)
                self.assertEqual({**published, "title": published["worldId"]}, result)

    def test_resolver_rejects_android_external_origin_hash_mismatch_and_unknown_revision(self):
        _, web = self.web_publish()
        _, android = self.publish()
        for link in (android["launchUrl"], android["publishUrl"]):
            code, error = self.resolve_link(link)
            self.assertEqual((400, "WRONG_PLATFORM"), (code, error["code"]))
        for link in (web["launchUrl"].replace(self.base, "https://unknown.example"),
                     web["publishUrl"].replace(self.base, "https://unknown.example"),
                     "file:///local/world.json", "//unknown.example/w/a/b", web["launchUrl"] + "&x=1"):
            self.assertEqual(400, self.resolve_link(link)[0], link)
        code, error = self.resolve_link(web["launchUrl"].replace(web["manifestSha256"], "0" * 64))
        self.assertEqual((400, "HASH_MISMATCH"), (code, error["code"]))
        self.assertEqual(404, self.resolve_link(self.base + "/w/missing/revision")[0])
        for path in ("/api/resolve", "/api/resolve?url=x&url=y", "/api/resolve?other=x"):
            self.assertEqual(400, self.request(path, authenticated=False)[0])

    def test_explicit_http_alias_resolves_to_https_but_player_and_host_header_cannot_change_origin(self):
        self.player_files()
        _, published = self.web_publish()
        tls_origin = "https://192.168.0.4:8789"
        self.service.store.config = replace(self.service.store.config, base_url=tls_origin, link_origins=(self.base,))
        for value in (published["launchUrl"], published["publishUrl"],
                      published["launchUrl"].replace(self.base + "/player/", "kimchily://world")):
            code, result = self.resolve_link(value, headers={"Host": "untrusted.example:1234"})
            self.assertEqual(200, code)
            self.assertTrue(result["launchUrl"].startswith(tls_origin + "/player/"))
            self.assertTrue(result["manifestUrl"].startswith(tls_origin + "/worlds/"))
            self.assertEqual(published["manifestSha256"], result["manifestSha256"])
        parsed = urllib.parse.urlsplit(published["launchUrl"])
        code, error, _ = self.request(parsed.path + "?" + parsed.query, authenticated=False)
        self.assertEqual((400, "INVALID_WEB_LAUNCH"), (code, json.loads(error)["code"]))
        code, body, _ = self.request("/api/worlds", authenticated=False, headers={"Host": "untrusted.example"})
        self.assertEqual(200, code)
        self.assertEqual([tls_origin, self.base], json.loads(body)["linkOrigins"])
        self.assertTrue(json.loads(body)["worlds"][0]["launchUrl"].startswith(tls_origin))
        self.assertEqual(400, self.resolve_link("https://untrusted.example/w/sample-world/webgl-revision-1")[0])

    def dev_files(self):
        root = self.service.store.config.data_dir / "tls"
        root.mkdir()
        certificate = b"public DER certificate fixture"
        (root / "ca.cer").write_bytes(certificate)
        public = {"httpsUrl": "https://192.168.0.4:8789", "caSha256": hashlib.sha256(certificate).hexdigest(),
                  "expiresAt": "2026-12-31T00:00:00Z", "lanAddress": "192.168.0.4"}
        (root / "public.json").write_text(json.dumps({**public, "privateKey": "not-public"}), encoding="utf-8")
        for name in ("ca-key.pem", "server-key.pem", "server.pem", "ca.pem"):
            (root / name).write_text("PRIVATE KEY FIXTURE")
        return root, certificate, public

    def test_dev_setup_exposes_only_matching_public_certificate_and_connection_fields(self):
        _, certificate, public = self.dev_files()
        code, body, headers = self.request("/dev/ca.cer", authenticated=False)
        self.assertEqual((200, certificate), (code, body))
        self.assertEqual("application/x-x509-ca-cert", headers["Content-Type"])
        self.assertEqual("no-store", headers["Cache-Control"])
        code, body, _ = self.request("/dev/connection", authenticated=False)
        self.assertEqual((200, public), (code, json.loads(body)))
        code, page, _ = self.request("/dev/setup", authenticated=False)
        self.assertEqual(200, code)
        self.assertIn(public["caSha256"].encode(), page)
        self.assertIn(public["httpsUrl"].encode(), page)
        self.assertIn(b'/dev/ca.cer', page)
        for route in ("/dev/ca-key.pem", "/dev/server-key.pem", "/dev/server.pem", "/dev/ca.pem",
                      "/dev/public.json", "/dev/tls/ca.cer", "/.local/tls/ca-key.pem", "/dev/ca.cer?file=ca-key.pem"):
            self.assertEqual(404, self.request(route, authenticated=False)[0], route)

    def test_dev_setup_missing_or_changed_certificate_is_explicit(self):
        self.assertEqual(404, self.request("/dev/setup", authenticated=False)[0])
        root, _, public = self.dev_files()
        (root / "ca.cer").write_bytes(b"a different certificate")
        code, body, _ = self.request("/dev/connection", authenticated=False)
        self.assertEqual((503, "DEV_SETUP_INVALID"), (code, json.loads(body)["code"]))

    def test_tls_certificate_and_key_must_be_paired_before_starting_a_service(self):
        for option in ("--tls-cert", "--tls-key"):
            arguments = ["server.py", "--public-base-url", "https://127.0.0.1:8789", option, "unused.pem"]
            with self.subTest(option=option), mock.patch.object(sys, "argv", arguments), mock.patch("sys.stderr", new=io.StringIO()):
                with self.assertRaises(SystemExit) as error:
                    server.main()
                self.assertEqual(2, error.exception.code)


if __name__ == "__main__":
    unittest.main()
