// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => {
    // Only the app's own files and book covers go through the cache; API calls
    // (Firebase, book catalogues) go straight to the network.
    const url = new URL(event.request.url);
    if (url.origin === self.location.origin || coverHosts.includes(url.hostname)) {
        event.respondWith(onFetch(event));
    }
});
self.addEventListener('message', event => {
    if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
});

const cacheNamePrefix = 'offline-cache-';
// Book covers from the catalogues, kept across app versions so they show offline.
const coverCacheName = 'book-covers-v1';
const coverHosts = ['books.google.com', 'books.googleusercontent.com', 'covers.openlibrary.org'];
const maxCovers = 2000;
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// The registration scope is the app's base path (e.g. /library/ on GitHub Pages).
const baseUrl = new URL(self.registration.scope);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    const url = new URL(event.request.url);
    if (event.request.method === 'GET' && coverHosts.includes(url.hostname)) {
        return coverFromCache(event.request);
    }

    let cachedResponse = null;
    if (event.request.method === 'GET') {
        // For all navigation requests, try to serve index.html from cache,
        // unless that request is for an offline resource.
        // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    return cachedResponse || fetch(event.request);
}

// Cache-first for cover images. Only CORS-readable responses are cached:
// opaque responses would count megabytes each against the storage quota.
async function coverFromCache(request) {
    const cache = await caches.open(coverCacheName);
    const cached = await cache.match(request.url);
    if (cached) return cached;

    try {
        const response = await fetch(request.url, { mode: 'cors', credentials: 'omit' });
        if (response.ok) {
            await cache.put(request.url, response.clone());
            trimCovers(cache);
        }
        return response;
    } catch {
        // No CORS (or offline): fall back to a normal image request, uncached.
        return fetch(request);
    }
}

async function trimCovers(cache) {
    const keys = await cache.keys();
    for (let i = 0; i < keys.length - maxCovers; i++) {
        await cache.delete(keys[i]);
    }
}
