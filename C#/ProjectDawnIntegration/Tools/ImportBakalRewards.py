"""Extract the installed Bakal result art, without modifying the source game.

Uses the existing pinned PyDnfEx decoder. NPK is not supported by this REA
provider; its targeted open returned file_format_rejected. Extraction is a
format-aware fallback, not evidence of original layout/timing code.
"""
from pathlib import Path
import hashlib, io, json, sys, uuid
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, 'E:/task/DnfRaidDemo/asset-work/reference/PyDnfEx')
from pydnfex.npk import NPK
from pydnfex.img import IMGFactory, ImageLink

SOURCE = Path('D:/Games/DNF/ImagePacks2/sprite_contents_2022_bakalraid_ui_bakal_reward.NPK')
OUT = ROOT/'UnityClient/Assets/Resources/DawnRewards'
SELECT = {
    'bakal_reward': [0, 1, 2, 4, 5, 8, 12, 17, 18],
    'card_my': list(range(1, 32)),
    'card_etc': list(range(1, 32)),
    'back_loop': list(range(30)),
    'title': [1, 2],
}

def meta(path):
    dest = Path(str(path)+'.meta')
    if dest.exists(): return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'projectdawn/bakal-rewards/'+str(path.relative_to(OUT))).hex
    dest.write_text(f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 12
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  maxTextureSize: 2048
  textureSettings:
    filterMode: 1
    aniso: 1
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  textureShape: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
''', encoding='utf-8')

def main():
    records = []
    with SOURCE.open('rb') as f:
        for entry in NPK.open(f).files:
            if '/bakalraid/' not in entry.name: continue
            name = Path(entry.name).stem
            if name not in SELECT: continue
            img = IMGFactory.open(io.BytesIO(entry.data))
            for index in SELECT[name]:
                frame = img.images[index]
                real = frame.final_image if isinstance(frame, ImageLink) else frame
                source = img.build(frame).convert('RGBA')
                # Preserve native frame anchors; no colour/key/alpha conversion.
                canvas = Image.new('RGBA', (real.mw, real.mh))
                canvas.alpha_composite(source, (real.x, real.y))
                dest = OUT/name/f'{index:03}.png'
                dest.parent.mkdir(parents=True, exist_ok=True)
                canvas.save(dest); meta(dest)
                records.append(dict(member=entry.name, frame=index, version=img.version,
                    memberSha256=hashlib.sha256(entry.data).hexdigest(),
                    offset=[real.x, real.y], size=list(source.size), canvas=list(canvas.size),
                    output=str(dest.relative_to(ROOT)).replace('\\','/'),
                    outputSha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
    record = dict(source=str(SOURCE), sourceSha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        evidence='decoded installed package bytes; original layout/timing scripts not recovered',
        rea='open_binary unsupported_target/file_format_rejected; custom NPK decoder fallback',
        frameCount=len(records), frames=records)
    (ROOT/'Tools/bakal-reward-asset-provenance.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'Imported {len(records)} original Bakal reward frames.')

if __name__ == '__main__': main()
