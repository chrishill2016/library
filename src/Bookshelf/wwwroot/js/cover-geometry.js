// Pure maths for cropping a book cover out of a photo: a best-guess outline and a perspective warp.
const OUTER_SHARE = 0.8;   // an outline this close to the best counts as convincing

// Corners are [topLeft, topRight, bottomRight, bottomLeft], each { x, y } in pixels.

/**
 * Guesses the book's outline in an RGBA image (ideally ~256px on the long side), or null when unsure.
 * Finds a few strong, near-straight edge candidates on each side of the centre, then picks the four
 * that make the most convincing closed outline: strong edges all the way round between its corners.
 */
export function detectCover(data, w, h) {
    if (w < 32 || h < 32) return null;
    const { gx, gy } = gradients(data, w, h);
    const lefts = candidates(gx, w, h, 'v', 0.02, 0.48, h * 0.25, h * 0.75);
    const rights = candidates(gx, w, h, 'v', 0.52, 0.98, h * 0.25, h * 0.75);
    const tops = candidates(gy, h, w, 'h', 0.02, 0.48, w * 0.25, w * 0.75);
    const bottoms = candidates(gy, h, w, 'h', 0.52, 0.98, w * 0.25, w * 0.75);

    const score = sides => {
        const corners = [cross(sides[0], sides[2]), cross(sides[1], sides[2]), cross(sides[1], sides[3]), cross(sides[0], sides[3])];
        if (!plausible(corners, w, h)) return null;
        const strengths = [
            edgeStrength(gy, w, h, corners[0], corners[1], 'h'),
            edgeStrength(gx, w, h, corners[1], corners[2], 'v'),
            edgeStrength(gy, w, h, corners[3], corners[2], 'h'),
            edgeStrength(gx, w, h, corners[0], corners[3], 'v'),
        ];
        // The weakest side matters most: clutter rarely lines up on all four sides.
        return { corners, value: Math.min(...strengths) * 2 + strengths.reduce((x, v) => x + v, 0) / 4 };
    };

    let best = null;
    for (const l of lefts) for (const r of rights) for (const t of tops) for (const b of bottoms) {
        const s = score([l, r, t, b]);
        if (s && (!best || s.value > best.value)) best = { ...s, sides: [l, r, t, b] };
    }
    if (!best) return null;

    // Covers often have a strong band inside (a title strip). Move each side outwards to the outermost
    // candidate that still gives a convincing outline.
    const sets = [lefts, rights, tops, bottoms];
    const outward = [-1, 1, -1, 1];
    let { sides } = best;
    for (let i = 0; i < 4; i++) {
        for (const line of sets[i]) {
            if ((line.c - sides[i].c) * outward[i] <= 0) continue;
            const trial = sides.map((x, j) => j === i ? line : x);
            const s = score(trial);
            if (s && s.value >= best.value * OUTER_SHARE) sides = trial;
        }
    }
    return score(sides).corners.map(p => ({ x: clamp(p.x, 0, w), y: clamp(p.y, 0, h) }));
}

/** Inside the photo, a decent share of it, convex, and roughly book-shaped. */
function plausible(c, w, h) {
    if (c.some(p => !p || p.x < -2 || p.y < -2 || p.x > w + 2 || p.y > h + 2)) return false;
    if (polygonArea(c) < w * h * 0.12) return false;
    for (let i = 0; i < 4; i++) {
        const [a, b, d] = [c[i], c[(i + 1) % 4], c[(i + 2) % 4]];
        if ((b.x - a.x) * (d.y - b.y) - (b.y - a.y) * (d.x - b.x) <= 0) return false;
    }
    const { width, height } = outputSize(c, Infinity);
    return width / height > 0.35 && width / height < 1.4;
}

/** Mean edge strength along the segment from p to q (one pixel of slack either side). */
function edgeStrength(g, w, h, p, q, dir) {
    const n = Math.max(8, Math.round(dist(p, q)));
    let sum = 0;
    for (let i = 0; i <= n; i++) {
        const x = Math.round(lerp(p.x, q.x, i / n)), y = Math.round(lerp(p.y, q.y, i / n));
        let m = 0;
        for (let k = -1; k <= 1; k++) {
            const xx = dir === 'v' ? x + k : x, yy = dir === 'h' ? y + k : y;
            if (xx >= 1 && yy >= 1 && xx < w - 1 && yy < h - 1) m = Math.max(m, g[yy * w + xx]);
        }
        sum += m;
    }
    return sum / (n + 1);
}

/** Sobel gradient strength summed over R, G and B: gx finds vertical edges, gy horizontal ones. */
function gradients(data, w, h) {
    const gx = new Float32Array(w * h), gy = new Float32Array(w * h);
    for (let y = 1; y < h - 1; y++) {
        for (let x = 1; x < w - 1; x++) {
            let sx = 0, sy = 0;
            for (let c = 0; c < 3; c++) {
                const p = (dx, dy) => data[((y + dy) * w + x + dx) * 4 + c];
                const vx = p(1, -1) + 2 * p(1, 0) + p(1, 1) - p(-1, -1) - 2 * p(-1, 0) - p(-1, 1);
                const vy = p(-1, 1) + 2 * p(0, 1) + p(1, 1) - p(-1, -1) - 2 * p(0, -1) - p(1, -1);
                sx += Math.abs(vx);
                sy += Math.abs(vy);
            }
            gx[y * w + x] = sx;
            gy[y * w + x] = sy;
        }
    }
    return { gx, gy };
}

