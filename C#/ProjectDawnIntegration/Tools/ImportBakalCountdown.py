"""Decode original countdown / operation-start artwork; never modify game packs.

REA rejected this NPK format. The existing PyDnfEx decoder is the documented
fallback. Pixels/anchors are source evidence; playback timing is authored.
"""
from pathlib import Path
import hashlib, io, json, sys
from PIL import Image
from ImportBakalRewards import meta, OUT
sys.path.insert(0, 'E:/task/DnfRaidDemo/asset-work/reference/PyDnfEx')
from pydnfex.npk import NPK
from pydnfex.img import IMGFactory, ImageLink

ROOT=Path(__file__).resolve().parents[1]
records=[]
for pack, name, indices in [('bakal_reward','countdown',range(30)),('bakal_message','bakal_message',[0,1,2,7])]:
    src=Path('D:/Games/DNF/ImagePacks2/sprite_contents_2022_bakalraid_ui_'+pack+'.NPK')
    with src.open('rb') as f:
        for entry in NPK.open(f).files:
            if '/bakalraid/' not in entry.name or Path(entry.name).stem!=name: continue
            img=IMGFactory.open(io.BytesIO(entry.data))
            for index in indices:
                frame=img.images[index]
                real=frame.final_image if isinstance(frame,ImageLink) else frame
                pixels=img.build(frame).convert('RGBA')
                if name=='countdown':
                    canvas=Image.new('RGBA',(real.mw,real.mh));canvas.alpha_composite(pixels,(real.x,real.y))
                else: canvas=pixels # Each independent banner component keeps its native aspect.
                dest=OUT/name/f'{index:03}.png';dest.parent.mkdir(parents=True,exist_ok=True)
                canvas.save(dest);meta(dest)
                records.append(dict(source=str(src),sourceSha256=hashlib.sha256(src.read_bytes()).hexdigest(),
                    member=entry.name,memberSha256=hashlib.sha256(entry.data).hexdigest(),frame=index,
                    offset=[real.x,real.y],canvas=[real.mw,real.mh],output=str(dest.relative_to(ROOT)),
                    outputSha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(ROOT/'Tools/bakal-countdown-provenance.json').write_text(json.dumps(dict(
    evidence='Original decoded pixels. 0..9 = 3, 10..19 = 2, 20..29 = 1; message 7 = 작전 시작.',
    limitation='No original ANI timing or network logic recovered; playback and server timeline are ProjectDawn implementation.',
    frames=records),ensure_ascii=False,indent=2),encoding='utf-8')
print('Imported 30 countdown frames and 4 original operation-start components.')
