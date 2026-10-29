// Reachability check for the level design. Models Maren's movement on the tile grid
// (walk, jump arcs, ladders, swimming at each tide phase, ring swings, Jet Valve, brine,
// Gullet pressure) and walks the critical path stage by stage.  Run: node tools/reach.js
'use strict';
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const root = path.join(__dirname, '..');
const ctx = { console, performance: { now: () => 0 } };
ctx.window = ctx;
vm.createContext(ctx);
for (const f of [...require('./data-files'), 'js/tide.js', 'js/world.js', 'js/narrative.js']) {
  vm.runInContext(fs.readFileSync(path.join(root, f), 'utf8'), ctx, { filename: f });
}
const H = ctx.H, W = H.World, T = H.Tide;
W.build();

function targets() {
  const out = {};
  for (const r of H.ROOMS) {
    const add = (e, tx, ty) => { out[(e.id || e.type) + '@' + r.id] = { x: r.ox + tx, y: r.oy + ty, room: r.id }; };
    r.map.forEach((row, ty) => { for (let tx = 0; tx < row.length; tx++) { const e = r.ents && r.ents[row[tx]]; if (e) add(e, tx, ty); } });
    (r.ents2 || []).forEach((e) => add(e, e.tx, e.ty));
  }
  return out;
}
const TG = targets();

const PHASES = [['low', 10], ['high', 10], ['ebbing', 60], ['rising', 60]];

