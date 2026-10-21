"""Same Unity processes: natural defeat -> town -> rematch -> second raid."""
import json,re,sys
from pathlib import Path
root=Path(sys.argv[1]).resolve()
logs=[(root/f'p{i}'/'player.log').read_text(encoding='utf-8-sig') for i in (1,2)]
rounds=[[json.loads((root/f'p{i}'/f'round-{r}'/'network-result.json').read_text(encoding='utf-8-sig')) for i in (1,2)] for r in (1,2)]
audits=[[(root/f'p{i}'/f'round-{r}'/'confirmed-audit.txt').read_text(encoding='utf-8-sig').splitlines() for i in (1,2)] for r in (1,2)]
encounters=[re.findall(r'EncounterStart round=(\d+) encounter=(\d+)',log) for log in logs]
revives=[[row for row in rows if ' ContentRevive ' in row] for rows in audits[0]]
states=[[dict(row.split(' ',1) for row in rows if not row.startswith('event ')) for rows in group] for group in audits]
common=[set(group[0])&set(group[1]) for group in states]
checks={
 'same_process_two_distinct_encounters':all(len(e)==2 and e[0][0]=='1' and e[1][0]=='2' and e[0][1]!=e[1][1] for e in encounters),
 'party_agrees_encounters':encounters[0]==encounters[1],
 'first_round_natural_defeat':all(r['endReason']=='Defeat' for r in rounds[0]),
 'three_shared_revives':all(len(rows)==3 and all(row.split()[5]=='50' for row in rows) for rows in revives),
 'revive_events_identical':revives[0]==revives[1],
 'second_round_normal_return':all(r['endReason']=='PartyReturn_NoRewards' for r in rounds[1]),
 'both_rounds_two_players':all(r['players']==2 for group in rounds for r in group),
 'no_admission_rejections':all(r['rejections']==0 for group in rounds for r in group),
 'second_round_played_90_seconds':all(r['frames']>=5300 for r in rounds[1]),
 'confirmed_states_equal_both_rounds':all(len(ticks)>=30 and all(group[0][t]==group[1][t] for t in ticks) for group,ticks in zip(states,common)),
 'no_unity_errors':all(json.loads((root/f'p{i}'/'flow-result.json').read_text(encoding='utf-8-sig'))['errors']==0 for i in (1,2)),
 'no_transport_failure':all('[DAWN_DISCONNECTED]' not in log for log in logs),
 'reentry_requested_by_both':all('ReentryRequested' in log for log in logs),
}
report=dict(passed=all(checks.values()),checks=checks,encounters=encounters[0],commonSnapshots=list(map(len,common)),revives=revives[0],
 limitation='One PC, two real Unity processes and AWS TLS. Automated inputs; no OS F5 key test.')
(root/'reentry-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2));sys.exit(0 if report['passed'] else 1)
