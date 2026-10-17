// Browser hooks for connectivity and service worker updates.

export function isOnline() {
    return navigator.onLine;
}

export function watchConnectivity(dotnetRef) {
    const notify = () => dotnetRef.invokeMethodAsync('OnConnectivityChanged', navigator.onLine);
    window.addEventListener('online', notify);
    window.addEventListener('offline', notify);
}

// index.html stores the registration promise on window.bookshelfServiceWorker.
export async function watchForUpdates(dotnetRef) {
    const registration = await window.bookshelfServiceWorker;
    if (!registration) return;

    const notifyIfWaiting = () => {
        if (registration.waiting && navigator.serviceWorker.controller) {
            dotnetRef.invokeMethodAsync('OnUpdateAvailable');
        }
    };

    notifyIfWaiting();
    registration.addEventListener('updatefound', () => {
        const worker = registration.installing;
        worker?.addEventListener('statechange', () => {
            if (worker.state === 'installed') notifyIfWaiting();
        });
    });

    // Check for a new version when the app comes back to the foreground, and hourly.
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') registration.update();
    });
    setInterval(() => registration.update(), 60 * 60 * 1000);
}

export async function applyUpdate() {
    const registration = await window.bookshelfServiceWorker;
    if (!registration?.waiting) {
        location.reload();
        return;
    }
    navigator.serviceWorker.addEventListener('controllerchange', () => location.reload(), { once: true });
    registration.waiting.postMessage({ type: 'SKIP_WAITING' });
}