const GW = Math.max(...H.ROOMS.map((r) => r.col + r.w)) * 40, GH = Math.max(...H.ROOMS.map((r) => r.row + r.h)) * 23;
function analyse(abil, flags, extraSolid = [], opts = {}) {
  W.flags = Object.assign({}, flags);
  const can = (a) => abil.includes(a);
  // Precompute per-phase grids: 0 empty,1 solid,2 one-way; water bits.
  const grids = (opts.phases || PHASES).map(([ph, into]) => {
    T.setPhase(ph, into);
    const sol = new Uint8Array(GW * GH), wat = new Uint8Array(GW * GH), lad = new Uint8Array(GW * GH), room = new Uint8Array(GW * GH);
    for (let y = 0; y < GH; y++) for (let x = 0; x < GW; x++) {
      const i = y * GW + x;
      const r = W.roomAtTile(x, y);
      if (!r) { sol[i] = 1; continue; }
      room[i] = 1;
      sol[i] = W.solid(x, y);
      lad[i] = W.isLadder(x, y) ? 1 : 0;
      const px = x * 16 + 8, py = y * 16 + 8;
      if (W.isWater(px, py)) wat[i] = 1 | (W.isBrine(px, py) ? 2 : 0) | (W.isDeep(px, py) ? 4 : 0);
    }
    for (const [x, y] of extraSolid) sol[y * GW + x] = 1;
    return { sol, wat, lad, room };
  });
  const rings = [];
  for (const r of H.ROOMS) for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) if (W.charAt(r.ox + x, r.oy + y) === 'o') rings.push([r.ox + x, r.oy + y]);
  const seen = new Uint8Array(GW * GH);
  const from = new Int32Array(GW * GH).fill(-1);
  const how = new Array(GW * GH);
  let cur = -1, curHow = '';
  let count = 0;
  let g = grids[0];
  const inb = (x, y) => x >= 0 && y >= 0 && x < GW && y < GH;
  const solid = (x, y) => (inb(x, y) ? g.sol[y * GW + x] : 1);
  const free = (x, y) => solid(x, y) !== 1;
  const bodyFree = (x, y) => free(x, y) && free(x, y - 1);
  const wbits = (x, y) => (inb(x, y) ? g.wat[y * GW + x] : 0);
  const water = (x, y) => wbits(x, y) & 1;
  const brine = (x, y) => wbits(x, y) & 2;
  const deep = (x, y) => wbits(x, y) & 4;
  const ladder = (x, y) => inb(x, y) && g.lad[y * GW + x];
  const spikes = (x, y) => W.charAt(x, y) === 'x';
  const stand = (x, y) => bodyFree(x, y) && solid(x, y + 1) >= 1 && !spikes(x, y);
  const swimOK = (x, y) => water(x, y) && !(brine(x, y) && can('boots')) && !(deep(x, y) && !can('seal'));
  // Brine floats her and the Gullet's pressure stops an unsealed suit diving: in that water only the surface can be
  // reached by a jump or a swing (weight takes her down from there, by falling).
  const held = (x, y) => water(x, y) && (brine(x, y) || (deep(x, y) && !can('seal'))) && water(x, y - 1);
  // Clinging to timber or rib bone: the wall on side s covers the whole body.
  const wallSide = (x, y) => (W.isWallHang(x - 1, y) && W.isWallHang(x - 1, y - 1) ? -1 : W.isWallHang(x + 1, y) && W.isWallHang(x + 1, y - 1) ? 1 : 0);
  const hang = (x, y) => bodyFree(x, y) && !stand(x, y) && wallSide(x, y) !== 0;
  const queue = [];
  const dist = opts.dijkstra ? new Float64Array(GW * GH).fill(Infinity) : null;
  const heap = [];
  const hpush = (d, i) => { heap.push([d, i]); let k = heap.length - 1; while (k > 0) { const pk = (k - 1) >> 1; if (heap[pk][0] <= heap[k][0]) break; [heap[pk], heap[k]] = [heap[k], heap[pk]]; k = pk; } };
  const hpop = () => { const top = heap[0]; const last = heap.pop(); if (heap.length) { heap[0] = last; let k = 0; for (;;) { const l = 2 * k + 1, r = l + 1; let m = k; if (l < heap.length && heap[l][0] < heap[m][0]) m = l; if (r < heap.length && heap[r][0] < heap[m][0]) m = r; if (m === k) break; [heap[m], heap[k]] = [heap[k], heap[m]]; k = m; } } return top; };
  const edges = opts.edges ? new Set() : null;
  const COST = { w: 1, l: 1, s: 1.3, b: 1.3, d: 2, j: 6, r: 20, start: 0 };
  const push = (x, y, mv) => {
    if (!inb(x, y)) return; const i = y * GW + x;
    if (!bodyFree(x, y) || !g.room[i]) return;
    mv = mv || curHow;
    if (dist) {
      const cx0 = cur >= 0 ? cur % GW : x, cy0 = cur >= 0 ? Math.floor(cur / GW) : y;
      const jetJump = mv === 'j' && (cy0 - y > 3 || Math.abs(x - cx0) > (cy0 - y >= 2 ? 4 : 5));
      const c = (COST[mv] || 1) + (mv === 'j' ? Math.abs(x - cx0) + Math.abs(y - cy0) + (jetJump ? 30 : 0) : mv === 'w' ? Math.abs(x - cx0) + Math.max(0, y - cy0) * 0.3 : 0);
      const nd = (cur >= 0 ? dist[cur] : 0) + c;
      if (nd < dist[i]) { dist[i] = nd; if (!seen[i]) count++; seen[i] = 1; from[i] = cur; how[i] = mv; hpush(nd, i); }
      return;
    }
    if (edges && cur >= 0 && cur !== i) edges.add(cur * GW * GH + i);
    if (!seen[i]) { seen[i] = 1; count++; queue.push(i); from[i] = cur; how[i] = mv; }
  };
  const fall = (x, y) => {
    for (let k = 0; k < 80; k++) {
      if (!bodyFree(x, y)) return;
      if (deep(x, y) && !can('seal')) { push(x, y); return; }
      if (stand(x, y) || ladder(x, y) || ladder(x, y - 1) || swimOK(x, y) || (water(x, y) && brine(x, y))) { push(x, y); if (!(brine(x, y) && can('boots')) || stand(x, y)) return; }
      y++;
    }
  };
  const jet = can('jet');
  const jumpDX = (dy) => (dy >= 3 ? (jet ? 7 : 4) : dy >= 2 ? (jet ? 8 : 4) : (jet ? 9 : 5));
  const maxUp = jet ? 6 : 3;
  const arc = (sx, sy, tx, ty) => {
    const top = Math.min(sy, ty) - (ty < sy ? 0 : 1);
    for (let y = sy; y >= top; y--) if (!bodyFree(sx, y)) return false;
    const s = Math.sign(tx - sx);
    if (s) for (let x = sx; x !== tx + s; x += s) if (!bodyFree(x, top)) return false;
    for (let y = top; y <= ty; y++) if (!bodyFree(tx, y)) return false;
    return true;
  };
  const losT = (x1, y1, x2, y2) => { const n = Math.max(Math.abs(x2 - x1), Math.abs(y2 - y1)) * 2; for (let i = 1; i < n; i++) { const t = i / n; if (!free(Math.round(x1 + (x2 - x1) * t), Math.round(y1 + (y2 - y1) * t))) return false; } return true; };
  const step = (x, y) => {
    cur = y * GW + x; curHow = 'w';
    const inW = water(x, y);
    const grounded = stand(x, y) || ladder(x, y) || ladder(x, y - 1);
    const surface = inW && !water(x, y - 1);
    const crushed = inW && deep(x, y) && !can('seal');
    if (inW && (brine(x, y) || crushed)) {
      curHow = 'b';
      // boots off: float up and sideways (boots can be toggled at any time)
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, -1]]) { const nx = x + dx, ny = y + dy; if (bodyFree(nx, ny)) { if (water(nx, ny)) push(nx, ny); else fall(nx, ny); } }
      if (!water(x, y - 1) || !water(x, y - 2)) for (let dy = 0; dy <= 2; dy++) for (let dx = -3; dx <= 3; dx++) if (stand(x + dx, y - dy) && arc(x, y, x + dx, y - dy)) push(x + dx, y - dy);
      if (!can('boots') || crushed) return;
      // with the boots on, the brine lets her sink
      if (brine(x, y)) fall(x, y + 1);
    }
    if (swimOK(x, y)) {
      curHow = 's';
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = x + dx, ny = y + dy;
        if (!bodyFree(nx, ny)) continue;
        // swimming down into brine with the boots on, she sinks through it
        if (swimOK(nx, ny)) push(nx, ny); else if (!water(nx, ny) || (brine(nx, ny) && can('boots'))) fall(nx, ny);
      }
    }
    if (ladder(x, y) || ladder(x, y - 1)) {
      curHow = 'l';
      for (const dy of [-1, 1]) if (bodyFree(x, y + dy) && (ladder(x, y + dy) || ladder(x, y + dy - 1) || stand(x, y + dy))) push(x, y + dy);
      if (!ladder(x, y - 1) && ladder(x, y)) fall(x, y - 1);
    }
    // From a cling, leap away from the wall: up to 3 tiles higher, out to 5 across.
    if (hang(x, y) && !grounded) {
      curHow = 'j';
      const away = -wallSide(x, y);
      for (let dy = -6; dy <= 3; dy++) for (let k = 1; k <= 5; k++) {
        const nx = x + away * k, ny = y - dy;
        if ((stand(nx, ny) || hang(nx, ny) || ladder(nx, ny) || swimOK(nx, ny)) && !held(nx, ny) && arc(x, y, nx, ny)) push(nx, ny);
      }
      fall(x, y + 1);
    }
    if (!(grounded || surface)) return;
    curHow = 'd';
    // A ground dash carries Maren across a short strip of salt crystals untouched.
    for (const s of [-1, 1]) for (let k = 1; k <= 3; k++) { const nx = x + s * k; if (!bodyFree(nx, y)) break; if (stand(nx, y)) { if (k > 1) push(nx, y); break; } if (!spikes(nx, y)) break; }
    curHow = 'w';
    for (const dx of [-1, 1]) { if (bodyFree(x + dx, y)) fall(x + dx, y); else if (bodyFree(x + dx, y - 1) && stand(x + dx, y - 1)) push(x + dx, y - 1, 'j'); }
    curHow = 'd';
    if (solid(x, y + 1) === 2) fall(x, y + 2);
    curHow = 'j';
    for (let dy = -10; dy <= maxUp; dy++) {
      const lim = dy < 0 ? jumpDX(0) + Math.floor(-dy * 0.5) : jumpDX(dy);
      for (let dx = -lim; dx <= lim; dx++) {
        const nx = x + dx, ny = y - dy;
        if (dx === 0 && dy === 0) continue;
        if (!(stand(nx, ny) || ladder(nx, ny) || ladder(nx, ny - 1) || swimOK(nx, ny) || hang(nx, ny)) || held(nx, ny)) continue;
        if (arc(x, y, nx, ny)) push(nx, ny);
      }
    }
    curHow = 'r';
    if (can('swing') && !opts.noSwing) for (const [rx, ry] of rings) {
      if (ry >= y - 1 || Math.abs(rx - x) > 7 || Math.abs(ry - y) > 8 || Math.hypot(rx - x, ry - (y - 1)) > 6.5 || !losT(x, y - 1, rx, ry)) continue;
      for (let ty = ry + 2; ty <= ry + 8; ty++) for (let tx = rx - 7; tx <= rx + 7; tx++) {
        if ((stand(tx, ty) || ladder(tx, ty)) && !held(tx, ty) && Math.hypot(tx - rx, ty - 1 - ry) <= 7.5 && losT(rx, ry, tx, ty - 1)) push(tx, ty);
      }
    }
  };
  const st0 = opts.start || [TG['start@reef'].x, TG['start@reef'].y];
  if (dist) {
    g = grids[0];
    push(st0[0], st0[1], 'start');
    while (heap.length) {
      const [d, i] = hpop();
      if (d > dist[i]) continue;
      step(i % GW, Math.floor(i / GW));
    }
  } else push(st0[0], st0[1], 'start');
  let changed = !dist, rounds = 0;
  while (changed && rounds < 20) {
    changed = false; rounds++;
    for (let pi = 0; pi < grids.length; pi++) {
      g = grids[pi];
      const before = count;
      for (let i = 0; i < seen.length; i++) if (seen[i]) queue.push(i);
      while (queue.length) { const i = queue.pop(); step(i % GW, Math.floor(i / GW)); }
      if (count !== before) changed = true;
    }
  }
  const set = new Set();
  for (let i = 0; i < seen.length; i++) if (seen[i]) set.add((i % GW) + ',' + Math.floor(i / GW));
  set.edges = edges;
  set.pathTo = (x, y) => {
    let i = y * GW + x; const out = [];
    if (!seen[i]) return null;
    while (i >= 0) { out.push([i % GW, Math.floor(i / GW), how[i]]); i = from[i]; }
    return out.reverse();
  };
  return set;
}

