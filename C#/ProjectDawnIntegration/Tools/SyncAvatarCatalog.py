from pathlib import Path
import json
r=Path(__file__).resolve().parents[1]
b=json.loads((r/'UnityClient/Assets/Resources/Art/berserker.json').read_text(encoding='utf-8-sig'))
a=json.loads((r/'UnityClient/Assets/Resources/AvatarImport/catalog.json').read_text(encoding='utf-8-sig'))
allow={p['part']:[o['code'] for o in p['options']] for p in b['parts']};allow['weapon']=[w['code'] for w in b['weapons']]
for p in a['parts']:allow.setdefault(p['part'],[]).extend(o['code'] for o in p['options'] if o['code'] not in allow.get(p['part'],[]))
allow['weapon'].extend(w['code'] for w in a['weapons'] if w['code'] not in allow['weapon'])
(r/'Data/AppearanceAllowList.json').write_text(json.dumps(allow,ensure_ascii=False,indent=2),encoding='utf-8')
print('Approved appearance options:',{k:len(v) for k,v in allow.items()})
