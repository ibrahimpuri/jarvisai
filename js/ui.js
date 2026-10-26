// DOM menus layered over the canvas. The game pauses while any menu is open.
'use strict';
H.UI = (() => {
  let root = null;
  let stack = [];       // close callbacks
  let dlg = null;       // active dialogue {lines, i, done}
  // Present when running inside the desktop app (see desktop/preload.js).
  const desktop = window.hollowmereDesktop || null;
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  const G = () => H.Game;
  const P = () => H.Game.player;

  function init() {
    root = document.getElementById('ui');
    window.addEventListener('keydown', (e) => {
      if (!isOpen()) return;
      if (dlg && (e.code === 'KeyE' || e.code === 'Space' || e.code === 'Enter')) { e.preventDefault(); advance(); return; }
      if (cine) {
        e.preventDefault();
        if (e.code === 'Escape') finishCine();
        else if (e.code === 'KeyE' || e.code === 'Space' || e.code === 'Enter') nextCine();
        return;
      }
      if (root.dataset.kind === 'inscription' && (e.code === 'KeyE' || e.code === 'Space' || e.code === 'Enter' || e.code === 'Escape')) { e.preventDefault(); close(); return; }
      if (root.dataset.kind === 'satchel' && (e.code === 'Tab' || e.code === 'KeyM' || e.code === 'KeyI')) {
        // Tab / M and I open the journal at the map and the satchel; pressed again, they close it.
        e.preventDefault();
        const want = e.code === 'KeyI' ? 'satchel' : 'map';
        if (satTab === want) close(); else satchel(want);
        return;
      }
      if (e.code === 'Escape') {
        e.preventDefault();
        if (root.dataset.kind === 'title' || root.dataset.kind === 'ending' || root.dataset.kind === 'death') return;
        back();
      }
    });
  }
  function isOpen() { return !root.classList.contains('hidden'); }
  let backAction = null;   // what cancel (Esc, or B on a pad) does on this screen
  function show(html, kind, clear) {
    root.innerHTML = html;
    root.dataset.kind = kind || '';
    root.classList.remove('hidden');
    root.classList.toggle('clear', !!clear);
    backAction = null;
    H.Input.clear();
    root.querySelectorAll('[data-act]').forEach((b) => b.addEventListener('click', () => { H.Audio.ui(); actions[b.dataset.act] && actions[b.dataset.act](b.dataset); }));
    refreshGlyphs();
    if (H.Input.pad.using) focusFirst();
  }
  function back() { if (backAction) backAction(); else close(); }

  // ---- button prompts in menus -------------------------------------------------------------------
  // [data-rich] holds text with {action} tokens; [data-foot] a row of [action, label] prompts. Both
  // are redrawn for whatever the player is holding, and again whenever that changes.
  let glyphStyle = null;
  function refreshGlyphs() {
    glyphStyle = H.Glyphs.current();
    root.querySelectorAll('[data-rich]').forEach((el) => { el.innerHTML = H.Glyphs.html(el.dataset.rich); });
    root.querySelectorAll('[data-foot]').forEach((el) => {
      const items = JSON.parse(el.dataset.foot).filter(([a]) => H.Glyphs.controls(a, glyphStyle).length);
      el.innerHTML = items.map(([a, l]) => `<span>${H.Glyphs.html('{' + a + '}')} ${esc(l)}</span>`).join('');
    });
  }
  const rich = (text, tag = 'span', cls = '') => `<${tag} class="${cls}" data-rich="${esc(text)}"></${tag}>`;
  const foot = (...items) => `<div class="foot" data-foot="${esc(JSON.stringify(items))}"></div>`;

  // ---- controller navigation -------------------------------------------------------------------
  const focusables = () => [...root.querySelectorAll('button:not([disabled])')].filter((b) => b.offsetParent !== null);
  const focused = () => root.querySelector('button.padfocus');
  function setFocus(b) {
    root.querySelectorAll('.padfocus').forEach((x) => x.classList.remove('padfocus'));
    if (b) { b.classList.add('padfocus'); b.scrollIntoView({ block: 'nearest' }); }
  }
  function focusFirst() { const l = focusables(); setFocus(l.find((b) => b.classList.contains('primary')) || l[0]); }
  function moveFocus(dx, dy) {
    const list = focusables();
    const cur = focused();
    if (!cur || !list.includes(cur)) { focusFirst(); return; }
    const a = cur.getBoundingClientRect(), ax = a.left + a.width / 2, ay = a.top + a.height / 2;
    let best = null, bs = 1e9;
    for (const b of list) {
      if (b === cur) continue;
      const r = b.getBoundingClientRect(), ddx = r.left + r.width / 2 - ax, ddy = r.top + r.height / 2 - ay;
      const along = dx ? ddx * dx : ddy * dy;
      if (along <= 4) continue;
      const score = along + (dx ? Math.abs(ddy) : Math.abs(ddx)) * 2.5;
      if (score < bs) { bs = score; best = b; }
    }
    if (best) setFocus(best);
  }
  function turnPage(d) {
    const tabs = [...root.querySelectorAll('.tabs button')];
    if (!tabs.length) return;
    const i = Math.max(0, tabs.findIndex((b) => b.classList.contains('sel')));
    tabs[(i + d + tabs.length) % tabs.length].click();
  }
  let repeatT = 0, heldDir = null;
  function padUpdate(dt) {
    const In = H.Input, B = In.B;
    if (H.Glyphs.current() !== glyphStyle) refreshGlyphs();
    if (!In.pad.using) { if (focused()) setFocus(null); return; }
    const kind = root.dataset.kind;
    const ok = In.confirmButton(), no = ok === B.A ? B.B : B.A;
    if (In.padHit(ok)) {
      if (dlg) advance(); else if (cine) nextCine(); else if (kind === 'inscription') close();
      else { const f = focused(); if (f) f.click(); else focusFirst(); }
      return;
    }
    if (In.padHit(no)) {
      if (cine) finishCine(); else if (dlg) advance(); else if (kind === 'inscription') close();
      else if (kind !== 'title' && kind !== 'ending' && kind !== 'death') back();
      return;
    }
    if (In.padHit(B.START)) { if (cine) finishCine(); else if (['pause', 'satchel', 'controls', 'controller'].includes(kind)) close(); return; }
    if (In.padHit(B.BACK) && kind === 'satchel') { close(); return; }
    if (In.padHit(B.TOUCH) && kind === 'satchel') { if (satTab === 'map') close(); else satchel('map'); return; }
    if (In.padHit(B.LB)) { if (kind === 'controls') controls(controlsBack, controlsTab === 'pad' ? 'keys' : 'pad'); else turnPage(-1); return; }
    if (In.padHit(B.RB)) { if (kind === 'controls') controls(controlsBack, controlsTab === 'pad' ? 'keys' : 'pad'); else turnPage(1); return; }
    // d-pad or left stick moves the focus, repeating while held
    const s = In.pad.stick;
    let dir = In.padHit(B.UP) ? 'u' : In.padHit(B.DOWN) ? 'd' : In.padHit(B.LEFT) ? 'l' : In.padHit(B.RIGHT) ? 'r' : null;
    const sd = s.y < -0.6 ? 'u' : s.y > 0.6 ? 'd' : s.x < -0.6 ? 'l' : s.x > 0.6 ? 'r' : null;
    if (!dir && sd) {
      if (sd !== heldDir) { dir = sd; repeatT = 0.35; } else { repeatT -= dt; if (repeatT <= 0) { dir = sd; repeatT = 0.12; } }
    }
    heldDir = sd;
    if (dir) moveFocus(dir === 'l' ? -1 : dir === 'r' ? 1 : 0, dir === 'u' ? -1 : dir === 'd' ? 1 : 0);
  }
  function close() {
    root.classList.add('hidden'); root.innerHTML = ''; root.dataset.kind = ''; dlg = null;
    if (cine) { clearTimeout(cine.timer); const d = cine.done; cine = null; if (d) setTimeout(d, 0); }
    H.Input.clear();
    const cb = stack.pop(); stack = []; if (cb) cb();
  }
  const actions = {};

  // ---- dialogue ------------------------------------------------------------------------
  function dialogue(who, lines, done) {
    dlg = { who, lines, i: 0, done };
    renderDlg();
  }
  function say(who, text, done) { dialogue(who, [text], done); }
  function renderDlg() {
    const d = dlg;
    show(`<div class="panel dialogue">${d.who ? `<div class="who">${esc(d.who)}</div>` : ''}${rich(d.lines[d.i], 'div', 'line')}
      ${foot(['confirm', d.i < d.lines.length - 1 ? 'continue' : 'close'])}</div>`, 'dialogue', true);
    root.querySelector('.panel').addEventListener('click', advance);
  }
  function advance() {
    if (!dlg) return;
    dlg.i++;
    if (dlg.i >= dlg.lines.length) { const done = dlg.done; dlg = null; close(); if (done) done(); return; }
    renderDlg();
  }
  function confirm(text, onYes) {
    actions.yes = () => { close(); onYes(); };
    actions.no = () => close();
    show(`<div class="panel narrow"><p>${esc(text)}</p><div class="btns center"><button data-act="yes" class="primary">Yes</button><button data-act="no">No</button></div>${foot(['confirm', 'choose'], ['cancel', 'no'])}</div>`, 'confirm');
  }

  // ---- title -------------------------------------------------------------------------------
  let opts = { difficulty: 'keeper', tide: 1 };
  function title() {
    const hasSave = H.Game.hasSave();
    const d = opts.difficulty, t = opts.tide;
    actions.diff = (ds) => { opts.difficulty = ds.v; title(); };
    actions.tide = (ds) => { opts.tide = parseFloat(ds.v); title(); };
    actions.newgame = () => { close(); H.Game.newGame(opts); };
    actions.cont = () => { close(); H.Game.loadGame(); };
    actions.controls = () => controls(title);
    actions.pad = () => controller(title);
    actions.fullscreen = () => desktop && desktop.toggleFullscreen();
    actions.exit = () => desktop && desktop.quit();
    show(`<div class="panel narrow">
      <h1>HOLLOWMERE</h1>
      <div class="sub">The tide redraws the map every ten minutes, and brings the dead back with it.</div>
      <h3>Difficulty</h3>
      <div class="opt">
        <button data-act="diff" data-v="pilgrim" class="${d === 'pilgrim' ? 'sel' : ''}">Pilgrim</button>
        <button data-act="diff" data-v="keeper" class="${d === 'keeper' ? 'sel' : ''}">Keeper</button>
        <button data-act="diff" data-v="drowned" class="${d === 'drowned' ? 'sel' : ''}">Drowned</button>
      </div>
      <p class="small muted">${{ pilgrim: 'More scrap and a slower tide.', keeper: 'As designed.', drowned: 'Saving costs 1 tallow, fewer lifeline drops, harder hits.' }[d]}</p>
      <h3>Tide length</h3>
      <div class="opt">
        ${[1, 1.5, 2].map((v) => `<button data-act="tide" data-v="${v}" class="${t === v ? 'sel' : ''}">${v * 100}%</button>`).join('')}
      </div>
      <p class="small muted">More time between tides, without softening combat.</p>
      <div class="btns col">
        <button data-act="newgame" class="primary">Row out to the lighthouse</button>
        ${hasSave ? '<button data-act="cont">Continue from the last Bell Room</button>' : ''}
        <button data-act="controls">Controls</button>
        <button data-act="pad">Controller settings</button>
        ${desktop ? '<button data-act="fullscreen">Toggle fullscreen (F11)</button><button data-act="exit">Quit to desktop</button>' : ''}
      </div>
      <p class="small muted" style="text-align:center;margin-top:14px">Keyboard and mouse, or an Xbox, PlayStation or Switch controller. Best with headphones.${desktop ? ' F11 or Alt+Enter toggles fullscreen.' : ''}</p>
      <p class="small muted" style="text-align:center">${esc(padLine())}</p>
    </div>`, 'title');
  }

  let controlsBack = null, controlsTab = 'keys';
  function padLine() {
    const pad = H.Input.pad;
    if (pad.index < 0) return 'No controller connected.';
    return `Controller: ${pad.name.split('(')[0].trim()} (${{ playstation: 'PlayStation', switch: 'Switch', xbox: 'Xbox' }[pad.kind]} layout).`;
  }
  function controls(back, tab) {
    controlsBack = back;
    controlsTab = tab || (H.Input.pad.using ? 'pad' : 'keys');
    actions.back = () => (back ? back() : close());
    actions.ctab = (ds) => controls(back, ds.t);
    actions.pad = () => controller(() => controls(back, controlsTab));
    let table;
    if (controlsTab === 'keys') {
      const k = [
        ['A / D', 'Move'], ['W / S', 'Climb ladders, swim, shorten / lengthen swing rope'], ['Space', 'Jump (down + Space drops through planks)'],
        ['Shift (hold)', 'Sneak: footsteps 2 m instead of 6 m'], ['C', 'Dash (0.2 s invulnerable). Jet Valve: air and water bursts in any direction'],
        ['Mouse', 'Aim'], ['Left click / J', 'Fire the selected weapon'], ['Right click / K (hold)', 'Focused beam: makes knots glint, double oil'],
        ['F', 'Gaff strike (melee, pry plates, kick barrels)'], ['Q (hold)', 'Gaff Swing from an iron ring / hook-pull a light enemy'],
        ['E', 'Interact, talk, drag a downed body'], ['X', 'Burn a downed body with lantern oil'], ['R', 'Reload (locks you in place)'],
        ['T', 'Shutter the lantern (nearly invisible, blind to weak points)'], ['Z', 'Poultice (2 pips over 3 s)'], ['G', 'Stitch Kit (full heal, kneel 4 s)'],
        ['V', 'Quick-craft 3 rivets from 1 scrap (2 s, crouched)'], ['B', 'Ballast Boots on / off'], ['1-4 / wheel', 'Select weapon'],
        ['I', 'Satchel, crafting, logbook'], ['Tab / M', 'Map'], ['Esc / P', 'Pause'],
      ];
      table = `<table class="keys">${k.map(([a, b]) => `<tr><td><kbd>${a}</kbd></td><td>${b}</td></tr>`).join('')}</table>`;
    } else {
      const rows = [
        ['{move}', 'Move, climb ladders, swim. A light tilt creeps quietly'], ['{sneak}', 'Sneak on / off'],
        ['{drop}', 'Drop through planks'], ['{aim}', 'Aim. With the stick left alone, Maren aims where she walks'],
        ['{jump}', 'Jump'], ['{dash}', 'Dash (0.2 s invulnerable)'], ['{gaff}', 'Gaff strike: melee, pry plates, kick barrels'],
        ['{interact}', 'Interact, talk, drag a downed body'], ['{burn}', 'Burn a downed body with lantern oil'],
        ['{swing}', 'Gaff Swing from an iron ring / hook-pull a light enemy'],
        ['{fire}', 'Fire the selected weapon'], ['{focus}', 'Focused beam: makes knots glint, double oil'],
        ['{reload}', 'Reload'], ['{quickcraft}', 'Quick-craft 3 rivets from 1 scrap'],
        ['{poultice}', 'Poultice'], ['{stitch}', 'Stitch Kit'], ['{boots}', 'Ballast Boots on / off'],
        ['{weapon}', 'Change weapon'], ['{shutter}', 'Shutter the lantern'],
        ['{journal}', 'Map, satchel and logbook; the bumpers turn the page'], ['{pause}', 'Pause'],
      ];
      table = `<p class="small muted">${esc(padLine())} Prompts follow the controller in your hands.</p><table class="keys pad">${rows.map(([a, b]) => `<tr><td>${rich(a)}</td><td>${b}</td></tr>`).join('')}</table>`;
    }
    show(`<div class="panel narrow"><h2>Controls</h2>
      <div class="tabs"><button data-act="ctab" data-t="keys" class="${controlsTab === 'keys' ? 'sel' : ''}">Keyboard and mouse</button><button data-act="ctab" data-t="pad" class="${controlsTab === 'pad' ? 'sel' : ''}">Controller</button></div>
      ${table}
      <h3>How Hollowmere works</h3>
      <p class="small">Every Brine body is held up by salt knots at its joints. Rivets on flesh barely scratch; hit the knots. The Crown knot in the chest is sealed until two limb knots break. A dropped body lying in water when the next Rising tide reaches it will stand again at High tide: haul it above the tide line or burn it.</p>
      <p class="small">The tide dial (top right) runs a 10-minute cycle: Low, Rising, High, Ebbing. Painted tide marks on pillars show where the water will reach. Bell Rooms are safe; resting there advances the tide to the next Low.</p>
      <div class="btns center"><button data-act="back">Back</button><button data-act="pad">Controller settings</button></div>${foot(['tabs', 'switch list'], ['cancel', 'back'])}</div>`, 'controls');
    backAction = actions.back;
  }

  function controller(back) {
    const S = H.Settings;
    const again = () => { S.save(); controller(back); };
    actions.back = () => (back ? back() : close());
    actions.vib = (ds) => { S.vibration = parseFloat(ds.v); again(); H.Input.rumble(0.5 * S.vibration, 0.5 * S.vibration, 0.25); };
    actions.assist = () => { S.aimAssist = !S.aimAssist; again(); };
    actions.prompts = (ds) => { S.prompts = ds.v; again(); };
    actions.nconfirm = (ds) => { S.nintendoConfirm = ds.v === '1'; again(); };
    const sel = (c) => (c ? 'sel' : '');
    show(`<div class="panel narrow"><h2>Controller</h2><p class="small muted">${esc(padLine())}</p>
      <h3>Vibration</h3><div class="opt">${[[0, 'Off'], [0.5, 'Gentle'], [1, 'Full']].map(([v, n]) => `<button data-act="vib" data-v="${v}" class="${sel(S.vibration === v)}">${n}</button>`).join('')}</div>
      <h3>Aim</h3><div class="opt"><button data-act="assist" class="${sel(S.aimAssist)}">Aim assist: ${S.aimAssist ? 'on' : 'off'}</button></div>
      <p class="small muted">Gyro aim and the PlayStation light bar are in the Godot edition of the game.</p>
      <h3>Button prompts</h3><div class="opt">${[['auto', 'Match controller'], ['xbox', 'Xbox'], ['playstation', 'PlayStation'], ['switch', 'Switch']].map(([v, n]) => `<button data-act="prompts" data-v="${v}" class="${sel(S.prompts === v)}">${n}</button>`).join('')}</div>
      <h3>Switch controllers: confirm in menus with</h3><div class="opt"><button data-act="nconfirm" data-v="1" class="${sel(S.nintendoConfirm)}">A (right button)</button><button data-act="nconfirm" data-v="0" class="${sel(!S.nintendoConfirm)}">B (bottom button)</button></div>
      <div class="btns center"><button data-act="back">Back</button></div>${foot(['confirm', 'choose'], ['cancel', 'back'])}</div>`, 'controller');
    backAction = actions.back;
  }

  function pause(note) {
    actions.resume = () => close();
    actions.controls = () => controls(() => pause());
    actions.pad = () => controller(() => pause());
    actions.hints = () => { H.Settings.hints = !H.Settings.hints; H.Settings.save(); pause(); };
    actions.quit = () => { close(); H.Game.toTitle(); };
    actions.fullscreen = () => desktop && desktop.toggleFullscreen();
    actions.exit = () => desktop && desktop.quit();
    actions.vol = (ds) => { H.Audio.setVolume(parseFloat(ds.v)); G().volume = parseFloat(ds.v); H.Settings.volume = G().volume; H.Settings.save(); pause(); };
    const v = G().volume;
    show(`<div class="panel narrow"><h2>Paused</h2>${note ? `<p class="bad">${esc(note)}</p>` : ''}
      <p class="muted small">${esc(G().room.name)} · ${esc(H.REGIONS[G().room.region].name)} · Act ${'I'.repeat(G().act)}</p>
      <h3>Volume</h3><div class="opt">${[0, 0.35, 0.7, 1].map((x) => `<button data-act="vol" data-v="${x}" class="${v === x ? 'sel' : ''}">${x === 0 ? 'Off' : Math.round(x * 100) + '%'}</button>`).join('')}</div>
      <h3>Hints</h3><div class="opt"><button data-act="hints" class="${H.Settings.hints ? 'sel' : ''}">${H.Settings.hints ? 'Hints: on' : 'Hints: off'}</button></div>
      <div class="btns col"><button data-act="resume" class="primary">Resume</button><button data-act="controls">Controls</button><button data-act="pad">Controller settings</button>${desktop ? '<button data-act="fullscreen">Toggle fullscreen (F11)</button>' : ''}<button data-act="quit">Quit to title (progress since the last save is lost)</button>${desktop ? '<button data-act="exit">Quit to desktop (progress since the last save is lost)</button>' : ''}</div>${foot(['confirm', 'choose'], ['cancel', 'resume'])}</div>`, 'pause');
  }

  // ---- Bell Room ------------------------------------------------------------------------------
  function bellMenu(bell) {
    const g = G(), p = P();
    actions.save = () => {
      if (g.difficulty === 'drowned') { if (p.bag.count('tallow') < 1) { H.toast('Saving on Drowned costs 1 tallow.'); H.Audio.deny(); return; } p.bag.remove('tallow', 1); }
      g.setRespawn(bell); g.save(); H.toast('Saved.'); bellMenu(bell);
    };
    actions.rest = () => {
      if (g.difficulty === 'drowned') { if (p.bag.count('tallow') < 1) { H.toast('Saving on Drowned costs 1 tallow.'); H.Audio.deny(); return; } p.bag.remove('tallow', 1); }
      close(); g.rest(bell);
    };
    actions.rel = () => reliquary(() => bellMenu(bell));
    actions.bench = () => bench(bell.room.id === 'chapel', () => bellMenu(bell));
    actions.leave = () => close();
    const lung = p.grafts.has('lung');
    show(`<div class="panel narrow"><h2>${esc(bell.spec.name)} Bell Room</h2>
      <p class="small muted">No Brine body will ever enter while the bell rings. Tide: <b>${H.Tide.phaseName()}</b>, ${H.fmtTime(H.Tide.phaseRemaining())} left.</p>
      <div class="btns col">
        <button data-act="save">Save${g.difficulty === 'drowned' ? ' (1 tallow)' : ''}</button>
        <button data-act="rest" class="primary">Rest: full health${lung ? ' (halved: Tide Lung)' : ''}, save, and wait for the next Low tide</button>
        <button data-act="rel">Reliquary chest (shared by every Bell Room)</button>
        <button data-act="bench">Bench: craft, upgrades, fittings${bell.room.id === 'chapel' ? ', surgery' : ''}</button>
        <button data-act="leave">Leave</button>
      </div>
      <p class="small muted">Resting skips time. Every body left lying in water re-knots on the way.</p>${foot(['confirm', 'choose'], ['cancel', 'leave'])}</div>`, 'bell');
  }

  function reliquary(back) {
    const p = P(), chest = G().reliquary;
    actions.put = (ds) => { const n = Math.min(p.bag.count(ds.i), ds.all ? 999 : 1); const moved = n - chest.add(ds.i, n); p.bag.remove(ds.i, moved); reliquary(back); };
    actions.take = (ds) => { const n = Math.min(chest.count(ds.i), ds.all ? 999 : 1); const left = p.bag.add(ds.i, n); chest.remove(ds.i, n - left); if (left) H.toast('Satchel full.'); reliquary(back); };
    actions.back = () => (back ? back() : close());
    const rows = (bag, act) => bag.list().map(([i, n]) => `<div class="row"><span>${esc(H.ITEMS[i].name)} <span class="muted">x${n}</span></span>
      <span><button data-act="${act}" data-i="${i}">1</button> <button data-act="${act}" data-i="${i}" data-all="1">All</button></span></div>`).join('') || '<p class="muted small">Empty.</p>';
    show(`<div class="panel"><h2>Reliquary</h2><div class="cols">
      <div><h3>Satchel (${p.bag.used()}/${p.bag.size} slots)</h3>${rows(p.bag, 'put')}</div>
      <div><h3>Reliquary chest</h3>${rows(chest, 'take')}</div></div>
      <div class="btns"><button data-act="back">Back</button></div>${foot(['confirm', 'move'], ['cancel', 'back'])}</div>`, 'reliquary');
    backAction = actions.back;
  }

  function recipeRows(where) {
    const p = P();
    return H.RECIPES.filter((r) => where === 'bench' || r.where === 'any').map((r) => {
      const ok = p.bag.has(r.cost) && p.bag.room(r.makes) >= r.n;
      const cost = Object.entries(r.cost).map(([k, v]) => `${v} ${H.ITEMS[k].name.toLowerCase()} <span class="muted">(${p.bag.count(k)})</span>`).join(', ');
      return `<div class="row"><span>${r.n} ${esc(H.ITEMS[r.makes].name)}<div class="desc">${cost} · ${r.where === 'any' ? 'anywhere, ' + r.time + ' s crouched' : 'bench, instant'}</div></span>
        <button data-act="craft" data-id="${r.id}" ${ok ? '' : 'disabled'}>Craft</button></div>`;
    }).join('');
  }

  let benchTab = 'craft';
  function bench(chapel, back) {
    const p = P(), g = G();
    actions.tab = (ds) => { benchTab = ds.t; bench(chapel, back); };
    actions.back = () => (back ? back() : close());
    actions.craft = (ds) => {
      const r = H.RECIPES.find((x) => x.id === ds.id);
      if (!p.bag.has(r.cost)) return;
      p.bag.pay(r.cost); p.bag.add(r.makes, r.n); H.Audio.craft(); bench(chapel, back);
    };
    actions.upgrade = (ds) => {
      const w = p.weapons[ds.w]; const cost = H.UPGRADE_COST[w.tier + 1];
      if (!cost || !p.bag.has(cost)) return;
      p.bag.pay(cost); w.tier++; H.Audio.craft(); H.toast(H.WEAPONS[ds.w].name + ': ' + H.WEAPONS[ds.w].tiers[w.tier].split(':')[0]); bench(chapel, back);
    };
    actions.fit = (ds) => {
      const f = ds.f;
      if (p.fittings.has(f)) p.fittings.delete(f);
      else {
        const used = [...p.fittings].reduce((s, k) => s + H.FITTINGS[k].p, 0);
        if (used + H.FITTINGS[f].p > p.pressure) { H.toast('Not enough Pressure Rating for that fitting.'); H.Audio.deny(); return; }
        p.fittings.add(f);
      }
      if (p.oil > p.oilMax()) p.oil = p.oilMax();
      const w = p.weapons.driver; if (w.mag > p.magSize('driver')) { p.bag.add('rivets', w.mag - p.magSize('driver')); w.mag = p.magSize('driver'); }
      bench(chapel, back);
    };
    actions.cut = (ds) => {
      confirm(`Cut out the ${H.GRAFTS[ds.g].name}? You lose 1 health pip permanently. Ansel can do this only once.`, () => {
        p.grafts.delete(ds.g); p.maxHp = Math.max(3, p.maxHp - 1); p.hp = Math.min(p.hp, p.maxHp); g.quests.surgeryUsed = true;
        H.Audio.hurt(); H.toast('The graft is cut out. You will carry the scar.');
        bench(chapel, back);
      });
    };
    const tabs = ['craft', 'upgrade', 'fittings'].concat(chapel ? ['surgery'] : []);
    let body = '';
    if (benchTab === 'craft') body = recipeRows('bench');
    if (benchTab === 'upgrade') {
      body = p.weaponOrder.map((id) => {
        const w = p.weapons[id], def = H.WEAPONS[id], next = H.UPGRADE_COST[w.tier + 1];
        const cost = next ? Object.entries(next).map(([k, v]) => `${v} ${H.ITEMS[k].name.toLowerCase()} (${p.bag.count(k)})`).join(', ') : '';
        return `<div class="row"><span>${def.name} <span class="muted">tier ${w.tier + 1}</span><div class="desc">${next ? 'Next: ' + esc(def.tiers[w.tier + 1]) + ' · ' + cost : 'Fully upgraded.'}</div></span>
          ${next ? `<button data-act="upgrade" data-w="${id}" ${p.bag.has(next) ? '' : 'disabled'}>Upgrade</button>` : ''}</div>`;
      }).join('') + '<p class="small muted">Tiers add behaviours, not just damage. Mere-ivory comes from bosses and hidden vaults.</p>';
    }
    if (benchTab === 'fittings') {
      const used = [...p.fittings].reduce((s, k) => s + H.FITTINGS[k].p, 0);
      body = `<p class="small">Pressure Rating: <b>${used} / ${p.pressure}</b>. Fittings are swapped only at Bell Rooms, so a loadout is a commitment for the next push.</p>` +
        ([...p.fittingsOwned].map((f) => { const d = H.FITTINGS[f]; const on = p.fittings.has(f); return `<div class="row"><span>${d.name} <span class="muted">(${d.p})</span><div class="desc">${d.effect}. Trade-off: ${d.trade}.</div></span><button data-act="fit" data-f="${f}" class="${on ? 'sel' : ''}">${on ? 'Equipped' : 'Equip'}</button></div>`; }).join('') || '<p class="muted small">You have not found any fittings yet. Search containers.</p>') +
        '<p class="small muted">Diver: Cork Lining, Muffled Soles, Tallow Reservoir. Rigger: Rivet Feeder, Bilge Pump. Breaker: Knot Splitter, Lead Plates.</p>';
    }
    if (benchTab === 'surgery') {
      body = g.quests.surgeryUsed ? '<p>Ansel has already used his one good scalpel on you.</p>' :
        ([...p.grafts].map((k) => `<div class="row"><span>${H.GRAFTS[k].name}<div class="desc">${H.GRAFTS[k].power}. Cost: ${H.GRAFTS[k].cost}.</div></span><button data-act="cut" data-g="${k}">Cut out (-1 pip)</button></div>`).join('') || '<p class="muted">You carry no grafts.</p>');
    }
    show(`<div class="panel"><h2>Bench</h2><div class="tabs">${tabs.map((t) => `<button data-act="tab" data-t="${t}" class="${t === benchTab ? 'sel' : ''}">${t[0].toUpperCase() + t.slice(1)}</button>`).join('')}</div>
      ${body}<div class="btns"><button data-act="back">Back</button></div>${foot(['tabs', 'change page'], ['cancel', 'back'])}</div>`, 'bench');
    backAction = actions.back;
  }

  // ---- satchel / logbook ----------------------------------------------------------------------
  let satTab = 'satchel';
  function satchel(tab) {
    const p = P(), g = G();
    if (tab) satTab = tab;
    actions.tab = (ds) => satchel(ds.t);
    actions.close = () => close();
    actions.craft = (ds) => { close(); p.quickCraft(ds.id); };
    actions.drop = (ds) => { const n = p.bag.count(ds.i); p.bag.remove(ds.i, n); g.spawnLoose(ds.i, p.cx(), p.y + p.h - 4, n); satchel(); };
    actions.read = (ds) => { const isPage = ds.k === 'p'; say(isPage ? "Agathe's log" : "Prior's tide table", (isPage ? H.Story.PAGES : H.Story.TABLES)[ds.n], () => satchel()); };
    const tabs = [['map', 'Map'], ['satchel', 'Satchel'], ['craft', 'Craft'], ['gear', 'Gear'], ['log', 'Logbook']];
    let body = '';
    if (satTab === 'map') { body = '<canvas class="map" width="760" height="470"></canvas>'; H.Hints.did('map'); }
    if (satTab === 'satchel') {
      const items = p.bag.list();
      const slots = [];
      for (const [i, n] of items) {
        const def = H.ITEMS[i];
        const s = H.Satchel.slotsFor(i, n);
        let left = n;
        for (let k = 0; k < s; k++) {
          const here = def.key ? n : Math.min(def.perSlot, left); left -= here;
          slots.push(`<div class="slot" title="${esc(def.desc)}">${esc(def.name)}${def.key ? ' <span class="muted">(key)</span>' : ''}<span class="n">${def.key ? '' : here}</span>${k === 0 ? `<div><button data-act="drop" data-i="${i}" style="padding:1px 6px;font-size:11px;margin-top:4px">Drop</button></div>` : ''}</div>`);
        }
      }
      while (slots.length < p.bag.size) slots.push('<div class="slot empty">—</div>');
      body = `<p class="small muted">${p.bag.used()} / ${p.bag.size} slots. Rivets 18 per slot, bolts 6, flasks and poultices 3, raw materials 10. Key items take 2 slots.</p><div class="grid">${slots.join('')}</div>`;
    }
    if (satTab === 'craft') body = '<p class="small muted">Simple recipes work anywhere but take time with Maren crouched and exposed. Complex ones need a Bell Room bench.</p>' + recipeRows('any');
    if (satTab === 'gear') {
      const ab = [...p.abilities].map((a) => `<div class="row"><span>${H.ABILITIES[a].name}${rich(H.ABILITIES[a].desc, 'div', 'desc')}</span></div>`).join('');
      const ws = p.weaponOrder.map((id) => `<div class="row"><span>${H.WEAPONS[id].name} <span class="muted">tier ${p.weapons[id].tier + 1}</span><div class="desc">${esc(H.WEAPONS[id].tiers[p.weapons[id].tier])}</div></span></div>`).join('');
      const fs = [...p.fittings].map((f) => `<div class="row"><span>${H.FITTINGS[f].name}<div class="desc">${H.FITTINGS[f].effect} / ${H.FITTINGS[f].trade}</div></span></div>`).join('') || '<p class="muted small">None equipped.</p>';
      const gr = [...p.grafts].map((k) => `<div class="row"><span>${H.GRAFTS[k].name} <span class="muted">(${H.GRAFTS[k].knot} knot)</span><div class="desc">${H.GRAFTS[k].power}. ${H.GRAFTS[k].cost}.</div></span></div>`).join('') || '<p class="muted small">None. The Choir offers them.</p>';
      body = `<div class="cols"><div><h3>Health ${p.hp}/${p.maxHp} · Pressure ${p.pressure}</h3>${ab}<h3>Weapons</h3>${ws}</div><div><h3>Fittings</h3>${fs}<h3>Brine Grafts</h3>${gr}</div></div>`;
    }
    if (satTab === 'log') {
      const q = g.quests;
      const obj = [];
      if (!W().flags.seawallOpen) obj.push('Fetch the Sluice Crank from the Tithe Hall altar and fit it to the seawall gate.');
      else if (g.act === 1) obj.push('Cross the Saltworks and face the Abbot of Salt.');
      else if (!p.has('jet')) obj.push('Find a way through the Lower Cloister. The Prior\'s door answers to the hymnal.');
      else if (!p.has('tidekey')) obj.push('Climb to the Sunken Choir.');
      else if (!p.has('seal')) obj.push('Use the Tide Key to reach the Ribvault above the Choir.');
      else if (!W().flags.skullOpen) obj.push('Weight the lift in the Prior\'s Walk and dive the Gullet. The Undertow waits there.');
      else obj.push('Climb from the Ribvault Bell Room to the Lantern Skull.');
      if (q.teodor === 'asked') obj.push('Odile: find Teodor in the Lower Cloister. Bring his tags, or break every limb knot but the Crown and bind him.');
      if (g.tables.size && q.tables !== 'given' && q.tables !== 'sold') obj.push(`Prior's tide tables: ${g.tables.size}/4. Ansel or the Choir's bowl.`);
      body = `<h3>Objectives</h3>${obj.map((o) => `<div class="row"><span>${esc(o)}</span></div>`).join('')}
        <h3>Agathe's log (${g.pages.size}/9)</h3><div class="btns">${[...g.pages].sort().map((n) => `<button data-act="read" data-k="p" data-n="${n}">Page ${n}</button>`).join('') || '<span class="muted small">No pages yet.</span>'}</div>
        <h3>Prior's tide tables (${g.tables.size}/4)</h3><div class="btns">${[...g.tables].sort().map((n) => `<button data-act="read" data-k="t" data-n="${n}">Table ${n}</button>`).join('') || '<span class="muted small">No tables yet.</span>'}</div>
        <p class="small muted">Stitches: ${Object.keys(g.collected).filter((k) => k.startsWith('stitch')).length}/4 · Brass Seals: ${Object.keys(g.collected).filter((k) => k.startsWith('seal')).length}/6 · Knots broken: ${Object.values(g.stats.knots).reduce((a, b) => a + b, 0)}</p>`;
    }
    show(`<div class="panel"><div class="tabs">${tabs.map(([t, n]) => `<button data-act="tab" data-t="${t}" class="${t === satTab ? 'sel' : ''}">${n}</button>`).join('')}</div>${body}
      <div class="btns"><button data-act="close">Close</button></div>${foot(['tabs', 'change page'], ['cancel', 'close'])}</div>`, 'satchel');
    if (satTab === 'map') H.HUD.drawMap(root.querySelector('canvas.map'));
  }
  const W = () => H.World;

  function map() { satchel('map'); }

  // ---- Choir menus ---------------------------------------------------------------------------
  function sluiceMenu() {
    const f = W().flags;
    actions.mid = () => { G().setChoirLevel('mid'); close(); };
    actions.raise = () => { G().setChoirLevel('raised'); close(); };
    actions.drain = () => confirm('Drain the Sunken Choir for good? Hesper wants the shrine kept flooded for the Saint\'s hymn. The Choir will turn on you.', () => G().setChoirLevel('drained'));
    actions.close = () => close();
    if (f.choirDrained) { say(null, 'The shrine is drained. The sluice is jammed open with silt; it will never hold water again.'); return; }
    show(`<div class="panel narrow"><h2>Choir sluice</h2><p class="small">The Tide Key turns in the wave-shaped lock. The Choir's water does not answer the moon; it answers the key.</p>
      <div class="btns col">
        <button data-act="raise" ${f.choirLevelName === 'raised' ? 'disabled' : ''}>Raise the water to the vault (swim up to the Ribvault)</button>
        <button data-act="mid" ${!f.choirLevelName || f.choirLevelName === 'mid' ? 'disabled' : ''}>Return the water to the shrine line</button>
        <button data-act="drain" class="bad">Drain the shrine (permanent)</button>
        <button data-act="close">Leave it</button>
      </div>${foot(['confirm', 'choose'], ['cancel', 'leave'])}</div>`, 'sluice');
  }
  function fontMenu() {
    const p = P(), g = G();
    actions.take = (ds) => {
      if (!g.freeGraft && p.bag.count('ivory') < 1) { H.toast('The font asks an offering of Mere-ivory for each graft.'); H.Audio.deny(); return; }
      confirm(`Take the ${H.GRAFTS[ds.g].name}? ${H.GRAFTS[ds.g].power}. It grows a knot at your ${H.GRAFTS[ds.g].knot.toLowerCase()}: ${H.GRAFTS[ds.g].cost}.`, () => {
        p.grafts.add(ds.g);
        if (g.freeGraft) g.freeGraft = false; else p.bag.remove('ivory', 1);
        H.Audio.crack(); H.Audio.bell(220, 0.4); G().flash = 0.2;
        if (ds.g === 'lung') p.breath = 30;
        setTimeout(() => H.Narr.think('firstGraft'), 400);
        H.toast(`The ${H.GRAFTS[ds.g].name} takes root. Salt crawls under your skin.`);
      });
    };
    actions.close = () => close();
    const rows = Object.entries(H.GRAFTS).map(([k, d]) => `<div class="row"><span>${d.name} <span class="muted">(${d.knot})</span><div class="desc">${d.power}. Cost: ${d.cost}.</div></span>
      ${p.grafts.has(k) ? '<span class="muted">Taken</span>' : `<button data-act="take" data-g="${k}">${g.freeGraft ? 'Take (free)' : 'Take (1 Mere-ivory)'}</button>`}</div>`).join('');
    show(`<div class="panel narrow"><h2>The Brine font</h2><p class="small">Each graft gives a strong power but grows a knot on Maren herself, which enemies can strike. Grafts change how others treat you, and which endings are open. A graft can be cut out once, at a Keeper's surgery.</p>${rows}
      <div class="btns"><button data-act="close">Step back</button></div>${foot(['confirm', 'choose'], ['cancel', 'step back'])}</div>`, 'font');
  }

  // ---- end screens -----------------------------------------------------------------------------
  // The ending plays as a cinematic (the ending, then what became of everyone), then a summary.
  function ending(title, paras, stats, fates) {
    const panel = () => {
      actions.title = () => { close(); H.Game.toTitle(); };
      show(`<div class="panel narrow ending"><h1 style="font-size:26px">${esc(title)}</h1>${paras.map((t) => `<p>${esc(t)}</p>`).join('')}
        ${fates && fates.length ? `<h3>Afterwards</h3>${fates.map((t) => `<p class="small">${esc(t)}</p>`).join('')}` : ''}
        <p class="small muted">${esc(stats)}</p><div class="btns center"><button data-act="title" class="primary">Return to title</button></div></div>`, 'ending');
    };
    cinematic([title].concat(paras, fates || []), panel, { first: 'title' });
  }
  function death(info, onWake) {
    actions.wake = () => { close(); onWake(); };
    show(`<div class="panel narrow death"><h2>${esc(info.title)}</h2><p class="lore">${esc(info.line)}</p><p class="small muted">${esc(info.detail)}</p>
      <div class="btns center"><button data-act="wake" class="primary">Wake at the bell</button></div></div>`, 'death');
  }

  // Words cut into the island, shown like an inscription.
  function inscription(title, text) {
    show(`<div class="inscription"><div class="ititle">${esc(title)}</div><div class="itext">${esc(text)}</div>${foot(['confirm', 'close'])}</div>`, 'inscription');
    root.querySelector('.inscription').addEventListener('click', () => close());
  }

  // Black-screen text, one line at a time. Confirm or a click advances; cancel skips.
  let cine = null;
  function cinematic(lines, done, opts = {}) {
    cine = { lines: lines.filter(Boolean), i: -1, done, opts, timer: null };
    show(`<div class="cinematic"><div class="cline"></div><div class="cskip">${foot(['confirm', 'continue'], ['cancel', 'skip'])}</div></div>`, 'cinematic');
    root.querySelector('.cinematic').addEventListener('click', () => nextCine());
    nextCine();
  }
  function nextCine() {
    if (!cine) return;
    clearTimeout(cine.timer);
    cine.i++;
    if (cine.i >= cine.lines.length) { finishCine(); return; }
    const el = root.querySelector('.cline');
    const text = cine.lines[cine.i];
    el.classList.remove('show');
    el.classList.toggle('big', cine.opts.first === 'title' && cine.i === 0);
    void el.offsetWidth;
    el.textContent = text;
    el.classList.add('show');
    cine.timer = setTimeout(nextCine, 2600 + text.length * 45);
  }
  function finishCine() {
    if (!cine) return;
    clearTimeout(cine.timer);
    const done = cine.done;
    cine = null;
    close();
    if (done) done();
  }

  return { init, isOpen, close, padUpdate, controller, say, dialogue, confirm, title, controls, pause, bellMenu, reliquary, bench, satchel, map, sluiceMenu, fontMenu, ending, death, inscription, cinematic, get cinematicOpen() { return !!cine; } };
})();
