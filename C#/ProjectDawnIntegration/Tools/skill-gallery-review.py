"""Verify real Unity gallery captures and optionally encode per-skill MP4s.

Usage: python Tools/skill-gallery-review.py <output>/skill-gallery --encode [--ffmpeg path]
This validates capture completeness and ordinary command acceptance, NOT accuracy to DNF.
"""
import argparse
import html
import json
import shutil
import subprocess
from pathlib import Path


def ffmpeg_path(explicit):
    if explicit:
        return explicit
    found = shutil.which('ffmpeg')
    if found:
        return found
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        raise SystemExit('ffmpeg was not found; pass --ffmpeg or omit --encode.')


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('directory', type=Path)
    ap.add_argument('--encode', action='store_true')
    ap.add_argument('--ffmpeg', default='')
    args = ap.parse_args()
    root = args.directory.resolve()
    data = json.loads((root / 'gallery.json').read_text(encoding='utf-8-sig'))
    issues = []
    if not data['completed'] or data['errors']:
        issues.append('Unity reported incomplete execution or runtime errors.')
    if not data['skills']:
        issues.append('No skill records.')
    executable = ffmpeg_path(args.ffmpeg) if args.encode else None
    cards = []
    for skill in data['skills']:
        frames = skill['frames']
        if not skill['complete'] or not skill['actionObserved'] or not skill['commandIssued']:
            issues.append(f"{skill['key']}: skill command/action missing.")
        previous = -1.0
        for frame in frames:
            path = (root / frame['file']).resolve()
            if not path.is_relative_to(root) or not path.is_file() or path.stat().st_size < 1000:
                issues.append(f"{skill['key']}: missing or invalid {frame['file']}")
            if frame['seconds'] <= previous:
                issues.append(f"{skill['key']}: non-increasing frame time.")
            previous = frame['seconds']
        if executable and frames and not issues:
            # Preserve measured simulation-time spacing instead of pretending all capture intervals are equal.
            lines = []
            for index, frame in enumerate(frames):
                filename = Path(frame['file']).name
                duration = frames[index + 1]['seconds'] - frame['seconds'] if index + 1 < len(frames) else 1 / data['requestedFps']
                lines += [f"file '{filename}'", f'duration {duration:.6f}']
            lines += [f"file '{Path(frames[-1]['file']).name}'"]
            concat = root / skill['key'] / 'frames.ffconcat'
            concat.write_text('\n'.join(lines) + '\n', encoding='utf-8')
            subprocess.run([executable, '-hide_banner', '-loglevel', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', concat.as_posix(),
                            '-vf', 'fps=30,pad=ceil(iw/2)*2:ceil(ih/2)*2', '-c:v', 'libx264', '-crf', '20', '-pix_fmt', 'yuv420p',
                            '-movflags', '+faststart', (root / (skill['key'] + '.mp4')).as_posix()], check=True)
        if not frames:
            continue
        indices = sorted(set(round((len(frames) - 1) * fraction) for fraction in (0, .16, .32, .48, .64, .8, 1)))
        thumbs = ''.join(f'<figure><a href="{html.escape(frames[i]["file"])}"><img loading="lazy" src="{html.escape(frames[i]["file"])}"></a><figcaption>{frames[i]["seconds"]:.2f}s / {html.escape(frames[i]["phase"])}</figcaption></figure>' for i in indices)
        video = f'<video controls preload="metadata" src="{skill["key"]}.mp4"></video>' if (root / (skill['key'] + '.mp4')).exists() else ''
        cards.append(f'<section id="{skill["key"]}"><h2>{html.escape(skill["name"])} <small>{skill["key"]}</small></h2><p>{len(frames)} rendered frames · {skill["observedDuration"]:.2f}s · command observed: {skill["actionObserved"]}</p>{video}<div class="frames">{thumbs}</div></section>')
    markup = '''<!doctype html><html lang="ko"><meta charset="utf-8"><title>버서커 스킬 실제 렌더 검토</title>
<style>body{background:#11141b;color:#e9edf4;font:16px system-ui;margin:32px;max-width:1500px}h1{color:#ffc28c}section{margin:36px 0;padding:20px;background:#1e2430;border-radius:12px}.frames{display:flex;overflow:auto;gap:12px}figure{margin:0;min-width:240px}img{width:240px}video{width:min(100%,900px)}small,figcaption{color:#a6bacd}a{color:#9ad7ff}</style>
<h1>버서커 스킬 실제 렌더 검토</h1><p>Unity의 실제 전투 입력으로 각각 시전한 화면입니다. 스킬 사이에는 상태를 초기화합니다. 고정 표적·오프라인 검토 전용이며 원작 일치나 온라인 판정 검증을 뜻하지 않습니다.</p>'''
    markup += f'<p>전체 {len(data["skills"])}종 · 컷신 {"표시" if data["cutscenesEnabled"] else "숨김 (모션 확인용)"} · <a href="gallery.json">프레임/입력 기록</a></p>'
    markup += ''.join(cards) + '</html>'
    (root / 'index.html').write_text(markup, encoding='utf-8')
    result = {'passed': not issues, 'skills': len(data['skills']), 'frames': sum(len(x['frames']) for x in data['skills']), 'issues': issues,
              'scope': 'Capture completeness and command acceptance; manual reference comparison still required.'}
    (root / 'verification.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))
    raise SystemExit(0 if not issues else 1)


if __name__ == '__main__':
    main()
