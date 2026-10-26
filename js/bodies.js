// Brine bodies: puppets held up by salt knots at their joints.
// Hits on flesh deal 10% damage; hits on a knot deal full damage; broken knots change behaviour.
'use strict';
(() => {
  const TS = H.TILE;
  const G = () => H.Game;
  const P = () => H.Game.player;

  class Body {
    constructor(spec, room) {
      this.isBody = true;
      this.spec = spec;
      this.room = room;
      this.key = spec.key;
      this.facing = spec.facing || (Math.random() < 0.5 ? -1 : 1);
      this.vx = 0; this.vy = 0;
      this.anim = Math.random() * 10;
      this.knots = [];
      this.mode = 'idle';
      this.pose = 'stand';
      this.stun = 0; this.pinT = 0; this.pinKnot = null;
      this.burnT = 0; this.gone = false; this.dead = false;
      this.reknot = false; this.dragged = false;
      this.atkT = 0; this.cool = 0;
      this.target = null; this.lastHeard = null; this.alert = 0;
      this.crust = {};
      this.stepUp = true;
      this.speed = 45;
      this.w = 12; this.h = 28;
      this.x = spec.x - this.w / 2; this.y = spec.y - this.h;
      this.hpScale = 1;
    }
    cx() { return this.x + this.w / 2; }
    cy() { return this.y + this.h / 2; }
    feet() { return this.y + this.h; }
    hitBox() { return { x: this.x, y: this.y, w: this.w, h: this.h }; }

    addKnot(id, type, joint, hp, o = {}) {
      const k = Object.assign({ id, type, joint, hp, max: hp, broken: false, plated: false, r: 4.5, sealedNeeds: 0, flare: 0 }, o);
      this.knots.push(k);
      return k;
    }
    knot(id) { return this.knots.find((k) => k.id === id); }
    brokenLimbs() { return this.knots.filter((k) => k.broken && k.type !== 'crown').length; }
    isSealed(k) { return k.sealedNeeds > 0 && this.brokenLimbs() < k.sealedNeeds; }
    alive() { return !this.dead && !this.gone && this.mode !== 'downed' && this.mode !== 'captured'; }

    // ---- skeleton -----------------------------------------------------------
    setPose(p) {
      if (p === this.pose) return;
      const bottom = this.y + this.h, mid = this.cx();
      this.pose = p;
      const dims = { stand: [12, 28], crawl: [24, 10], seated: [14, 20], lying: [26, 7], hang: [12, 28] }[p] || [12, 28];
      this.w = dims[0]; this.h = dims[1];
      this.x = mid - this.w / 2; this.y = bottom - this.h;
      // Make sure we do not end up inside a wall.
      for (let i = 0; i < 8 && H.Phys.boxSolid(this.x, this.y, this.w, this.h); i++) this.x += (i % 2 ? -1 : 1) * (i + 1) * 2;
    }
    skeleton() {
      const f = this.facing, x = this.cx(), gy = this.feet();
      const t = this.anim;
      const j = {};
      const walk = Math.abs(this.vx) > 5 ? Math.sin(t * 7) : 0;
      const armL = this.armOK('L'), armR = this.armOK('R');
      if (this.pose === 'stand') {
        const bob = Math.abs(walk) * 1;
        j.hip = { x, y: gy - 13 + bob };
        j.sternum = { x: x + f * 1, y: gy - 19 + bob };
        j.shoulderL = { x: x + f * 1.5, y: gy - 22 + bob };
        j.shoulderR = { x: x - f * 0.5, y: gy - 22 + bob };
        j.neck = { x: x + f * 2, y: gy - 24 + bob };
        j.head = { x: x + f * 3, y: gy - 26.5 + bob };
        j.jaw = { x: x + f * 5, y: gy - 24.5 + bob };
        const lk = this.knot('kneeL'), rk = this.knot('kneeR');
        const limp = (lk && lk.broken) || (rk && rk.broken);
        j.kneeL = { x: x + f * (walk * 3 + 1), y: gy - 7 };
        j.footL = { x: x + f * (walk * 5), y: gy };
        j.kneeR = { x: x + f * (-walk * 3 + 1), y: gy - 7 + (limp ? 1 : 0) };
        j.footR = { x: x + f * (-walk * 5), y: gy };
        const reach = this.mode === 'hunt' || this.mode === 'windup' || this.mode === 'attack' || this.mode === 'grab';
        const raise = this.mode === 'windup' ? -4 : 0;
        for (const side of ['L', 'R']) {
          const sh = j['shoulder' + side];
          const ok = side === 'L' ? armL : armR;
          if (!ok) {
            const sw = Math.sin(t * 3 + (side === 'L' ? 0 : 1)) * 1.5;
            j['elbow' + side] = { x: sh.x + sw, y: sh.y + 6 };
            j['hand' + side] = { x: sh.x + sw * 2, y: sh.y + 12 };
          } else if (reach) {
            const off = side === 'L' ? 0 : -1.5;
            j['elbow' + side] = { x: sh.x + f * 6, y: sh.y + 1 + raise + off };
            j['hand' + side] = { x: sh.x + f * 12, y: sh.y + raise * 2 + off };
          } else {
            j['elbow' + side] = { x: sh.x + f * 2, y: sh.y + 6 };
            j['hand' + side] = { x: sh.x + f * 4, y: sh.y + 11 };
          }
        }
      } else if (this.pose === 'crawl') {
        const c = Math.sin(t * 9) * 2;
        j.hip = { x: x - f * 8, y: gy - 4 };
        j.sternum = { x: x + f * 2, y: gy - 6 };
        j.shoulderL = { x: x + f * 5, y: gy - 7 };
        j.shoulderR = { x: x + f * 4, y: gy - 7 };
        j.neck = { x: x + f * 7, y: gy - 7 };
        j.head = { x: x + f * 10, y: gy - 7 };
        j.jaw = { x: x + f * 11.5, y: gy - 5 };
        j.kneeL = { x: x - f * 12, y: gy - 2 };
        j.footL = { x: x - f * 16, y: gy - 1 };
        j.kneeR = { x: x - f * 12.5, y: gy - 1 };
        j.footR = { x: x - f * 15, y: gy };
        for (const side of ['L', 'R']) {
          const sh = j['shoulder' + side];
          const ok = side === 'L' ? armL : armR;
          const cc = side === 'L' ? c : -c;
          if (ok) { j['elbow' + side] = { x: sh.x + f * (4 + cc), y: gy - 3 }; j['hand' + side] = { x: sh.x + f * (8 + cc), y: gy }; }
          else { j['elbow' + side] = { x: sh.x + f * 1, y: gy - 2 }; j['hand' + side] = { x: sh.x - f * 2, y: gy }; }
        }
      } else if (this.pose === 'seated') {
        j.hip = { x, y: gy - 8 };
        j.kneeL = { x: x + f * 6, y: gy - 8 }; j.footL = { x: x + f * 7, y: gy };
        j.kneeR = { x: x + f * 5, y: gy - 7.5 }; j.footR = { x: x + f * 5.5, y: gy };
        j.sternum = { x: x + f * 0.5, y: gy - 13 };
        j.shoulderL = { x: x + f * 1.5, y: gy - 16 }; j.shoulderR = { x: x, y: gy - 16 };
        j.neck = { x: x + f * 2.5, y: gy - 17 };
        j.head = { x: x + f * 4, y: gy - 18 };
        j.jaw = { x: x + f * 5.5, y: gy - 16 };
        j.elbowL = { x: x + f * 3, y: gy - 11 }; j.handL = { x: x + f * 6, y: gy - 9 };
        j.elbowR = { x: x + f * 1.5, y: gy - 11 }; j.handR = { x: x + f * 4.5, y: gy - 9 };
      } else { // lying / downed
        const y = gy - 2;
        j.head = { x: x + f * 11, y: y - 1 };
        j.jaw = { x: x + f * 12.5, y: y };
        j.neck = { x: x + f * 8, y };
        j.shoulderL = { x: x + f * 6, y: y - 1 }; j.shoulderR = { x: x + f * 6, y: y + 0.5 };
        j.sternum = { x: x + f * 3, y };
        j.hip = { x: x - f * 4, y };
        j.kneeL = { x: x - f * 9, y: y - 1 }; j.footL = { x: x - f * 13, y };
        j.kneeR = { x: x - f * 9, y: y + 1 }; j.footR = { x: x - f * 13.5, y: y + 1 };
        j.elbowL = { x: x + f * 4, y: y + 1 }; j.handL = { x: x + f * 1, y: y + 1.5 };
        j.elbowR = { x: x + f * 8, y: y + 1.5 }; j.handR = { x: x + f * 10, y: y + 1.5 };
      }
      return j;
    }
    knotPos(k) {
      const j = this._skel || (this._skel = this.skeleton());
      const p = j[k.joint];
      return p ? { x: p.x, y: p.y } : null;
    }
    armOK(side) {
      const e = this.knot('elbow' + side) || this.knot('shoulder' + side) || this.knot('arm' + side);
      return !e || !e.broken;
    }
    armsOK() { return this.armOK('L') || this.armOK('R'); }
    legsOK() {
      const a = this.knot('kneeL'), b = this.knot('kneeR');
      if (!a && !b) return true;
      return !((a && a.broken) && (b && b.broken));
    }

    // ---- damage --------------------------------------------------------------
    damageKnot(k, dmg, src) {
      if (k.broken || this.gone) return false;
      const pan = this.pan();
      if (this.isSealed(k)) { H.Audio.tink(pan); H.FX.sparks(this.knotPos(k).x, this.knotPos(k).y, 3); this.flinch(); return false; }
      if (k.plated) {
        if (src === 'gaff' || src === 'fire' || src === 'pry') {
          k.plateHp = (k.plateHp || 2) - (src === 'pry' ? 2 : 1);
          H.Audio.crack(pan); H.FX.salt(this.knotPos(k).x, this.knotPos(k).y, 6);
          if (k.plateHp <= 0) { k.plated = false; H.toast('Plate pried off.'); }
        } else { H.Audio.tink(pan); H.FX.sparks(this.knotPos(k).x, this.knotPos(k).y, 3); }
        this.flinch();
        return false;
      }
      // A knot flaring during a wind-up is brittle.
      if (k.flare > 0 && src === 'rivet') dmg *= 2;
      k.hp -= dmg;
      const p = this.knotPos(k);
      H.FX.salt(p.x, p.y, 5);
      this.wake(true);
      if (k.hp <= 0) {
        k.broken = true; k.hp = 0;
        H.Audio.crack(pan);
        H.FX.salt(p.x, p.y, 16);
        G().stats.knots[k.type] = (G().stats.knots[k.type] || 0) + 1;
        H.Narr.think('firstKnot');
        const ba = G().stats.byAct[G().act] || (G().stats.byAct[G().act] = {}); ba[k.type] = (ba[k.type] || 0) + 1;
        this.onKnotBroken(k, src);
      } else {
        H.Audio.tink(pan);
        this.flinch();
      }
      G().addSwell(0.3);
      return true;
    }
    damageFlesh(dmg, x, y, src) {
      if (this.gone) return;
      H.Audio.thud(this.pan());
      H.FX.blood(x, y, 3);
      this.wake(true);
      // 10% of the hit reaches the nearest open knot.
      let best = null, bd = 1e9;
      for (const k of this.knots) {
        if (k.broken || k.plated || this.isSealed(k)) continue;
        const p = this.knotPos(k); if (!p) continue;
        const d = H.dist(x, y, p.x, p.y);
        if (d < bd) { bd = d; best = k; }
      }
      if (best) {
        best.hp -= dmg * 0.1;
        if (best.hp <= 0) { best.broken = true; H.Audio.crack(this.pan()); this.onKnotBroken(best, src); }
      }
      this.flinch();
    }
    flinch() { if (this.mode === 'hunt') this.vx *= 0.3; }
    onKnotBroken(k, src) {
      if (k.type === 'crown') { this.down(src); return; }
      if (this.mode === 'grab' && (k.type === 'elbow' || k.type === 'shoulder' || k.type === 'arm')) this.releaseGrab();
      if (!this.legsOK() && this.pose === 'stand' && this.mode !== 'downed') this.setPose('crawl');
      if (this.mode === 'windup' && !this.canAttack()) this.mode = 'hunt';
    }
    canAttack() { return this.armsOK(); }
    down(src) {
      this.mode = 'downed';
      this.releaseGrab();
      this.vx = 0;
      this.setPose('lying');
      this.reknot = false;
      H.Audio.groan(this.pan(), 0.1);
      G().onBodyDowned(this, src);
    }
    // Re-knotting: the Brine grows new knots and the body stands again.
    standUp() {
      if (this.gone || this.corpse) return;
      this.reknot = false;
      for (const k of this.knots) { k.broken = false; k.hp = k.max; k.plated = !!k.basePlated; }
      G().applyEncrustation(this);
      this.setPose('stand');
      this.mode = 'hunt';
      this.anim = 0;
      H.Audio.groan(this.pan(), 0.25);
      H.FX.salt(this.cx(), this.cy(), 20);
    }
    burn() {
      this.burnT = 3;
      H.Audio.fire(this.pan());
    }
    pin(k, t, tier) {
      if (k.type === 'crown' || k.broken === undefined) return;
      this.pinT = t; this.pinKnot = k;
      if (tier >= 2) this.barbT = 2;
      if (tier >= 1) this.tetherable = true;
    }
    releaseGrab() {
      const p = P();
      if (p && p.grabbedBy === this) { p.grabbedBy = null; p.state = 'normal'; }
      if (this.mode === 'grab') { this.mode = 'recover'; this.atkT = 0.8; }
    }
    pan() { const p = P(); return p ? H.clamp((this.cx() - p.cx()) / 300, -1, 1) : 0; }

    // ---- perception ---------------------------------------------------------
    hear(x, y, d, r, src) {
      if (this.gone || this.corpse || this.mode === 'downed' || this.mode === 'captured') return;
      if (this.mode === 'dormant') {
        if (src === 'scream' || G().canWake()) { this.lastHeard = { x, y }; this.wake(); }
        return;
      }
      this.lastHeard = { x, y };
      this.alert = 3;
    }
    wake(force) {
      if (this.mode !== 'dormant' || this.corpse) return;
      if (!force && !G().canWake()) return;
      this.mode = 'rise'; this.atkT = 0.9;
      H.Audio.groan(this.pan());
      G().addSwell(1);
    }
    perceives() {
      const p = P();
      if (!p || p.dead || p.inBellRoom) return false;
      const d = H.dist(this.cx(), this.cy(), p.cx(), p.cy());
      const lit = p.lanternLit();
      const sight = lit ? 9 * TS * (p.hasFitting('tallowr') ? 1.3 : 1) : 1.6 * TS;
      if (d < 1.6 * TS) return true;
      if (d < sight && H.Phys.los(this.cx(), this.y + 4, p.cx(), p.cy())) return true;
      return false;
    }

    // ---- update -----------------------------------------------------------------
    update(dt) {
      this._skel = null;
      this.anim += dt;
      for (const k of this.knots) if (k.flare > 0) k.flare -= dt;
      if (this.burnT > 0) {
        this.burnT -= dt;
        if (Math.random() < 36 * dt) H.FX.embers(this.cx() + H.rand(-8, 8), this.cy(), 2);
        if (this.burnT <= 0) { this.gone = true; this.mode = 'gone'; G().onBodyBurned(this); H.FX.spawn(this.cx(), this.cy(), 20, { color: '#333', grav: -30 }); }
        return;
      }
      // Fire hurts knots.
      if (H.Fire.burnsAt(this.cx(), this.feet() - 4) && this.mode !== 'downed') {
        this.fireT = (this.fireT || 0) - dt;
        if (this.fireT <= 0) {
          this.fireT = 1;
          const k = this.knots.filter((k) => !k.broken && !this.isSealed(k));
          if (k.length) this.damageKnot(H.pick(k), 1, 'fire');
        }
      }
      if (this.mode === 'downed' || this.mode === 'captured') { this.physics(dt); return; }
      if (this.stun > 0) { this.stun -= dt; this.vx *= 0.8; this.physics(dt); return; }
      if (this.pinT > 0) {
        this.pinT -= dt; this.vx = 0;
        if (this.barbT !== undefined) {
          this.barbT -= dt;
          if (this.barbT <= 0 && this.pinKnot && !this.pinKnot.broken) { this.barbT = undefined; this.damageKnot(this.pinKnot, 99, 'harpoon'); }
        }
        this.physics(dt, true);
        return;
      }
      for (const id in this.crust) { this.crust[id] -= dt; if (this.crust[id] <= 0) delete this.crust[id]; }
      this.think(dt);
      this.physics(dt);
    }
    think(dt) {}
    physics(dt, pinned) {
      const inWater = H.World.isWater(this.cx(), this.cy());
      if (this.dragged) return;
      if (inWater) {
        this.vy += 250 * dt; if (this.vy > 70) this.vy = 70;
      } else {
        this.vy += 1100 * dt; if (this.vy > 500) this.vy = 500;
      }
      if (pinned) this.vx = 0;
      // Limp bodies do not slide.
      if (this.mode === 'downed' || this.mode === 'dormant' || this.mode === 'captured') this.vx *= this.onGround ? 0.7 : 0.98;
      H.Phys.move(this, dt);
      // Bodies stay in their own room.
      const r = this.room;
      if (this.x < r.px + 2) { this.x = r.px + 2; this.vx = 0; this.hitWall = -1; }
      if (this.x + this.w > r.px + r.pw - 2) { this.x = r.px + r.pw - 2 - this.w; this.vx = 0; this.hitWall = 1; }
      if (this.y > r.py + r.ph) { this.y = r.py + r.ph - this.h - 2; this.vy = 0; }
    }

    // Generic shambling pursuit used by humanoids.
    pursue(dt, tx, speed) {
      const dx = tx - this.cx();
      if (Math.abs(dx) > 4) { this.facing = Math.sign(dx); this.vx = H.approach(this.vx, this.facing * speed, 600 * dt); }
      else this.vx = H.approach(this.vx, 0, 600 * dt);
      if (this.hitWall && this.onGround && this.pose === 'stand' && this.legsOK()) this.vy = -260; // clamber
      else if (this.hitWall && this.pose === 'stand' && this.legsOK() && this.climbT === undefined) { this.climbT = 0.6; }
      if (this.climbT !== undefined) { this.climbT -= dt; this.vy = Math.min(this.vy, -60); if (this.climbT <= 0 || !this.hitWall) this.climbT = undefined; }
    }

    // ---- rendering --------------------------------------------------------------
    drawLimb(c, a, b, w, col) {
      c.strokeStyle = col; c.lineWidth = w; c.lineCap = 'round';
      c.beginPath(); c.moveTo(a.x, a.y); c.lineTo(b.x, b.y); c.stroke();
    }
    drawHuman(c, palette) {
      const j = this._skel || (this._skel = this.skeleton());
      const robe = palette.robe, skin = palette.skin, dark = palette.dark || '#1a1d1e';
      const back = (s) => (s === 'R' ? dark : skin);
      // back limbs
      this.drawLimb(c, j.hip, j.kneeR, 2.6, dark); this.drawLimb(c, j.kneeR, j.footR, 2.2, dark);
      this.drawLimb(c, j.shoulderR, j.elbowR, 2.2, dark); this.drawLimb(c, j.elbowR, j.handR, 1.8, dark);
      // robe body
      c.fillStyle = robe;
      c.beginPath();
      c.moveTo(j.shoulderL.x - 3, j.shoulderL.y - 1);
      c.lineTo(j.shoulderL.x + 3, j.shoulderL.y - 1);
      if (this.pose === 'stand') { c.lineTo(j.hip.x + 4.5, j.hip.y + 4); c.lineTo(j.hip.x - 4.5, j.hip.y + 4); }
      else { c.lineTo(j.hip.x + 3, j.hip.y + 2.5); c.lineTo(j.hip.x - 3, j.hip.y - 2.5); }
      c.closePath(); c.fill();
      if (this.pose === 'stand') { c.fillRect(j.hip.x - 4.5, j.hip.y, 9, 6); }
      // front limbs
      this.drawLimb(c, j.hip, j.kneeL, 2.8, robe); this.drawLimb(c, j.kneeL, j.footL, 2.4, skin);
      this.drawLimb(c, j.shoulderL, j.elbowL, 2.6, robe); this.drawLimb(c, j.elbowL, j.handL, 2, skin);
      // head + cowl
      c.fillStyle = skin; c.beginPath(); c.arc(j.head.x, j.head.y, 3.4, 0, Math.PI * 2); c.fill();
      if (palette.cowl) { c.fillStyle = palette.cowl; c.beginPath(); c.arc(j.head.x - this.facing * 0.8, j.head.y - 0.5, 3.8, Math.PI * 0.9, Math.PI * 2.1); c.fill(); }
      // salt crust speckles
      c.fillStyle = 'rgba(230,240,245,0.55)';
      for (let i = 0; i < 4; i++) {
        const t = (i * 0.37 + (this.key ? this.key.length * 0.13 : 0)) % 1;
        c.fillRect(H.lerp(j.shoulderL.x, j.hip.x, t) + ((i * 7) % 5) - 2, H.lerp(j.shoulderL.y, j.hip.y, t), 1, 1);
      }
      if (this.mode === 'windup' || this.mode === 'inhale') {
        c.fillStyle = 'rgba(255,240,200,0.5)';
        c.beginPath(); c.arc(j.jaw.x, j.jaw.y, 1.2, 0, Math.PI * 2); c.fill();
      }
    }
    drawKnots(c) {
      const vis = H.Render.knotVisibility;
      for (const k of this.knots) {
        const p = this.knotPos(k); if (!p) continue;
        let v = vis(p.x, p.y, this);
        if (k.flare > 0) v = Math.max(v, 0.9);
        if (this.mode === 'downed' || this.mode === 'dormant' && this.corpse) v *= 0.15;
        if (k.broken) {
          if (v > 0.05) { c.fillStyle = `rgba(60,70,75,${0.6 * v})`; c.fillRect(p.x - 1.5, p.y - 1.5, 3, 3); }
          continue;
        }
        if (v < 0.04) continue;
        const sealed = this.isSealed(k);
        const s = (k.r || 4.5) * (k.type === 'crown' ? 0.95 : 0.8);
        if (v > 0.5) { c.fillStyle = `rgba(190,240,255,${0.18 * v})`; c.beginPath(); c.arc(p.x, p.y, s + 4, 0, Math.PI * 2); c.fill(); }
        c.save(); c.translate(p.x, p.y); c.rotate(Math.PI / 4 + Math.sin(this.anim * 2 + p.x) * 0.1);
        const col = k.plated ? `rgba(200,190,170,${v})` : sealed ? `rgba(120,150,170,${v})` : k.flare > 0 ? `rgba(255,255,240,${v})` : `rgba(200,245,255,${v})`;
        c.fillStyle = col;
        c.fillRect(-s / 2, -s / 2, s, s);
        if (k.plated) { c.strokeStyle = `rgba(90,80,70,${v})`; c.lineWidth = 1; c.strokeRect(-s / 2 - 1, -s / 2 - 1, s + 2, s + 2); }
        if (k.hp < k.max && !k.plated) { c.strokeStyle = `rgba(40,60,70,${v})`; c.lineWidth = 0.8; c.beginPath(); c.moveTo(-s / 2, 0); c.lineTo(s / 2, -1); c.stroke(); }
        c.restore();
      }
    }
    draw(c) {}
    serialize() {
      return {
        x: this.x, y: this.y, mode: this.mode === 'rise' ? 'hunt' : this.mode, pose: this.pose, facing: this.facing, gone: this.gone,
        reknot: this.reknot, knots: this.knots.map((k) => ({ hp: k.hp, broken: k.broken, plated: k.plated })),
      };
    }
    restore(s) {
      this.setPose(s.pose || this.pose);
      this.x = s.x; this.y = s.y; this.facing = s.facing; this.gone = s.gone; this.reknot = s.reknot;
      this.mode = s.mode;
      if (['hunt', 'windup', 'attack', 'recover', 'grab', 'inhale', 'rise', 'idle'].includes(this.mode)) this.mode = this.awakeMode || 'hunt';
      s.knots && s.knots.forEach((ks, i) => { if (this.knots[i]) Object.assign(this.knots[i], ks); });
      if (this.gone) this.mode = 'gone';
    }
  }
  H.Body = Body;

  // ---------------------------------------------------------------- Shucker
  class Shucker extends Body {
    constructor(spec, room) {
      super(spec, room);
      this.type = 'shucker';
      this.addKnot('kneeL', 'knee', 'kneeL', 3);
      this.addKnot('kneeR', 'knee', 'kneeR', 3);
      this.addKnot('elbowL', 'elbow', 'elbowL', 3);
      this.addKnot('elbowR', 'elbow', 'elbowR', 3);
      this.addKnot('crown', 'crown', 'sternum', 4, { sealedNeeds: 2, r: 5 });
      this.teodor = !!spec.teodor;
      this.standOnWater = !!spec.standOnWater;
      if (spec.pose === 'seated') { this.setPose('seated'); this.mode = 'dormant'; }
      else if (spec.pose === 'lying') { this.setPose('lying'); this.mode = 'dormant'; }
      else this.mode = 'hunt';
      this.awakeMode = 'hunt';
      this.grabHits = 0;
    }
    onKnotBroken(k, src) {
      super.onKnotBroken(k, src);
      if (this.teodor && this.mode !== 'downed') {
        const limbs = ['kneeL', 'kneeR', 'elbowL', 'elbowR'].every((id) => this.knot(id).broken);
        if (limbs) { this.mode = 'captured'; this.setPose('lying'); this.releaseGrab(); H.toast('Teodor lies helpless, his Crown still bright. E to bind him for Odile.'); }
      }
    }
    down(src) {
      super.down(src);
      if (this.teodor && !this.breach && !['killed', 'tags', 'bound', 'captured', 'saved', 'dead'].includes(G().quest('teodor'))) {
        G().setQuest('teodor', 'killed');
        G().spawnPickup('tags', this.cx(), this.y - 4);
        H.toast("Brass tags spill from Teodor's collar.");
      }
    }
    think(dt) {
      const p = P();
      if (this.mode === 'dormant') {
        if (this.standOnWater && H.World.isWater(this.cx(), this.feet() - 3)) { this.standOnWater = false; this.wake(true); }
        return;
      }
      if (this.mode === 'rise') {
        this.atkT -= dt;
        if (this.atkT <= 0) { this.setPose(this.legsOK() ? 'stand' : 'crawl'); this.mode = 'hunt'; }
        return;
      }
      const crawling = this.pose === 'crawl';
      const speed = crawling ? 80 : (this.knot('kneeL').broken || this.knot('kneeR').broken ? 30 : 45);
      if (this.mode === 'grab') {
        this.vx = 0;
        if (!p || p.grabbedBy !== this) { this.mode = 'recover'; this.atkT = 0.6; return; }
        this.atkT -= dt;
        if (this.atkT <= 0) { this.atkT = 1.3; p.hurt(1, this); }
        return;
      }
      if (this.mode === 'windup') {
        this.vx *= 0.85; this.atkT -= dt;
        if (this.atkT <= 0) { this.mode = 'attack'; this.atkT = 0.3; this.vx = this.facing * (crawling ? 160 : 200); }
        return;
      }
      if (this.mode === 'attack') {
        this.atkT -= dt;
        if (p && !p.invuln() && H.rectsOverlap(this.hitBox(), p.box()) && this.armsOK()) {
          if (p.tryGrab(this)) { this.mode = 'grab'; this.atkT = 0.4; H.Audio.grab(); return; }
        }
        if (this.atkT <= 0) { this.mode = 'recover'; this.atkT = 0.7; }
        return;
      }
      if (this.mode === 'recover') { this.vx *= 0.8; this.atkT -= dt; if (this.atkT <= 0) this.mode = 'hunt'; return; }
      // hunt
      this.cool -= dt;
      if (this.perceives()) { this.target = { x: p.cx(), y: p.cy() }; this.alert = 3; }
      else if (this.lastHeard && this.alert > 0) { this.target = this.lastHeard; this.alert -= dt; }
      else { this.target = null; }
      if (this.target) {
        this.pursue(dt, this.target.x, speed);
        const d = H.dist(this.cx(), this.cy(), p.cx(), p.cy());
        if (p && !p.dead && d < 32 && this.cool <= 0 && this.armsOK() && this.perceives()) {
          this.mode = 'windup'; this.atkT = 0.6; this.cool = 1.4;
          this.facing = Math.sign(p.cx() - this.cx()) || this.facing;
          this.knots.forEach((k) => { if (!k.broken) k.flare = 0.6; });
          H.Audio.groan(this.pan(), 0.25);
        }
        // Armless Shuckers still body-check.
        if (!this.armsOK() && p && H.rectsOverlap(this.hitBox(), p.box()) && this.cool <= 0) { p.hurt(1, this); this.cool = 1.5; }
      } else {
        this.vx = H.approach(this.vx, 0, 300 * dt);
        if (Math.random() < dt * 0.1) H.Audio.groan(this.pan(), 0.05);
      }
    }
    draw(c) {
      if (this.gone) return;
      const tint = this.teodor ? { robe: '#2f3b44', skin: '#8b968c', cowl: null, dark: '#1b2228' } : { robe: '#3a3530', skin: '#7f8b80', cowl: '#2c2824' };
      if (this.burnT > 0) { tint.robe = '#1a1410'; tint.skin = '#3a2a20'; }
      this.drawHuman(c, tint);
      if (this.teodor) { c.fillStyle = '#c9a85a'; const j = this._skel; c.fillRect(j.neck.x - 1, j.neck.y, 2, 2); }
    }
  }

  // ---------------------------------------------------------------- Gullet
  class Gullet extends Body {
    constructor(spec, room) {
      super(spec, room);
      this.type = 'gullet';
      this.ax = spec.x; this.ay = spec.anchorY; this.len = (spec.len || 4) * TS;
      this.addKnot('tendril', 'tendril', 'tendril', 2, { r: 4 });
      this.addKnot('spine1', 'spine', 's1', 3);
      this.addKnot('spine2', 'spine', 's2', 3);
      this.addKnot('spine3', 'spine', 's3', 3);
      this.mode = 'hang'; this.awakeMode = 'ground';
      this.w = 22; this.h = 9;
      this.x = this.ax - this.w / 2; this.y = this.ay + this.len - this.h;
    }
    skeleton() {
      const j = {};
      const sway = this.mode === 'hang' ? Math.sin(this.anim * 1.3) * 2 : 0;
      if (this.mode === 'hang') {
        j.tendril = { x: this.ax + sway * 0.4, y: this.ay + this.len * 0.45 };
        const bx = this.ax + sway, by = this.ay + this.len;
        j.s1 = { x: bx, y: by - 10 }; j.s2 = { x: bx + 1, y: by - 5 }; j.s3 = { x: bx, y: by };
        j.head = { x: bx, y: by + 3 };
      } else {
        const f = this.facing, x = this.cx(), y = this.feet() - 4;
        const w = Math.sin(this.anim * 10) * 1.5;
        j.s1 = { x: x - f * 8, y: y + w }; j.s2 = { x, y: y - w }; j.s3 = { x: x + f * 7, y: y + w };
        j.head = { x: x + f * 12, y: y - 1 };
        j.tendril = { x: x - f * 14, y: y + 2 };
      }
      return j;
    }
    hitBox() {
      if (this.mode === 'hang') return { x: this.ax - 5, y: this.ay + this.len - 14, w: 10, h: 18 };
      return { x: this.x, y: this.y, w: this.w, h: this.h };
    }
    cx() { return this.mode === 'hang' ? this.ax : this.x + this.w / 2; }
    cy() { return this.mode === 'hang' ? this.ay + this.len - 5 : this.y + this.h / 2; }
    hear(x, y, d, r, src) {
      if (this.mode === 'hang') {
        // Drops on noise below it.
        if (Math.abs(x - this.ax) < 3.5 * TS && y > this.ay + this.len - 8 && (src === 'scream' || G().canWake())) this.drop();
        return;
      }
      super.hear(x, y, d, r, src);
    }
    drop(stunned) {
      if (this.mode !== 'hang') return;
      this.mode = 'fall';
      this.x = this.ax - this.w / 2; this.y = this.ay + this.len - this.h;
      this.vy = 50; this.stunOnLand = stunned;
      this.knot('tendril').broken = true;
      H.Audio.groan(this.pan(), 0.2);
    }
    onKnotBroken(k, src) {
      if (k.id === 'tendril' && this.mode === 'hang') { k.broken = false; this.drop(true); return; }
      if (k.type === 'spine' && this.knots.filter((q) => q.type === 'spine' && q.broken).length >= 3) this.down(src);
    }
    damageKnot(k, dmg, src) {
      if (k.type === 'spine' && this.mode === 'hang' && src !== 'fire') dmg *= 0.5;
      return super.damageKnot(k, dmg, src);
    }
    wake() { if (this.mode === 'hang') this.drop(); }
    think(dt) {
      const p = P();
      if (this.mode === 'hang') { this.vx = 0; this.vy = 0; return; }
      if (this.mode === 'fall') {
        if (this.onGround) {
          // Crushes whatever it lands on.
          for (const e of G().ents) {
            if (e !== this && e.isBody && e.alive && e.alive() && H.rectsOverlap(this.hitBox(), e.hitBox()) && e.knot && e.knot('kneeL')) {
              e.damageKnot(e.knot('kneeL'), 9, 'gullet'); e.damageKnot(e.knot('kneeR'), 9, 'gullet'); e.stun = 2;
            }
          }
          if (p && H.rectsOverlap(this.hitBox(), p.box())) p.hurt(2, this);
          H.Audio.thud(this.pan()); H.FX.salt(this.cx(), this.feet(), 10);
          G().shake(3);
          this.mode = 'ground'; this.stun = this.stunOnLand ? 3 : 0.8;
        }
        return;
      }
      if (this.mode === 'windup') {
        this.vx *= 0.8; this.atkT -= dt;
        if (this.atkT <= 0) { this.mode = 'attack'; this.atkT = 0.25; this.vx = this.facing * 220; }
        return;
      }
      if (this.mode === 'attack') {
        this.atkT -= dt;
        if (p && H.rectsOverlap(this.hitBox(), p.box())) { p.hurt(2, this); this.mode = 'recover'; this.atkT = 0.8; }
        if (this.atkT <= 0) { this.mode = 'recover'; this.atkT = 0.8; }
        return;
      }
      if (this.mode === 'recover') { this.vx *= 0.85; this.atkT -= dt; if (this.atkT <= 0) this.mode = 'ground'; return; }
      // ground crawl
      this.cool -= dt;
      if (this.perceives() || (this.lastHeard && this.alert > 0)) {
        const tx = this.perceives() ? p.cx() : this.lastHeard.x;
        this.alert -= dt;
        const dx = tx - this.cx();
        this.facing = Math.sign(dx) || this.facing;
        this.vx = H.approach(this.vx, this.facing * 70, 500 * dt);
        if (p && H.dist(this.cx(), this.cy(), p.cx(), p.cy()) < 40 && this.cool <= 0) {
          this.mode = 'windup'; this.atkT = 0.5; this.cool = 1.8;
          this.knots.forEach((k) => { if (!k.broken) k.flare = 0.5; });
          H.Audio.groan(this.pan(), 0.2);
        }
      } else this.vx *= 0.9;
    }
    physics(dt, pinned) {
      if (this.mode === 'hang') return;
      super.physics(dt, pinned);
    }
    draw(c) {
      if (this.gone) return;
      const j = this._skel || (this._skel = this.skeleton());
      c.strokeStyle = '#9fb7c0'; c.lineWidth = 1.5;
      if (this.mode === 'hang') {
        c.beginPath(); c.moveTo(this.ax, this.ay); c.quadraticCurveTo(this.ax + Math.sin(this.anim) * 3, this.ay + this.len / 2, j.s1.x, j.s1.y); c.stroke();
        c.fillStyle = 'rgba(220,240,250,0.7)'; c.fillRect(this.ax - 3, this.ay, 6, 2);
      }
      c.strokeStyle = this.burnT > 0 ? '#2a1c10' : '#4a5550'; c.lineWidth = 6; c.lineCap = 'round';
      c.beginPath(); c.moveTo(j.s1.x, j.s1.y); c.lineTo(j.s2.x, j.s2.y); c.lineTo(j.s3.x, j.s3.y); c.lineTo(j.head.x, j.head.y); c.stroke();
      c.strokeStyle = '#2a3230'; c.lineWidth = 1;
      for (const s of [j.s1, j.s2, j.s3]) { c.beginPath(); c.moveTo(s.x - 4, s.y + 3); c.lineTo(s.x - 6, s.y + 6); c.moveTo(s.x + 4, s.y + 3); c.lineTo(s.x + 6, s.y + 6); c.stroke(); }
      c.fillStyle = '#c0d0c8'; c.beginPath(); c.arc(j.head.x, j.head.y, 2.5, 0, Math.PI * 2); c.fill();
      if (this.mode === 'windup') { c.fillStyle = '#e0f0ff'; c.fillRect(j.head.x - 1, j.head.y - 1, 2, 2); }
    }
    serialize() { const s = super.serialize(); s.mode = this.mode === 'fall' ? 'ground' : this.mode; return s; }
    restore(s) {
      super.restore(s);
      if (s.mode === 'hang') { this.mode = 'hang'; this.x = this.ax - this.w / 2; this.y = this.ay + this.len - this.h; }
      else if (s.mode !== 'downed' && s.mode !== 'gone') { this.mode = 'ground'; this.knot('tendril').broken = true; }
      this.w = 22; this.h = 9;
    }
    setPose(p) { if (p === 'lying') { this.pose = 'lying'; } }
    standUp() {
      super.standUp();
      this.w = 22; this.h = 9; this.mode = 'ground'; this.knot('tendril').broken = true;
    }
  }

  // ---------------------------------------------------------------- Bellmouth
  class Bellmouth extends Body {
    constructor(spec, room) {
      super(spec, room);
      this.type = 'bellmouth';
      this.addKnot('jaw', 'jaw', 'jaw', 3);
      this.addKnot('shoulderL', 'shoulder', 'shoulderL', 3);
      this.addKnot('shoulderR', 'shoulder', 'shoulderR', 3);
      this.addKnot('crown', 'crown', 'sternum', 4, { sealedNeeds: 2, r: 5 });
      this.w = 16; this.h = 30; this.x = spec.x - 8; this.y = spec.y - 30;
      this.mode = 'idle'; this.awakeMode = 'idle';
      this.screamCool = 0;
      this.home = spec.x;
    }
    setPose(p) { if (p === 'lying') { super.setPose('lying'); } }
    seesLight() {
      const p = P();
      const lights = [];
      if (p && p.lanternLit() && !p.inBellRoom) lights.push({ x: p.cx(), y: p.y + 8, lantern: true });
      for (const pr of G().projectiles) if (pr.kind === 'flare') lights.push({ x: pr.x, y: pr.y });
      for (const l of lights) {
        const d = H.dist(this.cx(), this.y + 6, l.x, l.y);
        const range = l.lantern ? (p.focusing ? 13 : 8) * TS * (p.hasFitting('tallowr') ? 1.3 : 1) : 7 * TS;
        if (d > range) continue;
        const facingOK = Math.sign(l.x - this.cx()) === this.facing || Math.abs(l.x - this.cx()) < 20 || (l.lantern && p.beamHits(this.cx(), this.cy()));
        if (facingOK && H.Phys.los(this.cx(), this.y + 6, l.x, l.y)) return true;
      }
      return false;
    }
    think(dt) {
      const p = P();
      this.screamCool -= dt; this.cool -= dt;
      if (this.mode === 'inhale') {
        this.vx = 0; this.atkT -= dt;
        this.knot('jaw').flare = 0.2;
        if (this.knot('jaw').broken) { this.mode = 'idle'; this.screamCool = 4; H.toast('The Bellmouth chokes on its scream.'); return; }
        if (this.atkT <= 0) {
          this.mode = 'idle'; this.screamCool = 9;
          H.Audio.scream(this.pan());
          G().shake(4);
          G().addSwell(2);
          for (const e of G().ents) if (e !== this && e.isBody && e.mode === 'dormant' && !e.corpse) { e.lastHeard = { x: this.cx(), y: this.cy() }; e.wake(true); }
          for (const e of G().ents) if (e.type === 'gullet' && e.mode === 'hang') e.drop();
          H.Noise.emit(this.cx(), this.cy(), 20, 'scream');
        }
        return;
      }
      if (this.mode === 'windup') {
        this.vx = 0; this.atkT -= dt;
        if (this.atkT <= 0) {
          this.mode = 'recover'; this.atkT = 0.6;
          if (p && H.dist(this.cx(), this.cy(), p.cx(), p.cy()) < 34) p.hurt(1, this);
          H.Audio.thud(this.pan());
        }
        return;
      }
      if (this.mode === 'recover') { this.atkT -= dt; if (this.atkT <= 0) this.mode = 'idle'; return; }
      // idle: sway, wander, look for light
      if (Math.random() < dt * 0.3) this.facing = -this.facing;
      const dx = this.home - this.cx();
      this.vx = Math.abs(dx) > 24 ? Math.sign(dx) * 15 : this.vx * 0.9;
      if (!this.knot('jaw').broken && this.screamCool <= 0 && this.seesLight()) {
        this.mode = 'inhale'; this.atkT = 1.5;
        if (p) this.facing = Math.sign(p.cx() - this.cx()) || this.facing;
        H.Audio.inhale(this.pan());
        return;
      }
      if (p && this.armsOK() && this.cool <= 0 && H.dist(this.cx(), this.cy(), p.cx(), p.cy()) < 30) {
        this.mode = 'windup'; this.atkT = 0.7; this.cool = 2;
        this.facing = Math.sign(p.cx() - this.cx()) || 1;
        this.knots.forEach((k) => { if (!k.broken) k.flare = 0.7; });
      }
    }
    skeleton() {
      const j = super.skeleton();
      if (this.pose === 'stand') {
        const puff = this.mode === 'inhale' ? (1.5 - this.atkT) * 3 : 0;
        j.jaw = { x: j.jaw.x, y: j.jaw.y + 1 + puff * 0.3 };
        j.belly = puff;
      }
      return j;
    }
    draw(c) {
      if (this.gone) return;
      const j = this._skel || (this._skel = this.skeleton());
      this.drawHuman(c, { robe: '#d8d4c8', skin: '#8e9e98', cowl: null, dark: '#6a6a62' });
      if (this.pose === 'stand') {
        const b = (j.belly || 0) + 5;
        c.fillStyle = '#9aa8a2';
        c.beginPath(); c.ellipse(j.sternum.x + this.facing * 2, j.sternum.y + 3, b, b * 1.1, 0, 0, Math.PI * 2); c.fill();
        c.fillStyle = '#d8d4c8'; c.fillRect(j.sternum.x - 6, j.sternum.y + 4, 12, 3);
        if (this.mode === 'inhale') { c.fillStyle = '#101010'; c.beginPath(); c.arc(j.jaw.x, j.jaw.y, 2, 0, Math.PI * 2); c.fill(); }
      }
    }
  }

  // ---------------------------------------------------------------- Driftling
  class Driftling extends Body {
    constructor(spec, room) {
      super(spec, room);
      this.type = 'driftling';
      this.w = 6; this.h = 4; this.x = spec.x; this.y = spec.y;
      this.addKnot('core', 'core', 'core', 1, { r: 3 });
      this.mode = 'swim'; this.awakeMode = 'swim';
      this.vx = H.rand(-30, 30); this.vy = H.rand(-20, 20);
      this.nibble = 0; this.nest = spec.nest;
    }
    skeleton() { return { core: { x: this.cx(), y: this.cy() } }; }
    hear() {}
    wake() {}
    onKnotBroken(k) { this.gone = true; this.dead = true; H.FX.salt(this.cx(), this.cy(), 4); }
    damageFlesh(dmg, x, y) { this.onKnotBroken(this.knots[0]); }
    update(dt) {
      this._skel = null;
      this.anim += dt;
      if (this.gone) return;
      const p = P();
      const inWater = H.World.isWater(this.cx(), this.cy());
      if (!inWater) {
        this.vy += 600 * dt; this.flop = (this.flop || 0) + dt;
        if (this.flop > 3) { this.gone = true; this.dead = true; }
        H.Phys.move(this, dt);
        return;
      }
      this.flop = 0;
      // Drawn to light: flares first, then an unshuttered lantern.
      let tx = null, ty = null;
      let best = 1e9;
      for (const pr of G().projectiles) if (pr.kind === 'flare' && H.World.isWater(pr.x, pr.y + 2) || pr.kind === 'flare' && Math.abs(pr.y - H.World.surfaceAt(pr.x, pr.y)) < 40) {
        const d = H.dist(this.cx(), this.cy(), pr.x, pr.y);
        if (d < best && d < 260) { best = d; tx = pr.x; ty = pr.y; this.flareTarget = pr; }
      }
      if (tx === null && p && p.lanternLit() && !p.inBellRoom) {
        const d = H.dist(this.cx(), this.cy(), p.cx(), p.cy());
        if (d < 200) { tx = p.cx(); ty = p.cy(); this.flareTarget = null; }
      }
      if (tx === null && p && H.dist(this.cx(), this.cy(), p.cx(), p.cy()) < 28) { tx = p.cx(); ty = p.cy(); }
      if (tx !== null) {
        const a = Math.atan2(ty - this.cy(), tx - this.cx()) + Math.sin(this.anim * 5 + this.key.length) * 0.8;
        this.vx = H.approach(this.vx, Math.cos(a) * 110, 400 * dt);
        this.vy = H.approach(this.vy, Math.sin(a) * 110, 400 * dt);
      } else {
        this.vx += H.rand(-60, 60) * dt; this.vy += H.rand(-60, 60) * dt;
        this.vx *= 0.98; this.vy *= 0.98;
      }
      // keep under the surface
      const surf = H.World.surfaceAt(this.cx(), this.cy());
      if (this.cy() < surf + 4) this.vy = Math.abs(this.vy) + 20;
      H.Phys.move(this, dt);
      this.nibble -= dt;
      if (p && this.nibble <= 0 && H.rectsOverlap(this.hitBox(), p.box())) {
        this.nibble = 1.0;
        if (p.dashT > 0) { this.onKnotBroken(); return; }
        if (Math.random() < 0.5) p.hurt(1, this);
      }
      // A flare stuck to a body: the swarm strips its knots for you.
      if (this.flareTarget && this.flareTarget.stuck && this.flareTarget.stuck.body && this.nibble <= 0) {
        const b = this.flareTarget.stuck.body;
        if (H.dist(this.cx(), this.cy(), b.cx(), b.cy()) < 20 && b.knots) {
          this.nibble = 0.8;
          const ks = b.knots.filter((k) => !k.broken && !k.plated && !b.isSealed(k));
          if (ks.length) b.damageKnot(H.pick(ks), 1, 'driftling');
        }
      }
    }
    draw(c) {
      if (this.gone) return;
      const a = Math.atan2(this.vy, this.vx);
      c.save(); c.translate(this.cx(), this.cy()); c.rotate(a);
      c.fillStyle = '#5c7078'; c.beginPath(); c.ellipse(0, 0, 4, 2, 0, 0, Math.PI * 2); c.fill();
      c.beginPath(); c.moveTo(-3, 0); c.lineTo(-6, -2 + Math.sin(this.anim * 20)); c.lineTo(-6, 2 + Math.sin(this.anim * 20)); c.fill();
      c.restore();
    }
    serialize() { return null; }
  }

  // ---------------------------------------------------------------- Saltwright
  class Saltwright extends Body {
    constructor(spec, room) {
      super(spec, room);
      this.type = 'saltwright';
      this.addKnot('armL', 'arm', 'elbowL', 4, { plated: true, basePlated: true });
      this.addKnot('armR', 'arm', 'elbowR', 4, { plated: true, basePlated: true });
      this.addKnot('crown', 'crown', 'sternum', 5, { sealedNeeds: 2, r: 5 });
      this.w = 16; this.h = 30; this.x = spec.x - 8; this.y = spec.y - 30;
      this.mode = 'idle'; this.awakeMode = 'hunt';
    }
    setPose(p) { if (p === 'lying') super.setPose('lying'); }
    canAttack() { return this.armsOK(); }
    think(dt) {
      const p = P();
      this.cool -= dt;
      if (this.mode === 'windup') {
        this.vx = 0; this.atkT -= dt;
        if (this.atkT <= 0) {
          this.mode = 'recover'; this.atkT = 1.5; this.exposed = 1.5;
          H.Audio.thud(this.pan()); G().shake(2);
          const hx = this.cx() + this.facing * 22, hy = this.y + this.h - 8;
          if (p && Math.abs(p.cx() - hx) < 22 && Math.abs(p.cy() - hy) < 24) p.hurt(2, this);
          // The pick breaks salt walls and floors.
          for (let dy = -2; dy <= 1; dy++) for (let dx = 0; dx <= 1; dx++) {
            const tx = Math.floor((hx + this.facing * dx * 16) / TS), ty = Math.floor(hy / TS) + dy;
            if (H.World.charAt(tx, ty) === 'S') { H.World.setChar(tx, ty, ' '); H.FX.salt(tx * 16 + 8, ty * 16 + 8, 14); H.Audio.crack(); }
          }
          H.FX.salt(hx, hy + 6, 8);
          H.Noise.emit(hx, hy, 8, 'pick');
        }
        return;
      }
      if (this.mode === 'recover') { this.vx = 0; this.atkT -= dt; this.exposed = this.atkT; if (this.atkT <= 0) { this.mode = 'hunt'; this.exposed = 0; } return; }
      if (this.mode === 'idle') {
        if (this.perceives() || this.alert > 0) this.mode = 'hunt';
        this.vx *= 0.9;
        return;
      }
      if (this.perceives()) { this.target = { x: p.cx() }; this.alert = 4; }
      else if (this.lastHeard && this.alert > 0) { this.target = this.lastHeard; this.alert -= dt; }
      else this.target = null;
      if (this.target) {
        this.pursue(dt, this.target.x, 38);
        if (p && this.armsOK() && this.cool <= 0 && Math.abs(p.cx() - this.cx()) < 40 && Math.abs(p.cy() - this.cy()) < 30) {
          this.mode = 'windup'; this.atkT = 0.8; this.cool = 2.2;
          this.facing = Math.sign(p.cx() - this.cx()) || 1;
          this.knots.forEach((k) => { if (!k.broken) k.flare = 0.8; });
          H.Audio.groan(this.pan(), 0.3);
        }
      } else this.vx *= 0.9;
    }
    // In the 1.5 s after its swing, a gaff strike pries an arm plate off.
    damageKnot(k, dmg, src) {
      if (src === 'gaff' && k.plated && this.exposed > 0) src = 'pry';
      return super.damageKnot(k, dmg, src);
    }
    draw(c) {
      if (this.gone) return;
      const j = this._skel || (this._skel = this.skeleton());
      this.drawHuman(c, { robe: '#5a4a3a', skin: '#8a8a7e', cowl: '#3a3028', dark: '#2a221a' });
      if (this.pose === 'stand') {
        // salt armour plates on shoulders and a pick
        c.fillStyle = '#cfc8b8'; c.fillRect(j.shoulderL.x - 5, j.shoulderL.y - 3, 10, 4);
        const hand = j.handL;
        const ang = this.mode === 'windup' ? -2.2 * this.facing : this.mode === 'recover' ? 0.9 * this.facing : 0.3 * this.facing;
        c.save(); c.translate(hand.x, hand.y); c.rotate(ang);
        c.strokeStyle = '#6a5030'; c.lineWidth = 2; c.beginPath(); c.moveTo(0, 0); c.lineTo(0, -16); c.stroke();
        c.strokeStyle = '#9aa0a4'; c.lineWidth = 2.5; c.beginPath(); c.moveTo(-6, -15); c.quadraticCurveTo(0, -19, 7, -14); c.stroke();
        c.restore();
      }
    }
  }

  // ---------------------------------------------------------------- Grafted (Act III choir)
  class Grafted extends Body {
    constructor(spec, room) {
      super(spec, room);
      this.type = 'grafted';
      this.passive = !!spec.passive;
      this.addKnot('elbowL', 'elbow', 'elbowL', 3);
      this.addKnot('elbowR', 'elbow', 'elbowR', 3);
      this.addKnot('graft', 'graft', 'neck', 3, { r: 4 });
      this.addKnot('crown', 'crown', 'sternum', 4, { sealedNeeds: 2, r: 5 });
      this.mode = this.passive ? 'idle' : 'hunt'; this.awakeMode = this.passive ? 'idle' : 'hunt';
      this.healT = 0;
      this.boss = !!spec.boss;
      if (this.boss) { this.knots.forEach((k) => { k.max += 3; k.hp = k.max; }); this.addKnot('graft2', 'graft', 'hip', 4); }
    }
    perceives() { return this.passive ? false : super.perceives(); }
    hear(x, y, d, r, src) { if (!this.passive) super.hear(x, y, d, r, src); }
    damageKnot(k, dmg, src) { if (this.passive) { this.passive = false; this.mode = 'hunt'; G().onChoirAttacked(this); } return super.damageKnot(k, dmg, src); }
    damageFlesh(dmg, x, y, src) { if (this.passive) { this.passive = false; this.mode = 'hunt'; G().onChoirAttacked(this); } super.damageFlesh(dmg, x, y, src); }
    light() { return this.gone || this.mode === 'downed' ? null : { x: this.cx() + this.facing * 6, y: this.cy(), r: 60, i: 0.6, color: '255,200,120' }; }
    think(dt) {
      const p = P();
      if (this.passive) { this.vx *= 0.8; if (p) this.facing = Math.sign(p.cx() - this.cx()) || 1; return; }
      this.cool -= dt;
      // Retreat into water to heal.
      const hurt = this.knots.some((k) => k.broken && k.type !== 'crown');
      if (hurt && H.World.isWater(this.cx(), this.feet() - 2)) {
        this.healT += dt;
        if (this.healT > 3) { this.healT = 0; const k = this.knots.find((k) => k.broken && k.type !== 'crown'); if (k) { k.broken = false; k.hp = k.max; H.FX.salt(this.cx(), this.cy(), 8); } }
      }
      if (this.mode === 'windup') {
        this.vx = 0; this.atkT -= dt;
        if (this.atkT <= 0 && p) {
          this.mode = 'recover'; this.atkT = 1.0;
          const a = Math.atan2(p.cy() - (this.y + 10), p.cx() - this.cx());
          G().enemyShots.push({ x: this.cx(), y: this.y + 10, vx: Math.cos(a) * 340, vy: Math.sin(a) * 340, life: 2, dmg: 2, src: this });
          H.Audio.harpoon(this.pan());
        }
        return;
      }
      if (this.mode === 'recover') { this.atkT -= dt; if (this.atkT <= 0) this.mode = 'hunt'; return; }
      if (!p) return;
      const d = H.dist(this.cx(), this.cy(), p.cx(), p.cy());
      if (hurt && !H.World.isWater(this.cx(), this.feet() - 2)) {
        // look for water nearby
        for (const dir of [-1, 1]) for (let i = 1; i < 12; i++) {
          if (H.World.isWater(this.cx() + dir * i * TS, this.feet() - 2)) { this.pursue(dt, this.cx() + dir * i * TS, 50); return; }
        }
      }
      if (this.perceives() || d < 12 * TS) {
        if (d > 7 * TS) this.pursue(dt, p.cx(), 50);
        else if (d < 3 * TS) this.pursue(dt, this.cx() - Math.sign(p.cx() - this.cx()) * 40, 50);
        else this.vx *= 0.8;
        this.facing = Math.sign(p.cx() - this.cx()) || 1;
        if (this.cool <= 0 && this.armsOK() && H.Phys.los(this.cx(), this.y + 10, p.cx(), p.cy())) {
          this.mode = 'windup'; this.atkT = 0.9; this.cool = this.boss ? 1.6 : 2.6;
          this.knots.forEach((k) => { if (!k.broken) k.flare = 0.9; });
        }
      } else this.vx *= 0.9;
    }
    draw(c) {
      if (this.gone) return;
      const j = this._skel || (this._skel = this.skeleton());
      this.drawHuman(c, { robe: this.boss ? '#4a2f5a' : '#3a3a5a', skin: '#9aa6b0', cowl: this.boss ? '#c0a060' : '#2a2a40', dark: '#1a1a2a' });
      if (this.pose === 'stand' && this.mode !== 'downed') {
        c.fillStyle = '#ffd080'; c.fillRect(j.handL.x - 1.5, j.handL.y - 1, 3, 3);
        c.fillStyle = 'rgba(200,240,255,0.6)'; c.fillRect(j.neck.x - 2, j.neck.y - 1, 4, 2);
      }
    }
  }

  H.BodyTypes = { shucker: Shucker, gullet: Gullet, bellmouth: Bellmouth, driftling: Driftling, saltwright: Saltwright, grafted: Grafted };

  // A true corpse: no knots, never stands. Still a body you can drag (Dead Weight).
  class Corpse extends Body {
    constructor(spec, room) {
      super(spec, room);
      this.type = 'corpse'; this.corpse = true;
      this.sailor = !!spec.sailor;
      this.setPose(spec.pose === 'seated' || spec.pose === 'slumped' ? 'seated' : 'lying');
      this.mode = 'dormant';
      if (this.sailor) {
        // The tutorial sailor shows knots that glint, then go dark.
        this.addKnot('kneeL', 'knee', 'kneeL', 1); this.addKnot('kneeR', 'knee', 'kneeR', 1);
        this.addKnot('elbowL', 'elbow', 'elbowL', 1); this.addKnot('elbowR', 'elbow', 'elbowR', 1);
        this.fadeKnots = 0;
      }
    }
    hear() {}
    wake() {}
    damageKnot() { return false; }
    damageFlesh(d, x, y) { H.Audio.thud(this.pan()); H.FX.blood(x, y, 2); }
    update(dt) {
      this._skel = null;
      if (this.burnT > 0) { super.update(dt); return; }
      if (this.sailor && this.fadeKnots > 0) { this.fadeKnots -= dt; if (this.fadeKnots <= 0) this.knots = []; }
      if (this.mode !== 'downed') this.physics(dt);
      else this.physics(dt);
    }
    drawKnots(c) {
      if (!this.sailor || !this.knots.length) return;
      const f = this.fadeKnots > 0 ? this.fadeKnots / 3 : 1;
      for (const k of this.knots) {
        const p = this.knotPos(k);
        const v = H.Render.knotVisibility(p.x, p.y, this) * f;
        if (v < 0.05) continue;
        c.fillStyle = `rgba(200,245,255,${v})`; c.save(); c.translate(p.x, p.y); c.rotate(Math.PI / 4); c.fillRect(-1.8, -1.8, 3.6, 3.6); c.restore();
      }
    }
    draw(c) {
      if (this.gone) return;
      this.drawHuman(c, this.sailor ? { robe: '#2a3a4a', skin: '#8c9890', dark: '#1a242c' } : { robe: '#3b3631', skin: '#7a837a', cowl: '#2c2824' });
    }
    serialize() { return { x: this.x, y: this.y, gone: this.gone, pose: this.pose, facing: this.facing, knots: [] }; }
    restore(s) { this.x = s.x; this.y = s.y; this.gone = s.gone; this.facing = s.facing; this.setPose(s.pose || this.pose); if (this.sailor) this.knots = []; }
  }
  H.BodyTypes.corpse = Corpse;
})();
