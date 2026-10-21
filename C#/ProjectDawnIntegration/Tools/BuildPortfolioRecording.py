"""Encode real UTC aligned game captures and their original AudioListener mix."""
import argparse,bisect,json,subprocess,html
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
p=argparse.ArgumentParser();p.add_argument('run',type=Path);p.add_argument('--ffmpeg',required=True);a=p.parse_args()
root=a.run.resolve();out=root/'video';out.mkdir(exist_ok=True)
streams=[]
for slot in range(4):
 d=root/f'p{slot+1}'/'recording';rows=[json.loads(s) for s in (d/'frames.jsonl').read_text(encoding='utf-8-sig').splitlines()]
 streams.append((d,rows,[r['utcTicks'] for r in rows]))
# Start after dismissing the returning account's previous receipt, so the film
# opens in town rather than showing rewards from the rehearsal.
start=next(r['utcTicks'] for r in streams[0][1] if r['stage']=='Town' and not r['rewards']);end=streams[0][2][-1];fps=12;count=int((end-start)/1e7*fps)
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',21)
blank=Image.new('RGB',(960,600),'#10131a');ImageDraw.Draw(blank).text((330,280),'클라이언트 접속 준비 중',font=font,fill='#f3c994');blank.save(out/'waiting.jpg')
def ff(args):subprocess.run([a.ffmpeg,'-hide_banner','-loglevel','error','-y',*map(str,args)],check=True)
for slot,(directory,rows,times) in enumerate(streams):
 entries=['ffconcat version 1.0']
 for n in range(count):
  t=start+n/fps*1e7;k=bisect.bisect_right(times,t)-1
  file=out/'waiting.jpg' if k<0 else directory/rows[k]['file']
  entries.extend([f"file '{file.as_posix()}'",f'duration {1/fps:.9f}'])
 entries.append(f"file '{file.as_posix()}'")
 concat=out/f'p{slot+1}.ffconcat';concat.write_text('\n'.join(entries),encoding='utf-8')
 ff(['-safe','0','-f','concat','-i',concat,'-an','-r',fps,'-frames:v',count,'-c:v','libx264','-preset','veryfast','-crf','21','-pix_fmt','yuv420p',out/f'p{slot+1}.mp4'])
labels=Image.new('RGBA',(1920,1264));draw=ImageDraw.Draw(labels)
names=['새벽의검','붉은달','핏빛서약','여명의빛']
for i in range(4):
 x=i%2*960;y=i//2*632;draw.rectangle((x,y,x+960,y+32),fill='#10131a');draw.text((x+12,y+2),f'{i+1}P · {names[i]} | AWS TLS · 실제 서버 검증',font=font,fill='#f3c994')
labels.save(out/'labels.png')
filters=';'.join(f'[{i}:v]pad=960:632:0:32:color=black[v{i}]' for i in range(4))+';[v0][v1][v2][v3]xstack=inputs=4:layout=0_0|w0_0|0_h0|w0_h0[s];[s][4:v]overlay=0:0[v]'
ff([*sum((['-i',out/f'p{i}.mp4'] for i in range(1,5)),[]),'-loop','1','-i',out/'labels.png','-filter_complex',filters,'-map','[v]','-an','-frames:v',count,'-c:v','libx264','-preset','veryfast','-crf','21','-pix_fmt','yuv420p','-movflags','+faststart',out/'four-player-silent.mp4'])
audio=json.loads((streams[0][0]/'game-audio.json').read_text());assert audio['samples']>0 and audio['rms']>.0001 and audio['droppedBlocks']==0
offset=(start-audio['firstSampleUtcTicks'])/1e7;duration=count/fps
audioDuration=audio['samples']/audio['sampleRate']/audio['channels'];assert audioDuration-max(0,offset)>duration-.5
timing=f'atrim=start={offset},asetpts=PTS-STARTPTS' if offset>=0 else f'adelay={-offset*1000:.3f}:all=1'
gain=min(1,.95/max(.001,audio['peak']))
for source,dest in [('p1.mp4','main-player-with-audio.mp4'),('four-player-silent.mp4','four-player-with-audio.mp4')]:
 ff(['-i',out/source,'-i',streams[0][0]/'game-audio.wav','-map','0:v:0','-map','1:a:0','-af',timing+f',volume={gain},apad','-t',duration,'-c:v','copy','-c:a','aac','-b:a','192k','-movflags','+faststart',out/dest])
rows=streams[0][1];chapters=[]
audit=(root/'p1/flow-audit.txt').read_text(encoding='utf-8-sig').splitlines()
townTime=next(float(s.split(' ',1)[0]) for s in audit if s.endswith(' Town'))+.35
chatTimes={}
for scope in ['Town','Raid']:
 line=next((s for s in audit if s.endswith('PortfolioChatQueued '+scope)),None)
 if line:chatTimes[scope]=max(0,float(line.split(' ',1)[0])-townTime-(start-streams[0][2][0])/1e7)
def chapter(title,predicate):
 row=next((r for r in rows if r['utcTicks']>=start and predicate(r)),None)
 if row:chapters.append(dict(title=title,seconds=round((row['utcTicks']-start)/1e7,2),file=row['file']))
