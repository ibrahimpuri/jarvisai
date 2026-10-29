// Does the guide's way play? For each step of the main mission, with the island as it is when that step is the
// objective, take the rooms the guide leads through from the step before, wall off every other room, and check that
// Maren's movement (tools/reach.js) still gets her from one to the other. A failure means the guide sends a player
// through a room they cannot cross that way.
//   node tools/guidecheck.js
'use strict';
const { analyse, H, W, TG, breakCrystals } = require('./reach.js');

const GATES = 'KZQJUNOYh';            // doors the story holds shut
const TIDAL = 'LEV';                  // doors the tide opens; she can wait for them
function liveOpen(x, y) { const c = W.charAt(x, y); return W.solid(x, y) !== 1 || TIDAL.includes(c); }
function openAny(c) { return W.solidity(c) !== 1 || 'LEKZVYhQJUNO'.includes(c); }

// the rooms' seams, as godot/src/Game/Guide.cs builds them
const seams = {};
for (const r of H.ROOMS) {
  const links = seams[r.id] = {};
  for (let ty = 0; ty < r.th; ty++) for (let tx = 0; tx < r.tw; tx++) {
    if (tx !== 0 && ty !== 0 && tx !== r.tw - 1 && ty !== r.th - 1) continue;
    const wx = r.ox + tx, wy = r.oy + ty;
    if (!openAny(r.map[ty][tx])) continue;
    for (const [dx, dy] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) {
      const nx = tx + dx, ny = ty + dy;
      if (nx >= 0 && ny >= 0 && nx < r.tw && ny < r.th) continue;
      const nr = W.roomAtTile(wx + dx, wy + dy);
      if (!nr || nr === r || !openAny(nr.map[wy + dy - nr.oy][wx + dx - nr.ox])) continue;
      (links[nr.id] = links[nr.id] || []).push([wx, wy]);
    }
  }
}
function seamOpen(seam) {
  for (const [x, y] of seam) {
    if (!liveOpen(x, y)) continue;
    for (const [dx, dy] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) {
      const nr = W.roomAtTile(x + dx, y + dy);
      if (nr && nr !== W.roomAtTile(x, y) && liveOpen(x + dx, y + dy)) return true;
    }
  }
  return false;
}
// the rooms that only play one way (noEntryFrom, noThrough), as the guide reads them
function lifted(unless, abil) {
  if (!unless) return false;
  if (unless.startsWith('salt:')) { const r = H.ROOM[unless.slice(5)]; for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) if (W.charAt(r.ox + x, r.oy + y) === 'S') return false; return true; }
  return abil.includes(unless);
}
function cannotEnter(room, from, abil) { return (H.ROOM[room].noEntryFrom || []).some((e) => (Array.isArray(e) ? e[0] : e) === from && !lifted(Array.isArray(e) ? e[1] : null, abil)); }
function cannotCross(came, room, to, abil) { return (H.ROOM[room].noThrough || []).some(([a, b, unless]) => a === came && b === to && !lifted(unless, abil)); }
// how each room plays with each set of tools (tools/throughs.js), as the guide reads it
const THROUGH = (() => { try { return JSON.parse(require('fs').readFileSync(require('path').join(__dirname, '..', 'godot', 'data', 'through.json'), 'utf8')).tiers; } catch (e) { return {}; } })();
function ways(abil) { return THROUGH[abil.includes('seal') ? 'seal' : abil.includes('tidekey') ? 'tidekey' : abil.includes('jet') ? 'jet' : abil.includes('boots') ? 'boots' : 'swing']; }
function tableEnter(room, from, abil) { const w = ways(abil); return !w || !w[room] || from in w[room]; }
function tablePlays(came, room, to, abil) { const w = ways(abil); return !came || !w || !w[room] || !w[room][came] || w[room][came].includes(to); }
// searched over (room, the room she came in from), as godot/src/Game/Guide.cs does
function route(from, to, live, abil) {
  const key = (r, c) => r + '<' + (c || '');
  const prev = { [key(from)]: null }, q = [[from, null]];
  let end = null;
  while (q.length) {
    const [a, came] = q.shift(); if (a === to) { end = [a, came]; break; }
    for (const [b, seam] of Object.entries(seams[a])) {
      const k = key(b, a);
      if (k in prev || (live && !seamOpen(seam)) || cannotEnter(b, a, abil) || cannotCross(came, a, b, abil) || !tableEnter(b, a, abil) || !tablePlays(came, a, b, abil)) continue;
      prev[k] = [a, came]; q.push([b, a]);
    }
  }
  if (!end) return null;
  const p = []; for (let c = end; c; c = prev[key(c[0], c[1])]) p.unshift(c[0]);
  return p;
}

