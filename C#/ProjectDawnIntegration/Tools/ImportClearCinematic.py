"""Convert a supplied local MP4 to the Windows Unity playback format, with provenance."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('--ffmpeg',required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[1];out=root/'Artifacts/clear-cinematic-20261022';out.mkdir(parents=True,exist_ok=True)
target=out/'bakal-clear-baseline.mp4'
subprocess.run([a.ffmpeg,'-hide_banner','-loglevel','error','-y','-i',str(a.source),'-map','0:v:0','-map','0:a:0',
    '-c:v','libx264','-profile:v','baseline','-level:v','3.1','-pix_fmt','yuv420p','-bf','0','-preset','fast','-crf','20',
    '-r','30','-c:a','aac','-ar','48000','-ac','2','-b:a','192k','-movflags','+faststart',str(target)],check=True)
destination=root/'UnityClient/Assets/StreamingAssets/Cinematics/bakal-clear.mp4';destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(target,destination)
(out/'provenance.json').write_text(json.dumps(dict(source=str(a.source.resolve()),sourceSha256=hashlib.sha256(a.source.read_bytes()).hexdigest(),
    outputSha256=hashlib.sha256(target.read_bytes()).hexdigest(),output=str(destination.relative_to(root)),
    conversion='H.264 baseline/no B frames, 1280x720 30fps, AAC stereo 48kHz; complete supplied video and audio, no trimming.',
    serverFadeMs=2000,serverVideoMs=40720),ensure_ascii=False,indent=2),encoding='utf-8')
print(destination)
