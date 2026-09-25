"""Local-development immutable world publisher. No Unity or cloud account required."""
from __future__ import annotations

import argparse
import hashlib
import hmac
import html
import io
import json
import os
import re
import secrets
import shutil
import socket
import ssl
import stat
import sys
import tempfile
import threading
import urllib.parse
import zipfile
import zlib
from dataclasses import dataclass
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

# Dependencies stay inside this project when installed with tools/install.ps1.
sys.path.insert(0, str(Path(__file__).resolve().parent / ".deps"))

MAX_UPLOAD = 256 * 1024 * 1024
MAX_MANIFEST = 1024 * 1024
MAX_BUNDLES = 64
DEFAULT_WEBPLAYER = Path(__file__).resolve().parent.parent / "KimchilyUnityRuntime" / "Builds" / "WebGL"
DEFAULT_WEBAPP = Path(__file__).resolve().parent.parent / "KimchilyWebApp"
# The app directory also contains tests and development files. Only this public
# shell is addressable; adding a source file never makes it public automatically.
WEBAPP_FILES = {
    "app.js": "application/javascript; charset=utf-8",
    "state.js": "application/javascript; charset=utf-8",
    "styles.css": "text/css; charset=utf-8",
    "qr.js": "application/javascript; charset=utf-8",
    "scanner.js": "application/javascript; charset=utf-8",
    "vendor/jsQR-1.4.0.js": "application/javascript; charset=utf-8",
    "vendor/jsQR-LICENSE.txt": "text/plain; charset=utf-8",
    "icons/icon.svg": "image/svg+xml",
    "icons/icon-192.png": "image/png",
    "icons/icon-512.png": "image/png",
    "icons/icon-maskable-512.png": "image/png",
    "icons/apple-touch-icon.png": "image/png",
}
WEB_TYPES = {
    ".html": "text/html; charset=utf-8", ".js": "application/javascript; charset=utf-8",
    ".wasm": "application/wasm", ".data": "application/octet-stream",
    ".json": "application/json; charset=utf-8", ".css": "text/css; charset=utf-8",
    ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".gif": "image/gif",
    ".ico": "image/x-icon", ".svg": "image/svg+xml", ".webp": "image/webp",
    ".woff": "font/woff", ".woff2": "font/woff2", ".ttf": "font/ttf",
    ".bin": "application/octet-stream", ".bundle": "application/octet-stream",
    ".unityweb": "application/octet-stream", ".mp3": "audio/mpeg", ".ogg": "audio/ogg",
    ".wav": "audio/wav", ".mp4": "video/mp4", ".webm": "video/webm",
}
ID = re.compile(r"[A-Za-z0-9_-]{1,80}\Z")
FILE_NAME = re.compile(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,127}\Z")
SHA256 = re.compile(r"[0-9a-fA-F]{64}\Z")
RESERVED = {"con", "prn", "aux", "nul", *(f"com{x}" for x in range(1, 10)), *(f"lpt{x}" for x in range(1, 10))}


class PublishError(Exception):
    def __init__(self, code: str, message: str, status: int = 400):
        self.code, self.message, self.status = code, message, status
        super().__init__(message)


def safe_id(value: object) -> bool:
    return isinstance(value, str) and ID.fullmatch(value) is not None and value.lower() not in RESERVED


def safe_file(value: object) -> bool:
    return (isinstance(value, str) and FILE_NAME.fullmatch(value) is not None
            and not value.endswith(".") and value.split(".")[0].lower() not in RESERVED)


def redirected(path: Path) -> bool:
    return path.is_symlink() or getattr(path, "is_junction", lambda: False)()


def exact_query(query: str, keys: set[str], code: str) -> dict[str, str]:
    try:
        values = urllib.parse.parse_qs(query, keep_blank_values=True, strict_parsing=True,
                                      max_num_fields=len(keys), errors="strict")
    except (ValueError, UnicodeError) as error:
        raise PublishError(code, "Missing, duplicate or invalid URL parameters.") from error
    if set(values) != keys or any(len(value) != 1 or not value[0] for value in values.values()):
        raise PublishError(code, "Missing, duplicate or invalid URL parameters.")
    return {key: value[0] for key, value in values.items()}


def public_base(value: str) -> str:
    parsed = urllib.parse.urlsplit(value)
    if (parsed.scheme not in ("http", "https") or not parsed.hostname or parsed.username or parsed.password
            or parsed.query or parsed.fragment or parsed.path not in ("", "/") or len(value) > 256
            or any(ord(char) < 33 for char in value)):
        raise ValueError("public-base-url must be an absolute HTTP(S) origin, without a path, credentials or query.")
    _ = parsed.port  # Validate malformed ports as well.
    return value.rstrip("/")


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise PublishError("INVALID_MANIFEST", "Duplicate JSON property.")
        result[key] = value
    return result


