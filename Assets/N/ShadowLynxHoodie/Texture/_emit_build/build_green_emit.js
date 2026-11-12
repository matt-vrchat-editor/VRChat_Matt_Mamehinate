const sharp = require('sharp');
const path = require('path');
const fs = require('fs');

const texDir = path.resolve(__dirname, '..');
const outDir = __dirname;

const jobs = [
  {
    base: 'ParkaTex_Base_color_custom1.png',
    emit: 'ParkaTex_Emission_custom1.png',
    preview: 'preview_parka1.png',
  },
  {
    base: 'ParkaTex_Base_color_custom2.png',
    emit: 'ParkaTex_Emission_custom2.png',
    preview: 'preview_parka2.png',
  },
  {
    base: 'ShoesTex_Base_color_custom1.png',
    emit: 'ShoesTex_Emission_custom1.png',
    preview: 'preview_shoes1.png',
  },
  {
    base: 'ShoesTex_Base_color_custom2.png',
    emit: 'ShoesTex_Emission_custom2.png',
    preview: 'preview_shoes2.png',
  },
];

function hsv(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min;
  let h = 0;
  if (d) {
    if (max === r) h = ((g - b) / d) % 6;
    else if (max === g) h = (b - r) / d + 2;
    else h = (r - g) / d + 4;
    h /= 6; if (h < 0) h += 1;
  }
  return [h * 360, max ? d / max : 0, max];
}

/** Neon / accent green used on ShadowLynx details (zippers, laces, sole accents). */
function isAccentGreen(r, g, b, a) {
  if (a < 8) return false;
  const [h, s, v] = hsv(r, g, b);
  // lime / neon green band; keep bright-mid accents, skip muddy dark fabric greens
  if (h < 70 || h > 165) return false;
  if (s < 0.28) return false;
  if (v < 0.18) return false;
  // G must dominate (avoid yellow-ish / cyan-ish leftovers)
  if (!(g > r + 12 && g > b + 8)) return false;
  return true;
}

async function buildOne(job) {
  const basePath = path.join(texDir, job.base);
  const emitPath = path.join(texDir, job.emit);
  const bakPath = emitPath + '.bak_before_green.png';

  const { data, info } = await sharp(basePath).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width, height } = info;
  const n = width * height;
  const out = Buffer.alloc(n * 4);
  let onN = 0;

  for (let p = 0, i = 0; p < n; p++, i += 4) {
    const on = isAccentGreen(data[i], data[i + 1], data[i + 2], data[i + 3]);
    const v = on ? 255 : 0;
    if (on) onN++;
    out[i] = out[i + 1] = out[i + 2] = v;
    out[i + 3] = 255;
  }

  // 1px dilate so thin zipper/lace lines don't disappear after mipmaps
  const dil = Buffer.from(out);
  for (let y = 1; y < height - 1; y++) {
    for (let x = 1; x < width - 1; x++) {
      const i = (y * width + x) * 4;
      if (out[i] > 128) continue;
      if (!isAccentGreen(data[i], data[i + 1], data[i + 2], data[i + 3])) continue;
      let near = false;
      for (let dy = -1; dy <= 1 && !near; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          if (out[((y + dy) * width + (x + dx)) * 4] > 128) { near = true; break; }
        }
      }
      if (near) dil[i] = dil[i + 1] = dil[i + 2] = 255;
    }
  }

  if (fs.existsSync(emitPath) && !fs.existsSync(bakPath)) {
    fs.copyFileSync(emitPath, bakPath);
  }

  await sharp(dil, { raw: { width, height, channels: 4 } }).png().toFile(emitPath);

  // overlay preview
  const overlay = Buffer.alloc(n * 4);
  for (let i = 0; i < n * 4; i += 4) {
    overlay[i] = data[i];
    overlay[i + 1] = data[i + 1];
    overlay[i + 2] = data[i + 2];
    overlay[i + 3] = data[i + 3];
    if (dil[i] > 40) {
      overlay[i] = Math.floor(overlay[i] * 0.15);
      overlay[i + 1] = Math.min(255, Math.floor(overlay[i + 1] * 0.15 + 230));
      overlay[i + 2] = Math.min(255, Math.floor(overlay[i + 2] * 0.15 + 80));
    }
  }
  await sharp(overlay, { raw: { width, height, channels: 4 } })
    .resize(1024)
    .png()
    .toFile(path.join(outDir, job.preview));

  console.log(job.emit, { onN, pct: +(100 * onN / n).toFixed(2), w: width, h: height });
}

async function main() {
  for (const job of jobs) await buildOne(job);
  console.log('done');
}

main().catch((e) => { console.error(e); process.exit(1); });
