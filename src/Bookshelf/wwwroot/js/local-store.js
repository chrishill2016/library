// Minimal IndexedDB wrapper used by IndexedDbLocalStore.
const DB_NAME = 'bookshelf';
const DB_VERSION = 3;

let dbPromise;

export function open() {
    dbPromise ??= new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains('books')) db.createObjectStore('books', { keyPath: 'id' });
            if (!db.objectStoreNames.contains('outbox')) db.createObjectStore('outbox', { keyPath: 'id' });
            if (!db.objectStoreNames.contains('settings')) db.createObjectStore('settings', { keyPath: 'key' });
            // v2: cover photos (JPEG blobs) and their upload/delete state; see photos.js.
            if (!db.objectStoreNames.contains('photos')) db.createObjectStore('photos', { keyPath: 'id' });
            // v3: each person's rating, review and read dates per book.
            if (!db.objectStoreNames.contains('readings')) db.createObjectStore('readings', { keyPath: 'id' });
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
    return dbPromise;
}

function done(tx) {
    return new Promise((resolve, reject) => {
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error);
    });
}

function result(request) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

export async function getAll(store) {
    const db = await open();
    return result(db.transaction(store).objectStore(store).getAll());
}

export async function get(store, key) {
    const db = await open();
    return (await result(db.transaction(store).objectStore(store).get(key))) ?? null;
}

export async function count(store) {
    const db = await open();
    return result(db.transaction(store).objectStore(store).count());
}

export async function put(store, value) {
    const db = await open();
    const tx = db.transaction(store, 'readwrite');
    tx.objectStore(store).put(value);
    return done(tx);
}

export async function remove(store, key) {
    const db = await open();
    const tx = db.transaction(store, 'readwrite');
    tx.objectStore(store).delete(key);
    return done(tx);
}

// Writes a record and (optionally) its outbox entry in one transaction.
export async function putWithOutbox(store, value, outboxEntry) {
    const db = await open();
    const tx = db.transaction([store, 'outbox'], 'readwrite');
    tx.objectStore(store).put(value);
    if (outboxEntry) tx.objectStore('outbox').put(outboxEntry);
    return done(tx);
}

// Empties the given stores in one transaction.
export async function clear(...stores) {
    const db = await open();
    const tx = db.transaction(stores, 'readwrite');
    for (const store of stores) tx.objectStore(store).clear();
    return done(tx);
}
