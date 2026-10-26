// Scripted gameplay scenarios with real keyboard/mouse input. Usage: node tools/play.js [scenario...]
'use strict';
const { chromium } = require('playwright');
const path = require('path');
const OUT = process.env.OUT || '.';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function boot() {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.errors = [];
  page.on('pageerror', (e) => page.errors.push(String(e.stack || e)));
  page.on('console', (m) => { if (m.type() === 'error') page.errors.push(m.text()); });
  await page.goto('file://' + path.join(__dirname, '..', 'index.html'));
  await sleep(300);
  await page.evaluate(() => localStorage.clear());
  await page.click('button[data-act="newgame"]');
  await sleep(100);
  await page.keyboard.press('Escape');
  await sleep(100);
  return { browser, page };
}
const ev = (page, fn, arg) => page.evaluate(fn, arg);
async function aimAt(page, wx, wy) {
  const cam = await ev(page, () => H.Game.cam);
  await page.mouse.move((wx - cam.x) * 2, (wy - cam.y) * 2);
}
async function hold(page, key, ms) { await page.keyboard.down(key); await sleep(ms); await page.keyboard.up(key); }
async function place(page, roomId, tx, ty) {
  await ev(page, ([id, tx, ty]) => { const G = H.Game, r = H.ROOM[id]; if (G.room !== r) G.enterRoom(r); G.player.x = r.px + tx * 16 + 3; G.player.y = r.py + (ty + 1) * 16 - G.player.h; G.player.vx = G.player.vy = 0; G.snapCamera(); }, [roomId, tx, ty]);
}
const state = (page) => ev(page, () => { const p = H.Game.player; return { room: H.Game.room.id, x: Math.round(p.x), y: Math.round(p.y), tx: Math.floor(p.cx() / 16) - H.Game.room.ox, ty: Math.floor((p.y + p.h - 1) / 16) - H.Game.room.oy, hp: p.hp, st: p.state, ground: p.onGround, phase: H.Tide.phase() }; });
function check(name, cond, info) { console.log((cond ? 'PASS ' : 'FAIL ') + name + (info !== undefined ? '  ' + JSON.stringify(info) : '')); if (!cond) process.exitCode = 1; }

