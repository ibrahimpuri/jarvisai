// Text-only storytelling: Maren's thoughts, whispers of the drowned, carvings, dreams, fates.
'use strict';
H.Narr = (() => {
  const TS = H.TILE;
  const L = () => H.LORE;
  const G = () => H.Game;

  function fresh() { return { thoughts: {}, regions: {}, dreamIndex: 0, read: {}, lore: {}, heard: {} }; }
  function st() { return G().narr || (G().narr = fresh()); }

  // ---- thoughts: one quiet italic line, each shown once ----------------------------------
  let thought = null;
  function think(id) {
    const s = st();
    if (s.thoughts[id] || !L().thoughts[id]) return;
    s.thoughts[id] = true;
    thought = { text: L().thoughts[id], t: 5, max: 5 };
  }

  // ---- region epigraphs ----------------------------------------------------------------------
  let epigraph = null;
  function enterRegion(region) {
    const s = st();
    if (s.regions[region]) return false;
    s.regions[region] = true;
    if (L().epigraphs[region]) epigraph = { text: L().epigraphs[region], t: 6, max: 6 };
    return true;
  }

  // ---- whispers: hold the focused beam on a still body to hear it ----------------------------
  const whispers = [];
  let focusBody = null, focusT = 0;
  const heardThisVisit = new Set();
  function onRoomEnter() { heardThisVisit.clear(); whispers.length = 0; focusBody = null; }
  function whisperFor(b) {
    const W = L().whispers;
    if (b.key && W.named[b.key]) return W.named[b.key];
    if (b.sailor && W.named.sailor) return W.named.sailor;
    if (b.teodor && W.named.teodor) return W.named.teodor;
    const pool = b.corpse ? W.corpse : W[b.type] || W.shucker;
    // Stable per body so the same corpse always says the same thing.
    let h = 0; for (const ch of (b.key || '')) h = (h * 31 + ch.charCodeAt(0)) | 0;
    return pool[Math.abs(h) % pool.length];
  }
  function updateWhispers(dt) {
    const p = G().player;
    for (const w of whispers) { w.t -= dt; w.y -= dt * 4; }
    for (let i = whispers.length - 1; i >= 0; i--) if (whispers[i].t <= 0) whispers.splice(i, 1);
    if (!p || !p.focusing) { focusBody = null; focusT = 0; return; }
    let target = null;
    for (const e of G().ents) {
      if (!e.isBody || e.gone || e.boss || e.type === 'driftling' || e.type === 'undertow') continue;
      const still = e.corpse || e.mode === 'dormant' || e.mode === 'downed' || e.mode === 'captured' || (e.type === 'gullet' && e.mode === 'hang');
      if (!still || heardThisVisit.has(e)) continue;
      if (H.dist(p.cx(), p.cy(), e.cx(), e.cy()) > 200) continue;
      if (p.beamHits(e.cx(), e.cy())) { target = e; break; }
    }
    if (target !== focusBody) { focusBody = target; focusT = 0; }
    if (!target) return;
    focusT += dt;
    if (focusT > 0.7) {
      heardThisVisit.add(target);
      st().heard[target.key || ''] = true;
      whispers.push({ x: target.cx(), y: target.y - 6, text: whisperFor(target), t: 5.5, max: 5.5 });
      focusBody = null;
      if (!G().tips.whisper) G().tip('whisper', 'Hold the focused beam on the still and the drowned, and you can hear what they are thinking.', 5);
    }
  }

  // ---- carvings ---------------------------------------------------------------------------------
  // Place each carving on the nearest dry floor cell to its hint, clear of other entities.
  function carvingSpecs(room, taken) {
    const out = [];
    const list = (L().carvings[room.id] || []);
    list.forEach((c, i) => {
      const spot = findFloor(room, c.tx, c.ty, taken);
      if (!spot) return;
      taken.add(spot[0] + ',' + spot[1]);
      out.push({ type: 'carving', tx: spot[0], ty: spot[1], id: 'carving_' + room.id + '_' + i, title: c.title, text: c.text });
    });
    return out;
  }
  function findFloor(room, hx, hy, taken) {
    const W = H.World;
    let best = null, bd = 1e9;
    for (let dy = -6; dy <= 6; dy++) for (let dx = -8; dx <= 8; dx++) {
      const lx = hx + dx, ly = hy + dy;
      if (lx < 1 || ly < 1 || lx >= room.tw - 1 || ly >= room.th - 1) continue;
      const tx = room.ox + lx, ty = room.oy + ly;
      if (W.solidity(W.charAt(tx, ty)) !== 0 || W.solidity(W.charAt(tx, ty - 1)) !== 0) continue;
      if (W.isLadder(tx, ty)) continue;
      const below = W.charAt(tx, ty + 1);
      if (W.solidity(below) !== 1) continue;
      if (taken.has(lx + ',' + ly) || taken.has((lx - 1) + ',' + ly) || taken.has((lx + 1) + ',' + ly)) continue;
      // keep clear of the low-tide water line
      if (room.row === 4 && !room.fixedWater && (ty + 0.5) > H.Tide.SEA_LOW) continue;
      const d = Math.abs(dx) + Math.abs(dy) * 1.5;
      if (d < bd) { bd = d; best = [lx, ly]; }
    }
    return best;
  }
  function markRead(id) { st().read[id] = true; }
  function isRead(id) { return !!st().read[id]; }

  // ---- dreams, deaths, lore, fates --------------------------------------------------------------
  function nextDream() {
    const s = st();
    const d = L().dreams[s.dreamIndex % L().dreams.length];
    s.dreamIndex++;
    return d;
  }
  function deathText() {
    const D = L().death;
    return { title: H.pick(D.titles), line: H.pick(D.lines) };
  }
  function npcLore(id) {
    const lines = L().npcLore[id];
    if (!lines) return null;
    const s = st();
    const i = s.lore[id] || 0;
    s.lore[id] = i + 1;
    return lines[i % lines.length];
  }
  function fates(kind) {
    const g = G(), q = g.quests, F = L().fates, flags = H.World.flags;
    const out = [];
    if (kind === 'relight') out.push(q.tables === 'sold' ? F.ansel.sold : F.ansel.burned);
    else if (kind === 'hymn') out.push(q.tables === 'sold' ? F.ansel.sold : F.ansel.sunk);
    else out.push(q.tables === 'sold' ? F.ansel.sold : F.ansel.alive);
    out.push(q.odileLost ? F.odile.lost : F.odile.alive);
    if (q.teodor === 'saved') out.push(F.teodor.saved);
    else if (['killed', 'tags', 'dead'].includes(q.teodor)) out.push(F.teodor.dead);
    if (q.hesperDead || flags.choirDrained) out.push(F.hesper.dead);
    else out.push(kind === 'relight' ? F.hesper.burned : kind === 'hymn' ? F.hesper.sunk : F.hesper.alive);
    out.push(F.agathe[kind]);
    out.push(kind === 'relight' && g.player.grafts.size >= 2 ? F.maren.relightGrafted : F.maren[kind]);
    return out;
  }

  // ---- drawing ------------------------------------------------------------------------------------
  function wrap(c, text, maxW) {
    const words = text.split(' '); const lines = []; let line = '';
    for (const w of words) { const t = line ? line + ' ' + w : w; if (c.measureText(t).width > maxW && line) { lines.push(line); line = w; } else line = t; }
    if (line) lines.push(line);
    return lines;
  }
  function drawWorld(c) {
    c.font = 'italic 7px Georgia, serif';
    c.textAlign = 'center';
    for (const w of whispers) {
      const a = Math.min(1, (w.max - w.t) * 2, w.t / 1.5);
      const lines = wrap(c, w.text, 150);
      lines.forEach((ln, i) => {
        c.fillStyle = `rgba(0,0,0,${0.5 * a})`; c.fillText(ln, w.x + 0.6, w.y - (lines.length - 1 - i) * 9 + 0.6);
        c.fillStyle = `rgba(200,225,235,${0.85 * a})`; c.fillText(ln, w.x, w.y - (lines.length - 1 - i) * 9);
      });
    }
    c.textAlign = 'left';
  }
  function drawHUD(c, dt) {
    if (thought) {
      thought.t -= dt;
      const a = Math.min(1, (thought.max - thought.t) * 1.5, thought.t);
      c.font = 'italic 9px Georgia, serif'; c.textAlign = 'center';
      c.fillStyle = `rgba(0,0,0,${0.55 * a})`; c.fillText(thought.text, H.VIEW_W / 2 + 0.7, H.VIEW_H - 63.3);
      c.fillStyle = `rgba(236,226,200,${0.92 * a})`; c.fillText(thought.text, H.VIEW_W / 2, H.VIEW_H - 64);
      c.textAlign = 'left';
      if (thought.t <= 0) thought = null;
    }
  }
  function drawEpigraph(c, dt, y) {
    if (!epigraph) return;
    epigraph.t -= dt;
    const a = Math.min(1, (epigraph.max - epigraph.t - 0.8) * 1.2, epigraph.t / 1.2);
    if (a > 0) {
      c.font = 'italic 9px Georgia, serif'; c.textAlign = 'center';
      c.fillStyle = `rgba(200,190,165,${a})`; c.fillText(epigraph.text, H.VIEW_W / 2, y);
      c.textAlign = 'left';
    }
    if (epigraph.t <= 0) epigraph = null;
  }
  function epigraphActive() { return !!epigraph; }
  function reset() { thought = null; epigraph = null; whispers.length = 0; heardThisVisit.clear(); }

  return { fresh, think, enterRegion, onRoomEnter, updateWhispers, carvingSpecs, markRead, isRead, nextDream, deathText, npcLore, fates, drawWorld, drawHUD, drawEpigraph, epigraphActive, reset };
})();
