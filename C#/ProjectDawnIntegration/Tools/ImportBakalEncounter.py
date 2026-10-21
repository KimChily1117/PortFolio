"""Bakal first encounter: original decoded pixels, authored reference-video timeline.
REA's retained NPK rejection is documented in original-encounter-provenance.json.
Reads only pinned extraction evidence; no original game files are modified.
"""
from pathlib import Path
import hashlib,io,json
from PIL import Image,ImageChops,ImageOps,ImageDraw
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'Artifacts/bakal-encounter-review-20261022/intro'
OUT=ROOT/'UnityClient/Assets/Resources/BakalEncounter';OUT.mkdir(exist_ok=True)
records=[];effects=[]
def additive(im):
 raw=bytearray(im.tobytes())
 for n in range(0,len(raw),4):
  r,g,b,a=raw[n:n+4];peak=max(r,g,b)
  if peak:raw[n:n+4]=bytes((round(r*255/peak),round(g*255/peak),round(b*255/peak),round(a*peak/255)))
  else:raw[n+3]=0
 return Image.frombytes('RGBA',im.size,bytes(raw))

def opaque_glass(im):
 # Black is the original additive backdrop, not part of the shard. Keep its
 # native dark dragon-face RGB, remove only exterior black, fill interior holes.
 r,g,b,a=im.split();peak=ImageChops.lighter(ImageChops.lighter(r,g),b)
 mask=peak.point(lambda v:255 if v>24 else 0)
 padded=ImageOps.expand(mask,1,fill=0);holes=ImageOps.invert(padded)
 ImageDraw.floodfill(holes,(0,0),0)
 mask=ImageChops.lighter(padded,holes).crop((1,1,im.width+1,im.height+1))
 return Image.merge('RGBA',(r,g,b,mask))
def pack(key,group,indices,anchor,blend=False,source_meta=None,filename_pattern="{i:03}.png"):
 meta=source_meta if source_meta is not None else json.loads((SRC/group/'metadata.json').read_text());images=[];frames=[]
 for i in indices:
  fr=meta['frames'][i];p=SRC/group/filename_pattern.format(i=i);assert hashlib.sha256(p.read_bytes()).hexdigest()==fr['sha256'];im=Image.open(p).convert('RGBA')
  if key in ('glass','glass_b','glass_reflection'):
   im=opaque_glass(im)
  elif blend:im=additive(im)
  images.append(im);frames.append(fr)
 width=2048;x=y=h=0;positions=[]
 for im in images:
  if x+im.width>width:x=0;y+=h+2;h=0
  positions.append((x,y));x+=im.width+2;h=max(h,im.height)
 atlas=Image.new('RGBA',(width,y+h));rows=[]
 for n,(im,fr,(x,y)) in enumerate(zip(images,frames,positions)):
  atlas.paste(im,(x,y));rows.extend([n,x,y,im.width,im.height,*fr['offset']])
 buf=io.BytesIO();atlas.save(buf,format='PNG');data=buf.getvalue();(OUT/f'{key}.bytes').write_bytes(data)
 effects.append(dict(id='enc_'+key,resource='BakalEncounter/'+key,width=width,height=atlas.height,frameCount=len(frames),fps=16,canvasWidth=frames[0]['canvas'][0],canvasHeight=frames[0]['canvas'][1],anchor=anchor,frames=rows))
 records.append(dict(key=key,source=meta,indices=indices,anchor=anchor,runtimeSha256=hashlib.sha256(data).hexdigest(),additiveConversion=blend and key not in ('glass','glass_b','glass_reflection'),opaqueGlass=key in ('glass','glass_b','glass_reflection')))
pack('seated','bakal_intro',[0,1],[960,1295])
pack('stand','bakal_intro',list(range(2,7)),[960,870])
pack('reveal','bakal_intro',list(range(7,13)),[960,870])
pack('wine','bakal_wine',list(range(18,23)),[345,223])
pack('glass','glassa',list(range(33)),[390,193],True)
pack('glass_b','glassb',list(range(35)),[390,193],True)
pack('glass_reflection','glassc',list(range(35)),[390,193],True)
pack('splash','winesplash',list(range(35)),[452,215],True)
pack('particle','wineparticle',list(range(24)),[465,198],True)
pack('landing_a','landinga',list(range(18)),[89,96],True)
pack('landing_b','landingb',list(range(16)),[112,139],True)
pack('leap','body',[98,99,100,101,102,103,104,57,58,65,66,67,68,0],[250,371])
# 6 original adjacent incoming-room strips form the throne scene.
# Native images share 600px height; widths include a one-pixel join in strip2.
meta=json.loads((SRC/'throne/metadata.json').read_text());tile=next(m for m in meta if m['member'].endswith('/tile.img'))
for n,anchor in [(1,[150,170]),(2,[110,172])]:
 torch=next(m for m in meta if m['member'].endswith('/torchlightthrone_dodge'+str(n)+'.img'))
 pack('torch'+str(n),'throne',list(range(len(torch['frames']))),anchor,True,torch,'torchlightthrone_dodge'+str(n)+'-{i:03}.png')
ims=[]
for fr in tile['frames']:
 p=SRC/'throne'/f"tile-{fr['index']:03}.png";assert hashlib.sha256(p.read_bytes()).hexdigest()==fr['sha256'];ims.append(Image.open(p).convert('RGBA'))
scene=Image.new('RGBA',(sum(im.width for im in ims),600));far=Image.open(SRC/'throne/back_far-000.png').convert('RGBA').resize(scene.size);scene.alpha_composite(far);x=0
for im in ims:scene.alpha_composite(im,(x,0));x+=im.width
scene.save(OUT/'throne.png')
records.append(dict(key='throne',source=meta,composition='6 tile strips sequential x, back_far behind transparent arches; demo camera framing',runtimeSha256=hashlib.sha256((OUT/'throne.png').read_bytes()).hexdigest()))
(OUT/'manifest.json').write_text(json.dumps(dict(effects=effects),separators=(',',':')))
(ROOT/'Tools/original-encounter-provenance.json').write_text(json.dumps(dict(referenceVideo='C:/Users/YOUR_USER/Videos/2026-10-22 04-07-42.mp4',referenceStart=3,referenceLanding=14.8,duration=14,reflectionEvidence=dict(member='glassc.img',sourceVideoSeconds=[8.2,9.4],demoStart=5.08,demoEnd=6.43,note='Dragon face is baked into original glassc frames; surrounding glassb also original. Straight-alpha approximation; original shader/script unknown.'),reaEvidence='Artifacts/hit-reference-video-20261022/rea-open-result.json',decoder='E:/task/DnfRaidDemo/asset-work/reference/PyDnfEx',limitations='Original pixels verified. Camera paths, ANI timing, anchors and body leap mapping are authored visual reconstruction from user footage, not recovered original scripts. No physical knockback inferred.',records=records),indent=2))
print('Imported',len(effects),'atlases',sum(e['frameCount'] for e in effects),'frames and original throne scene')
