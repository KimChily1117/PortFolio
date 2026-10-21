"""Inspect actual four-client recordings; never alter game state for evidence."""
import json, sys, bisect
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(sys.argv[1]).resolve(); out=root/'rooms';out.mkdir(exist_ok=True)
streams=[]
for folder in sorted(root.glob('p[1-4]')):
    rows=[json.loads(s) for s in (folder/'recording/frames.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    streams.append((rows[0]['local'],folder/'recording',rows))
streams.sort()
rows=[r for _,_,stream in streams for r in stream]
checks={
 'four_distinct_clients': [s[0] for s in streams]==[0,1,2,3],
 'fixed_absolute_actor_slots_and_names': all([a['id'] for a in r['actors']]==[0,1,2,3] and all(a['name'] for a in r['actors']) for r in rows),
 'all_rooms_bounded':all(0<=a['room']<9 for r in rows for a in r['actors']),
 'boss_approaches':max(r['bossX'] for r in rows)-min(r['bossX'] for r in rows)>1,
 'party_split_across_rooms':any(len({a['room'] for a in r['actors']})>1 for r in rows),
 'marked_actor_carried_to_other_room':any(r['markTarget']>=0 and r['actors'][r['markTarget']]['room']!=4 for r in rows),
 'remote_room_meteor_burn':any(f['RoomId']!=4 and f['StartTick']<=r['tick']<f['EndTick'] for r in rows for f in r['fires']),
 'empty_room_fire_continues':any(f['StartTick']<=r['tick']<f['EndTick'] and all(a['room']!=f['RoomId'] for a in r['actors']) for r in rows for f in r['fires']),
 'damage_trail_visible':any(r['bossDamage']>r['bossHp']+.002 for r in rows),
 'trail_never_below_hp':all(r['bossDamage']+.00001>=r['bossHp'] for r in rows),
 'trail_catches_up_after_hit':any(b['bossDamage']<a['bossDamage']-.0001 and abs(b['bossHp']-a['bossHp'])<.00001 for _,_,rs in streams for a,b in zip(rs,rs[1:])),
}
rounds={}
round_alive={}
for r in rows:
    if len(r.get('markTargets',[]))==2:
        rounds.setdefault(r['markDeadline'],set()).add(tuple(sorted(r['markTargets'])))
        round_alive.setdefault(r['markDeadline'],sum(a['hp']>0 for a in r['actors']))
ordered=[next(iter(v)) for _,v in sorted(rounds.items())]
checks['two_unique_marks_per_round']=bool(ordered) and all(len(set(pair))==2 for pair in ordered)
checks['four_clients_agree_mark_pairs']=all(len(v)==1 for v in rounds.values())
full_pairs=[(rounds[a],rounds[b]) for a,b in zip(sorted(rounds),sorted(rounds)[1:]) if round_alive[a]==4 and round_alive[b]==4]
checks['no_consecutive_target_reuse_when_four_alive']=bool(full_pairs) and all(not set(next(iter(a)))&set(next(iter(b))) for a,b in full_pairs)
checks['simultaneous_drops_in_distinct_rooms']=any(any(a['StartTick']==b['StartTick'] and a['RoomId']!=b['RoomId'] for i,a in enumerate(r['fires']) for b in r['fires'][i+1:]) for r in rows)
checks['new_marks_while_old_fire_active']=any(r['markTargets'] and any(f['StartTick']<=r['tick']<f['EndTick'] for f in r['fires']) for r in rows)
checks['overlapping_fire_generations']=any(len({f['StartTick'] for f in r['fires'] if f['StartTick']<=r['tick']<f['EndTick']})>=2 for r in rows)
checks['all_four_corner_deposits']=set(f['RoomId'] for r in rows for f in r['fires']) >= {0,2,6,8}
checks['ten_second_mark_cadence']=len(rounds)>=3 and all(b-a==600 for a,b in zip(sorted(rounds),sorted(rounds)[1:]))
checks['returned_to_boss_room']=any(any(r['room']!=4 for r in rs) and rs[-1]['room']==4 for _,_,rs in streams)
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
def nearest(rs,t): return min(rs,key=lambda r:abs(r['utcTicks']-t))
def save_pair(name,chosen,other):
    image=Image.new('RGB',(1920,640),'#151820');draw=ImageDraw.Draw(image)
    for i,(stream,row,label) in enumerate((chosen,other)):
        image.paste(Image.open(stream[1]/row['file']).convert('RGB'),(960*i,40))
        draw.text((960*i+12,7),label,font=font,fill='#ffcf9a')
    image.save(out/name)
    return abs(chosen[1]['utcTicks']-other[1]['utcTicks'])/1e4
fire_pair=None
for stream in streams:
    for r in stream[2]:
        if any(f['RoomId']==r['room']!=4 and f['StartTick']<r['tick']<f['EndTick'] for f in r['fires']):
            remote=next((s for s in streams if s[0]!=stream[0] and nearest(s[2],r['utcTicks'])['room']==4),None)
            if remote:
                rr=nearest(remote[2],r['utcTicks'])
                fire_pair=(stream,r,remote,rr);break
    if fire_pair:break
checks['fire_rendered_with_other_room_view']=fire_pair is not None
skew=None
if fire_pair:
    s,r,o,rr=fire_pair
    skew=save_pair('rooms-comparison.png',(s,r,f"{s[0]+1}P · 방 {r['room']+1} · 메테오 후 불바다"),(o,rr,f"{o[0]+1}P · 중앙 보스 방 · 같은 촬영 시각"))
    Image.open(s[1]/r['file']).save(out/'fire-room.png')
overlap=next(((s,r) for s in streams for r in s[2] if r['markTargets'] and len({f['StartTick'] for f in r['fires'] if f['StartTick']<=r['tick']<f['EndTick']})>=2 and r['cutActor']<0),None)
if overlap: Image.open(overlap[0][1]/overlap[1]['file']).save(out/'overlap-map.png')
hp=max(((s,r) for s in streams for r in s[2] if r['cutActor']<0),key=lambda sr:sr[1]['bossDamage']-sr[1]['bossHp'])
Image.open(hp[0][1]/hp[1]['file']).save(out/'boss-hp-trail.png')
report={'passed':all(checks.values()),'checks':checks,'pairCaptureSkewMs':skew,
 'observedLocalRooms':{str(s[0]+1):sorted({r['room']+1 for r in s[2]}) for s in streams},
 'fourAliveConsecutivePairsChecked':len(full_pairs),
 'markPairsInOrder':[[i+1 for i in pair] for pair in ordered],
 'maxBossTrailFraction':hp[1]['bossDamage']-hp[1]['bossHp'],
 'scope':'One PC, four independent Unity processes, 30ms each-direction TCP delay. Nine-room topology and all directions checked separately by RoomTests.'}
(out/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2));sys.exit(0 if report['passed'] else 1)
