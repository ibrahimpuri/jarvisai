// Visual tour: teleports through rooms and screenshots each. Usage: node tools/tour.js outdir [roomIds...]
'use strict';
const { chromium } = require('playwright');
const path = require('path');
const out = process.argv[2] || '.';
const only = process.argv.slice(3);
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e.stack || e)));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  await page.goto('file://' + path.join(__dirname, '..', 'index.html'));
  await page.waitForTimeout(500);
  await page.click('button[data-act="newgame"]');
  await page.waitForTimeout(200);
  await page.keyboard.press('KeyE');
  await page.mouse.move(1100, 360);
  const ids = only.length ? only : await page.evaluate(() => H.ROOMS.map((r) => r.id));
  for (const id of ids) {
    await page.evaluate((id) => {
      const G = H.Game, r = H.ROOM[id];
      G.god = true;
      G.enterRoom(r);
      const p = G.player;
      // find an open spot near the middle bottom
      const spots = [];
      for (let ty = r.th - 2; ty > 1; ty--) for (let tx = 1; tx < r.tw - 1; tx++) {
        const wx = r.ox + tx, wy = r.oy + ty;
        if (H.World.solid(wx, wy + 1) === 1 && H.World.solid(wx, wy) === 0 && H.World.solid(wx, wy - 1) === 0) spots.push([tx, ty]);
      }
      spots.sort((a, b) => Math.abs(a[0] - r.tw / 2) - Math.abs(b[0] - r.tw / 2));
      const s = spots[0] || [r.tw / 2, r.th / 2];
      p.x = r.px + s[0] * 16 + 3; p.y = r.py + (s[1] + 1) * 16 - p.h;
      G.snapCamera();
    }, id);
    await page.waitForTimeout(400);
    await page.screenshot({ path: path.join(out, 'room_' + id + '.png') });
  }
  console.log(errors.length ? 'ERRORS:\n' + [...new Set(errors)].join('\n') : 'no errors');
  await browser.close();
})();
