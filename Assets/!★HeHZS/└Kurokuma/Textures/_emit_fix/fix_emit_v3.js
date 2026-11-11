const sharp = require('sharp');
const path = require('path');
const fs = require('fs');

const texDir = path.resolve(__dirname, '..');
const colorPath = path.join(texDir, 'Kurokuma_Color.png');
const emitPath = path.join(texDir, 'Kurokuma_emission_mask.png');
const bakPath = emitPath + '.bak_before_fix.png';
const HSVG = { h: 0.351, s: 1.75, v: 0.62, g: 1 };

function lil(r, g, b) {
  const c = [r / 255, g / 255, b / 255];
  const cr = c[0], cg = c[1], cb = c[2];
  const p = cb > cg ? [cb, cg, -1.0, 2.0 / 3.0] : [cg, cb, 0.0, -1.0 / 3.0];
  const q = p[0] > cr ? [p[0], p[1], p[3], cr] : [cr, p[1], p[2], p[0]];
  const d = q[0] - Math.min(q[3], q[1]);
  const e = 1e-10;
  let hx = Math.abs(q[2] + (q[3] - q[1]) / (6 * d + e)) + HSVG.h;
  let sx = Math.min(1, Math.max(0, (d / (q[0] + e)) * HSVG.s));
  let vx = Math.min(1, Math.max(0, q[0] * HSVG.v));
  return [0, 0, 0].map((_, i) => {
    const t = hx + [1, 2 / 3, 1 / 3][i];
    const frac = t - Math.floor(t);
    return (vx - vx * sx + vx * sx * Math.min(1, Math.max(0, Math.abs(frac * 6 - 3) - 1))) * 255;
  });
}

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

function isSrcPink(r, g, b, a) {
  if (a < 8) return false;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  const sat = max ? (max - min) / max : 0;
  return r > 120 && r > g + 15 && b > g - 5 && sat > 0.12;
}

