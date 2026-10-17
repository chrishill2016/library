// Cover photos: resize/trim on the device, keep in IndexedDB, sync with Firebase Storage.
// Records: { id, blob, createdAt, attached, uploaded, pendingDelete }.
import { open } from './local-store.js';
import { uploadCover, downloadCover, deleteCover } from './firebase.js';

const STORE = 'photos';
const MAX_SIDE = 800;     // longest side in pixels
const ASPECT = 2 / 3;     // book cover: width / height
const QUALITY = 0.8;

const urls = new Map();   // photoId → object URL (one per session)

function tx(mode, fn) {
    return open().then(db => new Promise((resolve, reject) => {
        const t = db.transaction(STORE, mode);
        const result = fn(t.objectStore(STORE));
        // For reads, resolve with the request's result (undefined when the key is missing).
        t.oncomplete = () => resolve(result instanceof IDBRequest ? result.result : result);
        t.onerror = () => reject(t.error);
        t.onabort = () => reject(t.error);
    }));
}

const getRecord = id => tx('readonly', s => s.get(id)).then(r => r ?? null);
const putRecord = record => tx('readwrite', s => { s.put(record); });
const deleteRecord = id => tx('readwrite', s => { s.delete(id); });

/** Centre-trims to a 2:3 book shape and scales so the longest side is at most 800px. */
export function coverSize(width, height) {
    let cropW = width, cropH = height;
    if (width / height > ASPECT) cropW = Math.round(height * ASPECT);
    else cropH = Math.round(width / ASPECT);
    const scale = Math.min(1, MAX_SIDE / Math.max(cropW, cropH));
    return {
        sx: Math.round((width - cropW) / 2), sy: Math.round((height - cropH) / 2), sw: cropW, sh: cropH,
        dw: Math.round(cropW * scale), dh: Math.round(cropH * scale),
    };
}

async function toCoverJpeg(file) {
    // imageOrientation applies the camera's EXIF rotation.
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
    const c = coverSize(bitmap.width, bitmap.height);
    const canvas = document.createElement('canvas');
    canvas.width = c.dw;
    canvas.height = c.dh;
    canvas.getContext('2d').drawImage(bitmap, c.sx, c.sy, c.sw, c.sh, 0, 0, c.dw, c.dh);
    bitmap.close?.();
    return new Promise((resolve, reject) =>
        canvas.toBlob(b => b ? resolve(b) : reject(new Error('Could not encode the photo.')), 'image/jpeg', QUALITY));
}

/** Processes the file chosen in an <input type=file>; returns { id, url, bytes } or null if none chosen. */
export async function captureFromInput(input) {
    const file = input.files?.[0];
    input.value = '';   // allow choosing the same file again
    if (!file) return null;
    const blob = await toCoverJpeg(file);
    const id = crypto.randomUUID().replaceAll('-', '');
    await putRecord({ id, blob, createdAt: Date.now(), attached: false, uploaded: false, pendingDelete: false });
    const url = URL.createObjectURL(blob);
    urls.set(id, url);
    return { id, url, bytes: blob.size };
}

/** Object URL for a photo: from this device, else downloaded (and kept) when libraryId is given. */
export async function getUrl(photoId, libraryId) {
    if (urls.has(photoId)) return urls.get(photoId);
    let record = await getRecord(photoId);
    if (!record && libraryId && navigator.onLine) {
        try {
            const blob = await downloadCover(libraryId, photoId);
            record = { id: photoId, blob, createdAt: Date.now(), attached: true, uploaded: true, pendingDelete: false };
            await putRecord(record);
        } catch {
            return null;   // not uploaded yet by the other device, or offline
        }
    }
    if (!record || record.pendingDelete) return null;
    const url = URL.createObjectURL(record.blob);
    urls.set(photoId, url);
    return url;
}

export async function markAttached(photoId) {
    const record = await getRecord(photoId);
    if (record && !record.attached) await putRecord({ ...record, attached: true });
}

/**
 * Photo no longer used: delete it locally, and queue a remote delete unless it was
 * only ever on this device. A photo this device never saw (uploaded by the other
 * phone) may still be in Storage, so it gets a tombstone record too.
 */
export async function markForDeletion(photoId) {
    const record = await getRecord(photoId);
    const url = urls.get(photoId);
    if (url) { URL.revokeObjectURL(url); urls.delete(photoId); }
    if (record && !record.uploaded && !record.attached) {
        await deleteRecord(photoId);
        return;
    }
    await putRecord({ id: photoId, blob: null, createdAt: Date.now(), attached: true, uploaded: true, pendingDelete: true });
}

/** Uploads attached photos and deletes removed ones. Returns counts; stops at the first network error. */
export async function processQueue(libraryId) {
    const records = await tx('readonly', s => s.getAll());
    let uploaded = 0, deleted = 0;
    for (const r of records) {
        if (r.pendingDelete) {
            await deleteCover(libraryId, r.id);
            await deleteRecord(r.id);
            deleted++;
        } else if (r.attached && !r.uploaded) {
            await uploadCover(libraryId, r.id, r.blob);
            await putRecord({ ...r, uploaded: true });
            uploaded++;
        } else if (!r.attached && Date.now() - r.createdAt > 24 * 60 * 60 * 1000) {
            await deleteRecord(r.id);   // taken but never saved to a book
        }
    }
    return { uploaded, deleted };
}

export async function pendingCount() {
    const records = await tx('readonly', s => s.getAll());
    return records.filter(r => r.pendingDelete || (r.attached && !r.uploaded)).length;
}
