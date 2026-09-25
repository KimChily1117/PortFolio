import assert from 'node:assert/strict';
import test from 'node:test';
import {
  fetchWorldCatalog, normalizeServerOrigin, parseWorldQr, resolveWorldQr,
  validateWorldPublication, WorldClientError,
} from '../src/world-client.ts';
import type { WorldPublication } from '../src/world-client.ts';

const ORIGIN = 'https://192.168.0.4:8789';
const OLD_ORIGIN = 'http://192.168.0.4:8788';
const HASH = 'a'.repeat(64);

function publication(origin = ORIGIN, world = 'garden', revision = 'webgl-1'): WorldPublication {
  const manifestUrl = `${origin}/worlds/${world}/${revision}/world.json`;
  return {
    worldId: world, revisionId: revision, title: world,
    manifestUrl, manifestSha256: HASH,
    launchUrl: `${origin}/player/?manifest=${encodeURIComponent(manifestUrl)}&sha256=${HASH}`,
    publishUrl: `${origin}/w/${world}/${revision}`,
    qrUrl: `${origin}/qr/${world}/${revision}.png`,
  };
}

function jsonFetch(payload: unknown, status = 200, onRequest?: (url: string, init?: RequestInit) => void): typeof fetch {
  return (async (input, init) => {
    onRequest?.(String(input), init);
    return new Response(JSON.stringify(payload), { status, headers: { 'Content-Type': 'application/json' } });
  }) as typeof fetch;
}

function hasCode(code: string): (error: unknown) => boolean {
  return error => error instanceof WorldClientError && error.code === code && /[가-힣]/.test(error.message);
}

test('server origins normalize HTTPS and reject credentials, paths, queries and fragments', () => {
  assert.equal(normalizeServerOrigin('  https://Example.COM:443/  '), 'https://example.com');
  assert.equal(normalizeServerOrigin(ORIGIN), ORIGIN);
  for (const value of ['192.168.0.4:8788', 'https://user:pass@example.com', 'https://example.com/worlds',
    'https://example.com/a/../', 'https://example.com?', 'https://example.com#', 'https://example.com\\path',
    'https://example.com/ /', 'file:///tmp/world', 'javascript:alert(1)']) {
    assert.throws(() => normalizeServerOrigin(value), WorldClientError, value);
  }
});

test('development HTTP is limited to private LAN or localhost and is disabled by default', () => {
  for (const value of [OLD_ORIGIN, 'http://10.2.3.4', 'http://172.16.1.2', 'http://172.31.255.1',
    'http://127.0.0.1:8788', 'http://localhost:8788', 'http://[::1]:8788', 'http://[fd12::1]:8788']) {
    assert.throws(() => normalizeServerOrigin(value), hasCode('HTTP_NOT_ALLOWED'));
    assert.equal(normalizeServerOrigin(value, { allowLanHttp: true }), new URL(value).origin);
  }
  for (const value of ['http://8.8.8.8', 'http://172.15.0.1', 'http://172.32.0.1', 'http://example.com',
    'http://192.168.0.4.attacker.example', 'http://localhost.attacker.example', 'http://[2001:4860::1]']) {
    assert.throws(() => normalizeServerOrigin(value, { allowLanHttp: true }), hasCode('HTTP_NOT_ALLOWED'));
  }
});

test('QR parsing accepts player, landing, legacy and relative publisher links', () => {
  const world = publication();
  const query = new URL(world.launchUrl).search;
  for (const [link, kind] of [[world.launchUrl, 'player'], [world.publishUrl, 'landing'],
    ['kimchily://world' + query, 'legacy'], ['/w/garden/webgl-1', 'landing'], ['/player/' + query, 'player']]) {
    const parsed = parseWorldQr(link, ORIGIN);
    assert.equal(parsed.kind, kind);
    assert.equal(parsed.worldId, world.worldId);
    assert.equal(parsed.revisionId, world.revisionId);
    if (kind !== 'landing') assert.equal(parsed.manifestSha256, HASH);
  }
});

