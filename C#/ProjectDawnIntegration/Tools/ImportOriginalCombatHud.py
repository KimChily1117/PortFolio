"""Import bounded original DNF HUD frames. Source NPKs are opened read-only.
REA 6.5 rejected this proprietary package format; existing PyDnfEx decoder is used.
All native indices, file ranges, offsets and SHA-256 are retained. Visual roles below
are human-inspected reconstruction choices, not recovered original UI/ANI code.
"""
from pathlib import Path
import hashlib,io,json,sys
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
SOURCE=Path('D:/Games/DNF/ImagePacks2')
DECODER=Path('E:/task/DnfRaidDemo/asset-work/reference/PyDnfEx')
sys.path.insert(0,str(DECODER))
from pydnfex.npk import NPK
from pydnfex.img import IMGFactory,ImageLink
OUT=ROOT/'UnityClient/Assets/Resources/OriginalCombatHud';OUT.mkdir(parents=True,exist_ok=True)
records=[];textures=[];cache={}
def load(pack,member):
 key=(pack,member)
 if key not in cache:
  path=SOURCE/pack
  with path.open('rb') as f:
   npk=NPK.open(f);entry=next(e for e in npk.files if e.name==member);data=entry.data
  cache[key]=(IMGFactory.open(io.BytesIO(data)),dict(npk=str(path),npkSha256=hashlib.sha256(path.read_bytes()).hexdigest(),member=member,memberOffset=entry._offset,memberLength=len(data),memberSha256=hashlib.sha256(data).hexdigest()))
 return cache[key]
def save(name,pack,member,index,role):
 img,source=load(pack,member);frame=img.images[index];real=frame.final_image if isinstance(frame,ImageLink) else frame;im=img.build(frame).convert('RGBA')
 buf=io.BytesIO();im.save(buf,format='PNG');png=buf.getvalue();(OUT/(name+'.bytes')).write_bytes(png)
 item=dict(id=name,resource='OriginalCombatHud/'+name,width=im.width,height=im.height,index=index,offset=[real.x,real.y],canvas=[real.mw,real.mh],role=role)
 textures.append(item);records.append(dict(**item,source=source,sha256=hashlib.sha256(png).hexdigest()))
 return im
