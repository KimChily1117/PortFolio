"""Audit the intro boundary from real four-client screenshots/timestamps and server events."""
import json,sys,re
from pathlib import Path
root=Path(sys.argv[1]).resolve();streams=[];evidence=[]
for p in sorted(root.glob('p[1-4]')):
 rows=[json.loads(x) for x in (p/'recording/frames.jsonl').read_text(encoding='utf-8-sig').splitlines()]
 intro=[r for r in rows if 0<=r['tick']<840]
 events=[x.split() for x in (p/'confirmed-audit.txt').read_text(encoding='utf-8-sig').splitlines() if x.startswith('event ')]
 initial=intro[0]['actors'] if intro else []
 audit=(p/'flow-audit.txt').read_text(encoding='utf-8-sig')
 cues=[(int(a),float(b)) for a,b in re.findall(r'MatchingCountCue stage=(\d+) age=([\d.]+)',audit)]
 def stamp(name):
  found=re.findall(r'^([\d.]+) '+name,audit,re.M);return float(found[0]) if found else -1
 matchBegin=stamp('MatchingCountdownBegin');matchEnd=stamp('MatchingCountdownEnd');transition=stamp('TransitionBegin Demo');encounter=stamp('EncounterStart')
 evidence.append(dict(cues=cues,matchingSeconds=matchEnd-matchBegin,matchingBeforeLoading=matchBegin>=0 and matchEnd>=matchBegin+3.99 and transition>=matchEnd and encounter>transition,matchingScreens=all((p/f'matching-count-{n}.png').exists() for n in range(4)),client=p.name,local=rows[0]['local'],introFrames=len(intro),introRange=[min((r['tick'] for r in intro),default=-1),max((r['tick'] for r in intro),default=-1)],
  protected=bool(intro) and all(r['seconds']==0 and all(a['sequence']==0 and a['hp']==initial[i]['hp'] and a['x']==initial[i]['x'] and a['y']==initial[i]['y'] for i,a in enumerate(r['actors'])) for r in intro),
  resumed=any(r['tick']>840 and r['seconds']>0 for r in rows),
  landing=[int(e[2]) for e in events if e[3]=='ContentEncounterLanding'],ready=[int(e[2]) for e in events if e[3]=='ContentEncounterReady'],
  premature=[e for e in events if int(e[2])<=840 and e[3] in ('ContentSkill','ContentDamage','ContentPattern','ContentRoomPattern','ContentMeteorImpact')]))
 streams.append(rows)
checks=dict(four_slots=sorted(e['local'] for e in evidence)==[0,1,2,3],
 original_countdown_and_start=all([a for a,b in e['cues']]==[0,1,2,3] and all(a<=b<a+.35 for a,b in e['cues']) for e in evidence),
 matching_before_scene=all(e['matchingBeforeLoading'] and e['matchingScreens'] and 3.99<=e['matchingSeconds']<4.3 for e in evidence),
 intro_recorded_on_all_four=all(e['introFrames']>=30 and e['introRange'][0]<180 and e['introRange'][1]>780 for e in evidence),
 no_combat_or_timer_during_intro=all(e['protected'] and not e['premature'] for e in evidence),
 same_landing_tick=all(e['landing']==[708] for e in evidence),same_ready_tick=all(e['ready']==[840] for e in evidence),all_resume=all(e['resumed'] for e in evidence))
report=dict(passed=all(checks.values()),checks=checks,evidence=evidence,scope='Actual rendered four-client state + server-confirmed cues; not frame-perfect artwork comparison')
(root/'encounter-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2));sys.exit(0 if report['passed'] else 1)
