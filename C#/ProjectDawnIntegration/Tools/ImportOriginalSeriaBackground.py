"""Read original Seria background, preserving the existing town scene's crop size."""
import hashlib, io, json, sys
from pathlib import Path
sys.path.insert(0,'E:/task/DnfRaidDemo/asset-work/reference/PyDnfEx')
from pydnfex.npk import NPK
from pydnfex.img import IMGFactory

ROOT=Path(__file__).resolve().parents[1]
source=Path('D:/Games/DNF/ImagePacks2/sprite_map_town_01_seria_room.NPK')
with source.open('rb') as stream:
    entry=next(e for e in NPK.open(stream).files if e.name.endswith('/backgruond.img'))
    data=entry.data
    decoded=IMGFactory.open(io.BytesIO(data))
    picture=decoded.build(decoded.images[0]).convert('RGBA')
    record=dict(npk=str(source),npkSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                member=entry.name,offset=entry._offset,length=len(data),
                imgSha256=hashlib.sha256(data).hexdigest(),frame=0,decodedSize=list(picture.size))
destination=ROOT/'UnityClient/Assets/Resources/OriginalWorldEffects/seria_room_exact.png'
destination.parent.mkdir(parents=True,exist_ok=True)
picture.crop((82,0,1036,560)).save(destination)
record.update(resource='OriginalWorldEffects/seria_room_exact',crop=[82,0,1036,560],
              cropNote='Demo crop matching existing scene image dimensions. No resampling.',
              runtimeSha256=hashlib.sha256(destination.read_bytes()).hexdigest())
(ROOT/'Tools/original-seria-background-provenance.json').write_text(json.dumps(record,indent=2))
print('Imported NPK Seria background: crop 954x560, original pixels unchanged')
