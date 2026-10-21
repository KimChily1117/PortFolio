"""Verify variable-size Unity/AWS entry and chat evidence without relaxing combat regression."""
import json,re,sys
from pathlib import Path
root=Path(sys.argv[1]).resolve()
n=json.loads((root/'processes.json').read_text(encoding='utf-8-sig'))['partySize']
flows=[];nets=[];audits=[];events=[];states=[]
for i in range(1,n+1):
 p=root/f'p{i}'
 flows.append(json.loads((p/'flow-result.json').read_text(encoding='utf-8-sig')))
 nets.append(json.loads((p/'network-result.json').read_text(encoding='utf-8-sig')))
 audits.append((p/'flow-audit.txt').read_text(encoding='utf-8-sig'))
 lines=(p/'confirmed-audit.txt').read_text(encoding='utf-8-sig').splitlines()
 events.append([s for s in lines if s.startswith('event ')])
 states.append(dict(s.split(' ',1) for s in lines if not s.startswith('event ')))
common=set.intersection(*(set(s) for s in states));shortest=min(map(len,events))
received=[re.findall(r'ChatReceived ChatDungeon sender=(\d+) seq=(\d+)',a) for a in audits]
checks={
 'selected_party_size': all(f'MatchSize {n}' in a for a in audits),
 'exact_player_count':all(r['players']==n for r in nets),
 'distinct_slots': sorted(r['slot'] for r in nets)==list(range(n)),
 'one_lua_host':sum(r['luaRequests']>0 for r in nets)==1,
 'all_returned':all(r['completed'] and r['stage']=='Town' for r in flows),
 'no_unity_errors':all(r['errors']==0 for r in flows),
 'no_admission_rejections':all(r['rejections']==0 for r in nets),
 'appearance_preserved':all(r['raidLook'] and r['raidLook']==r['returnedLook'] for r in flows),
 'committed_states_equal':len(common)>=30 and all(len({s[t] for s in states})==1 for t in common),
 'committed_events_equal':shortest>=30 and all(e[:shortest]==events[0][:shortest] for e in events),
 'no_protocol_abort':all(r['endReason'] in ('PartyReturn_NoRewards','Defeat','Clear') for r in nets),
 'dungeon_chat_once_each':all(len(r)==n and len({s for s,q in r})==n and [int(q) for s,q in r]==list(range(1,n+1)) for r in received),
 'dungeon_chat_equal':all(r==received[0] for r in received),
 'enter_handler_opened_and_sent':all(a.count('ChatInputOpened')==2 and a.count('ChatSubmitted')==2 for a in audits),
 'own_town_bubble_rendered':all('ChatBubbleShown ChatTown' in a for a in audits),
 'dungeon_bubbles_rendered':all('ChatBubbleShown ChatDungeon' in a for a in audits),
 'screenshots_present':all((root/f'p{i}'/name).is_file() for i in range(1,n+1) for name in ('chat-town.png','chat-dungeon.png','returned-town.png')),
}
report=dict(passed=all(checks.values()),partySize=n,checks=checks,commonStates=len(common),commonEvents=shortest,
 rejections=[r['rejections'] for r in nets],
 limitation='One PC, multiple Unity processes; automated Enter handler, not physical keyboard IME. Full combat regression remains separate.')
(root/'party-flow-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2));sys.exit(0 if report['passed'] else 1)
