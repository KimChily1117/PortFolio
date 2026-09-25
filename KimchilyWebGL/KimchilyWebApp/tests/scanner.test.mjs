import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
const source = await readFile(new URL('../scanner.js', import.meta.url), 'utf8');
const { createQrScanner } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
function deferred() { let resolve, reject; const promise = new Promise((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; }
function fixture(overrides = {}) {
  let stopped = 0, requests = 0, sequence = 0;
  const track = { stop() { stopped++; } }, stream = { getTracks: () => [track] };
  const callbacks = new Map(), results = [], errors = [], states = [];
  const doc = new EventTarget(); doc.hidden = false;
  const win = new EventTarget();
  const video = { muted: false, playsInline: false, srcObject: null, readyState: 2, videoWidth: 1920, videoHeight: 1080,
    play: async () => {}, pause() {}, setAttribute() {} };
  const canvas = { width: 1, height: 1, getContext: () => ({ drawImage() {}, getImageData: () => ({ data: new Uint8ClampedArray(4), width: canvas.width, height: canvas.height }) }) };
  const environment = { document: doc, window: win, isSecureContext: true,
    mediaDevices: { async getUserMedia(constraints) { requests++; assert.equal(constraints.audio, false); assert.equal(constraints.video.facingMode.ideal, 'environment'); return stream; } },
    requestAnimationFrame(fn) { const id = ++sequence; callbacks.set(id, fn); return id; },
    cancelAnimationFrame(id) { callbacks.delete(id); }, createCanvas: () => canvas, decode: () => null,
    loadImage: async () => ({ naturalWidth: 1000, naturalHeight: 1000 }), ...overrides
  };
  const scanner = createQrScanner({ video, onResult: value => results.push(value), onError: error => errors.push(error), onState: value => states.push(value), environment });
  return { scanner, video, canvas, doc, win, stream, results, errors, states, callbacks, stopped: () => stopped, requests: () => requests,
    tick(time = 0) { const [id, fn] = callbacks.entries().next().value; callbacks.delete(id); fn(time); } };
}
test('camera is inert until start and uses muted inline rear-camera preference', async () => {
  const f = fixture(); assert.equal(f.requests(), 0); await f.scanner.start();
  assert.equal(f.requests(), 1); assert.equal(f.video.muted, true); assert.equal(f.video.playsInline, true);
  assert.equal(f.scanner.getState(), 'scanning'); f.scanner.dispose(); assert.equal(f.stopped(), 1);
});
test('QR detection stops every track and decoding loop before callback', async () => {
  const f = fixture({ decode: () => ({ data: 'http://localhost/w/a/b' }) }); await f.scanner.start(); f.tick();
  assert.equal(f.stopped(), 1); assert.equal(f.video.srcObject, null); assert.equal(f.callbacks.size, 0);
  assert.deepEqual(f.results, ['http://localhost/w/a/b']); assert.equal(f.canvas.width, 960); assert.equal(f.scanner.getState(), 'stopped');
});
test('cancel during permission request closes a late stream without restarting', async () => {
  const pending = deferred(); const f = fixture({ mediaDevices: { getUserMedia: () => pending.promise } });
  const starting = f.scanner.start(); f.scanner.stop(); pending.resolve(f.stream);
  assert.equal(await starting, false); assert.equal(f.stopped(), 1); assert.equal(f.video.srcObject, null); assert.equal(f.callbacks.size, 0);
});
test('second start cannot be replaced by an older permission response', async () => {
  const first = deferred(), second = deferred(); let count = 0, oldStops = 0;
  const f = fixture({ mediaDevices: { getUserMedia: () => (++count === 1 ? first.promise : second.promise) } });
  const a = f.scanner.start(), b = f.scanner.start(); second.resolve(f.stream); await b;
  first.resolve({ getTracks: () => [{ stop() { oldStops++; } }] }); await a;
  assert.equal(oldStops, 1); assert.equal(f.video.srcObject, f.stream); assert.equal(f.scanner.getState(), 'scanning'); f.scanner.dispose();
});
test('cancel while video.play is pending does not resume scanning', async () => {
  const pending = deferred(); const f = fixture(); f.video.play = () => pending.promise;
  const starting = f.scanner.start(); await Promise.resolve(); f.scanner.stop(); pending.resolve(); await starting;
  assert.equal(f.callbacks.size, 0); assert.equal(f.video.srcObject, null); assert.equal(f.scanner.getState(), 'stopped');
});
test('hidden page and pagehide release the camera', async () => {
  const f = fixture(); await f.scanner.start(); f.doc.hidden = true; f.doc.dispatchEvent(new Event('visibilitychange'));
  assert.equal(f.stopped(), 1); assert.equal(f.callbacks.size, 0);
  f.doc.hidden = false; await f.scanner.start(); f.win.dispatchEvent(new Event('pagehide')); assert.equal(f.stopped(), 2);
});
test('insecure camera error still leaves image scanning available', async () => {
  const f = fixture({ isSecureContext: false, decode: () => ({ data: 'image QR' }) });
  assert.equal(await f.scanner.start(), false); assert.equal(f.requests(), 0); assert.match(f.errors[0].message, /HTTPS/);
  assert.equal(await f.scanner.scanFile({ size: 100, type: 'image/png' }), 'image QR'); assert.deepEqual(f.results, ['image QR']);
});
test('permission denial is actionable and does not leave a loop', async () => {
  const f = fixture({ mediaDevices: { getUserMedia: async () => { throw Object.assign(new Error('denied'), { name: 'NotAllowedError' }); } } });
  assert.equal(await f.scanner.start(), false); assert.match(f.errors[0].message, /권한/); assert.equal(f.callbacks.size, 0);
});
test('image fallback stops active camera, supports inversion, and never navigates', async () => {
  const f = fixture({ decode: (_, width, height, options) => { assert.equal(options.inversionAttempts, 'attemptBoth'); return { data: 'image text' }; } });
  await f.scanner.start(); await f.scanner.scanFile({ size: 100, type: 'image/png' });
  assert.equal(f.stopped(), 1); assert.deepEqual(f.results, ['image text']); assert.equal(f.callbacks.size, 0);
});
test('image decode cancellation ignores a late completed image', async () => {
  const pending = deferred(); let signal;
  const f = fixture({ loadImage: (_, value) => { signal = value; return pending.promise; }, decode: () => ({ data: 'late' }) });
  const reading = f.scanner.scanFile({ size: 100, type: 'image/jpeg' }); f.scanner.stop();
  assert.equal(signal.aborted, true); pending.resolve({ width: 500, height: 500 }); await reading; assert.equal(f.results.length, 0);
});
test('oversized or executable images are rejected before loading', async () => {
  let loaded = 0; const f = fixture({ loadImage: async () => { loaded++; } });
  await f.scanner.scanFile({ size: 20 * 1024 * 1024, type: 'image/png' });
  await f.scanner.scanFile({ size: 100, type: 'image/svg+xml' }); assert.equal(loaded, 0); assert.equal(f.errors.length, 2);
});
test('decoder errors stop all camera work', async () => {
  const f = fixture({ decode: () => { throw new Error('decode failed'); } }); await f.scanner.start(); f.tick();
  assert.equal(f.stopped(), 1); assert.equal(f.callbacks.size, 0); assert.match(f.errors[0].message, /decode failed/);
});
test('dispose while permission pending closes late grant and prevents restarting', async () => {
  const pending = deferred(); const f = fixture({ mediaDevices: { getUserMedia: () => pending.promise } });
  const start = f.scanner.start(); f.scanner.dispose(); pending.resolve(f.stream); await start;
  assert.equal(f.stopped(), 1); assert.equal(await f.scanner.start(), false);
});
