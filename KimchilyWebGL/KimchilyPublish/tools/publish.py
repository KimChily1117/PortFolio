"""Publish an existing Android or WebGL build without exposing the local bearer token."""
import argparse
import hashlib
import http.client
import json
import sys
import tempfile
import urllib.parse
import zipfile
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT))
from server import MAX_UPLOAD, public_base, read_manifest


def publish(build_dir: Path, origin: str, token_file: Path) -> dict:
    build_dir = build_dir.resolve()
    raw = (build_dir / "world.json").read_bytes()
    manifest = read_manifest(raw)
    token = token_file.read_text(encoding="utf-8").strip()
    if not token or "\r" in token or "\n" in token:
        raise ValueError("Invalid local token file.")
    with tempfile.TemporaryDirectory(prefix="kimchily-publish-") as temporary:
        archive_path = Path(temporary) / "world.zip"
        with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
            archive.writestr("world.json", raw)
            for bundle in manifest["bundles"]:
                path = build_dir / bundle["fileName"]
                if not path.is_file() or path.is_symlink() or path.resolve().parent != build_dir:
                    raise ValueError("A declared bundle is missing or is not a regular build file.")
                digest = hashlib.sha256()
                with path.open("rb") as source:
                    while block := source.read(1024 * 1024):
                        digest.update(block)
                if path.stat().st_size != bundle["sizeBytes"] or digest.hexdigest() != bundle["sha256"].lower():
                    raise ValueError("A local bundle differs from its manifest size or SHA-256.")
                archive.write(path, arcname=bundle["fileName"])
        size = archive_path.stat().st_size
        if size > MAX_UPLOAD:
            raise ValueError("ZIP exceeds the 256 MiB upload limit.")
        address = urllib.parse.urlsplit(public_base(origin))
        connection_type = http.client.HTTPSConnection if address.scheme == "https" else http.client.HTTPConnection
        connection = connection_type(address.hostname, address.port, timeout=120)
        try:
            connection.putrequest("POST", "/api/publish")
            connection.putheader("Authorization", "Bearer " + token)
            connection.putheader("Content-Type", "application/zip")
            connection.putheader("Content-Length", str(size))
            connection.endheaders()
            with archive_path.open("rb") as archive:
                while block := archive.read(1024 * 1024):
                    connection.send(block)
            response = connection.getresponse()
            payload = json.loads(response.read(1024 * 1024).decode("utf-8"))
            if response.status != 201:
                raise RuntimeError(f"Publish failed ({response.status}): {payload.get('code', 'UNKNOWN')} — {payload.get('message', '')}")
            return payload
        finally:
            connection.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("build_dir", type=Path)
    parser.add_argument("--server-url", default="http://127.0.0.1:8788", type=public_base)
    parser.add_argument("--token-file", type=Path, default=PROJECT / ".local" / "token")
    parser.add_argument("--output", type=Path, default=PROJECT / ".local" / "publish-result.json")
    arguments = parser.parse_args()
    try:
        result = publish(arguments.build_dir, arguments.server_url, arguments.token_file)
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        print("Published: " + result["publishUrl"])
        print("Response saved to: " + str(arguments.output.resolve()))
    except Exception as error:
        parser.exit(1, str(error) + "\n")


if __name__ == "__main__":
    main()
