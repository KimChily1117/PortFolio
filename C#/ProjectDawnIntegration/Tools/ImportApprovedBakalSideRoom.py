"""Install the side-room composition explicitly accepted in the 2026-10-22 chat."""
from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parents[1]
review=root/'Artifacts/bakal-encounter-review-20261022'
source=review/'bakal-side-room-candidate.png'
record=json.loads((review/'bakal-side-room-provenance.json').read_text(encoding='utf-8'))
assert hashlib.sha256(source.read_bytes()).hexdigest()==record['output']['sha256']
target=root/'UnityClient/Assets/Resources/OriginalWorldEffects/bakal_side_room.png'
shutil.copy2(source,target)
record['purpose']='User accepted side room; intro/phase-transition cinematics remain pending further review.'
record['runtimeResource']='OriginalWorldEffects/bakal_side_room'
(root/'Tools/original-bakal-side-room-provenance.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(target)
