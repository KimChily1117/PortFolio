"""Verify personal combo routing and real server-committed potion events in four clients."""
import json,re,sys
from pathlib import Path
root=Path(sys.argv[1]).resolve()
combos=[];heals=[]
for i in range(1,5):
    log=(root/f'p{i}/player.log').read_text(encoding='utf-8-sig')
    combos.append([dict(re.findall(r'(\w+)=([^ ]+)',line)) for line in log.splitlines() if line.startswith('[PERSONAL_COMBO] ')])
    rows=[]
    for line in (root/f'p{i}/confirmed-audit.txt').read_text(encoding='utf-8-sig').splitlines():
        fields=line.split()
        if len(fields)==6 and fields[0]=='event' and fields[3]=='ContentHeal':
            rows.append(tuple(map(int,[fields[1],fields[2],fields[4],fields[5]])))
    heals.append(rows)
by_target={}
for sequence,tick,target,amount in heals[0]:by_target.setdefault(target,[]).append((tick,amount))
checks={
 'all_four_local_combo_counters_exercised':all(combos),
 'four_distinct_slots':sorted({int(r['local']) for rows in combos for r in rows})==[0,1,2,3],
 'only_own_confirmed_hits_counted':all(r['source']==r['local'] for rows in combos for r in rows),
 'no_duplicate_combo_hits':all(len(rows)==len({r['key'] for r in rows}) for rows in combos),
 'same_heals_confirmed_on_all_four':bool(heals[0]) and all(rows==heals[0] for rows in heals),
 'all_four_players_used_potions':len(by_target)==4,
 'maximum_five_per_player':all(len(rows)<=5 for rows in by_target.values()),
 'fifteen_second_cooldown':all(b[0]-a[0]>=900 for rows in by_target.values() for a,b in zip(rows,rows[1:])),
 'positive_heal_capped_at_forty':all(0<amount<=40 for rows in by_target.values() for _,amount in rows),
}
report=dict(passed=all(checks.values()),checks=checks,comboHits=[len(rows) for rows in combos],healEvents=heals[0],
 scope='Actual loopback four-client run; personal UI routing and server healing evidence, not subjective feel or external WAN.')
(root/'combat-hud-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2));sys.exit(0 if report['passed'] else 1)