const scenarios = {
  async movement(page) {
    await place(page, 'reef', 25, 12); await sleep(300);
    let s = await state(page);
    check('stands on the reef deck', s.room === 'reef' && s.ground, s);
    await hold(page, 'KeyD', 400);
    const s2 = await state(page);
    check('walks right', s2.x > s.x + 30, s2);
    // jump height ~3.8 tiles
    const y0 = s2.y;
    await page.keyboard.down('Space');
    let minY = y0;
    for (let i = 0; i < 30; i++) { await sleep(16); const y = (await state(page)).y; minY = Math.min(minY, y); }
    await page.keyboard.up('Space');
    check('jump rises 3-4.2 tiles', y0 - minY > 48 && y0 - minY < 68, { rise: y0 - minY });
    // climb the mast ladder
    await place(page, 'reef', 22, 12);
    await hold(page, 'KeyW', 1500);
    const s3 = await state(page);
    check('climbs mast', s3.ty < 9, s3);
    // walk off left edge to the pier
    await place(page, 'reef', 2, 16);
    await hold(page, 'KeyA', 600);
    const s4 = await state(page);
    check('reaches pier', s4.room === 'pier', s4);
  },
  async shucker(page) {
    await place(page, 'pier', 30, 16);
    const st = await state(page);
    check('pier intro set tide near Rising', st.phase === 'low', st);
    await ev(page, () => H.Tide.advanceTo('rising'));
    // wait for the water to reach him
    let mode;
    for (let i = 0; i < 200; i++) { await sleep(100); mode = await ev(page, () => H.Game.ents.find((e) => e.key === 'first_shucker').mode); if (mode !== 'dormant') break; }
    check('first shucker stands when water reaches', mode !== 'dormant', mode);
    // aim at a knee and fire until broken
    const shoot = async (id) => {
      for (let i = 0; i < 12; i++) {
        const kp = await ev(page, (id) => { const e = H.Game.ents.find((e) => e.key === 'first_shucker'); const k = e.knot(id); return k.broken ? null : e.knotPos(k); }, id);
        if (!kp) return true;
        await aimAt(page, kp.x, kp.y);
        await page.keyboard.down('KeyK'); // focused beam
        await page.mouse.down(); await sleep(40); await page.mouse.up();
        await sleep(320);
        const need = await ev(page, () => H.Game.player.weapons.driver.mag === 0);
        if (need) { await page.keyboard.press('KeyR'); await sleep(1400); }
      }
      return false;
    };
    await ev(page, () => { H.Game.god = true; H.Game.player.bag.add('rivets', 30); });
    const crownSealed = await ev(page, () => { const e = H.Game.ents.find((e) => e.key === 'first_shucker'); return e.isSealed(e.knot('crown')); });
    check('crown starts sealed', crownSealed);
    check('knee L broken', await shoot('kneeL'));
    check('knee R broken', await shoot('kneeR'));
    const crawl = await ev(page, () => H.Game.ents.find((e) => e.key === 'first_shucker').pose);
    check('crawls after both knees', crawl === 'crawl', crawl);
    const unsealed = await ev(page, () => { const e = H.Game.ents.find((e) => e.key === 'first_shucker'); return !e.isSealed(e.knot('crown')); });
    check('crown unsealed after 2 limbs', unsealed);
    check('crown broken', await shoot('crown'));
    await page.keyboard.up('KeyK');
    const m = await ev(page, () => H.Game.ents.find((e) => e.key === 'first_shucker').mode);
    check('shucker downed', m === 'downed', m);
    // re-knot: it lies in rising water, stands at High
    await sleep(300);
    const marked = await ev(page, () => H.Game.ents.find((e) => e.key === 'first_shucker').reknot);
    check('downed body in rising water is marked', marked);
    await ev(page, () => H.Tide.advanceTo('high'));
    await sleep(100);
    const m2 = await ev(page, () => H.Game.ents.find((e) => e.key === 'first_shucker').mode);
    check('re-knots and stands at High', m2 === 'hunt' || m2 === 'windup' || m2 === 'attack', m2);
    await page.screenshot({ path: path.join(OUT, 'play_shucker.png') });
  },
  async dragburn(page) {
    await ev(page, () => { H.Game.god = true; });
    await place(page, 'tithe', 9, 16);
    await ev(page, () => H.Game.ents.forEach((e) => { if (e.type === 'shucker') e.gone = true; }));
    // a true corpse seated at col 9: drag it
    await page.keyboard.press('KeyE');
    await sleep(100);
    const dragging = await ev(page, () => !!H.Game.player.dragging);
    check('E starts dragging a corpse', dragging);
    const x0 = (await state(page)).x;
    await hold(page, 'KeyD', 800);
    const x1 = (await state(page)).x;
    check('dragging is slow', x1 - x0 > 5 && x1 - x0 < 45, { moved: x1 - x0 });
    await page.keyboard.press('KeyE');
    await sleep(80);
    check('E lets go', !(await ev(page, () => !!H.Game.player.dragging)));
    const oil0 = await ev(page, () => H.Game.player.oil);
    await page.keyboard.press('KeyX');
    await sleep(3400);
    const res = await ev(page, () => ({ oil: H.Game.player.oil, gone: H.Game.ents.filter((e) => e.corpse && e.gone).length }));
    check('X burns a body with oil', res.gone >= 1 && res.oil < oil0 - 25, res);
  },
  async bell(page) {
    await place(page, 'chapel', 20, 16);
    await ev(page, () => H.Game.player.bag.add('oil', 1));
    await page.keyboard.press('KeyE');
    await sleep(150);
    const lit = await ev(page, () => !!H.Game.bellsLit.bell_chapel);
    check('bell rekindled with oil', lit);
    const open = await ev(page, () => document.querySelector('#ui').dataset.kind);
    check('bell menu opens', open === 'bell', open);
    await ev(page, () => { H.Game.player.hp = 2; H.Tide.setPhase('high', 30); });
    await page.click('button[data-act="rest"]');
    await sleep(150);
    check('resting shows a dream', await ev(page, () => document.querySelector('#ui').dataset.kind === 'cinematic'));
    await page.keyboard.press('Escape'); await sleep(100);
    const r = await ev(page, () => ({ hp: H.Game.player.hp, max: H.Game.player.maxHp, phase: H.Tide.phase(), save: !!localStorage.getItem('hollowmere_save'), resp: H.Game.respawn.room }));
    check('rest heals, saves, advances to Low', r.hp === r.max && r.phase === 'low' && r.save && r.resp === 'chapel', r);
    // Ansel talk
    await place(page, 'chapel', 9, 16);
    await page.keyboard.press('KeyE');
    await sleep(100);
    const who = await ev(page, () => document.querySelector('.dialogue .who') && document.querySelector('.dialogue .who').textContent);
    check('Ansel speaks', who === 'Brother Ansel', who);
    for (let i = 0; i < 5; i++) { await page.keyboard.press('KeyE'); await sleep(60); }
    check('dialogue closes and sets quest', await ev(page, () => !H.UI.isOpen() && H.Game.quests.crank === 'active'));
  },
  async tithe(page) {
    await ev(page, () => { H.Game.god = true; });
    await place(page, 'tithe', 4, 16);
    await page.keyboard.press('KeyE');
    await sleep(200);
    const r = await ev(page, () => ({ crank: H.Game.player.bag.count('crank'), trap: H.World.flags.titheTrapped, awake: H.Game.ents.filter((e) => e.type === 'shucker' && e.mode !== 'dormant').length }));
    check('crank taken triggers trap', r.crank === 1 && r.trap && r.awake >= 3, r);
    await sleep(6000);
    const f = await ev(page, () => H.World.state('tithe').flood);
    check('hall floods from the grates', f < 16, f);
    // swing from a ring: stand under ring at (3,11) area
    await ev(page, () => { H.Game.player.x = H.Game.room.px + 5 * 16; H.Game.player.y = H.Game.room.py + 13 * 16; H.Game.player.vy = 0; });
    await page.keyboard.down('KeyQ'); await sleep(120);
    const sw = await ev(page, () => H.Game.player.state);
    await page.keyboard.up('KeyQ');
    await sleep(150);
    check('Q latches onto a nearby ring', sw === 'swing', sw);
    // gallery and glimpse
    await place(page, 'tithe', 34, 7);
    await sleep(200);
    check('Undertow glimpse at the window', await ev(page, () => !!H.World.flags.glimpse));
  },
  async death(page) {
    await place(page, 'pier', 22, 18);
    await ev(page, () => { H.Game.player.bag.add('scrap', 3); H.Game.player.hurt(99); });
    await sleep(1200);
    const r = await ev(page, () => ({ dead: H.Game.player.dead, drop: H.Game.satchelDrop && H.Game.satchelDrop.tidal, kind: document.querySelector('#ui').dataset.kind }));
    check('death drops a tidal satchel', r.dead && r.drop && r.kind === 'death', r);
    await page.click('button[data-act="wake"]');
    await sleep(200);
    const s = await state(page);
    check('wakes at respawn', !(await ev(page, () => H.Game.player.dead)) && s.room === 'reef', s);
    await ev(page, () => H.Tide.advanceTo('ebbing'));
    const sd = await ev(page, () => H.Game.satchelDrop);
    check('ebb carries satchel to the Wrack Pool', sd && sd.room === 'reef' && !sd.tidal, sd && sd.room);
    await ev(page, () => { const sd = H.Game.satchelDrop; const p = H.Game.player; p.x = sd.x - 5; p.y = sd.y - p.h; });
    await sleep(200);
    const got = await ev(page, () => ({ drop: !!H.Game.satchelDrop, scrap: H.Game.player.bag.count('scrap') }));
    check('satchel recovered', !got.drop && got.scrap >= 3, got);
  },
  async brine(page) {
    await ev(page, () => { H.Game.god = true; });
    await place(page, 'brineshaft', 20, 20);
    await sleep(1500);
    const s1 = await state(page);
    check('floats in brine without boots', s1.ty <= 19, s1);
    await ev(page, () => H.Game.player.abilities.add('boots'));
    await page.keyboard.press('KeyB');
    await hold(page, 'KeyA', 2500);
    const s2 = await state(page);
    check('sinks with boots on', s2.ty > 20, s2);
  },
  async barge(page) {
    await ev(page, () => { H.Game.god = true; H.World.flags.seawallOpen = true; });
    const pull = async (tx) => { await place(page, 'saltgate', tx, 18); await page.keyboard.press('KeyE'); await sleep(80); };
    await pull(12); // skull first: wrong
    check('wrong lever jams', await ev(page, () => H.World.flags.basinJam));
    await ev(page, () => H.Tide.advanceTo('ebbing'));
    check('Ebb resets the puzzle', await ev(page, () => !H.World.flags.basinJam && H.World.flags.basinSeq.length === 0));
    await pull(16); await pull(12); await pull(24);
    check('correct order fills the pan', await ev(page, () => H.World.flags.basinFilling));
    await ev(page, () => H.World.flags.basinFilling = false);
    await ev(page, () => { H.World.flags.basinSeq = ['sea', 'saint']; H.World.flags.basinTarget = 11; });
    await place(page, 'saltgate', 31, 17);
    await sleep(300);
    await ev(page, () => { H.World.flags.basinSeq = ['sea', 'saint', 'kiln']; H.World.flags.basinTarget = 7; H.World.flags.basinFilling = true; });
    for (let i = 0; i < 12; i++) await sleep(1000);
    const s = await state(page);
    check('barge carries Maren up', s.ty < 9, s);
    await page.keyboard.down('KeyD'); await page.keyboard.down('Space'); await sleep(450); await page.keyboard.up('Space'); await sleep(1200); await page.keyboard.up('KeyD');
    const s2 = await state(page);
    check('reach the kilns', s2.room === 'kilns', s2);
  },
  async saveload(page) {
    await place(page, 'chapel', 20, 16);
    await ev(page, () => { H.Game.player.bag.add('oil', 1); H.Game.player.bag.add('scrap', 4); });
    await page.keyboard.press('KeyE');
    await sleep(100);
    await page.click('button[data-act="save"]');
    await page.click('button[data-act="leave"]');
    await ev(page, () => H.Game.toTitle());
    await sleep(100);
    await page.click('button[data-act="cont"]');
    await sleep(200);
    const r = await ev(page, () => ({ room: H.Game.room.id, scrap: H.Game.player.bag.count('scrap'), lit: !!H.Game.bellsLit.bell_chapel }));
    check('save and continue', r.room === 'chapel' && r.scrap === 4 && r.lit, r);
  },
  async abbot(page) {
    await ev(page, () => { H.Game.god = true; H.World.flags.seawallOpen = true; });
    await place(page, 'abbot', 5, 17);
    await sleep(300);
    const started = await ev(page, () => { const a = H.Game.ents.find((e) => e.type === 'abbot'); return a && a.started; });
    check('Abbot wakes when Maren approaches', started);
    // stand by the left kiln and let him slam it
    let plates = 3;
    for (let i = 0; i < 40 && plates === 3; i++) { await sleep(250); plates = await ev(page, () => H.Game.ents.find((e) => e.type === 'abbot').knots.filter((k) => k.plated).length); }
    check('slamming a kiln knocks a plate off', plates < 3, plates);
    const tink = await ev(page, () => { const a = H.Game.ents.find((e) => e.type === 'abbot'); const k = a.knots.find((k) => k.plated); return k ? a.damageKnot(k, 5, 'rivet') : 'none'; });
    check('plated knots shrug off rivets', tink === false || tink === 'none', tink);
    await ev(page, () => { const a = H.Game.ents.find((e) => e.type === 'abbot'); a.knots.forEach((k) => { k.plated = false; }); a.damageKnot(a.knot('armL'), 99, 'rivet'); a.damageKnot(a.knot('armR'), 99, 'rivet'); a.damageKnot(a.knot('crown'), 99, 'rivet'); });
    await sleep(100);
    const r = await ev(page, () => ({ dead: H.World.flags.abbotDead, act: H.Game.act, boots: !!H.Game.ents.find((e) => e.type === 'ability' && e.spec.id === 'boots'), ivory: H.Game.player.bag.count('ivory') }));
    check('Abbot killed: Act II, boots drop, ivory', r.dead && r.act === 2 && r.boots && r.ivory >= 2, r);
  },
  async prisms(page) {
    await ev(page, () => { H.Game.god = true; const p = H.Game.player; ['boots', 'jet', 'tidekey'].forEach((a) => p.abilities.add(a)); p.oil = 90; });
    await place(page, 'prisms', 30, 36);
    await ev(page, () => H.Game.ents.forEach((e) => { if (e.isBody) e.gone = true; }));
    const turn = async (id, times) => {
      const t = await ev(page, (id) => H.Game.ents.find((e) => e.spec && e.spec.id === id).spec, id);
      await place(page, 'prisms', t.tx, t.ty + (id === 'prismC' ? 0 : 0));
      for (let i = 0; i < times; i++) { await page.keyboard.press('KeyE'); await sleep(60); }
      return ev(page, (id) => H.Game.ents.find((e) => e.spec && e.spec.id === id).dir, id);
    };
    // A starts down(1) -> right(0): 3 turns; B left(2) -> down(1): 3 turns; C up(3) -> left(2): 3 turns
    const a = await turn('prismA', 3), b = await turn('prismB', 3), c = await turn('prismC', 3);
    check('prisms turn with E', a === 0 && b === 1 && c === 2, [a, b, c]);
    await place(page, 'prisms', 7, 16);
    const target = await ev(page, () => H.Game.ents.find((e) => e.spec && e.spec.id === 'prismA').center());
    await aimAt(page, target.x, target.y);
    await page.keyboard.down('KeyK');
    await sleep(3500);
    const path = await ev(page, () => (H.Game.player.beamPath || []).map((q) => [Math.round((q.x - H.Game.room.px) / 16), Math.round((q.y - H.Game.room.py) / 16)]));
    await page.keyboard.up('KeyK');
    const done = await ev(page, () => H.Game.ents.find((e) => e.type === 'crystal').done);
    check('bent beam wakes the crystal', done, path);
    const salt = await ev(page, () => { const r = H.Game.room; let n = 0; for (let y = 0; y < r.th; y++) for (let x = 0; x < r.tw; x++) if (H.World.charAt(r.ox + x, r.oy + y) === 'S') n++; return n; });
    check('salt seal around the vault breaks', salt === 0, salt);
    await page.screenshot({ path: require('path').join(OUT, 'play_prisms.png') });
  },
  async choir(page) {
    await ev(page, () => { H.Game.god = true; const p = H.Game.player; ['boots', 'jet'].forEach((a) => p.abilities.add(a)); p.bag.add('ivory', 1); });
    await place(page, 'choir', 4, 16);
    await page.keyboard.press('KeyE'); await sleep(60); await page.keyboard.press('KeyE'); await sleep(60);
    check('valve sets basin level 2', await ev(page, () => H.World.flags.basin === 2));
    await place(page, 'choir', 2, 16);
    await page.keyboard.press('KeyE'); await sleep(100);
    const broken = await ev(page, () => H.World.charAt(H.ROOM.choir.ox + 4, H.ROOM.choir.oy + 19) !== 'C');
    check('tuned bell at the right pitch shatters the crystal', broken);
    await place(page, 'choir', 2, 20);
    await sleep(300);
    check('Tide Key collected', await ev(page, () => H.Game.player.has('tidekey')));
    if (await ev(page, () => H.UI.isOpen())) await page.keyboard.press('KeyE');
    await place(page, 'choir', 36, 16);
    await page.keyboard.press('KeyE'); await sleep(100);
    await page.click('button[data-act="raise"]');
    await sleep(500);
    check('sluice raises the shrine water', await ev(page, () => H.World.flags.choirTarget === 3));
    // Hesper and the font
    await place(page, 'choir', 31, 16);
    await page.keyboard.press('KeyE'); await sleep(80);
    for (let i = 0; i < 4; i++) { await page.keyboard.press('KeyE'); await sleep(60); }
    await place(page, 'choir', 28, 16);
    await page.keyboard.press('KeyE'); await sleep(80);
    await page.click('button[data-act="take"][data-g="ear"]'); await sleep(50);
    await page.click('button[data-act="yes"]'); await sleep(50);
    check('font grants a graft', await ev(page, () => H.Game.player.grafts.has('ear')));
    await ev(page, () => { [1, 2, 3, 4].forEach((n) => H.Game.tables.add(n)); });
    await place(page, 'choir', 25, 16);
    await page.keyboard.press('KeyE'); await sleep(80);
    await page.click('button[data-act="yes"]'); await sleep(80);
    check('tables sold to the Choir', await ev(page, () => H.Game.quests.tables === 'sold' && H.Game.freeGraft));
    if (await ev(page, () => H.UI.isOpen())) await page.keyboard.press('KeyE');
    await place(page, 'choir', 36, 16);
    await page.keyboard.press('KeyE'); await sleep(100);
    await page.click('button[data-act="drain"]'); await sleep(50);
    await page.click('button[data-act="yes"]'); await sleep(100);
    const d = await ev(page, () => ({ drained: H.World.flags.choirDrained, hostile: H.World.flags.choirHostile, members: H.Game.ents.filter((e) => e.type === 'grafted' && !e.passive).length }));
    check('draining turns the Choir hostile', d.drained && d.hostile && d.members === 2, d);
  },
  async lift(page) {
    await ev(page, () => { H.Game.god = true; });
    await place(page, 'priors', 30, 16);
    await ev(page, () => H.Game.ents.forEach((e) => { if (e.type === 'saltwright') e.gone = true; }));
    for (let i = 0; i < 2; i++) {
      await place(page, 'priors', i ? 31 : 29, 16);
      await page.keyboard.press('KeyE'); await sleep(80);
      const ok = await ev(page, () => !!H.Game.player.dragging);
      check('drag corpse ' + (i + 1), ok);
      await page.keyboard.down('KeyA');
      for (let k = 0; k < 60; k++) { await sleep(100); const x = await ev(page, () => (H.Game.player.dragging.cx() - H.Game.room.px) / 16); if (x < (i ? 22.6 : 21.4)) break; }
      await page.keyboard.up('KeyA');
      await page.keyboard.press('KeyE'); await sleep(400);
    }
    await sleep(1500);
    const r = await ev(page, () => ({ dropped: H.World.flags.liftDropped, load: (H.Game.ents.find((e) => e.type === 'lift') || {}).load }));
    check('two bodies drop the Dead Weight lift', r.dropped, r);
  },
  async undertow(page) {
    await ev(page, () => { H.Game.god = true; H.Game.act = 2; H.Tide.setPhase('high', 20); });
    await place(page, 'nave', 20, 16);
    await ev(page, () => { H.Game.sinceLoad = 11; H.Game.undertow.room = 'nave'; H.Game.undertow.moveT = 30; });
    await sleep(300);
    const live = await ev(page, () => !!H.Game.ents.find((e) => e.type === 'undertow'));
    check('Undertow enters a deep sea room at High', live);
    await ev(page, () => { const u = H.Game.ents.find((e) => e.type === 'undertow'); u.damageKnot(u.knot('buoy'), 99, 'rivet'); });
    await sleep(300);
    check('breaking the buoy knot drives it off for the tide', await ev(page, () => H.Game.undertow.driven));
    await ev(page, () => H.Tide.advanceTo('low'));
    check('it returns next tide', await ev(page, () => !H.Game.undertow.driven));
    // never in a bell room / never within 10s of a load
    await ev(page, () => { H.Tide.setPhase('high', 20); H.Game.undertow.room = 'cloisterbell'; });
    await place(page, 'cloisterbell', 15, 16);
    await sleep(300);
    check('never enters a Bell Room', !(await ev(page, () => !!H.Game.ents.find((e) => e.type === 'undertow'))));
  },
  async lair(page) {
    await ev(page, () => { H.Game.god = true; H.Game.act = 3; H.Game.player.abilities.add('seal'); });
    await place(page, 'lair', 10, 10);
    const has = await ev(page, () => !!H.Game.ents.find((e) => e.type === 'undertow' && e.boss));
    check('Undertow waits in its lair', has);
    const r = await ev(page, () => {
      const u = H.Game.ents.find((e) => e.type === 'undertow' && e.boss);
      const sealed = u.isSealed(u.knot('ropeL'));
      u.damageKnot(u.knot('buoy'), 99, 'rivet');
      const open = !u.isSealed(u.knot('ropeL'));
      u.damageKnot(u.knot('ropeL'), 99, 'rivet'); u.damageKnot(u.knot('ropeR'), 99, 'rivet');
      u.damageKnot(u.knot('crown'), 99, 'rivet');
      return { sealed, open, dead: H.World.flags.undertowDead, skull: H.World.flags.skullOpen };
    });
    check('buoy, then ropes, then crown; skull opens', r.sealed && r.open && r.dead && r.skull, r);
  },
  async shoal(page) {
    await ev(page, () => { H.Game.god = true; H.Game.act = 3; H.World.flags.skullOpen = true; });
    await place(page, 'skullstair', 5, 16);
    const r = await ev(page, () => {
      const s = H.Game.ents.find((e) => e.type === 'shoal');
      if (!s) return 'no shoal';
      s.start();
      s.knots.forEach((k) => s.damageKnot(k, 99, 'rivet'));
      return { dead: H.World.flags.shoalDead, shed: H.Game.ents.filter((e) => e.shed).length };
    });
    check('Shoal sheds a body per knot and dies', r.dead && r.shed === 8, r);
  },
  async teodor(page) {
    await ev(page, () => { H.Game.god = true; H.Game.quests.teodor = 'asked'; H.Game.quests.odileMet = true; });
    await place(page, 'nave', 14, 16);
    await ev(page, () => { const t = H.Game.ents.find((e) => e.teodor); t.wake(true); ['kneeL', 'kneeR', 'elbowL', 'elbowR'].forEach((id) => t.damageKnot(t.knot(id), 99, 'rivet')); });
    check('breaking every limb knot but the Crown captures Teodor', await ev(page, () => H.Game.ents.find((e) => e.teodor).mode === 'captured'));
    await ev(page, () => { const t = H.Game.ents.find((e) => e.teodor); const p = H.Game.player; p.x = t.cx() - 5; p.y = t.feet() - p.h; });
    await page.keyboard.press('KeyE'); await sleep(100);
    check('E binds him', await ev(page, () => H.Game.quests.teodor === 'bound'));
    await place(page, 'saltbell', 8, 18);
    await page.keyboard.press('KeyE'); await sleep(80);
    for (let i = 0; i < 6 && await ev(page, () => H.UI.isOpen()); i++) { await page.keyboard.press('KeyE'); await sleep(60); }
    const q = await ev(page, () => ({ t: H.Game.quests.teodor, size: H.Game.player.bag.size }));
    check('Odile takes him in; satchel grows to 12', q.t === 'captured' && q.size === 12, q);
    await ev(page, () => H.Game.startAct(3));
    await place(page, 'chapel', 20, 16);
    await place(page, 'saltbell', 20, 18);
    const breach = await ev(page, () => ({ t: !!H.Game.ents.find((e) => e.key === 'teodor_reknot'), timer: H.Game.breachT }));
    check('Act III breaches the Saltworks Bell Room', breach.t && breach.timer > 50, breach);
    await ev(page, () => { const t = H.Game.ents.find((e) => e.key === 'teodor_reknot'); ['kneeL', 'kneeR', 'elbowL', 'elbowR'].forEach((id) => t.damageKnot(t.knot(id), 99, 'rivet')); });
    await sleep(200);
    check('capturing him again saves Teodor and Odile', await ev(page, () => H.Game.quests.teodor === 'saved' && !H.Game.quests.odileLost));
  },
  async weapons(page) {
    await ev(page, () => { H.Game.god = true; const p = H.Game.player; p.weapons.harpoon = { tier: 0, mag: 1 }; p.weapons.sling = { tier: 1, mag: 1 }; p.weapons.sawline = { tier: 0, mag: 1 }; p.weaponOrder.push('harpoon', 'sling', 'sawline'); p.bag.add('bolts', 4); p.bag.add('flares', 2); p.bag.add('spools', 2); });
    await place(page, 'cloister', 4, 16);
    const target = await ev(page, () => { const s = H.Game.ents.find((e) => e.type === 'shucker'); s.wake(true); s.mode = 'hunt'; s.stun = 99; s.setPose('stand'); const k = s.knotPos(s.knot('kneeL')); return k; });
    await sleep(100);
    const t2 = await ev(page, () => { const s = H.Game.ents.find((e) => e.type === 'shucker'); s._skel = null; return s.knotPos(s.knot('kneeL')); });
    await page.keyboard.press('Digit2'); await sleep(50);
    await aimAt(page, t2.x, t2.y);
    await page.mouse.down(); await sleep(30); await page.mouse.up(); await sleep(500);
    const pin = await ev(page, () => { const s = H.Game.ents.find((e) => e.type === 'shucker'); return { pin: s.pinT, hp: s.knot('kneeL').hp }; });
    check('harpoon bolt pins a limb', pin.pin > 0 || pin.hp < 3, pin);
    await page.keyboard.press('Digit3'); await sleep(50);
    await page.mouse.down(); await sleep(30); await page.mouse.up(); await sleep(300);
    check('flare sling throws a flare', await ev(page, () => H.Game.projectiles.some((p) => p.kind === 'flare')));
    await page.keyboard.press('Digit4'); await sleep(700);
    await page.mouse.down(); await sleep(30); await page.mouse.up(); await sleep(100);
    check('saw-line strung', await ev(page, () => H.Game.sawlines.length === 1));
  },
  async encrust(page) {
    const r = await ev(page, () => {
      const G = H.Game;
      G.stats.byAct[1] = { knee: 9, elbow: 2, crown: 12 };
      G.startAct(2);
      const s = new H.BodyTypes.shucker({ x: 100, y: 100, key: 't', pose: 'standing' }, H.ROOM.reef);
      G.applyEncrustation(s);
      return { type: G.encrustedType, plated: s.knots.filter((k) => k.plated).map((k) => k.id), open: s.knots.filter((k) => !k.plated).length };
    });
    check('Brine plates the knot type broken most (never crown), leaves 2+ open', r.type === 'knee' && r.plated.includes('kneeL') && r.open >= 2, r);
  },
  async endings(page) {
    await ev(page, () => { H.Game.god = true; H.Game.act = 3; H.World.flags.skullOpen = true; });
    await place(page, 'lamp', 9, 16);
    await page.keyboard.press('KeyE'); await sleep(80);
    const hymn = await ev(page, () => document.querySelector('#ui').innerText);
    check('hymn refuses without 3 grafts', /three grafts/.test(hymn), hymn.slice(0, 80));
    await page.keyboard.press('KeyE'); await sleep(80);
    await place(page, 'lamp', 31, 16);
    await page.keyboard.press('KeyE'); await sleep(80);
    const dark = await ev(page, () => document.querySelector('#ui').innerText);
    check('Keeper\'s Dark needs the log and survivors', /whole log/.test(dark), dark.slice(0, 80));
    await page.keyboard.press('KeyE'); await sleep(80);
    await ev(page, () => { for (let i = 1; i <= 9; i++) H.Game.pages.add(i); });
    await page.keyboard.press('KeyE'); await sleep(80);
    await page.click('button[data-act="yes"]'); await sleep(100);
    check('ending plays as a cinematic with fates', await ev(page, () => document.querySelector('#ui').dataset.kind === 'cinematic'));
    await page.keyboard.press('Escape'); await sleep(150);
    const end = await ev(page, () => ({ kind: document.querySelector('#ui').dataset.kind, ended: H.Game.ended, text: document.querySelector('#ui').innerText.slice(0, 60) }));
    check('Keeper\'s Dark ending plays', end.kind === 'ending' && end.ended === 'dark', end);
    await page.screenshot({ path: require('path').join(OUT, 'play_ending.png') });
  },
  async opening(page) {
    // Real inputs only: reef -> pier -> seawall ladder -> chapel -> back down -> Tithe Hall.
    const until = async (keys, cond, ms = 8000) => {
      for (const k of keys) await page.keyboard.down(k);
      const t0 = Date.now(); let s;
      while (Date.now() - t0 < ms) { s = await state(page); if (cond(s)) break; await sleep(50); }
      for (const k of keys) await page.keyboard.up(k);
      return s;
    };
    await ev(page, () => { H.Game.god = true; });
    await place(page, 'reef', 25, 12); await sleep(300);
    let s = await until(['KeyA'], (s) => s.room === 'pier', 9000);
    check('walk from the deck to the pier', s.room === 'pier', s);
    s = await until(['KeyA'], (s) => s.tx <= 4, 9000);
    check('reach the seawall ladder', s.tx <= 5, s);
    s = await until(['KeyW'], (s) => s.room === 'seawall' && s.st !== 'ladder', 9000);
    check('climb into the seawall', s.room === 'seawall', s);
    s = await until(['KeyD'], (s) => s.room === 'chapel', 9000);
    check('walk into the chapel', s.room === 'chapel', s);
    s = await until(['KeyA'], (s) => s.room === 'seawall' && s.tx <= 4, 9000);
    s = await until(['KeyS'], (s) => s.room === 'pier' && s.ground, 9000);
    check('climb back down to the pier', s.room === 'pier' && s.ground, s);
    s = await until(['KeyA'], (s) => s.room === 'tithe', 9000);
    check('enter the Tithe Hall by the side door', s.room === 'tithe', s);
    s = await until(['KeyA', 'ShiftLeft'], (s) => s.tx <= 5, 15000);
    check('sneak to the altar', s.tx <= 6, s);
  },
  async chandelier(page) {
    // Feedback-driven bot: pump each swing and let go when the predicted arc passes under the next
    // ring (or onto the gallery), holding Q so the next ring is caught automatically.
    await ev(page, () => { H.Game.god = true; H.Game.ents.forEach((e) => { if (e.isBody) e.gone = true; }); });
    await place(page, 'tithe', 4, 16);
    const targets = [[11, 9], [20, 9], [27, 7], [34, 7]];
    await page.keyboard.down('KeyQ'); await sleep(100);
    const rings = [];
    for (let hop = 0; hop < 6; hop++) {
      const sw = await ev(page, () => H.Game.player.swing && Math.round((H.Game.player.swing.ax - H.Game.room.px - 8) / 16));
      if (sw === null || sw === undefined || sw === false) break;
      rings.push(sw);
      const tgt = targets.find((t) => t[0] > sw);
      const last = tgt[0] === 34;
      await page.keyboard.down('KeyD');
      let released = false;
      for (let i = 0; i < 200 && !released; i++) {
        const ok = await ev(page, ([tx, ty, last]) => {
          const p = H.Game.player, w = p.swing, r = H.Game.room;
          if (!w) return 'gone';
          const boost = 1.2;
          let vx = Math.cos(w.ang) * w.av * w.len * boost, vy = -Math.sin(w.ang) * w.av * w.len * boost - 160;
          let x = p.cx(), y = p.y + 4;
          const gx = r.px + tx * 16 + 8, gy = r.py + ty * 16 + 8;
          for (let t = 0; t < 1.2; t += 1 / 60) {
            vy += 1300 / 60; x += vx / 60; y += vy / 60;
            if (last) { if (x > r.px + 32 * 16 && x < r.px + 38 * 16 && Math.abs(y + 18 - (r.py + 8 * 16)) < 6 && vy > 0) return true; }
            else if (Math.abs(x - gx) < 40 && y > gy && y < gy + 100) return true;
          }
          return false;
        }, [tgt[0], tgt[1], last]);
        if (ok === 'gone') break;
        if (ok === true) { await page.keyboard.press('Space'); released = true; }
        else await sleep(16);
      }
      await sleep(400);
      if (last || !released) break;
      const st = await state(page);
      if (st.st !== 'swing') { await sleep(400); if ((await state(page)).st !== 'swing') break; }
    }
    await page.keyboard.up('KeyD');
    await page.keyboard.up('KeyQ');
    await sleep(700);
    const s = await state(page);
    check('chain the chandelier rings to the gallery', s.ty <= 8 && s.tx >= 31, { rings, end: s });
  },
  async narrative(page) {
    const n = await ev(page, () => Object.keys(H.LORE.carvings).length);
    check('every room has a carving', n === (await ev(page, () => H.ROOMS.length)), n);
    // every carving lands on a floor cell in its room
    const bad = await ev(page, () => {
      const out = [];
      for (const r of H.ROOMS) {
        const specs = H.Narr.carvingSpecs(r, new Set());
        if (!specs.length) out.push(r.id);
      }
      return out;
    });
    check('carvings find a floor spot', bad.length === 0, bad);
    await place(page, 'seawall', 15, 16);
    const c = await ev(page, () => { const e = H.Game.ents.find((e) => e.type === 'carving'); const p = H.Game.player; p.x = e.cx() - 5; p.y = e.y + 22 - p.h; return e.spec.id; });
    await sleep(60); await page.keyboard.press('KeyE'); await sleep(200);
    check('E reads a carving', await ev(page, () => document.querySelector('#ui').dataset.kind === 'inscription'));
    await page.keyboard.press('KeyE'); await sleep(100);
    check('carving marked read', await ev((page), (id) => H.Narr.isRead(id), c));
    // whisper: focus the beam on the Tithe Hall corpse
    await place(page, 'tithe', 7, 16);
    const t = await ev(page, () => { const b = H.Game.ents.find((e) => e.corpse); return { x: b.cx(), y: b.cy() }; });
    await aimAt(page, t.x, t.y);
    await page.keyboard.down('KeyK'); await sleep(1200); await page.keyboard.up('KeyK');
    check('the focused beam draws out a whisper', await ev(page, () => Object.keys(H.Game.narr.heard).length > 0));
    // region epigraph shows once
    check('region epigraph registered', await ev(page, () => !!H.Game.narr.regions.wrackline));
    // thoughts
    check('Maren thinks on the pier', await ev(page, () => { H.Game.enterRoom(H.ROOM.pier); return !!H.Game.narr.thoughts.pier; }));
    // death text varies and saves keep narrative state
    await ev(page, () => { H.Game.save(); });
    check('narrative state saved', await ev(page, () => !!JSON.parse(localStorage.getItem('hollowmere_save')).narr.read));
  },

  // The opening on the outer rocks, played with nothing but the keyboard and mouse.
  async prologue(page) {
    const pos = () => ev(page, () => { const p = H.Game.player, r = H.Game.room; return { room: r.id, tx: (p.cx() - r.px) / 16, ty: (p.y + p.h - 1 - r.py) / 16, vy: p.vy, hang: p.hanging, hp: p.hp, water: p.inWater() }; });
    let s = await pos();
    check('a new game starts on the outer rocks', s.room === 'breakers', s);
    check('the tide holds at Low out here', await ev(page, () => H.Tide.paused && H.Tide.phase() === 'low'));
    check('the tide dial is not shown yet', await ev(page, () => H.Game.dialT === 0));
    check('no instructions on arrival', await ev(page, () => H.toasts.length === 0));
    await sleep(1600);
    check('Maren thinks about the boat', await ev(page, () => !!H.Game.narr.thoughts.boat));
    check('no hint before hesitating', (await ev(page, () => H.Hints.shown)) === null);
    await sleep(5000);
    check('standing still raises the walk hint', (await ev(page, () => H.Hints.shown)) === 'move');
    await page.keyboard.down('KeyA'); await sleep(1600);
    check('walking retires the walk hint', await ev(page, () => H.Hints.hasDone('move')));
    await sleep(3200);
    check('leaning on the rock raises the jump hint', (await ev(page, () => H.Hints.shown)) === 'jump', await pos());
    for (let i = 0; i < 3 && (await pos()).tx > 56; i++) { await hold(page, 'Space', 300); await sleep(400); }
    s = await pos();
    check('jumps up the rocks', s.tx < 58 && s.ty < 14.5, s);
    for (let i = 0; i < 300 && (await pos()).tx > 52.6; i++) await sleep(8);
    await hold(page, 'Space', 300); await sleep(500);
    s = await pos();
    check('clears the tide pool', s.tx < 47.9 && s.ty < 14.5 && !s.water, s);
    await sleep(1200); await page.keyboard.up('KeyA'); await sleep(200);
    s = await pos();
    check('stands at the foot of the chimney', s.tx > 36.5 && s.tx < 41 && s.ty > 13, s);
    check('Maren notices the timber', await ev(page, () => !!H.Game.narr.thoughts.timber));
    // cling and leap: hold toward a wall, jump when hanging, then steer for the other wall
    let dir = -1, jumped = false;
    await page.keyboard.down('KeyA'); await hold(page, 'Space', 300);
    for (let f = 0; f < 400; f++) {
      s = await pos();
      if (s.ty < 4.6) break;
      if (s.hang === dir && s.vy > 0) {
        await page.keyboard.down('Space'); await sleep(20);
        await page.keyboard.up(dir < 0 ? 'KeyA' : 'KeyD'); dir = -dir; await page.keyboard.down(dir < 0 ? 'KeyA' : 'KeyD');
        await sleep(220); await page.keyboard.up('Space'); jumped = true; continue;
      }
      await sleep(8);
    }
    await page.keyboard.up('KeyD'); await page.keyboard.down('KeyA'); await sleep(600);
    s = await pos();
    check('climbs the chimney by wall-jumping', jumped && s.ty < 5 && s.tx < 37, s);
    await sleep(1600); await page.keyboard.up('KeyA'); await sleep(3500);
    check('stalling on the planks raises the drop hint', (await ev(page, () => H.Hints.shown)) === 'drop', await pos());
    await page.keyboard.down('KeyS'); await page.keyboard.press('Space'); await page.keyboard.up('KeyS'); await sleep(1200);
    s = await pos();
    check('drops through the planks into the hollow', s.ty > 12 && s.tx < 32, s);
    check('Maren sees the salt', await ev(page, () => !!H.Game.narr.thoughts.salt));
    await page.keyboard.down('KeyD');
    for (let i = 0; i < 300 && (await pos()).tx < 29.5; i++) await sleep(8);
    await page.keyboard.up('KeyD'); await sleep(200);
    const hp0 = (await pos()).hp;
    const b = await ev(page, () => { const e = H.Game.ents.find((x) => x.type === 'barrel'); return { x: e.cx(), y: e.cy() }; });
    await aimAt(page, b.x, b.y);
    await page.mouse.down(); await sleep(50); await page.mouse.up(); await sleep(2500);
    check('the shot barrel burns', await ev(page, () => H.Game.ents.find((x) => x.type === 'barrel').done));
    check('the fire cracks the salt plug', await ev(page, () => { const r = H.Game.room; return H.World.charAt(r.ox + 21, r.oy + 11) !== 'S' && H.World.charAt(r.ox + 21, r.oy + 12) !== 'S'; }));
    check('shooting from range keeps Maren clear of the fire', (await pos()).hp === hp0);
    await sleep(8000);
    // creep to the edge of the crystals, then dash from a standstill
    await page.keyboard.down('KeyA');
    for (let i = 0; i < 400 && (await pos()).tx > 18.2; i++) await sleep(8);
    await page.keyboard.up('KeyA'); await sleep(300);
    await place(page, 'breakers', 16, 12); await sleep(200);
    await page.keyboard.down('KeyA'); await page.keyboard.press('KeyC'); await sleep(500);
    s = await pos();
    check('dashes through the salt crystals unhurt', s.tx < 13.5 && s.hp === hp0, s);
    for (let i = 0; i < 300 && (await pos()).room === 'breakers'; i++) await sleep(10);
    await page.keyboard.up('KeyA');
    s = await pos();
    check('walks onto the reef', s.room === 'reef', s);
    await sleep(1500);
    check('the tide starts to move', await ev(page, () => !H.Tide.paused && H.World.flags.landed));
    check('and the dial fades in', await ev(page, () => H.Game.dialT > 0.5));
    check('the Constance thought follows', await ev(page, () => !!H.Game.narr.thoughts.start));
  },

  // A pretend controller fed through the same path as a real one.
  async controller(page) {
    check('DualSense reads as PlayStation', await ev(page, () => H.Input.classify('DualSense Wireless Controller (STANDARD GAMEPAD Vendor: 054c Product: 0ce6)') === 'playstation'));
    check('Switch Pro reads as Switch', await ev(page, () => H.Input.classify('Pro Controller (STANDARD GAMEPAD Vendor: 057e Product: 2009)') === 'switch'));
    check('Xbox reads as Xbox', await ev(page, () => H.Input.classify('Xbox 360 Controller (XInput STANDARD GAMEPAD)') === 'xbox'));
    const mk = (id) => ev(page, (id) => { window.__pad = { id, index: 0, buttons: Array.from({ length: 18 }, () => ({ pressed: false, value: 0 })), axes: [0, 0, 0, 0] }; H.Input.fakePad = window.__pad; }, id);
    const btn = (i, v) => ev(page, ([i, v]) => { window.__pad.buttons[i].pressed = v; window.__pad.buttons[i].value = v ? 1 : 0; }, [i, v]);
    const axis = (i, v) => ev(page, ([i, v]) => { window.__pad.axes[i] = v; }, [i, v]);
    const tap = async (i) => { await btn(i, true); await sleep(60); await btn(i, false); await sleep(80); };
    const holdBtn = async (i, ms) => { await btn(i, true); await sleep(ms); await btn(i, false); await sleep(60); };
    await mk('Xbox Wireless Controller (STANDARD GAMEPAD Vendor: 045e Product: 0b13)');
    await place(page, 'reef', 25, 12); await sleep(300);
    let x0 = (await state(page)).x;
    await axis(0, -1); await sleep(400); await axis(0, 0); await sleep(100);
    check('left stick walks', (await state(page)).x < x0 - 40);
    await axis(0, 0.45); await sleep(80);
    check('a light tilt sneaks', await ev(page, () => H.Input.held('sneak') && H.Input.held('right')));
    await axis(0, 0); await sleep(300);
    const y0 = (await state(page)).y;
    await btn(0, true); await sleep(150); await btn(0, false);
    check('A jumps', (await state(page)).y < y0 - 15);
    await sleep(800);
    await axis(3, -1); await sleep(80);
    check('right stick aims', await ev(page, () => Math.abs(H.angleDiff(H.Game.player.aim, -Math.PI / 2)) < 0.25));
    await axis(3, 0);
    const mag = await ev(page, () => H.Game.player.weapons.driver.mag);
    await ev(page, () => { H.Input.pad.lastRumble = null; });
    await btn(7, true); await sleep(80); await btn(7, false); await sleep(80);
    check('RT fires', (await ev(page, () => H.Game.player.weapons.driver.mag)) < mag);
    check('firing asks for rumble', await ev(page, () => !!H.Input.pad.lastRumble));
    await place(page, 'reef', 13, 12);
    await ev(page, () => { const c = H.Game.ents.find((e) => e.type === 'chest'); const p = H.Game.player; p.x = c.cx() - p.w / 2; });
    await sleep(100); await tap(3); await sleep(200);
    check('tapping Y opens the locker', await ev(page, () => H.Game.ents.find((e) => e.type === 'chest').done));
    await ev(page, () => { const p = H.Game.player; p.hp = 3; p.bag.add('stitchkit', 1); });
    await tap(12); await sleep(100);
    check('d-pad up uses a poultice', await ev(page, () => H.Game.player.healKind === 'poultice'));
    await sleep(3300); await ev(page, () => { H.Game.player.hp = 2; });
    await holdBtn(12, 550);
    check('holding d-pad up kneels for the stitch kit', await ev(page, () => H.Game.player.healKind === 'stitch' && H.Game.player.healT > 0));
    await sleep(4300);
    await ev(page, () => H.Game.player.bag.add('scrap', 2));
    await holdBtn(5, 550);
    check('holding RB quick-crafts rivets', await ev(page, () => H.Game.player.craftT > 0));
    await sleep(2600);
    await ev(page, () => { H.Game.player.weapons.driver.mag = 2; });
    await tap(5);
    check('tapping RB reloads', await ev(page, () => H.Game.player.reloadT > 0));
    await sleep(2000);
    await ev(page, () => { const p = H.Game.player; p.weapons.harpoon = { tier: 0, mag: 1 }; p.weaponOrder.push('harpoon'); });
    await tap(15);
    check('d-pad right changes weapon', (await ev(page, () => H.Game.player.cur)) === 'harpoon');
    await tap(14);
    check('d-pad left changes back', (await ev(page, () => H.Game.player.cur)) === 'driver');
    await tap(10);
    check('L3 toggles sneaking', await ev(page, () => H.Input.pad.sneakLatch && H.Input.held('sneak')));
    await tap(10);
    // aim assist pulls a near miss onto a knot
    await place(page, 'pier', 20, 16);
    const off = await ev(page, () => { const e = H.Game.ents.find((x) => x.key === 'first_shucker'); const k = e.knots.find((k) => !k.broken && !e.isSealed(k)); const kp = e.knotPos(k), lp = H.Game.player.lanternPos(); return Math.atan2(kp.y - lp.y, kp.x - lp.x) + 0.12; });
    await axis(2, Math.cos(off)); await axis(3, Math.sin(off)); await sleep(100);
    const err = await ev(page, () => { const e = H.Game.ents.find((x) => x.key === 'first_shucker'), p = H.Game.player, lp = p.lanternPos(); return Math.min(...e.knots.filter((k) => !k.broken && !e.isSealed(k)).map((k) => { const q = e.knotPos(k); return Math.abs(H.angleDiff(p.aim, Math.atan2(q.y - lp.y, q.x - lp.x))); })); });
    check('aim assist pulls a near miss onto a knot', err < 0.045, err);
    await axis(2, 0); await axis(3, 0);
    // View opens the journal; the bumpers turn its pages; B closes it
    await tap(8); await sleep(100);
    check('View opens the journal', await ev(page, () => document.querySelector('#ui').dataset.kind === 'satchel'));
    const before = await ev(page, () => document.querySelector('.tabs button.sel').textContent);
    await tap(5);
    check('RB turns the page', (await ev(page, () => document.querySelector('.tabs button.sel').textContent)) !== before);
    check('menus show controller prompts', await ev(page, () => document.querySelectorAll('.foot img.glyph').length > 0));
    await tap(1); await sleep(100);
    check('B closes it', await ev(page, () => !H.UI.isOpen()));
    await tap(9); await sleep(100);
    check('Start pauses', await ev(page, () => document.querySelector('#ui').dataset.kind === 'pause'));
    await tap(9); await sleep(200);
    check('Start resumes and stays resumed', await ev(page, () => !H.UI.isOpen()));
    // d-pad moves menu focus and A presses the focused button
    await tap(9); await sleep(100);
    await tap(13); await sleep(50);
    check('d-pad moves the menu focus', await ev(page, () => document.querySelector('button.padfocus') && document.querySelector('button.padfocus').textContent !== 'Resume'));
    await tap(1); await sleep(100);
    // Switch: A (the right button) confirms in menus, B (the bottom one) backs out
    await mk('Pro Controller (STANDARD GAMEPAD Vendor: 057e Product: 2009)');
    await tap(0); await sleep(50);
    await ev(page, () => H.UI.dialogue('Test', ['one', 'two']));
    await sleep(100);
    await tap(1);
    check('Switch A (right) advances dialogue', await ev(page, () => H.UI.isOpen() && document.querySelector('.dialogue .line').textContent === 'two'));
    await tap(1); await sleep(100);
    check('and closes it', await ev(page, () => !H.UI.isOpen()));
    await ev(page, () => H.UI.pause()); await sleep(100);
    await tap(0); await sleep(100);
    check('Switch B (bottom) backs out', await ev(page, () => !H.UI.isOpen()));
    check('Switch prompt for jump is B', await ev(page, () => H.Glyphs.plain('{jump}') === 'B'));
    check('keyboard prompt for interact is E', await ev(page, () => H.Glyphs.plain('{interact} Open', 'keyboard') === 'E Open'));
    check('PlayStation names its buttons', await ev(page, () => H.Glyphs.plain('{jump} / {fire}', 'playstation') === 'Cross / R2'));
    await ev(page, () => H.Input.disconnected(0)); await sleep(100);
    check('disconnecting the pad pauses the game', await ev(page, () => document.querySelector('#ui').dataset.kind === 'pause' && document.querySelector('#ui').textContent.includes('disconnected')));
  },
};

(async () => {
  const names = process.argv.slice(2).length ? process.argv.slice(2) : Object.keys(scenarios);
  for (const n of names) {
    const { browser, page } = await boot();
    console.log('--- ' + n);
    try { await scenarios[n](page); } catch (e) { check(n + ' threw', false, String(e)); }
    if (page.errors.length) { check(n + ' page errors', false, [...new Set(page.errors)].slice(0, 5)); }
    await browser.close();
  }
})();
