import { resolveQrPayload } from './qr.js';
import { createQrScanner } from './scanner.js';
import { normalizeCatalog, readRecents, clearRecents, prepareEntry, recentTime } from './state.js';

const $ = id => document.getElementById(id);
const origin = location.origin;
const safeStorage = name => { try { return window[name]; } catch (_) { return null; } };
const local = safeStorage('localStorage'), session = safeStorage('sessionStorage');
let allowedOrigins = [origin], catalogRequest = null, entryRequest = null, deferredInstall = null;
let mode = 'scan';
const entryDialog = $('entry-dialog');
const canUseCamera = isSecureContext && !!navigator.mediaDevices?.getUserMedia;
const scanner = createQrScanner({
  video: $('camera-video'),
  onResult: text => { scanner.stop(); void enterLink(text, true); },
  onError: error => { showEntryMessage(error?.message || 'QR를 읽지 못했어요. 다른 이미지를 선택하거나 링크로 입장해 주세요.', 'error'); },
  onState: state => {
    $('camera-stage').dataset.live = String(state === 'scanning');
    $('camera-status').textContent = ({ starting: '카메라 권한을 확인하고 있어요.', scanning: 'QR를 화면 안에 맞춰 주세요.', decoding: '이미지에서 QR를 찾고 있어요.', stopped: '카메라로 월드 QR를 비춰 주세요.' })[state] || '';
    $('start-camera').textContent = state === 'scanning' ? '카메라 끄기' : '카메라 켜기';
    $('start-camera').disabled = !canUseCamera || ['starting', 'decoding'].includes(state);
    $('pick-image').disabled = state === 'decoding';
  }
});

function showMessage(message, kind = 'info') {
  $('status-message').textContent = message;
  $('status-message').dataset.kind = kind;
  $('status-message').hidden = !message;
}
function showEntryMessage(message, kind = 'info') {
  $('entry-message').textContent = message;
  $('entry-message').dataset.kind = kind;
  $('entry-message').hidden = !message;
}
function setMode(next) {
  mode = next;
  for (const name of ['scan', 'link']) {
    const active = name === next;
    $(`${name}-tab`).setAttribute('aria-selected', String(active));
    $(`${name}-tab`).tabIndex = active ? 0 : -1;
    $(`${name}-panel`).hidden = !active;
  }
  if (next !== 'scan') scanner.stop();
}
function openEntry(next = 'scan') {
  if (entryRequest) return;
  setMode(next);
  showEntryMessage('');
  if (!entryDialog.open) entryDialog.showModal();
  if (next === 'link') $('world-link').focus();
}

