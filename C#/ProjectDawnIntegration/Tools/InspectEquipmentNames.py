"""Read-only evidence audit, not a decoder for the installed modern Script.pvf.

Searches every byte in the explicitly listed files for exact plaintext probes.
Separately decodes every frame of the existing swordman weapon icon package.
Never labels an image frame index as an original item ID or invents item names.
"""
from pathlib import Path
import csv, hashlib, io, json, mmap, sys
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Research' / 'equipment-names'
GAME = Path(r'D:\Games\DNF')
DECODER = Path(r'E:\task\DnfRaidDemo\asset-work\reference\PyDnfEx')
sys.path.insert(0, str(DECODER))
from pydnfex.npk import NPK
from pydnfex.img import IMGFactory, ImageLink


def sha(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def write(name, value):
    (OUT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def audit():
    # Probes are search terms, not recovered names or a complete dictionary.
    korean = ['창성의 구원자', '오만에 가득찬 눈', '나태함을 가진 발',
              '근원을 삼킨', '결전의', '마법으로 봉인된']
    ascii_terms = ['stringtable.bin', 'equipment.lst', '[name]', '[icon]',
                   'equipment/character/swordman', '.equ', 'Script.pvf']
    probes = [(s, e, s.encode(e)) for s in korean for e in ('utf-8', 'utf-16le', 'cp949')]
    probes += [(s, 'ascii', s.encode('ascii')) for s in ascii_terms]
    files = []
    for name in ('Script.pvf', 'dstr.dat', 'sk.dat', 'package.lst', 'DNF.exe'):
        path = GAME / name
        with path.open('rb') as f, mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ) as data:
            rows = []
            for term, encoding, raw in probes:
                offsets = []
                start = 0
                while True:
                    pos = data.find(raw, start)
                    if pos < 0:
                        break
                    offsets.append(pos)
                    start = pos + len(raw)
                rows.append(dict(term=term, encoding=encoding, count=len(offsets),
                                 fileOffsets=offsets))
            files.append(dict(path=str(path), length=len(data), sha256=sha(path),
                              first32Hex=data[:32].hex(), probes=rows))
        print('Scanned:', name, flush=True)
    write('plaintext-audit.json', dict(
        schemaVersion=1, scope='Every byte of the five explicitly listed local files; exact literal probes only.',
        limits=['Not decryption, decompression, a string-table parser, or complete Korean string recovery.',
                'An absent probe is not proof that an equipment name is absent from the installed game.',
                'A matched probe is not a recovered item record or item-to-icon mapping.'], files=files))


def icons():
    target = GAME / 'ImagePacks2' / 'sprite_item_weapon_swordman.NPK'
    icons_dir = OUT / 'Icons'
    icons_dir.mkdir(exist_ok=True)
    package_hash = sha(target)
    rows, members, errors = [], [], []
    with target.open('rb') as f:
        pack = NPK.open(f)
        for entry in pack.files:
            img = IMGFactory.open(io.BytesIO(entry.data))
            kind = Path(entry.name).stem
            member = dict(member=entry.name, offset=entry._offset, length=len(entry.data),
                          sha256=hashlib.sha256(entry.data).hexdigest(), imgVersion=img.version,
                          frameCount=len(img.images))
            members.append(member)
            for index, frame in enumerate(img.images):
                asset_id = f'dnf-icon-{kind}-{index:04d}'
                try:
                    actual = frame.final_image if isinstance(frame, ImageLink) else frame
                    im = img.build(frame).convert('RGBA')
                    output = icons_dir / (asset_id + '.png')
                    im.save(output)
                    rows.append(dict(assetId=asset_id, kind=kind, sourceMember=entry.name,
                                     sourceFrame=index, width=im.width, height=im.height,
                                     sourceFrameOffset=[actual.x, actual.y],
                                     relativePng=str(output.relative_to(OUT)).replace('\\', '/'),
                                     pngSha256=sha(output), originalItemId=None, originalName=None,
                                     originalStats=None, iconToWeaponSpriteMapping=None))
                except Exception as exc:
                    errors.append(dict(member=entry.name, frame=index, exception=type(exc).__name__, message=str(exc)))
    write('swordman-icon-catalog.json', dict(schemaVersion=1, package=str(target), packageSha256=package_hash,
        decoder=dict(repository='https://github.com/HsOjo/PyDnfEx', commit='e417cefe43f54a40e20ef0df235aa1c8ecd8595a',
                     license='GPL-3.0', localPath=str(DECODER)), members=members, icons=rows, errors=errors,
        limits=['Frame numbers are visual asset indices, not original item IDs.',
                'Original names, stats, rarity, icon-to-weapon-sprite mapping are not recovered.']))
    # Human-review sheets; original sprites are scaled with nearest-neighbor only.
    for kind in sorted(set(r['kind'] for r in rows)):
        subset = [r for r in rows if r['kind'] == kind]
        canvas = Image.new('RGB', (800, ((len(subset)+9)//10)*80), '#18202b')
        draw = ImageDraw.Draw(canvas)
        for i, row in enumerate(subset):
            x, y = (i % 10)*80, (i//10)*80
            sprite = Image.open(OUT / row['relativePng']).convert('RGBA')
            sprite.thumbnail((48,48), Image.Resampling.NEAREST)
            if sprite.width <= 28 and sprite.height <=28:
                sprite = sprite.resize((sprite.width*2,sprite.height*2),Image.Resampling.NEAREST)
            canvas.paste(sprite, (x+(80-sprite.width)//2,y+4),sprite)
            draw.text((x+10,y+61),f"{row['sourceFrame']:04d}",fill='white')
        canvas.save(OUT / f'icons-{kind}.png')
    with (OUT / 'swordman-icon-catalog.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=['assetId','kind','sourceMember','sourceFrame','originalItemId','originalName','relativePng','pngSha256'])
        writer.writeheader()
        writer.writerows({k:r.get(k) for k in writer.fieldnames} for r in rows)
    write('verified-equipment-names.json', dict(schemaVersion=1, entries=[], verifiedRecordCount=0,
        status='unresolved', reason='No decoded item-name table and no item-ID/name/icon mapping recovered from this installation.',
        doNotUseAs='This empty recovery result does not mean that the original game contains no names.'))
    print(json.dumps(dict(decodedIcons=len(rows), members=len(members), errors=len(errors)),ensure_ascii=False))


if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    audit()
    icons()
