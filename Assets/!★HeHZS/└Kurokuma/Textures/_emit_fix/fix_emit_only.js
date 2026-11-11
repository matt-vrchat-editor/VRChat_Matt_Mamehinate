const sharp = require('sharp');
const path = require('path');
const fs = require('fs');

const texDir = path.resolve(__dirname, '..');
const colorPath = path.join(texDir, 'Kurokuma_Color.png');
const emitPath = path.join(texDir, 'Kurokuma_emission_mask.png');
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
  console.log('load');
  const color = await sharp(colorPath).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const emitIn = await sharp(emitPath).ensureAlpha().raw().toBuffer();
  const { width, height, data } = { width: color.info.width, height: color.info.height, data: color.data };
  const n = width * height;

  // Precompute pink + baked green stats + local variance
  console.log('analyze');
  const pink = new Uint8Array(n);
  const highVar = new Uint8Array(n);
  const greenS = new Float32Array(n); // sat after HSVG, -1 if not green
  const greenV = new Float32Array(n);

  for (let y = 1; y < height - 1; y++) {
    for (let x = 1; x < width - 1; x++) {
      const p = y * width + x;
      const i = p * 4;
      if (!isSrcPink(data[i], data[i + 1], data[i + 2], data[i + 3])) {
        greenS[p] = -1;
        continue;
      }
      pink[p] = 1;

      let sum = 0, sum2 = 0, c = 0;
      for (let dy = -2; dy <= 2; dy++) {
        for (let dx = -2; dx <= 2; dx++) {
          const j = ((y + dy) * width + (x + dx)) * 4;
          const lum = (data[j] + data[j + 1] + data[j + 2]) / 3;
          sum += lum; sum2 += lum * lum; c++;
        }
      }
      const mean = sum / c;
      const vr = sum2 / c - mean * mean;
      if (vr >= 25) highVar[p] = 1;

      const [nr, ng, nb] = lil(data[i], data[i + 1], data[i + 2]);
      const [h, s, v] = hsv(nr, ng, nb);
      if (s >= 0.35 && v >= 0.10 && h >= 80 && h <= 155) {
        greenS[p] = s;
        greenV[p] = v;
      } else {
        greenS[p] = -1;
      }
    }
  }

  // Seed = current emission
  const seed = new Uint8Array(n);
  let seedN = 0;
  for (let p = 0, i = 0; p < n; p++, i += 4) {
    if (emitIn[i] > 40) { seed[p] = 1; seedN++; }
  }
  console.log({ seedN });

  // Grow from seeds into pink+green pixels with distance limit.
  // Prefer high-variance (clothing) paths; allow limited steps into low-var to bridge gaps.
  const MAX_DIST = 220;
  const MAX_FLAT_STREAK = 12;
  const dist = new Int32Array(n);
  dist.fill(-1);
  const flatStreak = new Uint8Array(n);
  const q = new Int32Array(n);
  let qh = 0, qt = 0;

  for (let p = 0; p < n; p++) {
    if (!seed[p]) continue;
    dist[p] = 0;
    flatStreak[p] = 0;
    q[qt++] = p;
  }

  console.log('grow');
  while (qh < qt) {
    const p = q[qh++];
    const d0 = dist[p];
    if (d0 >= MAX_DIST) continue;
    const x = p % width, y = (p / width) | 0;
    const nbs = [p - 1, p + 1, p - width, p + width];
    const xs = [x - 1, x + 1, x, x];
    const ys = [y, y, y - 1, y + 1];
    for (let k = 0; k < 4; k++) {
      const nx = xs[k], ny = ys[k];
      if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
      const np = nbs[k];
      if (dist[np] !== -1) continue;
      if (!pink[np] || greenS[np] < 0) continue;

      let flat = flatStreak[p];
      if (!highVar[np]) {
        flat += 1;
        if (flat > MAX_FLAT_STREAK) continue; // don't flood into flat atlas bg
      } else {
        flat = 0;
      }

      dist[np] = d0 + 1;
      flatStreak[np] = flat;
      q[qt++] = np;
    }
  }

  // Also include high-variance green pink components that are reasonably sized
  // (catches isolated accents the seed grow might miss)
  console.log('extra components');
  const visited = new Uint8Array(n);
  const extra = new Uint8Array(n);
  const maxComp = Math.floor(n * 0.035);
  let extraN = 0;
  for (let start = 0; start < n; start++) {
    if (!pink[start] || greenS[start] < 0 || !highVar[start] || visited[start]) continue;
    const stack = [start];
    visited[start] = 1;
    const comp = [];
    let touchesBorder = false;
    let overlapSeedOrGrow = false;
    while (stack.length) {
      const idx = stack.pop();
      comp.push(idx);
      const x = idx % width, y = (idx / width) | 0;
      if (x <= 1 || y <= 1 || x >= width - 2 || y >= height - 2) touchesBorder = true;
      if (dist[idx] !== -1) overlapSeedOrGrow = true;
      for (const [nx, ny] of [[x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]]) {
        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
        const nidx = ny * width + nx;
        if (visited[nidx]) continue;
        // allow component through pink green; prefer highVar but allow short bridges
        if (!pink[nidx] || greenS[nidx] < 0) continue;
        if (!highVar[nidx] && !highVar[idx]) continue;
        visited[nidx] = 1;
        stack.push(nidx);
      }
    }
    if (comp.length > maxComp && touchesBorder && !overlapSeedOrGrow) continue;
    if (comp.length > maxComp && touchesBorder) continue;
    // Keep if overlaps grown region OR is a moderate clothing accent
    if (overlapSeedOrGrow || (comp.length >= 80 && comp.length <= maxComp && !touchesBorder)) {
      for (const idx of comp) {
        if (!extra[idx]) { extra[idx] = 1; extraN++; }
      }
    }
  }
  console.log({ extraN, grown: dist.reduce((a, d) => a + (d >= 0 ? 1 : 0), 0) });

  // Shoe zone: only dark green recesses for AL
  // From screenshot: sole body = bright/light green; recessed blocks = darker green
  // After HSVG: dark ≈ higher sat + lower/mid value; light sole ≈ can be high sat too but often higher V
  // Use shoe spatial boxes + value split among greens.
  const shoeBoxes = [
    { x0: 0, y0: 3350, x1: 820, y1: 4095 },
    { x0: 820, y0: 3350, x1: 1720, y1: 4095 },
  ];
  // Sole circles (higher up) — these were the tread/side circles; keep if dark-ish
  const soleCircleBox = { x0: 0, y0: 2420, x1: 900, y1: 2920 };

  function inBox(x, y, b) {
    return x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1;
  }

  const out = Buffer.alloc(n * 4);
  let keepN = 0, shoeDark = 0, shoeSkip = 0;
  for (let p = 0, i = 0; p < n; p++, i += 4) {
    const grown = dist[p] >= 0 || extra[p] || seed[p];
    let on = false;
    if (grown && greenS[p] >= 0) {
      const x = p % width, y = (p / width) | 0;
      let inShoe = false;
      for (const b of shoeBoxes) if (inBox(x, y, b)) { inShoe = true; break; }
      const inSoleCircle = inBox(x, y, soleCircleBox);

      if (inShoe || inSoleCircle) {
        // Dark green only: sat high and not the brightest mint/sole fill
        // Recessed blocks are darker (lower V). Sole fill is brighter.
        const s = greenS[p], v = greenV[p];
        const isDark = s >= 0.50 && v <= 0.52;
        if (isDark) { on = true; shoeDark++; }
        else shoeSkip++;
      } else {
        on = true;
      }
    }

    const v = on ? 255 : 0;
    if (on) keepN++;
    out[i] = out[i + 1] = out[i + 2] = v;
    out[i + 3] = 255;
  }
  console.log({ keepN, shoeDark, shoeSkip, seedN });

  // Small dilate for thin edges (1px), but not into shoe-light areas
  const dil = Buffer.from(out);
  for (let y = 1; y < height - 1; y++) {
    for (let x = 1; x < width - 1; x++) {
      const p = y * width + x;
      const i = p * 4;
      if (out[i] > 128) continue;
      if (greenS[p] < 0) continue;
      let inShoe = false;
      for (const b of shoeBoxes) if (inBox(x, y, b)) { inShoe = true; break; }
      if (inShoe || inBox(x, y, soleCircleBox)) {
        if (!(greenS[p] >= 0.50 && greenV[p] <= 0.52)) continue;
      }
      let near = false;
      for (let dy = -1; dy <= 1 && !near; dy++)
        for (let dx = -1; dx <= 1; dx++)
          if (out[((y + dy) * width + x + dx) * 4] > 128) { near = true; break; }
      if (near) dil[i] = dil[i + 1] = dil[i + 2] = 255;
    }
  }

  // Backup original emit once
  const bak = emitPath + '.bak_before_fix.png';
  if (!fs.existsSync(bak)) fs.copyFileSync(emitPath, bak);

  await sharp(dil, { raw: { width, height, channels: 4 } }).png().toFile(emitPath);
  await sharp(emitPath).resize(1024).png().toFile(path.join(__dirname, 'preview_emit.png'));

  // overlay preview
  const bakedPrev = Buffer.alloc(1024 * 1024 * 4);
  const small = await sharp(colorPath).resize(1024).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const emSmall = await sharp(emitPath).resize(1024, null, { kernel: 'nearest' }).ensureAlpha().raw().toBuffer();
  for (let i = 0; i < small.data.length; i += 4) {
    const [nr, ng, nb] = lil(small.data[i], small.data[i + 1], small.data[i + 2]);
    bakedPrev[i] = Math.round(nr);
    bakedPrev[i + 1] = Math.round(ng);
    bakedPrev[i + 2] = Math.round(nb);
    bakedPrev[i + 3] = small.data[i + 3];
    if (emSmall[i] > 40) {
      bakedPrev[i] = Math.floor(bakedPrev[i] * 0.2);
      bakedPrev[i + 1] = Math.min(255, Math.floor(bakedPrev[i + 1] * 0.2 + 220));
      bakedPrev[i + 2] = Math.min(255, Math.floor(bakedPrev[i + 2] * 0.2 + 160));
    }
  }
  await sharp(bakedPrev, { raw: { width: 1024, height: 1024, channels: 4 } }).png()
    .toFile(path.join(__dirname, 'preview_overlay.png'));
  await sharp(emitPath).extract({ left: 0, top: 2800, width: 1700, height: 1296 }).resize(900).png()
    .toFile(path.join(__dirname, 'preview_shoe_emit.png'));
  await sharp(colorPath).extract({ left: 0, top: 2800, width: 1700, height: 1296 }).resize(900).png()
    .toFile(path.join(__dirname, 'preview_shoe_color.png'));

  console.log('done', {
    kb: Math.round(fs.statSync(emitPath).size / 1024),
    bak: fs.existsSync(bak),
  });
}

main().catch((e) => { console.error(e); process.exit(1); });