function near(seen, t, r = 2) {
  for (let dy = -r; dy <= r + 1; dy++) for (let dx = -r; dx <= r; dx++) if (seen.has((t.x + dx) + ',' + (t.y + dy))) return true;
  return false;
}

const stages = [
  { name: 'The Breakers: ashore to the reef', abil: ['swing'], flags: {}, start: 'start@breakers', breakB: true, need: ['ch_cache@breakers', 'start@reef', 'ch_constance@reef'] },
  { name: 'GATE: the salt plug holds until it burns', abil: ['swing'], flags: {}, start: 'start@breakers', mustNot: ['start@reef'] },
  { name: 'Start (Gaff Swing only)', abil: ['swing'], flags: {}, need: ['ch_constance@reef', 'bell_chapel@chapel', 'crank@tithe', 'seawall_socket@seawall', 'first_shucker@pier', 'page_tithe@tithe', 'ch_gallery@tithe_gallery',
    'ch_cottage@cottage', 'ch_lookout@lookout', 'bell_ferry@ferrybell', 'ch_ferrybell@ferrybell', 'ch_marguerite@wrecks', 'ch_ferry@ferry', 'ch_hithe@hithe', 'ch_arch@seaarch',
    'ch_hold@constance_hold', 'ch_seabed@breakersfloor', 'ch_smugglers@smugglers'], want: ['seal_reef@reef', 'stitch_seawall@seawall'] },
  { name: 'GATE: the sump keeps its box from anyone who floats', abil: ['swing'], flags: { seawallOpen: true }, breakS: true, need: ['ch_pools@tidepools'], mustNot: ['ch_sump@saltsump'] },
  { name: 'Seawall gate open', abil: ['swing'], flags: { seawallOpen: true }, need: ['saint@saltgate', 'sea@saltgate', 'kiln@saltgate', 'bell_salt@saltbell', 'barge@saltgate'], want: ['seal_pans@saltgate', 'ch_pans@saltgate'],
    // the barge guards the Kilns: no way round it, from the Bell Room's ladder or anywhere else
    mustNot: ['ch_kilns@kilns', 'ch_upperkilns@upperkilns', 'bell_mine@minebell', 'abbot@abbot'] },
  { name: 'Barge puzzle solved', abil: ['swing'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true }, pool: true, need: ['ch_kilns@kilns', 'harpoon@kilns', 'kiln@abbot', 'abbot@abbot',
    'ch_sheds@sheds', 'ch_carts@cartyard', 'ch_salthouse@salthouse', 'ch_upperkilns@upperkilns', 'ch_terraces@terraces', 'ch_furnace@furnace', 'bell_mine@minebell', 'ch_minebell@minebell',
    'ch_picks@picks', 'sawline@picks', 'ch_winze@winze', 'ch_seam@seam', 'ch_collapse@collapse', 'ch_saltcloister@saltcloister'], want: ['stitch_kilns@kilns', 'page_kilns@kilns'], mustNot: ['ch_seep@seep', 'ch_cistern@cisterns'] },
  { name: 'Ballast Boots', abil: ['swing', 'boots'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, abbotDead: true }, pool: true, need: ['bell_cloister@cloisterbell', 'sling@nave', 'teodor@nave', 'ch_brine@brineshaft', 'table_1@cloister', 'table_2@nave', 'ch_seep@seep',
    'ch_lavatorium@lavatorium', 'ch_chapter@chapter', 'ch_warming@warming', 'ch_undercroft@undercroft', 'ch_nightstair@nightstair', 'ch_dorter@dorter', 'ch_scriptorium@scriptorium', 'ch_library@library', 'ch_counting@counting', 'almoner@refectory'],
    want: ['seal_nave@nave', 'page_cloister@cloister', 'ch_cloister@cloister'],
    // the Almoner's chained doors are the only way west
    mustNot: ['bell_almonry@almonry', 'ch_kitchens@kitchens', 'jet@priors', 'table_3@priors', 'ch_lodging@lodging', 'ch_lightwell@lightwell'] },
  { name: 'The Almoner dead', abil: ['swing', 'boots'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, abbotDead: true, almonerDead: true }, pool: true, need: ['bell_almonry@almonry', 'ch_almonry@almonry', 'jet@priors', 'table_3@priors',
    'ch_kitchens@kitchens', 'ch_infirmary@infirmary', 'ch_charnel@charnel', 'ch_cells@cells', 'ch_garth@garth', 'ch_dole@dole', 'ch_sacristy@sacristy', 'ch_pulpitum@pulpitum', 'ch_lodging@lodging'],
    // what the Jet Valve opens, back in the Cloister
    mustNot: ['ch_lightwell@lightwell', 'ch_bellcote@bellcote', 'ch_treasury@treasury'] },
  { name: 'GATE: the Baptistery\'s crystal holds the Choir\'s west', abil: ['swing', 'boots', 'jet'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true }, pool: true,
    need: ['valve@baptistery', 'tunedbell@baptistery', 'ch_narthex@narthex', 'ch_stalls@stalls', 'ch_songschool@songschool', 'ch_crypt@crypt'], mustNot: ['bell_vestry@vestry', 'tunedbell@organloft'] },
  { name: 'Jet Valve', abil: ['swing', 'boots', 'jet'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true }, pool: true, crystals: ['baptistery'],
    need: ['sluice@choir', 'hesper@choir', 'bell_choir@choirbell', 'tunedbell@choir', 'valve@choir', 'table_4@choirsteps', 'ch_lightwell@lightwell', 'ch_bellcote@bellcote', 'ch_treasury@treasury',
      'bell_vestry@vestry', 'ch_vestry@vestry', 'ch_belfry@belltower', 'valve@organloft', 'tunedbell@organloft', 'valve@bonechoir', 'tunedbell@bonechoir'],
    want: ['stitch_steps@choirsteps', 'ch_steps@choirsteps', 'page_cbell@choirbell'],
    // the Precentor holds the doors east of the loft: the Tide Key, and the galleries back
    mustNot: ['tidekey@tuning', 'ch_choirmaster@choirmaster', 'ch_nightgallery@nightgallery', 'ch_bonechoir@bonechoir'] },
  { name: 'The Precentor dead', abil: ['swing', 'boots', 'jet'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true }, pool: true, crystals: ['baptistery'],
    need: ['tidekey@tuning', 'ch_choirmaster@choirmaster', 'ch_pipegallery@pipegallery', 'ch_bellows@bellows', 'ch_nightgallery@nightgallery', 'ch_clerestory@clerestory'],
    // the bone choir's vault needs the Tide Key's full basin
    mustNot: ['ch_bonechoir@bonechoir'] },
  { name: 'Tide Key (crystal broken, water raised)', abil: ['swing', 'boots', 'jet', 'tidekey'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true }, pool: true, choir: 3, breakC: true, crystals: ['baptistery', 'bonechoir'], need: ['tidekey@tuning', 'ch_bonechoir@bonechoir', 'ch_choirpit@choir', 'bell_rib@ribbell', 'ch_ribs@ribshaft', 'ch_breastbone@breastbone', 'ch_westribs@westribs', 'ch_lung@lung', 'ch_marrow@marrow',
      'ch_ribladder@ribladder', 'ch_collarbone@collarbone', 'ch_vertebrae@vertebrae'],
    // the Hanging Mother holds the Ribvault's way down, and the prisms
    mustNot: ['bell_sternum@sternumbell', 'prismA@prisms', 'seal@prisms', 'ch_floating@floating', 'ch_lanternlung@lanternlung', 'ch_skullgate@skullgate', 'ch_breastplate@breastplate'], want: ['seal_ribs@ribshaft', 'page_ribs@ribshaft', 'seal_choir@choir', 'page_choir@choir'] },
  { name: 'The Hanging Mother dead', abil: ['swing', 'boots', 'jet', 'tidekey'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, motherDead: true }, pool: true, crystals: ['baptistery', 'bonechoir'], choir: 3, breakC: true,
    need: ['bell_sternum@sternumbell', 'ch_sternumbell@sternumbell', 'ch_eastribs@eastribs', 'ch_floating@floating', 'ch_breastplate@breastplate', 'ch_lanternlung@lanternlung', 'ch_skullgate@skullgate', 'prismA@prisms', 'prismB@prisms', 'prismC@prisms'],
    // the Heart-Root's brine is deep: the Seal first
    mustNot: ['ch_heartroot@heartroot'] },
  { name: 'Drained choir route instead', abil: ['swing', 'boots', 'jet', 'tidekey'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, choirDrained: true }, pool: true, crystals: ['baptistery', 'bonechoir'], choir: 21, breakC: true, need: ['bell_rib@ribbell', 'ch_cache@choir'] },
  { name: 'Prisms solved: Pressure Seal', abil: ['swing', 'boots', 'jet', 'tidekey'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, motherDead: true }, pool: true, crystals: ['baptistery', 'bonechoir'], choir: 3, breakC: true, breakS: true, need: ['seal@prisms', 'ch_prisms@prisms'], want: ['stitch_prisms@prisms'] },
  { name: 'Seal + lift dropped', abil: ['swing', 'boots', 'jet', 'tidekey', 'seal'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, motherDead: true, liftDropped: true }, pool: true, crystals: ['baptistery', 'bonechoir'], choir: 3, breakC: true, breakS: true, lift: true, need: ['page_gullet@gulletmouth', 'page_lair@lair', 'ch_cistern@cisterns', 'ch_sump@saltsump', 'ch_heartroot@heartroot',
    'ch_throatgate@throatgate', 'ch_longthroat@longthroat', 'ch_stomach@stomach', 'bell_drowned@drownedbell', 'ch_drownedbell@drownedbell', 'ch_pylorus@pylorus', 'ch_deep@deep',
    'bell_lastair@lastair', 'ch_lastair@lastair', 'ch_galleries@galleries', 'toller@buoychapel', 'ch_wreck@wreck', 'ch_whalefall@whalefall'], want: ['seal_gullet@gulletmouth'] },
  { name: 'GATE: the Saint\'s epiglottis keeps the lair shut from the Gullet Mouth', abil: ['swing', 'boots', 'jet', 'tidekey', 'seal'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, motherDead: true, liftDropped: true }, pool: true, crystals: ['baptistery', 'bonechoir'], choir: 3, breakC: true, breakS: true, lift: true, start: 'page_gullet@gulletmouth', avoid: ['buoychapel'], mustNot: ['page_lair@lair'] },
  { name: 'The Undertow dead: the lair opens onto the Gullet Mouth', abil: ['swing', 'boots', 'jet', 'tidekey', 'seal'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, motherDead: true, liftDropped: true, undertowDead: true }, pool: true, crystals: ['baptistery', 'bonechoir'], choir: 3, breakC: true, breakS: true, lift: true, start: 'page_gullet@gulletmouth', avoid: ['buoychapel'], need: ['page_lair@lair'] },
  { name: 'GATE: no Jet means no Choir', abil: ['swing', 'boots'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true }, pool: true, mustNot: ['hesper@choir', 'tidekey@tuning'] },
  { name: 'GATE: no Tide Key means no Ribvault', abil: ['swing', 'boots', 'jet'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true }, pool: true, crystals: ['baptistery', 'bonechoir'], breakC: true, mustNot: ['bell_rib@ribbell', 'seal@prisms'] },
  { name: 'GATE: no Seal means no Gullet', abil: ['swing', 'boots', 'jet', 'tidekey'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, motherDead: true, liftDropped: true }, pool: true, crystals: ['baptistery', 'bonechoir'], choir: 3, breakC: true, lift: true, mustNot: ['page_lair@lair'] },
  { name: 'GATE: Skull closed', abil: ['swing', 'boots', 'jet', 'tidekey', 'seal'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, motherDead: true, liftDropped: true }, pool: true, crystals: ['baptistery', 'bonechoir'], choir: 3, breakC: true, breakS: true, lift: true, mustNot: ['agathe@lamp'] },
  { name: 'Skull open: the Skull up to the Shoal', abil: ['swing', 'boots', 'jet', 'tidekey', 'seal'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, motherDead: true, liftDropped: true, skullOpen: true, undertowDead: true }, pool: true, crystals: ['baptistery', 'bonechoir'], choir: 3, breakC: true, breakS: true, lift: true,
    need: ['shoal@brainpan', 'ch_sinus@sinus', 'bell_watch@keeperswatch', 'ch_watch@keeperswatch', 'ch_crown@crown', 'ch_lefteye@lefteye', 'ch_nasal@nasal', 'ch_righteye@righteye', 'templeA@temple', 'templeB@temple', 'ch_temple@temple', 'ch_earbone@earbone', 'ch_jaw@jaw', 'ch_tongue@jaw'],
    mustNot: ['agathe@lamp', 'bell_agathe@agathesroom'] },
  { name: 'The Shoal dead: the Lamp', abil: ['swing', 'boots', 'jet', 'tidekey', 'seal'], flags: { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, motherDead: true, liftDropped: true, skullOpen: true, undertowDead: true, shoalDead: true }, pool: true, crystals: ['baptistery', 'bonechoir'], choir: 3, breakC: true, breakS: true, lift: true,
    need: ['agathe@lamp', 'lampwork@lamp', 'beacon@lamp', 'hymnbell@lamp', 'bell_agathe@agathesroom', 'ch_agathe@agathesroom'] },
];

