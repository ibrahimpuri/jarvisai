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
  await page.keyboard.press('KeyE');
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
    let s = await state(page);
    check('starts on reef deck', s.room === 'reef' && s.ground, s);
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
    await ev(page, () => { H.Game.god = true; const p = H.Game.player; ['boots', 'jet'].forEach((a) => p.abilities.add(a)); });
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
    const end = await ev(page, () => ({ kind: document.querySelector('#ui').dataset.kind, ended: H.Game.ended, text: document.querySelector('#ui').innerText.slice(0, 60) }));
    check('Keeper\'s Dark ending plays', end.kind === 'ending' && end.ended === 'dark', end);
    await page.screenshot({ path: require('path').join(OUT, 'play_ending.png') });
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
