import json,re,sys
from pathlib import Path
root=Path(sys.argv[1]);checks={};details={}
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
before=read(root/'aws-db-before-equip.json');after=read(root/'aws-db-after-equip.json')
checks['four_owned_grants']=len(before)==4 and len({r['item'] for r in before})==4 and all(r['owner']==r['player'] and r['equipped']==0 for r in before)
checks['same_items_equipped_in_mysql']=len(after)==4 and all(any(b['item']==r['item'] and b['owner']==r['owner'] and b['encounter']==r['encounter'] for b in before) and r['equipped']==1 for r in after)
encounters=[];slots=[]
for i in range(1,5):
 d=root/f'p{i}';flow=read(d/'flow-result.json');net=read(d/'network-result.json');receipt=read(d/'reward-receipt.json');audit=(d/'flow-audit.txt').read_text(encoding='utf-8-sig')
 rows=[json.loads(s) for s in (d/'recording/frames.jsonl').read_text(encoding='utf-8-sig').splitlines()]
 slots.append(net['slot']);encounters.append(receipt['encounter'])
 checks[f'p{i}_clear_and_return']=flow['completed'] and flow['stage']=='Town' and net['endReason']=='Clear' and net['bossHp']==0
 checks[f'p{i}_no_unity_errors']=flow['errors']==0
 checks[f'p{i}_normal_boss_hp']=max(r['maxBossHp'] for r in rows)==32500 and next(r for r in rows if r['stage']=='Raid')['combatBossHp']==32500
 checks[f'p{i}_both_phases']=any(r['phaseTransition'] for r in rows) and any(r['phase']==2 and not r['phaseTransition'] and r['combatBossHp']>0 for r in rows)
 checks[f'p{i}_saved_then_equipped']='PortfolioSqlReceipt' in audit and audit.index('PortfolioSqlReceipt')<audit.index('PortfolioEquipRequested')<audit.index('PortfolioSqlEquipConfirmed')
 checks[f'p{i}_town_dungeon_appearance']=flow['raidLook']==flow['returnedLook']
 checks[f'p{i}_chat_enter_submit_both']=all(f'ChatSubmitted {scope}' in audit and f'ChatGuiEnter {scope}' in audit for scope in ('ChatTown','ChatDungeon'))
 checks[f'p{i}_chat_received_both']=all(f'ChatReceived {scope}' in audit for scope in ('ChatTown','ChatDungeon'))
 checks[f'p{i}_chat_bubble_both']=all(f'ChatBubbleShown {scope}' in audit for scope in ('ChatTown','ChatDungeon'))
 phase1=max(r['seconds'] for r in rows if r['phase']==1)
 phase2=[r for r in rows if r['phase']==2 and not r['phaseTransition'] and r['combatBossHp']>0]
 phase2Duration=phase2[-1]['seconds']-phase2[0]['seconds']
 checks[f'p{i}_sufficient_combat']=phase1>=30 and phase2Duration>=30
 details[f'p{i}']=dict(phase1Seconds=phase1,phase2Seconds=phase2Duration,inputs=net['inputs'],rejections=net['rejections'],luaConfirmed=net['luaConfirmed'],groundAvoids=net['groundAvoids'],meteorAvoids=net['meteorAvoids'],patterns=net['patterns'],skills=net['skills'],chatTownReceived=audit.count('ChatReceived ChatTown'),chatDungeonReceived=audit.count('ChatReceived ChatDungeon'))
checks['four_distinct_slots']=sorted(slots)==[0,1,2,3]
checks['single_encounter']=len(set(encounters))==1 and int(encounters[0])==before[0]['encounter']
serverLog=(root/'aws-server.log').read_text(encoding='utf-8-sig')
late=[s for s in serverLog.splitlines() if '[RAID_INPUT_REJECT]' in s and 'result=TooLate' in s]
report=dict(passed=all(checks.values()),checks=checks,details=details,networkClean=len(late)==0,tooLateCount=len(late),knownIssues=[] if not late else ['Actual server logged '+str(len(late))+' TooLate input rejections. This count is broadcast to all clients; do not sum the four counters. Functional recording completed; zero-rejection network stability is NOT established.'],scope='Real AWS TLS/MySQL, four processes on one Windows PC, automated ordinary game/IMGUI inputs, production HP/damage. No claim of four independent physical PCs or WAN latency stress test.')
(root/'portfolio-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2));sys.exit(0 if report['passed'] else 1)
