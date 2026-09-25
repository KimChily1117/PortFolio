import { validateWorldPublication, type ServerOptions, type WorldPublication } from './world-client.ts';

export const SERVER_KEY = 'kimchily.mobile.server.v1';
export const RECENT_KEY = 'kimchily.mobile.recent.v1';
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
