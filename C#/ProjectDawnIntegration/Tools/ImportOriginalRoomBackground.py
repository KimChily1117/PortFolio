"""Compose original Bakal corridor sprites for the demo's side rooms.

The source artwork is extracted. Map placement is authored here, not claimed to
reproduce DNF's original .map scripts. Source installation remains read-only.
"""
from pathlib import Path
import hashlib, json
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Artifacts/world-resource-reference-20261022/corridor'
OUT=ROOT/'UnityClient/Assets/Resources/OriginalWorldEffects'
OUT.mkdir(parents=True,exist_ok=True)
provenance=json.loads((SOURCE/'provenance.json').read_text())
for member in provenance:
    for frame in member['frames']:
        assert hashlib.sha256((ROOT/frame['path']).read_bytes()).hexdigest()==frame['sha256']

def frame(name,index=0):return Image.open(SOURCE/f'{name}-{index}.png').convert('RGBA')
sky_path=Path('E:/task/DnfRaidDemo/asset-work/decoded/arena_back_far/0000.png')
sky=Image.open(sky_path).convert('RGBA')
canvas=Image.new('RGBA',(1280,720),(37,28,43,255))
for x in range(0,1280,sky.width):canvas.alpha_composite(sky,(x,0))
for x,index in [(-90,0),(720,1)]:canvas.alpha_composite(frame('back_middle',index),(x,72))
# Keep the same floor depth (423..720) as the authoritative arena projection.
for x in range(0,1280,224):
    canvas.alpha_composite(frame('tile',(x//224)%2),(x,423))
    canvas.alpha_composite(frame('tile_ex',(x//224)%2),(x,600))
wall=frame('object_wall',0)
for x in [-280,319,918]:canvas.alpha_composite(wall,(x,184))
# Side architecture does not cover the walkable central lane.
left=frame('object_building',0).resize((220,296),Image.Resampling.NEAREST)
canvas.alpha_composite(left,(-110,170))
canvas.alpha_composite(left.transpose(Image.Transpose.FLIP_LEFT_RIGHT),(1170,170))
destination=OUT/'bakal_corridor.png';canvas.convert('RGB').save(destination)
canvas.convert('RGB').save(SOURCE/'composed-preview.jpg')
(ROOT/'Tools/original-room-background-provenance.json').write_text(json.dumps(dict(
    resource='OriginalWorldEffects/bakal_corridor',size=[1280,720],
    runtimeSha256=hashlib.sha256(destination.read_bytes()).hexdigest(),
    sourceRecords=provenance,
    sky=dict(path=str(sky_path),pngSha256=hashlib.sha256(sky_path.read_bytes()).hexdigest(),
             originalMemberMetadata='E:/task/DnfRaidDemo/asset-work/decoded/arena_back_far/metadata.json'),
    limitation='Original sprites, demo-authored layout; original map scripts were not recovered.'),indent=2))
print('Imported original-art Bakal corridor composition, 1280x720')
