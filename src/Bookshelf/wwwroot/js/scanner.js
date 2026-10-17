// Live barcode scanning with the browser's BarcodeDetector (Chrome on Android).
// Calls dotnetRef.OnBarcode(value) for each new code; repeats of the same code
// are ignored for a few seconds so holding a book still doesn't re-add it.

const REPEAT_IGNORE_MS = 4000;
const SCAN_INTERVAL_MS = 150;

let session = null;

export async function isSupported() {
    if (!('BarcodeDetector' in window) || !navigator.mediaDevices?.getUserMedia) return false;
    try {
        const formats = await BarcodeDetector.getSupportedFormats();
        return formats.includes('ean_13');
    } catch {
        return false;
    }
}

export async function start(video, dotnetRef) {
    await stop();
    const stream = await navigator.mediaDevices.getUserMedia({
        audio: false,
        video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
    });
    video.srcObject = stream;
    video.setAttribute('playsinline', '');
    video.muted = true;
    await video.play();

    const detector = new BarcodeDetector({ formats: ['ean_13', 'ean_8', 'upc_a'] });
    const track = stream.getVideoTracks()[0];
    const state = { stream, track, video, dotnetRef, detector, lastCode: null, lastAt: 0, busy: false, timer: 0 };
    session = state;

    state.timer = setInterval(async () => {
        if (state.busy || video.readyState < 2) return;
        state.busy = true;
        try {
            const codes = await detector.detect(video);
            for (const { rawValue } of codes) {
                const now = Date.now();
                if (rawValue === state.lastCode && now - state.lastAt < REPEAT_IGNORE_MS) continue;
                state.lastCode = rawValue;
                state.lastAt = now;
                navigator.vibrate?.(60);
                await dotnetRef.invokeMethodAsync('OnBarcode', rawValue);
            }
        } catch {
            // A failed frame is fine; try the next one.
        } finally {
            state.busy = false;
        }
    }, SCAN_INTERVAL_MS);

    const caps = track.getCapabilities?.() ?? {};
    return { hasTorch: !!caps.torch };
}

export async function setTorch(on) {
    if (!session?.track) return;
    try {
        await session.track.applyConstraints({ advanced: [{ torch: !!on }] });
    } catch {
        // Torch not supported on this camera.
    }
}

export async function stop() {
    if (!session) return;
    clearInterval(session.timer);
    session.stream.getTracks().forEach(t => t.stop());
    session.video.srcObject = null;
    session = null;
}
