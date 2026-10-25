// Noise, light, fire, particles and projectiles: the sandbox systems every room shares.
'use strict';
H.Noise = {
  // Every action has a noise radius in metres (tiles).
  emit(x, y, metres, src) {
    const G = H.Game;
    if (!G || !G.ents) return;
    const r = metres * H.M;
    G.noiseRings.push({ x, y, r, t: 0.5 });
    for (const e of G.ents) if (e.hear && !e.dead) {
      const d = H.dist(x, y, e.cx(), e.cy());
      if (d <= r) e.hear(x, y, d, r, src);
    }
  },
};

H.FX = {
  parts: [],
  spawn(x, y, n, opt = {}) {
    for (let i = 0; i < n; i++) {
      const a = opt.ang !== undefined ? opt.ang + H.rand(-opt.spread || 0, opt.spread || 0) : H.rand(0, Math.PI * 2);
      const s = H.rand(opt.smin || 20, opt.smax || 90);
      this.parts.push({
        x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s,
        life: H.rand(opt.lmin || 0.3, opt.lmax || 0.8), max: 1,
        color: opt.color || '#ddd', size: opt.size || 1.5, grav: opt.grav !== undefined ? opt.grav : 200,
        glow: !!opt.glow,
      });
      const p = this.parts[this.parts.length - 1]; p.max = p.life;
    }
  },
  salt(x, y, n = 8) { this.spawn(x, y, n, { color: '#e8f4ff', size: 1.6, smin: 40, smax: 140, glow: true }); },
  blood(x, y, n = 5) { this.spawn(x, y, n, { color: '#3a4a40', size: 1.6 }); },
  splash(x, y, n = 10) { this.spawn(x, y, n, { color: '#9fc4d0', ang: -Math.PI / 2, spread: 1.0, smin: 40, smax: 130, grav: 400 }); },
  sparks(x, y, n = 6) { this.spawn(x, y, n, { color: '#ffd080', size: 1.2, smin: 60, smax: 160, glow: true, lmin: 0.1, lmax: 0.3 }); },
  bubbles(x, y, n = 1) { this.spawn(x, y, n, { color: '#bfe0ff', ang: -Math.PI / 2, spread: 0.4, smin: 15, smax: 35, grav: -40, lmin: 0.6, lmax: 1.4 }); },
  embers(x, y, n = 2) { this.spawn(x, y, n, { color: '#ff8a30', ang: -Math.PI / 2, spread: 0.6, smin: 10, smax: 50, grav: -30, glow: true }); },
  update(dt) {
    for (const p of this.parts) {
      p.life -= dt; p.vy += p.grav * dt; p.x += p.vx * dt; p.y += p.vy * dt;
    }
    this.parts = this.parts.filter((p) => p.life > 0);
  },
};

H.Fire = {
  cells: new Map(), // "tx,ty" -> {t, spread}
  clear() { this.cells.clear(); },
  ignite(tx, ty, t = 6) {
    const W = H.World;
    const k = tx + ',' + ty;
    if (W.solid(tx, ty) === 1) return;
    const px = tx * H.TILE + 8, py = ty * H.TILE + 8;
    if (W.isWater(px, py)) return;
    if (!this.cells.has(k)) this.cells.set(k, { tx, ty, t, spread: 0.7 });
  },
  spill(x, y, radiusTiles) {
    const tx0 = Math.floor(x / H.TILE), ty0 = Math.floor(y / H.TILE);
    for (let dx = -radiusTiles; dx <= radiusTiles; dx++) {
      for (let dy = -2; dy <= 1; dy++) {
        const tx = tx0 + dx, ty = ty0 + dy;
        // Oil pools on top of solid ground.
        if (H.World.solid(tx, ty) !== 1 && H.World.solid(tx, ty + 1) >= 1) this.ignite(tx, ty, 6 + Math.random() * 2);
      }
    }
    H.Audio.fire();
    H.Noise.emit(x, y, 8, 'fire');
  },
  burnsAt(x, y) { return this.cells.has(Math.floor(x / H.TILE) + ',' + Math.floor(y / H.TILE)); },
  update(dt) {
    const W = H.World;
    const add = [];
    for (const [k, c] of this.cells) {
      c.t -= dt; c.spread -= dt;
      const px = c.tx * H.TILE + 8, py = c.ty * H.TILE + 8;
      if (W.isWater(px, py)) c.t = 0;
      if (Math.random() < dt * 6) H.FX.embers(px + H.rand(-6, 6), py + 6, 1);
      if (c.spread <= 0) {
        c.spread = 0.9;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, -1], [0, 1], [1, 1], [-1, 1], [1, -1], [-1, -1]]) {
          const tx = c.tx + dx, ty = c.ty + dy;
          const ch = W.charAt(tx, ty);
          // Fire spreads along timber and rigging, and cracks salt walls.
          if (ch === 'b') { W.setChar(tx, ty, ' '); add.push([tx, ty, 4]); H.FX.embers(tx * 16 + 8, ty * 16 + 8, 6); }
          else if (ch === 'S') { W.setChar(tx, ty, ' '); H.FX.salt(tx * 16 + 8, ty * 16 + 8, 12); H.Audio.crack(); }
          else if ((ch === 'T' || ch === 'P' || ch === '=') && Math.random() < 0.35) {
            // burn the air cell beside the timber
            for (const [ex, ey] of [[0, -1], [1, 0], [-1, 0]]) {
              if (W.solid(tx + ex, ty + ey) === 0 && !this.cells.has((tx + ex) + ',' + (ty + ey))) { add.push([tx + ex, ty + ey, 3]); break; }
            }
          }
        }
      }
      if (c.t <= 0) this.cells.delete(k);
    }
    for (const [tx, ty, t] of add) this.ignite(tx, ty, t);
  },
};

