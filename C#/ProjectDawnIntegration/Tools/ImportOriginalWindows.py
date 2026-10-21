"""Decode selected original inventory/skill-window PNGs; source install is read-only."""
from pathlib import Path
import hashlib,io,json,sys,uuid
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,'E:/task/DnfRaidDemo/asset-work/reference/PyDnfEx')
from pydnfex.npk import NPK
from pydnfex.img import IMGFactory,ImageLink
OUT=ROOT/'UnityClient/Assets/Resources/DawnWindows'
SOURCES=[
 ('sprite_interface.NPK','windowcommon.img','chrome',list(range(9))+[12,13,15,42,43,57,66,67,69]),
 ('sprite_interface_newstyle_windows_inventory.NPK','inventory.img','inventory',[11,18,19,20,21,32,39,49,53,65,82,88,103,110,131,148,174,175,181,202,215]),
 ('sprite_interface_newstyle_windows_inventory.NPK','inventorybackground.img','background',[0,1]),
 ('sprite_interface2_ui_skillshop2.NPK','skillshop2.img','skill',[5,6,7,8,9,10,16,18,19,20,21,27,51,80,82,86,95,109,110,111,112,113,116,117,118,119]),
]

def main():
 records=[]
 for pack,member,group,indices in SOURCES:
  path=Path('D:/Games/DNF/ImagePacks2')/pack
  pack_sha=hashlib.file_digest(path.open('rb'),'sha256').hexdigest()
  with path.open('rb') as f:
   entry=next(e for e in NPK.open(f).files if e.name.endswith('/'+member))
   img=IMGFactory.open(io.BytesIO(entry.data))
   for i in indices:
    fr=img.images[i];real=fr.final_image if isinstance(fr,ImageLink) else fr
    image=img.build(fr).convert('RGBA');p=OUT/group/f'{i:03}.png';p.parent.mkdir(parents=True,exist_ok=True);image.save(p)
    # Static window pieces are stored cropped: their decoded dimensions are the
    # authoritative aspect ratio. Do not fit the original animation canvas.
    meta=Path(str(p)+'.meta')
    if not meta.exists():
     guid=uuid.uuid5(uuid.NAMESPACE_URL,'dawn-original-window/'+group+'/'+str(i)).hex
     meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 12
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  maxTextureSize: 2048
  textureSettings:
    filterMode: 1
    wrapU: 1
    wrapV: 1
  nPOTScale: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0
    overridden: 0
''',encoding='utf-8')
    records.append(dict(pack=pack,packSha256=pack_sha,member=entry.name,
      memberSha256=hashlib.sha256(entry.data).hexdigest(),index=i,offset=[real.x,real.y],canvas=[real.mw,real.mh],
      size=list(image.size),output=str(p.relative_to(ROOT)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
 (ROOT/'Tools/original-window-provenance.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
 print('Imported',len(records),'original static UI pieces')
if __name__=='__main__':main()
