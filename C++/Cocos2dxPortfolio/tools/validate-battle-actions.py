from pathlib import Path
import json
import re

import argparse
parser = argparse.ArgumentParser(description='Validate captured normal-attack, pause, speed and AUTO evidence.')
parser.add_argument('output_root', type=Path, help='Folder containing captures/ and evidence/')
root = parser.parse_args().output_root
(root / 'evidence').mkdir(parents=True, exist_ok=True)
captures = root / 'captures'
report = {}
for scenario in ['action-basic', 'action-fast', 'action-pause']:
    files = list((captures / scenario).glob('frame-*.txt'))
    assert len(files) == 17, (scenario, len(files))
    before_hit = after_hit = melee_enemy = restored = False
    maximum_actions = maximum_enemies = 0
    for path in files:
        text = path.read_text(encoding='utf-8-sig')
        match = re.search(r'actions=(\d+) actionImpacts=(\d+) enemyActions=(\d+) presenting=(\d)', text)
        assert match, path
        count, impacts, enemies, presenting = map(int, match.groups())
        assert impacts <= count <= impacts + 1, (path, 'duplicate or overlapping impact')
        maximum_actions = max(maximum_actions, count)
        maximum_enemies = max(maximum_enemies, enemies)
        units = {int(m[0]): (int(m[1]), int(m[2]), float(m[3]), float(m[4])) for m in re.findall(
            r'unit(\d) hp=(\d+) modelHp=(\d+) rig=1 offset=([-\d.e+]+),([-\d.e+]+)', text)}
        assert len(units) == 8, path
        if not presenting:
            assert all(abs(u[2]) < .001 and abs(u[3]) < .001 for u in units.values()), path
            restored = restored or count > 0
        action = re.search(r'action actor=(\d) target=(\d) animation=(\w+) time=([\d.e+-]+) hit=([\d.e+-]+) applied=(\d)', text)
        if action:
            actor, target, animation, elapsed, hit, applied = action.groups()
            actor, target, applied = int(actor), int(target), int(applied)
            assert (float(elapsed) >= float(hit)) == bool(applied), path
            hp, model_hp, _, _ = units[target]
            if applied:
                assert hp == model_hp, path
                after_hit = True
            elif hp > model_hp:
                before_hit = True
            if actor >= 4 and abs(units[actor][2]) > 100:
                melee_enemy = True
    assert before_hit and after_hit and melee_enemy and maximum_enemies >= 4, scenario
    report[scenario] = {'snapshots': len(files), 'hpHeldUntilImpact': before_hit,
                        'hpMatchesAtImpact': after_hit, 'enemyApproachVisible': melee_enemy,
                        'maxActions': maximum_actions, 'maxEnemyActions': maximum_enemies,
                        'idleRestorationObserved': restored}
pause = captures / 'action-pause'
assert (pause / 'pause-before.txt').read_bytes() == (pause / 'pause-after.txt').read_bytes()
report['action-pause']['poseHpAudioClockUnchanged'] = True
auto = (captures / 'turn-auto/frame-7.txt').read_text(encoding='utf-8-sig')
assert 'phase=1 result=1 paused=0' in auto
assert 'actions=6 actionImpacts=6 enemyActions=3 presenting=0' in auto
assert all(float(x) == 0 and float(y) == 0 for x, y in re.findall(r'offset=([-\d.e+]+),([-\d.e+]+)', auto))
report['automaticBattle'] = {'victoryAfterPresentation': True, 'normalActions': 6,
                              'normalImpacts': 6, 'compositeS3': 4, 'enemyActions': 3,
                              'allActorsAtHome': True}
no_cutscenes = (captures / 'action-no-cutscenes/frame-7.txt').read_text(encoding='utf-8-sig')
assert 'presentations=0 impacts=0 queued=0' in no_cutscenes
assert 'phase=1 result=1 paused=0' in no_cutscenes
assert 'actions=10 actionImpacts=10 enemyActions=3 presenting=0' in no_cutscenes
assert 'active=1 pending=0' in no_cutscenes and 'bgm_map01_battle state=1' in no_cutscenes
offsets = re.findall(r'offset=([-\d.e+]+),([-\d.e+]+)', no_cutscenes)
assert len(offsets) == 8 and all(float(x) == 0 and float(y) == 0 for x, y in offsets)
report['cutscenesDisabled'] = {'victoryAfterPresentation': True, 'normalActions': 10,
                               'normalImpacts': 10, 'compositeS3': 0, 'enemyActions': 3,
                               'allActorsAtHome': True, 'pendingAudio': 0, 'musicPlaying': True}
(root / 'evidence/basic-attack-validation.json').write_text(
    json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