test('QR parsing rejects ambiguous, external, malformed and normalized traversal inputs', () => {
  const world = publication();
  const invalid = [publication('https://external.example').launchUrl,
    `${ORIGIN}/player/?manifest=${encodeURIComponent(world.manifestUrl)}`,
    `${world.launchUrl}&sha256=${HASH}`, `${world.launchUrl}&extra=x`,
    `${ORIGIN}/player/?sha256=${HASH}&sha256=${HASH}`, world.launchUrl.replace(HASH, 'bad'),
    `${world.launchUrl}#`, `${world.publishUrl}?`, `${ORIGIN}/other?manifest=x`,
    `${ORIGIN}/w/a/../garden/webgl-1`, `${ORIGIN}/w/%2e%2e/garden/webgl-1`,
    `${ORIGIN}/player/?manifest=%ZZ&sha256=${HASH}`, '//external.example/w/garden/webgl-1',
    `kimchily://user:pass@world${new URL(world.launchUrl).search}`,
    `kimchily://world:80${new URL(world.launchUrl).search}`];
  for (const value of invalid) assert.throws(() => parseWorldQr(value, ORIGIN), WorldClientError, value);
});

test('response validation binds all public links, identities and hash to the canonical origin', () => {
  assert.deepEqual(validateWorldPublication(publication(), ORIGIN), publication());
  const variants: Record<string, unknown>[] = [
    { ...publication(), worldId: 'different' },
    { ...publication(), manifestSha256: 'b'.repeat(64) },
    { ...publication(), manifestUrl: publication('https://other.example').manifestUrl },
    { ...publication(), publishUrl: publication('https://other.example').publishUrl },
    { ...publication(), qrUrl: publication('https://other.example').qrUrl },
    { ...publication(), launchUrl: publication('https://other.example').launchUrl },
    { ...publication(), launchUrl: 'kimchily://world' + new URL(publication().launchUrl).search },
    { ...publication(), platform: 'Android' },
  ];
  for (const value of variants) assert.throws(() => validateWorldPublication(value, ORIGIN), WorldClientError);
});

test('catalog caps examination at 100 records and skips malformed and duplicate worlds', async () => {
  const worlds: unknown[] = [null, { bad: true }, publication(), publication(ORIGIN, 'garden', 'webgl-2'),
    publication('https://other.example', 'outside')];
  for (let i = 0; i < 110; i++) worlds.push(publication(ORIGIN, `world-${i}`));
  const result = await fetchWorldCatalog(ORIGIN, { fetch: jsonFetch({ worlds, linkOrigins: [ORIGIN, OLD_ORIGIN,
    'https://user:pass@bad.example', 'file:///bad', 'http://public.example', ORIGIN] }) });
  assert.equal(result.worlds.length, 96);
  assert.equal(result.worlds[0].worldId, 'garden');
  assert.equal(result.worlds.at(-1)?.worldId, 'world-94');
  assert.deepEqual(result.linkOrigins, [ORIGIN, OLD_ORIGIN]);
});

test('catalog requests only its configured API and does not attach credentials', async () => {
  const calls: string[] = [];
  await fetchWorldCatalog(OLD_ORIGIN, { allowLanHttp: true, fetch: jsonFetch({ worlds: [] }, 200, (url, init) => {
    calls.push(url);
    assert.equal(init?.credentials, 'omit');
    assert.equal(init?.redirect, 'error');
    assert.equal(init?.cache, 'no-store');
    assert.ok(init?.signal);
  }) });
  assert.deepEqual(calls, [OLD_ORIGIN + '/api/worlds']);
});

test('untrusted QR input is rejected before any API or QR-origin fetch', async () => {
  let count = 0;
  const fetcher = jsonFetch(publication(), 200, () => count++);
  await assert.rejects(resolveWorldQr(ORIGIN, publication('https://outside.example').launchUrl, { fetch: fetcher }),
    hasCode('UNTRUSTED_WORLD'));
  assert.equal(count, 0);
});

