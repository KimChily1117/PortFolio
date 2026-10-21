"""Build the approved recording into a short portrait gameplay highlight.

All video and audio come from the completed four-client recording. Alternate
camera shots retain the synchronized 1P game mix; no external music is added.
"""
from pathlib import Path
import json
import subprocess
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'Artifacts/portfolio-4p-20261022-062155/video'
OUT = ROOT / 'Artifacts/instagram-story-20261022'
FF = Path('E:/task/ProjectDawnTools/media/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe')
FONT = ROOT / 'UnityClient/Assets/Resources/Fonts/DNFForgedBlade-Medium.ttf'
MIX = SRC / 'main-player-with-audio.mp4'
OUT.mkdir(parents=True, exist_ok=True)

# Source seconds, kept chronological. Short cuts remove waiting and empty rooms.
SHOTS = [
    ('main-player-with-audio', 66.8, 69.5, 0),
    ('main-player-with-audio', 71.0, 74.5, 0),
    ('p3', 85.0, 88.2, 1),
    ('main-player-with-audio', 110.0, 114.2, 1),
    ('main-player-with-audio', 118.5, 121.0, 2),
    ('main-player-with-audio', 123.0, 127.2, 2),
    ('main-player-with-audio', 131.0, 134.2, 3),
    ('p3', 154.0, 157.0, 3),
    ('p3', 160.5, 163.7, 3),
    ('main-player-with-audio', 197.0, 199.0, 4),
    ('main-player-with-audio', 211.0, 218.0, 4),
]
CHAPTERS = [
    ('01 / ENCOUNTER', '폭룡과의 조우', '전투가 시작되는 순간.'),
    ('02 / COMBAT', '몰아치는 전투', '피하고, 파고들고, 베어낸다.'),
    ('03 / TRANSFORMATION', '깨어나는 폭룡', '전장이 바뀌면, 전투도 바뀐다.'),
    ('04 / PHASE TWO', '끝까지 함께', '4인 협동 레이드'),
    ('05 / RAID CLEAR', '기계 혁명 성공', '마지막은, 금빛으로.'),
]

def run(args, log):
    with open(OUT / log, 'w', encoding='utf-8') as f:
        subprocess.run([str(FF), '-hide_banner', '-y', *args], stdout=f, stderr=f, check=True)

def font(size):
    return ImageFont.truetype(str(FONT), size)

def centered(draw, text, y, size, fill):
    ft = font(size)
    box = draw.textbbox((0, 0), text, font=ft)
    draw.text(((1080 - (box[2] - box[0])) / 2, y), text, font=ft, fill=fill)

timeline = []
cursor = 0.0
for i, (source, start, end, chapter) in enumerate(SHOTS):
    duration = round(end - start, 3)
    # 30fps delivery duplicates existing captured frames, without synthetic motion.
    run(['-ss', str(start), '-i', str(SRC / (source + '.mp4')),
         '-ss', str(start), '-i', str(MIX), '-t', str(duration),
         '-map', '0:v:0', '-map', '1:a:0',
         '-vf', 'scale=960:600:flags=lanczos,setsar=1,fps=30',
         '-af', f'afade=t=in:d=0.035,afade=t=out:st={duration-0.045}:d=0.045',
         '-c:v', 'libx264', '-preset', 'fast', '-crf', '18', '-pix_fmt', 'yuv420p',
         '-c:a', 'aac', '-b:a', '192k', '-ar', '48000', '-ac', '2',
         str(OUT / f'cut-{i:02d}.mp4')], f'cut-{i:02d}.log')
    timeline.append(dict(shot=i, source=source+'.mp4', source_start=start,
                         source_end=end, output_start=round(cursor,3),
                         output_end=round(cursor+duration,3), chapter=chapter))
    cursor += duration
    print(f'Cut {i+1}/{len(SHOTS)} ready', flush=True)

(OUT / 'cuts.txt').write_text(''.join(f"file 'cut-{i:02d}.mp4'\n" for i in range(len(SHOTS))), encoding='utf-8')
run(['-f', 'concat', '-safe', '0', '-i', str(OUT/'cuts.txt'), '-c', 'copy',
     '-movflags', '+faststart', str(OUT/'bakal-highlights-landscape.mp4')], 'concat.log')

inputs = ['-i', str(OUT/'bakal-highlights-landscape.mp4')]
for i, (label, title, subtitle) in enumerate(CHAPTERS):
    im = Image.new('RGBA', (1080,1920))
    d = ImageDraw.Draw(im)
    # Keep text away from story app top/bottom controls. Full gameplay is 1080x675.
    centered(d, 'PROJECT DAWN', 276, 38, '#E5D7BD')
    centered(d, 'BAKAL RAID', 330, 88, '#FFFFFF')
    d.line((458, 455, 622, 455), fill='#D64336', width=4)
    centered(d, label, 507, 24, '#DCC5A2')
    d.line((0, 621, 1080, 621), fill=(244,188,105,100), width=2)
    d.line((0, 1299, 1080, 1299), fill=(244,188,105,100), width=2)
    centered(d, title, 1373, 62, '#FFF4DF')
    centered(d, subtitle, 1462, 29, '#CFC4BA')
    centered(d, '4 PLAYER CO-OP  /  GAMEPLAY HIGHLIGHTS', 1630, 21, '#9F948D')
    for j in range(5):
        x = 447+j*40
        d.rounded_rectangle((x,1563,x+22,1567), radius=2, fill='#E8BD77' if j==i else '#514440')
    path = OUT/f'overlay-{i}.png'
    im.save(path)
    inputs += ['-i', str(path)]

filters = [
    '[0:v]split=2[bg][fg]',
    '[bg]scale=270:480:force_original_aspect_ratio=increase,crop=270:480,boxblur=12:2,'
    'scale=1080:1920,eq=brightness=-0.16:saturation=0.65[back]',
    '[back]drawbox=x=0:y=0:w=iw:h=ih:color=black@0.48:t=fill[dark]',
    '[fg]scale=1080:675:flags=lanczos[game]',
    '[dark][game]overlay=0:623[base0]',
]
for i in range(len(CHAPTERS)):
    first = min(x['output_start'] for x in timeline if x['chapter']==i)
    last = max(x['output_end'] for x in timeline if x['chapter']==i)
    filters.append(f"[base{i}][{i+1}:v]overlay=0:0:enable='gte(t,{first})*lt(t,{last})'[base{i+1}]")
filters.append(f'[base5]fade=t=out:st={cursor-0.5}:d=0.5,format=yuv420p[out]')
run([*inputs, '-filter_complex', ';'.join(filters), '-map','[out]', '-map','0:a:0',
     '-t',str(round(cursor,3)), '-af',f'afade=t=out:st={cursor-0.5}:d=0.5',
     '-c:v','libx264','-preset','fast','-crf','19','-c:a','aac','-b:a','192k',
     '-movflags','+faststart',str(OUT/'bakal-instagram-story.mp4')], 'portrait-render.log')
(OUT/'edit-timeline.json').write_text(json.dumps(dict(duration=round(cursor,3),
    resolution=[1080,1920], delivery_fps=30, notes='Source capture frames retained; no motion interpolation. Original synchronized 1P audio mix.',
    chapters=CHAPTERS, shots=timeline), ensure_ascii=False, indent=2),encoding='utf-8')
print(f'Complete: {cursor:.1f}s portrait and landscape highlights',flush=True)
