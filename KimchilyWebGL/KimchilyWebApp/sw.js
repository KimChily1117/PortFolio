const CACHE_NAME = 'kimchily-home-v1';
const SHELL = [
  '/', '/manifest.webmanifest', '/app/styles.css', '/app/app.js', '/app/state.js',
  '/app/qr.js', '/app/scanner.js', '/app/vendor/jsQR-1.4.0.js',
  '/app/icons/icon.svg', '/app/icons/icon-192.png', '/app/icons/icon-512.png',
  '/app/icons/icon-maskable-512.png', '/app/icons/apple-touch-icon.png'
];
const SHELL_PATHS = new Set(SHELL);

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE_NAME).then(cache => cache.addAll(SHELL)).then(() => self.skipWaiting()));
});
self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(names => Promise.all(names.filter(name => name.startsWith('kimchily-home-') && name !== CACHE_NAME).map(name => caches.delete(name)))).then(() => self.clients.claim()));
});
self.addEventListener('fetch', event => {
  const request = event.request, url = new URL(request.url);
  // Never cache player builds, world manifests/bundles, API replies, credentials, or CA setup.
  if (request.method !== 'GET' || url.origin !== self.location.origin || url.search || !SHELL_PATHS.has(url.pathname)) return;
  event.respondWith((async () => {
    const cache = await caches.open(CACHE_NAME);
    try {
      const response = await fetch(request);
      if (response.ok && response.type !== 'opaque' && !response.redirected) await cache.put(request, response.clone());
      return response;
    } catch (error) {
      const cached = await cache.match(url.pathname);
      if (cached) return cached;
      throw error;
    }
  })());
});
