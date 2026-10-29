// Checks the room data: sizes, legend entries, overlaps, and that every open edge cell
// leads into an open cell of a neighbouring room. Run: node tools/validate.js
'use strict';
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const root = path.join(__dirname, '..');
const ctx = { window: {}, console };
ctx.window = ctx;
vm.createContext(ctx);
for (const f of require('./data-files')) {
  vm.runInContext(fs.readFileSync(path.join(root, f), 'utf8'), ctx, { filename: f });
}
const H = ctx.H;
const SOLID = new Set('#_TRMPSbxCG='.split('').filter((c) => c !== '=' && c !== 'b'));
const TILECHARS = new Set('#_TRMP=bSxoHELVCKZYyhGf|waQJU '.split(''));
let errors = 0;
const err = (m) => { errors++; console.log('ERROR ' + m); };
const warn = (m) => console.log('warn  ' + m);

const cells = new Map(); // "x,y" -> {room, ch}
for (const r of H.ROOMS) {
  const w = (r.w || 1) * H.SCREEN_W, h = (r.h || 1) * H.SCREEN_H;
  if (r.map.length !== h) err(`${r.id}: ${r.map.length} rows, expected ${h}`);
  r.map.forEach((row, y) => {
    if (row.length !== w) err(`${r.id}: row ${y} has ${row.length} chars, expected ${w}: "${row}"`);
    for (let x = 0; x < row.length; x++) {
      const ch = row[x];
      if (!TILECHARS.has(ch) && !(r.ents && r.ents[ch])) err(`${r.id}: unknown char '${ch}' at ${x},${y}`);
      const wx = r.col * H.SCREEN_W + x, wy = r.row * H.SCREEN_H + y;
      const k = wx + ',' + wy;
      if (cells.has(k)) err(`${r.id} overlaps ${cells.get(k).room.id} at ${k}`);
      cells.set(k, { room: r, ch: TILECHARS.has(ch) ? ch : ' ' });
    }
  });
  for (const ch of Object.keys(r.ents || {})) {
    if (!r.map.some((row) => row.includes(ch))) warn(`${r.id}: legend '${ch}' never placed`);
  }
}

const open = (c) => c && !SOLID.has(c.ch);
let links = 0;
for (const r of H.ROOMS) {
  const w = (r.w || 1) * H.SCREEN_W, h = (r.h || 1) * H.SCREEN_H;
  const ox = r.col * H.SCREEN_W, oy = r.row * H.SCREEN_H;
  const edge = [];
  for (let y = 0; y < h; y++) { edge.push([0, y, -1, 0]); edge.push([w - 1, y, 1, 0]); }
  for (let x = 0; x < w; x++) { edge.push([x, 0, 0, -1]); edge.push([x, h - 1, 0, 1]); }
  for (const [x, y, dx, dy] of edge) {
    const here = cells.get((ox + x) + ',' + (oy + y));
    if (!open(here)) continue;
    const there = cells.get((ox + x + dx) + ',' + (oy + y + dy));
    if (!there) { err(`${r.id}: open edge at ${x},${y} leads off the world`); continue; }
    if (!open(there)) err(`${r.id}: open edge at ${x},${y} meets solid '${there.ch}' in ${there.room.id}`);
    else if (there.room !== r) links++;
  }
}
console.log(`${H.ROOMS.length} rooms, ${links} open edge cells linked, ${errors} errors`);
process.exit(errors ? 1 : 0);
