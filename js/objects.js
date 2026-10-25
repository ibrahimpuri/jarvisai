// Interactable objects. Each has x,y,w,h in world pixels, a prompt and an interact().
'use strict';
(() => {
  const TS = H.TILE;
  const G = () => H.Game;
  const P = () => H.Game.player;
  const W = () => H.World;

  class Obj {
    constructor(spec, room) {
      this.spec = spec; this.room = room; this.key = spec.key; this.type = spec.type;
      this.w = spec.w || 12; this.h = spec.h || 14;
      this.x = spec.x - this.w / 2; this.y = spec.y - this.h;
      this.done = false; this.t = 0;
    }
    cx() { return this.x + this.w / 2; }
    cy() { return this.y + this.h / 2; }
    near(p, r = 22) { return Math.abs(p.cx() - this.cx()) < r + this.w / 2 && Math.abs(p.cy() - this.cy()) < 26 + this.h / 2; }
    prompt() { return null; }
    interact() {}
    update(dt) { this.t += dt; }
    draw(c) {}
    light() { return null; }
    serialize() { return this.done ? { done: true } : null; }
    restore(s) { if (s && s.done) this.done = true; }
  }

  // ---------------------------------------------------------------- containers
  class Chest extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 16; this.h = 11; this.x = spec.x - 8; this.y = spec.y - 11; this.contents = Object.assign({}, spec.loot || {}); this.fitting = spec.fitting; }
    hidden() { return this.spec.needsDrained && !W().flags.choirDrained; }
    prompt() { return this.hidden() ? null : this.done ? null : 'E  Open ' + (this.spec.label || 'container'); }
    interact() {
      if (this.hidden() || this.done) return;
      const p = P();
      // Lifeline rule: scarcity bites without soft-locking anyone.
      const rivets = p.bag.count('rivets') + (p.weapons.driver ? p.weapons.driver.mag : 0);
      const mats = H.Satchel.MATERIALS.some((m) => p.bag.count(m) > 0);
      if (rivets < 3 && !mats && !this.lifeline && (G().difficulty !== 'drowned' || Math.random() < 0.5)) { this.contents.scrap = (this.contents.scrap || 0) + 2; this.lifeline = true; }
      const got = [];
      for (const [item, n] of Object.entries(this.contents)) {
        const n2 = G().difficulty === 'pilgrim' && item === 'scrap' ? n + 1 : n;
        const left = p.bag.add(item, n2);
        if (n2 - left > 0) got.push(`${n2 - left} ${H.ITEMS[item].name}`);
        if (left > 0) this.contents[item] = left; else delete this.contents[item];
      }
      if (this.fitting) { p.fittingsOwned.add(this.fitting); got.push('fitting: ' + H.FITTINGS[this.fitting].name); this.fitting = null; }
      if (Object.keys(this.contents).length === 0) this.done = true;
      H.Audio.pickup();
      if (got.length) H.toast('Found ' + got.join(', ') + '.');
      if (!this.done) H.toast('Your satchel is full. Some is left behind.');
      if (this.spec.id === 'ch_cache') G().onCacheOpened();
    }
    draw(c) {
      if (this.hidden()) return;
      c.fillStyle = this.done ? '#2a2420' : '#4a3a28';
      c.fillRect(this.x, this.y + 2, this.w, this.h - 2);
      c.fillStyle = this.done ? '#3a3028' : '#6a5238';
      c.fillRect(this.x - 1, this.y, this.w + 2, 4);
      if (!this.done) { c.fillStyle = '#c0a060'; c.fillRect(this.cx() - 1, this.y + 3, 2, 3); }
    }
    serialize() { return { done: this.done, contents: this.contents, fitting: this.fitting }; }
    restore(s) { this.done = s.done; if (s.contents) this.contents = s.contents; this.fitting = s.fitting; }
  }

  class Pickup extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 8; this.h = 8; this.x = spec.x - 4; this.y = spec.y - 8; this.vy = 0; }
    prompt() { return null; }
    update(dt) {
      super.update(dt);
      const p = P();
      if (!this.done && p && H.rectsOverlap(p.box(), { x: this.x - 4, y: this.y - 4, w: this.w + 8, h: this.h + 8 })) this.collect(p);
      // settle on the ground
      const e = { x: this.x, y: this.y, w: this.w, h: this.h, vx: 0, vy: (this.vy = Math.min(this.vy + 600 * dt, 200)) };
      if (W().isWater(this.cx(), this.cy())) e.vy = Math.min(e.vy, 30);
      H.Phys.move(e, dt);
      this.x = e.x; this.y = e.y; this.vy = e.vy;
    }
    collect(p) {
      const item = this.spec.item, n = this.spec.n || 1;
      const left = p.bag.add(item, n);
      if (left === n) { if (!this.warned) { H.toast('No room in the satchel for ' + H.ITEMS[item].name + '.'); this.warned = true; } return; }
      this.spec.n = left;
      H.Audio.pickup();
      H.toast(`+${n - left} ${H.ITEMS[item].name}`, 2);
      if (left <= 0) { this.done = true; G().removeLoose(this); }
      if (item === 'tags') G().onTagsTaken();
      if (item === 'crank') G().onCrankTaken();
    }
    draw(c) {
      if (this.done) return;
      const col = H.ITEMS[this.spec.item].color;
      const bob = Math.sin(this.t * 3) * 1;
      c.fillStyle = 'rgba(255,230,160,0.15)'; c.beginPath(); c.arc(this.cx(), this.cy() + bob, 7, 0, Math.PI * 2); c.fill();
      c.fillStyle = col; c.fillRect(this.x + 1, this.y + 2 + bob, this.w - 2, this.h - 3);
    }
  }

  class Sign extends Obj {
    prompt() { return 'E  Read'; }
    interact() { H.UI.say(null, this.spec.text); }
    draw(c) {
      c.fillStyle = 'rgba(230,230,210,0.5)';
      c.fillRect(this.cx() - 5, this.y + 2, 10, 1); c.fillRect(this.cx() - 4, this.y + 5, 8, 1); c.fillRect(this.cx() - 5, this.y + 8, 7, 1);
    }
  }

  // ---------------------------------------------------------------- Bell Room
  class Bell extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 18; this.h = 30; this.x = spec.x - 9; this.y = spec.y - 30; }
    lit() { return !!G().bellsLit[this.spec.id]; }
    silenced() { return this.spec.id === 'bell_chapel' && G().quest('tables') === 'sold'; }
    prompt() { return this.silenced() ? 'E  The bell is silent' : G().bellRoomBreached(this.room.id) ? 'E  The bell is drowned out' : this.lit() ? 'E  Bell Room' : 'E  Rekindle the bell lamp (1 oil flask)'; }
    interact() {
      if (this.silenced()) { H.UI.say(null, 'The bell hangs cracked and silent. Without its resonance, this room is no longer safe.'); return; }
      if (G().bellRoomBreached(this.room.id)) { H.UI.say(null, 'Brine water has breached the room and the bell\'s note is drowned out. Clear the room before you can rest here.'); return; }
      if (!this.lit()) {
        const p = P();
        if (p.bag.count('oil') < 1) { H.UI.say(null, 'The bell lamp is cold. You need an oil flask to rekindle it.'); H.Audio.deny(); return; }
        p.bag.remove('oil', 1);
        G().bellsLit[this.spec.id] = true;
        H.Audio.roomBell();
        H.toast('Bell Room rekindled. No Brine body will enter while it rings.');
        G().setRespawn(this);
        G().save();
      }
      H.UI.bellMenu(this);
    }
    light() { return this.lit() && !this.silenced() ? { x: this.cx(), y: this.y + 6, r: 150, i: 0.9, color: '255,200,110' } : null; }
    draw(c) {
      const x = this.cx(), y = this.y;
      c.fillStyle = '#3a2a1a'; c.fillRect(x - 10, y, 2, 30); c.fillRect(x + 8, y, 2, 30); c.fillRect(x - 10, y, 20, 3);
      const sw = Math.sin(this.t * 1.5) * (this.lit() ? 0.06 : 0.01);
      c.save(); c.translate(x, y + 3); c.rotate(sw);
      c.fillStyle = this.silenced() ? '#4a4030' : '#a08040';
      c.beginPath(); c.moveTo(-3, 0); c.lineTo(3, 0); c.lineTo(7, 14); c.lineTo(-7, 14); c.closePath(); c.fill();
      c.fillStyle = '#c0a060'; c.fillRect(-8, 13, 16, 2);
      c.restore();
      c.fillStyle = this.lit() ? '#ffd27a' : '#3a3020';
      c.fillRect(x - 2, y + 22, 4, 6);
    }
  }
  class Reliquary extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 18; this.h = 12; this.x = spec.x - 9; this.y = spec.y - 12; }
    prompt() { return 'E  Reliquary chest'; }
    interact() { H.UI.reliquary(); }
    draw(c) {
      c.fillStyle = '#5a4030'; c.fillRect(this.x, this.y + 2, this.w, this.h - 2);
      c.fillStyle = '#c0a060'; c.fillRect(this.x, this.y, this.w, 3); c.fillRect(this.cx() - 2, this.y + 4, 4, 4);
    }
  }
  class Bench extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 22; this.h = 12; this.x = spec.x - 11; this.y = spec.y - 12; }
    prompt() { return 'E  Bench: craft, upgrade, fittings'; }
    interact() { H.UI.bench(this.room.id === 'chapel'); }
    draw(c) {
      c.fillStyle = '#4a3a2a'; c.fillRect(this.x, this.y + 3, this.w, 3);
      c.fillRect(this.x + 1, this.y + 6, 2, 6); c.fillRect(this.x + this.w - 3, this.y + 6, 2, 6);
      c.fillStyle = '#8a8a8a'; c.fillRect(this.x + 4, this.y, 5, 3);
      c.fillStyle = '#6a5a3a'; c.fillRect(this.x + 13, this.y + 1, 6, 2);
    }
  }

  class NPC extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 12; this.h = 26; this.x = spec.x - 6; this.y = spec.y - 26; this.id = spec.id; }
    present() { return H.Story.npcPresent(this.id, this.room.id); }
    prompt() { return this.present() ? 'E  Speak with ' + H.Story.npcName(this.id) : null; }
    interact() { if (this.present()) H.Story.talk(this.id); }
    draw(c) {
      if (!this.present()) return;
      const x = this.cx(), gy = this.y + this.h, f = P() && P().cx() < x ? -1 : 1;
      const look = H.Story.npcLook(this.id);
      c.fillStyle = look.robe;
      c.beginPath(); c.moveTo(x - 6, gy); c.lineTo(x - 4, gy - 18); c.lineTo(x + 4, gy - 18); c.lineTo(x + 6, gy); c.fill();
      c.fillStyle = look.skin; c.beginPath(); c.arc(x + f, gy - 21, 3.6, 0, Math.PI * 2); c.fill();
      if (look.hat) { c.fillStyle = look.hat; c.fillRect(x - 4 + f, gy - 26, 8, 3); }
      if (look.crust) { c.fillStyle = 'rgba(230,240,250,0.8)'; c.fillRect(x - 4, gy - 14, 3, 2); c.fillRect(x + 1, gy - 9, 2, 2); }
      if (look.lamp) { c.fillStyle = '#ffd080'; c.fillRect(x + f * 6, gy - 12, 3, 3); }
    }
    light() { const l = this.present() && H.Story.npcLook(this.id).lamp; return l ? { x: this.cx(), y: this.cy(), r: 50, i: 0.5, color: '255,200,120' } : null; }
  }

  // ---------------------------------------------------------------- Wrackline objects
  class Socket extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 12; this.h = 14; }
    prompt() { return W().flags.seawallOpen ? null : P().bag.count('crank') ? 'E  Fit the Sluice Crank' : 'E  Examine gate socket'; }
    interact() {
      if (W().flags.seawallOpen) return;
      const p = P();
      if (!p.bag.count('crank')) { H.UI.say(null, 'An empty socket for a sluice crank. Ansel said the crank is in the Tithe Hall, below the seawall.'); return; }
      p.bag.remove('crank', 1);
      W().flags.seawallOpen = true;
      H.Audio.rumble(); G().shake(4);
      H.toast('The seawall gate grinds open. The way up to the Saltworks is clear.');
      G().checkpointQuest('crank');
    }
    draw(c) {
      c.fillStyle = '#3a3a3a'; c.fillRect(this.x, this.y, this.w, this.h);
      c.fillStyle = W().flags.seawallOpen ? '#c08040' : '#1a1a1a'; c.fillRect(this.cx() - 2, this.y + 4, 4, 4);
      if (W().flags.seawallOpen) { c.fillStyle = '#c08040'; c.fillRect(this.cx() - 1, this.y - 6, 2, 10); c.fillRect(this.cx() - 5, this.y - 6, 10, 2); }
    }
  }
  class Crank extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 14; this.h = 14; }
    prompt() { return this.done ? null : 'E  Lift the Sluice Crank from the altar'; }
    interact() {
      if (this.done) return;
      const p = P();
      if (p.bag.room('crank') < 1) { H.toast('The crank takes 2 satchel slots. Make room first.'); H.Audio.deny(); return; }
      p.bag.add('crank', 1);
      this.done = true;
      H.Audio.pickup();
      G().onCrankTaken();
    }
    draw(c) {
      c.fillStyle = '#5a4a3a'; c.fillRect(this.x - 4, this.y + 6, this.w + 8, 8);
      c.fillStyle = '#d8d0c0'; c.fillRect(this.x - 5, this.y + 5, this.w + 10, 2);
      if (!this.done) { c.fillStyle = '#c08040'; c.fillRect(this.cx() - 1, this.y - 4, 2, 10); c.fillRect(this.cx() - 6, this.y - 4, 12, 2); }
    }
  }
  class Barrel extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 12; this.h = 14; this.shootable = true; }
    prompt() { return this.done ? null : 'F  Kick over (or shoot) the oil barrel'; }
    hitBox() { return { x: this.x, y: this.y, w: this.w, h: this.h }; }
    onShot() { this.burst(); }
    onGaff() { this.burst(); }
    burst() {
      if (this.done) return;
      this.done = true;
      H.FX.spawn(this.cx(), this.cy(), 30, { color: '#ffb040', glow: true, smin: 40, smax: 200 });
      H.Fire.spill(this.cx(), this.y + this.h - 4, 4);
      G().shake(4);
    }
    draw(c) {
      if (this.done) { c.fillStyle = '#2a2018'; c.fillRect(this.x, this.y + this.h - 4, this.w + 4, 4); return; }
      c.fillStyle = '#6a4a2a'; c.fillRect(this.x, this.y, this.w, this.h);
      c.fillStyle = '#3a3a3a'; c.fillRect(this.x, this.y + 3, this.w, 1.5); c.fillRect(this.x, this.y + this.h - 4, this.w, 1.5);
    }
  }
  class Pews extends Obj {
    update() {}
    draw(c) {
      const gy = this.room.py + 17 * TS;
      c.fillStyle = '#3a2e22';
      for (const [a, b] of this.spec.spans) {
        const x0 = this.room.px + a * TS, x1 = this.room.px + (b + 1) * TS;
        c.fillRect(x0, gy - 8, x1 - x0, 3);
        c.fillRect(x0, gy - 14, 3, 14);
        c.fillRect(x1 - 4, gy - 8, 2, 8);
      }
    }
  }

  // ---------------------------------------------------------------- Saltworks objects
  const SEQ = ['sea', 'saint', 'kiln'];
  class Lever extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 10; this.h = 16; }
    prompt() { const f = W().flags; return f.basinCrossed ? null : f.basinJam ? 'E  The gates are jammed until the Ebb' : 'E  Throw the ' + { wave: 'wave', skull: 'skull', flame: 'flame' }[this.spec.icon] + ' lever'; }
    pulled() { return (W().flags.basinSeq || []).includes(this.spec.id); }
    interact() {
      const f = W().flags;
      if (f.basinCrossed || f.basinJam || this.pulled()) { H.Audio.deny(); return; }
      f.basinSeq = (f.basinSeq || []).concat(this.spec.id);
      H.Audio.thud(); H.Audio.reload();
      const i = f.basinSeq.length - 1;
      if (f.basinSeq[i] !== SEQ[i]) {
        f.basinJam = true; f.basinTarget = 19;
        H.Audio.rumble();
        H.toast('Wrong order. The pan drains with a groan and the gates jam until the Ebb.');
        return;
      }
      f.basinTarget = [15, 11, 7][i];
      if (f.basinSeq.length === 3) {
        f.basinFilling = true;
        H.toast('The sluices open in order. The pan floods and the barge lifts. The hatch is closing!');
        H.Audio.rumble();
      } else H.toast('Water spills into the pan.');
    }
    draw(c) {
      c.fillStyle = '#3a3a3a'; c.fillRect(this.cx() - 4, this.y + this.h - 4, 8, 4);
      const a = this.pulled() ? 0.6 : -0.6;
      c.save(); c.translate(this.cx(), this.y + this.h - 3); c.rotate(a);
      c.fillStyle = '#7a6040'; c.fillRect(-1, -12, 2, 12);
      c.restore();
      c.fillStyle = '#c0a060';
      const ix = this.cx(), iy = this.y - 4;
      if (this.spec.icon === 'wave') { c.fillRect(ix - 4, iy, 3, 1); c.fillRect(ix - 1, iy - 1, 3, 1); c.fillRect(ix + 2, iy, 3, 1); }
      if (this.spec.icon === 'skull') { c.fillRect(ix - 2, iy - 2, 4, 3); c.fillStyle = '#000'; c.fillRect(ix - 1, iy - 1, 1, 1); c.fillRect(ix + 1, iy - 1, 1, 1); }
      if (this.spec.icon === 'flame') { c.fillStyle = '#ff9040'; c.fillRect(ix - 1, iy - 3, 2, 4); c.fillRect(ix - 2, iy - 1, 4, 2); }
    }
  }
  class Barge extends Obj {
    constructor(spec, room) { super(spec, room); this.w = (spec.w || 4) * TS; this.h = 6; this.x = spec.x - 8; this.y = spec.y - 6; }
    update(dt) {
      const surf = W().surfaceAt(this.cx(), this.y);
      const floorY = this.room.py + 19 * TS - this.h;
      const target = Math.min(floorY, surf - this.h * 0.4);
      this.dy = H.approach(this.y, target, 40 * dt) - this.y;
      this.y += this.dy;
    }
    platformRect() { return { x: this.x, y: this.y, w: this.w, h: this.h, oneWay: true, dy: this.dy || 0 }; }
    draw(c) {
      c.fillStyle = '#5a4a38'; c.fillRect(this.x, this.y, this.w, this.h);
      c.fillStyle = '#e8e8f0'; c.fillRect(this.x + 4, this.y - 4, this.w - 8, 4);
      c.fillStyle = '#3a2e22'; c.fillRect(this.x, this.y + this.h - 2, this.w, 2);
    }
  }
  class Floater extends Obj {
    constructor(spec, room) { super(spec, room); this.w = (spec.w || 3) * TS; this.h = 5; this.x = spec.x - 8; this.restY = spec.y - 5; this.y = this.restY; }
    update(dt) {
      const surf = W().surfaceAt(this.cx(), this.y);
      const target = Math.min(this.restY, surf - 3 + Math.sin(this.t * 1.3 + this.x) * 1);
      this.t += dt;
      this.dy = H.approach(this.y, target, 60 * dt) - this.y;
      this.y += this.dy;
    }
    platformRect() { return { x: this.x, y: this.y, w: this.w, h: this.h, oneWay: true, dy: this.dy || 0 }; }
    draw(c) {
      c.fillStyle = '#3a2e22'; c.fillRect(this.x, this.y, this.w, 3);
      c.fillRect(this.x, this.y - 7, 3, 10); c.fillRect(this.x + this.w - 4, this.y, 2, 5);
    }
  }
  class Lift extends Obj {
    constructor(spec, room) { super(spec, room); this.w = (spec.w || 4) * TS; this.h = 6; this.x = spec.x - 8; this.y = spec.y - TS; this.drop = 0; }
    update(dt) {
      if (this.done) return;
      if (this.falling) {
        this.drop += dt; this.y += 220 * dt;
        if (this.drop > 1.2) { this.done = true; this.openShaft(); }
        return;
      }
      // Count knotless bodies resting on the pan.
      let n = 0;
      for (const e of G().ents) {
        if (!e.isBody || e.gone) continue;
        const knotless = e.corpse || e.mode === 'downed';
        if (knotless && !e.dragged && e.cx() > this.x && e.cx() < this.x + this.w && Math.abs(e.feet() - this.y) < 6) n++;
      }
      this.load = n;
      if (n >= 2) {
        this.falling = true;
        H.Audio.rumble(); G().shake(5);
        H.toast('The pan takes the dead weight. The lift drops into the Gullet!');
        for (const e of G().ents) if (e.isBody && e.cx() > this.x && e.cx() < this.x + this.w && Math.abs(e.feet() - this.y) < 8) { e.gone = true; }
      }
    }
    openShaft() {
      const tx = Math.floor((this.x + 24) / TS);
      const ty0 = Math.floor(this.room.py / TS) + 17;
      for (let ty = ty0; ty < ty0 + 6; ty++) W().setChar(tx, ty, 'H');
      W().flags.liftDropped = true;
    }
    platformRect() { return this.done ? null : { x: this.x, y: this.y, w: this.w, h: this.h, oneWay: false, dy: this.falling ? 220 / 60 : 0 }; }
    prompt() { return this.done ? null : 'Lift pan: ' + (this.load || 0) + ' / 2 bodies. Drag knotless dead onto it (E near a body).'; }
    draw(c) {
      if (this.done) return;
      c.fillStyle = '#5a5a5a'; c.fillRect(this.x, this.y, this.w, this.h);
      c.strokeStyle = '#7a7a7a'; c.lineWidth = 1;
      c.beginPath(); c.moveTo(this.x + 2, this.y); c.lineTo(this.cx(), this.y - 30 - this.drop * 40); c.lineTo(this.x + this.w - 2, this.y); c.stroke();
    }
    serialize() { return { done: this.done }; }
  }

  // ---------------------------------------------------------------- Ribvault puzzle
  const DIRS = [[1, 0], [0, 1], [-1, 0], [0, -1]];
  class Prism extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 10; this.h = 12; this.x = spec.x - 5; this.y = spec.y - 12 + 2; this.dir = spec.dir || 0; this.lit = 0; }
    prompt() { return 'E  Turn the prism'; }
    interact() { this.dir = (this.dir + 1) % 4; H.Audio.ui(); H.Audio.tink(); }
    center() { return { x: this.room.px + this.spec.tx * TS + 8, y: this.room.py + this.spec.ty * TS + 8 }; }
    draw(c) {
      const p = this.center();
      c.fillStyle = '#4a4a4a'; c.fillRect(p.x - 1, p.y + 4, 2, 4);
      c.save(); c.translate(p.x, p.y); c.rotate(Math.PI / 4);
      c.fillStyle = this.lit > 0 ? 'rgba(255,250,220,0.95)' : 'rgba(210,240,255,0.7)'; c.fillRect(-4, -4, 8, 8);
      c.restore();
      const [dx, dy] = DIRS[this.dir];
      c.fillStyle = '#ffd080'; c.fillRect(p.x + dx * 6 - 1, p.y + dy * 6 - 1, 2, 2);
      this.lit = Math.max(0, this.lit - 0.05);
    }
    serialize() { return { dir: this.dir }; }
    restore(s) { if (s && s.dir !== undefined) this.dir = s.dir; }
  }
  class Crystal extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 10; this.h = 14; this.charge = 0; }
    center() { return { x: this.room.px + this.spec.tx * TS + 8, y: this.room.py + this.spec.ty * TS + 8 }; }
    hitByBeam(dt) {
      if (this.done) return;
      this.charge += dt;
      if (this.charge > 2) {
        this.done = true;
        H.Audio.bell(660, 0.4); H.Audio.crack(); G().shake(3);
        // The crystal wakes and the salt seals around the vault crack open.
        for (let y = 0; y < this.room.th; y++) for (let x = 0; x < this.room.tw; x++) {
          if (W().charAt(this.room.ox + x, this.room.oy + y) === 'S') { W().setChar(this.room.ox + x, this.room.oy + y, ' '); H.FX.salt(this.room.px + x * TS + 8, this.room.py + y * TS + 8, 6); }
        }
        H.toast('The crystal wakes with a ringing note. Salt shears away somewhere above.');
      }
    }
    update(dt) { super.update(dt); if (!this.done) this.charge = Math.max(0, this.charge - dt * 0.3); }
    light() { return this.done ? { x: this.center().x, y: this.center().y, r: 80, i: 0.7, color: '200,240,255' } : null; }
    draw(c) {
      const p = this.center();
      c.fillStyle = this.done ? '#e0f8ff' : `rgba(120,150,170,${0.6 + this.charge * 0.2})`;
      c.beginPath(); c.moveTo(p.x, p.y - 8); c.lineTo(p.x + 5, p.y + 2); c.lineTo(p.x, p.y + 8); c.lineTo(p.x - 5, p.y + 2); c.fill();
    }
  }

  // ---------------------------------------------------------------- Sunken Choir objects
  class TunedBell extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 14; this.h = 18; }
    prompt() { return 'E / F  Strike the tuned bell'; }
    basinLevel() { return W().flags.choirLevelName === 'raised' ? 3 : (W().flags.basin || 0); }
    interact() { this.ring(); }
    onGaff() { this.ring(); }
    ring() {
      const lvl = this.basinLevel();
      H.Audio.bell([392, 330, 262, 196][lvl], 0.5);
      // Crystal walls in this room shatter at their own pitch.
      const pitches = this.room.crystals || {};
      let broke = false;
      for (const [k, pitch] of Object.entries(pitches)) {
        if (pitch !== lvl) continue;
        const col = parseInt(k.split('@')[1], 10);
        for (let y = 0; y < this.room.th; y++) {
          if (W().charAt(this.room.ox + col, this.room.oy + y) === 'C') { W().setChar(this.room.ox + col, this.room.oy + y, ' '); H.FX.salt(this.room.px + col * TS + 8, this.room.py + y * TS + 8, 14); broke = true; }
        }
      }
      if (broke) { H.Audio.crack(); H.toast('A crystal wall sings and shatters.'); }
      else H.toast(['The bell rings high and thin.', 'The bell rings a little lower.', 'The bell rings low.', 'The bell booms, deep as the sea.'][lvl], 2.5);
    }
    draw(c) {
      const x = this.cx(), y = this.y;
      c.fillStyle = '#4a3a2a'; c.fillRect(x - 7, y, 14, 2);
      c.fillStyle = '#9a8a5a'; c.beginPath(); c.moveTo(x - 2, y + 2); c.lineTo(x + 2, y + 2); c.lineTo(x + 5, y + 11); c.lineTo(x - 5, y + 11); c.fill();
      // basin below with its water level
      c.fillStyle = '#3a3a40'; c.fillRect(x - 7, y + 12, 14, 6);
      const lvl = this.basinLevel();
      c.fillStyle = '#5a8aa0'; c.fillRect(x - 6, y + 17 - lvl * 1.6, 12, lvl * 1.6);
    }
  }
  class Valve extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 10; this.h = 10; }
    prompt() { return 'E  Turn the basin valve (level ' + (W().flags.basin || 0) + ')'; }
    interact() { W().flags.basin = ((W().flags.basin || 0) + 1) % 3; H.Audio.ui(); H.Audio.splash(0.1); }
    draw(c) {
      c.strokeStyle = '#8a6a3a'; c.lineWidth = 1.5; c.beginPath(); c.arc(this.cx(), this.cy(), 4, 0, Math.PI * 2); c.stroke();
      c.beginPath(); c.moveTo(this.cx() - 4, this.cy()); c.lineTo(this.cx() + 4, this.cy()); c.moveTo(this.cx(), this.cy() - 4); c.lineTo(this.cx(), this.cy() + 4); c.stroke();
    }
  }
  class Sluice extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 12; this.h = 18; }
    prompt() { return P().has('tidekey') ? 'E  Work the sluice with the Tide Key' : 'E  Examine the sluice'; }
    interact() {
      if (!P().has('tidekey')) { H.UI.say(null, 'A sluice wheel with a keyhole shaped like a wave. The Choir\'s water is held here.'); return; }
      H.UI.sluiceMenu();
    }
    draw(c) {
      c.fillStyle = '#3a3a3a'; c.fillRect(this.x, this.y + 4, this.w, this.h - 4);
      c.strokeStyle = '#8a7a5a'; c.lineWidth = 1.5; c.beginPath(); c.arc(this.cx(), this.y + 4, 5, 0, Math.PI * 2); c.stroke();
    }
  }
  class Font extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 16; this.h = 12; }
    prompt() { return H.Story.choirFriendly() ? 'E  The Brine font' : null; }
    interact() { H.UI.fontMenu(); }
    draw(c) {
      c.fillStyle = '#6a6a72'; c.fillRect(this.x + 3, this.y + 4, this.w - 6, this.h - 4);
      c.fillStyle = '#8a8a92'; c.fillRect(this.x, this.y, this.w, 5);
      c.fillStyle = 'rgba(200,245,255,0.8)'; c.fillRect(this.x + 2, this.y + 1, this.w - 4, 2);
    }
    light() { return { x: this.cx(), y: this.y, r: 40, i: 0.4, color: '180,230,255' }; }
  }
  class Bowl extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 12; this.h = 10; }
    prompt() { return H.Story.choirFriendly() && G().quest('tables') !== 'given' && G().quest('tables') !== 'sold' ? 'E  The offering bowl' : null; }
    interact() { H.Story.offerTables(); }
    draw(c) { c.fillStyle = '#7a6a4a'; c.beginPath(); c.ellipse(this.cx(), this.y + 4, 6, 3, 0, 0, Math.PI); c.fill(); c.fillRect(this.cx() - 1, this.y + 5, 2, 5); }
  }

  // ---------------------------------------------------------------- collectibles
  class Collectible extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 10; this.h = 10; this.y -= 2; }
    update(dt) {
      super.update(dt);
      const p = P();
      if (!this.done && p && H.rectsOverlap(p.box(), { x: this.x - 2, y: this.y - 2, w: this.w + 4, h: this.h + 4 })) { this.done = true; G().collect(this.spec); }
    }
    light() { return this.done ? null : { x: this.cx(), y: this.cy(), r: 36, i: 0.5, color: this.spec.type === 'page' || this.spec.type === 'table' ? '255,240,200' : '255,210,120' }; }
    draw(c) {
      if (this.done) return;
      const x = this.cx(), y = this.cy() + Math.sin(this.t * 2.5) * 1.5;
      const t = this.spec.type;
      if (t === 'stitch') { c.fillStyle = '#e06060'; c.beginPath(); c.arc(x - 2, y - 1, 2.5, 0, Math.PI * 2); c.arc(x + 2, y - 1, 2.5, 0, Math.PI * 2); c.fill(); c.beginPath(); c.moveTo(x - 4.5, y); c.lineTo(x, y + 5); c.lineTo(x + 4.5, y); c.fill(); }
      else if (t === 'seal') { c.fillStyle = '#c8a050'; c.beginPath(); c.arc(x, y, 4, 0, Math.PI * 2); c.fill(); c.fillStyle = '#8a6a30'; c.beginPath(); c.arc(x, y, 2, 0, Math.PI * 2); c.fill(); }
      else if (t === 'page' || t === 'table') { c.fillStyle = t === 'page' ? '#f0e6cc' : '#d8e6f0'; c.fillRect(x - 3, y - 4, 6, 8); c.fillStyle = '#6a5a4a'; c.fillRect(x - 2, y - 2, 4, 0.8); c.fillRect(x - 2, y, 4, 0.8); }
      else if (t === 'ability' || t === 'weapon') {
        c.fillStyle = 'rgba(255,220,140,0.25)'; c.beginPath(); c.arc(x, y, 7, 0, Math.PI * 2); c.fill();
        c.fillStyle = '#e0c070'; c.save(); c.translate(x, y); c.rotate(this.t); c.fillRect(-3, -3, 6, 6); c.restore();
      }
    }
  }

  class WrackPool extends Obj {
    draw(c) {
      c.strokeStyle = 'rgba(120,180,200,0.35)'; c.lineWidth = 1;
      const x = this.cx(), y = this.y + this.h;
      for (let i = 0; i < 3; i++) { c.beginPath(); c.ellipse(x, y - 1, 4 + i * 4 + Math.sin(this.t * 2 + i) * 1, 1.5 + i, 0, 0, Math.PI * 2); c.stroke(); }
    }
  }
  class Start extends Obj { draw() {} }
  class DriftNest extends Obj {
    constructor(spec, room) { super(spec, room); this.spawned = []; }
    update(dt) {
      const high = this.spec.always || H.Tide.phase() === 'high';
      this.spawned = this.spawned.filter((d) => !d.gone);
      if (high && this.spawned.length === 0 && !this.used && W().isWater(this.cx(), this.cy())) {
        this.used = true;
        const n = this.spec.n || 6;
        for (let i = 0; i < n; i++) {
          const d = new H.BodyTypes.driftling({ x: this.cx() + H.rand(-20, 20), y: this.cy() + H.rand(-10, 10), key: 'drift' + H.uid(), nest: this }, this.room);
          this.spawned.push(d); G().ents.push(d);
        }
      }
      if (!high && !this.spec.always) { for (const d of this.spawned) d.gone = true; this.spawned = []; this.used = false; }
    }
    serialize() { return null; }
  }

  // Dropped satchel after death.
  class SatchelDrop extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 12; this.h = 9; this.x = spec.x - 6; this.y = spec.y - 9; this.bag = spec.bag; }
    update(dt) {
      super.update(dt);
      const p = P();
      if (p && !p.dead && H.rectsOverlap(p.box(), this)) G().recoverSatchel(this);
    }
    light() { return { x: this.cx(), y: this.cy(), r: 30, i: 0.5, color: '255,220,150' }; }
    draw(c) {
      c.fillStyle = '#6a4a2a'; c.fillRect(this.x, this.y + 2, this.w, this.h - 2);
      c.fillStyle = '#8a6a3a'; c.fillRect(this.x + 2, this.y, this.w - 4, 3);
      c.fillStyle = '#c0a060'; c.fillRect(this.cx() - 1, this.y + 3, 2, 2);
    }
    serialize() { return null; }
  }

  // ---------------------------------------------------------------- ending objects
  class Lampwork extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 30; this.h = 40; }
    prompt() { return 'E  The great lamp'; }
    interact() { H.Story.ending('relight'); }
    light() { return { x: this.cx(), y: this.y + 8, r: 60, i: 0.4, color: '255,230,180' }; }
    draw(c) {
      const x = this.cx(), y = this.y;
      c.fillStyle = '#3a3a3a'; c.fillRect(x - 12, y + 24, 24, 16);
      c.fillStyle = '#6a6a5a'; c.fillRect(x - 10, y + 4, 20, 20);
      c.fillStyle = 'rgba(255,240,200,0.4)'; c.fillRect(x - 8, y + 6, 16, 16);
      c.fillStyle = '#c0a060'; c.fillRect(x - 12, y, 24, 4);
    }
  }
  class Beacon extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 12; this.h = 22; }
    prompt() { return 'E  The warning beacon'; }
    interact() { H.Story.ending('dark'); }
    draw(c) { c.fillStyle = '#5a2a1a'; c.fillRect(this.x + 2, this.y + 6, 8, 16); c.fillStyle = '#c03020'; c.fillRect(this.x, this.y, 12, 6); }
  }
  class HymnBell extends Obj {
    constructor(spec, room) { super(spec, room); this.w = 14; this.h = 20; }
    prompt() { return 'E  The Saint\'s bell'; }
    interact() { H.Story.ending('hymn'); }
    draw(c) {
      const x = this.cx();
      c.fillStyle = '#4a4a5a'; c.fillRect(x - 7, this.y, 14, 2);
      c.fillStyle = '#7a8aa0'; c.beginPath(); c.moveTo(x - 2, this.y + 2); c.lineTo(x + 2, this.y + 2); c.lineTo(x + 6, this.y + 14); c.lineTo(x - 6, this.y + 14); c.fill();
    }
  }

  H.ObjTypes = {
    chest: Chest, pickup: Pickup, sign: Sign, bell: Bell, reliquary: Reliquary, bench: Bench, npc: NPC,
    socket: Socket, crank: Crank, barrel: Barrel, pews: Pews, lever: Lever, barge: Barge, floater: Floater, lift: Lift,
    prism: Prism, crystal: Crystal, tunedbell: TunedBell, valve: Valve, sluice: Sluice, font: Font, bowl: Bowl,
    stitch: Collectible, seal: Collectible, page: Collectible, table: Collectible, ability: Collectible, weapon: Collectible,
    wrackpool: WrackPool, start: Start, driftnest: DriftNest, satchel: SatchelDrop,
    lampwork: Lampwork, beacon: Beacon, hymnbell: HymnBell, kiln: H.Kiln,
    choirmember: null,
  };
  H.PRISM_DIRS = DIRS;
})();
