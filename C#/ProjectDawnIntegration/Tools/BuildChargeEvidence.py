"""Make a short charge clip and comparison images from actual four-client captures."""
import argparse
import bisect
import json
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument('run', type=Path)
parser.add_argument('--ffmpeg', required=True)
args = parser.parse_args()
root = args.run.resolve()
out = root / 'video'
report = json.loads((root / 'charge-verification.json').read_text(encoding='utf-8-sig'))
assert report['passed'], 'Charge verification must pass first'
streams = []
for directory in sorted(root.glob('p[1-4]/recording')):
    rows = [json.loads(s) for s in (directory / 'frames.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    streams.append((rows[0]['local'], directory, rows, [r['utcTicks'] for r in rows]))
streams.sort()
assert [s[0] for s in streams] == [0, 1, 2, 3]
groups = [g for g in report['chargeGroups'] if len(g) == 3]
assert groups
group = groups[min(1, len(groups) - 1)]
leader = streams[0][2]
begin, end = group[0]['tick'], group[-1]['tick'] + 150
warning = min((r for s in streams for r in s[2] if begin <= r['tick'] <= end
               and r['chargeWave'] == 1 and r['chargeTarget'] >= 0
               and r['room'] == 4 and r['chargeAge'] < r['chargeTell']),
              key=lambda r: abs(r['tick'] - (begin + 25)))
fire = max((r for s in streams for r in s[2] if begin <= r['tick'] <= end and r['room'] == 4),
           key=lambda r: r['chargeTrailCount'])
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 20)
skews = []
for row, filename in [(warning, 'charge-four-views.jpg'), (fire, 'short-charge-fire.jpg')]:
    canvas = Image.new('RGB', (1920, 1264), '#151820')
    draw = ImageDraw.Draw(canvas)
    for slot, directory, rows, times in streams:
        k = bisect.bisect_left(times, row['utcTicks'])
        selected = min((rows[j] for j in (k - 1, k) if 0 <= j < len(rows)),
                       key=lambda r: abs(r['utcTicks'] - row['utcTicks']))
        x, y = slot % 2 * 960, slot // 2 * 632
        with Image.open(directory / selected['file']) as image:
            canvas.paste(image, (x, y + 32))
        draw.text((x + 12, y + 3), f'{slot + 1}P · 실제 촬영 · 서버 틱 {selected["tick"]}', font=font, fill='#ffcf9a')
        skews.append(abs(selected['utcTicks'] - row['utcTicks']) / 1e4)
    canvas.save(out / filename, quality=94)
start = max(s[3][0] for s in streams)
clip_row = min(leader, key=lambda r: abs(r['tick'] - (begin - 30)))
offset = max(0, (clip_row['utcTicks'] - start) / 1e7)
subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-ss', str(offset),
                '-i', str(out / 'four-player-full.mp4'), '-t', '12', '-c:v', 'libx264',
                '-preset', 'veryfast', '-crf', '21', '-an', '-movflags', '+faststart',
                str(out / 'boss-charge-four-players.mp4')], check=True)
evidence = dict(chargeGroup=group, videoStartSeconds=offset, durationSeconds=12,
                maxImageCaptureSkewMs=max(skews), warningTick=warning['tick'], fireTick=fire['tick'])
(out / 'charge-evidence.json').write_text(json.dumps(evidence, indent=2), encoding='utf-8')
page = (out / 'index.html').read_text(encoding='utf-8')
section = '''<h2>지목 후 3연속 돌진과 짧은 불길</h2>
<p>회차별 지목 → 0.8초 예고 → 돌진. 2.7초 간격으로 3회 반복하며 경로의 약한 불길은 2.5초간 유지됩니다.
불길은 기본 피해 3/초, 겹쳐도 중복 틱 없이 방어력과 점프 회피가 적용됩니다.</p>
<video controls preload="metadata" src="boss-charge-four-players.mp4"></video>
<img src="charge-four-views.jpg"><img src="short-charge-fire.jpg">
<p><a href="../charge-verification.json">돌진·불길 검증</a> · <a href="charge-evidence.json">촬영 근거</a></p>'''
if 'boss-charge-four-players.mp4' not in page:
    page = page.replace('<h2>전체 전투</h2>', section + '<h2>전체 전투</h2>')
(out / 'index.html').write_text(page, encoding='utf-8')
print(json.dumps(evidence, indent=2))
