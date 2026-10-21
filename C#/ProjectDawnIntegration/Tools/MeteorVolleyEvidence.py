"""Verify four authored drops; allow skips only with all actors proven outside boss room.

Do not infer a skip from missing damage or a missing impact. Unknown player IDs,
an occupied boss room, missing actions, wrong timing, or extra impacts all fail.
"""
def verify(rows, actor_ids, fall_ticks=72):
    rooms={i:4 for i in actor_ids}; groups=[];current=None;last_tick=0
    for row in rows:
        f=row.split()
        if len(f)<6 or f[0]!='event':continue
        tick=int(f[2]);kind=f[3];target=int(f[4]);value=int(f[5]);last_tick=tick
        if kind=='ContentRoomEnter' and target in rooms:rooms[target]=value
        if kind=='ContentPattern':
            if current is not None:current['closed']=True;groups.append(current)
            current={'actions':[],'skips':[],'impacts':[],'closed':False} if value==6 else None
        elif current is not None:
            if kind=='ContentPatternAction' and value==2:
                current['actions'].append(tick)
                if len(rooms)==4 and all(r!=4 for r in rooms.values()):current['skips'].append(tick)
            elif kind=='ContentMeteorImpact':current['impacts'].append(tick)
    if current is not None and len(current['actions'])==4 and last_tick>=current['actions'][-1]+fall_ticks:groups.append(current)
    for g in groups:
        expected=[t+fall_ticks for t in g['actions'] if t not in g['skips']]
        g['passed']=len(g['actions'])==4 and all(b-a==87 for a,b in zip(g['actions'],g['actions'][1:])) and g['impacts']==expected
    return {'passed':bool(groups) and all(g['passed'] for g in groups) and any(len(g['impacts'])==4 for g in groups),
            'verifiedEmptyRoomSkips':sum(len(g['skips']) for g in groups),'volleys':groups}

if __name__=='__main__':
    def events(skip=False,leave=False):
        r=['event 1 1 ContentPattern 100 6']
        if leave:r += [f'event {i+2} 2 ContentRoomEnter {i} 0' for i in range(4)]
        for i,t in enumerate((10,97,184,271)):
            if i==1 and leave:r.append('event 8 90 ContentRoomEnter 0 4')
            r.append(f'event 9 {t} ContentPatternAction 100 2')
            if i or not skip:r.append(f'event 10 {t+72} ContentMeteorImpact 100 1')
        r.append('event 11 400 ContentPattern 100 0');return r
    normal=events()
    assert verify(normal,range(4))['passed']
    assert not verify(events(skip=True),range(4))['passed']
    assert verify(normal+events(skip=True,leave=True),range(4))['passed']
    assert not verify(normal+events(skip=True,leave=True),[])['passed']
    assert not verify(normal+[s for s in events() if ' 97 ContentPatternAction ' not in s],range(4))['passed']
    assert not verify(normal+[s.replace(' 169 ContentMeteorImpact ',' 170 ContentMeteorImpact ') for s in events()],range(4))['passed']
    assert not verify(normal+events(leave=True),range(4))['passed']
    print('[METEOR_EVIDENCE_VALIDATED] 7')
