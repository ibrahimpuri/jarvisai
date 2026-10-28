// Exports the game's data (rooms, items, recipes, lore) to JSON for the Godot build.
// Usage: node tools/export-data.js [out.json]
'use strict';
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const root = path.join(__dirname, '..');
const out = process.argv[2] || path.join(root, 'godot', 'data', 'game.json');
const ctx = { window: {}, console };
ctx.window = ctx;
vm.createContext(ctx);
for (const f of require('./data-files')) {
  vm.runInContext(fs.readFileSync(path.join(root, f), 'utf8'), ctx, { filename: f });
}
const H = ctx.H;
const data = {
  regions: H.REGIONS,
  rooms: H.ROOMS,
  items: H.ITEMS,
  recipes: H.RECIPES,
  weapons: H.WEAPONS,
  upgradeCost: H.UPGRADE_COST,
  fittings: H.FITTINGS,
  grafts: H.GRAFTS,
  abilities: H.ABILITIES,
  knotTypes: H.KNOT_TYPES,
  lore: H.LORE,
};
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(data, null, 1));
console.log(`wrote ${out}: ${H.ROOMS.length} rooms, ${Object.keys(H.ITEMS).length} items, ${Object.keys(H.LORE.carvings).length} carved rooms`);
