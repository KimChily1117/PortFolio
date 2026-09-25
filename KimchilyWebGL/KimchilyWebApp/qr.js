const ID = '[A-Za-z0-9_-]{1,80}';
const MANIFEST = new RegExp('^/worlds/(' + ID + ')/(' + ID + ')/world\\.json$');
const LANDING = new RegExp('^/w/(' + ID + ')/(' + ID + ')$');
const HASH = /^[a-fA-F0-9]{64}$/;

function fail(message) { throw new Error(message); }
function trustedOrigins(origin, aliases) {
  const current = new URL(origin);
  if (!['http:', 'https:'].includes(current.protocol) || current.username || current.password)
    fail('월드 서버 주소가 올바르지 않습니다.');
  const trusted = new Set([current.origin]);
  for (const alias of aliases || []) {
    try {
      const parsed = new URL(alias);
      if (['http:', 'https:'].includes(parsed.protocol) && !parsed.username && !parsed.password &&
          parsed.pathname === '/' && !parsed.search && !parsed.hash) trusted.add(parsed.origin);
    } catch (_) { /* Ignore malformed server configuration, never broaden trust. */ }
  }
  return { current: current.origin, trusted };
}
function strictText(value) {
  if (typeof value !== 'string' || value.length > 8192) fail('QR 링크가 너무 길거나 올바르지 않습니다.');
  const text = value.trim();
  if (!text || /[\u0000-\u0020\u007f\\]/.test(text) || /%(?![a-fA-F0-9]{2})/.test(text) || text.includes('#'))
    fail('QR 링크에 허용되지 않는 문자가 있습니다.');
  return text;
}
function manifestQuery(url, trusted) {
  const keys = [...url.searchParams.keys()];
  if (keys.length !== 2 || url.searchParams.getAll('manifest').length !== 1 || url.searchParams.getAll('sha256').length !== 1)
    fail('월드 링크에는 manifest와 sha256 값이 한 번씩 필요합니다.');
  const hash = url.searchParams.get('sha256');
  if (!HASH.test(hash)) fail('월드 링크의 SHA-256 값이 올바르지 않습니다.');
  let manifest;
  try { manifest = new URL(strictText(url.searchParams.get('manifest'))); }
  catch (_) { fail('월드 manifest 주소가 올바르지 않습니다.'); }
  const match = MANIFEST.exec(manifest.pathname);
  if (!match || !trusted.has(manifest.origin) || manifest.username || manifest.password || manifest.search || manifest.hash || manifest.href.includes('?'))
    fail('등록된 서버의 월드 manifest 주소가 아닙니다.');
  return { worldId: match[1], revisionId: match[2], manifestUrl: manifest.href, manifestSha256: hash.toLowerCase() };
}

/** Syntax/origin validation only. Resolve with the server before offering launch. */
export function parseQrPayload(value, origin = globalThis.location?.origin, allowedOrigins = []) {
  const { current, trusted } = trustedOrigins(origin, allowedOrigins);
  const text = strictText(value);
  let url;
  try {
    if (text.startsWith('//')) fail('주소 형식이 올바르지 않습니다.');
    url = text.startsWith('/') ? new URL(text, current) : new URL(text);
  } catch (_) { fail('Kimchily 월드 링크가 아닙니다.'); }
  if (url.username || url.password || url.hash) fail('계정 정보나 fragment를 포함한 링크는 사용할 수 없습니다.');
  if (url.protocol === 'kimchily:') {
    if (url.hostname !== 'world' || url.port || url.pathname) fail('Kimchily 월드 링크 형식이 올바르지 않습니다.');
    return { kind: 'legacy', url: url.href, ...manifestQuery(url, trusted) };
  }
  if (!trusted.has(url.origin)) fail('등록된 월드 서버의 QR만 열 수 있습니다.');
  const landing = LANDING.exec(url.pathname);
  if (landing) {
    if (text.includes('?')) fail('월드 소개 링크에는 query를 추가할 수 없습니다.');
    return { kind: 'landing', url: url.href, worldId: landing[1], revisionId: landing[2] };
  }
  if (url.pathname !== '/player/' && url.pathname !== '/player') fail('지원하는 Kimchily 월드 링크가 아닙니다.');
  return { kind: 'player', url: url.href, ...manifestQuery(url, trusted) };
}

/** The resolver checks local stored revision/platform/hash and never fetches an external URL. */
export async function resolveQrPayload(value, options = {}) {
  const origin = options.origin || globalThis.location?.origin;
  const parsed = parseQrPayload(value, origin, options.allowedOrigins || []);
  const fetcher = options.fetch || globalThis.fetch;
  const api = new URL('/api/resolve', origin);
  api.searchParams.set('url', parsed.url);
  const response = await fetcher(api.href, {
    signal: options.signal, credentials: 'same-origin', redirect: 'error', cache: 'no-store',
    headers: { Accept: 'application/json' }
  });
  if (!response.ok) {
    let message = '이 월드를 열 수 없습니다. WebGL로 게시된 QR인지 확인해 주세요.';
    try { const error = await response.json(); if (typeof error.message === 'string') message = error.message; } catch (_) { }
    fail(message);
  }
  const result = await response.json();
  if (!result || typeof result.launchUrl !== 'string') fail('서버의 월드 응답이 올바르지 않습니다.');
  // The server upgrades allowed old HTTP links to its current HTTPS origin.
  const launch = parseQrPayload(result.launchUrl, origin);
  if (launch.kind !== 'player' || launch.worldId !== parsed.worldId || launch.revisionId !== parsed.revisionId ||
      result.worldId !== launch.worldId || result.revisionId !== launch.revisionId ||
      result.manifestUrl !== launch.manifestUrl || typeof result.manifestSha256 !== 'string' ||
      result.manifestSha256.toLowerCase() !== launch.manifestSha256 ||
      (parsed.manifestSha256 && parsed.manifestSha256 !== launch.manifestSha256))
    fail('서버 응답이 스캔한 월드와 일치하지 않습니다.');
  return { ...result, launchUrl: launch.url };
}
