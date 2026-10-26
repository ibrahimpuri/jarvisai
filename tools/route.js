// Route playback: the reachability model plans a path, then an in-page autopilot drives
// Maren along it through the real game input layer and physics.  Usage: node tools/route.js [route...]
'use strict';
const { chromium } = require('playwright');
const path = require('path');
const R = require('./reach.js');
const { analyse, H, W, T } = R;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const ALL = ['swing', 'boots', 'jet', 'tidekey', 'seal', 'lens'];
const BASE = { seawallOpen: true, basinFilling: true, basinCrossed: true, priorWedged: true };
const ROUTES = {
  seawall_to_saltbell: { abil: ['swing'], flags: { seawallOpen: true }, phase: 'low', from: ['seawall', 25, 16], to: ['saltbell', 18, 18] },
  brine_down: { abil: ['swing', 'boots'], flags: BASE, phase: 'low', from: ['saltbell', 4, 18], to: ['cloisterbell', 12, 16] },
  brine_up: { abil: ['swing', 'boots'], flags: BASE, phase: 'low', from: ['cloisterbell', 30, 16], to: ['saltbell', 18, 18] },
  kilns_to_abbot: { abil: ['swing'], flags: BASE, phase: 'low', from: ['kilns', 14, 18], to: ['abbot', 10, 17] },
  jet_chasm: { tracef: true, tracefrom: 3, abil: ['swing', 'boots', 'jet'], flags: BASE, phase: 'low', from: ['priors', 34, 16], to: ['choir', 30, 16] },
  ribs_climb: { abil: ['swing', 'boots', 'jet', 'tidekey'], flags: BASE, phase: 'low', choir: 3, from: ['ribshaft', 9, 44], to: ['ribbell', 11, 16] },
  choir_swim: { abil: ['swing', 'boots', 'jet', 'tidekey'], flags: BASE, phase: 'low', choir: 3, from: ['choir', 36, 16], to: ['ribshaft', 9, 39] },
  prisms_down: { abil: ['swing', 'boots', 'jet', 'tidekey'], flags: BASE, phase: 'low', choir: 3, from: ['ribbell', 30, 16], to: ['prisms', 26, 36] },
  prisms_up: { abil: ['swing', 'boots', 'jet', 'tidekey'], flags: BASE, phase: 'low', choir: 3, breakS: true, from: ['prisms', 26, 36], to: ['prisms', 34, 22] },
  gullet_dive: { abil: ALL, flags: Object.assign({ liftDropped: true }, BASE), phase: 'low', lift: true, from: ['priors', 26, 16], to: ['lair', 40, 17] },
  skull_climb: { abil: ALL, flags: Object.assign({ skullOpen: true }, BASE), phase: 'low', from: ['ribbell', 22, 16], to: ['lamp', 20, 16] },
  pews_high: { abil: ['swing', 'boots'], flags: BASE, phase: 'high', from: ['nave', 20, 16], to: ['cloisterbell', 20, 16] },
};

function plan(rt) {
  W.reset();
  if (rt.choir !== undefined) W.state('choir').choirLevel = rt.choir;
  if (rt.breakS) { const r = H.ROOM.prisms; for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) if (W.charAt(r.ox + x, r.oy + y) === 'S') W.setChar(r.ox + x, r.oy + y, ' '); }
  if (rt.lift) { const r = H.ROOM.priors; for (let y = 17; y < 23; y++) W.setChar(r.ox + 21, r.oy + y, 'H'); }
  W.state('saltgate').pool = { basin: 7 };
  const fr = H.ROOM[rt.from[0]], to = H.ROOM[rt.to[0]];
  const seen = analyse(rt.abil, rt.flags, [], { phases: [[rt.phase, 30]], start: [fr.ox + rt.from[1], fr.oy + rt.from[2]], noSwing: true, dijkstra: true });
  let p = seen.pathTo(to.ox + rt.to[1], to.oy + rt.to[2]);
  if (!p) for (let d = 1; d < 3 && !p; d++) for (const [dx, dy] of [[d, 0], [-d, 0], [0, d], [0, -d]]) { p = p || seen.pathTo(to.ox + rt.to[1] + dx, to.oy + rt.to[2] + dy); }
  return p;
}
// Collapse a cell path into autopilot steps.
function compress(p) {
  const steps = [];
  for (let k = 1; k < p.length; k++) {
    const [x, y, m] = p[k];
    const last = steps[steps.length - 1];
    if (m === 'w' && last && last.t === 'w') { last.x = x; last.y = y; continue; }
    if ((m === 's' || m === 'b') && last && last.t === m && last.dir === Math.sign(x - last.x) + ',' + Math.sign(y - last.y)) { last.x = x; last.y = y; continue; }
    if (m === 's' || m === 'b') { const px0 = last ? last.x : p[k - 1][0], py0 = last ? last.y : p[k - 1][1]; steps.push({ t: m, x, y, dir: Math.sign(x - px0) + ',' + Math.sign(y - py0) }); continue; }
    if (m === 'l' && last && last.t === 'l') { last.x = x; last.y = y; continue; }
    steps.push({ t: m, x, y });
  }
  return steps;
}

