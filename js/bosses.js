// The Undertow (stalker and final hunter), the Abbot of Salt, and the Shoal.
'use strict';
(() => {
  const TS = H.TILE;
  const G = () => H.Game;
  const P = () => H.Game.player;
  const Body = H.Body;

  // ---------------------------------------------------------------- Undertow (live body)
  class UndertowBody extends Body {
    constructor(spec, room) {
      super(spec, room);
      this.type = 'undertow';
      this.boss = !!spec.boss;
      this.w = 64; this.h = 30;
      this.x = spec.x - this.w / 2; this.y = spec.y - this.h / 2;
      this.addKnot('buoy', 'buoy', 'buoy', this.boss ? 24 : 24, { r: 7 });
      if (this.boss) {
        this.addKnot('ropeL', 'rope', 'ropeL', 8, { r: 5, needsBuoy: true });
        this.addKnot('ropeR', 'rope', 'ropeR', 8, { r: 5, needsBuoy: true });
        this.addKnot('crown', 'crown', 'core', 12, { r: 7, sealedNeeds: 3 });
      }
      this.mode = 'hunt'; this.awakeMode = 'hunt';
      this.leaving = false; this.bellT = 0; this.stunT = 0;
      this.dir = spec.dir || 1;
      this.facing = this.dir;
    }
    isSealed(k) {
      if (k.needsBuoy) return !this.knot('buoy').broken;
      return super.isSealed(k);
    }
    skeleton() {
      const f = this.facing, x = this.cx(), y = this.cy();
      const s = Math.sin(this.anim * 2) * 2;
      return {
        buoy: { x: x - f * 6, y: y - 16 + s },
        ropeL: { x: x + f * 18, y: y + 6 }, ropeR: { x: x - f * 22, y: y + 8 },
        core: { x: x + f * 4, y: y + 2 },
        head: { x: x + f * 28, y: y },
      };
    }
    hear() {}
    wake() {}
    onKnotBroken(k, src) {
      if (k.id === 'buoy') {
        if (!this.boss) {
          this.leaving = true;
          H.toast('The buoy knot shatters. The Undertow sinks away for the rest of this tide.');
          G().undertow.driveOff();
          H.Audio.buoy(0.6, this.pan());
        } else {
          this.stunT = 7; H.Audio.buoy(0.7, this.pan()); H.toast('It reels. The bell-ropes are exposed!');
        }
      }
      if (k.type === 'crown') this.die();
    }
    die() {
      this.gone = true; this.dead = true;
      H.FX.salt(this.cx(), this.cy(), 60);
      H.Audio.toll(); G().shake(8);
      G().onUndertowKilled();
    }
    update(dt) {
      this._skel = null;
      this.anim += dt;
      if (this.gone) return;
      for (const k of this.knots) if (k.flare > 0) k.flare -= dt;
      const p = P();
      this.bellT -= dt;
      if (this.bellT <= 0) { this.bellT = 2.2; H.Audio.buoy(0.5, this.pan()); }
      if (this.stunT > 0) {
        this.stunT -= dt; this.vx *= 0.95; this.vy = 20;
        if (this.stunT <= 0 && this.boss) {
          // Regrows its buoy knot if the ropes still hold.
          if (!this.knot('ropeL').broken || !this.knot('ropeR').broken) { const b = this.knot('buoy'); b.broken = false; b.hp = 12; H.toast('The buoy knot regrows.'); }
        }
        this.swimMove(dt);
        return;
      }
      if (this.leaving) {
        this.vx = this.dir * 140; this.vy = 30;
        this.swimMove(dt);
        if (this.x > this.room.px + this.room.pw + 40 || this.x + this.w < this.room.px - 40 || this.timeLeaving > 4) { this.gone = true; }
        this.timeLeaving = (this.timeLeaving || 0) + dt;
        return;
      }
      this.cool -= dt;
      if (!p) return;
      const pIn = H.World.isWater(p.cx(), p.cy());
      let tx = p.cx(), ty = p.cy();
      if (!pIn) ty = Math.max(p.cy() + 30, H.World.surfaceAt(p.cx(), p.cy()) + 20);
      if (this.mode === 'windup') {
        this.atkT -= dt; this.vx *= 0.9; this.vy *= 0.9;
        if (this.atkT <= 0) {
          this.mode = 'attack'; this.atkT = 0.5;
          const a = Math.atan2(ty - this.cy(), tx - this.cx());
          this.vx = Math.cos(a) * 300; this.vy = Math.sin(a) * 300;
        }
      } else if (this.mode === 'attack') {
        this.atkT -= dt;
        if (this.atkT <= 0) { this.mode = 'hunt'; }
      } else {
        const a = Math.atan2(ty - this.cy(), tx - this.cx());
        const sp = this.boss ? 95 : 80;
        this.vx = H.approach(this.vx, Math.cos(a) * sp, 200 * dt);
        this.vy = H.approach(this.vy, Math.sin(a) * sp, 200 * dt);
        if (this.cool <= 0 && H.dist(this.cx(), this.cy(), p.cx(), p.cy()) < 110 && (pIn || this.boss)) {
          this.mode = 'windup'; this.atkT = 0.8; this.cool = this.boss ? 2.2 : 3;
          this.knots.forEach((k) => { if (!k.broken) k.flare = 0.8; });
          H.Audio.groan(this.pan(), 0.4);
        }
      }
      if (Math.abs(this.vx) > 10) this.facing = Math.sign(this.vx);
      this.swimMove(dt);
      if (H.rectsOverlap(this.hitBox(), p.box()) && !p.invuln()) {
        p.hurt(2, this);
        p.vx += Math.sign(p.cx() - this.cx()) * 200; p.vy -= 120;
      }
    }
    swimMove(dt) {
      // Stays in water at least waist-deep.
      const nx = this.x + this.vx * dt, ny = this.y + this.vy * dt;
      const ok = (x, y) => H.World.isWater(x + this.w / 2, y + 6) && !H.Phys.boxSolid(x, y, this.w, this.h);
      if (ok(nx, this.y)) this.x = nx; else this.vx *= -0.3;
      if (ok(this.x, ny)) this.y = ny; else this.vy *= -0.3;
    }
    draw(c) {
      if (this.gone) return;
      const j = this._skel || (this._skel = this.skeleton());
      const f = this.facing, x = this.cx(), y = this.cy();
      // the drowned bell-ringer, fused to a channel buoy
      c.fillStyle = '#1c2a2e';
      c.beginPath(); c.ellipse(x, y, 34, 14, 0, 0, Math.PI * 2); c.fill();
      c.fillStyle = '#23353a';
      for (let i = 0; i < 5; i++) { c.beginPath(); c.arc(x - f * (24 - i * 11), y + 10 + Math.sin(this.anim * 3 + i) * 2, 5, 0, Math.PI * 2); c.fill(); }
      // arms like bell ropes
      c.strokeStyle = '#3a4a48'; c.lineWidth = 3;
      c.beginPath(); c.moveTo(x + f * 10, y); c.quadraticCurveTo(x + f * 22, y + 14, j.ropeL.x, j.ropeL.y + 8); c.stroke();
      c.beginPath(); c.moveTo(x - f * 12, y); c.quadraticCurveTo(x - f * 26, y + 16, j.ropeR.x, j.ropeR.y + 10); c.stroke();
      // head
      c.fillStyle = '#56645e'; c.beginPath(); c.arc(j.head.x, j.head.y, 7, 0, Math.PI * 2); c.fill();
      c.fillStyle = '#0a0f10'; c.fillRect(j.head.x + f * 2, j.head.y - 2, 3, 2);
      // the buoy cage on its back
      c.fillStyle = this.knot('buoy').broken ? '#4a2a20' : '#8a3a2a';
      c.beginPath(); c.moveTo(j.buoy.x - 7, j.buoy.y + 8); c.lineTo(j.buoy.x, j.buoy.y - 10); c.lineTo(j.buoy.x + 7, j.buoy.y + 8); c.fill();
      c.fillStyle = '#c0a060'; c.beginPath(); c.arc(j.buoy.x, j.buoy.y - 2, 3, 0, Math.PI * 2); c.fill();
    }
    serialize() { return null; }
  }

  // ---------------------------------------------------------------- Undertow (roaming system)
  H.UndertowSystem = class {
    constructor() { this.room = null; this.moveT = 10; this.driven = false; this.dead = false; this.ripples = {}; this.bellT = 0; this.from = null; this.live = null; }
    static sea() { return ['reef', 'pier', 'tithe', 'cloister', 'nave', 'priors']; }
    active() {
      if (this.dead || this.driven || G().act < 2) return false;
      const ph = H.Tide.phase();
      return (ph === 'rising' && H.Tide.phaseProgress() > 0.55) || ph === 'high';
    }
    neighbours(id) {
      const r = H.ROOM[id];
      return H.UndertowSystem.sea().filter((o) => o !== id && H.ROOM[o].row === r.row && Math.abs(H.ROOM[o].col - r.col) === 1);
    }
    driveOff() { this.driven = true; this.room = null; if (this.live) this.live = null; }
    onPhase(ph) { if (ph === 'low') { this.driven = false; this.ripples = {}; } if (ph === 'ebbing' || ph === 'low') this.room = null; }
    update(dt) {
      const p = P();
      if (!this.active()) { this.room = null; this.live = null; return; }
      if (!this.room) {
        // Rises out of the sea at the outer reef.
        this.room = 'reef'; this.moveT = 14; this.from = 1;
      }
      this.ripples[this.room] = true;
      const here = G().room;
      if (this.live && (this.live.gone || this.live.room !== here)) {
        if (this.live.gone && !this.driven) this.moveT = 0;
        this.live = null;
      }
      if (!this.live) {
        this.moveT -= dt;
        if (this.moveT <= 0) {
          const nb = this.neighbours(this.room);
          let next = H.pick(nb);
          if (p && H.UndertowSystem.sea().includes(here.id)) {
            // drift toward the player
            const best = nb.slice().sort((a, b) => Math.abs(H.ROOM[a].col - here.col) - Math.abs(H.ROOM[b].col - here.col))[0];
            if (Math.random() < 0.7) next = best;
          }
          this.from = Math.sign(H.ROOM[this.room].col - H.ROOM[next].col) || 1;
          this.room = next; this.moveT = H.rand(14, 22);
        }
        // Enter the player's room: never in a Bell Room, never within 10 s of a load.
        if (this.room === here.id && !here.bellRoom && G().sinceLoad > 10 && !p.dead) this.spawnLive(here);
      }
      // The bell is always audible before it arrives.
      this.bellT -= dt;
      if (this.bellT <= 0 && !this.live) {
        const r = H.ROOM[this.room];
        const dcol = r.col - here.col, drow = r.row - here.row;
        const dist = Math.abs(dcol) + Math.abs(drow);
        this.bellT = 2.6;
        if (dist <= 3) H.Audio.buoy(0.5 / (1 + dist * 1.3), H.clamp(dcol * 0.6, -1, 1));
      }
    }
    spawnLive(room) {
      // Enter from the side it came from, under the surface.
      const side = this.from > 0 ? room.px + room.pw - 40 : room.px + 40;
      const surf = H.World.roomSurface(room, side);
      if (!isFinite(surf)) return;
      const floor = room.py + room.ph;
      if (floor - surf < 3 * TS) return;
      const y = Math.min(surf + 2.2 * TS, floor - 3 * TS);
      const b = new UndertowBody({ x: side, y, key: 'undertow', dir: -this.from }, room);
      this.live = b;
      G().ents.push(b);
      H.Audio.buoy(0.8, this.from > 0 ? 0.8 : -0.8);
      H.toast('A buoy bell clangs close. The Undertow is in the water with you.', 3);
      G().addSwell(3);
    }
  };

  // ---------------------------------------------------------------- Abbot of Salt
  class Abbot extends Body {
    constructor(spec, room) {
      super(spec, room);
      this.type = 'abbot'; this.boss = true;
      this.w = 36; this.h = 66;
      this.x = spec.x - this.w / 2; this.y = spec.y - this.h;
      this.addKnot('armL', 'arm', 'armL', 6, { plated: true, basePlated: true, r: 6 });
      this.addKnot('armR', 'arm', 'armR', 6, { plated: true, basePlated: true, r: 6 });
      this.addKnot('crown', 'crown', 'crown', 8, { plated: true, basePlated: true, sealedNeeds: 2, r: 7 });
      this.mode = 'idle'; this.awakeMode = 'hunt';
      this.started = false; this.stepUp = false;
    }
    setPose() {}
    skeleton() {
      const f = this.facing, x = this.cx(), gy = this.feet();
      const raise = this.mode === 'windup' ? -12 : this.mode === 'recover' ? 10 : 0;
      return {
        crown: { x: x + f * 2, y: gy - 44 },
        armL: { x: x + f * 16, y: gy - 46 + raise * 0.4 },
        armR: { x: x - f * 14, y: gy - 44 },
        head: { x: x + f * 6, y: gy - 58 },
        hand: { x: x + f * 26, y: gy - 40 + raise },
      };
    }
    hitBox() { return { x: this.x, y: this.y, w: this.w, h: this.h }; }
    hear() {}
    damageKnot(k, dmg, src) {
      if (!this.started) this.start();
      // His plates fall only when he strikes the kilns.
      if (k.plated) { H.Audio.tink(this.pan()); H.FX.sparks(this.knotPos(k).x, this.knotPos(k).y, 4); return false; }
      return super.damageKnot(k, dmg, src);
    }
    damageFlesh(d, x, y, src) { if (!this.started) this.start(); H.Audio.tink(this.pan()); }
    start() { this.started = true; this.mode = 'hunt'; H.Audio.toll(); G().bossBar = this; H.toast('The Abbot of Salt rises from his kilns.'); }
    onKnotBroken(k, src) {
      if (k.type === 'crown') {
        this.gone = true; this.dead = true; this.mode = 'gone';
        H.FX.salt(this.cx(), this.cy(), 80); G().shake(10); H.Audio.toll();
        G().onAbbotKilled(this);
      }
    }
    think(dt) {
      const p = P();
      if (!this.started) {
        if (p && H.dist(p.cx(), p.cy(), this.cx(), this.cy()) < 9 * TS) this.start();
        return;
      }
      this.cool -= dt;
      if (this.mode === 'windup') {
        this.vx = 0; this.atkT -= dt;
        if (this.atkT <= 0) {
          this.mode = 'recover'; this.atkT = 1.2;
          const sx = this.slamX;
          H.Audio.thud(); H.Audio.rumble(); G().shake(7);
          H.FX.salt(sx, this.feet(), 30);
          if (p && Math.abs(p.cx() - sx) < 34 && p.y + p.h > this.feet() - 40) p.hurt(3, this);
          for (const e of G().ents) {
            if (e.type === 'kiln' && !e.broken && Math.abs(e.cx() - sx) < 34) {
              e.shatter();
              const k = this.knots.find((k) => k.plated);
              if (k) { k.plated = false; H.toast('The kiln bursts and a salt plate falls from the Abbot!'); }
            }
          }
        }
        return;
      }
      if (this.mode === 'sweep') {
        this.vx = 0; this.atkT -= dt;
        if (this.atkT <= 0) {
          this.mode = 'recover'; this.atkT = 0.8;
          H.Audio.gaff(); H.FX.salt(this.cx() + this.facing * 40, this.feet() - 4, 12);
          if (p && Math.abs(p.cx() - this.cx()) < 64 && p.y + p.h > this.feet() - 14) p.hurt(2, this);
        }
        return;
      }
      if (this.mode === 'recover') { this.atkT -= dt; if (this.atkT <= 0) this.mode = 'hunt'; return; }
      if (!p) return;
      const dx = p.cx() - this.cx();
      this.facing = Math.sign(dx) || 1;
      this.vx = Math.abs(dx) > 40 ? this.facing * 28 : 0;
      if (this.cool <= 0) {
        if (Math.abs(dx) < 52 && Math.random() < 0.5) {
          this.mode = 'sweep'; this.atkT = 0.8; this.cool = 2.0;
          this.knots.forEach((k) => { if (!k.broken) k.flare = 0.8; });
          H.Audio.groan(this.pan(), 0.4);
        } else {
          this.mode = 'windup'; this.atkT = 1.2; this.cool = 2.6;
          this.slamX = H.clamp(p.cx(), this.room.px + 40, this.room.px + this.room.pw - 40);
          this.knots.forEach((k) => { if (!k.broken) k.flare = 1.2; });
          H.Audio.groan(this.pan(), 0.5);
        }
      }
    }
    draw(c) {
      if (this.gone) return;
      const j = this._skel || (this._skel = this.skeleton());
      const x = this.cx(), gy = this.feet(), f = this.facing;
      // robe
      c.fillStyle = '#4a3f36';
      c.beginPath(); c.moveTo(x - 18, gy); c.lineTo(x - 14, gy - 50); c.lineTo(x + 14, gy - 50); c.lineTo(x + 18, gy); c.fill();
      // salt armour
      c.fillStyle = '#d8d0c0';
      c.fillRect(x - 15, gy - 52, 30, 8);
      c.fillRect(x - 10, gy - 30, 20, 4);
      // head + mitre
      c.fillStyle = '#8a9088'; c.beginPath(); c.arc(j.head.x, j.head.y, 7, 0, Math.PI * 2); c.fill();
      c.fillStyle = '#e8e0d0'; c.beginPath(); c.moveTo(j.head.x - 6, j.head.y - 5); c.lineTo(j.head.x, j.head.y - 18); c.lineTo(j.head.x + 6, j.head.y - 5); c.fill();
      // pick arm
      c.strokeStyle = '#3a322a'; c.lineWidth = 6; c.lineCap = 'round';
      c.beginPath(); c.moveTo(j.armL.x, j.armL.y); c.lineTo(j.hand.x, j.hand.y); c.stroke();
      const ang = this.mode === 'windup' ? -2.4 * f : this.mode === 'recover' ? 1.3 * f : 0.4 * f;
      c.save(); c.translate(j.hand.x, j.hand.y); c.rotate(ang);
      c.strokeStyle = '#6a5030'; c.lineWidth = 4; c.beginPath(); c.moveTo(0, 0); c.lineTo(0, -36); c.stroke();
      c.strokeStyle = '#b0b8bc'; c.lineWidth = 5; c.beginPath(); c.moveTo(-14, -34); c.quadraticCurveTo(0, -42, 16, -30); c.stroke();
      c.restore();
      c.strokeStyle = '#3a322a'; c.lineWidth = 5;
      c.beginPath(); c.moveTo(j.armR.x, j.armR.y); c.lineTo(j.armR.x - f * 6, j.armR.y + 18); c.stroke();
    }
    serialize() { return null; }
  }

  class Kiln {
    constructor(spec, room) { Object.assign(this, { x: spec.x - 12, y: spec.y - 26, w: 24, h: 26, room, broken: false, type: 'kiln', key: spec.key }); }
    cx() { return this.x + 12; }
    cy() { return this.y + 13; }
    shatter() { this.broken = true; H.FX.spawn(this.cx(), this.cy(), 30, { color: '#ff9040', glow: true, smin: 60, smax: 200 }); H.Audio.crack(); H.Fire.ignite(Math.floor(this.cx() / TS), Math.floor((this.y + 20) / TS), 5); }
    update() {}
    light() { return this.broken ? null : { x: this.cx(), y: this.y + 14, r: 70, i: 0.7, color: '255,120,40' }; }
    draw(c) {
      c.fillStyle = this.broken ? '#2a2420' : '#5a4a3a';
      c.fillRect(this.x, this.y, this.w, this.h);
      if (!this.broken) {
        c.fillStyle = '#ff8a30'; c.fillRect(this.x + 7, this.y + 12, 10, 8);
        c.fillStyle = '#6a5a4a'; c.fillRect(this.x - 2, this.y - 3, this.w + 4, 4);
      } else { c.fillStyle = '#1a1410'; c.fillRect(this.x + 4, this.y, 16, 12); }
    }
    serialize() { return { broken: this.broken }; }
    restore(s) { this.broken = s.broken; }
  }

  // ---------------------------------------------------------------- The Shoal
  class Shoal extends Body {
    constructor(spec, room) {
      super(spec, room);
      this.type = 'shoal'; this.boss = true;
      this.w = 46; this.h = 40; this.x = spec.x - 23; this.y = spec.y - 40;
      for (let i = 0; i < 8; i++) this.addKnot('k' + i, 'shoal', 'k' + i, 4, { r: 5 });
      this.mode = 'idle'; this.awakeMode = 'hunt'; this.rot = 0; this.started = false;
      this.stepUp = false;
    }
    setPose() {}
    skeleton() {
      const j = {}; const x = this.cx(), y = this.cy();
      for (let i = 0; i < 8; i++) { const a = this.rot + i * Math.PI / 4; j['k' + i] = { x: x + Math.cos(a) * 17, y: y + Math.sin(a) * 15 }; }
      return j;
    }
    hear() { if (!this.started) this.start(); }
    start() { this.started = true; this.mode = 'hunt'; G().bossBar = this; H.toast('Five bodies fused into one rolling mass. The Shoal.'); H.Audio.scream(); }
    damageKnot(k, d, s) { if (!this.started) this.start(); return super.damageKnot(k, d, s); }
    onKnotBroken(k) {
      // Each broken knot sheds a smaller body.
      const sp = new H.BodyTypes.shucker({ x: this.cx(), y: this.feet(), key: 'shed' + H.uid(), pose: 'standing' }, this.room);
      sp.knot('kneeL').broken = true; sp.knot('kneeR').broken = true; sp.setPose('crawl'); sp.mode = 'hunt';
      sp.knots.forEach((q) => { q.max = 2; if (!q.broken) q.hp = 2; });
      sp.shed = true;
      G().ents.push(sp);
      if (this.knots.every((q) => q.broken)) {
        this.gone = true; this.dead = true; H.FX.salt(this.cx(), this.cy(), 50); G().shake(6);
        G().onShoalKilled();
      }
    }
    think(dt) {
      const p = P();
      if (!this.started) { if (p && H.dist(p.cx(), p.cy(), this.cx(), this.cy()) < 10 * TS) this.start(); return; }
      if (!p) return;
      const dx = p.cx() - this.cx();
      this.facing = Math.sign(dx) || 1;
      const sp = 55 + this.knots.filter((k) => k.broken).length * 6;
      this.vx = H.approach(this.vx, this.facing * sp, 120 * dt);
      this.rot += this.vx * dt / 17;
      if (this.hitWall && this.onGround) this.vy = -200;
      if (H.rectsOverlap(this.hitBox(), p.box()) && !p.invuln()) { p.hurt(2, this); p.vx = this.facing * 220; p.vy = -160; }
    }
    draw(c) {
      if (this.gone) return;
      const x = this.cx(), y = this.cy();
      c.fillStyle = '#3a3a34';
      c.beginPath(); c.arc(x, y, 20, 0, Math.PI * 2); c.fill();
      c.strokeStyle = '#7f8b80'; c.lineWidth = 2.5;
      for (let i = 0; i < 10; i++) {
        const a = this.rot * 1.0 + i * 0.63;
        c.beginPath(); c.moveTo(x + Math.cos(a) * 10, y + Math.sin(a) * 10); c.lineTo(x + Math.cos(a + 0.3) * 23, y + Math.sin(a + 0.3) * 22); c.stroke();
      }
      c.fillStyle = '#7f8b80';
      for (let i = 0; i < 5; i++) { const a = this.rot + i * 1.25; c.beginPath(); c.arc(x + Math.cos(a) * 14, y + Math.sin(a) * 12, 3.2, 0, Math.PI * 2); c.fill(); }
    }
    serialize() { return null; }
  }

  H.BodyTypes.undertow = UndertowBody;
  H.BodyTypes.abbot = Abbot;
  H.BodyTypes.shoal = Shoal;
  H.Kiln = Kiln;
})();
