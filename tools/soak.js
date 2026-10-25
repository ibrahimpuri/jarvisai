// Soak test: a random bot wanders every room with enemies awake across tide phases,
// firing, striking and dashing, while we watch for runtime errors. Usage: node tools/soak.js [secondsPerRoom]
'use strict';
const { chromium } = require('playwright');
const path = require('path');
const per = +(process.argv[2] || 6);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errors = new Map();
  page.on('pageerror', (e) => { const k = String(e.stack || e).split('\n').slice(0, 3).join(' | '); errors.set(k, (errors.get(k) || 0) + 1); });
  await page.goto('file://' + path.join(__dirname, '..', 'index.html'));
  await sleep(300);
  await page.click('button[data-act="newgame"]'); await sleep(100);
  await page.keyboard.press('KeyE');
  const rooms = await page.evaluate(() => H.ROOMS.map((r) => r.id));
  const keys = ['KeyA', 'KeyD', 'KeyW', 'KeyS', 'Space', 'KeyF', 'KeyC', 'KeyQ', 'KeyT', 'KeyR', 'KeyX', 'KeyZ', 'Digit1', 'Digit2', 'Digit3', 'Digit4', 'KeyV', 'KeyB'];
  let i = 0;
  for (const id of rooms) {
    const phase = ['low', 'rising', 'high', 'ebbing'][i++ % 4];
    await page.evaluate(([id, phase, act]) => {
      const G = H.Game, p = G.player;
      G.god = true; G.act = act; H.Tide.spring = act >= 3 ? 2 : 0;
      ['boots', 'jet', 'tidekey', 'seal', 'lens'].forEach((a) => p.abilities.add(a));
      ['harpoon', 'sling', 'sawline'].forEach((w) => { if (!p.weapons[w]) { p.weapons[w] = { tier: 2, mag: 1 }; p.weaponOrder.push(w); } });
      p.weapons.driver.tier = 2; p.bag.size = 16; p.bag.add('rivets', 50); p.bag.add('bolts', 6); p.bag.add('flares', 3); p.bag.add('spools', 3); p.bag.add('scrap', 5);
      H.Tide.setPhase(phase, 30);
      const r = H.ROOM[id];
      G.enterRoom(r);
      let best = null;
      for (let ty = r.th - 2; ty > 1 && !best; ty--) for (let tx = 2; tx < r.tw - 2; tx++) if (H.World.solid(r.ox + tx, r.oy + ty + 1) === 1 && H.World.solid(r.ox + tx, r.oy + ty) === 0 && H.World.solid(r.ox + tx, r.oy + ty - 1) === 0) { best = [tx, ty]; break; }
      p.x = r.px + best[0] * 16 + 3; p.y = r.py + (best[1] + 1) * 16 - p.h; p.dead = false; p.hp = p.maxHp;
      G.snapCamera();
      for (const e of G.ents) if (e.isBody && e.wake) e.wake(true);
      G.sinceLoad = 11;
      if (H.UndertowSystem.sea().includes(id)) G.undertow.room = id;
    }, [id, phase, i > 12 ? 3 : 2]);
    const end = Date.now() + per * 1000;
    while (Date.now() < end) {
      const k = keys[Math.floor(Math.random() * keys.length)];
      await page.keyboard.down(k);
      await page.mouse.move(Math.random() * 1280, Math.random() * 720);
      if (Math.random() < 0.5) { await page.mouse.down({ button: Math.random() < 0.7 ? 'left' : 'right' }); }
      await sleep(80 + Math.random() * 200);
      await page.mouse.up({ button: 'left' }); await page.mouse.up({ button: 'right' });
      await page.keyboard.up(k);
      if (await page.evaluate(() => H.UI.isOpen())) await page.keyboard.press('Escape');
      if (await page.evaluate(() => H.UI.isOpen())) await page.evaluate(() => H.UI.close());
    }
    const st = await page.evaluate(() => ({ room: H.Game.room.id, ents: H.Game.ents.length, fps: 0 }));
    process.stdout.write(`${id}(${phase})->${st.room} `);
  }
  console.log('\n' + (errors.size ? 'ERRORS:\n' + [...errors].map(([k, n]) => n + 'x ' + k).join('\n') : 'no errors'));
  await browser.close();
})();
