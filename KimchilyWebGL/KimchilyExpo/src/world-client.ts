export interface WorldPublication {
  worldId: string;
  revisionId: string;
  title: string;
  manifestUrl: string;
  manifestSha256: string;
  launchUrl: string;
  publishUrl: string;
  qrUrl: string;
}

export interface WorldCatalog {
  worlds: WorldPublication[];
  linkOrigins: string[];
}

export interface ServerOptions {
  /** Development only: permits HTTP to private LAN or loopback addresses. */
  allowLanHttp?: boolean;
}

export interface RequestOptions extends ServerOptions {
  signal?: AbortSignal;
  timeoutMs?: number;
  fetch?: typeof globalThis.fetch;
}

export interface QrOptions extends ServerOptions {
  /** Only aliases returned by the configured server's catalog should be supplied. */
  allowedOrigins?: readonly string[];
}

export interface ResolveOptions extends RequestOptions, QrOptions {}

export interface ParsedWorldLink {
  kind: 'player' | 'landing' | 'legacy';
  url: string;
  worldId: string;
  revisionId: string;
  manifestUrl?: string;
  manifestSha256?: string;
}

export class WorldClientError extends Error {
  readonly code: string;

  constructor(message: string, code = 'INVALID_WORLD') {
    super(message);
    this.name = 'WorldClientError';
    this.code = code;
  }
}

const HASH = /^[a-fA-F0-9]{64}$/;
const ID = /^[A-Za-z0-9_-]{1,80}$/;
const RESERVED_ID = /^(con|prn|aux|nul|com[1-9]|lpt[1-9])$/i;
const MANIFEST_PATH = /^\/worlds\/([A-Za-z0-9_-]{1,80})\/([A-Za-z0-9_-]{1,80})\/world\.json$/;
const LANDING_PATH = /^\/w\/([A-Za-z0-9_-]{1,80})\/([A-Za-z0-9_-]{1,80})$/;

function fail(message: string, code?: string): never {
  throw new WorldClientError(message, code);
}

function validId(value: unknown): value is string {
  return typeof value === 'string' && ID.test(value) && !RESERVED_ID.test(value);
}

