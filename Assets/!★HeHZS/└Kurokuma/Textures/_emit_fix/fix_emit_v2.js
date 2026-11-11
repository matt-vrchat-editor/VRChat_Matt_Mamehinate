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
  // Restore backup if we already overwrote once
  if (fs.existsSync(bakPath)) {
    fs.copyFileSync(bakPath, emitPath);
    console.log('restored bak');
  }

  console.log('load');
  const color = await sharp(colorPath).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const emitIn = await sharp(emitPath).ensureAlpha().raw().toBuffer();
  const { width, height } = color.info;
  const data = color.data;
  const n = width * height;

  console.log('analyze');
  const pink = new Uint8Array(n);
  const greenS = new Float32Array(n);
  const greenV = new Float32Array(n);
  const denseFlat = new Uint8Array(n); // atlas-like: pink fills neighborhood

  for (let p = 0; p < n; p++) greenS[p] = -1;

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const p = y * width + x;
      const i = p * 4;
      if (!isSrcPink(data[i], data[i + 1], data[i + 2], data[i + 3])) continue;
      pink[p] = 1;
      const [nr, ng, nb] = lil(data[i], data[i + 1], data[i + 2]);
      const [h, s, v] = hsv(nr, ng, nb);
      if (s >= 0.35 && v >= 0.10 && h >= 80 && h <= 155) {
        greenS[p] = s;
        greenV[p] = v;
      }
    }
  }

  // density of pink in 11x11 window
  const R = 5;
  for (let y = R; y < height - R; y++) {
    for (let x = R; x < width - R; x++) {
      const p = y * width + x;
      if (!pink[p] || greenS[p] < 0) continue;
      let c = 0, tot = 0;
      for (let dy = -R; dy <= R; dy++) {
        for (let dx = -R; dx <= R; dx++) {
          tot++;
          if (pink[(y + dy) * width + (x + dx)]) c++;
        }
      }
      if (c / tot >= 0.82) denseFlat[p] = 1;
    }
  }

  // Seeds from current emit
  const seed = new Uint8Array(n);
  let seedN = 0;
  for (let p = 0, i = 0; p < n; p++, i += 4) {
    if (emitIn[i] > 40) { seed[p] = 1; seedN++; }
  }
  console.log({ seedN });

  // Grow from seeds into pink-green, avoid dense flat atlas
  const MAX_DIST = 500;
  const dist = new Int32Array(n);
  dist.fill(-1);
  const q = new Int32Array(Math.min(n, seedN * 50 + 1000000));
  let qh = 0, qt = 0;
  for (let p = 0; p < n; p++) {
    if (!seed[p]) continue;
    dist[p] = 0;
    q[qt++] = p;
  }

  console.log('grow');
  while (qh < qt) {
    const p = q[qh++];
    const d0 = dist[p];
    if (d0 >= MAX_DIST) continue;
    const x = p % width, y = (p / width) | 0;
    for (const [nx, ny] of [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]]) {
      if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
      const np = ny * width + nx;
      if (dist[np] !== -1) continue;
      if (!pink[np] || greenS[np] < 0) continue;
      // Block atlas-like dense flat regions (unless already a seed)
      if (denseFlat[np] && !seed[np]) {
        // allow a short fringe into dense areas only if near seed clothing
        if (d0 > 8) continue;
      }
      dist[np] = d0 + 1;
      if (qt < q.length) q[qt++] = np;
    }
  }

  // Fill any non-dense green-pink component that overlaps grown/seed and isn't huge border slab
  console.log('fill components');
  const visited = new Uint8Array(n);
  const keep = new Uint8Array(n);
  let grownN = 0;
  for (let p = 0; p < n; p++) if (dist[p] >= 0) { keep[p] = 1; grownN++; }

  for (let start = 0; start < n; start++) {
    if (!pink[start] || greenS[start] < 0 || visited[start] || denseFlat[start]) continue;
    const stack = [start];
    visited[start] = 1;
    const comp = [];
    let overlap = false;
    let borderHits = 0;
    while (stack.length) {
      const idx = stack.pop();
      comp.push(idx);
      if (keep[idx] || seed[idx]) overlap = true;
      const x = idx % width, y = (idx / width) | 0;
      if (x <= 2 || y <= 2 || x >= width - 3 || y >= height - 3) borderHits++;
      for (const [nx, ny] of [[x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]]) {
        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
        const nidx = ny * width + nx;
        if (visited[nidx] || !pink[nidx] || greenS[nidx] < 0) continue;
        if (denseFlat[nidx]) continue; // don't cross into atlas via this fill
        visited[nidx] = 1;
        stack.push(nidx);
      }
    }
    if (!overlap) continue;
    if (comp.length > n * 0.05 && borderHits > 20) continue;
    for (const idx of comp) keep[idx] = 1;
  }

  // Shoe filtering: dark green only in shoe/sole regions
  const shoeBoxes = [
    { x0: 0, y0: 3350, x1: 820, y1: 4095 },
    { x0: 820, y0: 3350, x1: 1720, y1: 4095 },
  ];
  const soleCircleBox = { x0: 0, y0: 2420, x1: 900, y1: 2920 };
  const inBox = (x, y, b) => x >= b.x0 && x <= b.x1 && y >= b.y0 && y <= b.y1;

  const out = Buffer.alloc(n * 4);
  let keepN = 0, shoeDark = 0, shoeSkip = 0;
  for (let p = 0, i = 0; p < n; p++, i += 4) {
    let on = false;
    if (keep[p] && greenS[p] >= 0) {
      const x = p % width, y = (p / width) | 0;
      let inShoe = false;
      for (const b of shoeBoxes) if (inBox(x, y, b)) { inShoe = true; break; }
      const inSole = inBox(x, y, soleCircleBox);
      if (inShoe || inSole) {
        // dark recessed green only (matches screenshot midsole blocks)
        if (greenS[p] >= 0.50 && greenV[p] <= 0.52) { on = true; shoeDark++; }
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
  console.log({ grownN, keepN, shoeDark, shoeSkip, seedN });

  // 1px dilate
  const dil = Buffer.from(out);
  for (let y = 1; y < height - 1; y++) {
    for (let x = 1; x < width - 1; x++) {
      const p = y * width + x;
      const i = p * 4;
      if (out[i] > 128 || greenS[p] < 0) continue;
      let inShoe = false;
      for (const b of shoeBoxes) if (inBox(x, y, b)) { inShoe = true; break; }
      if ((inShoe || inBox(x, y, soleCircleBox)) && !(greenS[p] >= 0.50 && greenV[p] <= 0.52)) continue;
      let near = false;
      for (let dy = -1; dy <= 1 && !near; dy++)
        for (let dx = -1; dx <= 1; dx++)
          if (out[((y + dy) * width + x + dx) * 4] > 128) { near = true; break; }
      if (near) dil[i] = dil[i + 1] = dil[i + 2] = 255;
    }
  }

  if (!fs.existsSync(bakPath)) fs.copyFileSync(emitPath, bakPath);
  await sharp(dil, { raw: { width, height, channels: 4 } }).png().toFile(emitPath);

  // previews
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
  await sharp(ov, { raw: { width: 1024, height: 1024, channels: 4 } }).png().toFile(path.join(__dirname, 'preview_overlay.png'));
  await sharp(emitPath).extract({ left: 0, top: 2800, width: 1700, height: 1296 }).resize(900).png()
    .toFile(path.join(__dirname, 'preview_shoe_emit.png'));

  // coverage on hem / ribbons area
  function cov(name, x0, y0, x1, y1) {
    let g = 0, ge = 0;
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const p = y * width + x, i = p * 4;
      if (greenS[p] < 0) continue;
      g++;
      if (dil[i] > 40) ge++;
    }
    console.log(name, { g, ge, pct: g ? (100 * ge / g).toFixed(1) + '%' : 'n/a' });
  }
  cov('hem_band', 0, 2950, 2800, 3350);
  cov('shoeL', 0, 3400, 800, 4095);
  cov('paws', 1800, 1100, 3600, 2100);
  cov('soles', 0, 2450, 850, 2850);
  console.log('done');
}

main().catch((e) => { console.error(e); process.exit(1); });
