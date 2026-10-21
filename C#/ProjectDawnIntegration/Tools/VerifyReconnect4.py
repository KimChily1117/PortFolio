"""Interrupted 4-Unity flow: fresh p2 has a later event baseline, not an empty replay."""
import json,sys
from pathlib import Path
r=Path(sys.argv[1]).resolve()
folders=[r/p for p in ['p1','p2-resume','p3','p4']]
flows=[json.loads((p/'flow-result.json').read_text(encoding='utf-8-sig')) for p in folders]
net=[json.loads((p/'network-result.json').read_text(encoding='utf-8-sig')) for p in folders]
audits=[]
for p in folders:
 rows=(p/'confirmed-audit.txt').read_text(encoding='utf-8-sig').splitlines()
 audits.append(dict(x.split(' ',1) for x in rows if not x.startswith('event ')))
common=set.intersection(*(set(a) for a in audits))
checks={
 'original_four_slots':sorted(n['slot'] for n in net)==[0,1,2,3],
 'all_return_town':all(f['completed'] and f['stage']=='Town' and f['errors']==0 for f in flows),
 'appearance_preserved':all(f['raidLook']==f['returnedLook']==f['loadedLook'] for f in flows),
 'fresh_login_resume_logged':'Resume Loading' in (folders[1]/'flow-audit.txt').read_text(),
 'new_session_epoch': '[RAID_RESUMED]' in (r/'server.log').read_text(encoding='utf-8-sig') and 'epoch=2' in (r/'server.log').read_text(encoding='utf-8-sig'),
 'at_least_50_common_states':len(common)>=50,
 'every_common_state_matches':all(len({a[t] for a in audits})==1 for t in common),
 'no_admission_or_protocol_failure':all(n['rejections']==0 and n['endReason']=='PartyReturn_NoRewards' for n in net),
 'same_final_event':len({n['events'] for n in net})==1,
}
out={'checks':checks,'commonSnapshots':len(common),'events':[n['events'] for n in net],'passed':all(checks.values()),'scope':'one PC, four Unity processes, 30ms each direction; one participant killed and fresh same-account client resumed'}
(r/'reconnect-verification.json').write_text(json.dumps(out,indent=2),encoding='utf-8');print(json.dumps(out,indent=2));sys.exit(not out['passed'])