// Rivet: hitscan. Returns {kind:'knot'|'body'|'wall'|'none', target, knot, x, y}
H.hitscan = (x, y, ang, maxD, opts = {}) => {
  const G = H.Game;
  const dx = Math.cos(ang), dy = Math.sin(ang);
  const wallD = H.Phys.ray(x, y, ang, maxD);
  const ex = x + dx * wallD, ey = y + dy * wallD;
  let best = { kind: wallD < maxD ? 'wall' : 'none', t: 1, x: ex, y: ey };
  for (const e of G.ents) {
    if (!e.isBody || e.dead || e.gone || e === opts.ignore) continue;
    if (e.knots) for (const k of e.knots) {
      if (k.broken) continue;
      const kp = e.knotPos(k);
      if (!kp) continue;
      const t = H.segCircle(x, y, ex, ey, kp.x, kp.y, k.r || 4.5);
      if (t >= 0 && t < best.t) best = { kind: 'knot', t, target: e, knot: k, x: x + (ex - x) * t, y: y + (ey - y) * t };
    }
  }
  if (best.kind !== 'knot') {
    for (const e of G.ents) {
      if (!e.isBody || e.dead || e.gone || e === opts.ignore) continue;
      const b = e.hitBox();
      const t = rayRect(x, y, ex, ey, b);
      if (t >= 0 && t < best.t) best = { kind: 'body', t, target: e, x: x + (ex - x) * t, y: y + (ey - y) * t };
    }
  }
  return best;
};

function rayRect(x1, y1, x2, y2, r) {
  let t0 = 0, t1 = 1;
  const dx = x2 - x1, dy = y2 - y1;
  const p = [-dx, dx, -dy, dy];
  const q = [x1 - r.x, r.x + r.w - x1, y1 - r.y, r.y + r.h - y1];
  for (let i = 0; i < 4; i++) {
    if (p[i] === 0) { if (q[i] < 0) return -1; }
    else {
      const t = q[i] / p[i];
      if (p[i] < 0) { if (t > t1) return -1; if (t > t0) t0 = t; }
      else { if (t < t0) return -1; if (t < t1) t1 = t; }
    }
  }
  return t0;
}
H.rayRect = rayRect;

