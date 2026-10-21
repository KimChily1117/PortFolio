"""Pack selected read-only decoded DNF contact frames and damage glyphs, retaining provenance."""
from pathlib import Path
import hashlib, json, math
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Artifacts/hit-resource-reference-20261022'
OUT=ROOT/'UnityClient/Assets/Resources/HitFeedback'
OUT.mkdir(parents=True,exist_ok=True)
records=[]
def pack(group,name,indices=None):
    meta=json.loads((SOURCE/group/'metadata.json').read_text(encoding='utf-8'))
    frames=[f for f in meta['frames'] if indices is None or f['index'] in indices]
    images=[]
    for frame in frames:
        path=SOURCE/group/(str(frame['index']).zfill(3)+'.png')
        assert hashlib.sha256(path.read_bytes()).hexdigest()==frame['sha256'],path
        images.append(Image.open(path).convert('RGBA'))
    width=1024; placements=[];x=y=rowheight=0
    for im in images:
        if x+im.width>width:x=0;y+=rowheight+2;rowheight=0
        placements.append((x,y));x+=im.width+2;rowheight=max(rowheight,im.height)
    height=y+rowheight
    atlas=Image.new('RGBA',(width,height));rows=[]
    for frame,im,(x,y) in zip(frames,images,placements):
        atlas.paste(im,(x,y))
        rows.extend([frame['index'],x,y,im.width,im.height,*frame['offset']])
    import io
    buffer=io.BytesIO();atlas.save(buffer,format='PNG');(OUT/(name+'.bytes')).write_bytes(buffer.getvalue())
    records.append(dict(group=group,resource='HitFeedback/'+name,source=meta,
                        atlasSha256=hashlib.sha256(buffer.getvalue()).hexdigest(),indices=[f['index'] for f in frames]))
    canvas=frames[0]['canvas']
    return dict(id='contact_'+name,resource='HitFeedback/'+name,width=width,height=height,
                frameCount=max(f['index'] for f in frames)+1,fps=24,canvasWidth=canvas[0],canvasHeight=canvas[1],
                anchor=[canvas[0]/2,canvas[1]/2],frames=rows)
effects=[pack('sprite_common_hiteffect__'+name,name) for name in ['slashlarge1','slashlarge2','slashlarge3','knocklarge','unionhit']]
font=pack('sprite_common_etc_damagefontskin__damagefont00','damagefont',list(range(10))+list(range(40,50)))
hud=[pack('sprite_interface2_ui_comboeffect__'+name,name) for name in ['dungeon_rank','dungeon_score','combo_bonus_num','newcombo_bonus']]
(OUT/'manifest.json').write_text(json.dumps(dict(effects=effects,font=font,hud=hud),separators=(',',':')),encoding='utf-8')
(ROOT/'Tools/hit-feedback-asset-provenance.json').write_text(json.dumps(dict(
    source='Original DNF NPK decoded with existing PyDnfEx; REA NPK route unsupported',
    timing='Playback order/duration and scale are demo-authored, not recovered ANI',
    records=records),indent=2,ensure_ascii=False),encoding='utf-8')
print('Imported',len(effects),'contact atlases and',len(font['frames'])//7,'original digit glyphs')
