"""Import a reproducible local preview selection from extracted Epic Seven resources.

The full extraction stays in .deps/epicseven-assets. This script only creates
new preview files and a catalog consumed by ResourceGallery, never edits the game.
Python + Pillow required for validating and converting image previews.
"""
import argparse
import hashlib
import json
import pathlib
import shutil
import wave

ROOT = pathlib.Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=pathlib.Path, default=ROOT / '.deps/epicseven-assets')
    parser.add_argument('--image-limit', type=int, default=48)
    parser.add_argument('--audio-limit', type=int, default=16)
    args = parser.parse_args()
    if min(args.image_limit, args.audio_limit) < 0:
        parser.error('limits must be nonnegative')
    from PIL import Image
    source = args.source.resolve()
    destination = ROOT / 'Resources/reference/epicseven'
    destination.mkdir(parents=True, exist_ok=True)
    (destination / 'images').mkdir(exist_ok=True)
    (destination / 'audio').mkdir(exist_ok=True)
    groups = {}
    for folder in ('converted', 'raw'):
        for file in sorted((source / folder).rglob('*')):
            if not file.is_file() or file.suffix.lower() not in {'.png', '.webp', '.jpg', '.jpeg'}:
                continue
            relative = file.relative_to(source).as_posix()
            category = ('배경' if any(s in relative.lower() for s in ('background', '/bg/', '/bg_', 'back/'))
                        else '캐릭터' if any(s in relative.lower() for s in ('character', 'portrait', 'face', 'hero'))
                        else 'UI' if any(s in relative.lower() for s in ('icon', '/ui/', 'button'))
                        else '이미지')
            groups.setdefault(category, []).append(file)
    # Round-robin categories avoids filling every preview with one asset family.
    candidates = []
    while any(groups.values()):
        for files in groups.values():
            if files: candidates.append(files.pop(0))
    items, rejected = [], []
    seen = set()
    for file in candidates:
        if len(items) >= args.image_limit: break
        try:
            with Image.open(file) as image:
                image.load()
                if min(image.size) < 32: continue
                original_size = image.size
                rgba = image.convert('RGBA')
                rgba.thumbnail((1536, 1536))
                fingerprint = hashlib.sha256(rgba.tobytes()).hexdigest()
                if fingerprint in seen: continue
                seen.add(fingerprint)
                target = destination / 'images' / (fingerprint[:20] + '.png')
                if not target.exists(): rgba.save(target)
                relative = file.relative_to(source).as_posix()
                items.append({'type': 'image', 'path': target.relative_to(ROOT / 'Resources').as_posix(),
                              'name': file.stem, 'category': relative.split('/')[1] if '/' in relative else 'image',
                              'source': relative, 'source_sha256': hashlib.sha256(file.read_bytes()).hexdigest(),
                              'original_size': original_size, 'preview_size': rgba.size})
        except (OSError, ValueError) as error:
            rejected.append({'source': str(file), 'error': str(error)})
    manifests = sorted((source / 'media/audio').rglob('manifest.json'))
    audio_groups = []
    for manifest in manifests:
        data = json.loads(manifest.read_text(encoding='utf-8'))
        tracks = data.get('tracks', [])
        # Prefer short UI clips and bounded BGM samples over long voice composites.
        tracks = [t for t in tracks if 0.05 <= t.get('frames', 0) / max(1, t.get('sample_rate', 0)) <= 90]
        if tracks: audio_groups.append(tracks)
    audio_count = 0
    while audio_count < args.audio_limit and any(audio_groups):
        for tracks in audio_groups:
            if not tracks or audio_count >= args.audio_limit: continue
            track = tracks.pop(0)
            file = source / 'media' / track['path']
            with wave.open(str(file), 'rb') as wav:
                if wav.getnframes() <= 0: continue
            fingerprint = hashlib.sha256(file.read_bytes()).hexdigest()
            target = destination / 'audio' / (fingerprint[:20] + '.wav')
            if not target.exists(): shutil.copy2(file, target)
            items.append({'type':'audio', 'path':target.relative_to(ROOT / 'Resources').as_posix(),
                          'name': track.get('metadata', {}).get('streamInfo', {}).get('name') or file.stem,
                          'category': manifest.parent.name, 'source': file.relative_to(source).as_posix(),
                          'source_sha256':fingerprint, 'duration_seconds':track['frames']/track['sample_rate']})
            audio_count += 1
    result = {'schema':1, 'source_root':str(source), 'items':items, 'rejected':rejected}
    temporary = destination / 'catalog.json.tmp'
    temporary.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    temporary.replace(destination / 'catalog.json')
    print(json.dumps({'images':sum(i['type']=='image' for i in items), 'audio':audio_count, 'catalog':str(destination / 'catalog.json'), 'rejected':len(rejected)}, ensure_ascii=False))

if __name__ == '__main__':
    main()