const F0 = { seawallOpen: true }, F1 = { ...F0, basinFilling: true, basinCrossed: true }, F2 = { ...F1, abbotDead: true }, F3 = { ...F2, almonerDead: true },
  F4 = { ...F3, priorWedged: true }, F5 = { ...F4, precentorDead: true }, F6 = { ...F5, motherDead: true }, F7 = { ...F6, liftDropped: true },
  F8 = { ...F7, undertowDead: true, skullOpen: true }, F9 = { ...F8, shoalDead: true };
const A0 = ['swing'], A1 = [...A0, 'boots'], A2 = [...A1, 'jet'], A3 = [...A2, 'tidekey'], A4 = [...A3, 'seal'];
// each step, with what she has and how the island stands while it is the objective (its own puzzle taken as solved)
const LEGS = [
  ['ashore', 'reef', 'pier', A0, {}], ['chapel', 'pier', 'chapel', A0, {}], ['crank', 'chapel', 'tithe', A0, {}], ['gate', 'tithe', 'seawall', A0, {}],
  ['climb', 'seawall', 'saltgate', A0, F0], ['pans', 'saltgate', 'kilns', A0, F1, { pool: 1 }], ['abbot', 'kilns', 'abbot', A0, F1, { pool: 1 }],
  ['cloister', 'abbot', 'cloister', A1, F2, { pool: 1 }], ['west', 'cloister', 'refectory', A1, F2, { pool: 1 }], ['prior', 'refectory', 'priors', A1, F3, { pool: 1 }],
  ['choir', 'priors', 'choir', A2, F4, { pool: 1 }], ['organ', 'choir', 'organloft', A2, F4, { pool: 1, crystals: ['baptistery'] }],
  ['tidekey', 'organloft', 'tuning', A2, F5, { pool: 1, crystals: ['baptistery'] }],
  ['ribvault', 'tuning', 'ribbell', A3, F5, { pool: 1, crystals: ['baptistery', 'bonechoir'], choir: 3 }],
  ['spine', 'ribbell', 'spine', A3, F5, { pool: 1, crystals: ['baptistery', 'bonechoir'], choir: 3 }],
  ['seal', 'spine', 'prisms', A3, F6, { pool: 1, crystals: ['baptistery', 'bonechoir'], choir: 3, salt: 1 }],
  ['gullet', 'prisms', 'gulletmouth', A4, F7, { pool: 1, crystals: ['baptistery', 'bonechoir'], choir: 3, salt: 1, lift: 1 }],
  ['undertow', 'gulletmouth', 'lair', A4, F7, { pool: 1, crystals: ['baptistery', 'bonechoir'], choir: 3, salt: 1, lift: 1 }],
  ['skull', 'lair', 'skullstair', A4, F8, { pool: 1, crystals: ['baptistery', 'bonechoir'], choir: 3, salt: 1, lift: 1 }],
  ['crown', 'skullstair', 'crown', A4, F8, { pool: 1, crystals: ['baptistery', 'bonechoir'], choir: 3, salt: 1, lift: 1 }],
  ['shoal', 'crown', 'brainpan', A4, F8, { pool: 1, crystals: ['baptistery', 'bonechoir'], choir: 3, salt: 1, lift: 1 }],
  ['agathe', 'brainpan', 'lamp', A4, F9, { pool: 1, crystals: ['baptistery', 'bonechoir'], choir: 3, salt: 1, lift: 1 }],
];