def read_manifest(raw: bytes, max_expanded: int = MAX_UPLOAD) -> dict:
    if not raw or len(raw) > MAX_MANIFEST:
        raise PublishError("INVALID_MANIFEST", "world.json is empty or exceeds 1 MiB.")
    try:
        manifest = json.loads(raw.decode("utf-8-sig"), object_pairs_hook=unique_object,
                              parse_constant=lambda _: (_ for _ in ()).throw(ValueError("Non-finite JSON number")))
    except (UnicodeError, ValueError, RecursionError) as error:
        raise PublishError("INVALID_MANIFEST", "world.json must contain valid UTF-8 JSON.") from error
    if not isinstance(manifest, dict) or type(manifest.get("schemaVersion")) is not int or manifest["schemaVersion"] != 1:
        raise PublishError("INVALID_MANIFEST", "Only world manifest schemaVersion 1 is supported.")
    if not safe_id(manifest.get("worldId")) or not safe_id(manifest.get("revisionId")):
        raise PublishError("INVALID_ID", "World and revision IDs must be safe identifiers of at most 80 characters.")
    for key in ("sdkVersion", "unityVersion", "platform", "renderPipeline", "entryScene"):
        if not isinstance(manifest.get(key), str) or not manifest[key].strip() or len(manifest[key]) > 512:
            raise PublishError("INVALID_MANIFEST", f"Missing or invalid {key}.")
    if manifest["platform"] not in ("Android", "WebGL"):
        raise PublishError("WRONG_PLATFORM", "Publish Android or WebGL content for its matching player.")
    scenes = manifest.get("scenes")
    if (not isinstance(scenes, list) or len(scenes) != 1
            or any(not isinstance(scene, str) or not scene.strip() or len(scene) > 512 for scene in scenes)
            or len({scene.casefold() for scene in scenes}) != len(scenes) or manifest["entryScene"] not in scenes):
        raise PublishError("INVALID_MANIFEST", "This publisher requires exactly one declared entry scene.")
    bundles = manifest.get("bundles")
    if not isinstance(bundles, list) or not 1 <= len(bundles) <= MAX_BUNDLES:
        raise PublishError("INVALID_MANIFEST", "Declare between 1 and 64 bundle files.")
    names, files, total = {}, set(), len(raw)
    for bundle in bundles:
        if not isinstance(bundle, dict):
            raise PublishError("INVALID_MANIFEST", "Invalid bundle record.")
        name, filename = bundle.get("name"), bundle.get("fileName")
        size, digest, dependencies = bundle.get("sizeBytes"), bundle.get("sha256"), bundle.get("dependencies")
        if (not safe_file(name) or not safe_file(filename) or filename.casefold() == "world.json"
                or type(size) is not int or not 0 <= size <= max_expanded
                or not isinstance(digest, str) or not SHA256.fullmatch(digest)
                or not isinstance(dependencies, list) or len(dependencies) > MAX_BUNDLES
                or any(not isinstance(dep, str) for dep in dependencies)
                or type(bundle.get("crc")) is not int or not 0 <= bundle["crc"] <= 0xFFFFFFFF
                or not isinstance(bundle.get("unityHash"), str) or not re.fullmatch(r"[0-9a-fA-F]{32}", bundle["unityHash"])):
            raise PublishError("INVALID_MANIFEST", "Invalid bundle name, size, hash or dependencies.")
        if name in names or filename.casefold() in files:
            raise PublishError("INVALID_MANIFEST", "Duplicate bundle name or file.")
        names[name] = bundle
        files.add(filename.casefold())
        total += size
    if total > max_expanded:
        raise PublishError("TOO_LARGE", "Expanded publication exceeds its size limit.", 413)
    visiting, visited = set(), set()

    def visit(name):
        if name in visiting:
            raise PublishError("INVALID_MANIFEST", "Cyclic bundle dependencies.")
        if name in visited:
            return
        if name not in names:
            raise PublishError("INVALID_MANIFEST", "Missing bundle dependency.")
        visiting.add(name)
        for dependency in names[name]["dependencies"]:
            visit(dependency)
        visiting.remove(name)
        visited.add(name)

    for name in names:
        visit(name)
    requirements = manifest.get("requiredTypes")
    if (not isinstance(requirements, list) or len(requirements) > 4096
            or any(not isinstance(item, dict) or any(not isinstance(item.get(key), str)
                    or not item[key].strip() or len(item[key]) > 512 for key in ("assembly", "type")) for item in requirements)):
        raise PublishError("INVALID_MANIFEST", "Invalid requiredTypes list.")
    return manifest


@dataclass(frozen=True)
class Config:
    data_dir: Path
    base_url: str
    token: str
    max_upload: int = MAX_UPLOAD
    max_expanded: int = MAX_UPLOAD
    webplayer_dir: Path = DEFAULT_WEBPLAYER
    webapp_dir: Path = DEFAULT_WEBAPP
    link_origins: tuple[str, ...] = ()


