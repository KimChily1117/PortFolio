"""Check phase-two cinematic protection and battlefield switch in four rendered clients."""
import json,sys
from pathlib import Path
root=Path(sys.argv[1]).resolve();evidence=[]
for p in sorted(root.glob('p[1-4]')):
 rows=[json.loads(line) for line in (p/'recording/frames.jsonl').read_text(encoding='utf-8-sig').splitlines()]
 active=[r for r in rows if r.get('phaseTransition')]
 events=[line.split() for line in (p/'confirmed-audit.txt').read_text(encoding='utf-8-sig').splitlines() if line.startswith('event ')]
 start=active[0]['phaseStart'] if active else -1
 initial=active[0] if active else None
 stable=bool(active) and all(r['seconds']==initial['seconds'] and r['combatBossHp']==initial['combatBossHp'] and all(a['hp']==b['hp'] and a['room']==b['room'] and a['x']==b['x'] and a['y']==b['y'] for a,b in zip(r['actors'],initial['actors'])) for r in active)
 boundaries={name:[int(e[2]) for e in events if e[3]=='Content'+name] for name in ['PhaseTransition','PhaseBattlefield','PhaseLanding','PhaseReady']}
 prohibited=[e for e in events if start<int(e[2])<=start+720 and e[3] in ('ContentDamage','ContentSkill','ContentPattern','ContentRoomPattern','ContentMeteorImpact','ContentStompImpact')]
 evidence.append(dict(client=p.name,slot=rows[0]['local'],start=start,samples=len(active),range=[min((r['tick']-start for r in active),default=-1),max((r['tick']-start for r in active),default=-1)],protected=stable,prohibited=prohibited,boundaries=boundaries,
  battlefieldCorrect=bool(active) and all(r['battlefield']==(r['tick']-start>=480) for r in active),
  resumed=any(r['tick']>start+720 and r['phase']==2 and r['battlefield'] and r['seconds']>initial['seconds'] for r in rows) if initial else False))
starts=[e['start'] for e in evidence]
checks=dict(four_slots=sorted(e['slot'] for e in evidence)==[0,1,2,3],same_start=len(set(starts))==1 and bool(starts) and starts[0]>=840,
 recorded=all(e['samples']>=40 and e['range'][0]<120 and e['range'][1]>660 for e in evidence),
 protected=all(e['protected'] and not e['prohibited'] for e in evidence),battlefield_visual_boundary=all(e['battlefieldCorrect'] for e in evidence),
 confirmed_boundaries=all(all(e['boundaries'][name]==[e['start']+offset] for name,offset in [('PhaseTransition',0),('PhaseBattlefield',480),('PhaseLanding',636),('PhaseReady',720)]) for e in evidence),
 resumed=all(e['resumed'] for e in evidence))
report=dict(passed=all(checks.values()),checks=checks,evidence=evidence,scope='Four local Unity clients through delayed transport. Actual rendered state and confirmed server events; not original ANI equivalence.')
(root/'phase2-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2));sys.exit(0 if report['passed'] else 1)
