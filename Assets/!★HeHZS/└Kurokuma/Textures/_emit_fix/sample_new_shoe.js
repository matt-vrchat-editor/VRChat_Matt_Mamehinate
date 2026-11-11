const sharp = require('sharp');
const path = require('path');

const shot = 'C:\\Users\\mattg\\.cursor\\projects\\d-VRChat-Mine-cliente-Mathues\\assets\\image-9732e687-e2ac-4902-a064-7d5812ce422d.png';

(async () => {
  const { data, info } = await sharp(shot).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  console.log(info.width, info.height);

  // Mark grid of sample points and print colors
  const pts = [];
  for (let fy = 0.35; fy <= 0.85; fy += 0.05) {
    for (let fx = 0.15; fx <= 0.85; fx += 0.05) {
      pts.push([fx, fy]);
    }
  }
  const marked = Buffer.from(data);
  for (const [fx, fy] of pts) {
    const x = Math.floor(fx * info.width);
    const y = Math.floor(fy * info.height);
    const i = (y * info.width + x) * 4;
    const r = data[i], g = data[i + 1], b = data[i + 2];
    // only log greenish
    if (g > r + 20 && g > b + 20) {
      console.log(`(${fx.toFixed(2)},${fy.toFixed(2)})`, { r, g, b, v: Math.max(r, g, b), sat: ((Math.max(r, g, b) - Math.min(r, g, b)) / Math.max(r, g, b)).toFixed(2) });
    }
    // mark magenta
    for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) {
      const xx = x + dx, yy = y + dy;
      if (xx < 0 || yy < 0 || xx >= info.width || yy >= info.height) continue;
      const j = (yy * info.width + xx) * 4;
      marked[j] = 255; marked[j + 1] = 0; marked[j + 2] = 255;
    }
  }
  await sharp(marked, { raw: { width: info.width, height: info.height, channels: 4 } }).png()
    .toFile(path.join(__dirname, 'shoe_shot_grid.png'));
})();
