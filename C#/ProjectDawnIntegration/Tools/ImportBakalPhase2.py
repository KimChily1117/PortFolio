"""Original Bakal phase-two art for the demo's authored synchronized timeline.
Retains source coordinates/hashes; no original ANI/map timing recovered.
"""
from pathlib import Path
import hashlib,io,json
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'Artifacts/bakal-encounter-review-20261022/phase2'
OUT=ROOT/'UnityClient/Assets/Resources/BakalPhase2';OUT.mkdir(exist_ok=True)
records=[];effects=[]
def additive(im):
 raw=bytearray(im.tobytes())
 for n in range(0,len(raw),4):
  r,g,b,a=raw[n:n+4];peak=max(r,g,b)
  if peak:raw[n:n+4]=bytes((round(r*255/peak),round(g*255/peak),round(b*255/peak),round(a*peak/255)))
  else:raw[n+3]=0
 return Image.frombytes('RGBA',im.size,bytes(raw))
def pack(key,group,anchor,blend=False,indices=None):
 meta=json.loads((SRC/group/'metadata.json').read_text());indices=indices or list(range(len(meta['frames'])));images=[];frames=[]
 for i in indices:
  fr=meta['frames'][i];p=SRC/group/f'{i:03}.png';assert hashlib.sha256(p.read_bytes()).hexdigest()==fr['sha256'];im=Image.open(p).convert('RGBA');images.append(additive(im) if blend else im);frames.append(fr)
 width=4096;x=y=h=0;positions=[]
 for im in images:
  if x+im.width>width:x=0;y+=h+2;h=0
  positions.append((x,y));x+=im.width+2;h=max(h,im.height)
 atlas=Image.new('RGBA',(width,y+h));rows=[]
 for n,(im,fr,(x,y)) in enumerate(zip(images,frames,positions)):
  atlas.paste(im,(x,y));rows.extend([n,x,y,im.width,im.height,*fr['offset']])
 buf=io.BytesIO();atlas.save(buf,format='PNG');data=buf.getvalue();(OUT/f'{key}.bytes').write_bytes(data)
 effects.append(dict(id='p2_'+key,resource='BakalPhase2/'+key,width=width,height=atlas.height,frameCount=len(frames),fps=16,canvasWidth=frames[0]['canvas'][0],canvasHeight=frames[0]['canvas'][1],anchor=anchor,frames=rows))
 records.append(dict(key=key,source=meta,indices=indices,anchor=anchor,runtimeSha256=hashlib.sha256(data).hexdigest(),additiveConversion=blend))
pack('incoming','bakal_2p_incoming',[0,0])
pack('rush_background','bakal_2p_change_b',[0,0])
pack('fall_background','bakal_2p_back',[0,0])
pack('fire_face_a','bakalface01',[210,210],True)
pack('fire_face_b','bakalface02',[250,250],True)
pack('face_particle','bakalface02particle',[250,250],True)
pack('eye_light','faceeyelight',[265,132],True)
pack('face_fractal','facefractal',[344,143],True)
pack('screen','2pagescreen01d',[0,0],True)
# Descent frames chosen from matching airborne silhouette; ANI sequencing unknown.
pack('descent','bakal_2p',[600,820],False,[60,61,62,63,64,65,66,67,68,69,78,79,80,0])
# Original ruined battlefield, composed to the demo's 1280x720 projection.
B=SRC/'battlefield';bm=json.loads((B/'metadata.json').read_text())
for m in bm:
 for f in m['frames']:assert hashlib.sha256((B/f['path']).read_bytes()).hexdigest()==f['sha256']
def fr(stem,n=0):return Image.open(B/f'{stem}-{n:03}.png').convert('RGBA')
scene=Image.new('RGBA',(1280,720),(44,31,40,255))
scene.alpha_composite(fr('back_far').resize((1280,512)),(0,-40))
# Reference shows a sparse distant skyline; back_middle is retained but not placed.
scene.alpha_composite(fr('castle',0).resize((220,307)),(530,22))
scene.alpha_composite(additive(fr('castle',3)).resize((144,219)),(563,52))
# Tile strip along rear parapet. Lower floor remains full lane width.
parapet=Image.new('RGBA',(2304,254))
for n in range(4):parapet.alpha_composite(fr('objecttile',n),(n*576,0))
scene.alpha_composite(parapet.crop((1024,0,2304,254)),(0,285))
# Preserve consecutive native parapet joins. Crossfade overlapping floor rows
# to avoid the demo composition exposing hard horizontal tile borders.
for y in [515,611,707]:
 for x in range(-64,1280,224):
  tile=fr('tile_ex',((x+64)//224)%4);alpha=tile.getchannel('A')
  for yy in range(24):
   for xx in range(tile.width):alpha.putpixel((xx,yy),round(alpha.getpixel((xx,yy))*yy/24))
  tile.putalpha(alpha);scene.alpha_composite(tile,(x,y))
for stem,n,x,y in [('object',3,-35,333),('object',4,1065,340),('bottomobject',0,125,482),('bottomobject',1,798,548),('bottomobject',2,438,641),('rock',3,1040,638),('rock',5,90,639)]:scene.alpha_composite(fr(stem,n),(x,y))
scene.convert('RGB').save(OUT/'battlefield.png')
scene.convert('RGB').save(B/'composed-preview.jpg')
records.append(dict(key='battlefield',source=bm,runtimeSha256=hashlib.sha256((OUT/'battlefield.png').read_bytes()).hexdigest(),composition='Original 2phase sky/castle/ruined parapet/tile sprites; demo-authored placement and scaling to 1280x720. Not recovered map script.'))
(OUT/'manifest.json').write_text(json.dumps(dict(effects=effects),separators=(',',':')))
(ROOT/'Tools/original-phase2-provenance.json').write_text(json.dumps(dict(referenceVideo='C:/Users/YOUR_USER/Desktop/새 폴더 (3)/바칼 2페 전화ㅓㄴ.mp4',referenceMetadata=json.loads((SRC/'provenance.json').read_text()),reaEvidence='Artifacts/hit-reference-video-20261022/rea-open-result.json',timelineSeconds=dict(darkening=[0,1.8],flash=[1.8,2.3],incoming=[2.3,4.4],whiteBranches=[4.4,5.3],fireFace=[4.4,5.8],blackout=[6,7.8],battlefieldReveal=[7.8,9.2],dragonDescent=[9.5,10.7]),limitations='Original pixels verified against retained PNG SHA256. Timings sampled at 2fps +/-0.25 sec. ANI, additive shader and map layout not recovered; cinematic is authored video reconstruction. Source video ends before final landing so continuation/landing timing is authored.',records=records),indent=2))
print('Imported',len(effects),'atlases',sum(e['frameCount'] for e in effects),'frames and phase2 battlefield')
