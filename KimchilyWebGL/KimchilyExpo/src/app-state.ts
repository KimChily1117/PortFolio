import { validateWorldPublication, type ServerOptions, type WorldPublication } from './world-client.ts';

export const SERVER_KEY = 'kimchily.mobile.server.v1';
export const RECENT_KEY = 'kimchily.mobile.recent.v1';
export const NICKNAME_KEY = 'kimchily.mobile.nickname.v1';
export function validNickname(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0 && value.trim().length <= 24 && !/[\u0000-\u001f\u007f-\u009f]/.test(value);
}
export function nicknameLaunch(url: string, nickname: string): string {
  if (!validNickname(nickname)) throw new Error('월드에서 사용할 닉네임을 1–24자로 입력해 주세요.');
  const launch = new URL(url); launch.hash = new URLSearchParams({ nickname: nickname.trim(), room: 'playground' }).toString(); return launch.href;
}
export interface RecentWorld { origin: string; world: WorldPublication; visitedAt: number }

export function parseRecents(raw: string | null, origin: string, options: ServerOptions): RecentWorld[] {
  if (!raw || raw.length > 100000 || !origin) return [];
  try {
    const values: unknown = JSON.parse(raw);
    if (!Array.isArray(values)) return [];
    const seen = new Set<string>(), result: RecentWorld[] = [];
    for (const entry of values.slice(0, 30)) {
      if (!entry || entry.origin !== origin || !Number.isSafeInteger(entry.visitedAt) || entry.visitedAt < 0) continue;
      try {
        const world = validateWorldPublication(entry.world, origin, options);
        if (seen.has(world.worldId)) continue;
        seen.add(world.worldId); result.push({ origin, world, visitedAt: entry.visitedAt });
      } catch { /* Ignore stale or tampered local records. */ }
      if (result.length === 6) break;
    }
    return result;
  } catch { return []; }
}

export function appendRecent(current: RecentWorld[], world: WorldPublication, origin: string, options: ServerOptions, now = Date.now()): RecentWorld[] {
  const validated = validateWorldPublication(world, origin, options);
  return [{ origin, world: validated, visitedAt: now }, ...current.filter(item => item.origin === origin && item.world.worldId !== validated.worldId)].slice(0, 6);
}

export function visitLabel(timestamp: number, now = Date.now()): string {
  const days = Math.floor((now - timestamp) / 86400000);
  return days <= 0 ? '오늘 방문' : days === 1 ? '어제 방문' : days < 30 ? `${days}일 전 방문` : '이전에 방문';
}