hud='sprite_interface2_hud.NPK';hm='sprite/interface2/hud/hud02.img'
for name,index,role in [('legacyBottom',0,'Original 820x81 full legacy bottom strip'),('hpOrb',1,'Original red 56x56 orb; vertical clipping follows demo HP'),('mpOrb',2,'Original blue 56x56 orb'),('itemTray',132,'Six-item quickslot tray233x75'),('skillTray',133,'Two-row skill tray233x77'),('slot',140,'Original34x34 quickslot border'),('orbDamageMask',36,'Original white circular mask; damage-trail placement is demo-authored')]:save(name,hud,hm,index,role)
modern='sprite_interface2_hud_2026.NPK';mm='sprite/interface2/hud/2026/hud.img'
for name,index,role in [('bottomFrame',2,'Modern378x86 dual-orb center panel'),('bottomFrameRaised',3,'Modern378x91 raised-center variant'),('bottomShadow',0,'Original center-panel shadow362x89'),('emptyOrb',4,'Original dark orb64x64'),('slotSmall',14,'Original28x28 slot'),('slotEmpty',13,'Original28x28 empty slot'),('bottomFade',54,'Original800x65 bottom fade'),('skillSlot32',98,'Original32x32 slot')]:save(name,modern,mm,index,role)
for name,index,role in [('hpGaugeBack',0,'Original240x27 bar background'),('hpGaugeFill',1,'Original white228x12 bar; tint and clip per current HP'),('hpGaugeHighlight',2,'Original212x3 highlight')]:save(name,modern,'sprite/interface2/hud/2026/hpmp_gaugebar.img',index,role)
monster='sprite_interface_newstyle_windows_monster_hp.NPK';bm='sprite/interface/newstyle/windows/monster_hp/monster_hp.img'
for name,index,role in [('bossFrame',9,'Original628x36 boss bar frame'),('bossBack',11,'Original630x36 dark boss bar'),('bossRed',2,'Original597x9 redHP'),('bossWhite',7,'Original597x9 white damage trail'),('bossOrange',3,'Original597x9 orangeHP'),('bossGreen',4,'Original597x9 greenHP')]:save(name,monster,bm,index,role)
party='sprite_interface2_party_partybase.NPK';pm='sprite/interface2/party/partybase/party_base.img'
bm2='sprite/interface/newstyle/windows/monster_hp/monster_hp_ver2.img'
for name,index,role in [('bossRaidFrame',11,'Original534x65 gold boss surround; opaque interior must be drawn BEFORE fill'),('bossRaidRed',13,'Original489x20 segmented red HP'),('bossRaidWhite',18,'Original489x20 white damage trail'),('bossStaggerFrame',20,'Original214x16 gold stagger surround'),('bossStaggerGold',22,'Original139x8 segmented gold stagger')]:save(name,monster,bm2,index,role)
for name,index,role in [('partyBase',0,'Original131x29 party portrait+health base'),('partyHp',1,'Original99x3 redHP'),('partyMp',2,'Original99x3 blueMP'),('partyTrail',8,'Original99x3 gray damage feedback')]:save(name,party,pm,index,role)
save('partyFrame','sprite_interface2_party_partyframe.NPK','sprite/interface2/party/partyframe/partyframe.img',22,'Original140x44 brown frame; paired with player-specific head portrait by application')
save('potionHp','sprite_item_stackable.NPK','sprite/item/stackable/consumption.img',40,'Visually inspected red vial with plus28x28; exact original item identity and effect are unknown. Used for demo health potion.')
save('potionMp','sprite_item_stackable.NPK','sprite/item/stackable/consumption.img',41,'Visually inspected blue vial28x28; exact original identity unknown; optional unused asset.')
# The modern vessel contains actual grayscale orb surface animation. Preserve the
# pixels; application may tint to HP/MP. Original loop FPS has not been recovered.
pack=modern;member='sprite/interface2/hud/2026/hpmp_vessel.img';img,source=load(pack,member);indices=list(range(1,33));atlas=Image.new('RGBA',(8*56,4*56));frames=[]
for n,index in enumerate(indices):
 im=img.build(img.images[index]).convert('RGBA');x=(n%8)*56;y=(n//8)*56;atlas.paste(im,(x,y));frames.extend([index,x,y,im.width,im.height])
buf=io.BytesIO();atlas.save(buf,format='PNG');(OUT/'orbSurface.bytes').write_bytes(buf.getvalue())
animation=dict(id='orbSurface',resource='OriginalCombatHud/orbSurface',width=atlas.width,height=atlas.height,frames=frames,frameStride=5,firstFrame=1,frameCount=32)
records.append(dict(**animation,source=source,sha256=hashlib.sha256(buf.getvalue()).hexdigest(),role='Original grayscale orb animation; playback rate and tint are reconstruction choices'))
manifest=dict(textures=textures,animations=[animation],note='Original pixels with native aspect ratios. UI composition, HP clipping/trail timing, tint and animation FPS are demo-authored.')
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
(ROOT/'Tools/original-combat-hud-provenance.json').write_text(json.dumps(dict(decoder=str(DECODER),decoderSources=[dict(path=str(p.relative_to(DECODER)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted((DECODER/'pydnfex').rglob('*.py'))],reaLimitation='NPK unsupported; retained rejection Artifacts/hit-reference-video-20261022/rea-open-result.json',records=records),indent=2,ensure_ascii=False),encoding='utf-8')
print('Imported',len(textures),'original HUD textures and1 orb animation atlas (32 frames).')
