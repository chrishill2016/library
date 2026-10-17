// Cover photos: crop/straighten on the device, keep in IndexedDB, sync with Firebase Storage.
// Records: { id, blob, createdAt, attached, uploaded, pendingDelete }.
import { open } from './local-store.js';
import { uploadCover, downloadCover, deleteCover } from './firebase.js';
import { detectCover, outputSize, warp } from './cover-geometry.js';

const STORE = 'photos';
const MAX_SIDE = 800;     // longest side in pixels
const ASPECT = 2 / 3;     // book cover: width / height, for the fallback outline
const WORK_SIDE = 1600;   // drafts are kept at most this big (enough detail for an 800px cover)
const DETECT_SIDE = 256;  // outline guessing works on a small copy
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

// Drafts: photos taken but not yet cropped. id → { canvas, url, corners (0–1), guess (0–1) }.
const drafts = new Map();

/** Opens the photo chosen in an <input type=file> as a draft with a best-guess outline; null if none chosen. */
export async function openDraft(input) {
    const file = input.files?.[0];
    input.value = '';   // allow choosing the same file again
    if (!file) return null;
    // imageOrientation applies the camera's EXIF rotation.
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
    const canvas = drawScaled(bitmap, WORK_SIDE);
    bitmap.close?.();

    const small = drawScaled(canvas, DETECT_SIDE);
    const pixels = small.getContext('2d').getImageData(0, 0, small.width, small.height).data;
    const found = detectCover(pixels, small.width, small.height);
    const guess = found ? found.map(p => ({ x: p.x / small.width, y: p.y / small.height })) : centredBook(canvas.width, canvas.height);

    const preview = await toBlob(canvas, 0.85);
    const id = crypto.randomUUID().replaceAll('-', '');
    drafts.set(id, { canvas, url: URL.createObjectURL(preview), corners: guess.map(p => ({ ...p })), guess, found: !!found });
    return { id, found: !!found };
}

/** Without a confident guess: a centred 2:3 box filling most of the photo. */
function centredBook(width, height) {
    let w = 0.9, h = 0.9;
    if (width * w / (height * h) > ASPECT) w = h * height * ASPECT / width;
    else h = w * width / ASPECT / height;
    const x0 = (1 - w) / 2, y0 = (1 - h) / 2;
    return [{ x: x0, y: y0 }, { x: x0 + w, y: y0 }, { x: x0 + w, y: y0 + h }, { x: x0, y: y0 + h }];
}

function drawScaled(source, maxSide) {
    const scale = Math.min(1, maxSide / Math.max(source.width, source.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(source.width * scale));
    canvas.height = Math.max(1, Math.round(source.height * scale));
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(source, 0, 0, canvas.width, canvas.height);
    return canvas;
}

const toBlob = (canvas, quality) => new Promise((resolve, reject) =>
    canvas.toBlob(b => b ? resolve(b) : reject(new Error('Could not encode the photo.')), 'image/jpeg', quality));

/**
 * Shows the draft in `stage` with four draggable corner handles over a darkened surround.
 * Built here rather than in Blazor so dragging stays smooth.
 */
export function showCropper(stage, id) {
    const draft = drafts.get(id);
    if (!draft) return;
    stage.replaceChildren();
    const frame = document.createElement('div');
    frame.className = 'crop-frame';
    const img = document.createElement('img');
    img.src = draft.url;
    img.alt = 'Photo to crop';
    img.draggable = false;
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('viewBox', '0 0 1 1');
    svg.setAttribute('preserveAspectRatio', 'none');
    svg.innerHTML = '<path class="crop-shade" fill-rule="evenodd"/><polygon class="crop-outline"/>';
    frame.append(img, svg);

    const names = ['top left', 'top right', 'bottom right', 'bottom left'];
    const handles = draft.corners.map((_, i) => {
        const h = document.createElement('div');
        h.className = 'crop-handle';
        h.setAttribute('role', 'slider');
        h.setAttribute('aria-label', `Move ${names[i]} corner`);
        h.dataset.corner = String(i);
        frame.append(h);
        return h;
    });

    const draw = () => {
        const pts = draft.corners.map(p => `${p.x},${p.y}`).join(' ');
        svg.querySelector('.crop-outline').setAttribute('points', pts);
        svg.querySelector('.crop-shade').setAttribute('d', `M0,0H1V1H0Z M${draft.corners.map(p => `${p.x},${p.y}`).join('L')}Z`);
        handles.forEach((h, i) => {
            h.style.left = `${draft.corners[i].x * 100}%`;
            h.style.top = `${draft.corners[i].y * 100}%`;
        });
    };

    let active = -1;
    const move = e => {
        if (active < 0) return;
        const r = frame.getBoundingClientRect();
        draft.corners[active] = {
            x: Math.min(1, Math.max(0, (e.clientX - r.left) / r.width)),
            y: Math.min(1, Math.max(0, (e.clientY - r.top) / r.height)),
        };
        draw();
    };
    frame.addEventListener('pointerdown', e => {
        // Grab the nearest corner, so a touch near (not exactly on) a handle still works.
        const r = frame.getBoundingClientRect();
        const px = e.clientX - r.left, py = e.clientY - r.top;
        const d = draft.corners.map(p => Math.hypot(p.x * r.width - px, p.y * r.height - py));
        const nearest = d.indexOf(Math.min(...d));
        if (d[nearest] > 64) return;
        active = nearest;
        handles[active].classList.add('dragging');
        frame.setPointerCapture(e.pointerId);
        e.preventDefault();
    });
    frame.addEventListener('pointermove', move);
    const end = () => {
        handles[active]?.classList.remove('dragging');
        active = -1;
    };
    frame.addEventListener('pointerup', end);
    frame.addEventListener('pointercancel', end);

    stage.append(frame);
    draw();
    draft.redraw = draw;
}

/** Puts the corners back on the guess, or on the whole photo. */
export function resetCorners(id, wholePhoto) {
    const draft = drafts.get(id);
    if (!draft) return;
    draft.corners = wholePhoto
        ? [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }, { x: 0, y: 1 }]
        : draft.guess.map(p => ({ ...p }));
    draft.redraw?.();
}

/** Straightens the outlined cover, stores it as a photo and returns { id, url, bytes }. */
export async function saveDraft(id) {
    const draft = drafts.get(id);
    if (!draft) throw new Error('That photo is no longer available.');
    const { canvas } = draft;
    const corners = draft.corners.map(p => ({ x: p.x * canvas.width, y: p.y * canvas.height }));
    const { width, height } = outputSize(corners, MAX_SIDE);
    const src = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data;
    const out = document.createElement('canvas');
    out.width = width;
    out.height = height;
    out.getContext('2d').putImageData(new ImageData(warp(src, canvas.width, canvas.height, corners, width, height), width, height), 0, 0);
    const blob = await toBlob(out, QUALITY);
    discardDraft(id);

    const photoId = crypto.randomUUID().replaceAll('-', '');
    await putRecord({ id: photoId, blob, createdAt: Date.now(), attached: false, uploaded: false, pendingDelete: false });
    const url = URL.createObjectURL(blob);
    urls.set(photoId, url);
    return { id: photoId, url, bytes: blob.size };
}

export function discardDraft(id) {
    const draft = drafts.get(id);
    if (!draft) return;
    URL.revokeObjectURL(draft.url);
    drafts.delete(id);
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