class WorldStore:
    def __init__(self, config: Config):
        self.config = config
        self.root = config.data_dir.resolve()
        self.worlds = self.root / "worlds"
        self.staging = self.root / "staging"
        self.worlds.mkdir(parents=True, exist_ok=True)
        self.staging.mkdir(parents=True, exist_ok=True)
        self.lock = threading.Lock()

    def publish(self, archive_path: Path) -> dict:
        stage = Path(tempfile.mkdtemp(prefix="publish-", dir=self.staging))
        try:
            with zipfile.ZipFile(archive_path, "r") as archive:
                entries = archive.infolist()
                if not 2 <= len(entries) <= MAX_BUNDLES + 1:
                    raise PublishError("INVALID_ZIP", "ZIP must contain only world.json and its declared bundle files.")
                lookup, folded, expanded = {}, set(), 0
                for entry in entries:
                    mode = stat.S_IFMT(entry.external_attr >> 16)
                    if (entry.orig_filename != entry.filename or not safe_file(entry.filename)
                            or entry.is_dir() or entry.external_attr & 0x10 or mode not in (0, stat.S_IFREG)
                            or entry.flag_bits & 1 or entry.compress_type not in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED)):
                        raise PublishError("INVALID_ZIP", "ZIP entries must be flat, regular, unencrypted files.")
                    if entry.filename.casefold() in folded:
                        raise PublishError("INVALID_ZIP", "Duplicate or case-colliding ZIP entries.")
                    folded.add(entry.filename.casefold())
                    lookup[entry.filename] = entry
                    expanded += entry.file_size
                    if entry.file_size < 0 or expanded > self.config.max_expanded:
                        raise PublishError("TOO_LARGE", "Expanded ZIP exceeds its size limit.", 413)
                if "world.json" not in lookup or lookup["world.json"].file_size > MAX_MANIFEST:
                    raise PublishError("INVALID_MANIFEST", "ZIP requires a world.json of at most 1 MiB.")
                with archive.open(lookup["world.json"]) as source:
                    raw = source.read(MAX_MANIFEST + 1)
                manifest = read_manifest(raw, self.config.max_expanded)
                expected = {"world.json", *(bundle["fileName"] for bundle in manifest["bundles"])}
                if set(lookup) != expected:
                    raise PublishError("INVALID_ZIP", "ZIP files differ from the declared bundle list.")
                (stage / "world.json").write_bytes(raw)
                for bundle in manifest["bundles"]:
                    filename, expected_size = bundle["fileName"], bundle["sizeBytes"]
                    if lookup[filename].file_size != expected_size:
                        raise PublishError("SIZE_MISMATCH", "A bundle size differs from its manifest.")
                    actual_size, digest = 0, hashlib.sha256()
                    with archive.open(lookup[filename]) as source, (stage / filename).open("xb") as target:
                        while True:
                            block = source.read(min(1024 * 1024, expected_size - actual_size + 1))
                            if not block:
                                break
                            actual_size += len(block)
                            if actual_size > expected_size:
                                raise PublishError("SIZE_MISMATCH", "A bundle expanded beyond its declared size.")
                            digest.update(block)
                            target.write(block)
                    if actual_size != expected_size or not hmac.compare_digest(digest.hexdigest(), bundle["sha256"].lower()):
                        raise PublishError("HASH_MISMATCH", "A bundle size or SHA-256 differs from its manifest.")
            world, revision = manifest["worldId"], manifest["revisionId"]
            destination = self.worlds / world / revision
            # A completed revision becomes visible with one directory rename.
            # Failed uploads never create a publicly addressable revision.
            with self.lock:
                destination.parent.mkdir(parents=True, exist_ok=True)
                if destination.exists():
                    raise PublishError("REVISION_EXISTS", "This world revision is immutable; build a new revision.", 409)
                # The catalog orders completed publications, not revision names or
                # upload start times. Set the marker timestamp at atomic commit.
                os.utime(stage / "world.json", None)
                os.rename(stage, destination)
            return self.links(world, revision, hashlib.sha256(raw).hexdigest(), manifest["platform"])
        except (zipfile.BadZipFile, zipfile.LargeZipFile, EOFError, RuntimeError, zlib.error) as error:
            raise PublishError("INVALID_ZIP", "Invalid, unsupported or damaged ZIP archive.") from error
        finally:
            if stage.exists():
                shutil.rmtree(stage)

    def revision(self, world: str, revision: str):
        if not safe_id(world) or not safe_id(revision):
            raise PublishError("NOT_FOUND", "World revision not found.", 404)
        folder = self.worlds / world / revision
        marker = folder / "world.json"
        if not marker.is_file() or any(redirected(path) for path in (marker, folder, folder.parent)):
            raise PublishError("NOT_FOUND", "World revision not found.", 404)
        with marker.open("rb") as source:
            raw = source.read(MAX_MANIFEST + 1)
        manifest = read_manifest(raw, self.config.max_expanded)
        if manifest["worldId"] != world or manifest["revisionId"] != revision:
            raise PublishError("INVALID_MANIFEST", "The manifest does not match its revision directory.")
        return folder, manifest, raw

    def links(self, world: str, revision: str, digest: str, platform: str = "Android"):
        manifest_url = f"{self.config.base_url}/worlds/{world}/{revision}/world.json"
        launch_base = f"{self.config.base_url}/player/?" if platform == "WebGL" else "kimchily://world?"
        launch_url = launch_base + urllib.parse.urlencode({"manifest": manifest_url, "sha256": digest})
        return {"worldId": world, "revisionId": revision, "manifestUrl": manifest_url,
                "manifestSha256": digest, "publishUrl": f"{self.config.base_url}/w/{world}/{revision}",
                "qrUrl": f"{self.config.base_url}/qr/{world}/{revision}.png", "launchUrl": launch_url}

    def web_world(self, world: str, revision: str, expected_hash: str | None = None) -> dict:
        _, manifest, raw = self.revision(world, revision)
        if manifest["platform"] != "WebGL":
            raise PublishError("WRONG_PLATFORM", "Open Android content with the Kimchily Android app.")
        digest = hashlib.sha256(raw).hexdigest()
        if expected_hash is not None and not hmac.compare_digest(digest, expected_hash.lower()):
            raise PublishError("HASH_MISMATCH", "The manifest does not match the SHA-256 in this launch link.")
        result = self.links(world, revision, digest, "WebGL")
        title = manifest.get("title")
        result["title"] = title.strip() if isinstance(title, str) and 0 < len(title.strip()) <= 120 else world
        return result

    def catalog(self, limit: int = 20) -> list[dict]:
        newest = []
        for world_dir in self.worlds.iterdir():
            if not safe_id(world_dir.name) or not world_dir.is_dir() or redirected(world_dir):
                continue
            candidates = []
            try:
                for revision_dir in world_dir.iterdir():
                    if not safe_id(revision_dir.name) or not revision_dir.is_dir() or redirected(revision_dir):
                        continue
                    try:
                        timestamp = (revision_dir / "world.json").stat().st_mtime_ns
                        candidates.append((timestamp, revision_dir.name))
                    except OSError:
                        continue
            except OSError:
                continue
            for timestamp, revision in sorted(candidates, reverse=True):
                try:
                    item = self.web_world(world_dir.name, revision)
                except (PublishError, OSError, ValueError, RecursionError):
                    continue  # A corrupt/newer/Android record cannot hide a valid Web revision.
                newest.append((timestamp, world_dir.name, revision, item))
                break
        newest.sort(key=lambda item: item[:3], reverse=True)
        return [item[3] for item in newest[:min(100, max(1, limit))]]

    def same_origin(self, address, allow_aliases=False) -> bool:
        origins = (self.config.base_url, *self.config.link_origins) if allow_aliases else (self.config.base_url,)
        try:
            if address.scheme not in ("http", "https") or address.username or address.password:
                return False
            for value in origins:
                origin = urllib.parse.urlsplit(value)
                if (address.scheme == origin.scheme and address.hostname == origin.hostname
                        and (address.port or (443 if address.scheme == "https" else 80))
                        == (origin.port or (443 if origin.scheme == "https" else 80))):
                    return True
            return False
        except ValueError:
            return False

    def manifest_link(self, query: str, allow_aliases=False) -> dict:
        values = exact_query(query, {"manifest", "sha256"}, "INVALID_WEB_LAUNCH")
        digest = values["sha256"]
        if not SHA256.fullmatch(digest):
            raise PublishError("INVALID_WEB_LAUNCH", "The manifest SHA-256 must contain 64 hexadecimal characters.")
        try:
            address = urllib.parse.urlsplit(values["manifest"])
        except ValueError as error:
            raise PublishError("INVALID_WEB_LAUNCH", "The manifest URL is invalid.") from error
        parts = address.path.split("/")
        if (not self.same_origin(address, allow_aliases) or address.query or address.fragment
                or len(parts) != 5 or parts[1] != "worlds" or parts[4] != "world.json"
                or not safe_id(parts[2]) or not safe_id(parts[3])):
            raise PublishError("INVALID_WEB_LAUNCH", "The manifest must be a revision on this publisher origin.")
        return self.web_world(parts[2], parts[3], digest)

    def resolve(self, value: str) -> dict:
        if not isinstance(value, str) or not value.strip() or len(value) > 4096:
            raise PublishError("INVALID_WEB_LAUNCH", "Enter a published world URL or QR link.")
        value = value.strip()
        if any(ord(char) < 33 for char in value) or "\\" in value:
            raise PublishError("INVALID_WEB_LAUNCH", "The world URL is invalid.")
        if value.startswith("/") and not value.startswith("//"):
            value = self.config.base_url + value
        try:
            address = urllib.parse.urlsplit(value)
        except ValueError as error:
            raise PublishError("INVALID_WEB_LAUNCH", "The world URL is invalid.") from error
        if address.fragment or "%" in address.path:
            raise PublishError("INVALID_WEB_LAUNCH", "The world URL is invalid.")
        if address.scheme == "kimchily" and address.netloc == "world" and address.path in ("", "/"):
            return self.manifest_link(address.query, allow_aliases=True)
        if not self.same_origin(address, allow_aliases=True):
            raise PublishError("INVALID_WEB_LAUNCH", "Only worlds from this publisher origin can be opened.")
        if address.path in ("/player/", "/player"):
            return self.manifest_link(address.query, allow_aliases=True)
        parts = address.path.split("/")
        if len(parts) == 4 and parts[1] == "w" and not address.query and safe_id(parts[2]) and safe_id(parts[3]):
            return self.web_world(parts[2], parts[3])
        raise PublishError("INVALID_WEB_LAUNCH", "Expected a published world page or player link.")


