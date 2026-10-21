"""Mux one client's real AudioListener capture into UTC-aligned four-view evidence."""
import argparse, json, subprocess
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('run',type=Path);p.add_argument('--ffmpeg',required=True);p.add_argument('--no-awakening',action='store_true');a=p.parse_args()
root=a.run.resolve();out=root/'video'
streams=[]
for path in root.glob('p[1-4]/recording/frames.jsonl'):
    rows=[json.loads(line) for line in path.read_text(encoding='utf-8-sig').splitlines()]
    streams.append((rows[0]['local'],path.parent,rows))
streams.sort();assert [s[0] for s in streams]==[0,1,2,3]
directory=streams[0][1];audio=json.loads((directory/'game-audio.json').read_text())
assert audio['samples']>0 and audio['rms']>.0001 and audio['channels']>0,'Empty/silent capture'
assert audio['droppedBlocks']==0,'Dropped DSP blocks'
start=max(s[2][0]['utcTicks'] for s in streams)
offset=(start-audio['firstSampleUtcTicks'])/1e7
verification=json.loads((out/'verification.json').read_text(encoding='utf-8'))
duration=verification['durationSeconds']
audio_duration=audio['samples']/audio['channels']/audio['sampleRate']
assert audio_duration-max(0,offset)>=duration-.25,'Audio ends before video'
gain=min(1,.95/max(.001,audio['peak']))
timing=f'atrim=start={offset},asetpts=PTS-STARTPTS' if offset>=0 else f'adelay={-offset*1000:.3f}:all=1'
def ff(args):
    subprocess.run([a.ffmpeg,'-hide_banner','-loglevel','error','-y',*map(str,args)],check=True)
ff(['-i',out/'four-player-full.mp4','-i',directory/'game-audio.wav','-map','0:v:0','-map','1:a:0',
    '-af',timing+f',volume={gain},apad','-t',duration,'-c:v','copy','-c:a','aac','-b:a','192k',
    '-movflags','+faststart',out/'four-player-with-audio.mp4'])
if not a.no_awakening:
    third=[r for r in streams[0][2] if r['cutActor']==0 and r['cutSkill']==17]
    t=(third[len(third)//2]['utcTicks']-start)/1e7
    ff(['-ss',max(0,t-5),'-i',out/'four-player-with-audio.mp4','-t','13',
        '-c:v','libx264','-preset','veryfast','-crf','20','-c:a','aac','-movflags','+faststart',out/'awakening-with-audio.mp4'])
report=dict(passed=True,audio=audio,offsetSeconds=offset,audioDuration=audio_duration,videoDuration=duration,
    gain=gain,scope='Actual local player 0 game mix, not dubbed; audio callback UTC alignment has DSP buffering uncertainty. One PC / four clients, not four PCs.')
(out/'audio-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
page=(out/'index.html').read_text(encoding='utf-8').replace('awakening-four-views.mp4','awakening-with-audio.mp4').replace('four-player-full.mp4','four-player-with-audio.mp4')
page=page.replace('무음 영상입니다.','1P 게임에서 실제 재생된 BGM·효과음을 녹음했습니다. 다른 세 클라이언트와 PC의 다른 소리는 포함하지 않습니다.')
page+='<p><a href="audio-verification.json">실제 오디오 수신·레벨·누락 검사</a></p>'
(out/'index.html').write_text(page,encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
