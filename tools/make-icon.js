// Draws the Hollowmere icon (a brass diving helmet with a lamp-gold porthole) and writes
// desktop/icon.png plus build/icon.ico with 256/128/64/48/32/16 px images. Run: node tools/make-icon.js
'use strict';
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const root = path.join(__dirname, '..');
const SIZES = [512, 256, 128, 64, 48, 32, 16];

const draw = `(size) => {
  const c = document.createElement('canvas'); c.width = c.height = size;
  const g = c.getContext('2d'); const s = size / 256;
  g.scale(s, s);
  // dark slate tile
  const r = 44;
  g.beginPath(); g.moveTo(r, 4); g.arcTo(252, 4, 252, 252, r); g.arcTo(252, 252, 4, 252, r); g.arcTo(4, 252, 4, 4, r); g.arcTo(4, 4, 252, 4, r); g.closePath();
  const bg = g.createLinearGradient(0, 0, 0, 256); bg.addColorStop(0, '#1d2a31'); bg.addColorStop(1, '#070b0e'); g.fillStyle = bg; g.fill();
  g.save(); g.clip();
  // sea line
  g.fillStyle = 'rgba(40,90,110,0.55)'; g.beginPath(); g.moveTo(0, 196);
  for (let x = 0; x <= 256; x += 16) g.quadraticCurveTo(x + 8, 188 + (x % 32 ? 6 : -6), x + 16, 196);
  g.lineTo(256, 256); g.lineTo(0, 256); g.closePath(); g.fill();
  g.restore();
  // helmet
  const brass = g.createRadialGradient(108, 92, 10, 128, 120, 92); brass.addColorStop(0, '#f0cf7a'); brass.addColorStop(0.55, '#b98a3c'); brass.addColorStop(1, '#5e4218');
  g.fillStyle = brass; g.beginPath(); g.arc(128, 118, 76, 0, Math.PI * 2); g.fill();
  g.fillStyle = '#7a5a26'; g.fillRect(58, 176, 140, 24); g.fillStyle = '#d6aa58'; g.fillRect(58, 176, 140, 6);
  // bolts on the collar
  g.fillStyle = '#e8c878'; for (let i = 0; i < 6; i++) { g.beginPath(); g.arc(72 + i * 22.5, 190, 4.5, 0, Math.PI * 2); g.fill(); }
  // porthole with lamp glow
  g.fillStyle = '#4a3410'; g.beginPath(); g.arc(128, 116, 44, 0, Math.PI * 2); g.fill();
  const glow = g.createRadialGradient(118, 104, 4, 128, 116, 38); glow.addColorStop(0, '#fff3c4'); glow.addColorStop(0.45, '#ffcf5a'); glow.addColorStop(1, '#a8620e');
  g.fillStyle = glow; g.beginPath(); g.arc(128, 116, 36, 0, Math.PI * 2); g.fill();
  g.strokeStyle = '#3a2808'; g.lineWidth = 5; g.beginPath(); g.moveTo(128, 80); g.lineTo(128, 152); g.moveTo(92, 116); g.lineTo(164, 116); g.stroke();
  g.strokeStyle = '#e8c878'; g.lineWidth = 6; g.beginPath(); g.arc(128, 116, 42, 0, Math.PI * 2); g.stroke();
  // a single salt knot glinting on the dome
  g.save(); g.translate(176, 64); g.rotate(Math.PI / 4); g.fillStyle = '#d8f6ff'; g.fillRect(-8, -8, 16, 16); g.restore();
  return c.toDataURL('image/png');
}`;

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const pngs = {};
  for (const size of SIZES) {
    const url = await page.evaluate(`(${draw})(${size})`);
    pngs[size] = Buffer.from(url.split(',')[1], 'base64');
  }
  await browser.close();
  fs.mkdirSync(path.join(root, 'build'), { recursive: true });
  fs.writeFileSync(path.join(root, 'desktop', 'icon.png'), pngs[512]);
  fs.writeFileSync(path.join(root, 'build', 'icon.png'), pngs[512]);
  // ICO container with PNG-compressed entries (supported since Windows Vista).
  const entries = SIZES.filter((s) => s <= 256);
  const header = Buffer.alloc(6 + 16 * entries.length);
  header.writeUInt16LE(0, 0); header.writeUInt16LE(1, 2); header.writeUInt16LE(entries.length, 4);
  let offset = header.length;
  entries.forEach((s, i) => {
    const o = 6 + i * 16;
    header.writeUInt8(s === 256 ? 0 : s, o); header.writeUInt8(s === 256 ? 0 : s, o + 1);
    header.writeUInt8(0, o + 2); header.writeUInt8(0, o + 3);
    header.writeUInt16LE(1, o + 4); header.writeUInt16LE(32, o + 6);
    header.writeUInt32LE(pngs[s].length, o + 8); header.writeUInt32LE(offset, o + 12);
    offset += pngs[s].length;
  });
  fs.writeFileSync(path.join(root, 'build', 'icon.ico'), Buffer.concat([header, ...entries.map((s) => pngs[s])]));
  console.log('wrote desktop/icon.png, build/icon.png, build/icon.ico (' + entries.join(', ') + ' px)');
})();