async function main() {
  if (fs.existsSync(bakPath)) {
    fs.copyFileSync(bakPath, emitPath);
    console.log('restored bak');
  }

  const color = await sharp(colorPath).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width, height, data } = { width: color.info.width, height: color.info.height, data: color.data };
  const n = width * height;
  const BORDER = 48;

  console.log('build mask');
  const out = Buffer.alloc(n * 4);
  let allGreen = 0, kept = 0, shoeDark = 0, shoeSkip = 0, borderSkip = 0;

  // Shoe UV zones — only dark green recesses get AL
  const shoeBoxes = [
    { x0: 0, y0: 3350, x1: 820, y1: 4095 },
    { x0: 820, y0: 3350, x1: 1720, y1: 4095 },
  ];
  const soleCircleBox = { x0: 0, y0: 2420, x1: 900, y1: 2920 };
  const inBox = (x, y, b) => x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1;

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const p = y * width + x;
      const i = p * 4;
      out[i + 3] = 255;

      if (!isSrcPink(data[i], data[i + 1], data[i + 2], data[i + 3])) {
        out[i] = out[i + 1] = out[i + 2] = 0;
        continue;
      }

      const [nr, ng, nb] = lil(data[i], data[i + 1], data[i + 2]);
      const [h, s, v] = hsv(nr, ng, nb);
      const isGreen = s >= 0.38 && v >= 0.12 && h >= 85 && h <= 150;
      if (!isGreen) {
        out[i] = out[i + 1] = out[i + 2] = 0;
        continue;
      }
      allGreen++;

      // Skip atlas padding near texture border
      const nearBorder = x < BORDER || y < BORDER || x >= width - BORDER || y >= height - BORDER;
      if (nearBorder) {
        borderSkip++;
        out[i] = out[i + 1] = out[i + 2] = 0;
        continue;
      }

      let inShoe = false;
      for (const b of shoeBoxes) if (inBox(x, y, b)) { inShoe = true; break; }
      const inSole = inBox(x, y, soleCircleBox);

      if (inShoe || inSole) {
        // Only dark green recesses on sole (screenshot: darker midsole blocks)
        // Bright sole fill has higher V after HSVG → skip
        if (s >= 0.52 && v <= 0.48) {
          out[i] = out[i + 1] = out[i + 2] = 255;
          kept++; shoeDark++;
        } else {
          out[i] = out[i + 1] = out[i + 2] = 0;
          shoeSkip++;
        }
      } else {
        // All other green outfit details
        out[i] = out[i + 1] = out[i + 2] = 255;
        kept++;
      }
    }
  }

  // 1px dilate outside shoe-light
  const dil = Buffer.from(out);
  for (let y = 1; y < height - 1; y++) {
    for (let x = 1; x < width - 1; x++) {
      const p = y * width + x;
      const i = p * 4;
      if (out[i] > 128) continue;
      if (!isSrcPink(data[i], data[i + 1], data[i + 2], data[i + 3])) continue;
      const [nr, ng, nb] = lil(data[i], data[i + 1], data[i + 2]);
      const [h, s, v] = hsv(nr, ng, nb);
      if (!(s >= 0.38 && v >= 0.12 && h >= 85 && h <= 150)) continue;
      let inShoe = false;
      for (const b of shoeBoxes) if (inBox(x, y, b)) { inShoe = true; break; }
      if (inShoe || inBox(x, y, soleCircleBox)) {
        if (!(s >= 0.52 && v <= 0.48)) continue;
      }
      let near = false;
      for (let dy = -1; dy <= 1 && !near; dy++)
        for (let dx = -1; dx <= 1; dx++)
          if (out[((y + dy) * width + x + dx) * 4] > 128) { near = true; break; }
      if (near) dil[i] = dil[i + 1] = dil[i + 2] = 255;
    }
  }

  console.log({ allGreen, kept, shoeDark, shoeSkip, borderSkip });

  if (!fs.existsSync(bakPath)) fs.copyFileSync(emitPath, bakPath);
  await sharp(dil, { raw: { width, height, channels: 4 } }).png().toFile(emitPath);

  await sharp(emitPath).resize(1024).png().toFile(path.join(__dirname, 'preview_emit.png'));
  const small = await sharp(colorPath).resize(1024).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const emSmall = await sharp(emitPath).resize(1024, null, { kernel: 'nearest' }).ensureAlpha().raw().toBuffer();
  const ov = Buffer.alloc(small.data.length);
  for (let i = 0; i < small.data.length; i += 4) {
    const [nr, ng, nb] = lil(small.data[i], small.data[i + 1], small.data[i + 2]);
    ov[i] = Math.round(nr); ov[i + 1] = Math.round(ng); ov[i + 2] = Math.round(nb); ov[i + 3] = small.data[i + 3];
    if (emSmall[i] > 40) {
      ov[i] = Math.floor(ov[i] * 0.2);
      ov[i + 1] = Math.min(255, Math.floor(ov[i + 1] * 0.2 + 220));
      ov[i + 2] = Math.min(255, Math.floor(ov[i + 2] * 0.2 + 160));
    }
  }
  await sharp(ov, { raw: { width: 1024, height: 1024, channels: 4 } }).png()
    .toFile(path.join(__dirname, 'preview_overlay.png'));
  await sharp(emitPath).extract({ left: 0, top: 2800, width: 1700, height: 1296 }).resize(900).png()
    .toFile(path.join(__dirname, 'preview_shoe_emit.png'));
  await sharp(emitPath).extract({ left: 1800, top: 1000, width: 1800, height: 1600 }).resize(900).png()
    .toFile(path.join(__dirname, 'preview_paws_emit.png'));

  function cov(name, x0, y0, x1, y1) {
    let g = 0, ge = 0;
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const i = (y * width + x) * 4;
      if (!isSrcPink(data[i], data[i + 1], data[i + 2], data[i + 3])) continue;
      const [nr, ng, nb] = lil(data[i], data[i + 1], data[i + 2]);
      const [h, s, v] = hsv(nr, ng, nb);
      if (!(s >= 0.38 && v >= 0.12 && h >= 85 && h <= 150)) continue;
      g++;
      if (dil[i] > 40) ge++;
    }
    console.log(name, { g, ge, pct: g ? (100 * ge / g).toFixed(1) + '%' : 'n/a' });
  }
  cov('hem', 50, 2950, 2800, 3350);
  cov('shoeL', 50, 3400, 800, 4090);
  cov('paws', 1800, 1100, 3600, 2100);
  cov('soles', 50, 2450, 850, 2850);
  cov('ribbons_area', 50, 2800, 1700, 3200);
  console.log('done kb', Math.round(fs.statSync(emitPath).size / 1024));
}

main().catch((e) => { console.error(e); process.exit(1); });
