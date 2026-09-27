// Generates assets/app.ico (256x256 PNG-compressed ICO) without any external dependency.
// Usage: node tools/make-icon.mjs
import { deflateSync } from "node:zlib";
import { writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const SIZE = 256;
const SS = 2; // supersampling factor for smooth edges

const TOP = [96, 171, 240];
const BOTTOM = [43, 108, 191];

function insideArrow(x, y) {
  // shaft
  if (x >= 107 && x <= 149 && y >= 51 && y <= 123) return true;
  // head: top edge y=123 spanning 72..184, apex at (128, 199)
  if (y >= 123 && y <= 199) {
    const t = (y - 123) / (199 - 123);
    if (Math.abs(x - 128) <= 56 * (1 - t)) return true;
  }
  return false;
}

const raw = Buffer.alloc(SIZE * SIZE * 4);
let p = 0;
for (let y = 0; y < SIZE; y++) {
  for (let x = 0; x < SIZE; x++) {
    let r = 0, g = 0, b = 0, a = 0, n = 0;
    for (let sy = 0; sy < SS; sy++) {
      for (let sx = 0; sx < SS; sx++) {
        const px = x + (sx + 0.5) / SS;
        const py = y + (sy + 0.5) / SS;
        n++;
        const d = Math.hypot(px - 127.5, py - 127.5);
        if (d <= 122) {
          const t = py / 255;
          let cr = TOP[0] + (BOTTOM[0] - TOP[0]) * t;
          let cg = TOP[1] + (BOTTOM[1] - TOP[1]) * t;
          let cb = TOP[2] + (BOTTOM[2] - TOP[2]) * t;
          if (d > 116) { cr *= 0.9; cg *= 0.9; cb *= 0.9; } // subtle rim
          if (py < 34 && d < 100) { cr += 18; cg += 18; cb += 18; } // soft top highlight
          if (insideArrow(px, py)) { cr = 255; cg = 255; cb = 255; }
          r += cr; g += cg; b += cb; a += 255;
        }
      }
    }
    raw[p++] = Math.round(r / n);
    raw[p++] = Math.round(g / n);
    raw[p++] = Math.round(b / n);
    raw[p++] = Math.round(a / n);
  }
}

let crcTable;
function crc32(buf) {
  if (!crcTable) {
    crcTable = new Int32Array(256);
    for (let n = 0; n < 256; n++) {
      let c = n;
      for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
      crcTable[n] = c;
    }
  }
  let c = -1;
  for (let i = 0; i < buf.length; i++) c = crcTable[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ -1) >>> 0;
}

function chunk(type, data) {
  const out = Buffer.alloc(8 + data.length + 4);
  out.writeUInt32BE(data.length, 0);
  out.write(type, 4, "ascii");
  data.copy(out, 8);
  out.writeUInt32BE(crc32(Buffer.concat([Buffer.from(type, "ascii"), data])), 8 + data.length);
  return out;
}

const ihdr = Buffer.alloc(13);
ihdr.writeUInt32BE(SIZE, 0);
ihdr.writeUInt32BE(SIZE, 4);
ihdr[8] = 8;  // bit depth
ihdr[9] = 6;  // RGBA
const stride = SIZE * 4;
const scan = Buffer.alloc((stride + 1) * SIZE);
for (let y = 0; y < SIZE; y++) {
  scan[y * (stride + 1)] = 0; // filter: none
  raw.copy(scan, y * (stride + 1) + 1, y * stride, (y + 1) * stride);
}

const png = Buffer.concat([
  Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
  chunk("IHDR", ihdr),
  chunk("IDAT", deflateSync(scan, { level: 9 })),
  chunk("IEND", Buffer.alloc(0)),
]);

// ICO container with a single PNG-compressed image (valid on Windows Vista+).
const ico = Buffer.alloc(22 + png.length);
ico.writeUInt16LE(0, 0);
ico.writeUInt16LE(1, 2); // icon
ico.writeUInt16LE(1, 4); // 1 image
ico[6] = 0;  // width 256 => 0
ico[7] = 0;  // height 256 => 0
ico.writeUInt16LE(1, 10);  // planes
ico.writeUInt16LE(32, 12); // bpp
ico.writeUInt32LE(png.length, 14);
ico.writeUInt32LE(22, 18);
png.copy(ico, 22);

const assetsDir = join(here, "..", "assets");
mkdirSync(assetsDir, { recursive: true });
writeFileSync(join(assetsDir, "app.ico"), ico);
console.log("assets/app.ico written:", ico.length, "bytes");
