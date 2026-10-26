// Rendering: sky and back walls, tiles, water, bodies, weather, the lantern, darkness, post, HUD.
'use strict';
H.Render = (() => {
  const TS = H.TILE, VW = H.VIEW_W, VH = H.VIEW_H;
  const LS = 2; // light buffers render at half resolution; they are soft anyway
  let canvas, ctx, dark, dctx, glow, gctx;
  let cache = { room: null, solid: null, back: null, dyn: [], windows: [], pillars: [], ceilings: [], tops: [] };
  let dirty = true;
  const stars = Array.from({ length: 80 }, () => ({ x: Math.random(), y: Math.random() * 0.55, s: Math.random(), p: Math.random() * 6 }));
  const bgCache = {};
  let overlays = [];
  let skyCanvas = null, cloudCanvas = null, grain = [], lighthouse = null, overlayKey = null;
  let rain = [], ambient = [], lightning = { t: 0, next: 12, flash: 0 };

  // Region palettes: stone, back wall, accent detail, colour grade.
  const PAL = {
    wrackline: { stone: [66, 76, 82], back: [26, 32, 36], accent: 'barnacle', grade: '30,60,80', bg: ['#0e1216', '#1a2228'] },
    saltworks: { stone: [98, 92, 84], back: [42, 38, 34], accent: 'salt', grade: '90,70,40', bg: ['#1a1713', '#2a251f'] },
    cloister: { stone: [60, 74, 70], back: [24, 32, 31], accent: 'moss', grade: '30,70,55', bg: ['#0c1212', '#182222'] },
    choir: { stone: [64, 70, 94], back: [24, 28, 44], accent: 'mosaic', grade: '40,40,100', bg: ['#0c0e16', '#181c2e'] },
    ribvault: { stone: [104, 94, 78], back: [38, 33, 27], accent: 'bone', grade: '90,65,35', bg: ['#16130e', '#262118'] },
    gullet: { stone: [46, 62, 68], back: [12, 20, 24], accent: 'flesh', grade: '20,60,70', bg: ['#050c0f', '#0c1a20'] },
    skull: { stone: [106, 96, 78], back: [40, 34, 26], accent: 'bone', grade: '110,75,30', bg: ['#18130c', '#2a1f12'] },
  };

  function init(c) {
    canvas = c; ctx = c.getContext('2d');
    dark = mk(VW / LS, VH / LS); dctx = dark.getContext('2d');
    glow = mk(VW / LS, VH / LS); gctx = glow.getContext('2d');
    buildSky();
    for (let f = 0; f < 2; f++) {
      const g = mk(320, 180), gc = g.getContext('2d'), img = gc.createImageData(320, 180);
      for (let i = 0; i < img.data.length; i += 4) { const v = Math.random() * 255; img.data[i] = img.data[i + 1] = img.data[i + 2] = v; img.data[i + 3] = 255; }
      gc.putImageData(img, 0, 0); grain.push(g);
    }
  }
  function mk(w, h) { const cv = document.createElement('canvas'); cv.width = w; cv.height = h; return cv; }
  function markDirty() { dirty = true; }

  // Deterministic hash for tile variation.
  const hash = (x, y) => { let h = (x * 374761393 + y * 668265263) | 0; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) >>> 0) / 4294967295; };
  const rgb = (c, k = 1, add = 0) => `rgb(${Math.round(c[0] * k + add)},${Math.round(c[1] * k + add)},${Math.round(c[2] * k + add)})`;

  // ---- static caches -------------------------------------------------------------------
  const SOLIDS = new Set('#_TRMPG'.split(''));
  const DECOR = new Set('=|wHoxa'.split(''));
  const DYNAMIC = new Set('ELVKZYhyCSb'.split(''));
  const COVERS = new Set('#GTPM'.split('')); // tiles that hide the back wall completely
  const isSolidCh = (ch) => SOLIDS.has(ch) || ch === 'S' || ch === 'C' || ch === 'E' || ch === 'L' || ch === 'V' || ch === 'K' || ch === 'Z' || ch === 'Y' || ch === 'h';

  function buildCaches(room) {
    const W = H.World, pal = PAL[room.region];
    const at = (lx, ly) => (lx < 0 || ly < 0 || lx >= room.tw || ly >= room.th ? W.charAt(room.ox + lx, room.oy + ly) : room.live[ly * room.tw + lx]);
    const solidAt = (lx, ly) => isSolidCh(at(lx, ly));
    // distance to the nearest solid cell, for ambient occlusion on back walls
    const dist = new Uint8Array(room.tw * room.th).fill(9);
    for (let ly = 0; ly < room.th; ly++) for (let lx = 0; lx < room.tw; lx++) {
      if (solidAt(lx, ly)) { dist[ly * room.tw + lx] = 0; continue; }
      let d = 9;
      for (let r = 1; r <= 4 && d === 9; r++) for (let dy = -r; dy <= r && d === 9; dy++) for (let dx = -r; dx <= r; dx++) if ((Math.abs(dx) === r || Math.abs(dy) === r) && solidAt(lx + dx, ly + dy)) { d = r; break; }
      dist[ly * room.tw + lx] = d;
    }
    // back holds the wall behind everything plus decor (ladders, planks, glass); solid is drawn over water and bodies
    const solid = mk(room.pw, room.ph), back = mk(room.pw, room.ph);
    const sc = solid.getContext('2d'), bc = back.getContext('2d');
    const dyn = [], windows = [], pillars = new Set(), ceilings = [], tops = [];
    for (let ly = 0; ly < room.th; ly++) for (let lx = 0; lx < room.tw; lx++) {
      const ch = at(lx, ly), x = lx * TS, y = ly * TS, tx = room.ox + lx, ty = room.oy + ly;
      if (!room.outdoor && !COVERS.has(ch) && ch !== 'w') drawBackTile(bc, x, y, tx, ty, pal, room, dist[ly * room.tw + lx]);
      if (ch === 'w') { drawWindowHole(bc, x, y, tx, ty); windows.push([tx, ty]); }
      if (SOLIDS.has(ch)) drawSolidTile(sc, ch, x, y, tx, ty, room, pal, solidAt, lx, ly);
      else if (DECOR.has(ch)) drawTile(bc, ch, x, y, tx, ty, room);
      if (DYNAMIC.has(ch)) dyn.push([tx, ty]);
      if (ch === '|') pillars.add(tx);
      if (!solidAt(lx, ly) && solidAt(lx, ly - 1) && SOLIDS.has(at(lx, ly - 1))) ceilings.push([tx, ty]);
      if (!solidAt(lx, ly) && solidAt(lx, ly + 1)) tops.push([tx, ty + 1]);
    }
    cache = { room, solid, back, dyn, windows, pillars: [...pillars], ceilings, tops };
    dirty = false;
  }

  function drawBackTile(c, x, y, tx, ty, pal, room, d) {
    const ao = 0.5 + 0.125 * Math.min(d, 4);
    const h = hash(tx, ty);
    const reg = room.region;
    // big shapes in the back wall: pilasters, ribs, folds
    let shape = 1;
    if (reg === 'cloister' || reg === 'choir' || reg === 'wrackline') { const m = ((tx % 10) + 10) % 10; if (m === 0) shape = 1.35; else if (m === 1) shape = 0.8; }
    if (reg === 'ribvault' || reg === 'skull') shape = 1 + 0.25 * Math.sin(tx * 0.9 + Math.sin(ty * 0.35) * 1.5);
    if (reg === 'gullet') shape = 1 + 0.3 * Math.sin(ty * 0.6 + tx * 0.25);
    const k = ao * shape;
    c.fillStyle = rgb(pal.back, k * (0.94 + h * 0.12));
    c.fillRect(x, y, TS, TS);
    // brick courses (bigger and fainter than the solid masonry)
    c.fillStyle = `rgba(0,0,0,${0.22 * ao})`;
    c.fillRect(x, y + 7, TS, 1); c.fillRect(x, y + 15, TS, 1);
    const off = (ty % 2) * 8;
    c.fillRect(x + ((off + 4) % 16), y, 1, 7); c.fillRect(x + ((off + 12) % 16), y + 8, 1, 7);
    if (reg === 'saltworks' && h > 0.72) { c.fillStyle = 'rgba(230,232,236,0.10)'; c.fillRect(x + h * 12, y + 3, 2, 1); }
    if (reg === 'cloister' && h > 0.85) { c.fillStyle = 'rgba(70,110,70,0.12)'; c.fillRect(x + 2, y + h * 10, 3, 2); }
    if (reg === 'choir' && h > 0.9) { c.fillStyle = ['rgba(80,90,170,0.12)', 'rgba(150,120,60,0.12)'][(tx + ty) & 1]; c.fillRect(x + 4, y + 4, 3, 3); }
  }
  function drawWindowHole(c, x, y, tx, ty) {
    // moonlit sky seen through the glass
    const g = c.createLinearGradient(0, y, 0, y + TS);
    g.addColorStop(0, '#24384a'); g.addColorStop(1, '#16222e');
    c.fillStyle = g; c.fillRect(x, y, TS, TS);
  }

  function drawSolidTile(c, ch, x, y, tx, ty, room, pal, solidAt, lx, ly) {
    const h = hash(tx, ty);
    const up = !solidAt(lx, ly - 1), down = !solidAt(lx, ly + 1), left = !solidAt(lx - 1, ly), right = !solidAt(lx + 1, ly);
    const exposed = up || down || left || right;
    switch (ch) {
      case '_': return;
      case '#': case 'G': {
        if (!exposed) {
          // inside a rock mass: dark and quiet
          c.fillStyle = rgb(pal.stone, 0.42 + h * 0.05); c.fillRect(x, y, TS, TS);
          if (h > 0.8) { c.fillStyle = 'rgba(0,0,0,0.18)'; c.fillRect(x + h * 10, y + 4, 4, 3); }
          break;
        }
        // masonry: two courses of bricks, offset by row
        for (let r = 0; r < 2; r++) {
          const row = ty * 2 + r, off = (row % 2) * 8;
          for (let b = -1; b < 2; b++) {
            const bx = x + off + b * 16 - 8;
            const v = hash(tx * 3 + b, row) - 0.5;
            c.fillStyle = rgb(pal.stone, 0.88 + v * 0.18);
            c.fillRect(Math.max(x, bx), y + r * 8, Math.min(x + TS, bx + 16) - Math.max(x, bx), 8);
          }
          c.fillStyle = 'rgba(0,0,0,0.32)';
          c.fillRect(x, y + r * 8 + 7, TS, 1);
          const m = x + ((off + 8) % 16);
          c.fillRect(m, y + r * 8, 1, 7);
        }
        c.fillStyle = 'rgba(255,255,255,0.04)'; c.fillRect(x, y, TS, 1);
        if (up) {
          c.fillStyle = 'rgba(170,200,210,0.24)'; c.fillRect(x, y, TS, 2);
          accentTop(c, x, y, tx, ty, h, pal.accent, room);
        }
        if (down) { c.fillStyle = 'rgba(0,0,0,0.38)'; c.fillRect(x, y + TS - 3, TS, 3); }
        if (left) { c.fillStyle = 'rgba(170,200,210,0.08)'; c.fillRect(x, y, 1, TS); }
        if (right) { c.fillStyle = 'rgba(0,0,0,0.22)'; c.fillRect(x + TS - 2, y, 2, TS); }
        if (ch === 'G') { c.fillStyle = '#121212'; for (let i = 2; i < TS; i += 4) c.fillRect(x + i, y + 1, 2, 6); }
        break;
      }
      case 'T': case 'P': {
        const base = ch === 'P' ? [76, 58, 42] : [80, 60, 40];
        c.fillStyle = rgb(base, 0.9 + h * 0.15); c.fillRect(x, y, TS, TS);
        c.fillStyle = 'rgba(0,0,0,0.28)'; for (let i = 0; i < TS; i += 5) c.fillRect(x, y + i, TS, 1);
        c.fillStyle = 'rgba(255,220,160,0.07)'; c.fillRect(x + 2 + h * 6, y + 1, 1, TS - 2);
        c.fillStyle = 'rgba(30,20,10,0.5)'; c.fillRect(x + 3, y + 2, 1, 1); c.fillRect(x + 12, y + 7, 1, 1);
        if (up) { c.fillStyle = 'rgba(210,180,130,0.28)'; c.fillRect(x, y, TS, 1); }
        if (down) { c.fillStyle = 'rgba(0,0,0,0.3)'; c.fillRect(x, y + TS - 2, TS, 2); }
        break;
      }
      case 'R': {
        const g = c.createLinearGradient(0, y, 0, y + TS);
        g.addColorStop(0, '#dccfb2'); g.addColorStop(0.5, '#b4a68a'); g.addColorStop(1, '#8a7c62');
        c.fillStyle = g; c.fillRect(x, y + 2, TS, TS - 4);
        c.fillStyle = 'rgba(80,60,40,0.35)'; c.fillRect(x + h * 12, y + 5, 2, 6);
        c.fillStyle = 'rgba(255,250,230,0.25)'; c.fillRect(x, y + 3, TS, 1);
        break;
      }
      case 'M': {
        c.fillStyle = '#4c4642'; c.fillRect(x, y, TS, TS);
        c.fillStyle = '#6c5c4c'; c.fillRect(x, y, TS, 2);
        c.fillStyle = 'rgba(0,0,0,0.35)'; c.fillRect(x + 7, y + 2, 1, TS - 2);
        c.fillStyle = '#8a7a64'; c.fillRect(x + 2, y + 4, 1.5, 1.5); c.fillRect(x + TS - 4, y + 4, 1.5, 1.5); c.fillRect(x + 2, y + 12, 1.5, 1.5);
        c.fillStyle = `rgba(150,70,30,${0.18 + h * 0.2})`; c.fillRect(x + h * 8, y + 6, 4, 8);
        break;
      }
    }
  }
  function accentTop(c, x, y, tx, ty, h, accent, room) {
    if (accent === 'salt' || h > 0.78) {
      c.fillStyle = 'rgba(236,240,245,0.55)';
      for (let i = 0; i < 4; i++) { const px = x + ((h * 97 + i * 5) % 15); c.fillRect(px, y - 1, 1.5, 2 + ((i + tx) % 2)); }
    }
    if (accent === 'barnacle' && room.row === 4) {
      c.fillStyle = 'rgba(30,40,40,0.6)'; c.fillRect(x + h * 12, y + 2, 2, 2); c.fillRect(x + ((h * 31) % 13), y + 3, 1, 1);
      c.fillStyle = 'rgba(60,110,70,0.5)'; c.fillRect(x + ((h * 53) % 12), y + 1, 3, 1);
    }
    if (accent === 'moss') { c.fillStyle = 'rgba(70,120,70,0.55)'; c.fillRect(x + h * 8, y, 6, 2); c.fillRect(x + ((h * 41) % 13), y + 2, 2, 1); }
    if (accent === 'mosaic' && h > 0.6) { c.fillStyle = ['#5a64b0', '#b09050', '#4a8a7a'][Math.floor(h * 3) % 3]; c.fillRect(x + 6, y + 4, 2, 2); }
    if (accent === 'flesh') { c.fillStyle = 'rgba(120,50,50,0.35)'; c.fillRect(x, y + 2, TS, 1); }
    if (accent === 'bone' && h > 0.5) { c.fillStyle = 'rgba(230,220,195,0.3)'; c.fillRect(x + 2, y + 1, 10, 1); }
  }

  function drawTile(c, ch, x, y, tx, ty, room) {
    const h = hash(tx, ty);
    switch (ch) {
      case '=': {
        c.fillStyle = '#5c4632'; c.fillRect(x, y, TS, 4);
        c.fillStyle = '#7e6244'; c.fillRect(x, y, TS, 1);
        c.fillStyle = 'rgba(0,0,0,0.35)'; c.fillRect(x, y + 3, TS, 1);
        c.fillStyle = '#3a2a1a'; if (tx % 3 === 0) c.fillRect(x + 6, y + 4, 2, 5);
        break;
      }
      case '|': {
        const g = c.createLinearGradient(x + 2, 0, x + TS - 2, 0);
        g.addColorStop(0, 'rgba(22,26,30,0.95)'); g.addColorStop(0.45, 'rgba(58,66,70,0.95)'); g.addColorStop(1, 'rgba(16,18,22,0.95)');
        c.fillStyle = g; c.fillRect(x + 2, y, TS - 4, TS);
        c.fillStyle = 'rgba(0,0,0,0.25)'; c.fillRect(x + 2, y + 15, TS - 4, 1);
        break;
      }
      case 'w': {
        const cols = ['rgba(150,50,50,0.45)', 'rgba(50,80,150,0.45)', 'rgba(170,140,50,0.4)', 'rgba(50,120,95,0.4)'];
        c.fillStyle = cols[Math.floor(h * 4)]; c.fillRect(x + 3, y + 1, TS - 6, TS - 2);
        c.fillStyle = 'rgba(255,255,255,0.08)'; c.fillRect(x + 4, y + 2, 2, 5);
        c.fillStyle = 'rgba(10,10,12,0.9)'; c.fillRect(x + 7, y, 1.5, TS); c.fillRect(x + 2, y + 7, TS - 4, 1.5);
        c.fillRect(x + 2, y, 1.5, TS); c.fillRect(x + TS - 3.5, y, 1.5, TS);
        break;
      }
      case 'H': {
        c.fillStyle = '#5c4632'; c.fillRect(x + 3, y, 2, TS); c.fillRect(x + TS - 5, y, 2, TS);
        c.fillStyle = '#6e5640'; c.fillRect(x + 3, y + 3, TS - 6, 2); c.fillRect(x + 3, y + 11, TS - 6, 2);
        c.fillStyle = 'rgba(0,0,0,0.3)'; c.fillRect(x + 3, y + 5, TS - 6, 1); c.fillRect(x + 3, y + 13, TS - 6, 1);
        break;
      }
      case 'o': {
        c.strokeStyle = '#9a9a9a'; c.lineWidth = 1.5; c.beginPath(); c.arc(x + 8, y + 8, 4, 0, Math.PI * 2); c.stroke();
        c.strokeStyle = 'rgba(255,255,255,0.25)'; c.lineWidth = 0.8; c.beginPath(); c.arc(x + 8, y + 8, 4, Math.PI * 1.1, Math.PI * 1.6); c.stroke();
        c.fillStyle = '#5a5a5a'; c.fillRect(x + 7, y, 2, 4);
        break;
      }
      case 'x': {
        for (let i = 0; i < 4; i++) {
          const bx = x + 1 + i * 4, tip = y + 5 + hash(tx + i, ty) * 5;
          c.fillStyle = 'rgba(225,240,250,0.95)'; c.beginPath(); c.moveTo(bx, y + TS); c.lineTo(bx + 1.5, tip); c.lineTo(bx + 3, y + TS); c.fill();
          c.fillStyle = 'rgba(150,190,210,0.6)'; c.beginPath(); c.moveTo(bx + 1.5, tip); c.lineTo(bx + 3, y + TS); c.lineTo(bx + 1.8, y + TS); c.fill();
        }
        break;
      }
      case 'a': {
        c.fillStyle = 'rgba(180,220,255,0.22)'; c.beginPath(); c.arc(x + 8, y + 8, 6, 0, Math.PI * 2); c.fill();
        c.strokeStyle = 'rgba(200,235,255,0.35)'; c.lineWidth = 1; c.beginPath(); c.arc(x + 8, y + 8, 4, 0, Math.PI * 2); c.stroke();
        break;
      }
    }
  }

  function drawDynamicTiles(c, room, cam) {
    const W = H.World;
    for (const [tx, ty] of cache.dyn) {
      const x = tx * TS, y = ty * TS;
      if (x + TS < cam.x || x > cam.x + VW || y + TS < cam.y || y > cam.y + VH) continue;
      const ch = W.charAt(tx, ty);
      const s = W.solidity(ch);
      switch (ch) {
        case 'E': case 'L': case 'V': case 'K': case 'Z': case 'Y': case 'h': {
          const col = { E: '#3a5a6a', L: '#4a3a2a', V: '#5a4a6a', K: '#5a5048', Z: '#6a5a3a', Y: '#3a4a5a', h: '#4a4a4a' }[ch];
          if (s === 1) {
            c.fillStyle = col; c.fillRect(x, y, TS, TS);
            c.fillStyle = 'rgba(0,0,0,0.35)'; c.fillRect(x + 2, y + 2, TS - 4, TS - 4);
            c.fillStyle = 'rgba(255,255,255,0.12)'; c.fillRect(x + 7, y + 2, 2, TS - 4);
            c.fillStyle = 'rgba(20,20,20,0.6)'; c.fillRect(x + 1, y + 4, 2, 1); c.fillRect(x + TS - 3, y + 4, 2, 1);
            if (ch === 'V') { c.fillStyle = '#c0a060'; c.fillRect(x + 6, y + 6, 4, 4); }
          } else if (ch === 'K' || ch === 'Z') drawTile(c, 'H', x, y, tx, ty, room);
          else { c.fillStyle = 'rgba(0,0,0,0.25)'; c.fillRect(x, y, 2, TS); c.fillRect(x + TS - 2, y, 2, TS); }
          break;
        }
        case 'y': if (s) drawTile(c, '=', x, y, tx, ty, room); else { c.fillStyle = 'rgba(80,70,60,0.25)'; c.fillRect(x, y, TS, 3); } break;
        case 'C': {
          const t = performance.now() / 600 + tx;
          c.fillStyle = 'rgba(190,230,250,0.55)'; c.fillRect(x + 2, y, TS - 4, TS);
          c.fillStyle = `rgba(255,255,255,${0.35 + Math.sin(t) * 0.15})`; c.fillRect(x + 4, y + 2, 2, TS - 4);
          break;
        }
        case 'S': {
          c.fillStyle = '#ccc4b4'; c.fillRect(x, y, TS, TS);
          c.fillStyle = 'rgba(255,255,255,0.35)'; c.fillRect(x, y, TS, 1);
          c.strokeStyle = 'rgba(90,80,70,0.6)'; c.lineWidth = 1;
          c.beginPath(); c.moveTo(x + 3, y + 2); c.lineTo(x + 8, y + 9); c.lineTo(x + 6, y + 14); c.moveTo(x + 8, y + 9); c.lineTo(x + 13, y + 7); c.stroke();
          break;
        }
        case 'b': {
          c.fillStyle = '#5a4a30'; c.fillRect(x, y, TS, 3);
          c.strokeStyle = '#7a6a50'; c.lineWidth = 1; c.beginPath(); c.moveTo(x, y + 1); c.lineTo(x + TS, y + 1); c.stroke();
          break;
        }
      }
    }
  }

  // ---- background ------------------------------------------------------------------------
  function buildSky() {
    skyCanvas = mk(VW, VH);
    const c = skyCanvas.getContext('2d');
    const g = c.createLinearGradient(0, 0, 0, VH);
    g.addColorStop(0, '#04070b'); g.addColorStop(0.55, '#0c141b'); g.addColorStop(1, '#16222b');
    c.fillStyle = g; c.fillRect(0, 0, VW, VH);
    // moon and halo
    const mx = 150, my = 62;
    const halo = c.createRadialGradient(mx, my, 6, mx, my, 120);
    halo.addColorStop(0, 'rgba(180,200,215,0.28)'); halo.addColorStop(1, 'rgba(180,200,215,0)');
    c.fillStyle = halo; c.fillRect(0, 0, VW, VH);
    c.fillStyle = '#d8e0e4'; c.beginPath(); c.arc(mx, my, 13, 0, Math.PI * 2); c.fill();
    c.fillStyle = 'rgba(150,165,175,0.5)'; c.beginPath(); c.arc(mx + 4, my - 3, 3, 0, Math.PI * 2); c.arc(mx - 4, my + 4, 2, 0, Math.PI * 2); c.fill();
    // far sea with a moon path
    const sea = c.createLinearGradient(0, 250, 0, VH);
    sea.addColorStop(0, '#0d1820'); sea.addColorStop(1, '#060c11');
    c.fillStyle = sea; c.fillRect(0, 250, VW, VH - 250);
    for (let i = 0; i < 60; i++) {
      const y = 252 + i * 1.8, w = 6 + i * 0.9 + Math.random() * 10;
      c.fillStyle = `rgba(190,210,220,${0.22 - i * 0.003})`;
      c.fillRect(mx - w / 2 + (Math.random() - 0.5) * 8, y, w, 1);
    }
    // clouds
    cloudCanvas = mk(1280, 170);
    const cc = cloudCanvas.getContext('2d');
    for (let i = 0; i < 26; i++) {
      const x = Math.random() * 1280, y = 30 + Math.random() * 110, r = 30 + Math.random() * 60;
      const cg = cc.createRadialGradient(x, y, 0, x, y, r);
      cg.addColorStop(0, 'rgba(40,52,62,0.55)'); cg.addColorStop(1, 'rgba(40,52,62,0)');
      cc.fillStyle = cg;
      for (const k of [-1280, 0, 1280]) { cc.save(); cc.translate(k, 0); cc.fillRect(x - r, y - r, r * 2, r * 2); cc.restore(); }
    }
    // the dark lighthouse on the skull
    lighthouse = mk(140, 150);
    const lc = lighthouse.getContext('2d');
    lc.fillStyle = '#070b0e';
    lc.beginPath(); lc.moveTo(0, 150); lc.quadraticCurveTo(40, 70, 70, 74); lc.quadraticCurveTo(105, 72, 140, 150); lc.fill();
    lc.beginPath(); lc.arc(70, 92, 26, Math.PI, 0); lc.fill();
    lc.fillRect(64, 30, 12, 50);
    lc.fillRect(60, 24, 20, 8);
    lc.beginPath(); lc.moveTo(60, 24); lc.lineTo(70, 12); lc.lineTo(80, 24); lc.fill();
    lc.fillStyle = '#0d1418'; lc.fillRect(66, 26, 8, 4);
  }

  function drawBackground(c, room, cam, t) {
    if (room.outdoor) {
      c.drawImage(skyCanvas, 0, 0);
      for (const s of stars) {
        const tw = 0.35 + 0.35 * Math.sin(t * 1.5 + s.p);
        const sx = ((s.x * 900 - cam.x * 0.03) % 900 + 900) % 900;
        c.fillStyle = `rgba(210,220,230,${tw})`; c.fillRect(sx, s.y * 220, s.s > 0.85 ? 1.5 : 1, s.s > 0.85 ? 1.5 : 1);
      }
      const cx = -((cam.x * 0.04 + t * 3) % 1280);
      c.drawImage(cloudCanvas, cx, 10); c.drawImage(cloudCanvas, cx + 1280, 10);
      c.drawImage(lighthouse, 470 - (cam.x - room.px) * 0.06, 120 - (cam.y - room.py) * 0.03);
      if (lightning.flash > 0) { c.fillStyle = `rgba(200,215,230,${lightning.flash * 0.35})`; c.fillRect(0, 0, VW, VH); }
      return;
    }
    const pal = PAL[room.region];
    if (!bgCache[room.region]) {
      const b = mk(VW, VH), bc = b.getContext('2d');
      const g = bc.createLinearGradient(0, 0, 0, VH);
      g.addColorStop(0, pal.bg[0]); g.addColorStop(1, pal.bg[1]);
      bc.fillStyle = g; bc.fillRect(0, 0, VW, VH);
      bgCache[room.region] = b;
    }
    c.drawImage(bgCache[room.region], 0, 0);
  }

  function drawTideMarks(c, room) {
    if (!(room.row === 4 && !room.fixedWater)) return;
    const T = H.Tide;
    const lo = (T.SEA_LOW - T.spring) * TS, hi = (T.SEA_HIGH - T.spring) * TS;
    c.font = '6px monospace';
    for (const tx of cache.pillars) {
      for (let i = 0; i < 4; i++) {
        const y = H.lerp(lo, hi, i / 3);
        c.fillStyle = 'rgba(200,170,90,0.55)'; c.fillRect(tx * TS + 3, y - 0.5, TS - 6, 1.2);
        c.fillStyle = 'rgba(200,170,90,0.45)'; c.fillText(['I', 'II', 'III', 'IV'][i], tx * TS + 4, y - 2);
      }
    }
  }

  // ---- water -------------------------------------------------------------------------------
  function drawWater(c, room, cam, t) {
    const W = H.World;
    const x0 = Math.max(room.px, cam.x - 4), x1 = Math.min(room.px + room.pw, cam.x + VW + 4);
    const bottom = Math.min(room.py + room.ph, cam.y + VH + 4);
    const pts = [];
    let top = Infinity;
    for (let x = x0; x <= x1 + 3; x += 4) {
      const s = W.roomSurface(room, x);
      const y = isFinite(s) ? s + Math.sin(x * 0.05 + t * 2) * 0.8 + Math.sin(x * 0.13 - t * 1.3) * 0.4 : bottom + 10;
      pts.push([x, y]); if (y < top) top = y;
    }
    if (!isFinite(top) || top > bottom) return;
    const brine = room.brine, deep = room.deep;
    const g = c.createLinearGradient(0, Math.max(top, cam.y - 40), 0, bottom + 60);
    if (brine) { g.addColorStop(0, 'rgba(190,205,190,0.38)'); g.addColorStop(1, 'rgba(110,130,115,0.6)'); }
    else if (deep) { g.addColorStop(0, 'rgba(25,70,90,0.5)'); g.addColorStop(1, 'rgba(4,16,24,0.78)'); }
    else { g.addColorStop(0, 'rgba(40,95,110,0.36)'); g.addColorStop(1, 'rgba(8,28,40,0.66)'); }
    c.beginPath();
    pts.forEach(([x, y], i) => (i ? c.lineTo(x, y) : c.moveTo(x, y)));
    c.lineTo(x1 + 3, bottom); c.lineTo(x0, bottom); c.closePath();
    c.fillStyle = g; c.fill();
    // caustic light drifting through the water
    c.save(); c.clip();
    c.globalCompositeOperation = 'lighter';
    c.strokeStyle = brine ? 'rgba(230,240,220,0.05)' : 'rgba(150,210,230,0.055)'; c.lineWidth = 2;
    for (let i = 0; i < 7; i++) {
      const by = top + 14 + i * 26;
      if (by > bottom) break;
      c.beginPath();
      for (let x = x0; x <= x1; x += 8) { const y = by + Math.sin(x * 0.035 + t * (0.9 + i * 0.13) + i) * 5 + Math.sin(x * 0.011 - t * 0.5) * 7; x === x0 ? c.moveTo(x, y) : c.lineTo(x, y); }
      c.stroke();
    }
    c.globalCompositeOperation = 'source-over';
    c.restore();
    // surface line with moving glints
    c.strokeStyle = brine ? 'rgba(235,245,235,0.6)' : 'rgba(150,210,228,0.55)'; c.lineWidth = 1;
    c.beginPath(); let on = false;
    for (const [x, y] of pts) { if (y > bottom) { on = false; continue; } on ? c.lineTo(x, y) : c.moveTo(x, y); on = true; }
    c.stroke();
    c.fillStyle = 'rgba(220,240,250,0.5)';
    for (let i = 0; i < pts.length; i += 3) {
      const [x, y] = pts[i];
      if (y > bottom) continue;
      const k = Math.sin(x * 0.3 + t * 3.1);
      if (k > 0.75) c.fillRect(x, y - 0.5, 3, 1);
    }
    // Ebb currents
    if (H.Tide.phase() === 'ebbing' && room.seaward) {
      c.strokeStyle = 'rgba(160,210,230,0.2)';
      for (let i = 0; i < 14; i++) {
        const px = ((i * 97 + t * 60 * room.seaward) % room.pw + room.pw) % room.pw + room.px;
        const s = W.roomSurface(room, px);
        if (!isFinite(s)) continue;
        const py = s + 10 + (i * 37) % Math.max(10, room.py + room.ph - s - 20);
        c.beginPath(); c.moveTo(px, py); c.lineTo(px + 12 * room.seaward, py); c.stroke();
      }
    }
  }

  // ---- weather and ambient life --------------------------------------------------------------
  function updateAtmosphere(room, cam, dt, t) {
    // rain, outdoors only
    if (room.outdoor) {
      while (rain.length < 170) rain.push({ x: Math.random() * (VW + 120) - 60, y: Math.random() * VH, v: 300 + Math.random() * 160, l: 6 + Math.random() * 8 });
      for (const d of rain) { d.y += d.v * dt; d.x -= d.v * 0.28 * dt; if (d.y > VH || d.x < -40) { d.y = -10; d.x = Math.random() * (VW + 120); } }
      const top = cache.tops.length && Math.random() < dt * 40 && pickVisible(cache.tops, cam);
      if (top) {
        const [tx, ty] = top;
        const x = tx * TS + Math.random() * TS, y = ty * TS;
        if (x > cam.x && x < cam.x + VW && y > cam.y && y < cam.y + VH && !H.World.solidPx(x, y - 4)) H.FX.spawn(x, y, 2, { color: 'rgba(170,200,215,0.7)', ang: -Math.PI / 2, spread: 0.9, smin: 20, smax: 50, grav: 300, lmin: 0.15, lmax: 0.3, size: 1 });
      }
      lightning.next -= dt;
      if (lightning.next <= 0) {
        lightning.next = 14 + Math.random() * 22; lightning.flash = 1; lightning.second = 0.18;
        setTimeout(() => H.Audio.rumble(), 700 + Math.random() * 1200);
      }
    } else rain.length = 0;
    if (lightning.second !== undefined) { lightning.second -= dt; if (lightning.second <= 0) { lightning.flash = Math.max(lightning.flash, 0.7); lightning.second = undefined; } }
    lightning.flash = Math.max(0, lightning.flash - dt * 2.6);

    // drifting motes, salt, drips and bubbles in world space around the camera
    const reg = room.region;
    const want = room.outdoor ? 0 : 46;
    if (ambient.length > want) ambient.length = want;
    while (ambient.length < want) ambient.push(spawnMote(room, cam, reg, true));
    for (let i = ambient.length - 1; i >= 0; i--) {
      const m = ambient[i];
      m.life -= dt;
      if (m.kind === 'drip') { m.vy += 500 * dt; if (H.World.solidPx(m.x, m.y + 2) || H.World.isWater(m.x, m.y + 2)) { H.FX.spawn(m.x, m.y, 3, { color: 'rgba(160,200,215,0.7)', ang: -Math.PI / 2, spread: 1, smin: 15, smax: 45, grav: 300, lmin: 0.2, lmax: 0.4, size: 1 }); m.life = 0; } }
      else if (m.kind === 'bubble') { m.vy = -18; m.vx = Math.sin(t * 3 + m.p) * 6; if (!H.World.isWater(m.x, m.y)) m.life = 0; }
      else { m.vx += Math.sin(t * 0.7 + m.p) * 2 * dt; }
      m.x += m.vx * dt; m.y += m.vy * dt;
      const off = m.x < cam.x - 40 || m.x > cam.x + VW + 40 || m.y < cam.y - 40 || m.y > cam.y + VH + 40;
      if (m.life <= 0 || off) ambient[i] = spawnMote(room, cam, reg, false);
    }
  }
  function pickVisible(list, cam) {
    for (let i = 0; i < 4 && list.length; i++) {
      const q = H.pick(list), x = q[0] * TS, y = q[1] * TS;
      if (x > cam.x - TS && x < cam.x + VW && y > cam.y - TS && y < cam.y + VH) return q;
    }
    return null;
  }
  function spawnMote(room, cam, reg, initial) {
    const x = cam.x + Math.random() * VW, y = cam.y + Math.random() * VH;
    const r = Math.random();
    const ceil = (reg === 'wrackline' || reg === 'cloister' || reg === 'choir' || reg === 'gullet') && r < 0.12 && pickVisible(cache.ceilings, cam);
    if (ceil) {
      const [tx, ty] = ceil;
      return { kind: 'drip', x: tx * TS + 4 + Math.random() * 8, y: ty * TS + 1, vx: 0, vy: 0, life: 4, p: 0 };
    }
    if (H.World.isWater(x, y)) return { kind: 'bubble', x, y, vx: 0, vy: -18, life: 3 + Math.random() * 3, p: Math.random() * 6 };
    if (reg === 'saltworks' && r < 0.6) return { kind: 'salt', x, y: initial ? y : cam.y - 5, vx: 4, vy: 9 + Math.random() * 6, life: 12, p: Math.random() * 6 };
    return { kind: 'dust', x, y, vx: (Math.random() - 0.5) * 4, vy: (Math.random() - 0.5) * 3, life: 6 + Math.random() * 8, p: Math.random() * 6 };
  }
  function drawAmbient(c) {
    for (const m of ambient) {
      if (m.kind === 'drip') { c.fillStyle = 'rgba(170,210,225,0.8)'; c.fillRect(m.x, m.y, 1, 2.5); }
      else if (m.kind === 'bubble') { c.strokeStyle = 'rgba(190,225,240,0.5)'; c.lineWidth = 0.6; c.beginPath(); c.arc(m.x, m.y, 1.4, 0, Math.PI * 2); c.stroke(); }
      else if (m.kind === 'salt') { c.fillStyle = 'rgba(240,242,246,0.6)'; c.fillRect(m.x, m.y, 1, 1); }
      else { c.fillStyle = 'rgba(220,210,180,0.32)'; c.fillRect(m.x, m.y, 1, 1); }
    }
  }
  function drawRain(c) {
    if (!rain.length) return;
    c.strokeStyle = 'rgba(170,195,210,0.28)'; c.lineWidth = 1;
    c.beginPath();
    for (const d of rain) { c.moveTo(d.x, d.y); c.lineTo(d.x + d.l * 0.28, d.y - d.l); }
    c.stroke();
  }

  // ---- lighting ---------------------------------------------------------------------------------
  function lanternPolygon(p, range, spread) {
    const lp = p.lanternPos();
    const n = 30;
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

  // Draw only the part of a room-sized cache that is on screen.
  function blit(c, img, room, cam) {
    const sx = Math.max(0, Math.floor(cam.x - room.px) - 2), sy = Math.max(0, Math.floor(cam.y - room.py) - 2);
    const sw = Math.min(room.pw - sx, VW + 6), sh = Math.min(room.ph - sy, VH + 6);
    if (sw > 0 && sh > 0) c.drawImage(img, sx, sy, sw, sh, room.px + sx, room.py + sy, sw, sh);
  }

  function draw() {
    const G = H.Game;
    const room = G.room, cam = G.cam, p = G.player;
    const c = ctx;
    const t = performance.now() / 1000;
    const dt = G.frameDt || 1 / 60;
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.imageSmoothingEnabled = false;
    if (dirty || cache.room !== room) buildCaches(room);
    updateAtmosphere(room, cam, dt, t);
    drawBackground(c, room, cam, t);

    const sx = (Math.random() - 0.5) * G.shakeAmt, sy = (Math.random() - 0.5) * G.shakeAmt;
    c.save();
    c.translate(Math.round(-cam.x + sx), Math.round(-cam.y + sy));
    blit(c, cache.back, room, cam);
    drawTideMarks(c, room);

    for (const e of G.ents) if (!e.isBody && e.draw) e.draw(c);
    for (const s of G.sawlines) s.draw(c);
    for (const e of G.ents) if (e.isBody && !e.gone) e.draw(c);
    p.draw(c);
    for (const pr of G.projectiles) pr.draw(c);
    for (const s of G.enemyShots) { c.strokeStyle = '#a0b0b8'; c.lineWidth = 1.5; c.beginPath(); c.moveTo(s.x, s.y); c.lineTo(s.x - s.vx * 0.02, s.y - s.vy * 0.02); c.stroke(); }
    drawFire(c, t);
    drawAmbient(c);
    drawWater(c, room, cam, t);
    blit(c, cache.solid, room, cam);
    drawDynamicTiles(c, room, cam);
    for (const e of G.ents) if (e.isBody && !e.gone && e.drawKnots) e.drawKnots(c);
    for (const tr of G.tracers) { c.strokeStyle = `rgba(255,230,180,${tr.t * 10})`; c.lineWidth = 1; c.beginPath(); c.moveTo(tr.x1, tr.y1); c.lineTo(tr.x2, tr.y2); c.stroke(); }
    for (const q of H.FX.parts) { c.globalAlpha = Math.max(0, q.life / q.max); c.fillStyle = q.color; c.fillRect(q.x - q.size / 2, q.y - q.size / 2, q.size, q.size); }
    c.globalAlpha = 1;
    H.Narr.drawWorld(c);
    for (const n of G.noiseRings) { c.strokeStyle = `rgba(200,200,180,${n.t * 0.12})`; c.lineWidth = 1; c.beginPath(); c.arc(n.x, n.y, n.r * (1 - n.t), 0, Math.PI * 2); c.stroke(); }
    if (p.beamPath && p.beamPath.length > 1) {
      c.strokeStyle = 'rgba(255,240,190,0.25)'; c.lineWidth = 3;
      c.beginPath(); c.moveTo(p.beamPath[0].x, p.beamPath[0].y);
      for (const q of p.beamPath) c.lineTo(q.x, q.y);
      c.stroke();
    }
    c.restore();
    drawRain(c);

    drawLighting(room, cam, p, sx, sy, t);

    if (p.headUnder()) { c.fillStyle = room.brine ? 'rgba(120,140,120,0.18)' : 'rgba(20,60,80,0.25)'; c.fillRect(0, 0, VW, VH); }
    if (lightning.flash > 0 && room.outdoor) { c.fillStyle = `rgba(210,225,240,${lightning.flash * 0.18})`; c.fillRect(0, 0, VW, VH); }
    if (G.flash > 0) { c.fillStyle = `rgba(160,20,20,${G.flash})`; c.fillRect(0, 0, VW, VH); }
    drawPost(c, room, t);
    if (G.pressureWarn > 0) {
      const g = c.createRadialGradient(VW / 2, VH / 2, 80, VW / 2, VH / 2, 360);
      g.addColorStop(0, 'rgba(0,0,0,0)'); g.addColorStop(1, `rgba(0,10,20,${0.7 * G.pressureWarn})`);
      c.fillStyle = g; c.fillRect(0, 0, VW, VH);
    }
    if (G.mode === 'play') H.HUD.draw(c);
  }

  function drawFire(c, t) {
    for (const [, f] of H.Fire.cells) {
      const fx = f.tx * TS, fy = f.ty * TS;
      const fl = Math.sin(t * 20 + f.tx) * 2, fl2 = Math.sin(t * 27 + f.tx * 3) * 1.5;
      c.fillStyle = 'rgba(200,60,20,0.6)'; c.beginPath(); c.moveTo(fx, fy + TS); c.quadraticCurveTo(fx + 3, fy + 6, fx + 8, fy + fl); c.quadraticCurveTo(fx + 13, fy + 6, fx + 16, fy + TS); c.fill();
      c.fillStyle = 'rgba(255,140,40,0.8)'; c.beginPath(); c.moveTo(fx + 3, fy + TS); c.quadraticCurveTo(fx + 5, fy + 9, fx + 8, fy + 4 + fl2); c.quadraticCurveTo(fx + 11, fy + 9, fx + 13, fy + TS); c.fill();
      c.fillStyle = 'rgba(255,230,150,0.9)'; c.beginPath(); c.moveTo(fx + 6, fy + TS); c.lineTo(fx + 8, fy + 10 - fl); c.lineTo(fx + 10, fy + TS); c.fill();
    }
  }

  function drawLighting(room, cam, p, sx, sy, t) {
    const G = H.Game;
    let ambientDark = room.outdoor ? 0.5 : room.bellRoom ? 0.45 : room.region === 'gullet' ? 0.86 : 0.7;
    if (room.outdoor) ambientDark *= 1 - lightning.flash * 0.7;
    dctx.setTransform(1 / LS, 0, 0, 1 / LS, 0, 0);
    gctx.setTransform(1 / LS, 0, 0, 1 / LS, 0, 0);
    dctx.globalCompositeOperation = 'source-over';
    dctx.clearRect(0, 0, VW, VH);
    dctx.fillStyle = `rgba(2,4,6,${ambientDark})`;
    dctx.fillRect(0, 0, VW, VH);
    gctx.clearRect(0, 0, VW, VH);
    dctx.globalCompositeOperation = 'destination-out';
    const ox = -cam.x + sx, oy = -cam.y + sy;
    const light = (x, y, r, i) => {
      if (x + ox + r < 0 || x + ox - r > VW || y + oy + r < 0 || y + oy - r > VH) return;
      const g = dctx.createRadialGradient(x + ox, y + oy, 0, x + ox, y + oy, r);
      g.addColorStop(0, `rgba(0,0,0,${i})`); g.addColorStop(1, 'rgba(0,0,0,0)');
      dctx.fillStyle = g; dctx.fillRect(x + ox - r, y + oy - r, r * 2, r * 2);
    };
    // Maren is always faintly visible to the player.
    light(p.cx(), p.cy(), p.lanternLit() ? 46 : 24, p.lanternLit() ? 0.85 : 0.45);
    if (p.lanternLit() && !p.dead) {
      const flick = 1 + Math.sin(t * 17) * 0.02 + Math.sin(t * 41) * 0.012;
      const range = (p.focusing ? 260 : 160) * flick, spread = p.focusing ? 0.24 : 0.7;
      const poly = lanternPolygon(p, range, spread);
      const g = dctx.createRadialGradient(poly.lp.x + ox, poly.lp.y + oy, 4, poly.lp.x + ox, poly.lp.y + oy, range);
      g.addColorStop(0, 'rgba(0,0,0,1)'); g.addColorStop(0.6, 'rgba(0,0,0,0.75)'); g.addColorStop(1, 'rgba(0,0,0,0)');
      dctx.fillStyle = g;
      dctx.beginPath();
      poly.pts.forEach(([x, y], i) => (i ? dctx.lineTo(x + ox, y + oy) : dctx.moveTo(x + ox, y + oy)));
      dctx.closePath(); dctx.fill();
      const gg = gctx.createRadialGradient(poly.lp.x + ox, poly.lp.y + oy, 4, poly.lp.x + ox, poly.lp.y + oy, range);
      gg.addColorStop(0, 'rgba(255,200,120,0.2)'); gg.addColorStop(1, 'rgba(255,200,120,0)');
      gctx.fillStyle = gg;
      gctx.beginPath(); poly.pts.forEach(([x, y], i) => (i ? gctx.lineTo(x + ox, y + oy) : gctx.moveTo(x + ox, y + oy))); gctx.closePath(); gctx.fill();
    }
    for (const l of G.frameLights) {
      if (l.player) continue;
      light(l.x, l.y, l.r, l.i);
      if (l.x + ox + l.r < 0 || l.x + ox - l.r > VW || l.y + oy + l.r < 0 || l.y + oy - l.r > VH) continue;
      const g = gctx.createRadialGradient(l.x + ox, l.y + oy, 0, l.x + ox, l.y + oy, l.r);
      g.addColorStop(0, `rgba(${l.color || '255,200,120'},0.18)`); g.addColorStop(1, `rgba(${l.color || '255,200,120'},0)`);
      gctx.fillStyle = g; gctx.fillRect(l.x + ox - l.r, l.y + oy - l.r, l.r * 2, l.r * 2);
    }
    for (const [, f] of H.Fire.cells) light(f.tx * TS + 8, f.ty * TS + 8, 60, 0.8);
    // light shafts falling through stained glass
    if (!room.outdoor && cache.windows.length) {
      for (const [tx, ty] of cache.windows) {
        const x = tx * TS + ox, y = ty * TS + oy;
        if (x < -160 || x > VW + 40 || y < -200 || y > VH + 20) continue;
        const hcol = ['255,160,140', '150,180,255', '255,225,150', '150,240,200'][Math.floor(hash(tx, ty) * 4)];
        const a = 0.09 + 0.03 * Math.sin(t * 0.6 + tx);
        const g = gctx.createLinearGradient(x, y, x + 50, y + 150);
        g.addColorStop(0, `rgba(${hcol},${a})`); g.addColorStop(1, `rgba(${hcol},0)`);
        gctx.fillStyle = g;
        gctx.beginPath(); gctx.moveTo(x + 2, y); gctx.lineTo(x + 14, y); gctx.lineTo(x + 70, y + 160); gctx.lineTo(x + 40, y + 160); gctx.closePath(); gctx.fill();
        dctx.fillStyle = 'rgba(0,0,0,0.12)';
        dctx.beginPath(); dctx.moveTo(x + 2, y); dctx.lineTo(x + 14, y); dctx.lineTo(x + 70, y + 160); dctx.lineTo(x + 40, y + 160); dctx.closePath(); dctx.fill();
      }
    }
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(dark, 0, 0, VW, VH);
    ctx.globalCompositeOperation = 'lighter';
    ctx.drawImage(glow, 0, 0, VW, VH);
    ctx.globalCompositeOperation = 'source-over';
    ctx.imageSmoothingEnabled = false;
  }

  // Vignette with a touch of the region's colour, and a little film grain.
  function drawPost(c, room, t) {
    const key = room.outdoor ? 'outdoor' : room.region;
    if (overlayKey !== key) {
      overlayKey = key;
      const grade = room.outdoor ? '40,60,80' : PAL[room.region].grade;
      overlays = grain.map((gr) => {
        const o = mk(VW, VH), oc = o.getContext('2d');
        oc.globalAlpha = 0.025; oc.drawImage(gr, 0, 0, VW, VH); oc.globalAlpha = 1;
        const g = oc.createRadialGradient(VW / 2, VH / 2, VH * 0.35, VW / 2, VH / 2, VW * 0.62);
        g.addColorStop(0, `rgba(${grade},0)`); g.addColorStop(0.6, `rgba(${grade},0.05)`); g.addColorStop(1, 'rgba(0,0,0,0.6)');
        oc.fillStyle = g; oc.fillRect(0, 0, VW, VH);
        return o;
      });
    }
    c.drawImage(overlays[Math.floor(t * 12) % overlays.length], 0, 0);
  }

  return { init, draw, markDirty, knotVisibility, drawTile, get ctx() { return ctx; } };
})();
