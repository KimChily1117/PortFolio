"""Check actual four-client post-clear presentation and server-confirmed skip state."""
import json,sys,re
from pathlib import Path
r=Path(sys.argv[1]);info=json.loads((r/'processes.json').read_text(encoding='utf-8-sig'))
evidence=[]
for p in sorted(r.glob('p[1-4]')):
    rows=[json.loads(s) for s in (p/'recording/frames.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    log=(p/'player.log').read_text(encoding='utf-8-sig')
    cine=[x for x in rows if x['clearCinematic']];rewards=[x for x in rows if x['rewards']]
    frames={x['videoFrame'] for x in cine if x['videoPlaying'] and x['videoFrame']>=0}
    end=re.findall(r'ClearCinematicEnd reason=(\w+) votes=(\d+) required=(\d+)',log)
    flow=json.loads((p/'flow-result.json').read_text(encoding='utf-8-sig'))
    evidence.append(dict(client=p.name,slot=rows[0]['local'],samples=len(cine),videoFrames=len(frames),
        fade=any(.2<x['clearElapsed']<1.8 for x in cine),noEarlyReward=all(not x['rewards'] for x in cine),
        twoVotesStillPlaying=any(x['clearVotes']==2 and x['clearCinematic'] for x in cine),
        rewardsUtc=rewards[0]['utcTicks'] if rewards else 0,rewardsElapsed=rewards[0]['clearElapsed'] if rewards else -1,
        end=list(end[-1]) if end else [],errors=flow['errors'],returned=flow['completed']))
full=info.get('cinematic')=='full';reason='Finished' if full else 'MajoritySkip'
checks=dict(four_slots=sorted(x['slot'] for x in evidence)==[0,1,2,3],
    fade_visible=all(x['fade'] for x in evidence),actual_video_progress=all(x['videoFrames']>=20 for x in evidence),
    rewards_hidden_during_video=all(x['noEarlyReward'] for x in evidence),
    shared_reason=all(x['end'] and x['end'][0]==reason for x in evidence),
    timing=all(x['rewardsElapsed']>=42.7 if full else 8.5<x['rewardsElapsed']<15 for x in evidence),
    majority=all(x['end'][1:]==['0','3'] if full else x['end'][1:]==['3','3'] and x['twoVotesStillPlaying'] for x in evidence),
    reward_sync=max(x['rewardsUtc'] for x in evidence)-min(x['rewardsUtc'] for x in evidence)<3500000,
    returned_without_errors=all(x['returned'] and x['errors']==0 for x in evidence))
report=dict(passed=all(checks.values()),checks=checks,evidence=evidence,scope='Four real local Unity clients; test-only reduced boss HP. Production clear, skip and SQL reward path. Auto votes use the same request method as ESC.')
(r/'cinematic-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2));sys.exit(0 if report['passed'] else 1)
