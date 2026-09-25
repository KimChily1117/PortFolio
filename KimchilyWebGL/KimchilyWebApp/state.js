import { parseQrPayload } from './qr.js';

export const RECENT_KEY = 'kimchily.recentWorlds.v1';
export const MAX_RECENTS = 6;

/** Keep only the exact same-origin player link and its matching immutable identity. */
export function normalizeWorld(value, origin) {
  if (!value || typeof value !== 'object') return null;
  try {
    const parsed = parseQrPayload(value.launchUrl, origin);
    if (parsed.kind !== 'player' || parsed.worldId !== value.worldId ||
        parsed.revisionId !== value.revisionId ||
        parsed.manifestSha256 !== String(value.manifestSha256 || '').toLowerCase()) return null;
    return {
      worldId: parsed.worldId,
      revisionId: parsed.revisionId,
      manifestSha256: parsed.manifestSha256,
      launchUrl: parsed.url,
      title: typeof value.title === 'string' && value.title.trim() ? value.title.trim().slice(0, 120) : parsed.worldId,
      visitedAt: Number.isSafeInteger(value.visitedAt) && value.visitedAt > 0 ? value.visitedAt : 0
    };
  } catch (_) { return null; }
}

export function normalizeCatalog(value, origin) {
  if (!value || !Array.isArray(value.worlds)) throw new Error('월드 목록 응답을 확인할 수 없어요.');
  const worlds = [], seen = new Set();
  for (const candidate of value.worlds.slice(0, 100)) {
    const world = normalizeWorld(candidate, origin);
    if (world && !seen.has(world.worldId)) { seen.add(world.worldId); worlds.push(world); }
  }
  // Origins are configured by this server, never taken from the scanned QR itself.
  const linkOrigins = [new URL(origin).origin];
  for (const candidate of Array.isArray(value.linkOrigins) ? value.linkOrigins.slice(0, 10) : []) {
    try {
      const url = new URL(candidate);
      if (['https:', 'http:'].includes(url.protocol) && !url.username && !url.password &&
          url.pathname === '/' && !url.search && !url.hash && !linkOrigins.includes(url.origin)) linkOrigins.push(url.origin);
    } catch (_) { /* Ignore invalid server aliases. */ }
  }
  return { worlds, linkOrigins };
}

export function readRecents(storage, origin) {
  try {
    const raw = storage.getItem(RECENT_KEY);
    if (!raw || raw.length > 65000) return [];
    const value = JSON.parse(raw);
    if (!Array.isArray(value)) return [];
    const seen = new Set();
    return value.slice(0, 100).map(item => normalizeWorld(item, origin))
      .filter(item => {
        if (!item || seen.has(item.worldId)) return false;
        seen.add(item.worldId); return true;
      }).slice(0, MAX_RECENTS);
  } catch (_) { return []; }
}

export function rememberWorld(storage, value, origin, now = Date.now()) {
  const world = normalizeWorld(value, origin);
  if (!world) throw new Error('입장할 월드 링크를 확인할 수 없어요.');
  const recents = [{ ...world, visitedAt: now }, ...readRecents(storage, origin).filter(item => item.worldId !== world.worldId)].slice(0, MAX_RECENTS);
  try { storage.setItem(RECENT_KEY, JSON.stringify(recents)); } catch (_) { /* Private mode/storage quota must not block entry. */ }
  return recents;
}

export function clearRecents(storage) {
  try { storage.removeItem(RECENT_KEY); } catch (_) { /* Storage can be unavailable in private mode. */ }
}

export function prepareEntry(value, origin, session, recentStorage, now) {
  const world = normalizeWorld(value, origin);
  if (!world) throw new Error('확인되지 않은 월드로는 이동할 수 없어요.');
  try { session.setItem('kimchily:returnHome', '1'); } catch (_) { /* Browser privacy settings may disable persistence. */ }
  rememberWorld(recentStorage, world, origin, now);
  return world.launchUrl;
}

export function recentTime(timestamp, now = Date.now()) {
  if (!timestamp || timestamp > now) return '최근에 열었던 월드';
  const days = Math.floor((now - timestamp) / 86400000);
  if (days === 0) return '오늘 방문';
  if (days === 1) return '어제 방문';
  return days < 30 ? `${days}일 전 방문` : '이전에 방문';
}