test('configured HTTP QR alias resolves only via HTTPS API and returns canonical HTTPS world', async () => {
  const old = publication(OLD_ORIGIN);
  const calls: string[] = [];
  const result = await resolveWorldQr(ORIGIN, old.launchUrl, {
    allowedOrigins: [OLD_ORIGIN], fetch: jsonFetch(publication(), 200, url => calls.push(url)),
  });
  assert.deepEqual(result, publication());
  assert.equal(calls.length, 1);
  const api = new URL(calls[0]);
  assert.equal(api.origin, ORIGIN);
  assert.equal(api.pathname, '/api/resolve');
  assert.equal(api.searchParams.get('url'), old.launchUrl);
  await assert.rejects(resolveWorldQr(ORIGIN, old.launchUrl, {
    allowedOrigins: [OLD_ORIGIN], fetch: jsonFetch(old),
  }), WorldClientError);
});

test('resolver cannot substitute another valid world or hash', async () => {
  for (const returned of [publication(ORIGIN, 'other'), publication(ORIGIN, 'garden', 'webgl-2')]) {
    await assert.rejects(resolveWorldQr(ORIGIN, publication().launchUrl, { fetch: jsonFetch(returned) }),
      hasCode('INVALID_RESPONSE'));
  }
  const changed = publication();
  changed.manifestSha256 = 'b'.repeat(64);
  changed.launchUrl = changed.launchUrl.replace(HASH, changed.manifestSha256);
  await assert.rejects(resolveWorldQr(ORIGIN, publication().launchUrl, { fetch: jsonFetch(changed) }),
    hasCode('INVALID_RESPONSE'));
});

test('Android errors are translated into plain Korean instead of exposing server English', async () => {
  await assert.rejects(resolveWorldQr(ORIGIN, publication().publishUrl, {
    fetch: jsonFetch({ code: 'WRONG_PLATFORM', message: 'Open Android content using the native app.' }, 400),
  }), error => error instanceof WorldClientError && error.code === 'WRONG_PLATFORM' &&
    error.message.includes('Android 앱용') && !error.message.includes('native app'));
});

test('malformed catalog and JSON fail with user-facing errors', async () => {
  await assert.rejects(fetchWorldCatalog(ORIGIN, { fetch: jsonFetch({ worlds: 'bad' }) }), hasCode('INVALID_RESPONSE'));
  const invalidJson = (async () => new Response('<html>offline</html>')) as typeof fetch;
  await assert.rejects(fetchWorldCatalog(ORIGIN, { fetch: invalidJson }), hasCode('INVALID_RESPONSE'));
});

test('request timeout aborts the owned fetch signal', async () => {
  let aborted = false;
  const hanging = ((_input: unknown, init?: RequestInit) => new Promise<Response>((_resolve, reject) => {
    init?.signal?.addEventListener('abort', () => { aborted = true; reject(new Error('aborted')); }, { once: true });
  })) as typeof fetch;
  await assert.rejects(fetchWorldCatalog(ORIGIN, { fetch: hanging, timeoutMs: 5 }), hasCode('TIMEOUT'));
  assert.equal(aborted, true);
});

test('caller cancellation propagates to fetch and pre-aborted requests never start', async () => {
  const controller = new AbortController();
  let calls = 0;
  const hanging = ((_input: unknown, init?: RequestInit) => new Promise<Response>((_resolve, reject) => {
    calls++;
    init?.signal?.addEventListener('abort', () => reject(new Error('aborted')), { once: true });
    controller.abort();
  })) as typeof fetch;
  await assert.rejects(fetchWorldCatalog(ORIGIN, { fetch: hanging, signal: controller.signal }), hasCode('ABORTED'));
  await assert.rejects(fetchWorldCatalog(ORIGIN, { fetch: hanging, signal: controller.signal }), hasCode('ABORTED'));
  assert.equal(calls, 1);
});

test('a response redirected away from the API is not accepted', async () => {
  const redirected = (async () => ({ ok: true, redirected: true, url: 'https://other.example/api/worlds',
    json: async () => ({ worlds: [] }) } as Response)) as typeof fetch;
  await assert.rejects(fetchWorldCatalog(ORIGIN, { fetch: redirected }), hasCode('INVALID_RESPONSE'));
});
