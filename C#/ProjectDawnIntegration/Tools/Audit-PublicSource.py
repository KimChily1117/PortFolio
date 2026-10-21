"""Audit this source folder's staged Git bytes; never print matched secret values."""
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
REPO = Path(subprocess.check_output(['git', 'rev-parse', '--show-toplevel'], cwd=ROOT, text=True).strip())
PREFIX = ROOT.relative_to(REPO).as_posix() + '/'
paths = subprocess.check_output(['git', 'ls-files', '-z', '--', PREFIX], cwd=REPO).decode().split('\0')
paths = [p for p in paths if p]
forbidden_ext = {'.dll', '.exe', '.pdb', '.npk', '.img', '.png', '.jpg', '.jpeg', '.gif', '.mp4', '.mp3', '.wav', '.ogg', '.ttf', '.otf', '.zip', '.7z', '.pem', '.pfx', '.key', '.bak', '.nupkg'}
forbidden_dirs = {'bin', 'obj', 'Library', 'Temp', 'UserSettings', 'Artifacts', 'Logs', 'Resources', 'StreamingAssets', 'RuntimeBinaries'}
rules = [
    ('aws-access-key', re.compile(r'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b')),
    ('github-token', re.compile(r'\bgh[pousr]_[A-Za-z0-9]{30,}\b')),
    ('private-key-content', re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----\s+[A-Za-z0-9+/=]{32,}')),
    ('personal-home', re.compile(r'C:[\\/]+Users[\\/]+yeop_sun', re.I)),
    ('actual-instance', re.compile(r'\bi-(?!0{17}\b)[0-9a-f]{17}\b')),
    ('jwt-token', re.compile(r'\beyJ[A-Za-z0-9_-]{25,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\b')),
    ('embedded-binary', re.compile(r'data:(?:image|audio|video)/[^;]+;base64,', re.I)),
]
issues = []
total = 0
for p in paths:
    relative = p[len(PREFIX):]
    file = Path(relative)
    if file.suffix.lower() in forbidden_ext or any(part in forbidden_dirs for part in file.parts):
        issues.append((relative, 0, 'excluded-file-type-or-directory'))
    if file.name in {'aws-playtest.json', 'cloud-playtest.json', 'local.json', '.env'} or re.search(r'(credentials|accounts|invite|private).*\.json$', file.name, re.I):
        issues.append((relative, 0, 'private-config-name'))
    raw = subprocess.check_output(['git', 'show', ':' + p], cwd=REPO)
    total += len(raw)
    if b'\0' in raw:
        issues.append((relative, 0, 'non-text-file'))
        continue
    text = raw.decode('utf-8-sig')
    for number, line in enumerate(text.splitlines(), 1):
        for label, rule in rules:
            if rule.search(line):
                issues.append((relative, number, label))
if not paths:
    issues.append(('', 0, 'no-staged-source-files'))
for path, line, reason in issues:
    print(f'FAIL {path}:{line} [{reason}]')
print(f'Staged source audit: {len(paths)} files / {total:,} bytes / {len(issues)} findings')
print('Pattern audit complements manual review; it is not proof against every secret format.')
sys.exit(1 if issues else 0)
