import test from 'node:test';
import assert from 'node:assert/strict';
import { normalizeWorld, normalizeCatalog, readRecents, rememberWorld, clearRecents, prepareEntry, RECENT_KEY } from '../state.js';

const origin = 'https://world.example';
const sha = 'a'.repeat(64);
function world(id = 'sample', revision = 'rev-1') {
  const manifest = `${origin}/worlds/${id}/${revision}/world.json`;
  return { worldId: id, revisionId: revision, manifestSha256: sha, title: `월드 ${id}`, launchUrl: `${origin}/player/?manifest=${encodeURIComponent(manifest)}&sha256=${sha}` };
}
function storage() {
  const values = new Map();
  return { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) };
}

test('catalog accepts matching same-origin worlds and removes untrusted or contradictory records', () => {
  const input = { worlds: [world(), { ...world('bad'), launchUrl: 'https://other.example/player/' }, { ...world('mismatch'), worldId: 'elsewhere' }, world()], linkOrigins: [origin, 'http://192.168.0.4:8788', 'javascript:alert(1)', 'https://other.example/a'] };
  const result = normalizeCatalog(input, origin);
  assert.equal(result.worlds.length, 1);
  assert.deepEqual(result.linkOrigins, [origin, 'http://192.168.0.4:8788']);
  assert.throws(() => normalizeCatalog({}, origin));
});
test('a landing URL or forged hash cannot become a ready-to-navigate world', () => {
  assert.equal(normalizeWorld({ ...world(), launchUrl: `${origin}/w/sample/rev-1` }, origin), null);
  assert.equal(normalizeWorld({ ...world(), manifestSha256: 'b'.repeat(64) }, origin), null);
});
test('recent visits are bounded and a new revision moves its world to the front', () => {
  const store = storage();
  for (let i = 0; i < 9; i++) rememberWorld(store, world(`world-${i}`), origin, 100 + i);
  assert.equal(readRecents(store, origin).length, 6);
  rememberWorld(store, world('world-5', 'rev-2'), origin, 999);
  const recent = readRecents(store, origin);
  assert.equal(recent[0].revisionId, 'rev-2');
  assert.equal(recent.filter(item => item.worldId === 'world-5').length, 1);
  assert.equal(recent[0].visitedAt, 999);
});
test('corrupt storage, oversized storage, foreign origins, and blocked storage do not break the home', () => {
  const store = storage(); store.setItem(RECENT_KEY, '{'); assert.deepEqual(readRecents(store, origin), []);
  store.setItem(RECENT_KEY, 'x'.repeat(66000)); assert.deepEqual(readRecents(store, origin), []);
  store.setItem(RECENT_KEY, JSON.stringify([world()])); assert.deepEqual(readRecents(store, 'https://other.example'), []);
  const blocked = { getItem() { throw Error(); }, setItem() { throw Error(); }, removeItem() { throw Error(); } };
  assert.deepEqual(readRecents(blocked, origin), []);
  assert.equal(rememberWorld(blocked, world(), origin).length, 1);
  assert.doesNotThrow(() => clearRecents(blocked));
});
test('entry prepares return-home marker and history only after URL and identity validation', () => {
  const session = storage(), recent = storage();
  assert.throws(() => prepareEntry({ ...world(), launchUrl: 'javascript:alert(1)' }, origin, session, recent));
  assert.equal(session.getItem('kimchily:returnHome'), null);
  assert.equal(prepareEntry(world(), origin, session, recent, 123), world().launchUrl);
  assert.equal(session.getItem('kimchily:returnHome'), '1');
  assert.equal(readRecents(recent, origin)[0].visitedAt, 123);
});
test('clearing recents affects only its dedicated key', () => {
  const store = storage(); store.setItem('unrelated', 'keep'); rememberWorld(store, world(), origin);
  clearRecents(store); assert.deepEqual(readRecents(store, origin), []); assert.equal(store.getItem('unrelated'), 'keep');
});
