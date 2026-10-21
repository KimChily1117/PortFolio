from pathlib import Path
import sys,io,json,hashlib,math
from PIL import Image,ImageDraw
sys.path.insert(0,'E:/task/DnfRaidDemo/asset-work/reference/PyDnfEx')
from pydnfex.npk import NPK
from pydnfex.img import IMGFactory,ImageLink
root=Path('D:/Games/DNF/ImagePacks2');out=Path('E:/task/ProjectDawnIntegration/Artifacts/original-combat-hud-20261022');out.mkdir(parents=True,exist_ok=True)
selections={'sprite_interface2_hud.NPK':['hud02.img','hpmp_vessel.img','hudskill.img','hud_bar_ani.img'],'sprite_interface2_hud_2026.NPK':['hud.img','hpmp_vessel.img','hpmp_gaugebar.img'],'sprite_interface_newstyle_windows_hud.NPK':['party_info.img'],'sprite_interface_newstyle_windows_monster_hp.NPK':['monster_hp.img','monster_hp_ver2.img'],'sprite_item_stackable.NPK':['consumption.img']}
selections.update({'sprite_interface2_party_partybase.NPK':['partybasesmall.img','party_base.img'], 'sprite_interface2_party_partyframe.NPK':['00_partyframe.img','01_partyframe.img','partyframe.img','partyframe_healingfeedback.img']})
records=[]
for pack,names in selections.items():
 with (root/pack).open('rb') as f:
  npk=NPK.open(f)
  for member in names:
   entry=next(e for e in npk.files if e.name.split('/')[-1]==member);data=entry.data;img=IMGFactory.open(io.BytesIO(data))
   name=pack.replace('.NPK','')+'__'+member.replace('.img','');folder=out/name;folder.mkdir(exist_ok=True);rows=[];thumbs=[]
   count=min(len(img.images),200) if member=='consumption.img' else len(img.images)
   for i,frame in enumerate(img.images[:count]):
    real=frame.final_image if isinstance(frame,ImageLink) else frame
    im=img.build(frame).convert('RGBA');path=folder/f'{i:03}.png';im.save(path)
    rows.append(dict(index=i,width=im.width,height=im.height,offset=[real.x,real.y],canvas=[real.mw,real.mh],sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    thumbs.append(im)
   meta=dict(npk=pack,npkSha256=hashlib.sha256((root/pack).read_bytes()).hexdigest(),member=entry.name,offset=entry._offset,length=len(data),memberSha256=hashlib.sha256(data).hexdigest(),totalFrames=len(img.images),frames=rows)
   (folder/'metadata.json').write_text(json.dumps(meta,indent=2));records.append(meta)
   size=160;cols=6;sheet=Image.new('RGB',(cols*size,math.ceil(count/cols)*(size+24)),(40,43,54));d=ImageDraw.Draw(sheet)
   for i,im in enumerate(thumbs):
    cell=im.copy();cell.thumbnail((size-6,size-6));x=(i%cols)*size;y=(i//cols)*(size+24);sheet.paste(cell,(x+(size-cell.width)//2,y+(size-cell.height)//2),cell);d.text((x+3,y+size),f'{i} {im.width}x{im.height}',fill='white')
   sheet.save(out/(name+'-sheet.png'));print(name,count,'of',len(img.images),flush=True)
(out/'inventory.json').write_text(json.dumps(records,indent=2))
