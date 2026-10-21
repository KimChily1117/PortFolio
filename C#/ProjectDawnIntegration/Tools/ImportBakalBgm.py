"""Import the user's local video audio. No network access; retain provenance and originals."""
import argparse, hashlib, json, shutil, subprocess
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('source', type=Path)
p.add_argument('--ffmpeg', required=True)
p.add_argument('--split', type=float, default=134)
a = p.parse_args()
root = Path(__file__).resolve().parents[1]
out = root/'Artifacts/bakal-supplied-bgm-20261022'
out.mkdir(parents=True, exist_ok=True)
sound = root/'UnityClient/Assets/Resources/Sound'
def run(args):
    subprocess.run([a.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', *map(str,args)], check=True)
run(['-i',a.source,'-map','0:a:0','-vn','-c:a','copy',out/'bakal-theme-full.m4a'])
files = []
for phase, timing in [(1,['-t',a.split]), (2,['-ss',a.split])]:
    name = f'bgm_bakal_dragon_king_{phase}phase.ogg'
    original = out/('previous-'+name)
    if not original.exists(): shutil.copy2(sound/name, original)
    run(['-i',a.source,*timing,'-map','0:a:0','-vn','-c:a','libvorbis','-q:a','6',out/name])
    shutil.copy2(out/name, sound/name)
    files.append(dict(phase=phase,file=name,sha256=hashlib.sha256((out/name).read_bytes()).hexdigest()))
(out/'provenance.json').write_text(json.dumps(dict(source=str(a.source.resolve()),
    sourceSha256=hashlib.sha256(a.source.read_bytes()).hexdigest(),splitSeconds=a.split,
    policy='User requested 02:14 boundary. Phase 1: [0,134); phase 2: [134,end), including ending section. No crossfade or time stretching.',
    fullAudio='bakal-theme-full.m4a',outputs=files),ensure_ascii=False,indent=2),encoding='utf-8')
print(out)
