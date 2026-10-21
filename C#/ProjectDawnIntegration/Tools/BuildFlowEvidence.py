"""Arrange actual Unity captures without altering their contents."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(sys.argv[1]).resolve()
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 21)
def sheet(entries, columns, name):
    canvas = Image.new('RGB', (640 * columns, 435 * ((len(entries)+columns-1)//columns)), '#11131a')
    draw = ImageDraw.Draw(canvas)
    for i, (label, path) in enumerate(entries):
        x, y = (i % columns)*640, (i // columns)*435
        draw.text((x+12, y+5), label, font=font, fill='#ffcc88')
        shot = Image.open(path).convert('RGB')
        shot.thumbnail((640, 400))
        canvas.paste(shot, (x, y+35))
    canvas.save(root/name)

sheet([(f'클라이언트 {i} · 레이드', root/f'p{i}/raid.png') for i in range(1,5)], 2, 'four-clients.png')
sheet([(label, root/'p1'/file) for label,file in [
    ('기존 로딩 화면', 'loading-Town.png'), ('마을 · 서버 승인 코디', 'town-gate.png'),
    ('레이드 · 같은 코디', 'raid.png'), ('마을 복귀 · 외형 유지', 'returned-town.png')]], 2, 'flow-overview.png')
sheet([(f'클라이언트 {i} · {label}', root/f'p{i}'/file)
       for i in range(1,5) for label,file in [('마을','town-gate.png'),('복귀','returned-town.png')]], 2, 'avatar-round-trip.png')
if all((root/f'p{i}/chat-dungeon.png').is_file() for i in range(1,5)):
    sheet([(f'클라이언트 {i} · 파티 채팅', root/f'p{i}/chat-dungeon.png')
           for i in range(1,5)], 2, 'four-chat-clients.png')
print(root/'flow-overview.png')