function make(tag, className, text) {
  const element = document.createElement(tag);
  if (className) element.className = className;
  if (text !== undefined) element.textContent = text;
  return element;
}
function renderWorlds(worlds) {
  $('world-list').replaceChildren();
  worlds.forEach((world, index) => {
    const card = make('article', 'world-card');
    const art = make('div', 'card-art');
    art.dataset.palette = String(index % 4); art.setAttribute('aria-hidden', 'true');
    art.append(make('span', 'card-badge', 'WEB WORLD'), make('div', 'card-portal'));
    const body = make('div', 'card-info');
    body.append(make('h3', '', world.title), make('p', 'card-meta', '새로운 이야기가 기다리는 공간'));
    const button = make('button', 'card-entry'); button.type = 'button';
    button.setAttribute('aria-label', `${world.title} 월드 입장`);
    button.append(make('span', '', '월드 입장'), make('span', '', '↗'));
    button.addEventListener('click', () => void enterLink(world.launchUrl));
    body.append(button); card.append(art, body); $('world-list').append(card);
  });
  $('world-count').textContent = String(worlds.length);
  $('world-count').hidden = worlds.length === 0;
  $('world-empty').hidden = worlds.length !== 0;
}
function renderRecents() {
  const recents = readRecents(local, origin);
  $('recent-section').hidden = recents.length === 0;
  $('recent-list').replaceChildren();
  for (const world of recents) {
    const button = make('button', 'recent-card'); button.type = 'button';
    button.setAttribute('aria-label', `${world.title} 다시 입장`);
    const copy = make('span', 'recent-copy');
    copy.append(make('strong', '', world.title), make('small', '', recentTime(world.visitedAt)));
    button.append(make('span', 'recent-mark', '↗'), copy, make('span', 'recent-arrow', '→'));
    button.addEventListener('click', () => void enterLink(world.launchUrl));
    $('recent-list').append(button);
  }
}
async function loadCatalog() {
  catalogRequest?.abort();
  const request = new AbortController(); catalogRequest = request;
  const timer = setTimeout(() => request.abort(), 12000);
  $('world-loading').hidden = false;
  $('world-error').hidden = true;
  $('world-empty').hidden = true;
  $('refresh-worlds').disabled = true;
  $('world-list').setAttribute('aria-busy', 'true');
  try {
    const response = await fetch('/api/worlds?limit=20', { signal: request.signal, cache: 'no-store', credentials: 'same-origin', redirect: 'error' });
    if (!response.ok) throw new Error(`목록 요청에 실패했어요 (${response.status}).`);
    const catalog = normalizeCatalog(await response.json(), origin);
    if (request !== catalogRequest || request.signal.aborted) return;
    allowedOrigins = catalog.linkOrigins;
    renderWorlds(catalog.worlds);
  } catch (_) {
    if (request !== catalogRequest) return;
    $('world-list').replaceChildren(); $('world-count').hidden = true;
    $('world-error').hidden = false;
    $('world-error-title').textContent = navigator.onLine ? '월드 서버에 연결하지 못했어요' : '다시 연결하면 월드를 볼 수 있어요';
    $('world-error-description').textContent = navigator.onLine ? '서버가 켜져 있는지, 같은 네트워크에 연결되어 있는지 확인해 주세요.' : '홈 화면과 최근 방문 기록은 남아 있어요. 월드 입장에는 연결이 필요해요.';
  } finally {
    clearTimeout(timer);
    if (request === catalogRequest) {
      catalogRequest = null; $('world-loading').hidden = true;
      $('refresh-worlds').disabled = false; $('world-list').removeAttribute('aria-busy');
    }
  }
}

async function enterLink(raw, reopenOnError = false) {
  if (entryRequest) return;
  if (!navigator.onLine) {
    const message = '지금은 오프라인이에요. 인터넷과 월드 서버에 연결한 뒤 다시 시도해 주세요.';
    if (entryDialog.open) showEntryMessage(message, 'error'); else showMessage(message, 'error');
    return;
  }
  const request = new AbortController(); entryRequest = request;
  let timedOut = false;
  const timer = setTimeout(() => { timedOut = true; request.abort(); }, 15000);
  scanner.stop();
  if (entryDialog.open) entryDialog.close();
  $('entry-overlay').hidden = false; $('cancel-entry').focus();
  try {
    const world = await resolveQrPayload(raw, { origin, allowedOrigins, signal: request.signal });
    if (request !== entryRequest || request.signal.aborted) return;
    const url = prepareEntry(world, origin, session, local);
    location.assign(url);
  } catch (error) {
    if (request !== entryRequest || (request.signal.aborted && !timedOut)) return;
    const message = timedOut ? '서버 응답이 늦어지고 있어요. 연결을 확인한 뒤 다시 시도해 주세요.' : error.message || '월드 연결을 확인하지 못했어요.';
    if (reopenOnError) {
      setMode(mode); entryDialog.showModal(); showEntryMessage(message, 'error');
    } else showMessage(message, 'error');
  } finally {
    clearTimeout(timer);
    if (request === entryRequest) { entryRequest = null; $('entry-overlay').hidden = true; }
  }
}
function cancelEntry() {
  entryRequest?.abort(); entryRequest = null; $('entry-overlay').hidden = true;
}
function updateConnection() {
  $('offline-banner').hidden = navigator.onLine;
}

