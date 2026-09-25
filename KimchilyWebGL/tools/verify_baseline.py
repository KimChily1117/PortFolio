"""Read-only original/preservation hashes and clone-local Unity package references."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import sys
from urllib.parse import unquote

SKIP = {
    'library', 'temp', 'logs', 'usersettings', 'obj', 'bin', 'node_modules', '.git',
    '.vs', '.idea', '__pycache__', '.deps', '.local', '.gradle', '.gradle-user-home',
    '.android-user-home', '.emulator', 'artifacts', 'worldbuilds', 'builds', 'preservation',
}


def digest(path: Path) -> str:
    with path.open('rb') as stream:
        sha = hashlib.sha256()
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            sha.update(chunk)
        return sha.hexdigest()


def within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except ValueError:
        return False


def check_record(base: Path, record: dict, label: str) -> str | None:
    name = record['path']
    file = base / name
    if not within(file, base):
        return f'{label}: path escapes its root: {name}'
    if not file.is_file():
        return f'{label}: missing file: {name}'
    if file.stat().st_size != record['size'] or digest(file) != record['sha256']:
        return f'{label}: changed file: {name}'
    return None


def verify_original(manifest: dict, clone: Path) -> list[str]:
    source = Path(manifest['source']).resolve()
    errors = []
    if within(clone, source) or within(source, clone):
        return ['Original and clone must be separate, non-nested directories.']
    for record in manifest['files']:
        error = check_record(source, record, 'original')
        if error:
            errors.append(error)
    for record in manifest.get('evidence', []):
        for base, label in [(source, 'original evidence'),
                            (clone / 'preservation/android-2022.3', 'preserved evidence')]:
            error = check_record(base, record, label)
            if error:
                errors.append(error)
    return errors


def file_references(value, location='$'):
    if isinstance(value, dict):
        for key, child in value.items():
            yield from file_references(child, f'{location}.{key}')
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from file_references(child, f'{location}[{index}]')
    elif isinstance(value, str) and value.lower().startswith('file:'):
        yield location, value


def audit_local_references(clone: Path) -> tuple[list[str], int]:
    errors = []
    count = 0
    for directory, folders, files in os.walk(clone):
        parent = Path(directory)
        folders[:] = [name for name in folders if name.lower() not in SKIP and
                      not (parent.name == 'KimchilyAndroid' and name.lower() == 'build')]
        for name in folders[:]:
            child = parent / name
            if child.is_symlink() or (hasattr(child, 'is_junction') and child.is_junction()):
                if not within(child, clone):
                    errors.append(f'Linked directory escapes clone: {child.relative_to(clone)}')
                folders.remove(name)
        for name in files:
            if name not in {'manifest.json', 'packages-lock.json', 'package.json'}:
                continue
            file = parent / name
            if not within(file, clone):
                errors.append(f'Package file escapes clone: {file.relative_to(clone)}')
                continue
            try:
                document = json.loads(file.read_text(encoding='utf-8-sig'))
            except (OSError, ValueError) as error:
                errors.append(f'Invalid package JSON: {file.relative_to(clone)}: {error}')
                continue
            for location, reference in file_references(document):
                count += 1
                raw = unquote(reference[5:])
                # file:///C:/... and ordinary file:C:/... both identify an absolute Windows path.
                if raw.startswith('///'):
                    raw = raw[3:]
                elif len(raw) > 2 and raw[0] == '/' and raw[2] == ':':
                    raw = raw[1:]
                destination = (parent / raw).resolve()
                prefix = f'{file.relative_to(clone)} {location}'
                if not within(destination, clone):
                    errors.append(f'{prefix}: local package reference escapes clone: {reference}')
                elif not destination.exists():
                    errors.append(f'{prefix}: local package reference is missing: {reference}')
    return errors, count


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path,
                        default=Path(__file__).resolve().parents[1] / 'preservation/source-manifest.json')
    parser.add_argument('--output', type=Path, help='Optional JSON report path inside the clone.')
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text(encoding='utf-8-sig'))
    clone = Path(__file__).resolve().parents[1]
    if Path(manifest['clone']).resolve() != clone:
        parser.error('The preservation manifest belongs to a different clone.')
    errors = verify_original(manifest, clone)
    reference_errors, references = audit_local_references(clone)
    errors.extend(reference_errors)
    result = {'checkedUtc': datetime.now(timezone.utc).isoformat(), 'passed': not errors,
              'source': manifest['source'], 'clone': str(clone),
              'originalFilesChecked': len(manifest['files']),
              'evidenceFilesChecked': len(manifest.get('evidence', [])),
              'localReferencesChecked': references, 'errors': errors}
    serialized = json.dumps(result, ensure_ascii=False, indent=2) + '\n'
    if args.output:
        output = args.output.resolve()
        if not within(output, clone) or output == args.manifest.resolve():
            parser.error('The report must be inside the clone and cannot overwrite its preservation manifest.')
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(serialized, encoding='utf-8')
    print(serialized, end='')
    return 0 if result['passed'] else 1


if __name__ == '__main__':
    sys.exit(main())
