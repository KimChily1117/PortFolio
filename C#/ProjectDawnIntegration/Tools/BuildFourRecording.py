"""Align four actual Unity recordings by capture UTC and encode a four-view video.
No game pixels are synthesized. Duplicate nearest captured frames at 12 fps;
alignment error and the cutscene observations are retained in verification.json.
"""
import argparse, bisect, json, math, subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ap=argparse.ArgumentParser()
ap.add_argument('run', type=Path)
ap.add_argument('--ffmpeg', required=True)
ap.add_argument('--reuse-videos', action='store_true', help='Recheck existing captures without re-encoding unchanged videos')
args=ap.parse_args(); root=args.run.resolve(); out=root/'video'; out.mkdir(exist_ok=True)
processes=json.loads((root/'processes.json').read_text(encoding='utf-8-sig')) if (root/'processes.json').exists() else {}
network_description=f"방향별 {processes.get('delayMs','미기록')}±{processes.get('jitterMs',0)}ms 지연"
streams=[]
for p in sorted(root.glob('p[1-4]')):
    rows=[json.loads(s) for s in (p/'recording/frames.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    assert rows and len({r['local'] for r in rows})==1
    streams.append((rows[0]['local'],p/'recording',rows,[r['utcTicks'] for r in rows]))
streams.sort(); assert [s[0] for s in streams]==[0,1,2,3]
start=max(s[3][0] for s in streams); end=min(s[3][-1] for s in streams)
fps=12; count=int((end-start)/1e7*fps)
def nearest(stream, t):
    k=bisect.bisect_left(stream[3],t)
    return min((stream[2][j] for j in (k-1,k) if 0<=j<len(stream[2])),key=lambda r:abs(r['utcTicks']-t))
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
labels=Image.new('RGBA',(1920,1264)); draw=ImageDraw.Draw(labels)
for slot,_,_,_ in streams:
    x=(slot%2)*960; y=(slot//2)*632
    draw.rectangle((x,y,x+960,y+32),fill='#151820')
    draw.text((x+12,y+3),f'{slot+1}P · 독립 클라이언트 · 같은 실제 촬영 시각',font=font,fill='#ffcf9a')
labels.save(out/'labels.png')
errors=[]
for slot,directory,rows,times in streams:
    stream=streams[slot]; entries=['ffconcat version 1.0']; selected=[]
    for n in range(count):
        t=start+round(n/fps*1e7); row=nearest(stream,t); selected.append(row)
        errors.append(abs(row['utcTicks']-t)/1e4)
        entries.extend([f"file '{(directory/row['file']).as_posix()}'",f'duration {1/fps:.9f}'])
    entries.append(f"file '{(directory/selected[-1]['file']).as_posix()}'")
    concat=out/f'p{slot+1}.ffconcat'; concat.write_text('\n'.join(entries)+'\n',encoding='utf-8')
    if not args.reuse_videos: subprocess.run([args.ffmpeg,'-hide_banner','-loglevel','error','-y','-safe','0','-f','concat','-i',concat.as_posix(),
        '-an','-r',str(fps),'-frames:v',str(count),'-c:v','libx264','-preset','veryfast','-crf','21','-pix_fmt','yuv420p',
        '-movflags','+faststart',str(out/f'p{slot+1}.mp4')],check=True)
inputs=sum((['-i',str(out/f'p{i}.mp4')] for i in range(1,5)),[])
filters=';'.join(f'[{i}:v]pad=960:632:0:32:color=black[v{i}]' for i in range(4))
filters+=';[v0][v1][v2][v3]xstack=inputs=4:layout=0_0|w0_0|0_h0|w0_h0[stack];[stack][4:v]overlay=0:0[v]'
if not args.reuse_videos: subprocess.run([args.ffmpeg,'-hide_banner','-loglevel','error','-y',*inputs,'-loop','1','-i',str(out/'labels.png'),
    '-filter_complex',filters,'-map','[v]','-an','-frames:v',str(count),'-c:v','libx264','-preset','veryfast','-crf','22',
    '-pix_fmt','yuv420p','-movflags','+faststart',str(out/'four-player-full.mp4')],check=True)

# Audit the frames actually drawn, not just the requested/confirmed skill IDs.
cuts=[r for s in streams for r in s[2] if r['cutActor']>=0]
third=[r for r in streams[0][2] if r['cutActor']==0 and r['cutSkill']==17]
assert third, 'No actual local third-awakening overlay recorded'
comparisons=[]
for selfrow in third:
    observed=[]
    for stream in streams[1:]:
        row=nearest(stream,selfrow['utcTicks']); actor=next(p for p in row['actors'] if p['id']==0)
        observed.append(dict(local=row['local'],cutActor=row['cutActor'],skill=actor['skill'],age=actor['age'],
                             duration=actor['duration'],tick=row['tick'],sequence=actor['sequence'],rendered=actor.get('rendered'),effectAge=actor.get('effectAge'),skewMs=abs(row['utcTicks']-selfrow['utcTicks'])/1e4))
    comparisons.append(dict(utcTicks=selfrow['utcTicks'],localTick=selfrow['tick'],localSequence=selfrow['actors'][0]['sequence'],localAge=next(p for p in selfrow['actors'] if p['id']==0)['age'],others=observed))
checks=dict(only_local_overlays=all(r['cutActor']==r['local'] for r in cuts),
    third_awaken_shown=len(third)>=5,
    other_screens_no_overlay=all(o['cutActor']==-1 for c in comparisons for o in c['others']),
    # Remote actions arrive after local prediction. Verify active action and advancement;
    # do not mistake the local cutscene's start offset for a remote animation-age bound.
    other_screens_skill_running=all(o['skill']==17 and 0<o['age']<o['duration'] for c in comparisons for o in c['others']),
    other_screens_action_advances=all(max(c['others'][i]['age'] for c in comparisons)-min(c['others'][i]['age'] for c in comparisons)>.8 for i in range(3)),
    overlay_starts_mid_skill=3<=comparisons[0]['localAge']<3.25,
    matched_frames_within_150ms=all(o['skewMs']<=150 for c in comparisons for o in c['others']))
adjusted=[c['localAge']-o['age']-(c['localTick']-o['tick'])/60 for c in comparisons for o in c['others']]
checks['same_confirmed_action_sequence']=all(c['localSequence']==o['sequence'] for c in comparisons for o in c['others'])
checks['remote_action_clock_within_two_ticks']=max(map(abs,adjusted))<=2/60+.0001
visible=[o for c in comparisons for o in c['others'] if o['rendered'] and o['effectAge'] is not None and o['effectAge']>=0]
checks['same_room_effect_clock_matches_body']=bool(visible) and all(abs(o['effectAge']-o['age'])<=2/60+.0001 for o in visible)
report=dict(maxClockAdjustedActionLagSeconds=max(map(abs,adjusted)),sameRoomEffectSamples=len(visible),passed=all(checks.values()),checks=checks,durationSeconds=count/fps,framesPerView=[len(s[2]) for s in streams],
    videoFps=fps,maxResampleSkewMs=max(errors),localThirdOverlayFrames=len(third),comparisons=comparisons,
    maxCutComparisonSkewMs=max(o['skewMs'] for c in comparisons for o in c['others']),
    remoteActionAgeLagSeconds=dict(min=min(c['localAge']-o['age'] for c in comparisons for o in c['others']),
                                  max=max(c['localAge']-o['age'] for c in comparisons for o in c['others'])),
    scope='Actual four Unity processes, loopback; '+network_description+'; UTC nearest-frame alignment; silent video; not four physical PCs.')
(out/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
t=(third[len(third)//2]['utcTicks']-start)/1e7
if not args.reuse_videos: subprocess.run([args.ffmpeg,'-hide_banner','-loglevel','error','-y','-ss',str(max(0,t-5)), '-i',str(out/'four-player-full.mp4'),
    '-t','13','-an','-c:v','libx264','-preset','veryfast','-crf','20','-movflags','+faststart',str(out/'awakening-four-views.mp4')],check=True)
if not args.reuse_videos: subprocess.run([args.ffmpeg,'-hide_banner','-loglevel','error','-y','-ss',str(t),'-i',str(out/'four-player-full.mp4'),
    '-frames:v','1',str(out/'cutscene-comparison.png')],check=True)
(out/'index.html').write_text('''<!doctype html><meta charset="utf-8"><title>4인 시연 · 본인 컷신 검증</title>
<style>body{background:#13151b;color:#eee;font:17px/1.7 system-ui;max-width:1500px;margin:30px auto}video,img{width:100%}a{color:#ffc99e}</style>
<h1>4인 동시 시연</h1><p>실제 네 클라이언트의 가까운 촬영 시각을 맞췄습니다. 한 PC의 4개 프로세스, 방향별 30ms 지연. 무음 영상입니다. 촬영 시각 오차는 전체 최대 약 242ms이며 진각성 비교 구간은 약 50ms 이내입니다. 가까운 촬영 프레임 사이 동작 시간 차이는 약 0.25~0.35초입니다. 서버 틱 차이를 보정한 원격 재생 시각 오차는 아래 검증 결과에 기록했습니다.</p>
<h2>진각성 비교</h2><p>왼쪽 위 1P가 시전자입니다. 본인 화면만 불투명 컷신을 표시하고 나머지 세 화면은 전투를 계속 표시합니다.</p>
<video controls src="awakening-four-views.mp4"></video><img src="cutscene-comparison.png">
<h2>전체 전투</h2><video controls preload="metadata" src="four-player-full.mp4"></video><p><a href="verification.json">표시 시각·진행 상태 검증</a></p>''',encoding='utf-8')
page=(out/'index.html').read_text(encoding='utf-8')
page=page.replace('방향별 30ms 지연',network_description)
page=page.replace('<h2>진각성 비교</h2>',f"<p>서버 틱 차이를 보정한 원격 재생 시각 최대 오차: {report['maxClockAdjustedActionLagSeconds']*1000:.3f}ms. 같은 방의 이펙트/몸체 비교 {report['sameRoomEffectSamples']}건.</p><h2>진각성 비교</h2>")
page=page.replace('약 242ms',f"약 {report['maxResampleSkewMs']:.1f}ms").replace('약 50ms',f"약 {report['maxCutComparisonSkewMs']:.1f}ms").replace('약 0.25~0.35초',f"약 {report['remoteActionAgeLagSeconds']['min']:.2f}~{report['remoteActionAgeLagSeconds']['max']:.2f}초")
if (root/'rooms/verification.json').exists():
    page=page.replace('<h2>진각성 비교</h2>','<h2>3×3 방과 2인 표식</h2><p>표식은 10초마다 두 명, 운반 시간 7초, 화재 16초입니다. 1·9번과 3·7번 대각선 방에 번갈아 유도합니다. 직격은 최대 HP 95%, 화재는 매초 최대 HP 20%입니다.</p><img src="../rooms/rooms-comparison.png"><h3>이전 불바다와 다음 표식 중첩</h3><img src="../rooms/overlap-map.png"><h2>보스 HP의 흰 피해 잔상</h2><img src="../rooms/boss-hp-trail.png"><p><a href="../rooms/verification.json">방·표식·HUD 검사</a></p><h2>진각성 비교</h2>')
(out/'index.html').write_text(page,encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='comparisons'},ensure_ascii=False,indent=2))
raise SystemExit(0 if report['passed'] else 1)
