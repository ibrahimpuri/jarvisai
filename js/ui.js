// DOM menus layered over the canvas. The game pauses while any menu is open.
'use strict';
H.UI = (() => {
  let root = null;
  let stack = [];       // close callbacks
  let dlg = null;       // active dialogue {lines, i, done}
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  const G = () => H.Game;
  const P = () => H.Game.player;

  function init() {
    root = document.getElementById('ui');
    window.addEventListener('keydown', (e) => {
      if (!isOpen()) return;
      if (dlg && (e.code === 'KeyE' || e.code === 'Space' || e.code === 'Enter')) { e.preventDefault(); advance(); return; }
      if (e.code === 'Escape' || ((e.code === 'Tab' || e.code === 'KeyM') && root.dataset.kind === 'map') || (e.code === 'KeyI' && root.dataset.kind === 'satchel')) {
        e.preventDefault();
        if (root.dataset.kind === 'title' || root.dataset.kind === 'ending' || root.dataset.kind === 'death') return;
        close();
      }
    });
  }
  function isOpen() { return !root.classList.contains('hidden'); }
  function show(html, kind, clear) {
    root.innerHTML = html;
    root.dataset.kind = kind || '';
    root.classList.remove('hidden');
    root.classList.toggle('clear', !!clear);
    H.Input.clear();
    root.querySelectorAll('[data-act]').forEach((b) => b.addEventListener('click', () => { H.Audio.ui(); actions[b.dataset.act] && actions[b.dataset.act](b.dataset); }));
  }
  function close() {
    root.classList.add('hidden'); root.innerHTML = ''; root.dataset.kind = ''; dlg = null;
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
    show(`<div class="panel dialogue">${d.who ? `<div class="who">${esc(d.who)}</div>` : ''}<div class="line">${esc(d.lines[d.i])}</div>
      <div class="more">${d.i < d.lines.length - 1 ? 'E / Space  continue' : 'E / Space  close'}</div></div>`, 'dialogue', true);
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
    show(`<div class="panel narrow"><p>${esc(text)}</p><div class="btns center"><button data-act="yes" class="primary">Yes</button><button data-act="no">No</button></div></div>`, 'confirm');
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
      </div>
      <p class="small muted" style="text-align:center;margin-top:14px">Mouse + keyboard. Best with headphones.</p>
    </div>`, 'title');
  }

  function controls(back) {
    actions.back = () => (back ? back() : close());
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
    show(`<div class="panel narrow"><h2>Controls</h2><table class="keys">${k.map(([a, b]) => `<tr><td><kbd>${a}</kbd></td><td>${b}</td></tr>`).join('')}</table>
      <h3>How Hollowmere works</h3>
      <p class="small">Every Brine body is held up by salt knots at its joints. Rivets on flesh barely scratch; hit the knots. The Crown knot in the chest is sealed until two limb knots break. A dropped body lying in water when the next Rising tide reaches it will stand again at High tide: haul it above the tide line (E) or burn it (X).</p>
      <p class="small">The tide dial (top right) runs a 10-minute cycle: Low, Rising, High, Ebbing. Painted tide marks on pillars show where the water will reach. Bell Rooms are safe; resting there advances the tide to the next Low.</p>
      <div class="btns center"><button data-act="back">Back</button></div></div>`, 'controls');
  }

  function pause() {
    actions.resume = () => close();
    actions.controls = () => controls(pause);
    actions.quit = () => { close(); H.Game.toTitle(); };
    actions.vol = (ds) => { H.Audio.setVolume(parseFloat(ds.v)); G().volume = parseFloat(ds.v); pause(); };
    const v = G().volume;
    show(`<div class="panel narrow"><h2>Paused</h2>
      <p class="muted small">${esc(G().room.name)} · ${esc(H.REGIONS[G().room.region].name)} · Act ${'I'.repeat(G().act)}</p>
      <h3>Volume</h3><div class="opt">${[0, 0.35, 0.7, 1].map((x) => `<button data-act="vol" data-v="${x}" class="${v === x ? 'sel' : ''}">${x === 0 ? 'Off' : Math.round(x * 100) + '%'}</button>`).join('')}</div>
      <div class="btns col"><button data-act="resume" class="primary">Resume</button><button data-act="controls">Controls</button><button data-act="quit">Quit to title (progress since the last save is lost)</button></div></div>`, 'pause');
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
      <p class="small muted">Resting skips time. Every body left lying in water re-knots on the way.</p></div>`, 'bell');
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
      <div class="btns"><button data-act="back">Back</button></div></div>`, 'reliquary');
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
      ${body}<div class="btns"><button data-act="back">Back</button></div></div>`, 'bench');
  }

  // ---- satchel / logbook ----------------------------------------------------------------------
  let satTab = 'satchel';
  function satchel() {
    const p = P(), g = G();
    actions.tab = (ds) => { satTab = ds.t; satchel(); };
    actions.close = () => close();
    actions.craft = (ds) => { close(); p.quickCraft(ds.id); };
    actions.drop = (ds) => { const n = p.bag.count(ds.i); p.bag.remove(ds.i, n); g.spawnLoose(ds.i, p.cx(), p.y + p.h - 4, n); satchel(); };
    actions.read = (ds) => { const isPage = ds.k === 'p'; say(isPage ? "Agathe's log" : "Prior's tide table", (isPage ? H.Story.PAGES : H.Story.TABLES)[ds.n], () => satchel()); };
    const tabs = [['satchel', 'Satchel'], ['craft', 'Craft'], ['gear', 'Gear'], ['log', 'Logbook']];
    let body = '';
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
      const ab = [...p.abilities].map((a) => `<div class="row"><span>${H.ABILITIES[a].name}<div class="desc">${H.ABILITIES[a].desc}</div></span></div>`).join('');
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
      <div class="btns"><button data-act="close">Close (I)</button></div></div>`, 'satchel');
  }
  const W = () => H.World;

  function map() {
    show(`<div class="panel"><canvas class="map" width="760" height="470"></canvas><div class="btns"><button data-act="close">Close (Tab)</button></div></div>`, 'map');
    actions.close = () => close();
    H.HUD.drawMap(root.querySelector('canvas'));
  }

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
      </div></div>`, 'sluice');
  }
  function fontMenu() {
    const p = P(), g = G();
    actions.take = (ds) => {
      confirm(`Take the ${H.GRAFTS[ds.g].name}? ${H.GRAFTS[ds.g].power}. It grows a knot at your ${H.GRAFTS[ds.g].knot.toLowerCase()}: ${H.GRAFTS[ds.g].cost}.`, () => {
        p.grafts.add(ds.g);
        if (g.freeGraft) g.freeGraft = false;
        H.Audio.crack(); H.Audio.bell(220, 0.4); G().flash = 0.2;
        if (ds.g === 'lung') p.breath = 30;
        H.toast(`The ${H.GRAFTS[ds.g].name} takes root. Salt crawls under your skin.`);
      });
    };
    actions.close = () => close();
    const rows = Object.entries(H.GRAFTS).map(([k, d]) => `<div class="row"><span>${d.name} <span class="muted">(${d.knot})</span><div class="desc">${d.power}. Cost: ${d.cost}.</div></span>
      ${p.grafts.has(k) ? '<span class="muted">Taken</span>' : `<button data-act="take" data-g="${k}">Take</button>`}</div>`).join('');
    show(`<div class="panel narrow"><h2>The Brine font</h2><p class="small">Each graft gives a strong power but grows a knot on Maren herself, which enemies can strike. Grafts change how others treat you, and which endings are open. A graft can be cut out once, at a Keeper's surgery.</p>${rows}
      <div class="btns"><button data-act="close">Step back</button></div></div>`, 'font');
  }

  // ---- end screens -----------------------------------------------------------------------------
  function ending(title, paras, stats) {
    actions.title = () => { close(); H.Game.toTitle(); };
    show(`<div class="panel narrow ending"><h1 style="font-size:26px">${esc(title)}</h1>${paras.map((t) => `<p>${esc(t)}</p>`).join('')}
      <p class="small muted">${esc(stats)}</p><div class="btns center"><button data-act="title" class="primary">Return to title</button></div></div>`, 'ending');
  }
  function death(text, onWake) {
    actions.wake = () => { close(); onWake(); };
    show(`<div class="panel narrow"><h2>The water takes you</h2><p>${esc(text)}</p><div class="btns center"><button data-act="wake" class="primary">Wake at the bell</button></div></div>`, 'death');
  }

  return { init, isOpen, close, say, dialogue, confirm, title, controls, pause, bellMenu, reliquary, bench, satchel, map, sluiceMenu, fontMenu, ending, death };
})();