const AUTOPILOT = `
window.AP = {
  steps: [], i: 0, held: new Set(), hit: new Set(), up: new Set(), st: {}, done: false, fail: null, log: [], frames: 0,
  start(steps) { this.steps = steps; this.i = 0; this.st = {}; this.done = false; this.fail = null; this.frames = 0; },
  press(a) { this.hit.add(a); this.held.add(a); },
  tick() {
    const p = H.Game.player; this.frames++;
    this.hit.clear(); this.up.clear();
    const wasJump = this.held.has('jump');
    this.held.clear();
    if (H.UI.isOpen()) { H.UI.close(); return; }
    if (this.done || this.fail) return;
    const s = this.steps[this.i];
    if (!s) { this.done = true; return; }
    const st = this.st;
    st.t = (st.t || 0) + 1 / 60;
    const tx = s.x * 16 + 8, cx = p.cx(), px = Math.floor(cx / 16), py = Math.floor((p.y + p.h - 1) / 16);
    const dx = tx - cx;
    const grounded = p.onGround || p.state === 'ladder';
    const next = () => { this.log.push(['done', this.i, s.t, px, py, p.state, +st.t.toFixed(2)]); this.i++; this.st = {}; };
    const steer = (dead) => { if (dx > dead) this.held.add('right'); else if (dx < -dead) this.held.add('left'); };
    if (st.t > 9) { this.fail = { step: this.i, s, at: [px, py], state: p.state }; return; }
    if (window.TRACEF && this.frames % 4 === 0 && (st.t || 0) < 1.5 && this.i >= (window.TRACEFROM || 2)) this.log.push(['f', this.i, px, py, p.state, p.onGround, Math.round(p.vx), Math.round(p.vy), p.dashT > 0 ? 'D' : '', [...this.held].join('+')]);
    if (s.t === 'w') {
      if (p.boots && !H.World.isBrine(cx, p.cy())) this.press('boots');
      steer(2);
      if (Math.abs(dx) < 4 && py === s.y && grounded) return next();
      if (p.inWater() && Math.abs(dx) < 8 && Math.abs(py - s.y) <= 2 && this.steps[this.i + 1] && 'sb'.includes(this.steps[this.i + 1].t)) return next();
      if (p.inWater() && Math.abs(dx) < 4 && Math.abs(py - s.y) <= 1) return next();
      // nudge over small lips
      if (p.hitWall && p.onGround) this.press('jump');
      return;
    }
    if (s.t === 'j') {
      if (!st.jumped) {
        if (p.state === 'ladder') { this.press('jump'); steer(1); st.jumped = true; st.air = 0; return; }
        if (!grounded && !(p.inWater() && !H.World.isWater(cx, p.y - 2))) { steer(3); return; }
        // like a player, wait out a gust before jumping
        if (p.windAt && p.windAt() !== 0) return;
        // run-up only when the target is far
        const ahead = Math.floor((cx + Math.sign(dx) * 10) / 16);
        const groundAhead = H.World.solid(ahead, py + 1) >= 1;
        if (Math.abs(dx) > 5.5 * 16 && groundAhead) { steer(2); return; }
        this.press('jump'); steer(1); st.jumped = true; st.air = 0; return;
      }
      st.air += 1 / 60;
      if (st.air < 0.42 && (wasJump || st.air < 0.05)) this.held.add('jump');
      // Jet dash toward far targets at the top of the jump
      if (p.has('jet') && !st.dashed && st.air > 0.18 && (Math.abs(dx) > 4.5 * 16 || (s.y < py - 2))) {
        st.dashed = true; this.press('dash');
        if (s.y < py - 2) this.held.add('up');
        if (Math.abs(dx) > 2 * 16) steer(0);
      }
      steer(3);
      // catch a ladder at the end of the jump
      const ladderT = H.World.isLadder(s.x, s.y) || H.World.isLadder(s.x, s.y - 1);
      if (ladderT && Math.abs(dx) < 12) this.held.add('up');
      if (ladderT && p.state === 'ladder' && Math.abs(dx) < 12) return next();
      if (st.air > 0.1 && (p.onGround || p.state === 'ladder' || p.inWater())) {
        if (py === s.y && Math.abs(dx) < 14) return next();
        if (Math.abs(py - s.y) <= 0 && Math.abs(dx) < 3 * 16) { this.steps.splice(this.i + 1, 0, { t: 'w', x: s.x, y: s.y }); return next(); }
        st.tries = (st.tries || 0) + 1; st.jumped = false; this.log.push(['retry', this.i, px, py]);
        if (st.tries > 4) this.fail = { step: this.i, s, at: [px, py], state: p.state };
      }
      return;
    }
    if (s.t === 'l') {
      if (p.state !== 'ladder') { steer(2); if (Math.abs(dx) < 6) this.held.add(s.y < py ? 'up' : 'down'); }
      else this.held.add(s.y < py ? 'up' : 'down');
      if (py === s.y || (p.state !== 'ladder' && py === s.y && grounded)) return next();
      if (p.state !== 'ladder' && Math.abs(py - s.y) <= 1 && grounded && st.t > 0.3) return next();
      return;
    }
    if (s.t === 'd') { this.held.add('down'); this.press('jump'); if (st.t > 0.4) next(); return; }
    if (s.t === 's' || s.t === 'b') {
      // Brine: boots on to sink, off to float.
      if (s.t === 'b') {
        const off = (s.y + 1) * 16 - 11 - p.cy();
        if (off > 14 && !p.boots) this.press('boots');
        else if (off < -14 && p.boots) this.press('boots');
      }
      const ty = (s.y + 1) * 16 - 11;
      const dy = ty - p.cy();
      steer(3);
      if (dy < -3) this.held.add('up'); else if (dy > 3) this.held.add('down');
      if (dy < -10 && !H.World.isWater(cx, p.y - 2)) this.press('jump');
      if (Math.abs(dx) < 6 && Math.abs(dy) < 10) return next();
      return;
    }
    // swing or unknown: skip
    next();
  },
};
H.Input.held = (a) => AP.held.has(a);
H.Input.hit = (a) => AP.hit.has(a);
H.Input.up = (a) => AP.up.has(a);
const _ef = H.Input.endFrame; H.Input.endFrame = () => { _ef(); AP.tick(); };
`;

