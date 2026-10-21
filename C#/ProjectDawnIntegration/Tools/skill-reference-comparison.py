"""Build a portable local video comparison from supplied reference and actual Unity captures."""
import argparse
import html
import json
import shutil
from pathlib import Path

ap = argparse.ArgumentParser()
ap.add_argument('gallery', type=Path)
args = ap.parse_args()
root = Path(__file__).resolve().parent.parent
review = root / 'Artifacts/skill-review-20261018'
references = json.loads((review / 'reference/reference-index.json').read_text(encoding='utf-8'))
gallery = json.loads((args.gallery / 'gallery.json').read_text(encoding='utf-8-sig'))
destination = review / 'comparison'
destination.mkdir(exist_ok=True)
(destination / 'reference').mkdir(exist_ok=True)
(destination / 'developed').mkdir(exist_ok=True)
mapping = dict(basic=1, frenzy=1, berserk=5, scratch=7, ragingfury=8, cruel=9, rage=10,
               bloodyrave=11, bloodsword=12, outrage=13, awaken1=14, bloodsnatch=15,
               brutal=16, bloodboom=17, fatalblood=18, awaken2=19, rampant=20, awaken3=22)
notes = {
    'basic': '원작 쪽은 프렌지 평타 영상입니다. 일반 평타와 동일 동작이라는 의미는 아닙니다.',
    'frenzy': '원작의 느린 재생 영상03 대신 정상 시연01과 비교합니다.',
    'bloodsword': '원작 영상에는 적중·허공 격발 두 시연이 들어 있습니다. 수정 빌드는 사용자 요청으로 검은 효과를 붉은 혈기로 바꿨습니다.',
    'bloodyrave': '사용자 지정: 검은 효과를 붉은 혈기로 변경하고 소용돌이를 기존 데모의 가로·세로 각각 3배로 확대했습니다. 원작과 의도적으로 다른 연출입니다.',
    'awaken1': '혈검 소환 → 자유 행동 가능한 유지 → 재입력 피니시. 영상 대기 시간을 시전 시간으로 복사하지 않았습니다.',
    'awaken3': '파티원에게 보이는 전투 효과를 확인하기 위해 양쪽 모두 컷신을 숨긴 영상입니다.',
}
rows = []
records = []
for skill in gallery['skills']:
    key = skill['key']
    source = args.gallery / f'{key}.mp4'
    # The gallery encoder keeps its own output location explicit.
    if not source.exists():
        matches = list(args.gallery.rglob(f'{key}.mp4'))
        if len(matches) != 1:
            raise SystemExit(f'Missing actual capture movie: {key}')
        source = matches[0]
    shutil.copy2(source, destination / 'developed' / f'{key}.mp4')
    reference = next((v for v in references if v['index'] == mapping.get(key)), None)
    if reference:
        name = f"video-{reference['index']:02d}.mp4"
        shutil.copy2(reference['path'], destination / 'reference' / name)
        original = f'<video controls loop muted playsinline preload="metadata" src="reference/{name}"></video>'
    else:
        original = '<p class="missing">저장 페이지에 해당 스킬의 독립 시연 영상이 없습니다.</p>'
    note = notes.get(key, '동작 순서·몸 자세·타격 단계·잔상을 비교합니다. 공격 속도와 피해 수치는 데모 기준입니다.')
    rows.append(f'<section id="{key}"><h2>{html.escape(skill["name"])} <small>{key}</small></h2>'
                f'<p>{html.escape(note)}</p><div class="pair"><figure><figcaption>제공된 원작 시연</figcaption>{original}</figure>'
                f'<figure><figcaption>수정 빌드 실제 렌더링 · 무음</figcaption><video controls loop muted playsinline preload="metadata" src="developed/{key}.mp4"></video></figure></div>'
                '<button onclick="playPair(this)">이 스킬 두 영상 처음부터 재생</button></section>')
    records.append(dict(skill=key, reference=reference['index'] if reference else None,
                        observedFrames=len(skill['frames']), commandAccepted=skill['complete']))
navigation = ' '.join(f'<a href="#{s["key"]}">{html.escape(s["name"])}</a>' for s in gallery['skills'])
page = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>버서커 스킬 원작 대조</title><style>
body{margin:0;background:#101117;color:#eee;font:16px/1.7 system-ui,"Malgun Gothic",sans-serif}main{max-width:1420px;margin:auto;padding:30px}
h1{margin:0}h2{color:#ffc7b9}small{font-size:14px;color:#aaa}nav{display:flex;flex-wrap:wrap;gap:8px;margin:24px 0}a,button{color:#ffd7ca;background:#28232a;padding:7px 12px;border:1px solid #65424b;border-radius:6px;text-decoration:none}
section{padding:20px 0 35px;border-top:1px solid #38313b;scroll-margin:20px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0;background:#191a22;padding:10px}figcaption{color:#bbb;margin-bottom:10px}video{width:100%;height:400px;object-fit:contain;background:#07080b}.missing{min-height:300px;display:grid;place-items:center;color:#aaa}button{cursor:pointer;margin-top:12px}.notice{max-width:1100px;color:#c3bec9}@media(max-width:800px){.pair{grid-template-columns:1fr}video{height:300px}}
</style><main><h1>버서커 스킬 · 원작 영상과 수정 빌드</h1>
<p class="notice">사용자가 제공한 저장 HTML의 영상과 실제 Unity 실행 화면을 나란히 배치했습니다. 왼쪽 원작에는 대기·공격 속도·편집 차이가 있으므로 동시 재생이 프레임 단위 동기화는 아닙니다. 오른쪽은 실제 렌더링 프레임을 시간 기록대로 인코딩한 무음 영상입니다. 원본 ANI·판정 규칙을 복원했다는 뜻은 아닙니다.</p>
<nav>''' + navigation + '</nav>' + ''.join(rows) + '''</main><script>
function playPair(button){document.querySelectorAll('video').forEach(v=>v.pause());button.closest('section').querySelectorAll('video').forEach(v=>{v.currentTime=0;v.play()})}
</script></html>'''
(destination / 'index.html').write_text(page, encoding='utf-8')
(destination / 'comparison.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
print(destination / 'index.html')