function record(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function localAddress(host: string): boolean {
  const hostname = host.toLowerCase().replace(/^\[|\]$/g, '');
  if (hostname === 'localhost' || hostname === '::1') return true;
  if (/^(?:fc|fd)[0-9a-f]{2}:/.test(hostname) || /^fe[89ab][0-9a-f]:/.test(hostname)) return true;
  const octets = hostname.split('.').map(Number);
  if (!/^\d+\.\d+\.\d+\.\d+$/.test(hostname) || octets.some(part => part < 0 || part > 255)) return false;
  return octets[0] === 10 || octets[0] === 127 ||
    octets[0] === 192 && octets[1] === 168 ||
    octets[0] === 172 && octets[1] >= 16 && octets[1] <= 31 ||
    octets[0] === 169 && octets[1] === 254;
}

/** Returns an origin without a trailing slash. Never guesses a URL scheme. */
export function normalizeServerOrigin(value: string, options: ServerOptions = {}): string {
  if (typeof value !== 'string' || value.length > 256)
    fail('서버 주소를 확인해 주세요.', 'INVALID_SERVER');
  const text = value.trim();
  if (!/^https?:\/\/[^/?#\\\s]+\/?$/i.test(text) || /[\u0000-\u0020\u007f]/.test(text))
    fail('서버 주소에는 http(s) 주소와 포트만 입력해 주세요.', 'INVALID_SERVER');
  let url: URL;
  try { url = new URL(text); } catch { return fail('서버 주소를 확인해 주세요.', 'INVALID_SERVER'); }
  if (url.username || url.password || url.pathname !== '/' || url.search || url.hash)
    fail('서버 주소에 계정 정보나 경로를 넣을 수 없습니다.', 'INVALID_SERVER');
  if (url.protocol !== 'https:' && !(url.protocol === 'http:' && options.allowLanHttp && localAddress(url.hostname)))
    fail('HTTPS 서버를 입력해 주세요. 개발 모드에서만 로컬 네트워크의 HTTP 주소를 사용할 수 있습니다.', 'HTTP_NOT_ALLOWED');
  return url.origin;
}

function qrText(value: unknown): string {
  if (typeof value !== 'string' || !value.trim() || value.length > 4096)
    fail('Kimchily 월드 QR이나 링크를 입력해 주세요.', 'INVALID_QR');
  const text = value.trim();
  if (/[\u0000-\u0020\u007f\\]/.test(text) || /%(?![a-fA-F0-9]{2})/.test(text) || text.includes('#'))
    fail('QR 링크에 허용되지 않는 문자가 있습니다.', 'INVALID_QR');
  return text;
}

function strictUrl(text: string, base?: string): URL {
  if (text.startsWith('//')) fail('QR 주소 형식이 올바르지 않습니다.', 'INVALID_QR');
  // URL normalizes dot segments before exposing pathname. Reject those original
  // paths as well, so accepted links match the publisher's literal route grammar.
  const rawPath = text.startsWith('/') ? text.split('?')[0] :
    text.match(/^[a-z][a-z0-9+.-]*:\/\/[^/?#]*(\/[^?#]*)?/i)?.[1] ?? '';
  if (rawPath.includes('%') || /(?:^|\/)\.{1,2}(?:\/|$)/.test(rawPath))
    fail('월드 링크의 경로가 올바르지 않습니다.', 'INVALID_QR');
  try {
    const url = text.startsWith('/') && base ? new URL(text, base) : new URL(text);
    if (url.username || url.password || url.hash) fail('계정 정보가 포함된 월드 링크는 열 수 없습니다.', 'INVALID_QR');
    return url;
  } catch (error) {
    if (error instanceof WorldClientError) throw error;
    return fail('Kimchily 월드 링크가 아닙니다.', 'INVALID_QR');
  }
}

function trustedAliases(origin: string, aliases: readonly string[] = []): string[] {
  const trusted = new Set([origin]);
  for (const alias of aliases.slice(0, 100)) {
    try {
      // HTTP aliases identify old QR inputs only. We never fetch these addresses;
      // API requests and returned player links always use the selected origin.
      trusted.add(normalizeServerOrigin(alias, { allowLanHttp: true }));
    } catch { /* Invalid configuration never adds another trusted address. */ }
  }
  return [...trusted];
}

function parseManifestQuery(url: URL, trusted: Set<string>): Required<Pick<ParsedWorldLink,
  'worldId' | 'revisionId' | 'manifestUrl' | 'manifestSha256'>> {
  const fields: string[] = [];
  url.searchParams.forEach((_, key) => fields.push(key));
  if (fields.length !== 2 || url.searchParams.getAll('manifest').length !== 1 || url.searchParams.getAll('sha256').length !== 1)
    fail('월드 링크에는 manifest와 sha256 값이 한 번씩 필요합니다.', 'INVALID_QR');
  const hash = url.searchParams.get('sha256') ?? '';
  if (!HASH.test(hash)) fail('월드 링크의 SHA-256 값이 올바르지 않습니다.', 'INVALID_QR');
  const manifestText = qrText(url.searchParams.get('manifest'));
  const manifest = strictUrl(manifestText);
  const match = MANIFEST_PATH.exec(manifest.pathname);
  if (!match || !validId(match[1]) || !validId(match[2]) || !trusted.has(manifest.origin) ||
      manifestText.includes('?') || manifest.search || manifest.hash)
    fail('등록된 서버의 월드 manifest 주소가 아닙니다.', 'UNTRUSTED_WORLD');
  return { worldId: match[1], revisionId: match[2], manifestUrl: manifest.href, manifestSha256: hash.toLowerCase() };
}

/** Validates QR syntax/origins without fetching any QR-supplied address. */
export function parseWorldQr(value: string, serverOrigin: string, options: QrOptions = {}): ParsedWorldLink {
  const origin = normalizeServerOrigin(serverOrigin, options);
  const trusted = new Set(trustedAliases(origin, options.allowedOrigins));
  const text = qrText(value);
  const url = strictUrl(text, origin);
  if (url.protocol === 'kimchily:') {
    if (url.hostname !== 'world' || url.port || !['', '/'].includes(url.pathname))
      fail('Kimchily 월드 링크 형식이 올바르지 않습니다.', 'INVALID_QR');
    return { kind: 'legacy', url: url.href, ...parseManifestQuery(url, trusted) };
  }
  if (!trusted.has(url.origin)) fail('등록된 월드 서버의 QR만 열 수 있습니다.', 'UNTRUSTED_WORLD');
  const landing = LANDING_PATH.exec(url.pathname);
  if (landing && validId(landing[1]) && validId(landing[2])) {
    if (text.includes('?')) fail('월드 소개 링크에는 추가 값을 넣을 수 없습니다.', 'INVALID_QR');
    return { kind: 'landing', url: url.href, worldId: landing[1], revisionId: landing[2] };
  }
  if (url.pathname !== '/player/' && url.pathname !== '/player')
    fail('지원하는 Kimchily 월드 링크가 아닙니다.', 'INVALID_QR');
  return { kind: 'player', url: url.href, ...parseManifestQuery(url, trusted) };
}

/** Validates every returned link against the selected server, excluding aliases. */
export function validateWorldPublication(value: unknown, serverOrigin: string, options: ServerOptions = {}): WorldPublication {
  const origin = normalizeServerOrigin(serverOrigin, options);
  if (!record(value) || !validId(value.worldId) || !validId(value.revisionId) ||
      typeof value.manifestSha256 !== 'string' || !HASH.test(value.manifestSha256) ||
      typeof value.launchUrl !== 'string' || value.platform !== undefined && value.platform !== 'WebGL')
    fail('서버가 올바르지 않은 월드 정보를 보냈습니다.', 'INVALID_RESPONSE');
  const world = value.worldId;
  const revision = value.revisionId;
  const paths: Record<string, string> = {
    manifestUrl: `/worlds/${world}/${revision}/world.json`,
    publishUrl: `/w/${world}/${revision}`,
    qrUrl: `/qr/${world}/${revision}.png`,
  };
  for (const [key, path] of Object.entries(paths)) {
    const text = qrText(value[key]);
    const url = strictUrl(text);
    if (url.origin !== origin || url.pathname !== path || text.includes('?') || url.search || url.hash)
      fail('서버의 월드 링크가 선택한 서버와 일치하지 않습니다.', 'INVALID_RESPONSE');
  }
  const launch = parseWorldQr(value.launchUrl, origin, options);
  const hash = value.manifestSha256.toLowerCase();
  if (launch.kind !== 'player' || launch.worldId !== world || launch.revisionId !== revision ||
      launch.manifestUrl !== value.manifestUrl || launch.manifestSha256 !== hash)
    fail('서버의 실행 링크와 월드 정보가 일치하지 않습니다.', 'INVALID_RESPONSE');
  return {
    worldId: world, revisionId: revision,
    title: typeof value.title === 'string' && value.title.trim() && value.title.length <= 120 ? value.title.trim() : world,
    manifestUrl: value.manifestUrl as string, manifestSha256: hash,
    launchUrl: launch.url, publishUrl: value.publishUrl as string, qrUrl: value.qrUrl as string,
  };
}

function problemMessage(code: unknown): string {
  switch (code) {
    case 'WRONG_PLATFORM': return '이 월드는 Android 앱용입니다. WebGL로 게시된 월드를 선택해 주세요.';
    case 'NOT_FOUND': return '게시된 월드를 찾을 수 없습니다. 새 QR을 확인해 주세요.';
    case 'HASH_MISMATCH': return 'QR 링크와 게시된 월드 정보가 일치하지 않습니다.';
    case 'INVALID_WEB_LAUNCH': return '이 서버에서 열 수 있는 월드 QR이 아닙니다.';
    default: return '서버에서 월드 정보를 가져오지 못했습니다. 잠시 후 다시 시도해 주세요.';
  }
}

async function requestJson(url: string, options: RequestOptions): Promise<unknown> {
  if (options.signal?.aborted) fail('월드 요청을 취소했습니다.', 'ABORTED');
  const controller = new AbortController();
  const abort = () => controller.abort();
  options.signal?.addEventListener('abort', abort, { once: true });
  let timedOut = false;
  const requestedTimeout = options.timeoutMs ?? 10_000;
  const timeout = Number.isFinite(requestedTimeout) && requestedTimeout > 0 ? Math.min(60_000, requestedTimeout) : 10_000;
  const timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeout);
  try {
    const response = await (options.fetch ?? globalThis.fetch)(url, {
      method: 'GET', signal: controller.signal, redirect: 'error', credentials: 'omit', cache: 'no-store',
      headers: { Accept: 'application/json' },
    });
    if (response.redirected || response.url && new URL(response.url).href !== new URL(url).href)
      fail('서버가 다른 주소로 요청을 이동했습니다. 서버 주소를 확인해 주세요.', 'INVALID_RESPONSE');
    let payload: unknown;
    try { payload = await response.json(); }
    catch { return fail('서버 응답을 읽을 수 없습니다.', 'INVALID_RESPONSE'); }
    if (!response.ok) {
      const code = record(payload) && typeof payload.code === 'string' ? payload.code : 'SERVER_ERROR';
      fail(problemMessage(code), code);
    }
    return payload;
  } catch (error) {
    if (timedOut) fail('서버 응답이 늦어 요청을 중단했습니다. 네트워크를 확인해 주세요.', 'TIMEOUT');
    if (options.signal?.aborted) fail('월드 요청을 취소했습니다.', 'ABORTED');
    if (error instanceof WorldClientError) throw error;
    return fail('서버에 연결할 수 없습니다. 주소와 네트워크를 확인해 주세요.', 'NETWORK_ERROR');
  } finally {
    clearTimeout(timer);
    options.signal?.removeEventListener('abort', abort);
  }
}

export async function fetchWorldCatalog(serverOrigin: string, options: RequestOptions = {}): Promise<WorldCatalog> {
  const origin = normalizeServerOrigin(serverOrigin, options);
  const payload = await requestJson(`${origin}/api/worlds`, options);
  if (!record(payload) || !Array.isArray(payload.worlds))
    fail('서버가 올바르지 않은 월드 목록을 보냈습니다.', 'INVALID_RESPONSE');
  const worlds: WorldPublication[] = [];
  const seen = new Set<string>();
  for (const candidate of payload.worlds.slice(0, 100)) {
    try {
      const world = validateWorldPublication(candidate, origin, options);
      if (!seen.has(world.worldId)) { worlds.push(world); seen.add(world.worldId); }
    } catch { /* Keep the valid catalog entries usable when one record is malformed. */ }
  }
  const aliases = Array.isArray(payload.linkOrigins) ? payload.linkOrigins.filter((value): value is string => typeof value === 'string') : [];
  return { worlds, linkOrigins: trustedAliases(origin, aliases) };
}

export async function resolveWorldQr(serverOrigin: string, value: string, options: ResolveOptions = {}): Promise<WorldPublication> {
  const origin = normalizeServerOrigin(serverOrigin, options);
  const parsed = parseWorldQr(value, origin, options); // Reject external QR inputs before calling fetch.
  const query = encodeURIComponent(parsed.url);
  const payload = await requestJson(`${origin}/api/resolve?url=${query}`, options);
  const world = validateWorldPublication(payload, origin, options);
  if (world.worldId !== parsed.worldId || world.revisionId !== parsed.revisionId ||
      parsed.manifestSha256 !== undefined && world.manifestSha256 !== parsed.manifestSha256)
    fail('서버 응답이 스캔한 월드와 일치하지 않습니다.', 'INVALID_RESPONSE');
  return world;
}
