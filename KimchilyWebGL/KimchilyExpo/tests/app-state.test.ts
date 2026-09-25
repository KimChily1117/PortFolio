import test from 'node:test';
import assert from 'node:assert/strict';
import { appendRecent, parseRecents, visitLabel } from '../src/app-state.ts';
import type { WorldPublication } from '../src/world-client.ts';

const origin = 'https://world.example';
function world(id = 'sample', revision = 'rev-1'): WorldPublication {
  const hash = 'a'.repeat(64), manifestUrl = `${origin}/worlds/${id}/${revision}/world.json`;
  return { worldId: id, revisionId: revision, title: `월드 ${id}`, manifestUrl, manifestSha256: hash,
    launchUrl: `${origin}/player/?manifest=${encodeURIComponent(manifestUrl)}&sha256=${hash}`,
    publishUrl: `${origin}/w/${id}/${revision}`, qrUrl: `${origin}/qr/${id}/${revision}.png` };
}
test('recent history caps at six and replaces a world with its newly opened revision', () => {
  let recent = appendRecent([], world(), origin, {}, 100);
  for (let i = 0; i < 8; i++) recent = appendRecent(recent, world(`world-${i}`), origin, {}, 101 + i);
  recent = appendRecent(recent, world('world-4', 'rev-2'), origin, {}, 200);
  assert.equal(recent.length, 6); assert.equal(recent[0].world.revisionId, 'rev-2');
  assert.equal(recent.filter(item => item.world.worldId === 'world-4').length, 1);
});
test('invalid stored JSON and foreign-server history do not create launchable entries', () => {
  assert.deepEqual(parseRecents('{', origin, {}), []);
  assert.deepEqual(parseRecents('x'.repeat(100001), origin, {}), []);
  const stored = JSON.stringify([{ origin: 'https://other.example', world: world(), visitedAt: 1 }]);
  assert.deepEqual(parseRecents(stored, origin, {}), []);
});
test('tampered URL, hash, timestamp, and duplicate records are filtered on hydration', () => {
  const valid = { origin, world: world(), visitedAt: 100 };
  const values = [
    { ...valid, world: { ...world(), launchUrl: 'javascript:alert(1)' } },
    { ...valid, world: { ...world(), manifestSha256: 'b'.repeat(64) } },
    { ...valid, visitedAt: '123' }, valid, valid
  ];
  assert.equal(parseRecents(JSON.stringify(values), origin, {}).length, 1);
  assert.throws(() => appendRecent([], { ...world(), worldId: 'elsewhere' }, origin, {}));
});
test('history cannot cross into HTTP when production validation is in force', () => {
  const record = JSON.stringify([{ origin: 'http://192.168.0.4:8788', world: world(), visitedAt: 100 }]);
  assert.deepEqual(parseRecents(record, 'http://192.168.0.4:8788', {}), []);
});
test('recent date labels handle today, yesterday, old dates, and clock adjustments', () => {
  const now = 50 * 86400000;
  assert.equal(visitLabel(now, now), '오늘 방문');
  assert.equal(visitLabel(now - 86400000, now), '어제 방문');
  assert.equal(visitLabel(now - 5 * 86400000, now), '5일 전 방문');
  assert.equal(visitLabel(1, now), '이전에 방문');
  assert.equal(visitLabel(now + 1000, now), '오늘 방문');
});
