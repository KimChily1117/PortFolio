"""Fetch the pinned official WebP source used for animated skill cutscenes."""
from pathlib import Path, PurePosixPath
import hashlib
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SHA256 = '7a89bb7a6a0c30b5c0f521c2673635381b4e7c916585f4a1f702d3b47f15d5e5'

def main():
    deps = ROOT / '.deps'
    deps.mkdir(exist_ok=True)
    archive = deps / 'libwebp-v1.6.0.zip'
    if not archive.exists():
        urllib.request.urlretrieve('https://codeload.github.com/webmproject/libwebp/zip/refs/tags/v1.6.0', archive)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != SHA256:
        raise RuntimeError('WebP archive checksum mismatch')
    destination = deps / 'libwebp-1.6.0'
    if not destination.exists():
        with zipfile.ZipFile(archive) as z:
            for entry in z.infolist():
                parts = PurePosixPath(entry.filename).parts
                if not parts or parts[0] != destination.name or '..' in parts:
                    raise ValueError('Unsafe archive member')
                if entry.is_dir():
                    continue
                path = deps.joinpath(*parts)
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(z.read(entry))
    print('WebP 1.6.0 ready:', destination)

if __name__ == '__main__':
    main()
