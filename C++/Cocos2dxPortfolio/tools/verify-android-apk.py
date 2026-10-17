"""Validate the complete packaged asset library against Resources, read-only."""
import argparse
import json
import pathlib
import zipfile
import zlib


def crc_file(path):
    crc = 0
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            crc = zlib.crc32(chunk, crc)
    return crc & 0xffffffff


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('apk', type=pathlib.Path)
    parser.add_argument('--resources', type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[1] / 'Resources')
    parser.add_argument('--report', type=pathlib.Path, required=True)
    args = parser.parse_args()
    expected = {'assets/' + p.relative_to(args.resources).as_posix(): p for p in args.resources.rglob('*') if p.is_file()}
    with zipfile.ZipFile(args.apk) as archive:
        entries = archive.infolist()
        if len({e.filename for e in entries}) != len(entries):
            raise RuntimeError('Duplicate APK entries')
        packaged = {e.filename: e for e in entries if e.filename.startswith('assets/') and not e.is_dir()}
        if expected.keys() != packaged.keys():
            raise RuntimeError(f'Asset mismatch: missing={sorted(expected.keys() - packaged.keys())}, extra={sorted(packaged.keys() - expected.keys())}')
        for name, source in expected.items():
            entry = packaged[name]
            if entry.file_size != source.stat().st_size or entry.CRC != crc_file(source):
                raise RuntimeError(f'Source CRC/length mismatch: {name}')
            if source.suffix.lower() in {'.ogg', '.webp', '.mp3'} and entry.compress_type != zipfile.ZIP_STORED:
                raise RuntimeError(f'Streamed compressed media must be ZIP_STORED: {name}')
        corrupt = archive.testzip()
        if corrupt:
            raise RuntimeError(f'Corrupt ZIP entry: {corrupt}')
        report = {'apk': str(args.apk.resolve()), 'apk_bytes': args.apk.stat().st_size,
                  'asset_count': len(expected), 'source_bytes': sum(e.file_size for e in packaged.values()),
                  'packaged_asset_bytes': sum(e.compress_size for e in packaged.values()),
                  'apk_entries': len(entries), 'exact_asset_set': True, 'source_crc_and_size': True,
                  'all_zip_entries_crc': True, 'streamed_media_stored': True}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
