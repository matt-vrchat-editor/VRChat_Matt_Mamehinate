const sharp = require('sharp');
const path = require('path');
const fs = require('fs');

const texDir = path.resolve(__dirname, '..');
const srcPath = path.join(texDir, 'Kurokuma_Color.png');
const outPath = path.join(texDir, 'Kurokuma_Color_custom.png');
const HSVG = { h: 0.351, s: 1.75, v: 0.62, g: 1 };

// Target grey from reference (~87-110); aim light grey "cinzinha claro"
const TARGET_GRAY = 118;
// Compensate lilToon value multiply (hsvg.v = 0.62)
const SRC_GRAY = Math.min(255, Math.round(TARGET_GRAY / HSVG.v)); // ~190

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

// Light mint green after HSVG (shoe panels) — NOT the saturated neon sole
function isLightGreen(nr, ng, nb) {
  const [h, s, v] = hsv(nr, ng, nb);
  if (!(h >= 70 && h <= 160 && v >= 0.20)) return false;
  // pastel / washed green
  return s < 0.42 || (s < 0.50 && v >= 0.55);
}

async function main() {
  console.log({ TARGET_GRAY, SRC_GRAY });
  const { data, info } = await sharp(srcPath).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width, height } = info;
  const out = Buffer.from(data);

  // Shoe upper panel UV regions (exclude sole circle area higher up)
  const shoeBoxes = [
    { x0: 0, y0: 3350, x1: 820, y1: 4095 },
    { x0: 820, y0: 3350, x1: 1720, y1: 4095 },
  ];
  const inBox = (x, y, b) => x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1;

  let changed = 0;
  for (let y = 3350; y < height; y++) {
    for (let x = 0; x < 1720; x++) {
      let inShoe = false;
      for (const b of shoeBoxes) if (inBox(x, y, b)) { inShoe = true; break; }
      if (!inShoe) continue;

      const i = (y * width + x) * 4;
      const r = data[i], g = data[i + 1], b = data[i + 2], a = data[i + 3];
      if (!isSrcPink(r, g, b, a)) continue;

      const [nr, ng, nb] = lil(r, g, b);
      if (!isLightGreen(nr, ng, nb)) continue;

      // Preserve subtle shading from original luminance
      const lum = Math.max(r, g, b) / 255;
      const shade = Math.min(255, Math.max(40, Math.round(SRC_GRAY * (0.75 + lum * 0.35))));
      out[i] = out[i + 1] = out[i + 2] = shade;
      changed++;
    }
  }
  console.log({ changed, SRC_GRAY });

  await sharp(out, { raw: { width, height, channels: 4 } }).png().toFile(outPath);

  // Preview shoe region with HSVG simulated
  const preview = await sharp(outPath).extract({ left: 0, top: 3000, width: 1700, height: 1096 }).ensureAlpha().raw()
    .toBuffer({ resolveWithObject: true });
  const baked = Buffer.alloc(preview.data.length);
  for (let i = 0; i < preview.data.length; i += 4) {
    const [nr, ng, nb] = lil(preview.data[i], preview.data[i + 1], preview.data[i + 2]);
    baked[i] = Math.round(nr); baked[i + 1] = Math.round(ng); baked[i + 2] = Math.round(nb);
    baked[i + 3] = preview.data[i + 3];
  }
  await sharp(baked, { raw: { width: preview.info.width, height: preview.info.height, channels: 4 } })
    .resize(900).png().toFile(path.join(__dirname, 'preview_shoe_gray.png'));

  // meta
  const metaPath = outPath + '.meta';
  if (!fs.existsSync(metaPath)) {
    const srcMeta = fs.readFileSync(srcPath + '.meta', 'utf8');
    const guid = [...Array(32)].map(() => Math.floor(Math.random() * 16).toString(16)).join('');
    fs.writeFileSync(metaPath, srcMeta.replace(/guid: .*/, `guid: ${guid}`));
    console.log('meta guid', guid);
  } else {
    const m = fs.readFileSync(metaPath, 'utf8');
    const g = m.match(/guid: ([a-f0-9]+)/);
    console.log('existing guid', g && g[1]);
  }
  console.log('done', (fs.statSync(outPath).size / 1048576).toFixed(2) + 'MB');
}

main().catch((e) => { console.error(e); process.exit(1); });
