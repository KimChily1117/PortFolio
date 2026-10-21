"""Compare evidence produced by four independent rendered Unity processes."""
import json
import sys
import re
from pathlib import Path
from Tools.MeteorVolleyEvidence import verify as verify_volley_evidence

root = Path(sys.argv[1]).resolve()
results, renders, audits, events = [], [], [], []
for slot in range(1, 5):
    folder = root / f'p{slot}'
    results.append(json.loads((folder / 'network-result.json').read_text(encoding='utf-8-sig')))
    renders.append(json.loads((folder / 'render-result.json').read_text(encoding='utf-8-sig')))
    rows = (folder / 'confirmed-audit.txt').read_text(encoding='utf-8-sig').splitlines()
    audits.append(dict(row.split(' ', 1) for row in rows if not row.startswith('event ')))
    events.append([row for row in rows if row.startswith('event ')])
common = set.intersection(*(set(a) for a in audits))
shortest = min(map(len, events))
checks = {
    'four_distinct_player_slots': sorted(r['slot'] for r in results) == [0, 1, 2, 3],
    'four_players_in_every_render': all(r['players'] == 4 for r in renders),
    'no_unity_errors': all(r['errors'] == 0 for r in renders),
    'rendered_frames': all(r['frames'] > 1000 and r['repaints'] > 1000 for r in renders),
    'inputs_from_all_clients': all(r['inputs'] > 100 for r in results),
    'no_admission_rejections': all(r['rejections'] == 0 for r in results),
    'no_protocol_abort': all(r.get('endReason', '') in (('', 'Clear', 'Clear_NoRewards', 'ValidationClear_NoRewards', 'Defeat', 'PartyReturn_NoRewards') if '--flow' in sys.argv else ('', 'Clear_NoRewards', 'ValidationClear_NoRewards', 'Defeat')) for r in results),
    'enough_common_snapshots': len(common) >= 50,
    'confirmed_snapshots_equal': all(len({a[t] for a in audits}) == 1 for t in common),
    'confirmed_event_prefix_equal': shortest > 50 and all(e[:shortest] == events[0][:shortest] for e in events),
    'all_21_skill_ids_confirmed': all(set(r['skills']) == set(range(21)) for r in results),
    'multiple_server_approved_patterns': all(len(r['patterns']) >= 3 for r in results),
    'jump_avoidance_confirmed': all(r['groundAvoids'] > 0 for r in results),
}
if all('luaRequests' in r for r in results):
    hosts=[r for r in results if r['luaRequests']>0]
    checks.update({
        'only_one_lua_host':len(hosts)==1,
        'lua_final_callback_received':len(hosts)==1 and hosts[0]['luaConfirmed']>0,
        'dawn_meteor_confirmed_on_all_four':all(6 in r['patterns'] for r in results),
        'meteor_shockwave_jump_confirmed':all(r['meteorAvoids']>0 for r in results),
    })
# Pattern 6 starts a volley. Only completed volleys are checked here; an
# encounter may end or the recording may stop during its final volley.
volleys=[]
current=None
for row in events[0][:shortest]:
    fields=row.split()
    if fields[3]=='ContentPattern':
        if current is not None: volleys.append(current)
        current=[] if float(fields[5])==6 else None
    elif fields[3]=='ContentMeteorImpact' and current is not None:
        current.append(int(fields[2]))
if current is not None and len(current)==4: volleys.append(current)
if all('luaRequests' in r for r in results):
    impact_ticks=[int(row.split()[2]) for row in events[0][:shortest] if row.split()[3]=='ContentMeteorImpact']
    shock_ticks=[int(row.split()[2]) for row in events[0][:shortest] if row.split()[3]=='ContentMeteorShockwave']
    checks['landing_and_shockwave_same_tick']=bool(impact_ticks) and impact_ticks==shock_ticks
    actor_ids=[]
    if '--flow' in sys.argv:
        for i in range(1,5):
            identities=re.findall(r'Entered RaidLoading object=(\d+)',(root/f'p{i}'/'player.log').read_text(encoding='utf-8-sig'))
            if len(identities)==1:actor_ids.append(int(identities[0]))
    volley_evidence=verify_volley_evidence(events[0][:shortest],actor_ids)
    checks['four_sequential_meteor_impacts']=volley_evidence['passed']
    checks['meteor_impact_interval_87_ticks']=bool(volleys) and all(abs(b-a-87)<=1 for v in volleys for a,b in zip(v,v[1:]))
if all('clockMappedInputs' in r for r in results):
    checks['all_four_using_measured_input_clocks']=all(r['clockMappedInputs']>100 and r['clockVersion']>=2 for r in results)
presentation=[]
if all('timelineSamples' in r for r in results):
    for slot in range(1,5):
        rows=[r.split() for r in (root/f'p{slot}'/'presentation-audit.txt').read_text(encoding='utf-8-sig').splitlines()]
        zones=[r for r in rows if r[0]=='zone']
        impacts={int(r[1]):int(r[2]) for r in rows if r[0]=='impact'}
        shocks={int(r[1]):int(r[2]) for r in rows if r[0]=='shock'}
        avoids={int(r[1]) for r in rows if r[0]=='avoid'}
        safe={int(r[1]) for r in rows if r[0]=='safe'} & set(shocks)
        compared=[(int(r[3]),int(r[4]),(impacts if r[2]=='DawnMeteorImpact' else shocks)[int(r[1])]) for r in zones
                  if int(r[1]) in (impacts if r[2]=='DawnMeteorImpact' else shocks)]
        presentation.append(dict(compared=len(compared),maxActivationError=max((abs(a-c) for a,b,c in compared),default=999),
            maxFrameLag=max((b-a for a,b,c in compared),default=999),safePredictions=len(safe),safeConfirmed=len(safe & avoids),
            safeUnconfirmed=sorted(safe-avoids),invulnerableAtImpact=sum(r[0]=='invulnerable' for r in rows),duplicateCues=len(zones)-len({r[1] for r in zones})))
    checks['current_time_projection_active']=all(r['timelineSamples']>1000 and 10<=r['timelineMaxLeadTicks']<=60 for r in results)
    checks['visual_activation_matches_server_tick']=all(p['compared']>=8 and p['maxActivationError']==0 for p in presentation)
    checks['landing_display_within_three_ticks']=all(p['maxFrameLag']<=3 for p in presentation)
    checks['no_replayed_landing_cues']=all(p['duplicateCues']==0 for p in presentation)
    checks['visible_safe_jumps_confirmed']=all(p['safeConfirmed']>=2 and not p['safeUnconfirmed'] for p in presentation)
report = dict(passed=all(checks.values()), checks=checks, commonSnapshots=len(common),
              commonContentEvents=shortest, presentation=presentation, meteorImpactTicks=volleys, results=results, renders=renders)
if all('luaRequests' in r for r in results):report['meteorVolleyEvidence']=volley_evidence
(root / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in report.items() if k not in ('results', 'renders')}, indent=2))
sys.exit(0 if report['passed'] else 1)
