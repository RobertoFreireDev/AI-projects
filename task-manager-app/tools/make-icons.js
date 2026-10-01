/* ============================================================
   make-icons.js — one-time build step (CLAUDE.md §13.5).

   Generates pwa/public/icon-192.png and pwa/public/icon-512.png from
   the app's own glyph (the `task` checkmark) on the dark background
   from §8. `public/` is copied verbatim into the Angular build output,
   so the icons ship next to index.html without a bundler step.

   Zero dependencies: pure Node (node:zlib for the PNG DEFLATE
   stream). No canvas, no image library, no network, no external
   icon-generator service. Run once:

       node tools/make-icons.js

   Not used at runtime — the app itself never generates images.
   ============================================================ */
'use strict';

const zlib = require('node:zlib');
const fs = require('node:fs');
const path = require('node:path');

/* --- palette (mirrors index.html :root, CLAUDE.md §8) --------- */
const BG = [0x0e, 0x10, 0x14];   // --bg
const FG = [0x7a, 0xa2, 0xff];   // --accent

/* --- geometry -------------------------------------------------
   Unit coordinates (0..1 of the icon square). The whole glyph
   lives inside the central 20%..80% band, which is the maskable
   safe zone, so the same file works for `any` and `maskable`.
   --------------------------------------------------------------- */
const CHECK = [
  [0.285, 0.520],
  [0.435, 0.670],
  [0.715, 0.345],
];
const STROKE = 0.088;   // stroke width, in unit coordinates

/* --- tiny rasteriser ------------------------------------------
   Antialiased stroked polyline by signed distance: for each pixel
   take the distance to the nearest segment and feather it across
   one pixel around the stroke's half-width.
   --------------------------------------------------------------- */
function distToSegment(px, py, ax, ay, bx, by) {
  const dx = bx - ax, dy = by - ay;
  const len2 = dx * dx + dy * dy;
  let t = len2 === 0 ? 0 : ((px - ax) * dx + (py - ay) * dy) / len2;
  t = t < 0 ? 0 : t > 1 ? 1 : t;             // round caps + joins
  const cx = ax + t * dx, cy = ay + t * dy;
  return Math.hypot(px - cx, py - cy);
}

function coverage(px, py, half, feather) {
  let d = Infinity;
  for (let i = 0; i < CHECK.length - 1; i++) {
    const a = CHECK[i], b = CHECK[i + 1];
    d = Math.min(d, distToSegment(px, py, a[0], a[1], b[0], b[1]));
  }
  if (d <= half - feather) return 1;
  if (d >= half + feather) return 0;
  return (half + feather - d) / (2 * feather);
}

function renderRGBA(size) {
  const half = STROKE / 2;
  const feather = 0.7 / size;             // ~sub-pixel edge softening
  const px = Buffer.alloc(size * size * 4);
  for (let y = 0; y < size; y++) {
    const uy = (y + 0.5) / size;
    for (let x = 0; x < size; x++) {
      const ux = (x + 0.5) / size;
      const a = coverage(ux, uy, half, feather);
      const o = (y * size + x) * 4;
      px[o]     = Math.round(BG[0] + (FG[0] - BG[0]) * a);
      px[o + 1] = Math.round(BG[1] + (FG[1] - BG[1]) * a);
      px[o + 2] = Math.round(BG[2] + (FG[2] - BG[2]) * a);
      px[o + 3] = 255;                    // fully opaque square
    }
  }
  return px;
}

/* --- minimal PNG encoder (8-bit RGBA, no interlace) ------------ */
const CRC_TABLE = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c;
  }
  return t;
})();

function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = CRC_TABLE[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length, 0);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body), 0);
  return Buffer.concat([len, body, crc]);
}

function encodePNG(size, rgba) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8;    // bit depth
  ihdr[9] = 6;    // colour type: RGBA
  ihdr[10] = 0;   // deflate
  ihdr[11] = 0;   // adaptive filtering
  ihdr[12] = 0;   // no interlace

  // One filter byte (0 = None) per scanline, then the raw row.
  const stride = size * 4;
  const raw = Buffer.alloc((stride + 1) * size);
  for (let y = 0; y < size; y++) {
    raw[y * (stride + 1)] = 0;
    rgba.copy(raw, y * (stride + 1) + 1, y * stride, (y + 1) * stride);
  }

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

/* --- write ----------------------------------------------------- */
const outDir = path.join(__dirname, '..', 'pwa', 'public');
fs.mkdirSync(outDir, { recursive: true });

for (const size of [192, 512]) {
  const file = path.join(outDir, `icon-${size}.png`);
  const png = encodePNG(size, renderRGBA(size));
  fs.writeFileSync(file, png);
  console.log(`wrote ${path.relative(path.join(__dirname, '..'), file)} (${size}x${size}, ${png.length} bytes)`);
}
