// Maren Hult: a working diver, not a warrior.
'use strict';
(() => {
  const TS = H.TILE;
  const I = () => H.Input;
  const W = () => H.World;
  const G = () => H.Game;

  const RUN = 150, SNEAK = 70, GRAV = 1300, JUMP = 400, COYOTE = 0.1, BUFFER = 0.12;

  class Player {
    constructor() {
      this.w = 10; this.h = 22;
      this.x = 0; this.y = 0; this.vx = 0; this.vy = 0;
      this.facing = 1;
      this.maxHp = 5; this.hp = 5;
      this.breath = 30; this.oil = 90;
      this.abilities = new Set(['swing']);
      this.weapons = { driver: { tier: 0, mag: 6 } };
      this.weaponOrder = ['driver'];
      this.cur = 'driver';
      this.bag = new H.Satchel.Bag(8);
      this.bag.add('rivets', 12);
      this.bag.add('poultice', 1);
      this.fittingsOwned = new Set();
      this.fittings = new Set();
      this.pressure = 4;
      this.grafts = new Set();
      this.shut = false;
      this.focusing = false;
      this.state = 'normal';
      this.coyote = 0; this.jumpBuf = 0;
      this.invulnT = 0; this.dashT = 0; this.dashCool = 0; this.airDashed = false;
      this.gaffCool = 0; this.fireCool = 0; this.reloadT = 0;
      this.healT = 0; this.healKind = null; this.healTick = 0;
      this.craftT = 0;
      this.grabbedBy = null; this.mash = 0;
      this.dragging = null;
      this.swing = null;
      this.boots = false;
      this.drownT = 0; this.pressureT = 0;
      this.stepT = 0; this.wasAir = false; this.fallStart = 0;
      this.aim = 0;
      this.staggerT = 0; this.gaffLooseT = 0;
      this.dead = false;
      this.stepUp = true;
      this.inBellRoom = false;
      this.beamPath = null;
    }
    cx() { return this.x + this.w / 2; }
    cy() { return this.y + this.h / 2; }
    box() { return { x: this.x, y: this.y, w: this.w, h: this.h }; }
    has(a) { return this.abilities.has(a); }
    hasFitting(f) { return this.fittings.has(f); }
    invuln() { return this.invulnT > 0 || (this.dashT > 0 && this.dashInv > 0); }
    weaponTier(w) { return this.weapons[w] ? this.weapons[w].tier : 0; }
    magSize(w) { return w === 'driver' ? (this.hasFitting('feeder') ? 5 : 6) : H.WEAPONS[w].mag; }
    breathMax() {
      if (this.grafts.has('lung')) return Infinity;
      let b = this.has('seal') ? 60 : 30;
      if (this.hasFitting('cork')) b *= 1.4;
      return b;
    }
    oilMax() { return this.hasFitting('tallowr') ? 135 : 90; }
    lanternLit() { return !this.shut && this.oil > 0 && !this.dead; }
    lanternPos() { return { x: this.cx() + this.facing * 3, y: this.y + 9 }; }
    headUnder() { return W().isWater(this.cx(), this.y + 3); }
    inWater() { return W().isWater(this.cx(), this.cy()); }
    locked() { return this.reloadT > 0 || this.craftT > 0 || (this.healT > 0 && this.healKind === 'stitch') || this.grabbedBy || this.staggerT > 0; }

    // Beam hit test used by Bellmouths: is (x,y) inside the lantern cone?
    beamHits(x, y) {
      if (!this.lanternLit()) return false;
      const lp = this.lanternPos();
      const d = H.dist(lp.x, lp.y, x, y);
      const range = this.focusing ? 260 : 130;
      const spread = this.focusing ? 0.22 : 0.65;
      if (d > range) return false;
      const a = Math.atan2(y - lp.y, x - lp.x);
      return Math.abs(H.angleDiff(this.aim, a)) < spread && H.Phys.los(lp.x, lp.y, x, y);
    }

    // ---- damage -------------------------------------------------------------
    hurt(n, src) {
      if (this.dead || this.invuln() || G().god) return;
      if (this.inBellRoom) return;
      if (this.hasFitting('lead') && n >= 2) n -= 1;
      if (G().difficulty === 'drowned' && n >= 1 && Math.random() < 0.15) n += 1;
      this.hp -= n;
      this.invulnT = 0.9;
      H.Audio.hurt(); G().shake(3); G().flash = 0.25;
      H.FX.blood(this.cx(), this.cy(), 6);
      // A hit cancels the remainder of a poultice; interrupts a Stitch Kit.
      if (this.healT > 0) { if (this.healKind === 'stitch') H.toast('Stitching interrupted. The kit is kept.'); this.healT = 0; }
      this.craftT = 0; this.reloadT = 0;
      if (src && src.cx) {
        const fromBehind = Math.sign(src.cx() - this.cx()) === -this.facing;
        if (this.grafts.has('ear') && fromBehind) { this.staggerT = 1; H.toast('The salt on your neck cracks. You stagger.', 1.5); }
        if (this.grafts.has('arm') && n >= 2) { this.gaffLooseT = 2; H.toast('The crusted wrist jolts. The gaff slips loose!', 1.5); }
        this.vx = Math.sign(this.cx() - src.cx()) * 120; if (!this.inWater()) this.vy = -150;
      }
      G().addSwell(0.5);
      if (this.hp <= 0) { this.hp = 0; G().onPlayerDeath(); }
    }
    tryGrab(enemy) {
      if (this.grabbedBy || this.invuln() || this.dead || this.state === 'swing' || G().god) return false;
      this.grabbedBy = enemy; this.state = 'grabbed';
      const cracked = enemy.knots && enemy.knots.some((k) => (k.type === 'elbow' || k.type === 'arm') && k.hp < k.max);
      this.mash = cracked ? 3 : 6;
      this.healT = 0; this.reloadT = 0; this.craftT = 0;
      if (this.dragging) this.stopDrag();
      H.toast('Grabbed! Mash F / E / Space to break free.', 2);
      return true;
    }

    // ---- update ---------------------------------------------------------------
    update(dt) {
      const In = I();
      if (this.dead) return;
      this.invulnT -= dt; this.dashCool -= dt; this.gaffCool -= dt; this.fireCool -= dt;
      this.staggerT -= dt; this.gaffLooseT -= dt;
      if (this.lastRing) this.lastRing.t -= dt;
      const r = G().room;
      this.inBellRoom = r.bellRoom && !(r.id === 'chapel' && G().quest('tables') === 'sold') && !G().bellRoomBreached(r.id);

      // aim from the mouse (or facing when keyboard-only)
      const cam = G().cam;
      const mx = In.mouse.x + cam.x, my = In.mouse.y + cam.y;
      this.aim = Math.atan2(my - (this.y + 9), mx - this.cx());
      if (this.state !== 'swing' && !this.dragging && !this.locked()) this.facing = Math.cos(this.aim) >= 0 ? 1 : -1;

      if (this.grabbedBy) { this.updateGrabbed(dt); return; }
      this.updateLantern(dt);
      this.updateTimers(dt);

      if (this.state === 'swing') { this.updateSwing(dt); this.afterMove(dt); return; }
      if (this.state === 'ladder') { this.updateLadder(dt); this.afterMove(dt); return; }
      if (this.inWater() && !(this.boots && !W().isBrine(this.cx(), this.cy()) ) && !this.bootsBrine()) { this.updateSwim(dt); this.afterMove(dt); return; }
      this.updateGround(dt);
      this.afterMove(dt);
    }
    bootsBrine() { return this.boots && W().isBrine(this.cx(), this.cy()); }

    updateTimers(dt) {
      const In = I();
      // Reload locks Maren in place.
      if (this.reloadT > 0) {
        this.reloadT -= dt;
        if (this.reloadT <= 0) this.finishReload();
      }
      if (this.craftT > 0) {
        this.craftT -= dt;
        if (this.craftT <= 0) { const rc = this.craftRecipe; this.bag.pay(rc.cost); this.bag.add(rc.makes, rc.n); H.Audio.craft(); H.toast(`Crafted ${rc.n} ${H.ITEMS[rc.makes].name}.`, 2); }
      }
      if (this.healT > 0) {
        this.healT -= dt;
        if (this.healKind === 'poultice') {
          this.healTick -= dt;
          if (this.healTick <= 0 && this.healLeft > 0) { this.healTick = 3 / this.healTotal; this.healLeft--; this.hp = Math.min(this.maxHp, this.hp + 1); }
        } else if (this.healKind === 'stitch' && this.healT <= 0) {
          this.bag.remove('stitchkit', 1); this.hp = this.maxHp; H.Audio.heal(); H.toast('Stitched. Full health.');
        }
      }
      if (In.hit('reload')) this.startReload();
      if (In.hit('poultice')) this.usePoultice();
      if (In.hit('stitch')) this.useStitch();
      if (In.hit('quickcraft')) this.quickCraft('rivets');
      if (In.hit('shutter')) { this.shut = !this.shut; H.Audio.ui(); H.toast(this.shut ? 'Lantern shuttered. You are nearly invisible, and blind to weak points.' : 'Lantern open.', 2); }
      if (In.hit('boots') && this.has('boots')) { this.boots = !this.boots; H.Audio.thud(); H.toast(this.boots ? 'Ballast Boots: on. You sink.' : 'Ballast Boots: off.', 1.5); }
      for (let i = 1; i <= 4; i++) if (In.hit('w' + i) && this.weaponOrder[i - 1]) this.selectWeapon(this.weaponOrder[i - 1]);
      if (In.mouse.wheel) {
        const i = this.weaponOrder.indexOf(this.cur);
        this.selectWeapon(this.weaponOrder[(i + In.mouse.wheel + this.weaponOrder.length) % this.weaponOrder.length]);
      }
      if (!this.locked() && this.healKind !== 'stitch') {
        if (In.mouse.l || In.held('fire')) this.fire();
        if (In.hit('gaff')) this.gaffStrike();
        if (In.hit('burn')) this.burnBody();
        if (In.hit('interact')) G().interact();
      }
    }

    updateLantern(dt) {
      const In = I();
      this.focusing = (In.mouse.r || In.held('focus')) && !this.shut && this.oil > 0;
      if (!this.shut && this.oil > 0 && !this.inBellRoom) this.oil -= dt * (this.focusing ? 1 : 0.5) * (G().room.bellRoom ? 0 : 1);
      if (this.oil <= 0) {
        this.oil = 0;
        if (this.bag.count('oil') > 0 && !this.shut) { this.bag.remove('oil', 1); this.oil = 90; H.toast('You refill the lantern from an oil flask.', 2); }
      }
      this.beamPath = this.focusing ? this.traceBeam() : null;
    }
    // Focused beam path, bent by prisms.
    traceBeam() {
      const lp = this.lanternPos();
      let x = lp.x, y = lp.y, ang = this.aim;
      const path = [{ x, y }];
      let budget = 1100;
      const used = new Set();
      for (let seg = 0; seg < 6 && budget > 0; seg++) {
        const dx = Math.cos(ang), dy = Math.sin(ang);
        let hit = null;
        let d = 0;
        for (; d < budget; d += 3) {
          const px = x + dx * d, py = y + dy * d;
          if (W().solidPx(px, py)) break;
          for (const e of G().ents) {
            if ((e.type === 'prism' && !used.has(e)) || (e.type === 'crystal')) {
              const c = e.center();
              if (Math.abs(px - c.x) < 6 && Math.abs(py - c.y) < 6) { hit = e; break; }
            }
          }
          if (hit) break;
        }
        const ex = x + dx * d, ey = y + dy * d;
        path.push({ x: ex, y: ey });
        budget -= d;
        if (!hit) break;
        if (hit.type === 'crystal') { hit.hitByBeam(1 / 60); break; }
        used.add(hit); hit.lit = 1;
        const c = hit.center();
        const [ddx, ddy] = H.PRISM_DIRS[hit.dir];
        x = c.x; y = c.y; ang = Math.atan2(ddy, ddx);
        path.push({ x, y });
      }
      return path;
    }

    updateGrabbed(dt) {
      const In = I();
      this.vx = 0; this.vy = 0;
      if (In.hit('gaff') || In.hit('interact') || In.hit('jump')) {
        this.mash--; G().shake(1); H.Audio.gaff();
        if (this.mash <= 0) {
          const e = this.grabbedBy; this.grabbedBy = null; this.state = 'normal';
          if (e) { e.mode = 'recover'; e.atkT = 1.0; e.stun = 0.6; e.vx = -e.facing * 80; }
          this.invulnT = 0.6; this.vx = -this.facing * 120;
          H.toast('Broke free!', 1.2);
        }
      }
      // You can still shoot the grabbing arm.
      if ((In.mouse.lp || In.hit('fire')) && this.fireCool <= 0) this.fire(true);
      if (this.grabbedBy && (this.grabbedBy.gone || this.grabbedBy.mode === 'downed')) { this.grabbedBy = null; this.state = 'normal'; }
    }

    updateGround(dt) {
      const In = I();
      const sneak = In.held('sneak');
      let speed = sneak ? SNEAK : RUN;
      if (this.dragging) speed = 32;
      if (this.healT > 0 && this.healKind === 'poultice') speed *= 0.4;
      if (this.boots && this.inWater()) speed = 85;
      if (this.locked()) speed = 0;
      const dir = (In.held('right') ? 1 : 0) - (In.held('left') ? 1 : 0);
      if (this.dashT > 0) {
        this.dashT -= dt; this.dashInv -= dt;
        if (this.dashT <= 0) this.vx *= 0.4;
      } else {
        const accel = this.onGround ? 1600 : 900;
        this.vx = H.approach(this.vx, dir * speed, accel * dt);
      }
      // Currents at Ebb drag at Maren unless she wears the boots.
      this.applyCurrent(dt);
      this.applyWind(dt);

      if (this.onGround) { this.coyote = COYOTE; this.airDashed = false; } else this.coyote -= dt;
      if (In.hit('jump')) this.jumpBuf = BUFFER; else this.jumpBuf -= dt;

      // Ladders
      this.ladderCool = (this.ladderCool || 0) - dt;
      if ((In.held('up') || (In.held('down') && !this.onGround && this.ladderCool <= 0)) && this.onLadderTile() && !this.dragging) { this.state = 'ladder'; this.vx = 0; this.vy = 0; this.x = Math.floor(this.cx() / TS) * TS + 8 - this.w / 2; return; }
      if (In.held('down') && this.onGround && this.ladderBelow() && !this.dragging) { this.state = 'ladder'; this.y += 4; this.x = Math.floor(this.cx() / TS) * TS + 8 - this.w / 2; return; }

      // Wall-hang on timber (never wet stone).
      const wallDir = dir !== 0 ? dir : 0;
      const hangTile = wallDir && this.wallAt(wallDir);
      if (!this.onGround && hangTile && this.vy > 0 && !this.dragging) {
        this.vy = Math.min(this.vy, 20);
        this.hanging = wallDir;
      } else this.hanging = 0;

      if (this.jumpBuf > 0 && !this.locked()) {
        if (In.held('down') && this.onGround && this.onOneWay()) { this.dropping = 0.25; this.jumpBuf = 0; }
        else if (this.coyote > 0 && !this.dragging) {
          this.vy = this.boots && this.inWater() ? -300 : -JUMP; this.coyote = 0; this.jumpBuf = 0;
          H.Audio.jump(); H.Noise.emit(this.cx(), this.y + this.h, 2, 'jump');
        } else if (this.hanging) {
          this.vy = -380; this.vx = -this.hanging * 190; this.facing = -this.hanging; this.jumpBuf = 0; this.hanging = 0; H.Audio.jump();
        }
      }
      if (In.up('jump') && this.vy < -120 && this.dashT <= 0) this.vy *= 0.5;
      if (this.dropping > 0) this.dropping -= dt;

      // Dash: short, 0.2 s of invulnerability. Jet Valve adds air and water bursts.
      if (In.hit('dash') && this.dashCool <= 0 && !this.locked() && !this.dragging) this.dash();

      // Gaff swing
      if ((In.hit('swing') || (In.held('swing') && !this.onGround && (!this.lastRing || this.lastRing.t <= 0.3))) && this.has('swing') && !this.locked()) this.startSwing(!In.hit('swing'));

      // gravity
      const g = this.boots && this.inWater() ? 900 : GRAV;
      if (this.dashT <= 0 || !this.dashAir) this.vy += g * dt;
      const maxFall = this.boots && this.inWater() ? 220 : 520;
      if (this.vy > maxFall) this.vy = maxFall;
      if (this.dashT > 0 && this.dashAir) this.vy = this.dashVy;

      const wasGround = this.onGround;
      const vyBefore = this.vy;
      if (!wasGround && this.prevY === undefined) this.prevY = this.y;
      H.Phys.move(this, dt);
      if (this.platform) { this.y += this.platform.dy || 0; }
      // landing
      if (this.onGround && !wasGround && vyBefore > 250) {
        H.Noise.emit(this.cx(), this.y + this.h, this.noiseScale() * 3, 'land');
        H.Audio.step(this.onMetal(), 0.18);
        if (this.boots && !this.hasFitting('soles')) this.stomp();
      }
      // footsteps
      if (this.onGround && Math.abs(this.vx) > 20) {
        this.stepT -= dt * Math.abs(this.vx) / 60;
        if (this.stepT <= 0) {
          this.stepT = 1;
          const m = (sneak ? 2 : 6) * this.noiseScale();
          H.Noise.emit(this.cx(), this.y + this.h, m, 'step');
          H.Audio.step(this.onMetal(), sneak ? 0.04 : 0.1);
        }
      }
    }
    noiseScale() {
      let s = 1;
      if (this.onMetal()) s *= 1.5;
      if (this.hasFitting('soles')) s *= 0.5;
      return s;
    }
    onMetal() {
      const ch = W().charAt(Math.floor(this.cx() / TS), Math.floor((this.y + this.h + 2) / TS));
      return ch === 'M' || ch === 'P';
    }
    onOneWay() { return W().solid(Math.floor(this.cx() / TS), Math.floor((this.y + this.h + 2) / TS)) === 2 || (this.platform && this.platform.oneWay); }
    onLadderTile() { return W().isLadder(Math.floor(this.cx() / TS), Math.floor((this.y + this.h - 4) / TS)) || W().isLadder(Math.floor(this.cx() / TS), Math.floor((this.y + 4) / TS)); }
    ladderBelow() { return W().isLadder(Math.floor(this.cx() / TS), Math.floor((this.y + this.h + 4) / TS)); }
    wallAt(dir) {
      const tx = Math.floor((dir > 0 ? this.x + this.w + 2 : this.x - 2) / TS);
      return W().isWallHang(tx, Math.floor((this.y + 6) / TS)) && W().isWallHang(tx, Math.floor((this.y + this.h - 6) / TS));
    }
    // Ribvault gusts: telegraphed by streaks of salt dust, they push Maren sideways.
    windAt() {
      const r = G().room;
      if (!r.windZones) return 0;
      const tx = Math.floor(this.cx() / TS) - r.ox, ty = Math.floor(this.cy() / TS) - r.oy;
      for (const z of r.windZones) {
        if (tx < z.x0 || tx > z.x1 || ty < z.y0 || ty > z.y1) continue;
        const ph = (G().stats.time % z.period) / z.period;
        // calm for half the cycle, then a gust that alternates direction each cycle
        const dir = Math.floor(G().stats.time / z.period) % 2 ? -1 : 1;
        return ph < 0.5 ? 0 : dir * z.force * Math.sin((ph - 0.5) * 2 * Math.PI);
      }
      return 0;
    }
    applyWind(dt) {
      const w = this.windAt();
      if (!w || this.boots) return;
      this.vx += w * dt * (this.onGround ? 0.5 : 1.6);
    }
    applyCurrent(dt) {
      if (H.Tide.phase() !== 'ebbing' || this.boots) return;
      const r = G().room;
      if (!r.seaward || !this.inWater()) return;
      this.vx += r.seaward * 160 * dt;
    }

    updateLadder(dt) {
      const In = I();
      const tx = Math.floor(this.cx() / TS);
      this.vx = 0;
      const v = (In.held('down') ? 1 : 0) - (In.held('up') ? 1 : 0);
      this.vy = v * 95;
      const ny = this.y + this.vy * dt;
      // stop at the top or bottom of the ladder
      const headTile = Math.floor((ny + 2) / TS), footTile = Math.floor((ny + this.h - 2) / TS);
      if (!W().isLadder(tx, headTile) && !W().isLadder(tx, footTile)) { this.state = 'normal'; return; }
      if (v > 0 && W().solid(tx, Math.floor((ny + this.h) / TS)) === 1) { this.state = 'normal'; this.y = Math.floor((ny + this.h) / TS) * TS - this.h; this.vy = 0; this.onGround = true; this.ladderCool = 0.3; return; }
      if (v < 0 && !W().isLadder(tx, headTile) && !W().isLadder(tx, footTile) && !W().isLadder(tx, Math.floor((ny + this.h - 10) / TS))) {
        // Step off the top rung onto it.
        this.state = 'normal';
        const top = Math.floor((ny + this.h + 2) / TS);
        if (W().solid(tx, top) >= 1) { this.y = top * TS - this.h; this.vy = 0; this.onGround = true; } else this.vy = -120;
        return;
      }
      this.y = ny;
      if (Math.abs(this.vy) > 1) { this.stepT -= dt * 3; if (this.stepT <= 0) { this.stepT = 1; H.Audio.step(true, 0.05); } }
      if (In.hit('jump')) { this.state = 'normal'; this.vy = -250; const d = (In.held('right') ? 1 : 0) - (In.held('left') ? 1 : 0); this.vx = d * 140; }
      else if ((In.held('left') || In.held('right')) && !In.held('up') && !In.held('down')) { this.state = 'normal'; }
      this.onGround = false;
    }

    updateSwim(dt) {
      const In = I();
      const brine = W().isBrine(this.cx(), this.cy());
      const dx = (In.held('right') ? 1 : 0) - (In.held('left') ? 1 : 0);
      const dy = (In.held('down') ? 1 : 0) - (In.held('up') ? 1 : 0);
      let sp = this.locked() ? 0 : 105;
      if (this.dragging) sp = 40;
      if (this.dashT > 0) { this.dashT -= dt; this.dashInv -= dt; }
      else {
        this.vx = H.approach(this.vx, dx * sp, 500 * dt);
        this.vy = H.approach(this.vy, dy * sp, 500 * dt);
      }
      // buoyancy
      let buoy = 25;
      if (this.hasFitting('lead')) buoy = 160;
      // Brine floats you like a cork; the Gullet's pressure will not let an unsealed suit dive.
      const crushed = W().isDeep(this.cx(), this.cy()) && !this.has('seal');
      if (brine || crushed) buoy = -520;
      if (this.dashT <= 0) this.vy += buoy * dt;
      if ((brine || crushed) && this.vy < -80) this.vy = -80;
      this.applyCurrent(dt);
      // surface: jump out
      const surf = W().surfaceAt(this.cx(), this.cy());
      const atSurface = this.y + 4 < surf + 4;
      if (In.hit('jump') && atSurface && !this.locked()) { this.vy = -330; H.Audio.splash(0.2); H.FX.splash(this.cx(), surf, 8); }
      if (In.hit('dash') && this.dashCool <= 0 && !this.locked() && !this.dragging) this.dash(true);
      if (In.hit('swing') && this.has('swing')) this.startSwing();
      if ((In.held('up') || In.held('down')) && this.onLadderTile() && !this.dragging) { this.state = 'ladder'; this.vx = 0; this.vy = 0; this.x = Math.floor(this.cx() / TS) * TS + 8 - this.w / 2; return; }
      this.coyote = 0;
      H.Phys.move(this, dt);
      if (Math.random() < dt * 2 && this.headUnder()) H.FX.bubbles(this.cx() + this.facing * 3, this.y + 3);
      this.stepT -= dt;
      if (this.stepT <= 0 && (Math.abs(this.vx) > 30 || Math.abs(this.vy) > 30)) { this.stepT = 0.6; H.Noise.emit(this.cx(), this.cy(), 3 * (this.hasFitting('soles') ? 0.7 : 1), 'swim'); }
    }

    dash(water) {
      const In = I();
      const jet = this.has('jet');
      const inAir = !this.onGround && !water;
      if ((inAir || water) && !jet) {
        if (water) { this.vx += this.facing * 60; this.dashCool = 0.6; }
        return;
      }
      if (inAir && this.airDashed) return;
      let dx = (In.held('right') ? 1 : 0) - (In.held('left') ? 1 : 0);
      let dy = (In.held('down') ? 1 : 0) - (In.held('up') ? 1 : 0);
      if (!jet || (!inAir && !water)) dy = 0;
      if (!dx && !dy) dx = this.facing;
      const len = Math.hypot(dx, dy); dx /= len; dy /= len;
      const slow = this.hasFitting('lead') ? 0.75 : 1;
      let sp = (inAir || water ? 440 : 340) * slow;
      if (inAir || water) {
        if (!this.grafts.has('lung') && !(this.breath >= 3)) { H.toast('Not enough suit air for the Jet Valve.', 1.5); return; }
        if (!this.grafts.has('lung')) this.breath -= 3;
        H.Audio.burst(); H.FX.bubbles(this.cx(), this.cy(), 8);
        this.airDashed = inAir;
      }
      this.vx = dx * sp; this.vy = dy * sp;
      this.dashVy = dy * sp; this.dashAir = inAir || water;
      this.dashT = inAir || water ? 0.22 : 0.15;
      this.dashInv = 0.2;
      this.dashCool = 0.5;
      H.Audio.jump();
    }
    stomp() {
      for (const e of G().ents) {
        if (!e.isBody || e.gone || !e.knots) continue;
        if (H.rectsOverlap({ x: this.x - 4, y: this.y + this.h - 4, w: this.w + 8, h: 12 }, e.hitBox())) {
          const k = e.knots.filter((k) => !k.broken && !e.isSealed(k)).sort((a, b) => {
            const pa = e.knotPos(a), pb = e.knotPos(b);
            return H.dist(pa.x, pa.y, this.cx(), this.y + this.h) - H.dist(pb.x, pb.y, this.cx(), this.y + this.h);
          })[0];
          if (k) { e.damageKnot(k, 3, 'stomp'); G().shake(3); H.toast('Stomp!', 1); }
          this.vy = -200;
          return;
        }
      }
    }

    // ---- gaff ------------------------------------------------------------------
    startSwing(ringOnly) {
      if (this.dragging) return;
      // Prefer a ring within reach and in sight.
      let best = null, bd = 1e9;
      const r = G().room;
      const tx0 = Math.floor((this.cx() - 110) / TS), tx1 = Math.floor((this.cx() + 110) / TS);
      const ty0 = Math.floor((this.cy() - 110) / TS), ty1 = Math.floor((this.cy() + 40) / TS);
      for (let ty = ty0; ty <= ty1; ty++) for (let tx = tx0; tx <= tx1; tx++) {
        if (W().charAt(tx, ty) !== 'o') continue;
        const rx = tx * TS + 8, ry = ty * TS + 8;
        let d = H.dist(this.cx(), this.y + 4, rx, ry);
        // Chaining: skip the ring just released and favour rings ahead of the swing.
        if (this.lastRing && this.lastRing.t > 0 && this.lastRing.x === rx && this.lastRing.y === ry) continue;
        if (!this.onGround && Math.abs(this.vx) > 40 && Math.sign(rx - this.cx()) === Math.sign(this.vx)) d *= 0.6;
        if (d < 110 * (this.onGround ? 1 : 1.25) && d < bd && ry < this.y + 8 && H.Phys.los(this.cx(), this.y + 4, rx, ry)) { bd = d; best = { x: rx, y: ry }; }
      }
      if (best) {
        // Keep the rope short enough that the bottom of the arc clears the floor.
        const floorD = H.Phys.ray(best.x, best.y, Math.PI / 2, 200);
        const len = Math.max(24, Math.min(H.dist(this.cx(), this.y + 4, best.x, best.y), 78, floorD - 22));
        const ang = Math.atan2(this.cx() - best.x, (this.y + 4) - best.y);
        this.swing = { ax: best.x, ay: best.y, len, ang, av: (this.vx * Math.cos(ang) - this.vy * Math.sin(ang)) / len };
        this.state = 'swing'; H.Audio.hook(); this.vy = 0;
        return;
      }
      if (ringOnly) return;
      // Otherwise a hook pull on a light enemy in front of you.
      for (const e of G().ents) {
        if (!e.isBody || !e.alive || !e.alive() || e.boss || e.type === 'undertow') continue;
        if (e.type === 'saltwright' && !(e.pinT > 0 && e.tetherable)) continue;
        if (e.pinT > 0 && e.tetherable && H.dist(this.cx(), this.cy(), e.cx(), e.cy()) < 160) {
          e.pinT = 0; e.vx = Math.sign(this.cx() - e.cx()) * 260; e.vy = -120; e.stun = 1.2;
          H.Audio.hook(); H.toast('Tether: yanked off the wall!', 1);
          return;
        }
        const d = H.dist(this.cx(), this.cy(), e.cx(), e.cy());
        const a = Math.atan2(e.cy() - this.cy(), e.cx() - this.cx());
        if (d < 90 && Math.abs(H.angleDiff(this.aim, a)) < 0.5 && H.Phys.los(this.cx(), this.cy(), e.cx(), e.cy())) {
          if (e.type === 'gullet' && e.mode === 'hang') { e.drop(true); }
          else { e.vx = -Math.cos(a) * 220; e.vy = -140; e.stun = 1.0; }
          // Tether: yank a pinned enemy toward you.
          H.Audio.hook(); H.toast('Gaff pull!', 0.8);
          return;
        }
      }
    }
    updateSwing(dt) {
      const In = I(), s = this.swing;
      const dir = (In.held('right') ? 1 : 0) - (In.held('left') ? 1 : 0);
      const g = 1100;
      // Pumping adds energy in the direction of motion, like a real swing.
      const pump = dir !== 0 && (Math.sign(s.av) === dir || Math.abs(s.av) < 0.5) ? dir * 7 : 0;
      s.av += (-g / s.len * Math.sin(s.ang) + pump) * dt;
      s.av *= 1 - 0.15 * dt;
      s.av = H.clamp(s.av, -6, 6);
      if (In.held('up')) s.len = Math.max(24, s.len - 60 * dt);
      if (In.held('down')) s.len = Math.min(110, H.Phys.ray(s.ax, s.ay, Math.PI / 2, 200) - 22, s.len + 60 * dt);
      s.ang += s.av * dt;
      const nx = s.ax + Math.sin(s.ang) * s.len - this.w / 2;
      const ny = s.ay + Math.cos(s.ang) * s.len - 4;
      if (H.Phys.boxSolid(nx, ny, this.w, this.h)) { s.av *= -0.4; }
      else { this.x = nx; this.y = ny; }
      this.facing = s.av >= 0 ? 1 : -1;
      if (!In.held('swing') || In.hit('jump')) {
        // Keep momentum on release.
        // A jump-release flings a little harder than simply letting go.
        const boost = In.hit('jump') ? 1.2 : 1;
        this.vx = Math.cos(s.ang) * s.av * s.len * boost;
        this.vy = -Math.sin(s.ang) * s.av * s.len * boost - (In.hit('jump') ? 160 : 0);
        this.lastRing = { x: s.ax, y: s.ay, t: 0.45 };
        this.state = 'normal'; this.swing = null;
      }
    }
    gaffStrike() {
      if (this.gaffCool > 0) return;
      if (this.gaffLooseT > 0) { H.Audio.deny(); return; }
      this.gaffCool = 0.38;
      this.gaffAnim = 0.18;
      H.Audio.gaff();
      const reach = 26;
      const hx = this.cx() + Math.cos(this.aim) * 16, hy = this.y + 9 + Math.sin(this.aim) * 16;
      const knotMul = this.hasFitting('splitter') ? 2 : 1, fleshMul = this.hasFitting('splitter') ? 0.5 : 1;
      let hitSomething = false;
      // Knots in reach first.
      let best = null, bd = reach;
      for (const e of G().ents) {
        if (!e.isBody || e.gone || !e.knots) continue;
        for (const k of e.knots) {
          if (k.broken) continue;
          const p = e.knotPos(k); if (!p) continue;
          const d = H.dist(hx, hy, p.x, p.y);
          if (d < bd) { bd = d; best = { e, k }; }
        }
      }
      if (best) {
        best.e.damageKnot(best.k, 1 * knotMul, 'gaff');
        if (this.grafts.has('arm') && !best.e.boss) { best.e.crust[best.k.id] = 3; best.e.stun = Math.max(best.e.stun, best.e.type === 'saltwright' ? 1.5 : 3); H.FX.salt(best.e.cx(), best.e.cy(), 10); }
        hitSomething = true;
      } else {
        for (const e of G().ents) {
          if (!e.isBody || e.gone) continue;
          const b = e.hitBox();
          if (hx > b.x - 10 && hx < b.x + b.w + 10 && hy > b.y - 10 && hy < b.y + b.h + 10) {
            e.damageFlesh(1 * fleshMul, hx, hy, 'gaff');
            // Gaff shove knocks light enemies back.
            if (e.type === 'shucker' || e.type === 'driftling' || e.type === 'grafted') { e.vx = Math.sign(e.cx() - this.cx()) * 140; e.stun = Math.max(e.stun, 0.35); }
            if (this.grafts.has('arm') && e.knots) { const k = e.knots.find((k) => !k.broken); if (k) e.crust[k.id] = 3; e.stun = Math.max(e.stun, 1.5); }
            hitSomething = true; break;
          }
        }
      }
      for (const e of G().ents) {
        if ((e.onGaff) && H.dist(hx, hy, e.cx(), e.cy()) < reach + 8) { e.onGaff(); hitSomething = true; }
      }
      H.Noise.emit(hx, hy, hitSomething ? 4 : 2, 'gaff');
    }

    // ---- ranged ------------------------------------------------------------------
    selectWeapon(w) { if (w && this.weapons[w] && w !== this.cur) { this.cur = w; this.reloadT = 0; H.Audio.ui(); } }
    startReload() {
      const w = this.weapons[this.cur], def = H.WEAPONS[this.cur];
      if (this.reloadT > 0 || w.mag >= this.magSize(this.cur)) return;
      if (this.bag.count(def.ammo) <= 0) { H.toast('No ' + H.ITEMS[def.ammo].name.toLowerCase() + ' in the satchel.', 1.5); H.Audio.empty(); return; }
      this.reloadT = def.reload * (this.cur === 'driver' && this.hasFitting('feeder') ? 0.6 : 1);
      H.Audio.reload();
    }
    finishReload() {
      const w = this.weapons[this.cur], def = H.WEAPONS[this.cur];
      const need = this.magSize(this.cur) - w.mag;
      w.mag += this.bag.remove(def.ammo, need);
    }
    fire(grabbed) {
      if (this.fireCool > 0) return;
      const w = this.weapons[this.cur], def = H.WEAPONS[this.cur];
      if (w.mag <= 0) {
        if (I().mouse.lp || I().hit('fire')) { H.Audio.empty(); this.startReload(); }
        this.fireCool = 0.2;
        return;
      }
      this.fireCool = def.cooldown;
      const lp = { x: this.cx() + Math.cos(this.aim) * 6, y: this.y + 9 + Math.sin(this.aim) * 6 };
      const pan = 0;
      if (this.cur === 'driver') {
        const shots = w.tier >= 1 && w.mag >= 2 ? 2 : 1;
        w.mag -= shots;
        let ang = this.aim;
        // Knot-seeker: snaps aim to a visible knot within 10 degrees.
        if (w.tier >= 2) {
          let best = null, bd = 0.175;
          for (const e of G().ents) if (e.isBody && !e.gone && e.knots) for (const k of e.knots) {
            if (k.broken || e.isSealed(k)) continue;
            const p = e.knotPos(k); if (!p) continue;
            if (H.Render.knotVisibility(p.x, p.y, e) < 0.3) continue;
            const a = Math.atan2(p.y - lp.y, p.x - lp.x);
            const d = Math.abs(H.angleDiff(this.aim, a));
            if (d < bd && H.Phys.los(lp.x, lp.y, p.x, p.y)) { bd = d; best = a; }
          }
          if (best !== null) ang = best;
        }
        for (let i = 0; i < shots; i++) {
          const a = ang + (i ? 0.03 : 0);
          const hit = this.shootRay(lp.x, lp.y, a, 340);
          G().tracers.push({ x1: lp.x, y1: lp.y, x2: hit.x, y2: hit.y, t: 0.08 });
          if (hit.kind === 'knot') hit.target.damageKnot(hit.knot, 2, 'rivet');
          else if (hit.kind === 'body') hit.target.damageFlesh(2, hit.x, hit.y, 'rivet');
          else if (hit.kind === 'object') hit.target.onShot();
          else if (hit.kind === 'wall') { H.FX.sparks(hit.x, hit.y, 3); H.Audio.tink(); this.ricochet(hit.x, hit.y); }
        }
        H.Audio.rivet(pan);
        H.Noise.emit(this.cx(), this.cy(), 10, 'rivet');
        G().shake(1);
      } else if (this.cur === 'harpoon') {
        w.mag--;
        G().projectiles.push(new H.Projectile('bolt', lp.x, lp.y, Math.cos(this.aim) * 520, Math.sin(this.aim) * 520, { grav: 120 }));
        H.Audio.harpoon(pan);
        H.Noise.emit(this.cx(), this.cy(), 6, 'harpoon');
      } else if (this.cur === 'sling') {
        w.mag--;
        const life = w.tier >= 2 ? 20 : 10;
        G().projectiles.push(new H.Projectile('flare', lp.x, lp.y, Math.cos(this.aim) * 300, Math.sin(this.aim) * 300 - 40, { grav: 500, life }));
        H.Audio.sling(pan); H.Audio.flare(pan);
        H.Noise.emit(this.cx(), this.cy(), 4, 'sling');
      } else if (this.cur === 'sawline') {
        this.placeSawLine(w);
      }
      if (w.mag <= 0) this.startReload();
    }
    shootRay(x, y, ang, maxD) {
      const hit = H.hitscan(x, y, ang, maxD);
      // shootable objects (oil barrels)
      const ex = hit.x, ey = hit.y;
      for (const e of G().ents) {
        if (!e.shootable || e.done) continue;
        const t = H.rayRect(x, y, ex, ey, e.hitBox());
        if (t >= 0 && t < (hit.t || 1)) return { kind: 'object', target: e, x: x + (ex - x) * t, y: y + (ey - y) * t, t };
      }
      return hit;
    }
    ricochet(x, y) {
      // A rivet against a bell or metal pulls attention there.
      const ch = W().charAt(Math.floor(x / TS), Math.floor(y / TS));
      if (ch === 'M') H.Noise.emit(x, y, 8, 'clang');
    }
    placeSawLine(w) {
      const y = this.y + this.h - 6;
      const lines = G().sawlines.filter((s) => !s.dead);
      const maxLines = w.tier >= 1 ? 2 : 1;
      if (lines.length >= maxLines) { lines[0].dead = true; }
      let x2 = this.cx();
      for (let i = 0; i < 10 * TS; i += 4) { x2 = this.cx() + this.facing * i; if (W().solidPx(x2, y)) break; }
      w.mag--;
      G().sawlines.push(new H.SawLine(this.cx(), x2, y, w.tier >= 2));
      H.Audio.hook(); H.toast('Saw-Line strung.', 1);
    }

    // ---- healing and crafting --------------------------------------------------------
    usePoultice() {
      if (this.healT > 0 || this.locked()) return;
      if (this.bag.count('poultice') <= 0) { H.toast('No poultices.', 1.5); H.Audio.deny(); return; }
      if (this.hp >= this.maxHp) { H.toast('You are not hurt.', 1); return; }
      this.bag.remove('poultice', 1);
      this.healKind = 'poultice'; this.healT = 3;
      this.healTotal = this.healLeft = 2 + (this.hasFitting('bilge') ? 1 : 0);
      this.healTick = 3 / this.healTotal;
      H.Audio.heal();
    }
    useStitch() {
      if (this.healT > 0 || this.locked()) return;
      if (this.bag.count('stitchkit') <= 0) { H.toast('No Stitch Kits.', 1.5); H.Audio.deny(); return; }
      if (this.hp >= this.maxHp) { H.toast('You are not hurt.', 1); return; }
      this.healKind = 'stitch'; this.healT = 4; this.vx = 0;
      H.toast('Kneeling to stitch (4 s)...', 2);
    }
    quickCraft(id) {
      const rc = H.RECIPES.find((r) => r.id === id);
      if (!rc || this.craftT > 0 || this.locked()) return false;
      if (!this.bag.has(rc.cost)) { H.toast('Missing materials for ' + H.ITEMS[rc.makes].name + '.', 1.5); H.Audio.deny(); return false; }
      if (this.bag.room(rc.makes) < rc.n) { H.toast('No room in the satchel.', 1.5); H.Audio.deny(); return false; }
      if (rc.where === 'bench') return false;
      this.craftRecipe = rc; this.craftT = rc.time; this.vx = 0;
      H.toast(`Crafting ${H.ITEMS[rc.makes].name}... (${rc.time} s, crouched)`, rc.time);
      return true;
    }

    // ---- bodies: drag and burn ----------------------------------------------------------
    nearestDownedBody(r = 22) {
      let best = null, bd = r;
      for (const e of G().ents) {
        if (!e.isBody || e.gone || e.boss) continue;
        if (!(e.mode === 'downed' || e.corpse || e.mode === 'captured')) continue;
        const d = Math.abs(e.cx() - this.cx()) + Math.abs(e.feet() - (this.y + this.h)) * 0.5;
        if (d < bd + e.w / 2) { bd = d; best = e; }
      }
      return best;
    }
    startDrag(e) { this.dragging = e; e.dragged = true; H.Audio.hook(); }
    stopDrag() {
      if (!this.dragging) return;
      const e = this.dragging; e.dragged = false; e.vy = 0; e.vx = 0;
      this.dragging = null;
      if (H.World.aboveTideLine(G().room, e.cx(), e.feet() - 2) && e.mode === 'downed') H.toast('Hauled above the tide line. It will stay down.', 2.5);
    }
    burnBody() {
      const e = this.nearestDownedBody(26);
      if (!e) return;
      if (this.oil < 30) { H.toast('Not enough lantern oil to burn it (needs a third of a flask).', 2); H.Audio.deny(); return; }
      this.oil -= 30;
      if (this.dragging === e) this.stopDrag();
      e.burn();
      H.Noise.emit(e.cx(), e.cy(), 6, 'fire');
      H.toast('You douse it with oil and set it alight. It will not stand again.', 2.5);
    }

    afterMove(dt) {
      // Dragged body follows behind.
      if (this.dragging) {
        const e = this.dragging;
        if (e.gone || this.state === 'ladder' || this.state === 'swing') { this.stopDrag(); }
        else {
          const tx = this.cx() - this.facing * 16 - e.w / 2;
          e.vx = (tx - e.x) * 8; e.vy = this.inWater() ? (this.y + this.h - e.h - e.y) * 6 : e.vy + 1100 * dt;
          e.facing = -this.facing;
          const save = e.dragged; e.dragged = false;
          H.Phys.move(e, dt);
          e.dragged = save;
          if (Math.abs(e.cx() - this.cx()) > 60) this.stopDrag();
        }
      }
      // breath
      const max = this.breathMax();
      const vent = W().charAt(Math.floor(this.cx() / TS), Math.floor((this.y + 4) / TS)) === 'a';
      if (this.headUnder() && !vent && isFinite(max)) {
        this.breath -= dt;
        if (Math.random() < dt * 3) H.FX.bubbles(this.cx() + this.facing * 3, this.y + 2);
        if (this.breath <= 0) {
          this.breath = 0; this.drownT -= dt;
          if (this.drownT <= 0) { this.drownT = 2; this.invulnT = 0; this.hurt(1); H.toast('Drowning!', 1); }
        }
      } else {
        this.breath = Math.min(isFinite(max) ? max : 30, this.breath + dt * (vent ? 12 : 8));
        this.drownT = 0;
      }
      // Gullet pressure without the seal.
      if (W().isDeep(this.cx(), this.cy()) && !this.has('seal')) {
        this.pressureT -= dt;
        G().pressureWarn = 1;
        if (this.pressureT <= 0) { this.pressureT = 1.5; this.invulnT = 0; this.hurt(1); H.toast('The pressure crushes your helmet. You need a Pressure Seal.', 2); }
      } else this.pressureT = 0.5;
      // salt crystals and fire
      if (W().charAt(Math.floor(this.cx() / TS), Math.floor((this.y + this.h - 2) / TS)) === 'x') { this.hurt(1); this.vy = -260; }
      if (H.Fire.burnsAt(this.cx(), this.y + this.h - 4)) this.hurt(1);
      // splash in/out
      const inW = this.inWater();
      if (inW !== this.wasIn) { if (Math.abs(this.vy) > 60) { H.Audio.splash(0.2); H.FX.splash(this.cx(), W().surfaceAt(this.cx(), this.cy()), 8); H.Noise.emit(this.cx(), this.cy(), 4, 'splash'); } this.wasIn = inW; }
      if (this.gaffAnim > 0) this.gaffAnim -= dt;
    }

    // ---- render -------------------------------------------------------------------------------
    draw(c) {
      if (this.dead) return;
      const x = this.cx(), gy = this.y + this.h, f = this.facing;
      const blink = this.invulnT > 0 && Math.floor(this.invulnT * 20) % 2 === 0;
      if (blink) c.globalAlpha = 0.5;
      const kneel = (this.healT > 0 && this.healKind === 'stitch') || this.craftT > 0;
      const run = this.onGround && Math.abs(this.vx) > 20 ? Math.sin(performance.now() / 80) : 0;
      const bodyTop = kneel ? gy - 16 : gy - 22;
      // legs
      c.strokeStyle = '#2a2622'; c.lineWidth = 3; c.lineCap = 'round';
      if (kneel) { c.beginPath(); c.moveTo(x, gy - 8); c.lineTo(x + f * 5, gy - 2); c.lineTo(x - f * 2, gy); c.stroke(); }
      else if (this.state === 'ladder') { c.beginPath(); c.moveTo(x - 2, gy - 9); c.lineTo(x - 2, gy - 1 + run); c.moveTo(x + 2, gy - 9); c.lineTo(x + 2, gy - 3 - run); c.stroke(); }
      else { c.beginPath(); c.moveTo(x, gy - 9); c.lineTo(x + f * run * 4, gy); c.moveTo(x, gy - 9); c.lineTo(x - f * run * 4, gy); c.stroke(); }
      // diving coat
      c.fillStyle = '#4a3c2c';
      c.fillRect(x - 4.5, bodyTop + 8, 9, kneel ? 8 : 12);
      c.fillStyle = '#3a2e22'; c.fillRect(x - 4.5, bodyTop + 14, 9, 2);
      // boots
      if (this.has('boots')) { c.fillStyle = this.boots ? '#6a5a40' : '#3a3228'; c.fillRect(x - 4, gy - 3, 8, 3); }
      // brass helmet
      c.fillStyle = '#b08840';
      c.beginPath(); c.arc(x, bodyTop + 4.5, 5.5, 0, Math.PI * 2); c.fill();
      c.fillStyle = '#8a6a30'; c.fillRect(x - 5, bodyTop + 7, 10, 2.5);
      c.fillStyle = this.headUnder() ? '#7aa8b8' : '#2a3a40';
      c.beginPath(); c.arc(x + f * 2, bodyTop + 4.5, 2.6, 0, Math.PI * 2); c.fill();
      c.fillStyle = 'rgba(255,240,200,0.6)'; c.fillRect(x + f * 2 - 1, bodyTop + 3, 1, 1);
      // lantern on the chest
      c.fillStyle = this.lanternLit() ? '#ffd27a' : '#3a3020';
      c.fillRect(x + f * 3 - 1.5, bodyTop + 9, 3, 3);
      // arm + gaff or weapon toward aim
      const ax = x + f * 1, ay = bodyTop + 10;
      const aim = this.state === 'swing' ? Math.atan2(this.swing.ay - ay, this.swing.ax - ax) : this.aim;
      c.strokeStyle = '#3a2e22'; c.lineWidth = 2.5;
      const hx = ax + Math.cos(aim) * 7, hy = ay + Math.sin(aim) * 7;
      c.beginPath(); c.moveTo(ax, ay); c.lineTo(hx, hy); c.stroke();
      if (this.state === 'swing') {
        c.strokeStyle = '#8a8a8a'; c.lineWidth = 1; c.beginPath(); c.moveTo(hx, hy); c.lineTo(this.swing.ax, this.swing.ay); c.stroke();
      } else if (this.gaffAnim > 0) {
        const a2 = aim + (this.gaffAnim / 0.18 - 0.5) * 1.6 * f;
        c.strokeStyle = '#8a8a8a'; c.lineWidth = 1.5;
        c.beginPath(); c.moveTo(hx, hy); c.lineTo(hx + Math.cos(a2) * 14, hy + Math.sin(a2) * 14); c.stroke();
        c.beginPath(); c.arc(hx + Math.cos(a2) * 14, hy + Math.sin(a2) * 14, 2.5, a2, a2 + Math.PI * 1.2 * f, f < 0); c.stroke();
      } else {
        const col = { driver: '#7a7a72', harpoon: '#8a9aa0', sling: '#8a6a4a', sawline: '#aaaaaa' }[this.cur];
        c.strokeStyle = col; c.lineWidth = 3;
        c.beginPath(); c.moveTo(hx, hy); c.lineTo(hx + Math.cos(aim) * 7, hy + Math.sin(aim) * 7); c.stroke();
      }
      if (this.reloadT > 0) { c.fillStyle = '#c9b37a'; c.fillRect(x - 6, bodyTop - 5, 12 * (1 - this.reloadT / H.WEAPONS[this.cur].reload), 1.5); }
      if (this.healT > 0) { c.fillStyle = '#7fbf7f'; c.fillRect(x - 6, bodyTop - 5, 12 * (this.healT / (this.healKind === 'stitch' ? 4 : 3)), 1.5); }
      if (this.craftT > 0) { c.fillStyle = '#c0c0c0'; c.fillRect(x - 6, bodyTop - 5, 12 * (1 - this.craftT / this.craftRecipe.time), 1.5); }
      // graft knots on Maren herself
      if (this.grafts.size) {
        c.fillStyle = 'rgba(200,245,255,0.85)';
        if (this.grafts.has('ear')) c.fillRect(x - f * 3, bodyTop + 8, 2, 2);
        if (this.grafts.has('arm')) c.fillRect(hx - 1, hy - 1, 2, 2);
        if (this.grafts.has('lung')) c.fillRect(x - 1, bodyTop + 12, 2, 2);
      }
      c.globalAlpha = 1;
    }

    toJSON() {
      return {
        maxHp: this.maxHp, hp: this.hp, oil: this.oil, abilities: [...this.abilities], weapons: this.weapons, weaponOrder: this.weaponOrder,
        cur: this.cur, bag: this.bag.toJSON(), fittingsOwned: [...this.fittingsOwned], fittings: [...this.fittings], pressure: this.pressure,
        grafts: [...this.grafts], shut: this.shut,
      };
    }
    load(o) {
      Object.assign(this, { maxHp: o.maxHp, hp: o.hp, oil: o.oil, weapons: o.weapons, weaponOrder: o.weaponOrder, cur: o.cur, pressure: o.pressure, shut: o.shut });
      this.abilities = new Set(o.abilities); this.bag = H.Satchel.Bag.from(o.bag);
      this.fittingsOwned = new Set(o.fittingsOwned); this.fittings = new Set(o.fittings); this.grafts = new Set(o.grafts);
      this.breath = this.breathMax(); if (!isFinite(this.breath)) this.breath = 30;
    }
  }
  H.Player = Player;
})();