/**
 * Up to `count` strong near-straight edges in a band. For 'v' lines x = c + s·(y − mid); for 'h' lines
 * y = c + s·(x − mid). 'across' is the axis the position varies along, 'along' the axis the line runs along.
 * Each is scored by the mean gradient on the line between `from` and `to`; nearby duplicates are dropped.
 */
function candidates(g, across, along, dir, lo, hi, from, to, count = 5) {
    const w = dir === 'v' ? across : along;
    const mid = along / 2;
    const a = Math.max(1, Math.floor(from)), b = Math.min(along - 2, Math.ceil(to));
    const lines = [];
    for (let c = Math.round(across * lo); c <= Math.round(across * hi); c++) {
        let best = { score: 0, c, s: 0 };
        for (let s = -0.2; s <= 0.2001; s += 0.02) {
            let sum = 0, n = 0;
            for (let t = a; t <= b; t++) {
                const u = Math.round(c + s * (t - mid));
                if (u < 1 || u > across - 2) continue;
                const at = k => dir === 'v' ? g[t * w + k] : g[k * w + t];
                sum += Math.max(at(u - 1), at(u), at(u + 1));
                n++;
            }
            const score = n > (b - a) * 0.8 ? sum / n : 0;
            if (score > best.score) best = { score, c, s };
        }
        if (best.score > 0) lines.push(best);
    }
    lines.sort((x, y) => y.score - x.score);
    const picked = [];
    for (const l of lines) {
        if (picked.length === count) break;
        if (picked.every(p => Math.abs(p.c - l.c) > across * 0.03)) picked.push(l);
    }
    return picked.map(l => ({ dir, c: l.c, s: l.s, mid }));
}

/** Where a vertical-ish line meets a horizontal-ish one. */
function cross(v, hz) {
    // x = v.c + v.s (y − v.mid);  y = hz.c + hz.s (x − hz.mid)
    const denom = 1 - v.s * hz.s;
    if (Math.abs(denom) < 1e-6) return null;
    const x = (v.c + v.s * (hz.c - hz.s * hz.mid - v.mid)) / denom;
    return { x, y: hz.c + hz.s * (x - hz.mid) };
}

function polygonArea(p) {
    let a = 0;
    for (let i = 0; i < p.length; i++) {
        const q = p[(i + 1) % p.length];
        a += p[i].x * q.y - q.x * p[i].y;
    }
    return Math.abs(a) / 2;
}

const lerp = (a, b, t) => a + (b - a) * t;
const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));
const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

/** Flat size for the outlined cover (average of opposite sides), scaled so the longest side ≤ maxSide. */
export function outputSize(corners, maxSide) {
    const [tl, tr, br, bl] = corners;
    const width = (dist(tl, tr) + dist(bl, br)) / 2;
    const height = (dist(tl, bl) + dist(tr, br)) / 2;
    const scale = Math.min(1, maxSide / Math.max(width, height));
    return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) };
}

/** Maps the unit square onto the quad (Heckbert): returns (u, v) → { x, y }. */
export function squareToQuad(corners) {
    const [{ x: x0, y: y0 }, { x: x1, y: y1 }, { x: x2, y: y2 }, { x: x3, y: y3 }] = corners;
    const dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
    const dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;
    let g = 0, h = 0;
    if (Math.abs(dx3) > 1e-9 || Math.abs(dy3) > 1e-9) {
        const det = dx1 * dy2 - dx2 * dy1;
        g = (dx3 * dy2 - dx2 * dy3) / det;
        h = (dx1 * dy3 - dx3 * dy1) / det;
    }
    const a = x1 - x0 + g * x1, b = x3 - x0 + h * x3, c = x0;
    const d = y1 - y0 + g * y1, e = y3 - y0 + h * y3, f = y0;
    return (u, v) => {
        const z = g * u + h * v + 1;
        return { x: (a * u + b * v + c) / z, y: (d * u + e * v + f) / z };
    };
}

/** Perspective-corrects the quad in `src` (RGBA, sw × sh) into a new dw × dh RGBA buffer (bilinear). */
export function warp(src, sw, sh, corners, dw, dh) {
    const out = new Uint8ClampedArray(dw * dh * 4);
    const map = squareToQuad(corners);
    for (let y = 0; y < dh; y++) {
        for (let x = 0; x < dw; x++) {
            const p = map((x + 0.5) / dw, (y + 0.5) / dh);
            const fx = clamp(p.x - 0.5, 0, sw - 1), fy = clamp(p.y - 0.5, 0, sh - 1);
            const x0 = Math.floor(fx), y0 = Math.floor(fy);
            const x1 = Math.min(x0 + 1, sw - 1), y1 = Math.min(y0 + 1, sh - 1);
            const ax = fx - x0, ay = fy - y0;
            const o = (y * dw + x) * 4;
            for (let c = 0; c < 4; c++) {
                const top = src[(y0 * sw + x0) * 4 + c] * (1 - ax) + src[(y0 * sw + x1) * 4 + c] * ax;
                const bot = src[(y1 * sw + x0) * 4 + c] * (1 - ax) + src[(y1 * sw + x1) * 4 + c] * ax;
                out[o + c] = top * (1 - ay) + bot * ay;
            }
        }
    }
    return out;
}
