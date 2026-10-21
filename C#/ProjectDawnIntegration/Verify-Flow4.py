import json,re,sys,subprocess
from pathlib import Path
root=Path(sys.argv[1]).resolve()
combat=subprocess.run([sys.executable,str(Path(__file__).with_name('Verify-Unity4.py')),str(root),'--flow'],capture_output=True,text=True)
if combat.returncode:print(combat.stdout,combat.stderr)
results=[json.loads((root/f'p{i}/flow-result.json').read_text(encoding='utf-8-sig')) for i in range(1,5)]
receipts=[json.loads((root/f'p{i}/reward-receipt.json').read_text(encoding='utf-8-sig')) if (root/f'p{i}/reward-receipt.json').exists() else None for i in range(1,5)]
def inventory_persisted(result,receipt):
 if sorted(result['equipped'])!=[100,91002]:return False
 own=next((entry for entry in (receipt or {}).get('entries',[]) if entry.get('name')==result['character']),None)
 reward_item=bool(receipt and receipt.get('status')=='Saved' and own and own.get('eligible') and own.get('equipmentTemplate',0)>0)
 return result['items']==7+int(reward_item)
audits=[(root/f'p{i}/flow-audit.txt').read_text(encoding='utf-8-sig').splitlines() for i in range(1,5)]
steps=['Login','Characters','InventoryLoaded 7','GateToLobby','Matching','LoadingShown Demo','SceneReady Bakal','Raid','ReturnReason','SceneReady Town','RoundTripComplete']
def ordered(rows):
 pos=-1
 for step in steps:
  found=next((i for i,row in enumerate(rows) if i>pos and (row.split(' ',1)[1]==step or row.split(' ',1)[1].startswith(step+' '))),None)
  if found is None:return False
  pos=found
 return True
shown=[re.findall(r'CutsceneShown actor=(\d+) local=(\d+)', '\n'.join(a)) for a in audits]
suppressed=[len(re.findall('CutsceneSuppressed', '\n'.join(a))) for a in audits]
shown_actors={int(actor) for pairs in shown for actor,_ in pairs}
suppressed_pairs={tuple(map(int,pair)) for a in audits for pair in re.findall(r'CutsceneSuppressed actor=(\d+) local=(\d+)', '\n'.join(a))}
checks=dict(combat_regression=combat.returncode==0,all_four_round_trip=all(r['completed'] and r['stage']=='Town' for r in results),
 no_unity_errors=all(r['errors']==0 for r in results),ordered_flow=all(ordered(a) for a in audits),
 equipment_persisted=all(inventory_persisted(r,receipt) for r,receipt in zip(results,receipts)),
 town_raid_same_appearance=all(r['raidLook'] and r['raidLook']==r['returnedLook'] for r in results),
 four_distinct_appearances=len({r['raidLook'] for r in results})==4,
 only_own_cutscenes=bool(sum(map(len,shown))) and all(actor==local for pairs in shown for actor,local in pairs),
 remote_cutscenes_filtered=bool(shown_actors) and all((actor,local) in suppressed_pairs for actor in shown_actors for local in range(4) if actor!=local),
 original_loading_and_town_captured=all((root/f'p{i}/loading-Town.png').exists() and (root/f'p{i}/town-gate.png').exists() and (root/f'p{i}/returned-town.png').exists() for i in range(1,5)))
report=dict(passed=all(checks.values()),checks=checks,localCutsceneCounts=list(map(len,shown)),remoteCutsceneSuppressions=suppressed,results=results)
if '--sql' in sys.argv:
 checks['sql_login_raid_return_same_appearance']=all(r.get('loadedLook') and r['loadedLook']==r['raidLook']==r['returnedLook'] for r in results)
 checks['no_appearance_resubmission_on_login']=all(not any('AppearanceSaveRequested' in row for row in a) for a in audits)
 report['passed']=all(checks.values())
(root/'flow-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='results'},indent=2));sys.exit(0 if report['passed'] else 1)
