import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = new URL('../', import.meta.url);
test('manifest and HTML reference existing shell resources with real PNG dimensions', () => {
  const manifest = JSON.parse(readFileSync(new URL('manifest.webmanifest', root), 'utf8'));
  assert.equal(manifest.start_url, '/'); assert.equal(manifest.scope, '/');
  for (const icon of manifest.icons) {
    const data = readFileSync(new URL(icon.src.replace('/app/', ''), root));
    const size = Number(icon.sizes.split('x')[0]);
    assert.equal(data.readUInt32BE(16), size); assert.equal(data.readUInt32BE(20), size);
  }
  const html = readFileSync(new URL('index.html', root), 'utf8');
  for (const [, path] of html.matchAll(/(?:src|href)="\/app\/([^"#?]+)"/g)) assert.ok(existsSync(new URL(path, root)), path);
  assert.ok(html.indexOf('vendor/jsQR-1.4.0.js') < html.indexOf('src="/app/app.js"'));
});
test('service worker only intercepts the explicit shell, never a world, player, API, or CA response', () => {
  const events = {};
  vm.runInNewContext(readFileSync(new URL('sw.js', root), 'utf8'), { self: { location: { origin: 'https://world.example' }, addEventListener: (name, fn) => { events[name] = fn; } }, URL, Set });
  for (const path of ['/player/', '/player/Build/player.wasm', '/worlds/a/b/world.json', '/api/worlds', '/api/resolve?url=x', '/dev/ca.cer', '/dev/setup', '/sw.js', '/?token=secret']) {
    let intercepted = false;
    events.fetch({ request: { method: 'GET', url: `https://world.example${path}` }, respondWith() { intercepted = true; } });
    assert.equal(intercepted, false, path);
  }
});
test('all precached paths are shipped and no API/world/player paths are in the install cache', async () => {
  const events = {}, cached = [];
  vm.runInNewContext(readFileSync(new URL('sw.js', root), 'utf8'), {
    self: { addEventListener: (name, fn) => { events[name] = fn; }, skipWaiting() {} }, URL, Set,
    caches: { open: async () => ({ addAll: async paths => cached.push(...paths) }) }
  });
  let work; events.install({ waitUntil(promise) { work = promise; } }); await work;
  for (const path of cached) {
    const file = path === '/' ? 'index.html' : path.replace(/^\/app\//, '').replace(/^\//, '');
    assert.ok(existsSync(new URL(file, root)), file);
    assert.ok(!/^\/(api|worlds|player|dev)\//.test(path));
  }
});
