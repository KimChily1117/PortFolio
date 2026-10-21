"""Import bounded, visually inspected Bakal sprites from the read-only NPK extraction.

Raw decoded files stay unchanged. Runtime files adapt opaque-black additive artwork
to straight-alpha GUI drawing; placement and animation timing are authored by this
demo, not recovered original ANI/map data.
"""
from pathlib import Path
import hashlib, io, json
from PIL import Image, ImageDraw, ImageChops

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Artifacts/world-resource-reference-20261022'
OUT = ROOT / 'UnityClient/Assets/Resources/OriginalWorldEffects'
OUT.mkdir(parents=True, exist_ok=True)

# frame exclusion: frame 16 is a low-opacity end image, not part of each loop.
SELECTIONS = [
    ('magiccircle_dn', 'ground_magic', [200, 100], 16, True),
    ('bakalmarkloop_dn', 'target_mark', [125, 125], 16, True),
    ('portal1_d', 'room_portal', [485, 630], None, True),
    ('firebackloop', 'ground_flame', [300, 420], None, True),
    ('fireloop', 'large_flame', [300, 640], None, True),
    ('thinfire', 'thin_flame', [127, 375], None, True),
    ('warstompshockdodge', 'impact_wave', [400, 750], None, True),
    ('floor', 'actor_shadow', [250, 250], None, False),
]

def adapt(im, additive):
    if not additive:
        return im
    raw=bytearray(im.tobytes())
    for i in range(0,len(raw),4):
        r,g,b,a=raw[i:i+4];peak=max(r,g,b)
        if peak:
            raw[i]=round(r*255/peak);raw[i+1]=round(g*255/peak);raw[i+2]=round(b*255/peak);raw[i+3]=round(a*peak/255)
        else:raw[i+3]=0
    return Image.frombytes('RGBA',im.size,bytes(raw))

effects, provenance = [], []
preview = Image.new('RGB', (1000, 560), (30, 32, 43))
draw = ImageDraw.Draw(preview)
for n, (group, key, anchor, take, additive) in enumerate(SELECTIONS):
    meta = json.loads((SOURCE/group/'metadata.json').read_text())
    frames = meta['frames'][:take]
    images = []
    for frame in frames:
        path = SOURCE/group/f"{frame['index']:03}.png"
        assert hashlib.sha256(path.read_bytes()).hexdigest() == frame['sha256']
        images.append(adapt(Image.open(path).convert('RGBA'), additive))
    width, x, y, rowheight = 2048, 0, 0, 0
    placements = []
    for im in images:
        if x+im.width > width:
            x, y, rowheight = 0, y+rowheight+2, 0
        placements.append((x,y)); x += im.width+2; rowheight=max(rowheight,im.height)
    height = y+rowheight
    atlas = Image.new('RGBA',(width,height)); rows=[]
    for frame, im, (x,y) in zip(frames,images,placements):
        atlas.paste(im,(x,y))
        rows += [frame['index'],x,y,im.width,im.height,*frame['offset']]
    buffer=io.BytesIO();atlas.save(buffer,format='PNG');data=buffer.getvalue()
    (OUT/(key+'.bytes')).write_bytes(data)
    effects.append(dict(id='world_'+key,resource='OriginalWorldEffects/'+key,
                        width=width,height=height,frameCount=len(frames),fps=16,
                        canvasWidth=frames[0]['canvas'][0],canvasHeight=frames[0]['canvas'][1],
                        anchor=anchor,frames=rows))
    provenance.append(dict(key=key,source=meta,indices=[f['index'] for f in frames],
                           runtimeSha256=hashlib.sha256(data).hexdigest(),
                           blendConversion='straight-alpha from RGB peak; approximate additive appearance' if additive else 'unchanged RGBA',
                           anchorAuthorship='demo authored',anchor=anchor))
    thumb=images[min(2,len(images)-1)].copy();thumb.thumbnail((230,220))
    px=n%4*250;py=n//4*280
    preview.paste(thumb,(px+(250-thumb.width)//2,py+(240-thumb.height)//2),thumb)
    draw.text((px+12,py+243),key,fill='white')

(OUT/'manifest.json').write_text(json.dumps(dict(effects=effects),separators=(',',':')))
(ROOT/'Tools/original-world-effect-provenance.json').write_text(json.dumps(dict(
    extractionScript='Artifacts/world-resource-extract-20261022.py',
    reaLimitation='NPK unsupported; reused retained rejection Artifacts/hit-reference-video-20261022/rea-open-result.json',
    interpretation='Sprites visually inspected. Semantic use, anchors, scale, frame rates and composition are demo decisions, not recovered original game scripts.',
    records=provenance),indent=2))
preview.save(SOURCE/'runtime-effects-preview.png')
print('Imported',len(effects),'original world effect atlases,',sum(v['frameCount'] for v in effects),'frames')
