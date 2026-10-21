"""Verify real Unity flow chat audits; never claim physical keyboard/IME testing."""
import json, re, sys
from pathlib import Path

root = Path(sys.argv[1]).resolve()
players = []
for i in range(1, 5):
    folder = root / f'p{i}'
    rows = (folder / 'flow-audit.txt').read_text(encoding='utf-8-sig').splitlines()
    contexts = [re.search(r'ChatContext (\w+) room=(\d+)', r).groups()
                for r in rows if 'ChatContext ' in r]
    received = [re.search(r'ChatReceived (\w+) sender=(\d+) seq=(\d+)', r).groups()
                for r in rows if 'ChatReceived ' in r]
    result = json.loads((folder / 'flow-result.json').read_text(encoding='utf-8-sig'))
    network = json.loads((folder / 'network-result.json').read_text(encoding='utf-8-sig'))
    object_ids={int(re.search(r'Entered (?:Town|RaidLoading) object=(\d+)',r).group(1))
                for r in rows if re.search(r'Entered (?:Town|RaidLoading) object=(\d+)',r)}
    assert len(object_ids)==1, 'Expected stable server object identity across town/raid'
    players.append(dict(slot=network['slot'], objectId=next(iter(object_ids)), contexts=contexts,
        town=[x for x in received if x[0] == 'ChatTown'],
        dungeon=[x for x in received if x[0] == 'ChatDungeon'],
        completed=result['completed'], errors=result['errors'],
        captures=all((folder / name).is_file() for name in ('chat-town.png', 'chat-dungeon.png'))))

checks = {
    'four_distinct_players': len({p['slot'] for p in players}) == 4,
    'two_town_channels_exercised': len({p['contexts'][0][1] for p in players}) == 2,
    'same_dungeon_context': len({next(c[1] for c in p['contexts'] if c[0] == 'ChatDungeon') for p in players}) == 1,
    'all_four_dungeon_messages_once_in_order': all(len(p['dungeon']) == 4 and
        {int(x[1]) for x in p['dungeon']} == {p['objectId'] for p in players} and
        [int(x[2]) for x in p['dungeon']] == [1, 2, 3, 4] for p in players),
    'same_dungeon_audit': all(p['dungeon'] == players[0]['dungeon'] for p in players),
    'town_recipients_only_own_channel': all(
        {int(x[1]) for x in p['town']} == {q['objectId'] for q in players if q['contexts'][0] == p['contexts'][0]}
        and len(p['town']) == len({int(x[1]) for x in p['town']}) for p in players),
    'all_returned_to_town_context': all(p['completed'] and p['contexts'][-1][0] == 'ChatTown' for p in players),
    'all_eight_captures_exist': all(p['captures'] for p in players),
    'no_unity_errors': all(p['errors'] == 0 for p in players),
}
report = dict(passed=all(checks.values()), checks=checks, players=players,
    limits=['Automated sends, not physical keyboard/IME input.',
            'One PC running four Unity processes against public AWS TLS.',
            'Different internal 3x3 rooms were not specifically asserted by this chat test.',
            'Combat regression is evaluated separately by Verify-Flow4.py.'])
(root / 'chat-verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k != 'players'}, ensure_ascii=False, indent=2))
sys.exit(0 if report['passed'] else 1)
