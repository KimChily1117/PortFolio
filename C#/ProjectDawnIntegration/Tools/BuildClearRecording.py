"""Four-view UTC-aligned clear cinematic evidence (no fabricated game frames)."""
import argparse,bisect,json,subprocess
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
p=argparse.ArgumentParser();p.add_argument('run',type=Path);p.add_argument('--ffmpeg',required=True);a=p.parse_args()
root=a.run.resolve();out=root/'video';out.mkdir(exist_ok=True)
streams=[]
for path in root.glob('p[1-4]/recording/frames.jsonl'):
    rows=[json.loads(s) for s in path.read_text(encoding='utf-8-sig').splitlines()]
    streams.append((rows[0]['local'],path.parent,rows,[r['utcTicks'] for r in rows]))
streams.sort();assert [s[0] for s in streams]==[0,1,2,3]
start=max(s[3][0] for s in streams);end=min(s[3][-1] for s in streams);fps=12;count=int((end-start)/1e7*fps)
def ff(args):subprocess.run([a.ffmpeg,'-hide_banner','-loglevel','error','-y',*map(str,args)],check=True)
for slot,directory,rows,times in streams:
    entries=['ffconcat version 1.0']
    for n in range(count):
        t=start+n/fps*1e7;k=bisect.bisect_left(times,t)
        row=min((rows[j] for j in [k-1,k] if 0<=j<len(rows)),key=lambda r:abs(r['utcTicks']-t))
        entries.extend([f"file '{(directory/row['file']).as_posix()}'",f'duration {1/fps:.9f}'])
    entries.append(f"file '{(directory/row['file']).as_posix()}'")
    concat=out/f'p{slot+1}.ffconcat';concat.write_text('\n'.join(entries),encoding='utf-8')
    ff(['-safe','0','-f','concat','-i',concat,'-an','-r',fps,'-frames:v',count,'-c:v','libx264','-preset','veryfast','-crf','22','-pix_fmt','yuv420p',out/f'p{slot+1}.mp4'])
labels=Image.new('RGBA',(1920,1264));draw=ImageDraw.Draw(labels);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
for i in range(4):
    x=i%2*960;y=i//2*632;draw.rectangle((x,y,x+960,y+32),fill='#151820');draw.text((x+10,y+2),f'{i+1}P · 클리어 시네마틱 · 서버 과반수 스킵',font=font,fill='#ffcf9a')
labels.save(out/'labels.png')
filters=';'.join(f'[{i}:v]pad=960:632:0:32:color=black[v{i}]' for i in range(4))+';[v0][v1][v2][v3]xstack=inputs=4:layout=0_0|w0_0|0_h0|w0_h0[s];[s][4:v]overlay=0:0[v]'
ff([*sum((['-i',out/f'p{i}.mp4'] for i in range(1,5)),[]),'-loop','1','-i',out/'labels.png','-filter_complex',filters,'-map','[v]','-an','-frames:v',count,'-c:v','libx264','-preset','veryfast','-crf','22','-pix_fmt','yuv420p','-movflags','+faststart',out/'four-player-full.mp4'])
(out/'verification.json').write_text(json.dumps(dict(durationSeconds=count/fps,frames=count,scope='Actual UTC-aligned local four-client fixture recording; see cinematic-verification.json')),encoding='utf-8')
(out/'index.html').write_text('''<!doctype html><meta charset="utf-8"><title>클리어 · 시네마틱 · 과반수 스킵</title><style>body{background:#14171f;color:#eee;font:18px/1.7 system-ui;max-width:1500px;margin:30px auto}video{width:100%}a{color:#ffc99e}</style><h1>4인 클리어 시네마틱</h1><p>실제 로컬 4개 클라이언트. 검증 시간을 줄이기 위해 보스 HP를 낮춘 테스트이며, 처치·클리어 확정·투표·SQL 보상 지급은 실제 경로입니다. 무음 영상입니다.</p><video controls src="four-player-full.mp4"></video><p><a href="../cinematic-verification.json">시네마틱 검사</a> · <a href="../reward-verification.json">SQL 보상 검사</a></p>''',encoding='utf-8')
subprocess.run([__import__('sys').executable,str(Path(__file__).with_name('MuxFourAudio.py')),str(root),'--ffmpeg',a.ffmpeg,'--no-awakening'],check=True)
