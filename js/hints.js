// Hints in the manner of Rain World: nothing is explained up front. A faint glyph and a word or two
// rise over Maren only when something is relevant and the player has stalled for a while, and a
// hint is retired for good the first time its action is done, whether or not it was ever shown.
'use strict';
H.Hints = (() => {
  const G = () => H.Game;
  const P = () => H.Game.player;
  const zones = new Set();          // zones Maren stands in this frame
  const waited = {};
  let shown = null, alpha = 0;
  let walked = 0, sneakT = 0, focusT = 0, sinceTurn = -1, sinceWeapon = -1, sinceBoots = -1, sinceJet = -1;
  let weaponsSeen = 1, hadBoots = false, hadJet = false;

  const st = () => { const n = G().narr || (G().narr = H.Narr.fresh()); n.hints = n.hints || {}; n.did = n.did || {}; return n; };
  const bodies = (r) => G().ents.filter((b) => b.isBody && !b.gone && H.dist(b.cx(), b.cy(), P().cx(), P().cy()) < r);
  const alive = (b) => !b.dead && !b.gone && b.mode !== 'downed' && b.mode !== 'captured';
  const hostile = (b) => alive(b) && !b.corpse && !b.passive && b.mode !== 'dormant' && b.knots && b.knots.some((k) => !k.broken);
  function ringAbove() {
    const p = P(), tx = Math.floor(p.cx() / H.TILE), ty = Math.floor(p.y / H.TILE);
    for (let dy = 0; dy <= 5; dy++) for (let dx = -4; dx <= 4; dx++) if (H.World.charAt(tx + dx, ty - dy) === 'o') return true;
    return false;
  }
  const crownBare = () => bodies(40).some((b) => alive(b) && b.knots && b.knots.some((k) => k.type === 'crown' && !k.broken && !b.isSealed(k)));
  const cur = () => P().weapons[P().cur];
  const ammo = () => H.WEAPONS[P().cur].ammo;

  // id, the action that retires it, seconds of hesitation before it shows, when it is relevant
  const DEFS = [
    ['move', 'move', 5, () => true],
    ['jump', 'jump', 2.5, () => zones.has('jump')],
    ['swim', 'swimout', 3, () => P().inWater()],
    ['ladder', 'climb', 2.5, () => P().state !== 'ladder' && P().onGround && (P().onLadderTile() || P().ladderBelow())],
    ['walljump', 'walljump', 5, () => zones.has('walljump')],
    ['drop', 'drop', 3, () => zones.has('drop') && P().onOneWay()],
    ['shoot', 'barrel', 7, () => zones.has('shoot') && G().ents.some((e) => e.type === 'barrel' && !e.done)],
    ['dash', 'dash', 3.5, () => zones.has('dash')],
    ['gaff', 'gaffknot', 1.5, crownBare],
    ['fight', 'knot', 3, () => bodies(220).some(hostile)],
    ['reload', 'reload', 1.5, () => cur().mag <= 0 && P().bag.count(ammo()) > 0 && P().reloadT <= 0],
    ['craft', 'craft', 2, () => P().cur === 'driver' && cur().mag <= 0 && P().bag.count('rivets') <= 0 && P().bag.count('scrap') > 0],
    ['heal', 'poultice', 4, () => P().hp < P().maxHp && P().bag.count('poultice') > 0 && P().healT <= 0 && !bodies(240).some(hostile)],
    ['swing', 'swing', 3, () => P().has('swing') && P().state !== 'swing' && ringAbove()],
    ['focus', 'focus', 4, () => P().lanternLit() && bodies(150).some((b) => b.corpse || b.sailor)],
    ['shutter', 'shutter', 2, () => !P().shut && bodies(200).some((b) => b.type === 'bellmouth' && alive(b))],
    ['sneak', 'sneak', 2, () => !H.Input.held('sneak') && Math.abs(P().vx) > 100 && bodies(130).some((b) => alive(b) && b.mode === 'dormant')],
    ['boots', 'boots', 0, () => sinceBoots >= 0 && sinceBoots < 10],
    ['jet', 'jet', 0, () => sinceJet >= 0 && sinceJet < 10 && (P().inWater() || !P().onGround)],
    ['weapon', 'weapon', 0, () => sinceWeapon >= 0 && sinceWeapon < 8],
    ['map', 'map', 0, () => sinceTurn >= 0 && sinceTurn < 7],
  ];

  function reset() {
    zones.clear(); for (const k in waited) delete waited[k];
    shown = null; alpha = 0;
    walked = sneakT = focusT = 0; sinceTurn = sinceWeapon = sinceBoots = sinceJet = -1;
    const p = P();
    weaponsSeen = p ? p.weaponOrder.length : 1; hadBoots = !!(p && p.has('boots')); hadJet = !!(p && p.has('jet'));
  }
  // Something Maren did; retires every hint that waits on it.
  function did(action) {
    if (!G().narr) return;
    const s = st();
    s.did[action] = true;
    for (const [id, by] of DEFS) if (by === action) s.hints[id] = true;
  }
  const hasDone = (action) => !!(G().narr && st().did[action]);
  const inZone = (id) => zones.add(id);
  function cut() { if (!st().hints.dash) waited.dash = 99; }
  function tideTurned() { if (!st().hints.map) sinceTurn = 0; }

  function update(dt) {
    const p = P();
    if (!p || p.dead || !G().narr) { zones.clear(); return; }
    if (p.onGround && Math.abs(p.vx) > 20) { walked += Math.abs(p.vx) * dt; if (walked > 40) did('move'); }
    if (H.Input.held('sneak') && Math.abs(p.vx) > 20) { sneakT += dt; if (sneakT > 1) did('sneak'); }
    if (p.focusing) { focusT += dt; if (focusT > 0.5) did('focus'); }
    if (p.weaponOrder.length > weaponsSeen) { weaponsSeen = p.weaponOrder.length; sinceWeapon = 0; }
    if (!hadBoots && p.has('boots')) { hadBoots = true; sinceBoots = 0; }
    if (!hadJet && p.has('jet')) { hadJet = true; sinceJet = 0; }
    if (sinceTurn >= 0) sinceTurn += dt;
    if (sinceWeapon >= 0) sinceWeapon += dt;
    if (sinceBoots >= 0) sinceBoots += dt;
    if (sinceJet >= 0) sinceJet += dt;

    let want = null;
    const done = st().hints;
    if (H.Settings.hints && !H.UI.isOpen()) {
      for (const [id, , delay, relevant] of DEFS) {
        if (done[id]) { delete waited[id]; continue; }
        let rel = false;
        try { rel = relevant(); } catch (e) { rel = false; }
        let w = waited[id] || 0;
        w = rel ? w + dt : Math.max(0, w - dt * 2);
        waited[id] = w;
        if (rel && w >= delay && !want) want = id;
      }
    }
    zones.clear();
    if (want !== shown) { alpha -= dt * 2.5; if (alpha <= 0) { alpha = 0; shown = want; } }
    else if (shown) alpha = Math.min(1, alpha + dt * 1.6);
  }

  function draw(c) {
    if (!shown || alpha <= 0.01) return;
    const text = H.LORE.hints[shown];
    if (!text) return;
    const p = P(), cam = G().cam;
    c.save();
    c.font = 'italic 7px Georgia, serif';
    const w = H.Glyphs.measure(c, text);
    let x = p.cx() - cam.x, y = p.y - cam.y - 13 - Math.sin(performance.now() / 500) * 1.2;
    x = H.clamp(x, w / 2 + 6, H.VIEW_W - w / 2 - 6);
    if (y < 70) y = p.y + p.h - cam.y + 16;   // no room above her head: show it at her feet
    const g = c.createRadialGradient(x, y - 3, 0, x, y - 3, w * 0.75 + 10);
    g.addColorStop(0, `rgba(0,0,0,${0.55 * alpha})`); g.addColorStop(1, 'rgba(0,0,0,0)');
    c.fillStyle = g; c.fillRect(x - w - 14, y - 22, w * 2 + 28, 38);
    c.textAlign = 'center';
    c.fillStyle = `rgba(235,224,199,${0.9 * alpha})`;
    H.Glyphs.drawText(c, text, x, y, alpha);
    c.restore();
  }

  return { reset, did, hasDone, inZone, cut, tideTurned, update, draw, get shown() { return alpha > 0.05 ? shown : null; } };
})();
