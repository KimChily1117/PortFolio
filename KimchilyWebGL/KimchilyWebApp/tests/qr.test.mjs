import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
const source = await readFile(new URL('../qr.js', import.meta.url), 'utf8');
const { parseQrPayload, resolveQrPayload } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
const origin = 'https://192.168.0.4:8789', oldOrigin = 'http://192.168.0.4:8788';
const hash = 'a'.repeat(64), manifest = origin + '/worlds/sample/rev-1/world.json';
const player = (address = manifest, digest = hash, base = origin) => base + '/player/?manifest=' + encodeURIComponent(address) + '&sha256=' + digest;
test('player QR validates IDs and pinned hash', () => {
  const result = parseQrPayload(player(), origin);
  assert.equal(result.kind, 'player'); assert.equal(result.worldId, 'sample'); assert.equal(result.revisionId, 'rev-1'); assert.equal(result.manifestSha256, hash);
});
test('landing QR accepts absolute and root-relative paths', () => {
  assert.equal(parseQrPayload('/w/sample/rev-1', origin).url, origin + '/w/sample/rev-1');
  assert.equal(parseQrPayload(origin + '/w/sample/rev-1', origin).kind, 'landing');
});
test('legacy QR still pins manifest to trusted origin', () => {
  assert.equal(parseQrPayload('kimchily://world?manifest=' + encodeURIComponent(manifest) + '&sha256=' + hash, origin).kind, 'legacy');
});
test('old HTTP QR requires an explicit catalog alias', () => {
  const raw = player(oldOrigin + '/worlds/sample/rev-1/world.json', hash, oldOrigin);
  assert.throws(() => parseQrPayload(raw, origin));
  assert.equal(parseQrPayload(raw, origin, [oldOrigin]).worldId, 'sample');
});
for (const [name, raw] of [
  ['external player', player('https://evil.test/worlds/sample/rev-1/world.json', hash, 'https://evil.test')],
  ['external manifest', player('https://evil.test/worlds/sample/rev-1/world.json')],
  ['javascript scheme', 'javascript:alert(1)'],
  ['data scheme', 'data:text/html,hello'],
  ['protocol-relative', '//192.168.0.4:8789/w/sample/rev-1'],
  ['credentials', origin.replace('://', '://user:password@') + '/w/sample/rev-1'],
  ['manifest credentials', player(manifest.replace('://', '://user:password@'))],
  ['duplicate hash', player() + '&sha256=' + hash],
  ['duplicate manifest', player() + '&manifest=' + encodeURIComponent(manifest)],
  ['unexpected player query', player() + '&redirect=https://evil.test'],
  ['unexpected landing query', origin + '/w/sample/rev-1?x=1'],
  ['empty landing query', origin + '/w/sample/rev-1?'],
  ['fragment', player() + '#extra'],
  ['manifest query', player(manifest + '?x=1')],
  ['empty manifest query', player(manifest + '?')],
  ['missing hash', origin + '/player/?manifest=' + encodeURIComponent(manifest)],
  ['bad hash', player(manifest, 'xyz')],
  ['bad escape', origin + '/w/sample/rev%ZZ'],
  ['encoded path separator', origin + '/w/sample%2Fother/rev-1'],
  ['backslash normalization', origin + '/w\\sample/rev-1'],
  ['control character', origin + '/w/sam\nple/rev-1'],
  ['unsupported legacy target', 'kimchily://settings?manifest=' + encodeURIComponent(manifest) + '&sha256=' + hash],
  ['legacy credentials', 'kimchily://u:p@world?manifest=' + encodeURIComponent(manifest) + '&sha256=' + hash],
  ['oversized payload', 'x'.repeat(8193)]
]) test('rejects ' + name, () => assert.throws(() => parseQrPayload(raw, origin)));
function answer(extra = {}) { return { worldId: 'sample', revisionId: 'rev-1', title: 'Sample', manifestUrl: manifest, manifestSha256: hash, launchUrl: player(), ...extra }; }
test('invalid QR cannot trigger even a resolver fetch', async () => {
  let called = false;
  await assert.rejects(resolveQrPayload('javascript:alert(1)', { origin, fetch: () => { called = true; } }));
  assert.equal(called, false);
});
test('resolver carries AbortSignal and disables redirects', async () => {
  const controller = new AbortController(); let options;
  const result = await resolveQrPayload(origin + '/w/sample/rev-1', { origin, signal: controller.signal,
    fetch: async (url, init) => { assert.equal(new URL(url).pathname, '/api/resolve'); options = init; return { ok: true, json: async () => answer() }; }
  });
  assert.equal(options.signal, controller.signal); assert.equal(options.redirect, 'error'); assert.equal(result.launchUrl, player());
});
test('old HTTP QR resolves only to a canonical current-origin launch', async () => {
  const raw = player(oldOrigin + '/worlds/sample/rev-1/world.json', hash, oldOrigin);
  const result = await resolveQrPayload(raw, { origin, allowedOrigins: [oldOrigin], fetch: async () => ({ ok: true, json: async () => answer() }) });
  assert.equal(result.launchUrl, player());
});
test('server Android/missing revision rejection is propagated without navigation', async () => {
  await assert.rejects(resolveQrPayload(player(), { origin, fetch: async () => ({ ok: false, json: async () => ({ message: 'WebGL required' }) }) }), /WebGL required/);
});
for (const [name, response] of [
  ['cross-origin launch', answer({ launchUrl: player(manifest, hash, 'https://evil.test') })],
  ['different revision', answer({ launchUrl: player(manifest.replace('rev-1', 'rev-2')) })],
  ['different pinned hash', answer({ manifestSha256: 'b'.repeat(64), launchUrl: player(manifest, 'b'.repeat(64)) })],
  ['mismatched manifest field', answer({ manifestUrl: 'https://evil.test/worlds/sample/rev-1/world.json' })],
  ['legacy launch instead of HTTPS', answer({ launchUrl: 'kimchily://world?manifest=' + encodeURIComponent(manifest) + '&sha256=' + hash })]
]) test('resolver rejects ' + name, async () => {
  await assert.rejects(resolveQrPayload(player(), { origin, fetch: async () => ({ ok: true, json: async () => response }) }));
});
