'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { parseLaunch, createHost } = require('../../Assets/WebGLTemplates/KimchilyWeb/host.js');
const sha = 'A'.repeat(64);
const page = 'http://localhost:8788/player/';
const manifest = 'http://localhost:8788/worlds/sample-world/rev_1/world.json';
const url = (m = manifest, hash = sha) => page + '?manifest=' + encodeURIComponent(m) + '&sha256=' + hash;
function fixture() {
  const sent = [], states = [];
  const host = createHost(parseLaunch(page), state => states.push(state));
  const instance = { SendMessage(object, method, json) { sent.push({ object, method, value: method === 'Receive' ? JSON.parse(json) : json }); } };
  const event = (type, requestId, extra) => host.receive(Object.assign({ protocolVersion: 1, type, requestId }, extra || {}));
  function ready() { host.start(); host.attach(instance); event('RuntimeReady', sent[0].value.requestId); }
  return { host, sent, states, instance, event, ready };
}
test('bare player opens the bundled demo', () => assert.deepEqual(parseLaunch(page), { worldId: 'demo', revisionId: 'builtin-v1' }));
test('remote link extracts exact world/revision and normalized hash', () => assert.deepEqual(parseLaunch(url()), {
  worldId: 'sample-world', revisionId: 'rev_1', manifestUrl: manifest, manifestSha256: sha.toLowerCase()
}));
test('development LAN HTTP works when the page is on the same server', () => {
  const origin = 'http://192.168.1.40:8788';
  assert.equal(parseLaunch(origin + '/player/?manifest=' + encodeURIComponent(origin + '/worlds/a/b/world.json') + '&sha256=' + sha).worldId, 'a');
});
for (const [name, value] of [
  ['missing checksum', page + '?manifest=' + encodeURIComponent(manifest)],
  ['missing manifest', page + '?sha256=' + sha],
  ['invalid checksum', url(manifest, 'bad')],
  ['duplicate manifest', url() + '&manifest=' + encodeURIComponent(manifest)],
  ['duplicate checksum', url() + '&sha256=' + sha],
  ['cross-origin URL', url('https://example.com/worlds/a/b/world.json')],
  ['relative manifest', url('/worlds/a/b/world.json')],
  ['manifest credentials', url('http://user:password@localhost:8788/worlds/a/b/world.json')],
  ['manifest query', url(manifest + '?secret=1')],
  ['manifest fragment', url(manifest + '#extra')],
  ['escaped world separator', url('http://localhost:8788/worlds/a%2Fb/c/world.json')],
  ['wrong manifest filename', url(manifest.replace('world.json', 'other.json'))],
  ['file-page URL', 'file:///player/index.html']
]) test('rejects ' + name + ' instead of falling back to demo', () => assert.throws(() => parseLaunch(value)));
test('early and repeated RuntimeReady cannot open twice', () => {
  const f = fixture(); f.host.start();
  f.event('RuntimeReady', ''); assert.equal(f.sent.length, 0);
  f.host.attach(f.instance); assert.equal(f.sent[0].value.type, 'Initialize');
  f.event('RuntimeReady', ''); assert.equal(f.sent.length, 1);
  f.event('RuntimeReady', f.sent[0].value.requestId); assert.equal(f.sent[1].value.type, 'OpenWorld');
  f.event('RuntimeReady', f.sent[0].value.requestId); assert.equal(f.sent.length, 2);
  assert.equal(f.sent[1].object, 'KimchilyHostBridge'); assert.equal(f.sent[1].method, 'Receive');
});
test('matching ready hides loading and stale events do not change state', () => {
  const f = fixture(); f.ready();
  f.event('WorldReady', 'old'); assert.equal(f.host.getState().phase, 'loading');
  f.event('WorldReady', f.sent[1].value.requestId); assert.equal(f.host.getState().phase, 'ready');
  assert.equal(f.host.start(), false);
});
test('failed script initialization waits for confirmed cleanup before retry', () => {
  const f = fixture(); f.ready(); const oldOpen = f.sent[1].value.requestId;
  f.event('WorldFailed', oldOpen, { code: 'SCRIPT_INITIALIZATION_FAILED', message: 'Invalid script' });
  assert.equal(f.sent[2].value.type, 'CloseWorld'); assert.equal(f.host.getState().phase, 'closing');
  assert.equal(f.host.start(), false);
  f.event('WorldClosed', f.sent[2].value.requestId); assert.equal(f.host.getState().phase, 'error');
  assert.equal(f.host.getState().canRetry, true); assert.match(f.host.getState().message, /Invalid script/);
  assert.equal(f.host.start(), true); assert.equal(f.sent[3].value.type, 'OpenWorld');
  assert.notEqual(f.sent[3].value.requestId, oldOpen);
});
test('close during load ignores cancellation and late readiness', () => {
  const f = fixture(); f.ready(); const openId = f.sent[1].value.requestId;
  f.host.close(); f.host.close(); assert.equal(f.sent.length, 3);
  f.event('WorldFailed', openId, { code: 'CANCELLED' });
  f.event('WorldReady', openId); assert.equal(f.host.getState().phase, 'closing');
  f.event('WorldClosed', f.sent[2].value.requestId); assert.equal(f.host.getState().phase, 'closed');
});
test('stale close acknowledgement cannot close a new revision session', () => {
  const f = fixture(); f.ready(); f.host.close(); const closedId = f.sent[2].value.requestId;
  f.event('WorldClosed', closedId); f.host.start();
  f.event('WorldClosed', closedId, { worldId: 'demo', revisionId: 'builtin-v1' });
  assert.equal(f.host.getState().phase, 'loading');
});
test('world UI close notification permits restarting', () => {
  const f = fixture(); f.ready();
  f.event('WorldClosed', 'unity-close-123', { worldId: 'demo', revisionId: 'builtin-v1' });
  assert.equal(f.host.getState().phase, 'closed'); assert.equal(f.host.start(), true);
});
test('failed cleanup is fatal instead of opening over a live scene', () => {
  const f = fixture(); f.ready(); f.host.close();
  f.event('WorldFailed', f.sent[2].value.requestId, { code: 'UNLOAD_FAILED', message: 'Unload failed' });
  assert.equal(f.host.getState().phase, 'fatal'); assert.equal(f.host.start(), false);
});
test('malformed/wrong protocol events are ignored', () => {
  const f = fixture(); f.ready();
  assert.equal(f.host.receive('{'), false);
  assert.equal(f.host.receive({ protocolVersion: 2, type: 'WorldReady' }), false);
  assert.equal(f.host.getState().phase, 'loading');
});
test('page interruption clears Unity input via its separate preserved endpoint', () => {
  const f = fixture(); f.host.cancelInput(); assert.equal(f.sent.length, 0);
  f.ready(); f.host.cancelInput(); assert.equal(f.sent[2].method, 'CancelInput');
});
