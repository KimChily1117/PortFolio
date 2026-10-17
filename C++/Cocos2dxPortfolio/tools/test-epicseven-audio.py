"""Check staged Ogg identity, channel/rate preservation and complete end pages."""
import argparse,hashlib,json,struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def validate(track):
    path=ROOT/'Resources'/track['path']
    with path.open('rb') as stream:
        header=stream.read(4096)
        if header[:4]!=b'OggS':raise ValueError('missing Ogg capture')
        payload=27+header[26]
        if header[payload:payload+7]!=b'\x01vorbis':raise ValueError('not Vorbis identification packet')
        channels=header[payload+11];rate=struct.unpack_from('<I',header,payload+12)[0]
        if (channels,rate)!=(track['channels'],track['sampleRate']):raise ValueError('channel/rate changed')
        stream.seek(max(0,path.stat().st_size-65536));tail=stream.read();last=tail.rfind(b'OggS')
        if last<0 or not (tail[last+5]&4):raise ValueError('missing end-of-stream page')
        frames=struct.unpack_from('<Q',tail,last+6)[0]
        if frames!=track['frames']:raise ValueError(f'sample length changed: {frames} != {track["frames"]}')
    return path.stat().st_size

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--priority-only',action='store_true');args=ap.parse_args()
    base=ROOT/'Resources/reference/audio';catalog=json.loads((base/'catalog.json').read_text(encoding='utf8'))
    mapping=json.loads((base/'bindings.json').read_text(encoding='utf8'));selected={t['id'] for b in mapping['bindings'].values() for t in b['tracks']}
    tracks=[t for t in catalog['tracks'] if not args.priority_only or t['id'] in selected]
    lookup={t['id']:t for t in catalog['tracks']};errors=[];total=0
    for key,binding in mapping['bindings'].items():
        for t in binding['tracks']:
            if t['id'] not in lookup or lookup[t['id']]['name']!=t['name']:errors.append(key+': sample identity mismatch')
    phase_count=0
    for hero in ('c1017','c1048','c1028','c1037'):
        try:
            plan=mapping['skillPlans'][hero]
            assert plan['available'] and plan['originalFmodTimingVerified'] is False
            source=(ROOT/'Resources'/plan['timeline']).read_bytes()
            assert hashlib.sha256(source).hexdigest()==plan['timelineSha256'],'timeline changed'
            timeline=json.loads(source)
            expected={p['id'] for p in mapping['bindings'][f'hero.{hero}.skill3.all_phases']['tracks']}
            phases=plan['phases'];actual=[p['id'] for p in phases]
            assert set(actual)==expected and len(actual)==len(expected),'missing/repeated canonical phase'
            assert [p['offset'] for p in phases]==sorted(p['offset'] for p in phases),'unsorted phases'
            for phase in phases:
                assert lookup[phase['id']]['name']==phase['name'],'phase sample identity'
                anchor=phase['anchor'];event=timeline['events'][anchor['eventIndex']]
                if anchor['kind']=='timeline_cue':
                    assert event['type']==anchor['type'] and event['params']==anchor['params']
                    offset=event['time']
                else:
                    assert event['type']=='EFFECT' and event['params']['effect']==anchor['effect']
                    primitive=timeline['effects'][anchor['effect']]['items'][anchor['primitiveIndex']]
                    assert primitive['source']==anchor['source'] and primitive['delay']==anchor['delay']
                    offset=event['time']+primitive['delay']
                assert abs(phase['offset']-offset)<1e-5,'cue offset mismatch'
                assert 0<=phase['offset'] and phase['offset']+phase['duration']<=timeline['duration']+.05,'phase outlives presentation'
                assert phase['association'].startswith('reconstruction:'),'unverified timing not labeled'
                phase_count+=1
        except Exception as ex:errors.append(hero+': phase validation '+str(ex))
    for track in tracks:
        try:total+=validate(track)
        except Exception as ex:errors.append(track['id']+': '+str(ex))
    result={'checked':len(tracks),'passed':len(tracks)-len(errors),'verifiedS3Phases':phase_count,'oggBytes':total,'errors':errors,'scope':'priority' if args.priority_only else 'all'}
    target=ROOT/'.deps/epicseven-assets/cutscene-analysis'/('audio-validation-priority.json' if args.priority_only else 'audio-validation.json')
    target.write_text(json.dumps(result,indent=2),encoding='utf8');print(json.dumps(result,ensure_ascii=False))
    raise SystemExit(1 if errors else 0)
if __name__=='__main__':main()
