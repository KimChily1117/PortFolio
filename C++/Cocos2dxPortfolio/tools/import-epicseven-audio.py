"""Stage every previously decoded FMOD sample as portable Ogg Vorbis.

Reads immutable WAV extraction + its FSB5 metadata. Uses an existing local
FFmpeg, never the game's DLLs. Resumable, bounded parallelism, 4 GiB reserve.
"""
import argparse, concurrent.futures, hashlib, json, os, re, shutil, subprocess, time, wave
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.deps/epicseven-assets'
MEDIA=BASE/'media'
OUT=ROOT/'Resources/reference/audio'
REPORT=BASE/'cutscene-analysis/audio-import.json'
DEFAULT_FFMPEG=Path('C:/Program Files/MediaHuman/YouTube Downloader/ffmpeg/ffmpeg.exe')
HEROES={'c1017':'achates','c1048':'araminta','c1028':'clarisa','c1037':'dominiel'}

def save(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    temp=(BASE/'work/audio-import'/path.relative_to(OUT)).with_suffix(path.suffix+'.tmp') if path.is_relative_to(OUT) else path.with_suffix(path.suffix+'.tmp')
    temp.parent.mkdir(parents=True,exist_ok=True)
    temp.write_text(json.dumps(value,ensure_ascii=False,separators=(',',':')),encoding='utf8');temp.replace(path)

def inventory():
    tracks=[];banks=[]
    for manifest in sorted((MEDIA/'audio').rglob('manifest.json')):
        data=json.loads(manifest.read_text(encoding='utf8'));bank=data['bank'].replace('\\','/')
        bank_info={k:v for k,v in data.items() if k!='tracks'};bank_info['bank']=bank;banks.append(bank_info)
        for track in data.get('tracks',[]):
            source=Path(track['path'].replace('\\','/'));meta=track.get('metadata',{});stream=meta.get('streamInfo',{})
            rel=source.relative_to('audio').with_suffix('.ogg')
            category='cinema' if bank=='cinema' else 'voice' if bank.startswith('ko/') else 'music' if bank.startswith('bgm') else 'ui' if bank.startswith('ui') else 'effect'
            target=OUT/'assets'/rel
            tracks.append({'id':rel.with_suffix('').as_posix(),'name':stream.get('name',''),'bank':bank,
                           'path':target.relative_to(ROOT/'Resources').as_posix(),'sourceWav':source.as_posix(),
                           'category':category,'language':'ko' if category=='voice' else source.parts[2] if category=='cinema' else '',
                           'frames':track['frames'],'sampleRate':track['sample_rate'],'channels':track['channels'],
                           'duration':round(track['frames']/track['sample_rate'],6),'wavBytes':track['bytes'],
                           'sourceEncoding':meta.get('encoding',''),'sourceLoop':meta.get('loopingInfo'),
                           'staged':target.exists(),'oggBytes':target.stat().st_size if target.exists() else 0})
    return tracks,banks

def prepare_cinema(ffmpeg):
    """Extract the installed movies' audio streams, not the movie frames."""
    folder=MEDIA/'audio/cinema';manifest=folder/'manifest.json'
    if manifest.exists():return
    videos=json.loads((MEDIA/'videos.json').read_text(encoding='utf8'));tracks=[]
    for video in videos:
        relative=Path(video['path'].replace('\\','/'));source=MEDIA/relative
        target=folder/relative.relative_to('cinema').with_suffix('.wav')
        if not target.exists():
            if shutil.disk_usage(BASE).free<4*1024**3:raise RuntimeError('4 GiB reserve reached')
            target.parent.mkdir(parents=True,exist_ok=True);temporary=target.with_suffix('.pending.wav')
            result=subprocess.run([str(ffmpeg),'-nostdin','-hide_banner','-loglevel','error','-y','-i',str(source),'-map','0:a:0','-vn','-c:a','pcm_s16le',str(temporary)],capture_output=True,text=True,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0),timeout=180)
            if result.returncode:raise RuntimeError('Movie audio decode failed: '+str(source)+' '+result.stderr[-500:])
            temporary.replace(target)
        with wave.open(str(target),'rb') as wav:
            tracks.append({'path':target.relative_to(MEDIA).as_posix(),'frames':wav.getnframes(),'sample_rate':wav.getframerate(),'channels':wav.getnchannels(),'bytes':target.stat().st_size,
                           'metadata':{'streamInfo':{'name':relative.as_posix()},'encoding':'AAC in MP4','sourceVideo':relative.as_posix(),'sourceSha256':video['sha256']}})
    save(manifest,{'bank':'cinema','containerType':'MP4 soundtracks, not FMOD bank','status':'complete','expected_tracks':len(tracks),'tracks':tracks})
    print(f'Extracted {len(tracks)} installed cinematic audio streams',flush=True)

