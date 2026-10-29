// The game: loop, rooms, tide consequences, acts, saving.
'use strict';
H.Game = (() => {
  const TS = H.TILE;
  const W = () => H.World;
  const SAVE_KEY = 'hollowmere_save';

  const G = {
    mode: 'title',
    room: null, ents: [], player: null,
    cam: { x: 0, y: 0 },
    projectiles: [], enemyShots: [], sawlines: [], tracers: [], noiseRings: [], frameLights: [],
    act: 1, quests: {}, stats: { knots: {}, byAct: { 1: {}, 2: {}, 3: {} }, deaths: 0, time: 0 },
    pages: new Set(), tables: new Set(), collected: {}, bellsLit: {},
    reliquary: null, respawn: null, satchelDrop: null,
    difficulty: 'keeper', swell: 0, valveT: 0, sinceLoad: 0,
    undertow: null, bossBar: null, encrustedType: null, freeGraft: false,
    shakeAmt: 0, flash: 0, pressureWarn: 0, titleT: 0, promptText: null, dialT: 1,
    volume: 0.7, god: false, ended: null, tips: {},
    wrackPools: {},
  };

  // ------------------------------------------------------------------ setup
  function init() {
    const canvas = document.getElementById('game');
    G.volume = H.Settings.volume;
    H.Audio.setVolume(G.volume);
    H.Input.init(canvas);
    H.Render.init(canvas);
    H.UI.init();
    W().build();
    findWrackPools();
    H.Tide.on(onPhase);
    G.player = new H.Player();
    G.room = H.ROOM.reef;
    G.reliquary = new H.Satchel.Bag(40);
    G.undertow = new H.UndertowSystem();
    // Title backdrop: the reef at night.
    enterRoom(H.ROOM.reef, true);
    placeAtStart();
    H.UI.title();
    // A controller dropping out mid-game pauses, as on a console.
    H.Input.notice = (msg) => {
      if (msg === 'disconnected') { if (G.mode === 'play' && !H.UI.isOpen() && !G.player.dead) H.UI.pause('Controller disconnected. Reconnect it, or carry on with keyboard and mouse.'); }
      else if (G.mode === 'play') H.toast(msg, 2.5);
    };
    let last = performance.now();
    function frame(now) {
      const dt = Math.min(0.033, (now - last) / 1000);
      last = now;
      H.Input.poll(dt);
      if (H.UI.isOpen()) H.UI.padUpdate(dt);
      try { step(dt); } catch (e) { console.error(e); }
      H.Input.endFrame();
      requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);
    // Alt-tabbing away pauses the game.
    window.addEventListener('blur', () => { if (G.mode === 'play' && !H.UI.isOpen() && !G.player.dead) H.UI.pause(); });
  }

  function findWrackPools() {
    for (const r of H.ROOMS) {
      const add = (tx, ty) => { G.wrackPools[r.region] = G.wrackPools[r.region] || { room: r.id, x: r.px + tx * TS + 8, y: r.py + (ty + 1) * TS - 2 }; };
      r.map.forEach((row, ty) => { for (let tx = 0; tx < row.length; tx++) { const e = r.ents && r.ents[row[tx]]; if (e && e.type === 'wrackpool') add(tx, ty); } });
      (r.ents2 || []).forEach((e) => { if (e.type === 'wrackpool') add(e.tx, e.ty); });
    }
  }

  function placeAtStart() {
    const s = G.ents.find((e) => e.type === 'start');
    const p = G.player;
    if (s) { p.x = s.cx() - p.w / 2; p.y = s.y + s.h - p.h; }
    G.respawn = G.respawn || { room: G.room.id, x: p.x, y: p.y };
    snapCamera();
  }

  function newGame(opts) {
    G.difficulty = opts.difficulty;
    H.Tide.scale = opts.tide * (opts.difficulty === 'pilgrim' ? 1.25 : 1);
    H.Tide.t = 0; H.Tide.cycle = 0; H.Tide.spring = 0;
    W().reset();
    G.player = new H.Player();
    if (G.difficulty === 'pilgrim') G.player.bag.add('scrap', 2);
    Object.assign(G, {
      act: 1, quests: {}, stats: { knots: {}, byAct: { 1: {}, 2: {}, 3: {} }, deaths: 0, time: 0 },
      pages: new Set(), tables: new Set(), collected: {}, bellsLit: {}, reliquary: new H.Satchel.Bag(40),
      respawn: null, satchelDrop: null, swell: 0, valveT: 0, undertow: new H.UndertowSystem(), bossBar: null,
      encrustedType: null, freeGraft: false, ended: null, tips: {}, mode: 'play', narr: H.Narr.fresh(),
    });
    H.Narr.reset();
    H.Hints.reset();
    H.FX.parts = [];
    G.dialT = 0;
    // Maren comes ashore on the outer rocks; the tide holds at Low until she reaches the reef.
    enterRoom(H.ROOM.breakers, true);
    placeAtStart();
    G.titleT = 6;
    H.UI.cinematic(H.LORE.opening.concat(H.LORE.openingAfter), () => setTimeout(() => H.Narr.think('boat'), 1200));
  }

  function toTitle() {
    G.mode = 'title';
    H.UI.title();
  }

  // ------------------------------------------------------------------ rooms
  function specKey(room, spec) { return spec.id || `${spec.type}@${spec.tx},${spec.ty}`; }

  function roomSpecs(room) {
    const out = [];
    room.map.forEach((row, ty) => {
      for (let tx = 0; tx < row.length; tx++) {
        const legend = room.ents && room.ents[row[tx]];
        if (legend) out.push(Object.assign({}, legend, { tx, ty }));
      }
    });
    (room.ents2 || []).forEach((e) => out.push(Object.assign({}, e)));
    const taken = new Set(out.map((s) => s.tx + ',' + s.ty));
    out.push(...H.Narr.carvingSpecs(room, taken));
    for (const s of out) {
      s.x = room.px + s.tx * TS + 8;
      s.y = room.py + (s.ty + 1) * TS;
      s.key = specKey(room, s);
    }
    return out;
  }

  function buildEntity(spec, room) {
    const f = W().flags;
    const t = spec.type;
    if (t === 'gullet') {
      let ty = spec.ty;
      while (ty > 0 && W().solid(room.ox + spec.tx, room.oy + ty) !== 1) ty--;
      spec.anchorY = room.py + (ty + 1) * TS;
      spec.x = room.px + spec.tx * TS + 8;
      return new H.BodyTypes.gullet(spec, room);
    }
    if (t === 'abbot') return f.abbotDead ? null : new H.BodyTypes.abbot(spec, room);
    if (t === 'shoal') return G.act >= 3 && !f.shoalDead ? new H.BodyTypes.shoal(spec, room) : null;
    if (t === 'grafted') return G.act >= 3 ? new H.BodyTypes.grafted(spec, room) : null;
    if (t === 'choirmember') {
      if (G.quests.hesperDead && W().flags.choirDrained) return null;
      const s = Object.assign({}, spec, { passive: H.Story.choirFriendly() });
      return new H.BodyTypes.grafted(s, room);
    }
    if (H.BodyTypes[t]) return new H.BodyTypes[t](spec, room);
    const C = H.ObjTypes[t];
    if (C) return new C(spec, room);
    return null;
  }

  function saveRoomState() {
    if (!G.room) return;
    const st = W().state(G.room.id);
    for (const e of G.ents) {
      if (!e.key || !e.serialize) continue;
      const s = e.serialize();
      if (s) st.ents[e.key] = s;
    }
  }

  function enterRoom(room, initial) {
    if (!initial) saveRoomState();
    const prevRegion = G.room && G.room.region;
    G.room = room;
    G.ents = [];
    G.projectiles = []; G.enemyShots = []; G.sawlines = []; G.tracers = [];
    H.Fire.clear();
    G.bossBar = null;
    const st = W().state(room.id);
    const firstVisit = !st.visited;
    st.visited = true;
    for (const spec of roomSpecs(room)) {
      const e = buildEntity(spec, room);
      if (!e) continue;
      const saved = st.ents[e.key];
      if (saved) {
        if (saved.reknotted && e.isBody) { e.restore(Object.assign({}, saved, { knots: null, mode: 'hunt' })); e.standUp(); delete saved.reknotted; }
        else e.restore(saved);
      } else if (e.isBody && G.act >= 2) applyEncrustation(e);
      if (e.collect !== undefined || ['stitch', 'seal', 'page', 'table', 'ability', 'weapon'].includes(e.type)) {
        if (saved && saved.done) continue;
      }
      G.ents.push(e);
    }
    // Loose items dropped or carried here.
    for (const l of st.loose || []) {
      const e = new H.ObjTypes.pickup({ type: 'pickup', item: l.item, n: l.n, x: l.x, y: l.y }, room);
      e.looseRef = l; G.ents.push(e);
    }
    if (G.satchelDrop && G.satchelDrop.room === room.id) {
      G.ents.push(new H.ObjTypes.satchel({ type: 'satchel', x: G.satchelDrop.x, y: G.satchelDrop.y, bag: G.satchelDrop.bag }, room));
    }
    roomEvents(room, firstVisit);
    G.sinceLoad = 0;
    if (firstVisit || prevRegion !== room.region) G.titleT = 2.5;
    if (H.Narr.enterRegion(room.region)) G.titleT = 6;
    H.Narr.onRoomEnter();
    markSeen();
  }

  function markSeen() {
    const st = W().state(G.room.id);
    st.seen[H.Tide.phase()] = true;
  }

  function roomEvents(room, firstVisit) {
    const f = W().flags, q = G.quests;
    if (room.id === 'pier' && !f.pierIntro) {
      f.pierIntro = true;
      if (H.Tide.phase() === 'low' && H.Tide.phaseRemaining() > 14) H.Tide.setPhase('low', H.Tide.len(0) - 12);
    }
    if (room.id === 'kilns' && f.basinFilling) f.basinCrossed = true;
    const enterThought = { pier: 'pier', chapel: 'chapel', choir: 'choir', skullstair: 'skull', lamp: 'agathe' }[room.id];
    if (enterThought) H.Narr.think(enterThought);
    // Reaching the reef: the tide starts to move, and the dial shows it.
    if (room.id !== 'breakers' && !f.landed) {
      f.landed = true;
      if (room.id === 'reef') H.Narr.think('start');
      // From here on a fall wakes Maren on the Constance's deck, not back out on the rocks.
      if (G.respawn && G.respawn.room === 'breakers') G.respawn = reefDeck();
    }
    H.Tide.paused = room.id === 'breakers' && !f.landed;
    // Act III: the Brine breaches one Bell Room.
    if (G.act >= 3 && room.id === breachRoom() && !f.breachDone) {
      if (room.id === 'saltbell' && q.teodor === 'captured') {
        const t = new H.BodyTypes.shucker({ x: room.px + 30 * TS, y: room.py + 19 * TS, key: 'teodor_reknot', pose: 'standing', teodor: true }, room);
        t.mode = 'hunt'; t.breach = true;
        G.ents.push(t); G.bossBar = t;
        G.breachT = 60;
        H.Audio.toll();
        H.toast('The spring tide breaches the Saltworks Bell Room. Teodor has re-knotted, and he is going for Odile! (60 s)', 5);
      } else if (room.id === 'cloisterbell') {
        for (let i = 0; i < 2; i++) {
          const s = new H.BodyTypes.shucker({ x: room.px + (14 + i * 10) * TS, y: room.py + 17 * TS, key: 'breach' + i, pose: 'standing' }, room);
          applyEncrustation(s); G.ents.push(s);
        }
        f.breachDone = true;
        H.toast('The spring tide has breached this Bell Room. The bell is drowned out. It is not safe here.', 4);
      }
    }
    if (room.id === 'choir' && G.act >= 3 && f.choirDrained && !q.hesperDead) {
      const h = new H.BodyTypes.grafted({ x: room.px + 31 * TS, y: room.py + 17 * TS, key: 'hesper_boss', boss: true }, room);
      h.hesper = true; G.ents.push(h); G.bossBar = h;
      H.toast('Hesper Vane waits in the dry shrine, lamp raised. "You took the Saint\'s water."', 4);
    }
    if (room.id === 'lamp' && H.Story.choirFriendly() && !q.hesperDead && G.act >= 3) {
      G.ents.push(new H.ObjTypes.npc({ type: 'npc', id: 'hesper', x: room.px + 11 * TS, y: room.py + 17 * TS, key: 'hesper_lamp' }, room));
    }
    if (room.id === 'lair' && !f.undertowDead) {
      const b = new H.BodyTypes.undertow({ x: room.px + 60 * TS, y: room.py + 11 * TS, key: 'undertow_boss', boss: true, dir: -1 }, room);
      G.ents.push(b); G.bossBar = b;
      H.Audio.buoy(0.9, 0.6);
      H.toast('The buoy bell tolls in the dark water. The Undertow is home.', 4);
    }
    // The Almoner is fought only in the Godot build; here the Refectory's chains fall away when she comes in.
    if (room.id === 'spine' && !f.motherDead) {
      f.motherDead = true;
      H.toast('Cut tendrils hang from the vertebrae. Whatever hung here is gone.', 4);
    }
    if (room.id === 'organloft' && !f.precentorDead) {
      f.precentorDead = true;
      H.toast('The organ is silent. Whatever screamed in it has stopped.', 4);
    }
    if (room.id === 'refectory' && !f.almonerDead) {
      f.almonerDead = true;
      H.toast('The chains on the Refectory doors hang loose. Whatever they held is gone.', 4);
    }
    if (room.id === 'abbot' && f.abbotDead && !G.player.has('boots')) {
      G.ents.push(new H.ObjTypes.ability({ type: 'ability', id: 'boots', x: room.px + 20 * TS, y: room.py + 18 * TS, key: 'boots_drop' }, room));
    }
  }

  function breachRoom() { return G.quests.teodor === 'captured' && !W().flags.breachDone ? 'saltbell' : 'cloisterbell'; }
  function bellRoomBreached(id) { return G.act >= 3 && id === 'cloisterbell' && W().flags.breachDone && breachRoom() === 'cloisterbell' || (id === 'saltbell' && G.breachT > 0); }

  // ------------------------------------------------------------------ tide consequences
  function onPhase(ph, prev) {
    if (G.mode !== 'play') return;
    H.Audio.tideBell();
    H.toast('The tide turns: ' + H.Tide.phaseName() + '.', 2.5);
    if (G.room) markSeen();
    if (G.undertow) G.undertow.onPhase(ph);
    const f = W().flags;
    if (ph === 'rising') {
      // Off-screen: every downed body below the high-water line will be reached by the water.
      for (const r of H.ROOMS) {
        if (r === G.room) continue;
        const st = W().states[r.id];
        if (!st) continue;
        for (const s of Object.values(st.ents)) {
          if (!s || s.mode !== 'downed' || s.gone || !s.knots || !s.knots.length) continue;
          if (inTideWater(r, s)) s.reknot = true;
        }
      }
      if (H.Tide.tidal(G.room)) H.Narr.think('rising');
      H.Hints.tideTurned();
    }
    if (ph === 'high') {
      if (H.Tide.tidal(G.room) && !G.room.fixedWater) H.Narr.think('firstHigh');
      for (const r of H.ROOMS) {
        const st = W().states[r.id];
        if (!st || r === G.room) continue;
        for (const s of Object.values(st.ents)) if (s && s.reknot && s.mode === 'downed' && !s.gone) { s.reknot = false; s.reknotted = true; s.mode = 'hunt'; }
      }
      let n = 0;
      for (const e of G.ents) if (e.isBody && e.reknot && e.mode === 'downed' && !e.gone) { e.standUp(); n++; }
      if (n) H.toast(n === 1 ? 'A body you left in the water grows new knots and stands.' : n + ' bodies you left in the water grow new knots and stand.', 3.5);
    }
    if (ph === 'ebbing') {
      // Currents drag loose items seaward to each region's Wrack Pool. Bolts that sink are lost.
      for (const r of H.ROOMS) {
        const st = W().states[r.id];
        if (!st || !st.loose) continue;
        const keep = [];
        for (const l of st.loose) {
          const inWater = W().roomSurface(r, l.x) < l.y - 2 && H.Tide.tidal(r);
          if (!inWater) { keep.push(l); continue; }
          if (l.item === 'bolts') continue;
          const wp = G.wrackPools[r.region];
          if (wp && wp.room !== r.id) { const ws = W().state(wp.room); (ws.loose = ws.loose || []).push(Object.assign(l, { x: wp.x + H.rand(-8, 8), y: wp.y })); }
          else if (wp) keep.push(Object.assign(l, { x: wp.x + H.rand(-8, 8), y: wp.y }));
        }
        st.loose = keep;
      }
      if (G.room) refreshLoose();
      const sd = G.satchelDrop;
      if (sd && sd.tidal) {
        const wp = G.wrackPools[H.ROOM[sd.room].region];
        if (wp) {
          Object.assign(sd, { room: wp.room, x: wp.x, y: wp.y, tidal: false });
          H.toast('The ebb carries your satchel to the Wrack Pool. It is marked on your map.', 4);
          if (G.room) { G.ents = G.ents.filter((e) => e.type !== 'satchel'); if (sd.room === G.room.id) G.ents.push(new H.ObjTypes.satchel({ type: 'satchel', x: sd.x, y: sd.y, bag: sd.bag }, G.room)); }
        }
      }
      // Ebb resets the barge puzzle if nobody crossed.
      if (!f.basinCrossed) { f.basinSeq = []; f.basinJam = false; f.basinFilling = false; f.basinTarget = 19; }
    }
  }
  function inTideWater(r, s) {
    const bx = s.x + 6, by = s.y + 4;
    if (r.fixedWater !== undefined || r.id === 'choir' || r.pools) return W().roomSurface(r, bx) < by;
    if (!H.Tide.tidal(r)) return false;
    return by > (H.Tide.SEA_HIGH - H.Tide.spring) * TS;
  }
  function refreshLoose() {
    G.ents = G.ents.filter((e) => !e.looseRef);
    for (const l of W().state(G.room.id).loose || []) {
      const e = new H.ObjTypes.pickup({ type: 'pickup', item: l.item, n: l.n, x: l.x, y: l.y }, G.room);
      e.looseRef = l; G.ents.push(e);
    }
  }

  // ------------------------------------------------------------------ helpers used by entities
  function spawnLoose(item, x, y, n = 1) {
    const st = W().state(G.room.id);
    st.loose = st.loose || [];
    const l = { item, n, x, y };
    st.loose.push(l);
    const e = new H.ObjTypes.pickup({ type: 'pickup', item, n, x, y }, G.room);
    e.looseRef = l; G.ents.push(e);
  }
  function spawnPickup(item, x, y) { spawnLoose(item, x, y, 1); }
  function removeLoose(e) {
    if (!e.looseRef) return;
    const st = W().state(G.room.id);
    st.loose = (st.loose || []).filter((l) => l !== e.looseRef);
  }
  function addSwell(n) {
    G.swell += n;
    // Pacing valve: after a peak, 60-90 s with no new wake-ups.
    if (G.swell > 9 && G.valveT <= 0) { G.valveT = H.rand(60, 90); G.swell = 0; }
  }
  function canWake() { return G.valveT <= 0; }
  function shake(n) {
    G.shakeAmt = Math.max(G.shakeAmt, n);
    // the pad feels every jolt the camera does
    H.Input.rumble(Math.min(1, 0.12 * n + 0.08), n >= 2 ? Math.min(1, 0.11 * n) : 0, 0.08 + 0.05 * n);
  }
  // Where the reef's start marker stands: the Constance's deck.
  function reefDeck() {
    const r = H.ROOM.reef, p = G.player;
    for (let ty = 0; ty < r.map.length; ty++) for (let tx = 0; tx < r.map[ty].length; tx++) {
      const e = r.ents && r.ents[r.map[ty][tx]];
      if (e && e.type === 'start') return { room: r.id, x: r.px + tx * TS + 8 - p.w / 2, y: r.py + (ty + 1) * TS - p.h };
    }
    return { room: r.id, x: r.px + 25 * TS, y: r.py + 12 * TS };
  }
  function ringNear(p, rad) {
    const tx = Math.floor(p.cx() / TS), ty = Math.floor(p.cy() / TS);
    for (let dy = -rad; dy <= 2; dy++) for (let dx = -rad; dx <= rad; dx++) if (W().charAt(tx + dx, ty + dy) === 'o') return true;
    return false;
  }
  function tip(id, text, t = 4) { if (G.tips[id]) return; G.tips[id] = true; H.toast(text, t); }
  function quest(k) { return G.quests[k]; }
  function setQuest(k, v) { G.quests[k] = v; }
  function checkpointQuest() {}
  function bellIdFor(roomId) { const r = H.ROOM[roomId]; for (const row of r.map) for (const ch of row) { const e = r.ents && r.ents[ch]; if (e && e.type === 'bell') return e.id; } return null; }

  function applyEncrustation(b) {
    const type = G.encrustedType;
    if (!type || !b.knots || b.boss || b.type === 'driftling') return;
    const open = b.knots.filter((k) => !k.plated);
    const targets = b.knots.filter((k) => k.type === type && !k.plated);
    for (const k of targets) {
      if (b.knots.filter((q) => !q.plated).length <= 2) break;
      k.plated = true; k.braced = true;
    }
  }

  function startAct(n) {
    if (G.act >= n) return;
    // Adaptive encrustation: the Brine armours what the player broke most.
    const counts = G.stats.byAct[G.act] || {};
    let best = null, bn = 0;
    for (const [t, c] of Object.entries(counts)) if (t !== 'crown' && c > bn) { bn = c; best = t; }
    G.encrustedType = best;
    G.act = n;
    if (n === 2) H.Narr.think('actTwo');
    if (n === 3) H.Narr.think('actThree');
    if (n === 2) H.toast('Act II. The Undertow begins to hunt at High tide.' + (best ? ` New Brine bodies grow salt plates over the ${best}.` : ''), 6);
    if (n === 3) {
      H.Tide.spring = 2;
      H.toast('Act III. Spring tides raise every water line by one level. The Brine is breaching the Bell Rooms.', 6);
    }
  }

  function onBodyDowned(b, src) {
    if (!b.boss) H.Narr.think('firstDown');
    addSwell(0.5);
    if (b.breach) { G.breachT = 0; W().flags.breachDone = true; G.quests.teodor = 'dead'; G.bossBar = null; H.toast('Teodor falls for the last time. Odile is safe.', 4); }
    if (b.hesper) { G.quests.hesperDead = true; G.bossBar = null; H.toast('Hesper Vane falls. The Choir\'s hymn goes unfinished.', 4); }
    if (b.type === 'grafted' && !b.hesper && !b.boss) { /* choir member */ }
  }
  function onBodyBurned(b) {}
  function onChoirAttacked() {
    if (W().flags.choirHostile) return;
    W().flags.choirHostile = true;
    for (const e of G.ents) if (e.type === 'grafted' && e.passive) { e.passive = false; e.mode = 'hunt'; }
    H.toast('The Choir turns on you.', 3);
  }
  function onCrankTaken() {
    const f = W().flags;
    if (f.titheTrapped) return;
    f.titheTrapped = true;
    // The trap: the hall's bell tolls, every active body wakes, and water pours up through the grates.
    H.Audio.toll(); shake(5);
    for (const e of G.ents) if (e.isBody && !e.corpse && e.mode === 'dormant') { e.lastHeard = { x: G.player.cx(), y: G.player.cy() }; e.wake(true); }
    for (const e of G.ents) if (e.type === 'gullet' && e.mode === 'hang') e.hear(G.player.cx(), G.player.cy(), 0, 999, 'scream');
    G.titheFlood = 0;
    H.toast('The crank\'s counterweight drops. The hall bell tolls overhead, and water pours up through the floor grates!', 5);
    H.Narr.think('titheTrap');
  }
  function onTagsTaken() {}
  function onCacheOpened() {
    const p = G.player;
    // On the drained path the cache replaces Hesper's satchel as the second expansion.
    if (!G.quests.hesperSatchel && !G.quests.cacheSatchel) { G.quests.cacheSatchel = true; p.bag.size = Math.max(p.bag.size + 4, 12); }
    H.toast("Odile's salvage cache, with a big salvage satchel. Satchel: " + p.bag.size + ' slots.', 4);
  }
  function onAbbotKilled(a) {
    W().flags.abbotDead = true;
    G.bossBar = null;
    const spec = { type: 'ability', id: 'boots', x: a.cx(), y: a.feet(), key: 'boots_drop' };
    G.ents.push(new H.ObjTypes.ability(spec, G.room));
    G.player.bag.add('ivory', 2);
    H.toast('The Abbot of Salt crumbles. 2 Mere-ivory. Something heavy clatters to the floor: his Ballast Boots.', 5);
    startAct(2);
  }
  function onUndertowKilled() {
    W().flags.undertowDead = true; W().flags.skullOpen = true;
    G.undertow.dead = true; G.bossBar = null;
    G.player.bag.add('ivory', 2);
    H.toast('The Undertow dies in its lair. Far above, the skull door of the Ribvault Bell Room grinds open.', 6);
  }
  function onShoalKilled() { W().flags.shoalDead = true; G.bossBar = null; H.toast('The Shoal comes apart. The lamp room is ahead.', 4); }
  function collect(spec) { H.Story.collect(spec); W().state(G.room.id).ents[spec.key] = { done: true }; }
  function setRespawn(bell) { G.respawn = { room: bell.room.id, x: bell.cx() - 20, y: bell.y + bell.h - G.player.h }; }

  function setChoirLevel(name) {
    const f = W().flags;
    const lvl = { mid: 17, raised: 3, drained: 21 }[name];
    f.choirLevelName = name;
    f.choirTarget = lvl;
    if (name === 'drained') {
      f.choirDrained = true;
      G.quests.choirWater = 'drained';
      onChoirAttacked();
      H.Audio.rumble(); shake(5);
      H.toast('The shrine drains with a roar. Steps emerge from the silt. The Choir screams.', 5);
      H.Render.markDirty();
    } else {
      H.Audio.rumble();
      H.toast(name === 'raised' ? 'The water climbs toward the vault above the shrine.' : 'The water settles back to the shrine line.', 3);
    }
  }

  // ------------------------------------------------------------------ interaction
  function interact() {
    const p = G.player;
    if (p.dragging) { p.stopDrag(); return; }
    let best = null, bd = 1e9;
    for (const e of G.ents) {
      if (e.isBody || !e.prompt || !e.near) continue;
      if (!e.near(p)) continue;
      const pr = e.prompt();
      if (!pr || pr.startsWith('{gaff}') || pr.startsWith('Lift pan')) continue;
      const d = Math.abs(e.cx() - p.cx());
      if (d < bd) { bd = d; best = e; }
    }
    if (best) { best.interact(); return; }
    const b = p.nearestDownedBody(24);
    if (b) {
      if (b.teodor && b.mode === 'captured') {
        G.quests.teodor = 'bound';
        b.gone = true;
        W().state(G.room.id).ents[b.key] = b.serialize();
        H.Audio.hook();
        H.toast('You bind Teodor with cord. Odile\'s people will carry him up to the Saltworks Bell Room. Tell her.', 5);
        return;
      }
      p.startDrag(b);
    }
  }
  function computePrompt() {
    const p = G.player;
    if (p.dragging) return '{interact} Let go   (dragging is slow; haul it above the tide line)';
    let best = null, bd = 1e9;
    for (const e of G.ents) {
      if (e.isBody || !e.prompt || !e.near || !e.near(p)) continue;
      const pr = e.prompt(); if (!pr) continue;
      const d = Math.abs(e.cx() - p.cx());
      if (d < bd) { bd = d; best = pr; }
    }
    if (best) return best;
    const b = p.nearestDownedBody(24);
    if (b) {
      if (b.teodor && b.mode === 'captured') return '{interact} Bind Teodor for Odile';
      return '{interact} Drag body   {burn} Burn it (oil)';
    }
    return null;
  }

  // ------------------------------------------------------------------ death and rest
  function onPlayerDeath() {
    const p = G.player;
    if (p.dead) return;
    H.Input.rumble(0.6, 1, 0.7);
    p.dead = true; p.grabbedBy = null; if (p.dragging) p.stopDrag();
    G.stats.deaths++;
    // The satchel stays where she fell; weapons and fittings stay with her.
    const bag = new H.Satchel.Bag(99);
    for (const [i, n] of p.bag.list()) bag.items[i] = n;
    if (G.satchelDrop) for (const [i, n] of G.satchelDrop.bag.list()) bag.items[i] = (bag.items[i] || 0) + n;
    p.bag.clear();
    const tidal = H.Tide.tidal(G.room) && p.inWater();
    let x = p.cx(), y = p.y + p.h;
    G.satchelDrop = { room: G.room.id, x, y, bag, tidal };
    H.Audio.toll();
    const dt = H.Narr.deathText();
    dt.detail = tidal ? 'You died in tidal water. Your satchel lies where you fell; the next Ebb will carry it to this region\'s Wrack Pool, marked on your map.' : 'Your satchel lies where you fell. Nothing is ever lost for good, but getting it back is its own small expedition.';
    setTimeout(() => H.UI.death(dt, respawn), 900);
  }
  function respawn() {
    const p = G.player, r = G.respawn;
    p.dead = false; p.hp = p.maxHp; p.breath = isFinite(p.breathMax()) ? p.breathMax() : 30; p.state = 'normal'; p.vx = p.vy = 0; p.invulnT = 2;
    p.grabbedBy = null; p.healT = 0; p.reloadT = 0; p.craftT = 0;
    if (p.oil < 30) p.oil = 30;
    enterRoom(H.ROOM[r.room]);
    p.x = r.x; p.y = r.y;
    snapCamera();
    G.breachT = 0;
    if (G.stats.deaths === 1) setTimeout(() => H.Narr.think('firstDeath'), 600);
  }
  function recoverSatchel(obj) {
    const p = G.player, sd = G.satchelDrop;
    if (!sd) return;
    for (const [i, n] of sd.bag.list()) {
      const left = p.bag.add(i, n);
      if (left) G.reliquary.add(i, left);
    }
    G.satchelDrop = null;
    G.ents = G.ents.filter((e) => e !== obj);
    H.Audio.pickup();
    H.toast('You recover your satchel. Anything that did not fit went to the Reliquary.', 4);
  }
  function rest(bell) {
    const p = G.player;
    const heal = p.grafts.has('lung') ? Math.ceil((p.maxHp - p.hp) / 2) : p.maxHp - p.hp;
    if (p.grafts.has('lung') && heal < p.maxHp - p.hp) { p.hurt; H.toast('The bell\'s resonance grinds at the salt in your chest. Healing is halved.', 3); }
    p.hp = Math.min(p.maxHp, p.hp + heal);
    setRespawn(bell);
    saveRoomState();
    H.Tide.advanceTo('low');
    G.titheFlood = undefined; delete W().state('tithe').flood;
    save();
    H.Audio.roomBell();
    G.flash = 0;
    H.UI.cinematic([H.Narr.nextDream()], () => H.toast('You wake by the bell. The tide has turned all the way round to Low.', 3));
  }

  // ------------------------------------------------------------------ save / load
  function hasSave() { try { return !!localStorage.getItem(SAVE_KEY); } catch (e) { return false; } }
  function save() {
    saveRoomState();
    const data = {
      v: 1, player: G.player.toJSON(), flags: W().flags, states: W().states,
      tide: { t: H.Tide.t, cycle: H.Tide.cycle, scale: H.Tide.scale, spring: H.Tide.spring },
      act: G.act, quests: G.quests, stats: G.stats, pages: [...G.pages], tables: [...G.tables], collected: G.collected,
      bellsLit: G.bellsLit, reliquary: G.reliquary.toJSON(), respawn: G.respawn,
      satchelDrop: G.satchelDrop ? Object.assign({}, G.satchelDrop, { bag: G.satchelDrop.bag.toJSON() }) : null,
      difficulty: G.difficulty, encrustedType: G.encrustedType, freeGraft: G.freeGraft, tips: G.tips, undertowDead: G.undertow.dead, narr: G.narr,
    };
    try { localStorage.setItem(SAVE_KEY, JSON.stringify(data)); } catch (e) { H.toast('Could not save (storage unavailable).'); }
  }
  function loadGame() {
    let d;
    try { d = JSON.parse(localStorage.getItem(SAVE_KEY)); } catch (e) { d = null; }
    if (!d) { H.UI.title(); return; }
    W().reset();
    W().flags = d.flags; W().states = d.states;
    Object.assign(H.Tide, d.tide);
    G.player = new H.Player(); G.player.load(d.player);
    Object.assign(G, {
      act: d.act, quests: d.quests, stats: d.stats, pages: new Set(d.pages), tables: new Set(d.tables), collected: d.collected,
      bellsLit: d.bellsLit, reliquary: H.Satchel.Bag.from(d.reliquary), respawn: d.respawn,
      satchelDrop: d.satchelDrop ? Object.assign({}, d.satchelDrop, { bag: H.Satchel.Bag.from(d.satchelDrop.bag) }) : null,
      difficulty: d.difficulty, encrustedType: d.encrustedType, freeGraft: d.freeGraft, tips: d.tips || {}, narr: Object.assign(H.Narr.fresh(), d.narr || {}),
      undertow: new H.UndertowSystem(), mode: 'play', ended: null, swell: 0, valveT: 0,
    });
    G.undertow.dead = d.undertowDead;
    G.dialT = 1;
    H.Narr.reset();
    H.Hints.reset();
    G.player.hp = G.player.maxHp;
    enterRoom(H.ROOM[G.respawn.room], true);
    G.player.x = G.respawn.x; G.player.y = G.respawn.y;
    snapCamera();
    G.titleT = 3;
  }

  function statsSummary() {
    const k = Object.values(G.stats.knots).reduce((a, b) => a + b, 0);
    return `Knots broken: ${k} · Deaths: ${G.stats.deaths} · Time: ${H.fmtTime(G.stats.time)} · Agathe's pages: ${G.pages.size}/9 · Grafts: ${G.player.grafts.size} · Difficulty: ${G.difficulty}`;
  }

  // ------------------------------------------------------------------ camera
  function snapCamera() { updateCamera(1, true); }
  function updateCamera(dt, snap) {
    const p = G.player, r = G.room;
    const look = p.facing * 30;
    let tx = p.cx() + look - H.VIEW_W / 2, ty = p.cy() - H.VIEW_H / 2 - 10;
    if (r.pw <= H.VIEW_W) tx = r.px + (r.pw - H.VIEW_W) / 2; else tx = H.clamp(tx, r.px, r.px + r.pw - H.VIEW_W);
    if (r.ph <= H.VIEW_H) ty = r.py + (r.ph - H.VIEW_H) / 2; else ty = H.clamp(ty, r.py, r.py + r.ph - H.VIEW_H);
    if (snap) { G.cam.x = tx; G.cam.y = ty; return; }
    G.cam.x = H.lerp(G.cam.x, tx, Math.min(1, dt * 6));
    G.cam.y = H.lerp(G.cam.y, ty, Math.min(1, dt * 6));
  }

  // ------------------------------------------------------------------ main step
  function step(dt) {
    const In = H.Input;
    if (G.mode !== 'play' || H.UI.isOpen()) {
      if (G.mode === 'title' && !H.UI.isOpen()) H.UI.title();
      // keep the backdrop alive on the title screen
      if (G.mode === 'title') { H.Tide.update(0); G.frameLights = []; }
      H.Render.draw();
      return;
    }
    if (In.hit('pause')) { H.UI.pause(); return; }
    if (In.hit('map')) { H.UI.map(); return; }
    if (In.hit('satchel')) { H.UI.satchel('satchel'); return; }
    if (In.hit('journal')) { H.UI.satchel(null); return; }
    if (In.hit('debug')) {
      if (In.held('sneak')) { const p = G.player; ['swing', 'boots', 'jet', 'tidekey', 'seal', 'lens'].forEach((a) => p.abilities.add(a)); ['harpoon', 'sling', 'sawline'].forEach((w) => { if (!p.weapons[w]) { p.weapons[w] = { tier: 0, mag: 1 }; p.weaponOrder.push(w); } }); p.bag.size = 16; p.bag.add('rivets', 36); p.bag.add('oil', 3); H.toast('DEBUG: all tools.'); }
      else { G.god = !G.god; H.toast('DEBUG: invulnerable ' + (G.god ? 'on' : 'off')); }
    }
    G.stats.time += dt;
    G.frameDt = dt;
    G.sinceLoad += dt;
    H.Tide.update(dt);
    const p = G.player;

    // Choir water and basin animate toward their targets.
    const f = W().flags;
    const cst = W().state('choir');
    if (f.choirTarget !== undefined) { cst.choirLevel = H.approach(cst.choirLevel !== undefined ? cst.choirLevel : 17, f.choirTarget, dt * 1.2); }
    const sst = W().state('saltgate');
    sst.pool = sst.pool || {};
    if (f.basinTarget !== undefined) sst.pool.basin = H.approach(sst.pool.basin !== undefined ? sst.pool.basin : 19, f.basinTarget, dt * (f.basinFilling ? 1.1 : 2));
    // Tithe Hall flood set-piece.
    if (G.titheFlood !== undefined) {
      G.titheFlood += dt;
      const t = G.titheFlood, st = W().state('tithe');
      if (t < 25) st.flood = H.lerp(17, 9.5, t / 25);
      else if (t < 80) st.flood = 9.5;
      else if (t < 110) st.flood = H.lerp(9.5, 18, (t - 80) / 30);
      else { delete st.flood; G.titheFlood = undefined; }
    }
    if (G.breachT > 0) {
      G.breachT -= dt;
      if (G.breachT <= 0 && !W().flags.breachDone) {
        W().flags.breachDone = true; G.quests.odileLost = true; G.quests.teodor = 'dead';
        for (const e of G.ents) if (e.key === 'teodor_reknot') e.gone = true;
        G.bossBar = null;
        H.toast('Too late. Odile Marrow is dead, and her bench with her.', 5);
      } else if (G.ents.some((e) => e.key === 'teodor_reknot' && e.mode === 'captured')) {
        W().flags.breachDone = true; G.breachT = 0; G.quests.teodor = 'saved'; G.bossBar = null;
        for (const e of G.ents) if (e.key === 'teodor_reknot') e.gone = true;
        H.toast('You break every limb knot again and leave his Crown whole. Teodor sags. This time, the Brine lets go. Odile weeps.', 6);
      }
    }

    // Dynamic platforms
    const plats = [];
    for (const e of G.ents) if (e.platformRect) { const pl = e.platformRect(); if (pl) plats.push(pl); }
    H.Phys.setPlatforms(plats);

    p.update(dt);
    // Room transitions: walk off any open edge into the neighbouring room.
    const r = G.room;
    if (!p.dead && (p.cx() < r.px || p.cx() >= r.px + r.pw || p.cy() < r.py || p.cy() >= r.py + r.ph)) {
      const nr = W().roomAtPx(p.cx(), p.cy());
      if (nr && nr !== r) {
        if (p.dragging) p.stopDrag();
        enterRoom(nr);
      } else {
        p.x = H.clamp(p.x, r.px, r.px + r.pw - p.w); p.y = H.clamp(p.y, r.py, r.py + r.ph - p.h);
      }
    }
    for (const e of G.ents) e.update && e.update(dt);
    // Re-knot marking: a downed body touched by water during the Rising tide.
    if (H.Tide.phase() === 'rising') {
      for (const e of G.ents) if (e.isBody && e.mode === 'downed' && !e.gone && !e.dragged && e.knots && e.knots.length && W().isWater(e.cx(), e.feet() - 3)) e.reknot = true;
    }
    G.ents = G.ents.filter((e) => !(e.isBody && e.gone && (e.type === 'driftling' || e.type === 'undertow' || e.shed)));
    for (const pr of G.projectiles) pr.update(dt);
    G.projectiles = G.projectiles.filter((q) => !q.dead);
    for (const s of G.sawlines) s.update(dt);
    G.sawlines = G.sawlines.filter((s) => !s.dead);
    for (const s of G.enemyShots) {
      s.x += s.vx * dt; s.y += s.vy * dt; s.life -= dt;
      if (W().solidPx(s.x, s.y)) s.life = 0;
      if (H.rectsOverlap({ x: s.x - 2, y: s.y - 2, w: 4, h: 4 }, p.box())) { p.hurt(s.dmg, s.src); s.life = 0; }
    }
    G.enemyShots = G.enemyShots.filter((s) => s.life > 0);
    H.Fire.update(dt);
    H.FX.update(dt);
    H.Narr.updateWhispers(dt);
    G.undertow.update(dt);

    // Tutorial: the sailor's knots glint under the focused beam, then go dark.
    if (G.room.id === 'reef' && p.focusing) {
      const s = G.ents.find((e) => e.sailor);
      if (s && s.knots.length && s.fadeKnots === 0 && p.beamHits(s.cx(), s.cy())) { s.fadeKnots = 3; setTimeout(() => H.Narr.think('sailorKnots'), 1500); }
    }
    if (G.room.id === 'pier') {
      const s = G.ents.find((e) => e.key === 'first_shucker');
      if (s && s.mode !== 'dormant') H.Narr.think('firstStand');
    }
    // A few things Maren notices for herself.
    if (!H.Narr.had('lowOil') && p.lanternLit() && p.oil < p.oilMax() * 0.15) H.Narr.think('lowOil');
    if (!H.Narr.had('rings') && p.has('swing') && ringNear(p, 6)) H.Narr.think('rings');
    if (!H.Narr.had('bellmouth') && G.ents.some((b) => b.type === 'bellmouth' && !b.gone && !b.dead && b.mode !== 'downed' && H.dist(b.cx(), b.cy(), p.cx(), p.cy()) < 220)) H.Narr.think('bellmouth');
    H.Hints.update(dt);
    // Undertow glimpse at the Tithe window.
    if (G.room.id === 'tithe' && f.titheTrapped && !f.glimpse && p.y < G.room.py + 8 * TS) {
      f.glimpse = true; G.glimpseT = 4;
      H.Audio.buoy(0.9, 0.7); shake(6);
      if (G.titheFlood !== undefined) G.titheFlood = Math.max(G.titheFlood, 25);
      const st = W().state('tithe'); if (st.flood !== undefined) st.flood -= 1;
      setTimeout(() => H.Narr.think('glimpse'), 4500);
      H.toast('At the window, the stained glass darkens. Something vast slides past outside with the slow clang of a buoy bell. It does not fight yet.', 7);
    }
    if (G.glimpseT > 0) G.glimpseT -= dt;
    // Wind gusts are telegraphed by streaks of salt dust.
    if (G.room.windZones) {
      const w = p.windAt();
      if (w && Math.random() < 48 * dt) H.FX.spawn(G.cam.x + (w > 0 ? 0 : H.VIEW_W), G.cam.y + Math.random() * H.VIEW_H, 1, { ang: w > 0 ? 0 : Math.PI, spread: 0.05, smin: Math.abs(w) * 2, smax: Math.abs(w) * 3, color: 'rgba(230,225,210,0.6)', grav: 0, lmin: 1.5, lmax: 2.5, size: 1.2 });
      if (w && !G.gusting) { G.gusting = true; H.Audio.burst(); }
      if (!w) G.gusting = false;
    }

    // Timers and feedback
    G.valveT -= dt;
    G.swell = Math.max(0, G.swell - dt * 0.12);
    G.shakeAmt = Math.max(0, G.shakeAmt - dt * 20);
    G.flash = Math.max(0, G.flash - dt);
    G.pressureWarn = Math.max(0, G.pressureWarn - dt * 2);
    G.titleT -= dt;
    for (const t of H.toasts) t.t -= dt;
    H.toasts = H.toasts.filter((t) => t.t > 0);
    for (const n of G.noiseRings) n.t -= dt;
    G.noiseRings = G.noiseRings.filter((n) => n.t > 0);
    for (const t of G.tracers) t.t -= dt;
    G.tracers = G.tracers.filter((t) => t.t > 0);
    if (Math.floor(G.stats.time) % 2 === 0) markSeen();

    // Lights for this frame
    const lights = [{ player: true }];
    for (const e of G.ents) if (e.light) { const l = e.light(); if (l) lights.push(l); }
    for (const pr of G.projectiles) { const l = pr.light(); if (l) lights.push(l); }
    G.frameLights = lights;

    G.promptText = computePrompt();
    // The tide holds at Low on the outer rocks until Maren reaches the reef; then the dial appears.
    H.Tide.paused = G.room.id === 'breakers' && !f.landed;
    if (f.landed) G.dialT = Math.min(1, G.dialT + dt * 0.6);
    updateCamera(dt);
    // Ambience follows the tide and the tension.
    const tension = H.clamp(G.swell / 9 + (G.undertow.live ? 0.6 : 0) + (G.bossBar ? 0.4 : 0), 0, 1);
    H.Audio.ambience(H.Tide.tidal(G.room) || G.room.outdoor ? H.Tide.level01() : 0.2, tension, p.headUnder());
    H.Render.draw();
    if (G.glimpseT > 0) drawGlimpse();
  }

  function drawGlimpse() {
    const c = H.Render.ctx;
    const t = 4 - G.glimpseT;
    const x = -200 + t * 220;
    c.fillStyle = 'rgba(0,0,0,0.75)';
    c.beginPath(); c.ellipse(x, 60, 160, 40, 0, 0, Math.PI * 2); c.fill();
    c.fillStyle = 'rgba(120,40,30,0.6)'; c.beginPath(); c.moveTo(x - 30, 40); c.lineTo(x - 20, 10); c.lineTo(x - 10, 40); c.fill();
  }

  G.init = init; G.newGame = newGame; G.loadGame = loadGame; G.save = save; G.hasSave = hasSave; G.toTitle = toTitle;
  G.spawnLoose = spawnLoose; G.spawnPickup = spawnPickup; G.removeLoose = removeLoose; G.addSwell = addSwell; G.canWake = canWake;
  G.shake = shake; G.tip = tip; G.quest = quest; G.setQuest = setQuest; G.checkpointQuest = checkpointQuest; G.bellIdFor = bellIdFor;
  G.applyEncrustation = applyEncrustation; G.startAct = startAct; G.onBodyDowned = onBodyDowned; G.onBodyBurned = onBodyBurned;
  G.onChoirAttacked = onChoirAttacked; G.onCrankTaken = onCrankTaken; G.onTagsTaken = onTagsTaken; G.onCacheOpened = onCacheOpened;
  G.onAbbotKilled = onAbbotKilled; G.onUndertowKilled = onUndertowKilled; G.onShoalKilled = onShoalKilled; G.collect = collect;
  G.setRespawn = setRespawn; G.setChoirLevel = setChoirLevel; G.interact = interact; G.onPlayerDeath = onPlayerDeath;
  G.recoverSatchel = recoverSatchel; G.rest = rest; G.statsSummary = statsSummary; G.bellRoomBreached = bellRoomBreached;
  G.enterRoom = enterRoom; G.snapCamera = snapCamera; G._step = step;
  return G;
})();

window.addEventListener('load', () => H.Game.init());
