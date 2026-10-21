"""Read-only NPK-derived user-selected d01/d02 frames, with provenance.
Raw extractions remain under Artifacts. RGB-peak alpha approximates additive
sprites for IMGUI; sequencing/anchor/scale are reconstruction choices.
"""
from pathlib import Path
from PIL import Image
import io,json,hashlib
ROOT=Path(__file__).resolve().parents[1]
source=ROOT/'Artifacts/result-direction-20261022/epic'
out=ROOT/'UnityClient/Assets/Resources/DawnRewards/Epic';out.mkdir(exist_ok=True)
effects=[];records=[]
for name in ['newepicd01','newepicd02']:
 meta=json.loads((source/name/'metadata.json').read_text());images=[];positions=[];x=y=row=0;width=2048
 for fr in meta['frames']:
  im=Image.open(source/name/f"{fr['index']:03}.png").convert('RGBA');raw=bytearray(im.tobytes())
  for n in range(0,len(raw),4):
   r,g,b,a=raw[n:n+4];peak=max(r,g,b)
   if peak:raw[n:n+4]=bytes([round(r*255/peak),round(g*255/peak),round(b*255/peak),round(a*peak/255)])
   else:raw[n+3]=0
  im=Image.frombytes('RGBA',im.size,bytes(raw));images.append(im)
  if x+im.width>width:x=0;y+=row+2;row=0
  positions.append((x,y));x+=im.width+2;row=max(row,im.height)
 atlas=Image.new('RGBA',(width,y+row));rows=[]
 for fr,im,(x,y) in zip(meta['frames'],images,positions):atlas.paste(im,(x,y));rows += [fr['index'],x,y,im.width,im.height,*fr['offset']]
 buf=io.BytesIO();atlas.save(buf,format='PNG');data=buf.getvalue();(out/(name+'.bytes')).write_bytes(data)
 effects.append(dict(id='reward_'+name,resource='DawnRewards/Epic/'+name,width=atlas.width,height=atlas.height,frames=rows,frameCount=len(images),fps=24,canvasWidth=1000,canvasHeight=1000,anchor=[500,500]))
 records.append(dict(source=meta,outputSha256=hashlib.sha256(data).hexdigest(),blend='RGB peak to straight alpha; approximation of additive rendering'))
(out/'manifest.json').write_text(json.dumps(dict(effects=effects)),encoding='utf-8')
(ROOT/'Artifacts/result-direction-20261022/epic-runtime-provenance.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print('Imported 20 original frames: d01 (15), d02 (5).')