(async () => {
  const names = process.argv.slice(2).length ? process.argv.slice(2) : Object.keys(ROUTES);
  const browser = await chromium.launch();
  let fails = 0;
  for (const n of names) {
    const rt = ROUTES[n];
    const p = plan(rt);
    if (!p) { console.log('NOPLAN ' + n); fails++; continue; }
    const steps = compress(p);
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    const errors = [];
    page.on('pageerror', (e) => errors.push(e.message));
    await page.goto('file://' + path.join(__dirname, '..', 'index.html'));
    await sleep(300);
    await page.click('button[data-act="newgame"]'); await sleep(100); await page.keyboard.press('Escape'); await sleep(50);
    await page.evaluate(AUTOPILOT);
    await page.evaluate(([rt, steps]) => {
      const G = H.Game, p = G.player;
      G.god = true; rt.abil.forEach((a) => p.abilities.add(a));
      Object.assign(H.World.flags, rt.flags);
      if (rt.choir !== undefined) { H.World.state('choir').choirLevel = rt.choir; H.World.flags.choirTarget = rt.choir; H.World.flags.choirLevelName = rt.choir === 3 ? 'raised' : 'mid'; }
      H.World.state('saltgate').pool = { basin: 7 }; H.World.flags.basinTarget = 7;
      if (rt.breakS) { const r = H.ROOM.prisms; for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) if (H.World.charAt(r.ox + x, r.oy + y) === 'S') H.World.setChar(r.ox + x, r.oy + y, ' '); }
      if (rt.lift) { const r = H.ROOM.priors; for (let y = 17; y < 23; y++) H.World.setChar(r.ox + 21, r.oy + y, 'H'); H.World.state('priors').ents['lift@20,17'] = { done: true }; }
      H.Tide.setPhase(rt.phase, 30); H.Tide.paused = true;
      const r = H.ROOM[rt.from[0]];
      G.enterRoom(r);
      G.ents.forEach((e) => { if (e.isBody) e.gone = true; });
      p.x = r.px + rt.from[1] * 16 + 3; p.y = r.py + (rt.from[2] + 1) * 16 - p.h;
      G.snapCamera();
      AP.start(steps); window.TRACEF = !!rt.tracef; window.TRACEFROM = rt.tracefrom || 2;
    }, [rt, steps]);
    let res;
    for (let k = 0; k < 600; k++) {
      await sleep(100);
      res = await page.evaluate(() => ({ done: AP.done, fail: AP.fail, i: AP.i, n: AP.steps.length, room: H.Game.room.id, log: AP.log.slice(-30) }));
      if (res.done || res.fail) break;
      await page.evaluate(() => { H.Game.ents.forEach((e) => { if (e.isBody) e.gone = true; }); });
    }
    const ok = res.done && res.room === rt.to[0];
    if (!ok) { fails++; await page.screenshot({ path: path.join(process.env.OUT || '.', 'route_' + n + '.png') }); }
    console.log(`${ok ? 'PASS' : 'FAIL'} ${n}: ${res.i}/${res.n} steps, ended in ${res.room}${res.fail ? ' stuck at ' + JSON.stringify(res.fail) : ''}${errors.length ? ' errors: ' + errors.slice(0, 2) : ''}`);
    if (!ok || process.env.TRACE) console.log('   log: ' + JSON.stringify(res.log));
    if (!ok) console.log('   plan: ' + steps.map((s) => { const r = H.World.roomAtTile(s.x, s.y); return s.t + '(' + r.id.slice(0, 5) + ' ' + (s.x - r.ox) + ',' + (s.y - r.oy) + ')'; }).join(' '));
    await page.close();
  }
  await browser.close();
  process.exit(fails ? 1 : 0);
})();
