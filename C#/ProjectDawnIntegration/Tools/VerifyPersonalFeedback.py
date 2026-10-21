"""Checks four-client routing/dedup evidence, not subjective feel or audio waveform quality."""
import json, re, sys
from pathlib import Path
root = Path(sys.argv[1]).resolve()
clients = []
for i in range(1, 5):
    rows = []
    for line in (root / f'p{i}/player.log').read_text(encoding='utf-8-sig').splitlines():
        if line.startswith('[PERSONAL_HIT] '):
            rows.append(dict(re.findall(r'(\w+)=([^ ]+)', line)))
    clients.append(rows)
profiles = {r['profile'] for rows in clients for r in rows}
checks = {
    'all_four_clients_exercised': all(clients),
    'four_distinct_local_slots': sorted({int(r['local']) for rows in clients for r in rows}) == [0,1,2,3],
    'only_local_source_feedback': all(r['local'] == r['source'] for rows in clients for r in rows),
    'no_predicted_confirmed_duplicate': all(len(rows) == len({r['key'] for r in rows}) for rows in clients),
    'target_profiles_exercised': {'basic','frenzy','bloodsword','overkill-impact','overkill-pulse'} <= profiles,
    'individual_hold_capped': all(0 <= float(r['hold']) <= .106 for rows in clients for r in rows),
}
report = dict(passed=all(checks.values()), checks=checks, profiles=sorted(profiles),
              clientHitCounts=[len(rows) for rows in clients],
              scope='Local contact routing and event dedup. Frame quality/audio balance require separate review.')
(root / 'personal-feedback-verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
sys.exit(0 if report['passed'] else 1)