// A tuned bell rung at its crystal's pitch shatters it.
function breakCrystals(id) { const r = H.ROOM[id]; for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) if (W.charAt(r.ox + x, r.oy + y) === 'C') W.setChar(r.ox + x, r.oy + y, ' '); }
// The barrel's fire cracks the salt plug in the Breakers.
function breakPlug() { const r = H.ROOM.breakers; for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) if (W.charAt(r.ox + x, r.oy + y) === 'S') W.setChar(r.ox + x, r.oy + y, ' '); }
if (require.main !== module) { module.exports = { analyse, H, W, T, TG }; return; }
let fails = 0;
const dumpArg = process.argv.indexOf('--dump');
const dump = dumpArg > 0 ? { stage: +process.argv[dumpArg + 1], room: process.argv[dumpArg + 2] } : null;
for (const [si, s] of stages.entries()) {
  if (dump && si !== dump.stage) continue;
  W.reset();
  if (s.pool) W.state('saltgate').pool = { basin: 7 };
  if (s.choir !== undefined) W.state('choir').choirLevel = s.choir;
  if (s.breakC) for (let y = 0; y < 23; y++) { const r = H.ROOM.choir; if (W.charAt(r.ox + 4, r.oy + y) === 'C') W.setChar(r.ox + 4, r.oy + y, ' '); if (s.choir === 3 && W.charAt(r.ox + 32, r.oy + y) === 'C') W.setChar(r.ox + 32, r.oy + y, ' '); }
  // salt walls crack in fire (or under a pick), anywhere on the island
  if (s.breakS) for (const r of H.ROOMS) for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) if (W.charAt(r.ox + x, r.oy + y) === 'S') W.setChar(r.ox + x, r.oy + y, ' ');
  if (s.lift) { const r = H.ROOM.priors; for (let y = 17; y < 23; y++) W.setChar(r.ox + 21, r.oy + y, 'H'); }
  if (s.breakB) breakPlug();
  for (const id of s.crystals || []) breakCrystals(id);
  const extra = [];
  if (!s.lift) { const r = H.ROOM.priors; for (let x = 20; x < 24; x++) extra.push([r.ox + x, r.oy + 17]); }
  // rooms walled off whole, to test one door on its own
  for (const id of s.avoid || []) { const r = H.ROOM[id]; for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) extra.push([r.ox + x, r.oy + y]); }
  const seen = analyse(s.abil, s.flags, extra, s.start ? { start: [TG[s.start].x, TG[s.start].y] } : {});
  const miss = (s.need || []).filter((k) => { if (!TG[k]) { console.log('  unknown target', k); return true; } return !near(seen, TG[k]); });
  const wmiss = (s.want || []).filter((k) => TG[k] && !near(seen, TG[k]));
  const rooms = new Set([...seen].map((k) => { const [x, y] = k.split(',').map(Number); return W.roomAtTile(x, y).id; }));
  console.log(`${miss.length ? 'FAIL' : 'ok  '} ${s.name}: ${seen.size} cells, ${rooms.size} rooms${miss.length ? '\n     missing: ' + miss.join(', ') : ''}${wmiss.length ? '\n     optional not reached: ' + wmiss.join(', ') : ''}`);
  const leak = (s.mustNot || []).filter((k) => near(seen, TG[k]));
  if (leak.length) { console.log('     GATE LEAK: ' + leak.join(', ')); fails++; }
  console.log('     rooms: ' + [...rooms].sort().join(' '));
  if (miss.length) fails++;
  if (dump) {
    const r = H.ROOM[dump.room];
    T.setPhase('low', 10);
    for (let y = 0; y < r.th; y++) {
      let line = '';
      for (let x = 0; x < r.tw; x++) { const k = (r.ox + x) + ',' + (r.oy + y); line += seen.has(k) ? '*' : r.map[y][x]; }
      console.log(String(y).padStart(2) + ' ' + line);
    }
  }
}
// Trap check: every reachable cell must be able to get back to the start or a Bell Room.
function trapCheck(name, abil, flags, prep) {
  W.reset();
  prep && prep();
  const seen = analyse(abil, flags, [], { edges: true });
  const N = GW * GH;
  const rev = new Map();
  for (const e of seen.edges) { const a = Math.floor(e / N), b = e % N; if (!rev.has(b)) rev.set(b, []); rev.get(b).push(a); }
  const safe = [];
  const st = TG['start@reef']; safe.push(st.y * GW + st.x);
  for (const [k, t] of Object.entries(TG)) if (k.startsWith('bell_')) for (let dx = -2; dx <= 2; dx++) safe.push(t.y * GW + t.x + dx);
  const back = new Uint8Array(N);
  const q = safe.filter((i) => seen.has((i % GW) + ',' + Math.floor(i / GW)));
  q.forEach((i) => (back[i] = 1));
  while (q.length) { const i = q.pop(); for (const a of rev.get(i) || []) if (!back[a]) { back[a] = 1; q.push(a); } }
  const traps = {};
  for (const k of seen) { const [x, y] = k.split(',').map(Number); if (!back[y * GW + x]) { const r = W.roomAtTile(x, y).id; (traps[r] = traps[r] || []).push([x - H.ROOM[r].ox, y - H.ROOM[r].oy]); } }
  const n = Object.values(traps).reduce((a, b) => a + b.length, 0);
  console.log(`${n ? 'TRAP' : 'ok  '} ${name}: ${n} cells cannot get back` + (n ? '\n' + Object.entries(traps).map(([r, c]) => '     ' + r + ': ' + c.slice(0, 8).map((p) => p.join(',')).join(' ') + (c.length > 8 ? ' (+' + (c.length - 8) + ')' : '')).join('\n') : ''));
  return n;
}
if (!dump) {
  // Every carving must stand somewhere the player can reach with every tool.
  W.reset(); W.state('saltgate').pool = { basin: 7 }; W.state('choir').choirLevel = 3;
  { const r = H.ROOM.priors; for (let y = 17; y < 23; y++) W.setChar(r.ox + 21, r.oy + y, 'H'); }
  { const c = H.ROOM.choir; for (let y = 0; y < 23; y++) for (const x of [4, 32]) if (W.charAt(c.ox + x, c.oy + y) === 'C') W.setChar(c.ox + x, c.oy + y, ' '); }
  for (const r of H.ROOMS) for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) if (W.charAt(r.ox + x, r.oy + y) === 'S') W.setChar(r.ox + x, r.oy + y, ' ');
  breakPlug();
  for (const r of H.ROOMS) if (r.crystals) breakCrystals(r.id);
  const allSeen = analyse(['swing', 'boots', 'jet', 'tidekey', 'seal', 'lens'], { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, liftDropped: true, skullOpen: true, almonerDead: true, precentorDead: true, motherDead: true, undertowDead: true, shoalDead: true });
  const unreached = [];
  for (const r of H.ROOMS) for (const c of H.Narr.carvingSpecs(r, new Set())) if (!near(allSeen, { x: r.ox + c.tx, y: r.oy + c.ty }, 1)) unreached.push(r.id + ' ' + c.tx + ',' + c.ty);
  console.log((unreached.length ? 'FAIL' : 'ok  ') + ' carvings reachable: ' + (unreached.length ? unreached.join('; ') : 'all ' + Object.keys(H.LORE.carvings).length));
  if (unreached.length) fails++;
  fails += trapCheck('traps, early game', ['swing'], { seawallOpen: true }) ? 1 : 0;
  fails += trapCheck('traps, barge solved (Act I)', ['swing'], { seawallOpen: true, basinFilling: true, basinCrossed: true }, () => { W.state('saltgate').pool = { basin: 7 }; }) ? 1 : 0;
  fails += trapCheck('traps, boots (Act II start)', ['swing', 'boots'], { seawallOpen: true, basinFilling: true, basinCrossed: true, abbotDead: true }, () => { W.state('saltgate').pool = { basin: 7 }; }) ? 1 : 0;
  fails += trapCheck('traps, boots, the Almoner dead', ['swing', 'boots'], { seawallOpen: true, basinFilling: true, basinCrossed: true, abbotDead: true, almonerDead: true }, () => { W.state('saltgate').pool = { basin: 7 }; }) ? 1 : 0;
  fails += trapCheck('traps, mid game', ['swing', 'boots', 'jet'], { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true }, () => { W.state('saltgate').pool = { basin: 7 }; }) ? 1 : 0;
  fails += trapCheck('traps, the Choir before the Precentor', ['swing', 'boots', 'jet'], { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true }, () => { W.state('saltgate').pool = { basin: 7 }; breakCrystals('baptistery'); }) ? 1 : 0;
  fails += trapCheck('traps, the Precentor dead', ['swing', 'boots', 'jet'], { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true }, () => { W.state('saltgate').pool = { basin: 7 }; breakCrystals('baptistery'); }) ? 1 : 0;
  fails += trapCheck('traps, Tide Key, shrine water normal', ['swing', 'boots', 'jet', 'tidekey'], { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true }, () => {
    W.state('saltgate').pool = { basin: 7 }; breakCrystals('baptistery'); breakCrystals('bonechoir');
    const c = H.ROOM.choir; for (let y = 0; y < 23; y++) if (W.charAt(c.ox + 4, c.oy + y) === 'C') W.setChar(c.ox + 4, c.oy + y, ' ');
  }) ? 1 : 0;
  fails += trapCheck('traps, the Hanging Mother dead', ['swing', 'boots', 'jet', 'tidekey'], { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, almonerDead: true, precentorDead: true, motherDead: true }, () => {
    W.state('saltgate').pool = { basin: 7 }; breakCrystals('baptistery'); breakCrystals('bonechoir'); W.state('choir').choirLevel = 3;
    const c = H.ROOM.choir; for (let y = 0; y < 23; y++) for (const x of [4, 32]) if (W.charAt(c.ox + x, c.oy + y) === 'C') W.setChar(c.ox + x, c.oy + y, ' ');
  }) ? 1 : 0;
  fails += trapCheck('traps, Choir drained', ['swing', 'boots', 'jet', 'tidekey'], { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, choirDrained: true, almonerDead: true, precentorDead: true }, () => {
    W.state('saltgate').pool = { basin: 7 }; breakCrystals('baptistery'); breakCrystals('bonechoir'); W.state('choir').choirLevel = 21;
    const c = H.ROOM.choir; for (let y = 0; y < 23; y++) if (W.charAt(c.ox + 4, c.oy + y) === 'C') W.setChar(c.ox + 4, c.oy + y, ' ');
  }) ? 1 : 0;
  fails += trapCheck('traps, the Skull before the Shoal', ['swing', 'boots', 'jet', 'tidekey', 'seal'], { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, liftDropped: true, skullOpen: true, almonerDead: true, precentorDead: true, motherDead: true, undertowDead: true }, () => {
    W.state('saltgate').pool = { basin: 7 }; breakCrystals('baptistery'); breakCrystals('bonechoir'); W.state('choir').choirLevel = 3;
    const r = H.ROOM.priors; for (let y = 17; y < 23; y++) W.setChar(r.ox + 21, r.oy + y, 'H');
    const c = H.ROOM.choir; for (let y = 0; y < 23; y++) for (const x of [4, 32]) if (W.charAt(c.ox + x, c.oy + y) === 'C') W.setChar(c.ox + x, c.oy + y, ' ');
  }) ? 1 : 0;
  fails += trapCheck('traps, all tools', ['swing', 'boots', 'jet', 'tidekey', 'seal', 'lens'], { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true, liftDropped: true, skullOpen: true, almonerDead: true, precentorDead: true, motherDead: true, undertowDead: true, shoalDead: true }, () => {
    W.state('saltgate').pool = { basin: 7 }; breakCrystals('baptistery'); breakCrystals('bonechoir'); W.state('choir').choirLevel = 3;
    const r = H.ROOM.priors; for (let y = 17; y < 23; y++) W.setChar(r.ox + 21, r.oy + y, 'H');
    const c = H.ROOM.choir; for (let y = 0; y < 23; y++) for (const x of [4, 32]) if (W.charAt(c.ox + x, c.oy + y) === 'C') W.setChar(c.ox + x, c.oy + y, ' ');
  }) ? 1 : 0;
}
process.exit(fails ? 1 : 0);