// ---- projectiles -----------------------------------------------------------
class Projectile {
  constructor(kind, x, y, vx, vy, opt = {}) {
    Object.assign(this, { kind, x, y, vx, vy, life: opt.life || 3, grav: opt.grav || 0, opt, dead: false, stuck: null });
  }
  update(dt) {
    const G = H.Game;
    if (this.stuck) { this.updateStuck(dt); return; }
    const nx = this.x + this.vx * dt, ny = this.y + this.vy * dt;
    this.vy += this.grav * dt;
    if (H.World.isWater(this.x, this.y)) { this.vx *= 1 - 2 * dt; this.vy *= 1 - 2 * dt; }
    // Hit bodies: knots first.
    const ang = Math.atan2(ny - this.y, nx - this.x);
    const d = Math.hypot(nx - this.x, ny - this.y);
    const hit = H.hitscan(this.x, this.y, ang, d + 1, {});
    if (hit.kind === 'knot' || hit.kind === 'body') { this.onHit(hit); return; }
    if (hit.kind === 'wall') { this.x = hit.x - Math.cos(ang) * 2; this.y = hit.y - Math.sin(ang) * 2; this.onWall(); return; }
    this.x = nx; this.y = ny;
    this.life -= dt;
    if (this.life <= 0) this.expire();
    if (G.room && (this.x < G.room.px - 50 || this.x > G.room.px + G.room.pw + 50 || this.y > G.room.py + G.room.ph + 50)) this.dead = true;
  }
  onHit(hit) {
    const G = H.Game, P = G.player;
    if (this.kind === 'bolt') {
      H.Audio.thud();
      if (hit.kind === 'knot') {
        hit.target.damageKnot(hit.knot, 1, 'harpoon');
        if (!hit.target.dead && hit.target.pin) hit.target.pin(hit.knot, 4, P.weaponTier('harpoon'));
      } else hit.target.damageFlesh(1, hit.x, hit.y, 'harpoon');
      // The bolt rides the body; it drops when the pin ends.
      this.stuck = { body: hit.target, t: 4 };
      this.x = hit.x; this.y = hit.y;
    } else if (this.kind === 'flare') {
      if (P.weaponTier('sling') >= 1) { this.stuck = { body: hit.target, ox: hit.x - hit.target.cx(), oy: hit.y - hit.target.cy() }; }
      else { this.vx *= -0.3; this.vy = -60; }
    }
  }
  onWall() {
    if (this.kind === 'bolt') {
      H.Audio.thud(); H.FX.sparks(this.x, this.y, 4);
      H.Game.spawnLoose('bolts', this.x, this.y);
      this.dead = true;
    } else if (this.kind === 'flare') {
      this.vx *= 0.3; this.vy = 0; this.grav = 0;
      this.stuck = { wall: true };
    }
  }
  updateStuck(dt) {
    if (this.kind === 'bolt') {
      const b = this.stuck.body;
      this.stuck.t -= dt;
      if (b && !b.dead) { this.x = b.cx(); this.y = b.cy(); }
      if (this.stuck.t <= 0 || !b || b.dead || b.gone) { H.Game.spawnLoose('bolts', this.x, this.y); this.dead = true; }
      return;
    }
    if (this.kind === 'flare') {
      const b = this.stuck.body;
      if (b && !b.dead && !b.gone) { this.x = b.cx() + this.stuck.ox; this.y = b.cy() + this.stuck.oy; }
      else if (this.stuck.body) { this.stuck = { wall: true }; }
      if (this.stuck.wall && !H.Phys.boxSolid(this.x - 1, this.y + 1, 2, 2)) this.y += 60 * dt;
      this.life -= dt;
      if (this.life <= 0) this.dead = true;
    }
  }
  expire() { this.dead = true; }
  light() {
    if (this.kind === 'flare') return { x: this.x, y: this.y, r: 110 + Math.sin(performance.now() / 60) * 6, i: 1, color: '255,140,60' };
    return null;
  }
  draw(c) {
    if (this.kind === 'bolt') {
      const a = this.stuck ? 0 : Math.atan2(this.vy, this.vx);
      c.save(); c.translate(this.x, this.y); c.rotate(a);
      c.strokeStyle = '#b8c4cc'; c.lineWidth = 1.5; c.beginPath(); c.moveTo(-9, 0); c.lineTo(5, 0); c.stroke();
      c.fillStyle = '#dfe8ee'; c.beginPath(); c.moveTo(5, -2); c.lineTo(9, 0); c.lineTo(5, 2); c.fill();
      c.restore();
    } else if (this.kind === 'flare') {
      c.fillStyle = '#ffd0a0'; c.fillRect(this.x - 1.5, this.y - 1.5, 3, 3);
      if (Math.random() < 0.5) H.FX.embers(this.x, this.y, 1);
    }
  }
}
H.Projectile = Projectile;

// A Saw-Line: a wire at knee height that cuts the knots of anything crossing it.
class SawLine {
  constructor(x1, x2, y, rewinds) { Object.assign(this, { x1: Math.min(x1, x2), x2: Math.max(x1, x2), y, cuts: 3, dead: false, rewinds, cooldown: new Map() }); }
  update(dt) {
    const G = H.Game;
    for (const e of G.ents) {
      if (!e.isBody || e.dead || e.gone || e.mode === 'downed' || e.mode === 'dormant') continue;
      const b = e.hitBox();
      if (b.x + b.w > this.x1 && b.x < this.x2 && b.y < this.y && b.y + b.h > this.y) {
        const cd = this.cooldown.get(e) || 0;
        if (cd > 0) continue;
        this.cooldown.set(e, 1.0);
        const k = e.knots && e.knots.find((k) => !k.broken && !k.sealed && (k.type === 'knee' || k.type === 'spine')) ||
                  e.knots && e.knots.find((k) => !k.broken && !k.sealed && !k.plated);
        if (k) { e.damageKnot(k, 2, 'saw'); H.FX.sparks(e.cx(), this.y, 6); H.Audio.crack(); }
        this.cuts--;
      }
    }
    for (const [e, t] of this.cooldown) this.cooldown.set(e, t - dt);
    if (this.cuts <= 0) {
      this.dead = true;
      if (this.rewinds) { G.player.bag.add('spools', 1); H.toast('The Saw-Line rewinds itself.'); }
    }
  }
  draw(c) {
    c.strokeStyle = 'rgba(220,220,230,0.8)'; c.lineWidth = 1;
    c.beginPath(); c.moveTo(this.x1, this.y); c.lineTo(this.x2, this.y); c.stroke();
    c.fillStyle = '#888'; c.fillRect(this.x1 - 1, this.y - 2, 3, 4); c.fillRect(this.x2 - 2, this.y - 2, 3, 4);
  }
}
H.SawLine = SawLine;
