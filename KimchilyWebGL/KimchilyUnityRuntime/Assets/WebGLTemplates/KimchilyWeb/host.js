(function (root, factory) {
  'use strict';
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.KimchilyWebHost = factory();
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';
  const identifier = '[A-Za-z0-9_-]{1,80}';
  const manifestPath = new RegExp('^/worlds/(' + identifier + ')/(' + identifier + ')/world\\.json$');

  function parseLaunch(href) {
    const page = new URL(href);
    if (!['https:', 'http:'].includes(page.protocol)) throw new Error('HTTP 또는 HTTPS 주소로 실행해 주세요.');
    const query = page.searchParams;
    if (!query.has('manifest') && !query.has('sha256')) return { worldId: 'demo', revisionId: 'builtin-v1' };
    if (query.getAll('manifest').length !== 1 || query.getAll('sha256').length !== 1)
      throw new Error('월드 링크의 manifest와 sha256 값이 필요합니다.');
    const sha = query.get('sha256');
    if (!/^[a-fA-F0-9]{64}$/.test(sha)) throw new Error('월드 링크의 SHA-256 값이 올바르지 않습니다.');
    let url;
    try { url = new URL(query.get('manifest')); }
    catch (_) { throw new Error('월드 manifest 주소가 올바르지 않습니다.'); }
    const match = manifestPath.exec(url.pathname);
    if (!match || url.origin !== page.origin || url.username || url.password || url.search || url.hash)
      throw new Error('현재 서버의 /worlds/월드/버전/world.json 주소를 사용해 주세요.');
    return { worldId: match[1], revisionId: match[2], manifestUrl: url.href, manifestSha256: sha.toLowerCase() };
  }

  function createHost(launch, onState) {
    let instance = null, serial = 0, initializeId = '', openId = '', closeId = '';
    let phase = 'idle', failure = '', wantsOpen = false, initialized = false;
    let current = { phase: 'idle', message: '월드를 시작할 준비가 되었습니다.', progress: 0, canRetry: true };
    function state(next, message, progress, canRetry) {
      phase = next;
      current = { phase: next, message: message, progress: progress || 0, canRetry: !!canRetry };
      onState(Object.assign({}, current));
    }
    function send(type, requestId, extra) {
      instance.SendMessage('KimchilyHostBridge', 'Receive', JSON.stringify(Object.assign({
        protocolVersion: 1, type: type, requestId: requestId
      }, extra || {})));
    }
    function id(kind) { return 'web-' + kind + '-' + (++serial); }
    function open() {
      if (!initialized || !wantsOpen || openId || closeId) return;
      wantsOpen = false; failure = ''; openId = id('open');
      state('loading', '월드를 불러오는 중입니다.', 0, false);
      send('OpenWorld', openId, launch);
    }
    function close() {
      if (!instance || !openId || closeId) return;
      closeId = id('close');
      state('closing', failure ? failure + ' · 정리 중입니다.' : '월드를 종료하는 중입니다.', 0, false);
      send('CloseWorld', closeId, { worldId: launch.worldId, revisionId: launch.revisionId });
    }
    return {
      start: function () {
        if (!['idle', 'closed', 'error'].includes(phase) || (phase === 'error' && !current.canRetry)) return false;
        wantsOpen = true; failure = '';
        if (!instance) state('booting', '실행기를 준비하는 중입니다.', 0, false);
        else open();
        return true;
      },
      attach: function (value) {
        if (instance) throw new Error('The browser host already owns a Unity instance.');
        instance = value; initializeId = id('initialize');
        // Request an acknowledgement even if the unsolicited startup event arrived
        // before createUnityInstance resolved. This is the only event that opens.
        send('Initialize', initializeId);
      },
      receive: function (raw) {
        let event;
        try { event = typeof raw === 'string' ? JSON.parse(raw) : raw; } catch (_) { return false; }
        if (!event || event.protocolVersion !== 1 || typeof event.type !== 'string') return false;
        if (event.type === 'RuntimeReady') {
          if (initializeId && event.requestId === initializeId && !initialized) { initialized = true; open(); }
          return true;
        }
        if (event.type === 'WorldFailed' && event.requestId === initializeId) {
          state('fatal', event.message || '실행기 초기화에 실패했습니다.', 0, false); return true;
        }
        if (!openId) return false;
        if (event.type === 'WorldProgress' && event.requestId === openId && !closeId) {
          state('loading', '월드를 불러오는 중입니다.', Math.max(0, Math.min(1, Number(event.progress) || 0)), false);
        } else if (event.type === 'WorldReady' && event.requestId === openId && !closeId) {
          state('ready', '왼쪽 스틱으로 이동 · 오른쪽 드래그로 시점 이동', 1, false);
        } else if (event.type === 'CloseRequested' && event.worldId === launch.worldId) {
          close();
        } else if (event.type === 'WorldClosed' && (event.requestId === closeId ||
          (typeof event.requestId === 'string' && event.requestId.startsWith('unity-close-') &&
            event.worldId === launch.worldId && event.revisionId === launch.revisionId))) {
          openId = ''; closeId = ''; wantsOpen = false;
          state(failure ? 'error' : 'closed', failure || '월드를 종료했습니다.', 0, true);
        } else if (event.type === 'WorldFailed' && event.requestId === closeId) {
          state('fatal', event.message || '월드를 정리하지 못했습니다. 페이지를 새로 열어 주세요.', 0, false);
        } else if (event.type === 'WorldFailed' && event.requestId === openId) {
          if (event.code === 'CANCELLED' && closeId) return true;
          failure = (event.code ? event.code + ': ' : '') + (event.message || '월드를 불러오지 못했습니다.');
          // Script startup failures unload asynchronously. Await CloseWorld before
          // retrying so the next OpenWorld cannot collide with that cleanup.
          close();
        }
        return true;
      },
      close: close,
      failBoot: function (message) { state('fatal', String(message), 0, false); },
      loaderProgress: function (value) { if (phase === 'booting') state('booting', '실행기를 준비하는 중입니다.', value, false); },
      cancelInput: function () { if (instance) instance.SendMessage('KimchilyHostBridge', 'CancelInput', ''); },
      getState: function () { return Object.assign({}, current); }
    };
  }
  return { parseLaunch: parseLaunch, createHost: createHost };
});
