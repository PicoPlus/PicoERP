/* PicoERP Service Worker
 * Handles:
 *  1. Offline caching of app shell (cache-first for static assets)
 *  2. Web Push notifications (push event → showNotification)
 *  3. Notification click → focus or open the app
 */

const CACHE_NAME = 'picoerp-v1';
const SHELL_URLS = [
  '/',
  '/css/picoerp.css',
  '/icon-192.png',
  '/icon-512.png',
  '/manifest.json'
];

// ── Install: pre-cache the app shell ─────────────────────────────────────────
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(SHELL_URLS))
      .then(() => self.skipWaiting())
  );
});

// ── Activate: clean up old caches ────────────────────────────────────────────
self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

// ── Fetch: network-first for navigation, cache-first for static ──────────────
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);

  // Skip non-GET, cross-origin, and Blazor SignalR requests
  if (event.request.method !== 'GET') return;
  if (url.origin !== self.location.origin) return;
  if (url.pathname.startsWith('/_blazor')) return;
  if (url.pathname.startsWith('/api/')) return;

  // Network-first for HTML navigation
  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request).catch(() => caches.match('/'))
    );
    return;
  }

  // Cache-first for everything else
  event.respondWith(
    caches.match(event.request).then(cached => cached || fetch(event.request))
  );
});

// ── Push: receive a push message and show a notification ─────────────────────
self.addEventListener('push', event => {
  let data = { title: 'پیکو ERP', body: 'یک رویداد جدید دارید', icon: '/icon-192.png', badge: '/icon-192.png', url: '/' };
  if (event.data) {
    try { Object.assign(data, event.data.json()); } catch { data.body = event.data.text(); }
  }

  event.waitUntil(
    self.registration.showNotification(data.title, {
      body:    data.body,
      icon:    data.icon  || '/icon-192.png',
      badge:   data.badge || '/icon-192.png',
      tag:     data.tag   || 'picoerp-notification',
      data:    { url: data.url || '/' },
      dir:     'rtl',
      lang:    'fa',
      vibrate: [200, 100, 200],
      requireInteraction: false
    })
  );
});

// ── Notification click: focus the app or open a new window ───────────────────
self.addEventListener('notificationclick', event => {
  event.notification.close();
  const targetUrl = (event.notification.data && event.notification.data.url) || '/';

  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windowClients => {
      for (const client of windowClients) {
        if (client.url.startsWith(self.location.origin) && 'focus' in client) {
          client.navigate(targetUrl);
          return client.focus();
        }
      }
      return clients.openWindow(targetUrl);
    })
  );
});