chapter('마을 · 채팅',lambda r:r['stage']=='Town')
if 'Town' in chatTimes:chapters.append(dict(title='Enter → 마을 채팅 → 말풍선',seconds=round(chatTimes['Town'],2)))
chapter('순차 매칭 · 대기 · 카운트다운',lambda r:r['stage']=='Matching')
chapter('보스 조우',lambda r:r['stage']=='Raid')
chapter('1페이즈 전투',lambda r:r['stage']=='Raid' and r['seconds']>1)
if 'Raid' in chatTimes:chapters.append(dict(title='던전 파티 채팅 · 방 이동',seconds=round(chatTimes['Raid'],2)))
chapter('2페이즈 전환',lambda r:r['phaseTransition'])
chapter('2페이즈 전투',lambda r:r['phase']==2 and not r['phaseTransition'] and r['combatBossHp']>0)
chapter('처치 · 클리어 시네마틱',lambda r:r['clearCinematic'])
chapter('보상 공개',lambda r:r['rewards'] and r['stage']=='Raid' and r['combatBossHp']==0)
item=json.loads((root/'p1/db-before-equip.json').read_text(encoding='utf-8-sig'))['item']
chapter('AWS MySQL 저장 확인 · 획득 장비 선택',lambda r:r['stage']=='Town' and r['selected']==item)
chapter('획득 장비 장착 · DB 재확인',lambda r:r['stage']=='Town' and r['selected']==item and r['equipped'])
if len(chatTimes)==2:
 for scope,t in chatTimes.items():
  ff(['-ss',max(0,t-2),'-i',out/'main-player-with-audio.mp4','-t',23,'-c:v','libx264','-preset','veryfast','-crf',20,'-c:a','aac','-b:a','192k',out/f'chat-{scope.lower()}.mp4'])
 concat=out/'chat.ffconcat';concat.write_text("ffconcat version 1.0\nfile 'chat-town.mp4'\nfile 'chat-raid.mp4'\n",encoding='utf-8')
 ff(['-safe','0','-f','concat','-i',concat,'-c','copy','-movflags','+faststart',out/'chat-town-and-dungeon.mp4'])
(out/'chapters.json').write_text(json.dumps(chapters,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'recording-verification.json').write_text(json.dumps(dict(durationSeconds=duration,frames=count,audio=audio,audioOffsetSeconds=offset,audioDuration=audioDuration,audioGain=gain,scope='One PC, four visible Unity clients, ordinary automated inputs, existing AWS TLS/MySQL server, unchanged production boss HP. Original AudioListener audio. 12 fps output repeats latest actual capture at its UTC time; no fabricated combat frames.'),ensure_ascii=False,indent=2),encoding='utf-8')
buttons=''.join(f'<button onclick="seek({c["seconds"]})">{int(c["seconds"]//60):02}:{int(c["seconds"]%60):02} {html.escape(c["title"])}</button>' for c in chapters)
(out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Project Dawn · AWS 4인 시연</title><style>body{margin:0;background:#0c1018;color:#e7eaf0;font:16px/1.7 system-ui}main{max-width:1500px;margin:36px auto;padding:0 24px}h1{font-size:38px;color:#f3c994}video{width:100%;background:#000;border:1px solid #31394a;border-radius:12px}button,a{color:#efce9d}button{padding:10px 14px;background:#192234;border:1px solid #354057;border-radius:7px;margin:4px;cursor:pointer}.info{padding:18px;background:#151d2b;border-radius:10px}.switch{margin:18px 0}</style><main><h1>Project Dawn · AWS 4인 레이드</h1><p>마을·채팅 → 순차 매칭 → 1·2페이즈 전투 → 처치 → 보상 → 실제 MySQL 저장 확인 → 획득 장비 장착</p><div class="info">한 Windows PC의 클라이언트 4개가 AWS 공개 TLS 서버에 접속했습니다. 일반 입력을 자동화했으며 보스 체력·피해·서버 판정은 변경하지 않았습니다. DB 표시는 SSH로 읽은 실제 보상·아이템 행이며, 장착은 게임의 일반 서버 요청을 사용합니다.</div><div class="switch"><button onclick="switchVideo('main-player-with-audio.mp4')">1P 크게 보기</button><button onclick="switchVideo('four-player-with-audio.mp4')">4인 동시 보기</button></div><video id="v" controls preload="metadata" src="main-player-with-audio.mp4"></video><section>'''+buttons+'''</section><p><a href="../portfolio-verification.json">전투·채팅·DB 검증</a> · <a href="../aws-db-before-equip.json">장착 전 SQL</a> · <a href="../aws-db-after-equip.json">장착 후 SQL</a> · <a href="recording-verification.json">녹화 방식·오디오 검증</a></p><p>12fps UTC 동기화 녹화. 소리는 1P 게임 AudioListener의 실제 BGM·효과음이며 추가 음원으로 덮지 않았습니다.</p></main><script>const v=document.getElementById('v');function seek(t){v.currentTime=t;v.play()}function switchVideo(s){const t=v.currentTime,p=!v.paused;v.src=s;v.addEventListener('loadedmetadata',()=>{v.currentTime=t;if(p)v.play()},{once:true})}</script></html>''',encoding='utf-8')
page=out/'index.html'
page.write_text(page.read_text(encoding='utf-8').replace('</main>','<h2>채팅 테스트만 보기 · 마을 → 던전</h2><p>실제 입력창의 Enter·문자·Enter 이벤트를 자동화한 발췌입니다. 두 구간 사이에 컷이 있습니다.</p><video controls preload="metadata" src="chat-town-and-dungeon.mp4"></video></main>'),encoding='utf-8')
v=json.loads((root/'portfolio-verification.json').read_text(encoding='utf-8'))
if not v['networkClean']:
 page.write_text(page.read_text(encoding='utf-8').replace('<div class="switch">',f'<p class="info">확인된 잔여 이슈: 실제 서버의 TooLate 입력 거절 {v["tooLateCount"]}건. 시연의 클리어·보상·장착은 완료했으며, 입력 거절 0건 안정성 검증은 통과한 것으로 처리하지 않았습니다.</p><div class="switch">'),encoding='utf-8')
print(json.dumps(dict(duration=duration,chapters=chapters),ensure_ascii=False))