function openInstall() {
  const ios = /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  const standalone = matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
  $('install-ios').hidden = !ios || standalone;
  $('install-other').hidden = ios || standalone;
  $('install-security').hidden = isSecureContext;
  $('installed-message').hidden = !standalone;
  $('native-install').hidden = !deferredInstall || standalone;
  if (!$('install-dialog').open) $('install-dialog').showModal();
}
$('open-scan').addEventListener('click', () => openEntry('scan'));
$('open-link').addEventListener('click', () => openEntry('link'));
$('scan-tab').addEventListener('click', () => setMode('scan'));
$('link-tab').addEventListener('click', () => { setMode('link'); $('world-link').focus(); });
document.querySelector('.entry-tabs').addEventListener('keydown', event => {
  if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
  event.preventDefault(); setMode(event.key === 'Home' ? 'scan' : event.key === 'End' ? 'link' : mode === 'scan' ? 'link' : 'scan'); $(`${mode}-tab`).focus();
});
$('close-entry').addEventListener('click', () => entryDialog.close());
entryDialog.addEventListener('close', () => scanner.stop());
$('start-camera').disabled = !canUseCamera;
$('camera-security-note').hidden = isSecureContext;
if (isSecureContext && !canUseCamera) $('camera-status').textContent = '이 브라우저에서는 카메라를 사용할 수 없어요. QR 이미지를 선택해 주세요.';
$('start-camera').addEventListener('click', async () => {
  showEntryMessage('');
  if ($('camera-stage').dataset.live === 'true') scanner.stop();
  else { try { await scanner.start(); } catch (error) { showEntryMessage(error.message, 'error'); } }
});
$('pick-image').addEventListener('click', () => $('qr-file').click());
$('qr-file').addEventListener('change', async event => {
  const file = event.target.files?.[0]; event.target.value = '';
  if (!file) return;
  showEntryMessage('');
  try { await scanner.scanFile(file); } catch (error) { showEntryMessage(error.message, 'error'); }
});
$('link-form').addEventListener('submit', event => { event.preventDefault(); void enterLink($('world-link').value, true); });
$('cancel-entry').addEventListener('click', cancelEntry);
$('refresh-worlds').addEventListener('click', () => void loadCatalog());
$('retry-worlds').addEventListener('click', () => void loadCatalog());
$('clear-recents').addEventListener('click', () => { clearRecents(local); renderRecents(); });
$('install-button').addEventListener('click', openInstall);
$('install-strip-button').addEventListener('click', openInstall);
$('close-install').addEventListener('click', () => $('install-dialog').close());
window.addEventListener('beforeinstallprompt', event => { event.preventDefault(); deferredInstall = event; });
window.addEventListener('appinstalled', () => { deferredInstall = null; $('install-dialog').close(); showMessage('홈 화면에 Kimchily를 추가했어요.'); });
$('native-install').addEventListener('click', async () => {
  if (!deferredInstall) return;
  const prompt = deferredInstall; deferredInstall = null; $('native-install').hidden = true;
  try { await prompt.prompt(); await prompt.userChoice; } catch (_) { /* Browser menu instructions stay visible. */ }
});
window.addEventListener('online', () => { updateConnection(); void loadCatalog(); });
window.addEventListener('offline', updateConnection);
window.addEventListener('pagehide', () => { scanner.stop(); cancelEntry(); catalogRequest?.abort(); });
window.addEventListener('pageshow', event => { if (event.persisted) { cancelEntry(); updateConnection(); renderRecents(); void loadCatalog(); } });
document.addEventListener('visibilitychange', () => { if (document.hidden) scanner.stop(); });
updateConnection(); renderRecents(); void loadCatalog();
if (isSecureContext && 'serviceWorker' in navigator) {
  navigator.serviceWorker.register('/sw.js', { scope: '/', updateViaCache: 'none' }).catch(() => { /* Live home remains usable when offline installation is unavailable. */ });
}