def bindings(tracks):
    by_name={}
    for track in tracks:by_name.setdefault(track['name'].lower(),[]).append(track)
    result={}
    def bind(key,names,preferred='',gain=.7):
        selected=[]
        for name in names:
            found=by_name.get(name.lower(),[])
            if not found:continue
            entry=next((r for r in found if r['bank']==preferred),found[0])
            selected.append({k:entry[k] for k in ['id','name','path','duration'] }|{'volume':gain})
        result[key]={'tracks':selected,'evidence':'exact embedded FSB5 sample name','trigger':'host reconstruction event; original FMOD event routing/timing not recovered'}
    bind('music.lobby',['bgm_lobby_base'],'bgm1.bank',.30)
    bind('music.battle',['bgm_map01_battle'],'bgm1.bank',.27)
    bind('ui.click',['SE_UI2_LOBBY_BUTTON_NORMAL'],'ui1.bank',.6)
    bind('ui.back',['SE_UI2_LOBBY_BUTTON_BACK'],'ui1.bank',.6)
    bind('ui.skill',['se_ui_skill_select'],'ui1.bank',.6)
    bind('attack.default',['se_chr_ras_skill_1_01'],'sfx1.bank',.55)
    for hero,model in HEROES.items():
        for skill in (1,2,3):
            voice=sorted(n for n in by_name if re.fullmatch(r'voc_'+hero+r'_skill'+str(skill)+r'_\d+(?:_\d+)?',n))
            bind(f'hero.{hero}.skill{skill}.voice',voice,'ko/voc1.audio_ko.bank',.8)
            prefix=f'se_chr_{model}_skill_{skill}'
            effects=sorted(n for n in by_name if n.startswith(prefix+'_'))
            # These are phase samples, not interchangeable variants. S1/S2 use
            # their first phase; S3 uses the separately audited visual-cue plan.
            first=[n for n in effects if re.match(re.escape(prefix)+r'_0?1(?:_|$)',n)]
            if not effects and prefix in by_name:effects=first=[prefix]
            bind(f'hero.{hero}.skill{skill}.sfx',first[:1],'sfx1.bank',.65)
            bind(f'hero.{hero}.skill{skill}.all_phases',effects,'sfx1.bank',.65)
    return {'schema':1,'backend':'Cocos AudioEngine / Ogg Vorbis','bindings':result,
            'limitations':['Embedded sample identity verified; FMOD event routing, phase delays, randomization and mixing are not reconstructed.',
                           'Hero skill voices are selected in round-robin order at the host action start, not a recovered original voice timeline.',
                           'All canonical S3 SFX phases use source visual cue times with explicitly reconstructed audio-to-cue associations.',
                           'Music loops the decoded clip; original FMOD intro/loop segments are not reconstructed.']}

