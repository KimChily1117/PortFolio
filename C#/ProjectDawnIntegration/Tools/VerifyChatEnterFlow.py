"""Check IMGUI-generated chat received over AWS; not a physical keyboard/IME test."""
import json
from pathlib import Path
import sys

root=Path(sys.argv[1]).resolve()
meta=json.loads((root/'processes.json').read_text(encoding='utf-8-sig'))
n=meta['partySize']
logs=[(root/f'p{i}/player.log').read_text(encoding='utf-8-sig') for i in range(1,n+1)]
flows=[json.loads((root/f'p{i}/flow-result.json').read_text(encoding='utf-8-sig')) for i in range(1,n+1)]
checks={
 'town_and_raid_opened_by_enter':all(s.count('[DAWN_FLOW] ChatInputOpened')==2 for s in logs),
 'four_gui_enter_events_each':all(s.count('[DAWN_FLOW] ChatGuiEnter ')==4 for s in logs),
 'two_submissions_each':all(s.count('[DAWN_FLOW] ChatSubmitted ')==2 for s in logs),
 'town_received_full_korean_text':all('ChatGuiTextMatch ChatTown True' in s for s in logs),
 'raid_received_full_korean_text_from_all':all(s.count('ChatGuiTextMatch ChatDungeon True')==n for s in logs),
 'no_truncated_or_changed_text':all('ChatGuiTextMatch ChatTown False' not in s and 'ChatGuiTextMatch ChatDungeon False' not in s for s in logs),
 'both_bubbles_rendered':all('ChatBubbleShown ChatTown' in s and 'ChatBubbleShown ChatDungeon' in s for s in logs),
 'all_returned_without_unity_error':all(f['completed'] and f['errors']==0 and f['stage']=='Town' for f in flows),
}
report=dict(passed=all(checks.values()),players=n,checks=checks,
 limitation='Unity IMGUI key/character events through focused TextField; real AWS receive. OS keyboard and Windows IME composition not exercised. Computer Use capture timed out twice.')
(root/'chat-enter-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
sys.exit(0 if report['passed'] else 1)
