"""Actual Unity result timing, local BGM switch and server return evidence."""
import json,re,sys
from pathlib import Path
root=Path(sys.argv[1]);evidence=[]
for p in sorted(root.glob('p[1-4]')):
    audit=(p/'flow-audit.txt').read_text(encoding='utf-8-sig')
    def event(name):
        return [float(t) for t in re.findall(r'^([\d.]+) '+name,audit,re.M)]
    start=event('ResultPresentationStart')[0];end=event('ClearCinematicEnd')[0]
    music=event('ResultBgm Liberation')[0]
    cards=[(int(s),float(a)) for s,a in re.findall(r'ResultCardRevealed slot=(\d+) age=([\d.]+)',audit)]
    town=event('TransitionBegin DawnTown')[-1]
    sfx=[(key,clip,float(age)) for key,clip,age in re.findall(r'ResultSfx key=(\w+) clip=(\w+) age=([\d.]+)',audit)]
    evidence.append(dict(client=p.name,sfx=sfx,afterVideo=start-end,musicOffset=music-start,cards=cards,returnAge=town-start))
checks=dict(four_clients=len(evidence)==4,
    start_after_video=all(0<=e['afterVideo']<.25 for e in evidence),
    liberation_at_start=all(abs(e['musicOffset'])<.05 for e in evidence),
    count_sound_order=all([x[0] for x in e['sfx'] if x[0].startswith('count')]==['count3','count2','count1'] for e in evidence),
    card_sound_order=all([x[0] for x in e['sfx'] if x[0].startswith(('unlock','open'))]==[x for i in range(4) for x in ('unlock'+str(i),'open'+str(i))] for e in evidence),
    slot_order=all([c[0] for c in e['cards']]==[0,1,2,3] for e in evidence),
    sequential_18=all(all(abs(c[1]-(6.3+i*1.8))<.2 for i,c in enumerate(e['cards'])) for e in evidence),
    fifteen_seconds_after_reveal=all(27.1<=e['returnAge']<=29 for e in evidence))
report=dict(passed=all(checks.values()),checks=checks,evidence=evidence,
    scope='Unity lifecycle timestamps and local AudioSource start log. Video/audio recording additionally provides visual and audible evidence.')
(root/'result-presentation-verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2));sys.exit(0 if report['passed'] else 1)