def skill_plans(mapping):
    plans={}
    for hero,model in HEROES.items():
        path=ROOT/'Resources/reference/skills/timelines'/(hero+'.s3.json')
        if not path.exists():plans[hero]={'available':False,'error':'Missing checked source timeline'};continue
        timeline=json.loads(path.read_text(encoding='utf8'));events=timeline['events']
        def cue(kind,match=lambda e:True):
            index,event=next((n,e) for n,e in enumerate(events) if e['type']==kind and match(e))
            return {'offset':event['time'],'anchor':{'kind':'timeline_cue','eventIndex':index,'type':kind,'time':event['time'],'params':event['params']}}
        def cfx(effect,source,delayed=False):
            index,event=next((n,e) for n,e in enumerate(events) if e['type']=='EFFECT' and e['params']['effect']==effect)
            item_index,item=next((n,p) for n,p in enumerate(timeline['effects'][effect]['items']) if p['source']==source and (not delayed or p['delay']>0))
            at=round(event['time']+item['delay'],6)
            return {'offset':at,'anchor':{'kind':'cfx_primitive','eventIndex':index,'effect':effect,'primitiveIndex':item_index,'source':source,'eventTime':event['time'],'delay':item['delay']}}
        try:
            if hero=='c1017':anchors=[cue('ANI'),cue('CUTIN'),cue('EFFECT')]
            elif hero=='c1048':anchors=[cue('ANI'),cue('CUTIN'),cue('EFFECT'),cue('BGHATCH')]
            elif hero=='c1028':anchors=[cue('ANI'),cfx('clarisa_skill_03_01','clarisa_sk2_eff_b'),cue('CUTIN'),cfx('clarisa_skill_03_black','clarisa_sk3_eff_black',True),cue('CAM',lambda e:e['time']==3.0)]
            else:anchors=[cue('ANI'),cfx('nixid_eff_sk3','nixid_skill_03_pati2'),cfx('nixid_eff_sk3','nixid_skill_03_pati4'),cfx('nixid_eff_sk3','nixid_skill_03_pati5'),cue('BGHATCH')]
            samples=mapping['bindings'][f'hero.{hero}.skill3.all_phases']['tracks']
            if len(samples)!=len(anchors):raise ValueError('Canonical SFX count differs from reviewed phase mapping')
            phases=[]
            for sample,anchor in zip(samples,anchors):
                phases.append({**sample,**anchor,'association':'reconstruction: audio sample associated with existing visual cue; original FMOD offset unknown'})
            phases.sort(key=lambda p:p['offset'])
            plans[hero]={'available':True,'timeline':path.relative_to(ROOT/'Resources').as_posix(),
                         'timelineSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'sourceSequence':timeline['source'],
                         'originalFmodTimingVerified':False,'phases':phases}
        except (StopIteration,KeyError,ValueError) as ex:plans[hero]={'available':False,'error':str(ex)}
    return plans

