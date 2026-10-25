// Headless smoke test: boots the game, starts a run, drives a few seconds of input,
// and reports console errors. Usage: node tools/smoke.js [outdir]
'use strict';
const { chromium } = require('playwright');
const path = require('path');
const out = process.argv[2] || '.';
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(String(e.stack || e)));
  await page.goto('file://' + path.join(__dirname, '..', 'index.html'));
  await page.waitForTimeout(800);
  await page.screenshot({ path: path.join(out, 'title.png') });
  await page.click('button[data-act="newgame"]');
  await page.waitForTimeout(300);
  await page.keyboard.press('KeyE');
  await page.waitForTimeout(500);
  await page.screenshot({ path: path.join(out, 'start.png') });
  await page.keyboard.down('KeyA'); await page.waitForTimeout(1500); await page.keyboard.up('KeyA');
  await page.keyboard.press('Space'); await page.waitForTimeout(600);
  await page.screenshot({ path: path.join(out, 'moved.png') });
  console.log(JSON.stringify(await page.evaluate(() => ({ room: H.Game.room.id, x: H.Game.player.x, y: H.Game.player.y, hp: H.Game.player.hp, phase: H.Tide.phase() }))));
  console.log(errors.length ? 'ERRORS:\n' + errors.join('\n') : 'no errors');
  await browser.close();
})();
