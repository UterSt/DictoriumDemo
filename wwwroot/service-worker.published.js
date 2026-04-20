// Published service worker - handles offline caching
self.importScripts('./service-worker-assets.js');
self.addEventListener('message', event => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

const cacheName = 'dictorium-demo-cache-v1';
const offlineAssetsCache = 'dictorium-demo-offline-v1';

async function onInstall(event) {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsExclude.every(u => !asset.url.startsWith(u)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(offlineAssetsCache).then(cache => cache.addAll(assetsRequests));
}

const offlineAssetsExclude = [ '.dll', 'service-worker-assets.js' ];

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

async function onFetch(event) {
    let cachedResponse = null;
    if (event.request.method === 'GET') {
        const shouldServeIndexHtml = event.request.mode === 'navigate';
        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(offlineAssetsCache);
        cachedResponse = await cache.match(request);
    }
    return cachedResponse || fetch(event.request);
}