class PublisherServer(ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = True

    def __init__(self, address, config: Config):
        self.store = WorldStore(config)
        self.slots = threading.BoundedSemaphore(8)
        super().__init__(address, PublisherHandler)

    def process_request(self, request, client_address):
        if not self.slots.acquire(blocking=False):
            try:
                request.sendall(b"HTTP/1.0 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n")
            finally:
                self.shutdown_request(request)
            return
        super().process_request(request, client_address)

    def process_request_thread(self, request, client_address):
        try:
            request.settimeout(30)
            super().process_request_thread(request, client_address)
        finally:
            self.slots.release()


class PublisherHandler(BaseHTTPRequestHandler):
    server_version = "KimchilyPublish/0.1"

    def log_message(self, format, *args):
        # Avoid logging authentication headers, uploaded manifests, or private filesystem paths.
        pass

    def respond(self, status: int, data: bytes, content_type: str, immutable=False):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Cache-Control", "public, max-age=31536000, immutable" if immutable else "no-store")
        self.end_headers()
        self.wfile.write(data)

    def json(self, status, data):
        self.respond(status, json.dumps(data, ensure_ascii=False).encode("utf-8"), "application/json; charset=utf-8")

    def problem(self, error):
        self.json(error.status, {"code": error.code, "message": error.message})

    def serve_file(self, path: Path, content_type: str, immutable=False, encoding=None, service_worker=False):
        # Open before headers so a missing/replaced file produces a real error status.
        with path.open("rb") as source:
            self.send_response(200)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(os.fstat(source.fileno()).st_size))
            self.send_header("Cache-Control", "public, max-age=31536000, immutable" if immutable else "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            if encoding:
                self.send_header("Content-Encoding", encoding)
            if service_worker:
                self.send_header("Service-Worker-Allowed", "/")
            self.end_headers()
            shutil.copyfileobj(source, self.wfile, 1024 * 1024)

    def validate_web_launch(self, query: str):
        if not query:
            return  # Allow the player shell to present its own launch instructions.
        self.server.store.manifest_link(query)

    def serve_app(self, request_path: str):
        special = {"/": ("index.html", "text/html; charset=utf-8"),
                   "/manifest.webmanifest": ("manifest.webmanifest", "application/manifest+json; charset=utf-8"),
                   "/sw.js": ("sw.js", "application/javascript; charset=utf-8")}
        if request_path in special:
            relative, content_type = special[request_path]
        else:
            relative = request_path[len("/app/"):]
            content_type = WEBAPP_FILES.get(relative)
            if content_type is None:
                raise PublishError("NOT_FOUND", "App resource not found.", 404)
        configured = self.server.store.config.webapp_dir
        if not configured.is_dir():
            raise PublishError("WEB_APP_MISSING", "The Kimchily web app has not been installed.", 503)
        root = configured.resolve()
        parts = relative.split("/")
        candidate = root.joinpath(*parts)
        for path in (configured, *(root.joinpath(*parts[:index]) for index in range(1, len(parts) + 1))):
            if redirected(path):
                raise PublishError("NOT_FOUND", "App resource not found.", 404)
        if not candidate.is_file() or not candidate.resolve().is_relative_to(root):
            raise PublishError("NOT_FOUND", "App resource not found.", 404)
        self.serve_file(candidate, content_type, service_worker=request_path == "/sw.js")

    def dev_connection(self):
        root = self.server.store.config.data_dir.resolve() / "tls"
        certificate, metadata = root / "ca.cer", root / "public.json"
        if (not certificate.is_file() or not metadata.is_file()
                or any(redirected(path) for path in (root, certificate, metadata))):
            raise PublishError("DEV_SETUP_MISSING", "Create this clone's local HTTPS certificate first.", 404)
        try:
            if certificate.stat().st_size > 256 * 1024 or metadata.stat().st_size > 16 * 1024:
                raise ValueError("Invalid setup file size")
            values = json.loads(metadata.read_text(encoding="utf-8-sig"))
            https_url = public_base(values["httpsUrl"])
            fingerprint = values["caSha256"]
            if (not https_url.startswith("https://") or not isinstance(fingerprint, str)
                    or not SHA256.fullmatch(fingerprint)
                    or not hmac.compare_digest(hashlib.sha256(certificate.read_bytes()).hexdigest(), fingerprint.lower())
                    or any(not isinstance(values.get(key), str) or not values[key]
                           or len(values[key]) > 128 or any(ord(char) < 32 for char in values[key])
                           for key in ("expiresAt", "lanAddress"))):
                raise ValueError("Invalid public HTTPS configuration")
        except (KeyError, TypeError, ValueError, UnicodeError) as error:
            raise PublishError("DEV_SETUP_INVALID", "Recreate the local HTTPS certificate and public connection information.", 503) from error
        # Reconstruct the public record: never return private paths, keys or any
        # additional configuration properties that may be added in the future.
        return certificate, {"httpsUrl": https_url, "caSha256": fingerprint.lower(),
                             "expiresAt": values["expiresAt"], "lanAddress": values["lanAddress"]}

    def serve_dev(self, route: str):
        certificate, connection = self.dev_connection()
        if route == "/dev/ca.cer":
            self.serve_file(certificate, "application/x-x509-ca-cert")
        elif route == "/dev/connection":
            self.json(200, connection)
        else:
            url, fingerprint, expires = (html.escape(connection[key], quote=True)
                                         for key in ("httpsUrl", "caSha256", "expiresAt"))
            page = f'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Kimchily 기기 연결</title><style>body{{font:16px system-ui;margin:24px auto;padding:0 20px;max-width:620px;line-height:1.7}}code{{overflow-wrap:anywhere}}a{{display:inline-block;margin:8px 0}}li{{margin:12px 0}}</style>
<h1>Kimchily 개발 서버 연결</h1><p>이 PC의 개발 인증서를 휴대전화에 설치하면 HTTPS에서 QR 카메라와 홈 화면 앱을 사용할 수 있습니다.</p>
<p>PC에 표시된 CA 지문과 아래 값이 같은지 확인하세요.</p><code>{fingerprint}</code><p>만료: {expires}</p>
<ol><li><a href="/dev/ca.cer">개발 CA 인증서 다운로드</a></li>
<li>iPhone/iPad: 설정에서 '프로파일이 다운로드됨'을 열어 설치합니다. 이어서 설정 → 일반 → 정보 → 인증서 신뢰 설정에서 이 개발 CA에 대한 완전 신뢰를 켭니다. <a href="https://support.apple.com/ko-kr/102390">Apple 인증서 신뢰 안내</a></li>
<li>Android: 설정의 인증서 설치에서 CA 인증서를 선택합니다. 메뉴 이름은 제조사에 따라 다를 수 있습니다.</li>
<li><a href="{url}">HTTPS Kimchily 홈 열기</a></li></ol>
<p>개발이 끝나면 설치한 개발 CA를 기기 설정에서 제거할 수 있습니다.</p></html>'''.encode("utf-8")
            self.respond(200, page, "text/html; charset=utf-8")

    def serve_player(self, parsed):
        relative = parsed.path[len("/player/"):]
        if not relative or relative == "index.html":
            self.validate_web_launch(parsed.query)
            relative = "index.html"
        elif parsed.query:
            raise PublishError("NOT_FOUND", "Player resource not found.", 404)
        parts = relative.split("/")
        if len(parts) > 16 or any(not safe_file(part) for part in parts):
            raise PublishError("NOT_FOUND", "Player resource not found.", 404)
        configured = self.server.store.config.webplayer_dir
        if not configured.is_dir() or not (configured / "index.html").is_file():
            raise PublishError("WEB_PLAYER_MISSING", "Build the WebGL player before opening it in a browser.", 503)
        root = configured.resolve()
        candidate = root.joinpath(*parts)
        # Block symlinks/junctions at every level, including the configured root.
        for path in (configured, *(root.joinpath(*parts[:index]) for index in range(1, len(parts) + 1))):
            if path.is_symlink() or getattr(path, "is_junction", lambda: False)():
                raise PublishError("NOT_FOUND", "Player resource not found.", 404)
        if not candidate.is_file() or not candidate.resolve().is_relative_to(root):
            raise PublishError("NOT_FOUND", "Player resource not found.", 404)
        filename = candidate.name.lower()
        encoding = None
        if filename.endswith((".gz", ".br")):
            suffix = Path(filename).suffix
            encoding = "gzip" if suffix == ".gz" else "br"
            filename = filename[:-len(suffix)]
        content_type = WEB_TYPES.get(Path(filename).suffix)
        if content_type is None:
            raise PublishError("NOT_FOUND", "Player resource not found.", 404)
        # This mutable development build is never immutable. World revision files
        # retain their independent immutable URLs. Precompressed bytes pass through.
        self.serve_file(candidate, content_type, encoding=encoding)

    def do_POST(self):
        upload_path = None
        try:
            if self.path != "/api/publish":
                raise PublishError("NOT_FOUND", "Endpoint not found.", 404)
            expected = ("Bearer " + self.server.store.config.token).encode("utf-8")
            supplied = self.headers.get("Authorization", "").encode("utf-8")
            if not hmac.compare_digest(expected, supplied):
                raise PublishError("UNAUTHORIZED", "A valid local publisher bearer token is required.", 401)
            if self.headers.get("Transfer-Encoding"):
                raise PublishError("INVALID_REQUEST", "Use a fixed Content-Length upload.")
            lengths = self.headers.get_all("Content-Length", [])
            if len(lengths) != 1 or not re.fullmatch(r"[0-9]{1,12}", lengths[0]):
                raise PublishError("LENGTH_REQUIRED", "A single valid Content-Length is required.", 411)
            length = int(lengths[0])
            if not 0 < length <= self.server.store.config.max_upload:
                raise PublishError("TOO_LARGE", "Upload exceeds the 256 MiB limit or is empty.", 413)
            if self.headers.get_content_type() != "application/zip":
                raise PublishError("INVALID_CONTENT_TYPE", "Upload application/zip.", 415)
            with tempfile.NamedTemporaryFile(dir=self.server.store.staging, prefix="upload-", suffix=".zip", delete=False) as target:
                upload_path = Path(target.name)
                remaining = length
                while remaining:
                    block = self.rfile.read(min(1024 * 1024, remaining))
                    if not block:
                        raise PublishError("INCOMPLETE_UPLOAD", "Upload ended before Content-Length.")
                    target.write(block)
                    remaining -= len(block)
            result = self.server.store.publish(upload_path)
            self.json(201, result)
        except PublishError as error:
            self.problem(error)
        except (socket.timeout, TimeoutError):
            self.problem(PublishError("UPLOAD_TIMEOUT", "Upload timed out.", 408))
        except (OSError, ValueError, RecursionError):
            self.problem(PublishError("PUBLISH_FAILED", "Publication could not be completed.", 500))
        finally:
            if upload_path is not None:
                upload_path.unlink(missing_ok=True)

    def do_GET(self):
        try:
            if self.path == "/health":
                self.json(200, {"status": "ok", "service": "Kimchily local publisher", "schemaVersion": 1})
                return
            parsed = urllib.parse.urlsplit(self.path)
            if parsed.scheme or parsed.netloc or parsed.fragment or "%" in parsed.path or "\\" in parsed.path:
                raise PublishError("NOT_FOUND", "Resource not found.", 404)
            if parsed.path in ("/dev/ca.cer", "/dev/connection", "/dev/setup"):
                if parsed.query:
                    raise PublishError("NOT_FOUND", "Resource not found.", 404)
                self.serve_dev(parsed.path)
                return
            if parsed.path == "/api/worlds":
                limit = 20
                if parsed.query:
                    value = exact_query(parsed.query, {"limit"}, "INVALID_REQUEST")["limit"]
                    if not re.fullmatch(r"[0-9]{1,6}", value) or int(value) < 1:
                        raise PublishError("INVALID_REQUEST", "The world limit must be a positive integer, capped at 100.")
                    limit = min(100, int(value))
                config = self.server.store.config
                self.json(200, {"worlds": self.server.store.catalog(limit),
                                "linkOrigins": list(dict.fromkeys((config.base_url, *config.link_origins)))})
                return
            if parsed.path == "/api/resolve":
                value = exact_query(parsed.query, {"url"}, "INVALID_REQUEST")["url"]
                self.json(200, self.server.store.resolve(value))
                return
            if parsed.path in ("/", "/manifest.webmanifest", "/sw.js") or parsed.path.startswith("/app/"):
                if parsed.query:
                    raise PublishError("NOT_FOUND", "App resource not found.", 404)
                self.serve_app(parsed.path)
                return
            if parsed.path == "/player":
                self.validate_web_launch(parsed.query)
                self.send_response(307)
                self.send_header("Location", "/player/" + ("?" + parsed.query if parsed.query else ""))
                self.send_header("Content-Length", "0")
                self.send_header("Cache-Control", "no-store")
                self.end_headers()
                return
            if parsed.path.startswith("/player/"):
                self.serve_player(parsed)
                return
            if parsed.query:
                raise PublishError("NOT_FOUND", "Resource not found.", 404)
            parts = parsed.path.split("/")
            if len(parts) == 5 and parts[1] == "worlds":
                _, _, world, revision, filename = parts
                folder, manifest, raw = self.server.store.revision(world, revision)
                allowed = {"world.json", *(item["fileName"] for item in manifest["bundles"])}
                if not safe_file(filename) or filename not in allowed:
                    raise PublishError("NOT_FOUND", "File not found.", 404)
                path = folder / filename
                if not path.is_file() or path.is_symlink():
                    raise PublishError("NOT_FOUND", "File not found.", 404)
                self.serve_file(path, "application/json; charset=utf-8" if filename == "world.json" else "application/octet-stream", immutable=True)
                return
            if len(parts) == 4 and parts[1] in ("w", "qr"):
                _, route, world, revision = parts
                if route == "qr":
                    if not revision.endswith(".png"):
                        raise PublishError("NOT_FOUND", "QR not found.", 404)
                    revision = revision[:-4]
                _, manifest, raw = self.server.store.revision(world, revision)
                links = self.server.store.links(world, revision, hashlib.sha256(raw).hexdigest(), manifest["platform"])
                if route == "qr":
                    import qrcode
                    # Automatic masks can create false finder candidates in ZXing 3.5.3.
                    # Mask 0 passed the published-link size/rotation regression corpus.
                    qr = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M,
                                       box_size=8, border=4, mask_pattern=0)
                    qr.add_data(links["launchUrl"])
                    qr.make(fit=True)
                    output = io.BytesIO()
                    qr.make_image(fill_color="black", back_color="white").save(output, format="PNG")
                    self.respond(200, output.getvalue(), "image/png", immutable=True)
                else:
                    self.respond(200, landing(links), "text/html; charset=utf-8", immutable=True)
                return
            raise PublishError("NOT_FOUND", "Resource not found.", 404)
        except PublishError as error:
            self.problem(error)
        except ImportError:
            self.problem(PublishError("QR_DEPENDENCY_MISSING", "Install the pinned publisher dependencies to create QR images.", 503))
        except (BrokenPipeError, ConnectionResetError):
            pass
        except (OSError, ValueError):
            self.problem(PublishError("READ_FAILED", "Resource could not be read.", 500))


def landing(links: dict) -> bytes:
    world, revision, launch, qr, manifest = (html.escape(links[key], quote=True)
        for key in ("worldId", "revisionId", "launchUrl", "qrUrl", "manifestUrl"))
    web = links["launchUrl"].startswith(("http://", "https://"))
    action = "브라우저에서 월드 열기" if web else "Kimchily 앱에서 열기"
    explanation = "QR을 스캔하거나 아래 버튼으로 브라우저에서 월드를 열어 보세요." if web else "QR을 스캔하거나 아래 버튼으로 Kimchily 앱에서 월드를 열어 보세요."
    requirement = "별도 앱 설치 없이 실행됩니다. 브라우저에서 게시 서버에 접속할 수 있어야 합니다." if web else "앱이 설치되어 있어야 하며, 휴대전화에서 게시 서버에 접속할 수 있어야 합니다."
    return f'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>{world} · Kimchily</title><style>body{{font:16px system-ui;background:#edf5ef;color:#163530;margin:0;padding:32px 20px}}main{{max-width:540px;margin:auto;background:white;padding:32px;border-radius:24px}}h1{{overflow-wrap:anywhere}}p{{line-height:1.7}}img{{width:100%;max-width:320px;height:auto;display:block;margin:20px auto}}a.button{{display:block;background:#117b68;color:white;padding:16px;border-radius:12px;text-align:center;text-decoration:none}}small{{display:block;color:#65736d;overflow-wrap:anywhere}}</style>
<main><small>KIMCHILY WORLD</small><h1>{world}</h1><p>{explanation}</p>
<img src="{qr}" alt="{action} QR 코드"><a class="button" href="{launch}">{action}</a>
<p>{requirement}</p><small>Revision: {revision}</small>
<p><a href="{manifest}">월드 정보 보기</a></p></main></html>'''.encode("utf-8")


def load_or_create_token(path: Path) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError:
        value = path.read_text(encoding="utf-8").strip()
    else:
        value = secrets.token_urlsafe(32)
        with os.fdopen(fd, "w", encoding="utf-8") as target:
            target.write(value + "\n")
    if not re.fullmatch(r"[A-Za-z0-9_-]{32,128}", value):
        raise ValueError("The local token file has an invalid format.")
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", default=8788, type=int)
    parser.add_argument("--public-base-url", required=True, type=public_base,
                        help="Origin reachable by the browser/phone, e.g. http://192.168.0.20:8788")
    parser.add_argument("--data-dir", type=Path, default=Path(__file__).resolve().parent / ".local")
    parser.add_argument("--webplayer-dir", type=Path, default=DEFAULT_WEBPLAYER,
                        help="WebGL build root containing index.html (served under /player/).")
    parser.add_argument("--webapp-dir", type=Path, default=DEFAULT_WEBAPP,
                        help="Kimchily web app source root; only the public file allowlist is served.")
    parser.add_argument("--link-origin", action="append", default=[], type=public_base,
                        help="Additional trusted QR origin accepted only by /api/resolve; repeatable.")
    parser.add_argument("--tls-cert", type=Path, help="PEM certificate chain for HTTPS.")
    parser.add_argument("--tls-key", type=Path, help="PEM private key for HTTPS.")
    arguments = parser.parse_args()
    if bool(arguments.tls_cert) != bool(arguments.tls_key):
        parser.error("--tls-cert and --tls-key must be supplied together.")
    # Fail before serving/publishing if a real QR cannot be generated.
    import qrcode
    from PIL import Image
    token = load_or_create_token(arguments.data_dir / "token")
    config = Config(arguments.data_dir, arguments.public_base_url, token, webplayer_dir=arguments.webplayer_dir,
                    webapp_dir=arguments.webapp_dir, link_origins=tuple(dict.fromkeys(arguments.link_origin)))
    server = PublisherServer((arguments.host, arguments.port), config)
    if arguments.tls_cert:
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        context.minimum_version = ssl.TLSVersion.TLSv1_2
        context.load_cert_chain(str(arguments.tls_cert), str(arguments.tls_key))
        server.socket = context.wrap_socket(server.socket, server_side=True)
    print(f"Kimchily local publisher: {arguments.public_base_url}", flush=True)
    print("Local bearer token is stored in the configured data directory; it is never logged.", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