let fails = 0;
const only = process.argv[2] && process.argv[2] !== '--bells' ? process.argv[2] : null;
// --bells: also from each Bell Room of the region she sets out from, where she wakes after dying
const legs = [];
for (const leg of LEGS) {
  legs.push(leg);
  if (process.argv.includes('--bells')) for (const r of H.ROOMS) if (r.bellRoom && r.region === H.ROOM[leg[1]].region && r.id !== leg[1] && r.id !== leg[2]) legs.push([leg[0] + '@' + r.id, r.id, ...leg.slice(2)]);
}
for (const [step, from, to, abil, flags, o = {}] of legs) {
  if (only && !only.split(',').includes(step)) continue;
  W.reset(); W.flags = Object.assign({}, flags);
  if (o.pool) W.state('saltgate').pool = { basin: 7 };
  if (o.choir) {
    W.state('choir').choirLevel = o.choir;
    const c = H.ROOM.choir; for (let y = 0; y < 23; y++) for (const x of [4, 32]) if (W.charAt(c.ox + x, c.oy + y) === 'C') W.setChar(c.ox + x, c.oy + y, ' ');
  }
  for (const id of o.crystals || []) breakCrystals(id);
  if (o.salt) for (const r of H.ROOMS) for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) if (W.charAt(r.ox + x, r.oy + y) === 'S') W.setChar(r.ox + x, r.oy + y, ' ');
  if (o.lift) { const r = H.ROOM.priors; for (let y = 17; y < 23; y++) W.setChar(r.ox + 21, r.oy + y, 'H'); }
  const way = route(from, to, true, abil) || route(from, to, false, abil);
  if (!way) { console.log(`FAIL ${step}: no route ${from} -> ${to}`); fails++; continue; }
  const keep = new Set(way), extra = [];
  for (const r of H.ROOMS) if (!keep.has(r.id)) for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) extra.push([r.ox + x, r.oy + y]);
  if (!o.lift) { const r = H.ROOM.priors; for (let x = 20; x < 24; x++) extra.push([r.ox + x, r.oy + 17]); }
  // start where the step before leaves her: at its thing, or anywhere she can stand in its room
  const inRoom = Object.entries(TG).filter(([k, v]) => v.room === from);
  const start = ['start', 'agathe', 'hesper', 'ansel', 'teodor', 'odile', 'bell_', 'crank', 'socket', 'kiln', 'lampwork', 'chest', 'tunedbell', 'shoal']
    .map((p) => inRoom.find(([k]) => k.startsWith(p))).find(Boolean) || inRoom.find(([k]) => !k.startsWith('seal') && !k.startsWith('stitch')) || inRoom[0];
  const seen = analyse(abil, flags, extra, { start: [start[1].x, start[1].y] });
  const there = [...seen].some((k) => { const [x, y] = k.split(',').map(Number); return W.roomAtTile(x, y)?.id === to; });
  const reached = new Set([...seen].map((k) => { const [x, y] = k.split(',').map(Number); return W.roomAtTile(x, y)?.id; }));
  const stops = way.filter((id) => !reached.has(id));
  console.log(`${there ? 'ok  ' : 'FAIL'} ${step}: ${way.join(' > ')}` + (there ? '' : `\n     from ${start[0]}; gets no further than ${way.filter((id) => reached.has(id)).pop()} (never reaches ${stops.join(', ')})`));
  if (!there) {
    fails++;
    // the way that does play: the cheapest path the movement model finds with the whole island open, room by room
    const all = analyse(abil, flags, o.lift ? [] : (() => { const r = H.ROOM.priors; return [20, 21, 22, 23].map((x) => [r.ox + x, r.oy + 17]); })(), { start: [start[1].x, start[1].y], dijkstra: true });
    const goal = Object.values(TG).find((v) => v.room === to);
    let path = null;
    if (goal) for (let d = 0; d < 4 && !path; d++) for (let dx = -d; dx <= d && !path; dx++) for (let dy = -d; dy <= d + 1 && !path; dy++) path = all.pathTo(goal.x + dx, goal.y + dy);
    if (path) {
      const rooms = []; for (const [x, y] of path) { const id = W.roomAtTile(x, y)?.id; if (id && rooms[rooms.length - 1] !== id) rooms.push(id); }
      console.log('     the way that plays: ' + rooms.join(' > '));
    } else { console.log('     (n/a: ' + from + ' cannot be reached at this point)'); fails--; }
  }
}
process.exit(fails ? 1 : 0);
