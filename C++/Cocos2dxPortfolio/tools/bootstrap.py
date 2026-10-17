"""Reproduce the pinned Cocos2d-x SDK with Python 3, without legacy Python 2 tools."""
from pathlib import Path, PurePosixPath
import hashlib
import json
import subprocess
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
DEPS = ROOT / '.deps'
ENGINE = DEPS / 'cocos2d-x'
REVISION = 'e3438ed3fd10a304b7ca2cd3dad9b29fead818d2'
DEPS_SHA256 = '721c0588f10c7e445d4b4103efcf3869203fffa9bf7836deb3c6bb51b2f8a764'

def main():
    DEPS.mkdir(exist_ok=True)
    if not ENGINE.exists():
        subprocess.run(['git','clone','--depth','1','--branch','cocos2d-x-4.0',
                        'https://github.com/cocos2d/cocos2d-x.git',str(ENGINE)],check=True)
    actual = subprocess.check_output(['git','-C',str(ENGINE),'rev-parse','HEAD'],text=True).strip()
    if actual != REVISION:
        raise RuntimeError(f'Expected pinned engine {REVISION}, found {actual}; leaving it intact.')
    config = json.loads((ENGINE/'external/config.json').read_text())
    archive = DEPS / f"{config['version']}.zip"
    if not (ENGINE/'external/glfw3/prebuilt/win32/glfw3.lib').exists():
        url = f"https://codeload.github.com/cocos2d/{config['repo_name']}/zip/refs/tags/{config['version']}"
        if not archive.exists():
            print('Downloading official engine dependencies...')
            urllib.request.urlretrieve(url,archive)
        if hashlib.sha256(archive.read_bytes()).hexdigest() != DEPS_SHA256:
            raise RuntimeError('Dependency archive checksum mismatch; extraction stopped.')
        with zipfile.ZipFile(archive) as z:
            for entry in z.infolist():
                if entry.is_dir(): continue
                parts = PurePosixPath(entry.filename).parts[1:]
                if not parts or '..' in parts: raise ValueError('Unsafe archive member')
                destination = ENGINE/'external'/Path(*parts)
                destination.parent.mkdir(parents=True,exist_ok=True)
                destination.write_bytes(z.read(entry))
        print('Dependency archive SHA256:', hashlib.sha256(archive.read_bytes()).hexdigest())
    print('Cocos2d-x 4.0 ready:', ENGINE)
    subprocess.run([__import__('sys').executable, str(ROOT/'tools/bootstrap-webp.py')], check=True)

if __name__ == '__main__': main()
