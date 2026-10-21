"""Package original Bakal raised-foot/plant frames; timing is demo-authored."""
from pathlib import Path
from PIL import Image
import json,hashlib,io
ROOT=Path(__file__).resolve().parents[1];SRC=ROOT/'Artifacts/bakal-encounter-review-20261022';OUT=ROOT/'UnityClient/Assets/Resources/BakalStomp';OUT.mkdir(exist_ok=True)
effects=[];records=[]
def additive(im):
 raw=bytearray(im.tobytes())
 for n in range(0,len(raw),4):
  r,g,b,a=raw[n:n+4];peak=max(r,g,b)
  if peak:raw[n:n+4]=bytes((round(r*255/peak),round(g*255/peak),round(b*255/peak),round(a*peak/255)))
  else:raw[n+3]=0
 return Image.frombytes('RGBA',im.size,bytes(raw))
def pack(key,source,anchor,indices=None,blend=False):
 m=json.loads((SRC/source/'metadata.json').read_text());indices=indices or list(range(len(m['frames'])));ims=[];fs=[]
 for i in indices:
  f=m['frames'][i];p=SRC/source/f'{i:03}.png';assert hashlib.sha256(p.read_bytes()).hexdigest()==f['sha256'];im=Image.open(p).convert('RGBA');ims.append(additive(im) if blend else im);fs.append(f)
 width=2048;x=y=h=0;pos=[]
 for im in ims:
  if x+im.width>width:x=0;y+=h+2;h=0
  pos.append((x,y));x+=im.width+2;h=max(h,im.height)
 atlas=Image.new('RGBA',(width,y+h));rows=[]
 for n,(im,f,(x,y)) in enumerate(zip(ims,fs,pos)):
  atlas.paste(im,(x,y));rows.extend([n,x,y,im.width,im.height,*f['offset']])
 buf=io.BytesIO();atlas.save(buf,format='PNG');data=buf.getvalue();(OUT/f'{key}.bytes').write_bytes(data)
 effects.append(dict(id='stomp_'+key,resource='BakalStomp/'+key,width=width,height=atlas.height,frameCount=len(fs),fps=16,canvasWidth=fs[0]['canvas'][0],canvasHeight=fs[0]['canvas'][1],anchor=anchor,frames=rows))
 records.append(dict(key=key,source=m,indices=indices,anchor=anchor,additiveConversion=blend,runtimeSha256=hashlib.sha256(data).hexdigest()))
pack('p1','intro/body',[250,371],list(range(49,60)))
pack('p2','phase2/bakal_2p',[600,828],list(range(110,120)))
pack('wave','stomp/warstompshockdodge',[400,660],blend=True)
pack('glow','stomp/warstompglow',[600,800],blend=True)
pack('fireback','stomp/warstompflameback',[465,380],blend=True)
pack('firefront','stomp/warstompflamefront',[465,380],blend=True)
# Exact original jump warning wings + upward arrow; keep five native variants.
pack('jump_guide','stomp/jump-search/123',[100,113])
(OUT/'manifest.json').write_text(json.dumps(dict(effects=effects),separators=(',',':')))
(ROOT/'Tools/original-stomp-provenance.json').write_text(json.dumps(dict(reaEvidence='Artifacts/hit-reference-video-20261022/rea-open-result.json',observation='Native body pixels clearly show knee raised then foot planted. No original ANI or combat event binding recovered.',mapping=dict(p1=dict(source=list(range(49,60)),raised=[3,7],plant=8,recovery=[9,10],footAnchor=[250,371],impactContact=[360,346]),p2=dict(source=list(range(110,120)),raised=[3,6],plant=7,recovery=[8,9],footAnchor=[600,828],impactContact=[725,817])),limitation='Selected animation and event timing are authored reconstruction. Impact contact is visually estimated in native image canvas; multiply by rendered actor scale and mirror X with facing. FX anchors and additive conversion are inferred, not recovered original shader/script.',records=records),indent=2))
print('Imported',len(effects),'atlases',sum(e['frameCount'] for e in effects),'frames')