def encode(track,ffmpeg):
    source=MEDIA/track['sourceWav'];target=ROOT/'Resources'/track['path']
    if target.exists():
        with target.open('rb') as f:
            if f.read(4)==b'OggS':return track['id'],target.stat().st_size,'cached'
        raise RuntimeError('Existing staged file is not Ogg: '+str(target))
    if shutil.disk_usage(OUT).free<4*1024**3:raise RuntimeError('4 GiB reserve reached')
    if source.stat().st_size!=track['wavBytes']:raise RuntimeError('WAV size changed: '+str(source))
    with wave.open(str(source),'rb') as wav:
        if (wav.getnframes(),wav.getframerate(),wav.getnchannels())!=(track['frames'],track['sampleRate'],track['channels']):raise RuntimeError('WAV header mismatch: '+str(source))
    target.parent.mkdir(parents=True,exist_ok=True)
    temp=BASE/'work/audio-encoding'/(track['id']+'.pending.ogg');temp.parent.mkdir(parents=True,exist_ok=True)
    command=[str(ffmpeg),'-nostdin','-hide_banner','-loglevel','error','-y','-i',str(source),'-map_metadata','-1','-c:a','libvorbis','-q:a','3','-threads','1',str(temp)]
    result=subprocess.run(command,capture_output=True,text=True,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0),timeout=180)
    if result.returncode:raise RuntimeError(track['id']+': '+result.stderr[-800:])
    with temp.open('rb') as f:
        if f.read(4)!=b'OggS':raise RuntimeError('Encoder did not produce Ogg')
    temp.replace(target);return track['id'],target.stat().st_size,'encoded'

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--ffmpeg',type=Path,default=DEFAULT_FFMPEG);ap.add_argument('--workers',type=int,default=4);ap.add_argument('--priority-only',action='store_true');ap.add_argument('--cinema-only',action='store_true');args=ap.parse_args()
    if not args.ffmpeg.exists():raise SystemExit('Supply --ffmpeg with an existing libvorbis-enabled FFmpeg executable')
    if args.cinema_only and REPORT.exists():
        previous=json.loads(REPORT.read_text(encoding='utf8'))
        if previous.get('totalTracks')==28984 and previous.get('status')=='complete':
            save(REPORT.with_name('audio-import-fmod.json'),previous)
    OUT.mkdir(parents=True,exist_ok=True)
    if not args.priority_only:prepare_cinema(args.ffmpeg)
    tracks,banks=inventory();mapping=bindings(tracks);mapping['skillPlans']=skill_plans(mapping)
    required={t['id'] for b in mapping['bindings'].values() for t in b['tracks']}
    priority=sorted(tracks,key=lambda t:(t['id'] not in required,t['category']!='music',t['id']))
    if args.priority_only:priority=[t for t in priority if t['id'] in required]
    if args.cinema_only:priority=[t for t in priority if t['category']=='cinema']
    save(OUT/'bindings.json',mapping)
    source_summary=json.loads((MEDIA/'summary.json').read_text(encoding='utf8'))
    report={'schema':1,'totalTracks':len(tracks),'bankSamples':sum(t['category']!='cinema' for t in tracks),'cinemaStreams':sum(t['category']=='cinema' for t in tracks),'selectedThisRun':len(priority),'banks':banks,
            'decoder':source_summary['decoder'],'decoderSha256':source_summary['decoder_sha256'],
            'encoder':str(args.ffmpeg),'encoderSha256':hashlib.sha256(args.ffmpeg.read_bytes()).hexdigest(),
            'encoderVersion':subprocess.run([str(args.ffmpeg),'-version'],capture_output=True,text=True).stdout.splitlines()[0],
            'encoding':'libvorbis q3; source sample rate/channels retained','status':'running','completed':0,'failures':[]}
    save(REPORT,report);save(OUT/'catalog.json',{'schema':1,'tracks':tracks,'banks':banks,'complete':False})
    started=time.monotonic();lookup={t['id']:t for t in tracks}
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1,min(4,args.workers))) as pool:
        for n,(tid,size,status) in enumerate(pool.map(lambda t:encode(t,args.ffmpeg),priority),1):
            lookup[tid].update(staged=True,oggBytes=size)
            if n%500==0 or n==len(priority):
                report.update(completed=n,elapsedSeconds=round(time.monotonic()-started,1),oggBytes=sum(t['oggBytes'] for t in tracks))
                save(REPORT,report);print(f"{n}/{len(priority)} Ogg tracks, {report['oggBytes']/2**20:.1f} MiB, {report['elapsedSeconds']} s",flush=True)
    complete=all(t['staged'] for t in tracks)
    report.update(status='complete' if complete else 'priority_complete',stagedTracks=sum(t['staged'] for t in tracks),oggBytes=sum(t['oggBytes'] for t in tracks))
    save(REPORT,report);save(OUT/'catalog.json',{'schema':1,'tracks':tracks,'banks':banks,'complete':complete})
    print(json.dumps({k:report[k] for k in ('status','stagedTracks','oggBytes')},ensure_ascii=False),flush=True)

if __name__=='__main__':main()
