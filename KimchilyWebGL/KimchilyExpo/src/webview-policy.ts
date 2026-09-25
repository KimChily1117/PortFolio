/** Top-level navigation cannot leave the configured world server. */
export function navigationDecision(raw: string, origin: string): 'allow' | 'home' | 'block' {
  try {
    const url = new URL(raw);
    if (!['https:', 'http:'].includes(url.protocol) || url.origin !== origin || url.username || url.password) return 'block';
    if (url.pathname === '/' && !url.search) return 'home';
    return 'allow';
  } catch { return 'block'; }
}

export interface RuntimeEvent { protocolVersion: 1; type: string; requestId: string; message?: string; code?: string }
export interface RuntimeContext {
  sourceUrl: string; launchUrl: string; worldId: string; revisionId: string;
  /** Android's WebMessageListener reports the frame origin instead of its full URL. */
  allowOriginOnlySource?: boolean;
}
export function readRuntimeEvent(raw: string, context: RuntimeContext): RuntimeEvent | null {
  if (typeof raw !== 'string' || raw.length > 16384) return null;
  try {
    const envelope = JSON.parse(raw);
    if (!envelope || typeof envelope.pageUrl !== 'string') return null;
    const launch = new URL(context.launchUrl), source = new URL(context.sourceUrl);
    if (new URL(envelope.pageUrl).href !== launch.href) return null;
    const originOnly = context.allowOriginOnlySource && source.origin === launch.origin &&
      source.pathname === '/' && !source.search && !source.hash && !source.username && !source.password;
    if (source.href !== launch.href && !originOnly) return null;
    const value = envelope.event;
    if (!value || value.protocolVersion !== 1 || !['RuntimeReady', 'WorldProgress', 'WorldReady', 'WorldFailed', 'WorldClosed'].includes(value.type)) return null;
    if (value.type !== 'RuntimeReady' && (value.worldId !== context.worldId || value.revisionId !== context.revisionId ||
        typeof value.requestId !== 'string' || !/^[A-Za-z0-9_-]{1,128}$/.test(value.requestId))) return null;
    return { protocolVersion: 1, type: value.type, requestId: typeof value.requestId === 'string' ? value.requestId : '', message: typeof value.message === 'string' ? value.message.slice(0, 500) : undefined, code: typeof value.code === 'string' ? value.code.slice(0, 80) : undefined };
  } catch { return null; }
}

// Installed before content and again after load: duplicate listener installation is guarded.
export const NATIVE_WORLD_BRIDGE = `
(function () {
  window.__KIMCHILY_NATIVE_HOST__ = true;
  if (window.__kimchilyNativeBridgeInstalled) return;
  window.__kimchilyNativeBridgeInstalled = true;
  window.addEventListener('kimchily-world-event', function (event) {
    try {
      if (window.ReactNativeWebView) window.ReactNativeWebView.postMessage(JSON.stringify({
        pageUrl: window.location.href, event: event.detail
      }));
    } catch (_) {}
  });
})(); true;
`;
