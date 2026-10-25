// Rendering: tiles, water, bodies, the lantern, darkness, HUD.
'use strict';
H.Render = (() => {
  const TS = H.TILE;
  let canvas, ctx, dark, dctx, glow, gctx;
  let tileCache = null, decorCache = null, tileCacheRoom = null;
  let dirty = true;
  const stars = Array.from({ length: 70 }, () => ({ x: Math.random(), y: Math.random() * 0.6, s: Math.random() }));

  function init(c) {
    canvas = c; ctx = c.getContext('2d');
    dark = document.createElement('canvas'); dark.width = H.VIEW_W; dark.height = H.VIEW_H; dctx = dark.getContext('2d');
    glow = document.createElement('canvas'); glow.width = H.VIEW_W; glow.height = H.VIEW_H; gctx = glow.getContext('2d');
  }
  function markDirty() { dirty = true; }

  // Deterministic hash for tile variation.
  const hash = (x, y) => { let h = (x * 374761393 + y * 668265263) | 0; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) >>> 0) / 4294967295; };

  // Solid tiles are drawn over water and bodies; decor (ladders, planks, glass, rings) underneath.
  const SOLIDS = new Set('#_TRMPG'.split(''));
  const DECOR = new Set('=|wHoxa'.split(''));
  function buildTiles(room, set) {
    const cv = document.createElement('canvas');
    cv.width = room.pw; cv.height = room.ph;
    const c = cv.getContext('2d');
    const reg = H.REGIONS[room.region];
    for (let ly = 0; ly < room.th; ly++) for (let lx = 0; lx < room.tw; lx++) {
      const tx = room.ox + lx, ty = room.oy + ly;
      const ch = H.World.charAt(tx, ty);
      if (!set.has(ch)) continue;
      drawTile(c, ch, lx * TS, ly * TS, tx, ty, room, reg);
    }
    return cv;
  }
  function airAt(tx, ty) { return H.World.solid(tx, ty) !== 1; }
  function drawTile(c, ch, x, y, tx, ty, room, reg) {
    const h = hash(tx, ty);
    switch (ch) {
      case '#': case 'G': {
        const base = room.region === 'saltworks' ? [92, 86, 78] : room.region === 'ribvault' ? [96, 86, 72] : room.region === 'skull' ? [98, 88, 72] : room.region === 'gullet' ? [42, 58, 64] : room.region === 'choir' ? [60, 66, 86] : [62, 72, 78];
        const v = (h - 0.5) * 10;
        c.fillStyle = `rgb(${base[0] + v},${base[1] + v},${base[2] + v})`;
        c.fillRect(x, y, TS, TS);
        c.fillStyle = 'rgba(0,0,0,0.25)';
        if ((ty % 2 === 0 ? tx : tx + 1) % 2 === 0) c.fillRect(x, y + TS - 1, TS, 1); else c.fillRect(x + TS - 1, y, 1, TS);
        if (airAt(tx, ty - 1)) { c.fillStyle = 'rgba(160,190,200,0.22)'; c.fillRect(x, y, TS, 2); if (room.region === 'saltworks' || h > 0.7) { c.fillStyle = 'rgba(235,240,245,0.35)'; c.fillRect(x + h * 10, y, 3, 1); } }
        if (airAt(tx - 1, ty)) { c.fillStyle = 'rgba(160,190,200,0.08)'; c.fillRect(x, y, 1, TS); }
        if (airAt(tx, ty + 1)) { c.fillStyle = 'rgba(0,0,0,0.3)'; c.fillRect(x, y + TS - 2, TS, 2); }
        if (ch === 'G') { c.fillStyle = '#1a1a1a'; for (let i = 2; i < TS; i += 4) c.fillRect(x + i, y + 1, 2, 5); }
        break;
      }
      case '_': break;
      case 'T': case 'P': {
        c.fillStyle = ch === 'P' ? '#4a3a2a' : '#4e3b28';
        c.fillRect(x, y, TS, TS);
        c.fillStyle = 'rgba(0,0,0,0.3)';
        for (let i = 0; i < TS; i += 5) c.fillRect(x, y + i, TS, 1);
        c.fillStyle = 'rgba(255,220,160,0.06)'; c.fillRect(x + 2, y + 1, 1, TS - 2);
        if (airAt(tx, ty - 1)) { c.fillStyle = 'rgba(200,170,120,0.25)'; c.fillRect(x, y, TS, 1); }
        break;
      }
      case 'R': {
        c.fillStyle = '#b8ac90'; c.fillRect(x, y + 2, TS, TS - 4);
        c.fillStyle = '#d8ccb0'; c.fillRect(x, y + 2, TS, 2);
        c.fillStyle = 'rgba(80,60,40,0.3)'; c.fillRect(x + h * 12, y + 5, 2, 6);
        break;
      }
      case 'M': {
        c.fillStyle = '#4a4440'; c.fillRect(x, y, TS, TS);
        c.fillStyle = '#6a5a4a'; c.fillRect(x, y, TS, 2);
        c.fillStyle = '#7a6a5a'; c.fillRect(x + 2, y + 4, 1.5, 1.5); c.fillRect(x + TS - 4, y + 4, 1.5, 1.5);
        c.fillStyle = `rgba(140,70,30,${0.15 + h * 0.2})`; c.fillRect(x + h * 8, y + 6, 5, 4);
        break;
      }
      case '=': {
        c.fillStyle = '#5a4430'; c.fillRect(x, y, TS, 4);
        c.fillStyle = '#7a5e40'; c.fillRect(x, y, TS, 1);
        c.fillStyle = '#3a2a1a'; if (tx % 3 === 0) c.fillRect(x + 6, y + 4, 2, 4);
        break;
      }
      case '|': {
        c.fillStyle = 'rgba(30,34,38,0.9)'; c.fillRect(x + 2, y, TS - 4, TS);
        c.fillStyle = 'rgba(80,90,95,0.25)'; c.fillRect(x + 3, y, 1, TS);
        break;
      }
      case 'w': {
        const cols = ['rgba(120,40,40,0.35)', 'rgba(40,70,120,0.35)', 'rgba(140,120,40,0.3)', 'rgba(40,100,80,0.3)'];
        c.fillStyle = 'rgba(20,20,24,0.8)'; c.fillRect(x + 2, y, TS - 4, TS);
        c.fillStyle = cols[Math.floor(h * 4)]; c.fillRect(x + 3, y + 1, TS - 6, TS - 2);
        c.fillStyle = 'rgba(0,0,0,0.6)'; c.fillRect(x + 7, y, 1.5, TS); c.fillRect(x + 2, y + 7, TS - 4, 1.5);
        break;
      }
      case 'H': {
        c.fillStyle = '#5a4430'; c.fillRect(x + 3, y, 2, TS); c.fillRect(x + TS - 5, y, 2, TS);
        c.fillRect(x + 3, y + 3, TS - 6, 2); c.fillRect(x + 3, y + 11, TS - 6, 2);
        break;
      }
      case 'o': {
        c.strokeStyle = '#8a8a8a'; c.lineWidth = 1.5; c.beginPath(); c.arc(x + 8, y + 8, 4, 0, Math.PI * 2); c.stroke();
        c.fillStyle = '#5a5a5a'; c.fillRect(x + 7, y, 2, 4);
        break;
      }
      case 'x': {
        c.fillStyle = 'rgba(225,240,250,0.9)';
        for (let i = 0; i < 4; i++) { const bx = x + 1 + i * 4; c.beginPath(); c.moveTo(bx, y + TS); c.lineTo(bx + 1.5, y + 6 + hash(tx + i, ty) * 5); c.lineTo(bx + 3, y + TS); c.fill(); }
        break;
      }
      case 'a': {
        c.fillStyle = 'rgba(180,220,255,0.25)'; c.beginPath(); c.arc(x + 8, y + 8, 6, 0, Math.PI * 2); c.fill();
        break;
      }
    }
  }

  function drawDynamicTiles(c, room, cam) {
    const W = H.World;
    const x0 = Math.max(0, Math.floor((cam.x - room.px) / TS)), x1 = Math.min(room.tw - 1, Math.ceil((cam.x + H.VIEW_W - room.px) / TS));
    const y0 = Math.max(0, Math.floor((cam.y - room.py) / TS)), y1 = Math.min(room.th - 1, Math.ceil((cam.y + H.VIEW_H - room.py) / TS));
    for (let ly = y0; ly <= y1; ly++) for (let lx = x0; lx <= x1; lx++) {
      const tx = room.ox + lx, ty = room.oy + ly;
      const ch = W.charAt(tx, ty);
      const x = room.px + lx * TS, y = room.py + ly * TS;
      const s = W.solidity(ch);
      switch (ch) {
        case 'E': case 'L': case 'V': case 'K': case 'Z': case 'Y': case 'h': {
          const col = { E: '#3a5a6a', L: '#4a3a2a', V: '#5a4a6a', K: '#5a5048', Z: '#6a5a3a', Y: '#3a4a5a', h: '#4a4a4a' }[ch];
          if (s === 1) {
            c.fillStyle = col; c.fillRect(x, y, TS, TS);
            c.fillStyle = 'rgba(0,0,0,0.35)'; c.fillRect(x + 2, y + 2, TS - 4, TS - 4);
            c.fillStyle = 'rgba(255,255,255,0.12)'; c.fillRect(x + 7, y + 2, 2, TS - 4);
            if (ch === 'V') { c.fillStyle = '#c0a060'; c.fillRect(x + 6, y + 6, 4, 4); }
          } else if (ch === 'K' || ch === 'Z') { drawTile(c, 'H', x, y, tx, ty, room); }
          else { c.fillStyle = 'rgba(0,0,0,0.25)'; c.fillRect(x, y, 2, TS); c.fillRect(x + TS - 2, y, 2, TS); }
          break;
        }
        case 'y': if (s) { drawTile(c, '=', x, y, tx, ty, room); } else { c.fillStyle = 'rgba(80,70,60,0.25)'; c.fillRect(x, y, TS, 3); } break;
        case 'C': {
          c.fillStyle = 'rgba(190,230,250,0.55)'; c.fillRect(x + 2, y, TS - 4, TS);
          c.fillStyle = 'rgba(255,255,255,0.5)'; c.fillRect(x + 4, y + 2, 2, TS - 4);
          break;
        }
        case 'S': {
          c.fillStyle = '#c8c0b0'; c.fillRect(x, y, TS, TS);
          c.strokeStyle = 'rgba(90,80,70,0.6)'; c.lineWidth = 1;
          c.beginPath(); c.moveTo(x + 3, y + 2); c.lineTo(x + 8, y + 9); c.lineTo(x + 6, y + 14); c.moveTo(x + 8, y + 9); c.lineTo(x + 13, y + 7); c.stroke();
          break;
        }
        case 'b': {
          c.strokeStyle = '#7a6a50'; c.lineWidth = 1; c.beginPath(); c.moveTo(x, y + 1); c.lineTo(x + TS, y + 1); c.stroke();
          c.fillStyle = '#5a4a30'; c.fillRect(x, y, TS, 3);
          break;
        }
      }
    }
  }

  function drawBackground(c, room, cam) {
    const reg = room.region;
    if (room.outdoor) {
      const g = c.createLinearGradient(0, 0, 0, H.VIEW_H);
      g.addColorStop(0, '#05080c'); g.addColorStop(1, '#0d161c');
      c.fillStyle = g; c.fillRect(0, 0, H.VIEW_W, H.VIEW_H);
      c.fillStyle = 'rgba(200,210,220,0.5)';
      for (const s of stars) { const sx = (s.x * 900 - cam.x * 0.05) % 900; c.fillRect(sx < 0 ? sx + 900 : sx, s.y * H.VIEW_H, s.s > 0.8 ? 1.5 : 1, s.s > 0.8 ? 1.5 : 1); }
      // the dark lighthouse on the skull, far away
      const lx = 520 - cam.x * 0.08, ly = 40 - cam.y * 0.05;
      c.fillStyle = '#0a0e12'; c.beginPath(); c.moveTo(lx - 30, ly + 120); c.quadraticCurveTo(lx, ly - 10, lx + 40, ly + 120); c.fill();
      c.fillStyle = '#121a20'; c.fillRect(lx + 2, ly + 10, 6, 30);
      return;
    }
    const cols = { wrackline: ['#0e1216', '#1a2228'], saltworks: ['#1a1713', '#2a251f'], cloister: ['#0c1212', '#182222'], choir: ['#0c0e16', '#181c2e'], ribvault: ['#16130e', '#262118'], gullet: ['#050c0f', '#0c1a20'], skull: ['#18130c', '#2a1f12'] }[reg];
    const g = c.createLinearGradient(0, 0, 0, H.VIEW_H);
    g.addColorStop(0, cols[0]); g.addColorStop(1, cols[1]);
    c.fillStyle = g; c.fillRect(0, 0, H.VIEW_W, H.VIEW_H);
    // distant arches / ribs
    c.strokeStyle = reg === 'ribvault' || reg === 'skull' ? 'rgba(160,140,110,0.06)' : 'rgba(120,140,150,0.05)';
    c.lineWidth = 10;
    for (let i = -1; i < 6; i++) {
      const ax = i * 160 - (cam.x * 0.3) % 160;
      c.beginPath(); c.arc(ax, H.VIEW_H + 40 - (cam.y * 0.15) % 40, 140, Math.PI, Math.PI * 2); c.stroke();
    }
  }

  function drawTideMarks(c, room) {
    if (!(room.row === 4 && !room.fixedWater)) return;
    const T = H.Tide;
    const lo = (T.SEA_LOW - T.spring) * TS, hi = (T.SEA_HIGH - T.spring) * TS;
    c.font = '6px monospace';
    for (let lx = 0; lx < room.tw; lx++) {
      const tx = room.ox + lx;
      let pillar = false;
      for (let ly = 0; ly < room.th; ly++) if (H.World.charAt(tx, room.oy + ly) === '|') { pillar = true; break; }
      if (!pillar) continue;
      for (let i = 0; i < 4; i++) {
        const y = H.lerp(lo, hi, i / 3);
        c.fillStyle = 'rgba(200,170,90,0.55)'; c.fillRect(tx * TS + 3, y - 0.5, TS - 6, 1.2);
        c.fillStyle = 'rgba(200,170,90,0.45)'; c.fillText(['I', 'II', 'III', 'IV'][i], tx * TS + 4, y - 2);
      }
    }
  }

  function drawWater(c, room, cam) {
    const t = performance.now() / 1000;
    const W = H.World;
    const x0 = Math.max(room.px, cam.x - 4), x1 = Math.min(room.px + room.pw, cam.x + H.VIEW_W + 4);
    const bottom = room.py + room.ph;
    const brine = room.brine;
    c.fillStyle = brine ? 'rgba(170,190,180,0.35)' : room.deep ? 'rgba(20,60,80,0.5)' : 'rgba(30,70,85,0.42)';
    c.beginPath();
    let started = false;
    const pts = [];
    for (let x = x0; x <= x1; x += 4) {
      const s = W.roomSurface(room, x);
      const y = isFinite(s) ? s + Math.sin(x * 0.05 + t * 2) * 0.8 : bottom + 10;
      pts.push([x, y]);
    }
    for (const [x, y] of pts) { if (!started) { c.moveTo(x, y); started = true; } else c.lineTo(x, y); }
    c.lineTo(x1, bottom); c.lineTo(x0, bottom); c.closePath(); c.fill();
    c.strokeStyle = brine ? 'rgba(230,240,230,0.5)' : 'rgba(140,200,220,0.5)'; c.lineWidth = 1;
    c.beginPath(); started = false;
    for (const [x, y] of pts) { if (y > bottom) { started = false; continue; } if (!started) { c.moveTo(x, y); started = true; } else c.lineTo(x, y); }
    c.stroke();
    // Ebb currents
    if (H.Tide.phase() === 'ebbing' && room.seaward) {
      c.strokeStyle = 'rgba(160,210,230,0.18)';
      for (let i = 0; i < 12; i++) {
        const px = ((i * 97 + t * 60 * room.seaward) % room.pw + room.pw) % room.pw + room.px;
        const s = W.roomSurface(room, px);
        if (!isFinite(s)) continue;
        const py = s + 10 + (i * 37) % Math.max(10, bottom - s - 20);
        c.beginPath(); c.moveTo(px, py); c.lineTo(px + 10 * room.seaward, py); c.stroke();
      }
    }
  }

  // ---- lighting ---------------------------------------------------------------
  function lanternPolygon(p, range, spread) {
    const lp = p.lanternPos();
    const n = 28;
    const pts = [[lp.x, lp.y]];
    for (let i = 0; i <= n; i++) {
      const a = p.aim - spread + (spread * 2 * i) / n;
      const d = H.Phys.ray(lp.x, lp.y, a, range);
      pts.push([lp.x + Math.cos(a) * (d + 4), lp.y + Math.sin(a) * (d + 4)]);
    }
    return { lp, pts };
  }

  function knotVisibility(x, y, body) {
    const G = H.Game, p = G.player;
    if (!p || p.dead) return 0;
    let v = 0;
    const d = H.dist(p.cx(), p.cy(), x, y);
    if (p.has('lens') && d < 170 && H.Phys.los(p.cx(), p.y + 6, x, y)) v = Math.max(v, 0.95 * (1 - d / 260));
    if (p.lanternLit()) {
      if (p.focusing) {
        const lp = p.lanternPos();
        const a = Math.atan2(y - lp.y, x - lp.x);
        const dd = H.dist(lp.x, lp.y, x, y);
        if (dd < 260 && Math.abs(H.angleDiff(p.aim, a)) < 0.25 && H.Phys.los(lp.x, lp.y, x, y)) v = 1;
      }
      if (v < 0.5 && d < 80) v = Math.max(v, 0.35 * (1 - d / 80) + 0.12);
    }
    for (const l of G.frameLights) {
      if (l.player) continue;
      const dl = H.dist(l.x, l.y, x, y);
      if (dl < l.r * 0.6) v = Math.max(v, 0.45 * (1 - dl / (l.r * 0.6)));
    }
    return v;
  }

  function draw() {
    const G = H.Game;
    const room = G.room, cam = G.cam, p = G.player;
    const c = ctx;
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.imageSmoothingEnabled = false;
    drawBackground(c, room, cam);

    const sx = (Math.random() - 0.5) * G.shakeAmt, sy = (Math.random() - 0.5) * G.shakeAmt;
    c.save();
    c.translate(Math.round(-cam.x + sx), Math.round(-cam.y + sy));

    if (dirty || tileCacheRoom !== room) { tileCache = buildTiles(room, SOLIDS); decorCache = buildTiles(room, DECOR); tileCacheRoom = room; dirty = false; }
    c.drawImage(decorCache, room.px, room.py);
    drawTideMarks(c, room);

    // objects behind bodies
    for (const e of G.ents) if (!e.isBody && e.draw) e.draw(c);
    for (const s of G.sawlines) s.draw(c);
    for (const e of G.ents) if (e.isBody && !e.gone) { e.draw(c); }
    p.draw(c);
    for (const pr of G.projectiles) pr.draw(c);
    for (const s of G.enemyShots) { c.strokeStyle = '#a0b0b8'; c.lineWidth = 1.5; c.beginPath(); c.moveTo(s.x, s.y); c.lineTo(s.x - s.vx * 0.02, s.y - s.vy * 0.02); c.stroke(); }
    // fire
    for (const [, f] of H.Fire.cells) {
      const fx = f.tx * TS, fy = f.ty * TS;
      const fl = Math.sin(performance.now() / 50 + f.tx) * 2;
      c.fillStyle = 'rgba(255,120,30,0.75)'; c.beginPath(); c.moveTo(fx + 1, fy + TS); c.lineTo(fx + 8, fy + 2 + fl); c.lineTo(fx + 15, fy + TS); c.fill();
      c.fillStyle = 'rgba(255,220,120,0.8)'; c.beginPath(); c.moveTo(fx + 5, fy + TS); c.lineTo(fx + 8, fy + 8 - fl); c.lineTo(fx + 11, fy + TS); c.fill();
    }
    drawWater(c, room, cam);
    c.drawImage(tileCache, room.px, room.py);
    drawDynamicTiles(c, room, cam);
    // knots are drawn above water so they read
    for (const e of G.ents) if (e.isBody && !e.gone && e.drawKnots) e.drawKnots(c);
    // tracers
    for (const t of G.tracers) { c.strokeStyle = `rgba(255,230,180,${t.t * 10})`; c.lineWidth = 1; c.beginPath(); c.moveTo(t.x1, t.y1); c.lineTo(t.x2, t.y2); c.stroke(); }
    // particles
    for (const q of H.FX.parts) { c.globalAlpha = Math.max(0, q.life / q.max); c.fillStyle = q.color; c.fillRect(q.x - q.size / 2, q.y - q.size / 2, q.size, q.size); }
    c.globalAlpha = 1;
    // noise rings (subtle, helps read the sound system)
    for (const n of G.noiseRings) { c.strokeStyle = `rgba(200,200,180,${n.t * 0.12})`; c.lineWidth = 1; c.beginPath(); c.arc(n.x, n.y, n.r * (1 - n.t), 0, Math.PI * 2); c.stroke(); }
    // focused beam
    if (p.beamPath && p.beamPath.length > 1) {
      c.strokeStyle = 'rgba(255,240,190,0.25)'; c.lineWidth = 3;
      c.beginPath(); c.moveTo(p.beamPath[0].x, p.beamPath[0].y);
      for (const q of p.beamPath) c.lineTo(q.x, q.y);
      c.stroke();
    }
    c.restore();

    drawLighting(room, cam, p, sx, sy);

    // underwater tint
    if (p.headUnder()) { c.fillStyle = room.brine ? 'rgba(120,140,120,0.18)' : 'rgba(20,60,80,0.25)'; c.fillRect(0, 0, H.VIEW_W, H.VIEW_H); }
    if (G.flash > 0) { c.fillStyle = `rgba(160,20,20,${G.flash})`; c.fillRect(0, 0, H.VIEW_W, H.VIEW_H); }
    if (G.pressureWarn > 0) {
      const g = c.createRadialGradient(H.VIEW_W / 2, H.VIEW_H / 2, 80, H.VIEW_W / 2, H.VIEW_H / 2, 360);
      g.addColorStop(0, 'rgba(0,0,0,0)'); g.addColorStop(1, `rgba(0,10,20,${0.7 * G.pressureWarn})`);
      c.fillStyle = g; c.fillRect(0, 0, H.VIEW_W, H.VIEW_H);
    }
    if (H.Game.mode === 'play') H.HUD.draw(c);
  }

  function drawLighting(room, cam, p, sx, sy) {
    const G = H.Game;
    const ambient = room.outdoor ? 0.28 : room.bellRoom ? 0.3 : room.region === 'gullet' ? 0.66 : 0.5;
    dctx.globalCompositeOperation = 'source-over';
    dctx.fillStyle = `rgba(2,4,6,${ambient})`;
    dctx.fillRect(0, 0, H.VIEW_W, H.VIEW_H);
    gctx.clearRect(0, 0, H.VIEW_W, H.VIEW_H);
    dctx.globalCompositeOperation = 'destination-out';
    const ox = -cam.x + sx, oy = -cam.y + sy;
    const light = (x, y, r, i) => {
      const g = dctx.createRadialGradient(x + ox, y + oy, 0, x + ox, y + oy, r);
      g.addColorStop(0, `rgba(0,0,0,${i})`); g.addColorStop(1, 'rgba(0,0,0,0)');
      dctx.fillStyle = g; dctx.beginPath(); dctx.arc(x + ox, y + oy, r, 0, Math.PI * 2); dctx.fill();
    };
    // Maren is always faintly visible to the player.
    light(p.cx(), p.cy(), p.lanternLit() ? 46 : 24, p.lanternLit() ? 0.85 : 0.45);
    if (p.lanternLit() && !p.dead) {
      const range = p.focusing ? 260 : 160, spread = p.focusing ? 0.24 : 0.7;
      const poly = lanternPolygon(p, range, spread);
      const g = dctx.createRadialGradient(poly.lp.x + ox, poly.lp.y + oy, 4, poly.lp.x + ox, poly.lp.y + oy, range);
      g.addColorStop(0, 'rgba(0,0,0,1)'); g.addColorStop(0.6, 'rgba(0,0,0,0.75)'); g.addColorStop(1, 'rgba(0,0,0,0)');
      dctx.fillStyle = g;
      dctx.beginPath();
      poly.pts.forEach(([x, y], i) => (i ? dctx.lineTo(x + ox, y + oy) : dctx.moveTo(x + ox, y + oy)));
      dctx.closePath(); dctx.fill();
      gctx.fillStyle = gctx.createRadialGradient(poly.lp.x + ox, poly.lp.y + oy, 4, poly.lp.x + ox, poly.lp.y + oy, range);
      const gg = gctx.fillStyle;
      gg.addColorStop(0, 'rgba(255,200,120,0.18)'); gg.addColorStop(1, 'rgba(255,200,120,0)');
      gctx.beginPath(); poly.pts.forEach(([x, y], i) => (i ? gctx.lineTo(x + ox, y + oy) : gctx.moveTo(x + ox, y + oy))); gctx.closePath(); gctx.fill();
    }
    for (const l of G.frameLights) {
      if (l.player) continue;
      light(l.x, l.y, l.r, l.i);
      const g = gctx.createRadialGradient(l.x + ox, l.y + oy, 0, l.x + ox, l.y + oy, l.r);
      g.addColorStop(0, `rgba(${l.color || '255,200,120'},0.16)`); g.addColorStop(1, `rgba(${l.color || '255,200,120'},0)`);
      gctx.fillStyle = g; gctx.beginPath(); gctx.arc(l.x + ox, l.y + oy, l.r, 0, Math.PI * 2); gctx.fill();
    }
    for (const [, f] of H.Fire.cells) light(f.tx * TS + 8, f.ty * TS + 8, 60, 0.8);
    ctx.drawImage(dark, 0, 0);
    ctx.globalCompositeOperation = 'lighter';
    ctx.drawImage(glow, 0, 0);
    ctx.globalCompositeOperation = 'source-over';
  }

  return { init, draw, markDirty, knotVisibility, drawTile, get ctx() { return ctx; } };
})();
