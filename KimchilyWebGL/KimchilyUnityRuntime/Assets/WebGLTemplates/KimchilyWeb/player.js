(function () {
  'use strict';
  const element = id => document.getElementById(id);
  const canvas = element('unity-canvas'), panel = element('panel'), start = element('start');
  const close = element('close'), reload = element('reload'), fullscreen = element('fullscreen');
  const status = element('status'), progress = element('progress');
  let host, bootStarted = false, launch;
  function render(state) {
    panel.hidden = state.phase === 'ready';
    status.textContent = state.message;
    progress.hidden = !['booting', 'loading', 'closing'].includes(state.phase);
    progress.value = state.progress;
    start.hidden = !state.canRetry;
    start.textContent = ['closed', 'error'].includes(state.phase) ? '다시 시작' : '월드 시작';
    close.hidden = !['loading', 'ready'].includes(state.phase);
    reload.hidden = state.phase !== 'fatal';
    fullscreen.hidden = state.phase !== 'ready' || !element('player').requestFullscreen;
    if (state.phase === 'ready') canvas.focus({ preventScroll: true });
    if (state.phase === 'closed') {
      try {
        if (sessionStorage.getItem('kimchily:returnHome') === '1') {
          sessionStorage.removeItem('kimchily:returnHome');
          location.assign('/');
        }
      } catch (_) { /* Storage can be disabled; the visible home link still works. */ }
    }
  }
  try {
    launch = KimchilyWebHost.parseLaunch(location.href);
    element('world-label').textContent = launch.manifestUrl ? launch.worldId + ' / ' + launch.revisionId : 'Kimchily Demo';
    host = KimchilyWebHost.createHost(launch, render);
  } catch (error) {
    status.textContent = error.message; start.hidden = true;
    element('title').textContent = '월드 링크를 확인해 주세요.';
    return;
  }
  // Install before loading Unity: RuntimeReady can precede the loader Promise.
  window.KimchilyWebReceive = function (json) {
    host.receive(json);
    try {
      const event = JSON.parse(json);
      window.dispatchEvent(new CustomEvent('kimchily-world-event', { detail: event }));
    }
    catch (_) { /* A malformed diagnostic event must not break the host. */ }
  };
  start.addEventListener('click', function () {
    if (!host.start() || bootStarted) return;
    bootStarted = true;
    const config = Object.assign({}, window.KimchilyUnityConfig);
    const loaderUrl = config.loaderUrl; delete config.loaderUrl;
    config.showBanner = function (message, type) {
      if (type === 'error') host.failBoot(message);
      else console.warn('[Kimchily Web]', message);
    };
    const loader = document.createElement('script');
    loader.src = loaderUrl;
    loader.onerror = () => host.failBoot('실행기를 내려받지 못했습니다. 서버 주소와 연결을 확인해 주세요.');
    loader.onload = function () {
      if (typeof createUnityInstance !== 'function') { host.failBoot('Unity 로더를 실행하지 못했습니다.'); return; }
      createUnityInstance(canvas, config, value => host.loaderProgress(value))
        .then(instance => host.attach(instance))
        .catch(error => host.failBoot('실행기를 시작하지 못했습니다. ' + String(error)));
    };
    document.body.appendChild(loader);
  });
  close.addEventListener('click', () => { host.cancelInput(); host.close(); });
  reload.addEventListener('click', () => location.reload());
  fullscreen.addEventListener('click', () => {
    const request = element('player').requestFullscreen();
    if (request && request.catch) request.catch(() => {});
  });
  canvas.addEventListener('contextmenu', event => event.preventDefault());
  ['touchstart', 'touchmove'].forEach(type => canvas.addEventListener(type, event => event.preventDefault(), { passive: false }));
  canvas.addEventListener('touchcancel', () => host.cancelInput(), { passive: true });
  canvas.addEventListener('pointerdown', () => canvas.focus({ preventScroll: true }));
  canvas.addEventListener('wheel', event => event.preventDefault(), { passive: false });
  document.addEventListener('visibilitychange', () => { if (document.hidden) host.cancelInput(); });
  window.addEventListener('blur', () => host.cancelInput());
  window.addEventListener('orientationchange', () => host.cancelInput());
  window.addEventListener('resize', () => host.cancelInput());
  render(host.getState());
})();
