"""Add original Bakal countdown ring, reversed for empty-to-full demo progress.
Reads the selected NPK without modifying it; preserves other world manifest entries.
Run after ImportOriginalWorldEffects.py when rebuilding all imported world artwork.
Original encounter sprites are intentionally not imported here.
"""
from pathlib import Path
import hashlib,io,json,sys
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
SOURCE=Path('D:/Games/DNF/ImagePacks2/sprite_contents_2022_bakalraid_ui_bakal_monsterpattern.NPK')
MEMBER='sprite/contents/2022/bakalraid/ui/bakal_monsterpattern/gaugesequence.img'
DECODER=Path('E:/task/DnfRaidDemo/asset-work/reference/PyDnfEx')
sys.path.insert(0,str(DECODER))
from pydnfex.npk import NPK
from pydnfex.img import IMGFactory,ImageLink
OUT=ROOT/'UnityClient/Assets/Resources/OriginalWorldEffects'
manifestPath=OUT/'manifest.json'
manifest=json.loads(manifestPath.read_text(encoding='utf-8'))
with SOURCE.open('rb') as f:
 npk=NPK.open(f);entry=next(x for x in npk.files if x.name==MEMBER);data=entry.data
img=IMGFactory.open(io.BytesIO(data));assert len(img.images)==100
atlas=Image.new('RGBA',(440,440));rows=[];mapping=[]
for outputIndex,sourceIndex in enumerate(range(99,-1,-1)):
 frame=img.images[sourceIndex];real=frame.final_image if isinstance(frame,ImageLink) else frame
 im=img.build(frame).convert('RGBA');assert im.size==(42,42);assert (real.mw,real.mh)==(54,54)
 x=outputIndex%10*44;y=outputIndex//10*44;atlas.paste(im,(x,y))
 rows.extend([outputIndex,x,y,im.width,im.height,real.x,real.y])
 mapping.append(dict(runtimeFrame=outputIndex,sourceFrame=sourceIndex,offset=[real.x,real.y],canvas=[real.mw,real.mh]))
buffer=io.BytesIO();atlas.save(buffer,format='PNG');encoded=buffer.getvalue();(OUT/'mark_gauge.bytes').write_bytes(encoded)
item=dict(id='world_mark_gauge',resource='OriginalWorldEffects/mark_gauge',width=440,height=440,frameCount=100,fps=100,canvasWidth=54,canvasHeight=54,anchor=[27,27],frames=rows)
manifest['effects']=[e for e in manifest['effects'] if e.get('id')!='world_mark_gauge']+[item]
manifestPath.write_text(json.dumps(manifest,separators=(',',':')),encoding='utf-8')
record=dict(key='mark_gauge',source=dict(npk=str(SOURCE),npkSha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),member=MEMBER,offset=entry._offset,length=len(data),imgSha256=hashlib.sha256(data).hexdigest()),indices=list(range(99,-1,-1)),frameMapping=mapping,runtimeSha256=hashlib.sha256(encoded).hexdigest(),blendConversion='unchanged RGBA',anchorAuthorship='canvas center [27,27]; demo progress mapping',anchor=[27,27],playback='Original ring drains at ascending native index; reverse99..0 gives demo empty-to-full progress. fps100/duration1 are demo-authored, not recovered ANI.',importer='Tools/ImportOriginalMarkGauge.py')
provenancePath=ROOT/'Tools/original-world-effect-provenance.json'
provenance=json.loads(provenancePath.read_text(encoding='utf-8'))
provenance['records']=[r for r in provenance['records'] if r.get('key')!='mark_gauge']+[record]
provenancePath.write_text(json.dumps(provenance,indent=2),encoding='utf-8')
(ROOT/'Tools/original-mark-gauge-provenance.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
print('Added world_mark_gauge:100 frames native99->0; preserved',len(manifest['effects'])-1,'existing entries')
