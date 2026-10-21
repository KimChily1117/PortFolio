"""Contact sheets made only from actual captured frames, with simulation-time labels."""
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(sys.argv[1]).resolve()
data = json.loads((root / 'gallery.json').read_text(encoding='utf-8-sig'))
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 16)
special = {
    'awaken1': [0.7, 1.3, 2.1, 2.6, 3.5, 4.8],
    'awaken3': [0.8, 1.6, 2.5, 4.0, 5.8, 6.8],
    'outrage': [.7, 1.0, 1.1, 1.5, 1.9, 2.3],
    'awaken2': [.65, 1.1, 1.5, 1.95, 2.5, 3.1],
}
overview = Image.new('RGB', (1680, 265 * ((len(data['skills']) + 3) // 4)), '#151820')
od = ImageDraw.Draw(overview)
for index, skill in enumerate(data['skills']):
    if not skill['frames']:
        continue
    duration = max(f['actionDuration'] for f in skill['frames'])
    times = special.get(skill['key'], [.52 + duration * x for x in [0, .2, .4, .6, .8, 1.2]])
    contact = Image.new('RGB', (840, 795), '#151820')
    draw = ImageDraw.Draw(contact)
    chosen = []
    for n, seconds in enumerate(times):
        frame = min(skill['frames'], key=lambda f: abs(f['seconds'] - seconds))
        shot = Image.open(root / frame['file']).convert('RGB')
        # Original screen coordinates; crop is for close inspection, never used as game output.
        shot = shot.crop((220, 200, 1060, 650)).resize((420, 225))
        x, y = (n % 2) * 420, (n // 2) * 265
        draw.text((x + 8, y + 5), f"{skill['name']}  {frame['seconds']:.2f}s", font=font, fill='#ffd5a5')
        contact.paste(shot, (x, y + 35))
        chosen.append(shot)
    contact.save(root / f"review-{skill['key']}.png")
    x, y = (index % 4) * 420, (index // 4) * 265
    od.text((x + 8, y + 5), skill['name'], font=font, fill='#ffd5a5')
    overview.paste(chosen[3], (x, y + 35))
overview.save(root / 'skills-overview.png')
print(root / 'skills-overview.png')
