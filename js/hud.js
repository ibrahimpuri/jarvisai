// HUD drawn on the canvas, and the logbook map.
'use strict';
H.HUD = (() => {
  const PH_COL = { low: '#38464e', rising: '#3f7a86', high: '#24456e', ebbing: '#5a7462' };

  function pips(c, p) {
    for (let i = 0; i < p.maxHp; i++) {
      const x = 14 + i * 13, y = 14;
      c.save(); c.translate(x, y); c.rotate(Math.PI / 4);
      c.fillStyle = i < p.hp ? '#d8c08a' : 'rgba(80,70,60,0.6)';
      c.fillRect(-4, -4, 8, 8);
      c.strokeStyle = 'rgba(0,0,0,0.6)'; c.strokeRect(-4, -4, 8, 8);
      c.restore();
    }
  }

  function breath(c, p) {
    const max = p.breathMax();
    if (!isFinite(max)) return;
    if (!p.headUnder() && p.breath >= max - 0.1) return;
    // Shown as bubbles rising from the helmet rather than a bar.
    const n = Math.ceil(p.breath / 3);
    const total = Math.ceil(max / 3);
    for (let i = 0; i < total; i++) {
      c.fillStyle = i < n ? 'rgba(180,220,255,0.85)' : 'rgba(80,100,120,0.35)';
      c.beginPath(); c.arc(12 + i * 8, 31, 3, 0, Math.PI * 2); c.fill();
    }
  }

  function lantern(c, p) {
    const y = p.breath < p.breathMax() - 0.1 || p.headUnder() ? 42 : 30;
    c.fillStyle = p.shut ? '#555' : '#ffd27a';
    c.fillRect(10, y, 5, 7);
    c.fillStyle = 'rgba(60,50,40,0.7)'; c.fillRect(18, y + 2, 50, 3);
    c.fillStyle = p.focusing ? '#fff0c0' : '#c9a040';
    c.fillRect(18, y + 2, 50 * (p.oil / p.oilMax()), 3);
    c.fillStyle = '#aaa'; c.font = '7px monospace';
    c.fillText('x' + p.bag.count('oil'), 72, y + 6);
    if (p.shut) { c.fillStyle = '#888'; c.fillText('SHUT', 88, y + 6); }
  }

  function dial(c) {
    const T = H.Tide;
    const cx = H.VIEW_W - 30, cy = 30, r = 20;
    const total = T.total();
    let a0 = -Math.PI / 2;
    for (let i = 0; i < 4; i++) {
      const a1 = a0 + (T.len(i) / total) * Math.PI * 2;
      c.fillStyle = PH_COL[T.PHASES[i].id];
      c.beginPath(); c.moveTo(cx, cy); c.arc(cx, cy, r, a0, a1); c.closePath(); c.fill();
      a0 = a1;
    }
    c.strokeStyle = '#c0a060'; c.lineWidth = 1.5; c.beginPath(); c.arc(cx, cy, r, 0, Math.PI * 2); c.stroke();
    const a = -Math.PI / 2 + (T.t / total) * Math.PI * 2;
    c.strokeStyle = '#f0e0b0'; c.lineWidth = 2;
    c.beginPath(); c.moveTo(cx, cy); c.lineTo(cx + Math.cos(a) * (r - 2), cy + Math.sin(a) * (r - 2)); c.stroke();
    c.fillStyle = '#c0a060'; c.beginPath(); c.arc(cx, cy, 2.5, 0, Math.PI * 2); c.fill();
    c.font = '8px monospace'; c.textAlign = 'right';
    c.fillStyle = '#e0d4b0';
    c.fillText(T.phaseName().toUpperCase(), cx - r - 6, cy - 2);
    c.fillStyle = '#9a9080';
    c.fillText(H.fmtTime(T.phaseRemaining()), cx - r - 6, cy + 8);
    if (T.spring) { c.fillStyle = '#a06060'; c.fillText('SPRING TIDES', cx - r - 6, cy + 18); }
    c.textAlign = 'left';
  }

  function weapon(c, p) {
    const w = p.weapons[p.cur], def = H.WEAPONS[p.cur];
    const x = H.VIEW_W - 130, y = H.VIEW_H - 30;
    c.fillStyle = 'rgba(10,12,14,0.55)'; c.fillRect(x - 6, y - 12, 132, 38);
    c.font = '8px monospace'; c.fillStyle = '#d8c8a0';
    c.fillText(def.name + (w.tier ? ' ' + 'I'.repeat(w.tier + 1) : ''), x, y - 2);
    c.font = '11px monospace'; c.fillStyle = '#f0e0c0';
    c.fillText(w.mag + ' / ' + p.bag.count(def.ammo), x, y + 11);
    c.font = '7px monospace'; c.fillStyle = '#888';
    c.fillText(p.weaponOrder.map((id, i) => (id === p.cur ? '[' + (i + 1) + ']' : ' ' + (i + 1) + ' ')).join(''), x + 64, y + 11);
    c.fillStyle = '#7fbf7f'; c.fillText('Poultice ' + p.bag.count('poultice') + '  Stitch ' + p.bag.count('stitchkit') + '  Scrap ' + p.bag.count('scrap'), x, y + 22);
  }

  function prompt(c) {
    const G = H.Game;
    const text = G.promptText;
    if (!text) return;
    c.font = '8px monospace';
    const w = c.measureText(text).width + 14;
    const x = (H.VIEW_W - w) / 2, y = H.VIEW_H - 46;
    c.fillStyle = 'rgba(10,12,14,0.7)'; c.fillRect(x, y, w, 15);
    c.fillStyle = '#e8dcb8'; c.fillText(text, x + 7, y + 10);
  }

  function toasts(c, dt) {
    c.font = '8px monospace'; c.textAlign = 'center';
    let y = 60;
    for (const t of H.toasts) {
      const a = Math.min(1, t.t, (t.max - t.t) * 4 + 0.2);
      const w = Math.min(H.VIEW_W - 40, c.measureText(t.text).width + 16);
      c.fillStyle = `rgba(10,12,14,${0.65 * a})`; c.fillRect(H.VIEW_W / 2 - w / 2, y - 9, w, 13);
      c.fillStyle = `rgba(232,220,184,${a})`; c.fillText(t.text, H.VIEW_W / 2, y);
      y += 15;
    }
    c.textAlign = 'left';
  }

  function roomTitle(c) {
    const G = H.Game;
    if (G.titleT <= 0) return;
    const a = Math.min(1, G.titleT);
    c.textAlign = 'center';
    c.font = '14px Georgia, serif'; c.fillStyle = `rgba(232,220,184,${a})`;
    c.fillText(G.room.name, H.VIEW_W / 2, 110);
    c.font = '8px monospace'; c.fillStyle = `rgba(160,150,130,${a})`;
    c.fillText(H.REGIONS[G.room.region].name.toUpperCase() + (G.room.bellRoom ? '  ·  BELL ROOM' : ''), H.VIEW_W / 2, 124);
    c.textAlign = 'left';
  }

  function bossBar(c) {
    const b = H.Game.bossBar;
    if (!b || b.gone || b.dead) return;
    const names = { abbot: 'THE ABBOT OF SALT', undertow: 'THE UNDERTOW', shoal: 'THE SHOAL', grafted: 'HESPER VANE, THE CANTOR', shucker: 'TEODOR' };
    const total = b.knots.length, left = b.knots.filter((k) => !k.broken).length;
    const x = 120, y = H.VIEW_H - 16, w = H.VIEW_W - 240;
    c.font = '8px monospace'; c.textAlign = 'center'; c.fillStyle = '#d8c8a0';
    c.fillText(names[b.type] || '', H.VIEW_W / 2, y - 4);
    c.textAlign = 'left';
    for (let i = 0; i < total; i++) {
      c.fillStyle = i < left ? 'rgba(200,240,255,0.85)' : 'rgba(60,70,80,0.6)';
      c.fillRect(x + i * (w / total) + 1, y, w / total - 2, 4);
    }
  }

  function grabbed(c, p) {
    if (!p.grabbedBy) return;
    c.font = '12px monospace'; c.textAlign = 'center'; c.fillStyle = '#ff9a7a';
    c.fillText('BREAK FREE  [F / E / SPACE]  x' + p.mash, H.VIEW_W / 2, H.VIEW_H / 2 + 50);
    c.textAlign = 'left';
  }

  function crosshair(c) {
    const m = H.Input.mouse;
    c.strokeStyle = 'rgba(240,230,200,0.7)'; c.lineWidth = 1;
    c.beginPath(); c.moveTo(m.x - 4, m.y); c.lineTo(m.x - 1.5, m.y); c.moveTo(m.x + 1.5, m.y); c.lineTo(m.x + 4, m.y);
    c.moveTo(m.x, m.y - 4); c.lineTo(m.x, m.y - 1.5); c.moveTo(m.x, m.y + 1.5); c.lineTo(m.x, m.y + 4); c.stroke();
  }

  function draw(c) {
    const p = H.Game.player;
    // Earlier passes leave stroke state behind (the background arches use a 10 px line).
    c.lineWidth = 1; c.lineCap = 'butt'; c.globalAlpha = 1; c.textAlign = 'left';
    pips(c, p); breath(c, p); lantern(c, p);
    dial(c); weapon(c, p); prompt(c); toasts(c); roomTitle(c); bossBar(c); grabbed(c, p);
    if (H.Game.god) { c.fillStyle = '#f66'; c.font = '7px monospace'; c.fillText('DEBUG', 6, H.VIEW_H - 4); }
    crosshair(c);
  }

  // ---- logbook map --------------------------------------------------------------------
  function drawMap(cv) {
    const c = cv.getContext('2d');
    const G = H.Game;
    const W = cv.width, Hh = cv.height;
    c.fillStyle = '#1a1712'; c.fillRect(0, 0, W, Hh);
    c.fillStyle = '#2a251c'; c.fillRect(8, 8, W - 16, Hh - 16);
    const cols = 8, rows = 6;
    const sw = Math.floor((W - 40) / cols), sh = Math.floor((Hh - 60) / rows);
    const ox = 20, oy = 36;
    c.font = '11px Georgia, serif'; c.fillStyle = '#d8c8a0';
    c.fillText("Maren's logbook — the island", 20, 24);
    for (const r of H.ROOMS) {
      const st = H.World.states[r.id];
      if (!st || !st.visited) continue;
      const x = ox + r.col * sw, y = oy + r.row * sh, w = r.w * sw - 3, h = r.h * sh - 3;
      c.fillStyle = H.REGIONS[r.region].color; c.globalAlpha = 0.55; c.fillRect(x, y, w, h); c.globalAlpha = 1;
      c.strokeStyle = r === G.room ? '#ffe0a0' : '#5a5040'; c.lineWidth = r === G.room ? 2 : 1; c.strokeRect(x, y, w, h);
      // water lines only for tide phases Maren has actually seen here
      const lineFor = (sea) => y + ((sea - r.oy) / r.th) * h;
      if (r.row === 4 && !r.fixedWater) {
        if (st.seen.low) { c.strokeStyle = '#7ab0c0'; c.beginPath(); c.moveTo(x, lineFor(H.Tide.SEA_LOW)); c.lineTo(x + w, lineFor(H.Tide.SEA_LOW)); c.stroke(); }
        if (st.seen.high) { c.strokeStyle = '#3a70b0'; c.beginPath(); c.moveTo(x, lineFor(H.Tide.SEA_HIGH)); c.lineTo(x + w, lineFor(H.Tide.SEA_HIGH)); c.stroke(); }
        if (st.seen.ebbing) { c.fillStyle = '#9ac0a0'; c.font = '8px monospace'; c.fillText('E', x + w - 9, y + 10); }
        const sounded = st.seen.low && st.seen.high && st.seen.ebbing;
        if (sounded) { c.fillStyle = '#d8c8a0'; c.font = '8px monospace'; c.fillText('✓', x + 3, y + 10); }
      }
      if (r.bellRoom) { c.fillStyle = G.bellsLit[(G.bellIdFor(r.id))] ? '#ffd27a' : '#6a5a3a'; c.beginPath(); c.arc(x + w / 2, y + h / 2, 4, 0, Math.PI * 2); c.fill(); }
      if (G.undertow && G.undertow.ripples[r.id]) { c.strokeStyle = 'rgba(160,200,230,0.6)'; for (let i = 1; i <= 2; i++) { c.beginPath(); c.arc(x + w * 0.3, y + h * 0.75, i * 4, 0, Math.PI * 2); c.stroke(); } }
      if (r.wrackpool || H.ROOM[r.id].ents && Object.values(r.ents).some((e) => e.type === 'wrackpool')) { c.fillStyle = '#5a8aa0'; c.font = '8px monospace'; c.fillText('W', x + 3, y + h - 4); }
    }
    // Salt Ear: every Brine body within two rooms
    if (G.player.grafts.has('ear')) {
      for (const r of H.ROOMS) {
        const st = H.World.states[r.id];
        if (Math.abs(r.col - G.room.col) + Math.abs(r.row - G.room.row) > 2) continue;
        const n = st ? Object.values(st.ents).filter((e) => e && e.knots && e.knots.length && !e.gone && e.mode !== 'downed').length : 0;
        const x = ox + r.col * sw, y = oy + r.row * sh;
        for (let i = 0; i < Math.min(n, 8); i++) { c.fillStyle = '#ff8080'; c.fillRect(x + 6 + i * 5, y + sh / 2, 3, 3); }
      }
    }
    if (G.satchelDrop) {
      const r = H.ROOM[G.satchelDrop.room];
      const x = ox + r.col * sw + ((G.satchelDrop.x - r.px) / r.pw) * r.w * sw, y = oy + r.row * sh + ((G.satchelDrop.y - r.py) / r.ph) * r.h * sh;
      c.fillStyle = '#e0a040'; c.fillRect(x - 3, y - 3, 6, 6);
    }
    // Maren
    const p = G.player, r = G.room;
    const px = ox + r.col * sw + ((p.cx() - r.px) / r.pw) * r.w * sw, py = oy + r.row * sh + ((p.cy() - r.py) / r.ph) * r.h * sh;
    c.fillStyle = '#fff0c0'; c.beginPath(); c.arc(px, py, 3, 0, Math.PI * 2); c.fill();
    c.font = '9px monospace'; c.fillStyle = '#9a9080';
    c.fillText('Water lines appear for tide phases you have witnessed. ✓ = fully sounded. W = Wrack Pool. Ripples = the Undertow passed.', 20, Hh - 14);
  }

  return { draw, drawMap };
})();
