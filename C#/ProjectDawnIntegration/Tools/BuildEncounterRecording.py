"""Encode actual Unity end-of-frame captures, preserving recorded wall-clock gaps. No audio capture."""
import argparse, json, subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('run',type=Path);p.add_argument('--ffmpeg',required=True)
a=p.parse_args();run=a.run.resolve();rows=json.loads((run/'frames.json').read_text(encoding='utf-8'))['frames']
assert len(rows)>20
lines=[]
for i,row in enumerate(rows):
    name=row['filename'];assert Path(name).name==name and "'" not in name
    assert (run/'frames'/name).is_file()
    lines.append("file 'frames/"+name+"'")
    dt=rows[i+1]['timestamp']-row['timestamp'] if i+1<len(rows) else 1/12
    assert 0<dt<5
    lines.append(f'duration {dt:.6f}')
lines.append("file 'frames/"+rows[-1]['filename']+"'")
(run/'frames.concat').write_text('\n'.join(lines),encoding='utf-8')
subprocess.run([a.ffmpeg,'-y','-hide_banner','-loglevel','error','-f','concat','-safe','0','-i',str(run/'frames.concat'),'-vf','fps=30','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(run/'encounter-demo.mp4')],check=True)
print(json.dumps(dict(frames=len(rows),duration=rows[-1]['timestamp']-rows[0]['timestamp'],audio=False,path=str(run/'encounter-demo.mp4'))))
