from pathlib import Path
import re,json
src=Path('Tools/ImportDawnFlow.py').read_text(encoding='utf-8-sig')
exec(src[:src.index("for name,new in [('Title(Login)'")])
cp(Path('E:/task/C#/Project_Dawn/Assets/Resources/Prefabs/UI/Scene/BossHpBar.prefab'),out/'BossHpBar.prefab')
for number,name in [(2,'BossHealthFill'),(7,'BossDamageFill')]:
 cp(Path('E:/task/C#/Project_Dawn/Assets/Resources/Arts/UI/HpBar/sprite_interface_newstyle_windows_monster_hp/monster_hp.img')/(str(number)+'.png'),out/(name+'.png'))
(r/'Tools/boss-hud-asset-provenance.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
print('Imported original boss HUD and dependencies:',len(records))
