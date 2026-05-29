'use strict';

const CACHE_NAME = 'hygiene-audit-v2';
const STATIC_ASSETS = [
    '/Content/sneat/vendor/css/core.css',
    '/Content/sneat/vendor/css/theme-default.css',
    '/Content/sneat/css/demo.css',
    '/Content/sneat/vendor/fonts/boxicons.css',
    '/Content/sneat/vendor/libs/perfect-scrollbar/perfect-scrollbar.css',
    '/Content/sneat/vendor/libs/jquery/jquery.js',
    '/Content/sneat/vendor/libs/popper/popper.js',
    '/Content/sneat/vendor/js/bootstrap.js',
    '/Content/sneat/vendor/libs/perfect-scrollbar/perfect-scrollbar.js',
    '/Content/sneat/vendor/js/menu.js',
    '/Content/sneat/vendor/js/helpers.js',
    '/Content/sneat/js/config.js',
    '/Content/sneat/js/main.js'
];

self.addEventListener('install', function (event) {
    event.waitUntil(
        caches.open(CACHE_NAME).then(function (cache) {
            return cache.addAll(STATIC_ASSETS);
        }).then(function () {
            return self.skipWaiting();
        })
    );
});

self.addEventListener('activate', function (event) {
    event.waitUntil(
        caches.keys().then(function (cacheNames) {
            return Promise.all(
                cacheNames.filter(function (name) { return name !== CACHE_NAME; })
                          .map(function (name) { return caches.delete(name); })
            );
        }).then(function () {
            return self.clients.claim();
        })
    );
});

self.addEventListener('fetch', function (event) {
    var url = new URL(event.request.url);

    // Skip non-GET, cross-origin, and API requests (network-only)
    if (event.request.method !== 'GET') return;
    if (url.origin !== location.origin) return;
    if (url.pathname.startsWith('/api/')) return;

    // App viewmodels: network-first so logic updates ship immediately after deploy,
    // falling back to cache when offline.
    if (url.pathname.startsWith('/Scripts/app/')) {
        event.respondWith(
            fetch(event.request).then(function (response) {
                var clone = response.clone();
                caches.open(CACHE_NAME).then(function (cache) { cache.put(event.request, clone); });
                return response;
            }).catch(function () {
                return caches.match(event.request);
            })
        );
        return;
    }

    // Vendor static assets: cache-first
    if (url.pathname.startsWith('/Content/sneat/') ||
        url.pathname.startsWith('/Scripts/') ||
        url.pathname.endsWith('.css') ||
        url.pathname.endsWith('.js') ||
        url.pathname.endsWith('.woff2') ||
        url.pathname.endsWith('.woff') ||
        url.pathname.endsWith('.ttf')) {
        event.respondWith(
            caches.match(event.request).then(function (cached) {
                return cached || fetch(event.request).then(function (response) {
                    var clone = response.clone();
                    caches.open(CACHE_NAME).then(function (cache) { cache.put(event.request, clone); });
                    return response;
                });
            })
        );
        return;
    }

    // HTML pages: network-first, fall back to cache
    event.respondWith(
        fetch(event.request).then(function (response) {
            var clone = response.clone();
            caches.open(CACHE_NAME).then(function (cache) { cache.put(event.request, clone); });
            return response;
        }).catch(function () {
            return caches.match(event.request);
        })
    );
});
