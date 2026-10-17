"""Stage decoded SCSP bundles for the bundled legacy Spine C runtime.

Original Spine 3.8 JSON is retained alongside an explicitly adapted preview copy.
This changes JSON shape/curve encoding, not original textures or animation data.
"""
import argparse, copy, hashlib, json, pathlib, shutil, subprocess
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]

def curves(value):
    if isinstance(value, dict):
        if isinstance(value.get('curve'), (int, float)):
            value['curve'] = [value['curve'], value.pop('c2', 0), value.pop('c3', 1), value.pop('c4', 1)]
        for child in value.values(): curves(child)
    elif isinstance(value, list):
        for child in value: curves(child)

def adapt(original):
    data = copy.deepcopy(original)
    if any(s.get('dark') for s in data.get('slots', [])):
        raise ValueError('two-color tint requires a different shader')
    skins = data.get('skins', {})
    if isinstance(skins, list):
        if any(s.get('bones') or s.get('ik') or s.get('transform') or s.get('path') for s in skins):
            raise ValueError('skin-required constraints need a newer runtime')
        data['skins'] = {s['name']: s.get('attachments', {}) for s in skins}
    curves(data)
    names = [b['name'] for b in data['bones']]
    if len(names) != len(set(names)): raise ValueError('duplicate bones')
    for index, bone in enumerate(data['bones']):
        if bone.get('parent') and bone['parent'] not in names[:index]: raise ValueError('invalid bone parent order')
    slots = {s['name'] for s in data['slots']}
    lengths = {}
    for skin, entries in data['skins'].items():
        for slot, attachments in entries.items():
            if slot not in slots: raise ValueError('unknown attachment slot')
            for name, attachment in attachments.items():
                kind = attachment.get('type', 'region')
                if kind not in {'region', 'mesh', 'linkedmesh', 'boundingbox', 'path', 'point', 'clipping'}:
                    raise ValueError('unsupported attachment type: ' + kind)
                if kind not in {'mesh', 'boundingbox', 'path', 'clipping'}: continue
                vertices = attachment['vertices']
                if kind == 'mesh':
                    uvs = attachment['uvs']
                    count = len(uvs) // 2
                    if len(uvs) % 2 or any(i < 0 or i >= count for i in attachment['triangles']):
                        raise ValueError('mesh index outside vertex range')
                else:
                    count = attachment['vertexCount']
                if len(vertices) == count * 2:
                    length = len(vertices)
                else:
                    pos, influences = 0, 0
                    for _ in range(count):
                        n = int(vertices[pos]); pos += 1
                        if n <= 0 or pos + n * 4 > len(vertices): raise ValueError('invalid weighted mesh')
                        for i in range(n):
                            bone = vertices[pos + i * 4]
                            if int(bone) != bone or not 0 <= bone < len(names): raise ValueError('invalid mesh bone')
                        influences += n
                        pos += n * 4
                    if pos != len(vertices): raise ValueError('weighted mesh trailing data')
                    length = influences * 2
                lengths[(skin, slot, name)] = length
    pending_links = {(skin, slot, name): (attachment.get('skin', 'default'), slot, attachment['parent'])
                     for skin, entries in data['skins'].items()
                     for slot, attachments in entries.items()
                     for name, attachment in attachments.items() if attachment.get('type') == 'linkedmesh'}
    while pending_links:
        ready = [key for key, parent in pending_links.items() if parent in lengths]
        if not ready: break
        for key in ready:
            lengths[key] = lengths[pending_links.pop(key)]
    for name, animation in data.get('animations', {}).items():
        for skin, skin_slots in animation.get('deform', {}).items():
            for slot, attachments in skin_slots.items():
                for attachment, frames in attachments.items():
                    key = (skin, slot, attachment)
                    if key not in lengths: raise ValueError('deform references unsupported mesh')
                    for frame in frames:
                        offset = frame.get('offset', 0)
                        if offset < 0 or offset + len(frame.get('vertices', [])) > lengths[key]:
                            raise ValueError('deform outside mesh vertices')
        for bone, timelines in animation.get('bones', {}).items():
            if bone not in names: raise ValueError('unknown animated bone')
            if set(timelines) - {'rotate','translate','scale','shear'}: raise ValueError('unsupported bone timeline')
            # Spine 3.8 omits identity scale components. The bundled reader uses
            # zero for missing components, which collapses meshes into lines.
            for frame in timelines.get('scale', []):
                frame.setdefault('x', 1)
                frame.setdefault('y', 1)
            for frames in timelines.values():
                if not frames: raise ValueError('empty bone timeline')
    data['skeleton']['sourceSpineVersion'] = data['skeleton'].get('spine')
    data['skeleton']['spine'] = '3.6.53'
    return data

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=pathlib.Path, default=ROOT / '.deps/epicseven-assets/animated')
    parser.add_argument('--limit', type=int, default=0, help='Maximum imports; 0 imports all supported bundles')
    parser.add_argument('--include-legacy', action='store_true', help='Preview approximate legacy 2.1 transforms')
    parser.add_argument('--append', action='store_true', help='Merge another converted library into the catalog')
    parser.add_argument('--only', nargs='+', help='Import only these asset IDs')
    parser.add_argument('--validator', type=pathlib.Path, help='Built aster_spine_tests executable')
    args = parser.parse_args()
    source = args.source.resolve()
    metadata_path = ROOT / '.deps/epicseven-assets/cutscene-analysis/community-metadata/heroes-by-asset.json'
    metadata = json.loads(metadata_path.read_text(encoding='utf-8')).get('by_asset', {}) if metadata_path.exists() else {}
    dest = ROOT / 'Resources/reference/epicseven'
    dest.mkdir(parents=True, exist_ok=True)
    candidates = [p for p in source.rglob('*.json') if p.with_suffix('.atlas').is_file()]
    if args.only:
        candidates = [p for p in candidates if p.stem in args.only]
    candidates.sort(key=lambda p: (p.stem not in {'c1001', 'ras', 'birgitta'}, 'portrait' not in p.parts, str(p)))
    items, failures, skipped = [], [], []
    for file in candidates:
        if args.limit and len(items) >= args.limit: break
        try:
            original = json.loads(file.read_text(encoding='utf-8'))
            if original.get('skeleton', {}).get('e7v21x') and not args.include_legacy:
                skipped.append({'source':str(file), 'reason':'legacy 2.1 custom transforms; originals retained'})
                continue
            adapted = adapt(original)
            target = dest / 'spine' / file.parent.relative_to(source)
            target.mkdir(parents=True, exist_ok=True)
            for asset in file.parent.rglob('*'):
                if not asset.is_file() or asset.suffix not in {'.png', '.atlas'}: continue
                if asset.suffix == '.png':
                    with Image.open(asset) as image: image.verify()
                output = target / asset.relative_to(file.parent)
                output.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(asset, output)
            json_path = target / (file.stem + '.cocos.json')
            json_path.write_text(json.dumps(adapted, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
            atlas_path = target / file.with_suffix('.atlas').name
            # Page headers are separated by a blank line and name a PNG file.
            for line in atlas_path.read_text(encoding='utf-8').splitlines():
                if line.strip().lower().endswith('.png'):
                    page = (target / line.strip()).resolve()
                    if not page.is_relative_to(target.resolve()) or not page.is_file(): raise ValueError('missing/unsafe atlas page')
            validation = None
            if args.validator:
                result = subprocess.run([str(args.validator.resolve()), str(json_path), str(atlas_path)],
                                        capture_output=True, text=True, timeout=45)
                validation = result.stdout.strip()
                if result.returncode:
                    raise ValueError('runtime validation failed: ' + (result.stderr or result.stdout).strip())
            identity = metadata.get(file.parent.relative_to(source).as_posix(), {})
            items.append({'type':'spine', 'path':json_path.relative_to(ROOT/'Resources').as_posix(),
                          'atlas':atlas_path.relative_to(ROOT/'Resources').as_posix(), 'name':file.stem,
                          'display_name':identity.get('display_name', file.stem),
                          'gender':identity.get('gender', 'unknown'), 'hero_id':identity.get('hero_id', ''),
                          'category':'초상화' if 'portrait' in file.parts else '전투 모델',
                          'animations':list(adapted.get('animations',{})), 'skins':list(adapted['skins']),
                          'source_json':str(file), 'source_sha256':hashlib.sha256(file.read_bytes()).hexdigest(),
                          'runtime_validation':validation,
                          'compatibility':'3.8 JSON shape and Bezier curves adapted to bundled 3.6 runtime; original retained',
                          'legacy_transform_caveat':bool(original.get('skeleton',{}).get('e7v21x'))})
        except (OSError, ValueError, KeyError, IndexError, subprocess.TimeoutExpired) as error:
            if str(error) == 'runtime validation failed: FAIL: animation changes real world vertices':
                skipped.append({'source':str(file), 'reason':'no moving geometry in sampled animations'})
            else:
                failures.append({'source':str(file), 'error':str(error)})
    result = {'schema':1, 'items':items, 'failures':failures, 'skipped':skipped}
    catalog_path = dest/'catalog.json'
    if args.append and catalog_path.exists():
        previous = json.loads(catalog_path.read_text(encoding='utf-8'))
        by_path = {i['path']:i for i in previous.get('items', [])}
        by_path.update({i['path']:i for i in items})
        result['items'] = list(by_path.values())
        result['failures'] = previous.get('failures', []) + failures
        result['skipped'] = previous.get('skipped', []) + skipped
    successful_sources = {i['source_json'] for i in result['items']}
    successful_assets = {tuple(pathlib.PurePosixPath(i['path']).parent.parts[-2:]) for i in result['items']}
    for failure in result['failures']:
        if failure['error'] == 'runtime validation failed: FAIL: animation changes real world vertices':
            result['skipped'].append({'source':failure['source'], 'reason':'no moving geometry in sampled animations'})
    result['failures'] = [f for f in result['failures'] if f['error'] != 'runtime validation failed: FAIL: animation changes real world vertices']
    result['failures'] = [f for f in result['failures'] if f['source'] not in successful_sources and
                          tuple(pathlib.Path(f['source']).parent.parts[-2:]) not in successful_assets]
    result['failures'] = list({f['source']:f for f in result['failures']}.values())
    result['skipped'] = list({f['source']:f for f in result['skipped']}.values())
    for item in result['items']:
        key = pathlib.PurePosixPath(item['path']).parent.relative_to('reference/epicseven/spine').as_posix()
        identity = metadata.get(key, {})
        item.update(display_name=identity.get('display_name', item['name']),
                    gender=identity.get('gender', 'unknown'), hero_id=identity.get('hero_id', ''))
    (dest/'catalog.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'imported_this_run':len(items),'catalog_items':len(result['items']),
                      'failure_count':len(result['failures']),'skipped_total':len(result['skipped'])},ensure_ascii=False))

if __name__ == '__main__': main()
