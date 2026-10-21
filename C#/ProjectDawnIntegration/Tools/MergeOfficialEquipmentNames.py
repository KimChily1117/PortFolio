"""Merge independently paginated official public dictionary categories."""
from pathlib import Path
import csv,json

root=Path(__file__).resolve().parents[1]/'Research'/'equipment-web'
categories=['mt_10','mt_20','30_00','60_00','oath','oath_crystal','title']
merged={};coverage=[]
for category in categories:
    file=root/f'equipment-name-catalog-{category}.json'
    if not file.exists():
        coverage.append(dict(category=category,complete=False,count=0,reason='Not fetched'));continue
    data=json.loads(file.read_text(encoding='utf-8'))
    coverage.append(dict(category=category,complete=data['complete'],count=data['count'],pages=len(data['pages']),reason=data['reason'],file=file.name))
    for item in data['items']:
        key=item['itemId']
        if key not in merged:
            merged[key]=dict(item,sourceCategories=[category])
        elif category not in merged[key]['sourceCategories']:
            merged[key]['sourceCategories'].append(category)
items=list(merged.values())
result=dict(schemaVersion=1,sourcePage='https://df.nexon.com/guide/equipment',
    coverage=coverage,complete=all(c['complete'] for c in coverage),count=len(items),
    distinctNameCount=len({i['itemName'] for i in items}),items=items,
    scope='Public equipment dictionary pages for the seven listed categories; not original game binary extraction.',
    limits=['Pagination is not an atomic database snapshot. The site may change during collection.',
            'Item IDs are deduplicated across pages/categories; names may legitimately repeat.',
            'Set/enchantment endpoints, hidden items, and all Script.pvf records are outside scope.',
            'Image URLs recorded; only eight selected reward/existing equipment icons downloaded.'])
(root/'equipment-name-catalog.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
with (root/'equipment-name-catalog.csv').open('w',encoding='utf-8-sig',newline='') as f:
    fields=['itemId','itemName','itemType','itemDetailType','level','imageUrl']
    w=csv.DictWriter(f,fieldnames=fields);w.writeheader();w.writerows({k:i.get(k) for k in fields} for i in items)
print(json.dumps(dict(count=result['count'],distinctNameCount=result['distinctNameCount'],complete=result['complete'],coverage=coverage),ensure_ascii=True))
