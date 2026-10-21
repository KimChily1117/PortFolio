"""Verify the new boss sequence using actual four-client capture metadata and final events."""
import json,sys
from pathlib import Path
root=Path(sys.argv[1]).resolve();streams=[]
for folder in root.glob('p[1-4]'):
 rows=[json.loads(line) for line in (folder/'recording/frames.jsonl').read_text(encoding='utf-8-sig').splitlines()]
 streams.append((rows[0]['local'],folder,rows))
streams.sort();allrows=[r for _,_,rs in streams for r in rs]
locked=[]
for line in (streams[0][1]/'confirmed-audit.txt').read_text().splitlines():
 parts=line.split()
 if len(parts)>=6 and parts[0]=='event' and parts[3]=='ContentChargeLocked':locked.append({'tick':int(parts[2]),'target':int(parts[5])})
groups=[]
for hit in locked:
 if not groups or hit['tick']-groups[-1][-1]['tick']!=162:groups.append([])
 groups[-1].append(hit)
checks={
 'four_independent_clients':[s[0] for s in streams]==[0,1,2,3],
 'charge_committed_on_all_clients':all(8 in json.loads((p/'network-result.json').read_text(encoding='utf-8-sig'))['patterns'] for _,p,_ in streams),
 'complete_three_charge_sequence':any(len(g)==3 for g in groups),
 'each_charge_targets_real_slot':bool(locked) and all(0<=x['target']<4 for x in locked),
 'three_warning_numbers_in_actual_draws':all(any(r['room']==4 and r['chargeWave']==wave and 0<=r['chargeAge']<r['chargeTell'] and r['chargeTarget']>=0 for r in allrows) for wave in [1,2,3]),
 'target_mark_visible_in_boss_room':any(r['room']==4 and r['chargeTarget']>=0 and r['actors'][r['chargeTarget']]['room']==4 and r['chargeAge']<r['chargeTell'] for r in allrows),
 'fire_trail_actually_drawn':sum(r['chargeTrailCount']>0 for r in allrows)>10,
 'trail_disappears_while_staying_in_boss_room':any(a['room']==b['room']==4 and a['chargeTrailCount']>0 and b['chargeTrailCount']==0 for _,_,rs in streams for a,b in zip(rs,rs[1:])),
}
report={'passed':all(checks.values()),'checks':checks,'chargeGroups':groups,'maximumVisibleTrailPatches':max(r['chargeTrailCount'] for r in allrows),'scope':'Four local Unity processes; 30 ms each direction; authoritative .NET 10 server; no HP or invulnerability overrides.'}
(root/'charge-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2));sys.exit(0 if report['passed'] else 1)
