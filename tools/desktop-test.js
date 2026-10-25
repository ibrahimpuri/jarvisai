// Launches the packaged desktop build, plays briefly, checks the desktop bridge, fullscreen,
// GPU status and that a save survives a restart. Usage: node tools/desktop-test.js <path-to-executable> [outdir]
'use strict';
let pw;
try { pw = require('playwright'); } catch (e) { pw = require('playwright-core'); }
const electron = pw._electron;
const path = require('path');
const os = require('os');
const fs = require('fs');
const exe = process.argv[2];
const out = process.argv[3] || '.';
const userData = fs.mkdtempSync(path.join(os.tmpdir(), 'hm-desktop-'));
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let fails = 0;
const check = (n, c, i) => { console.log((c ? 'PASS ' : 'FAIL ') + n + (i !== undefined ? '  ' + JSON.stringify(i) : '')); if (!c) fails++; };

async function launch() {
  const app = await electron.launch({ executablePath: exe, args: ['--no-sandbox', `--user-data-dir=${userData}`] });
  const win = await app.firstWindow();
  await win.waitForLoadState('domcontentloaded');
  await sleep(1200);
  return { app, win };
}

(async () => {
  let { app, win } = await launch();
  const errors = [];
  win.on('pageerror', (e) => errors.push(e.message));
  check('window title', (await win.title()) === 'Hollowmere', await win.title());
  check('loads the minified build', await win.evaluate(() => !!document.querySelector('script[src="game.min.js"]')));
  check('desktop bridge present', await win.evaluate(() => !!window.hollowmereDesktop));
  check('starts fullscreen', await win.evaluate(() => window.hollowmereDesktop.isFullscreen()));
  check('title has desktop buttons', await win.evaluate(() => !!document.querySelector('button[data-act="exit"]') && !!document.querySelector('button[data-act="fullscreen"]')));
  check('no Node access in the page', await win.evaluate(() => typeof require === 'undefined' && typeof process === 'undefined'));
  const gpu = await app.evaluate(async ({ app }) => app.getGPUFeatureStatus());
  console.log('     GPU feature status: ' + JSON.stringify({ canvas: gpu['2d_canvas'], gpu_compositing: gpu.gpu_compositing, rasterization: gpu.rasterization }));
  await win.click('button[data-act="fullscreen"]');
  await sleep(600);
  check('fullscreen toggles off', !(await win.evaluate(() => window.hollowmereDesktop.isFullscreen())));
  // play a little, then save at the chapel bell
  await win.click('button[data-act="newgame"]'); await sleep(200);
  await win.keyboard.press('KeyE'); await sleep(200);
  await win.keyboard.down('KeyA'); await sleep(1500); await win.keyboard.up('KeyA');
  const st = await win.evaluate(() => ({ room: H.Game.room.id, x: Math.round(H.Game.player.x) }));
  check('game runs and takes input', st.room === 'reef' || st.room === 'pier', st);
  await win.evaluate(() => {
    const G = H.Game, r = H.ROOM.chapel; G.enterRoom(r); G.player.x = r.px + 20 * 16 + 3; G.player.y = r.py + 17 * 16 - G.player.h; G.snapCamera();
    G.player.bag.add('oil', 1); G.player.bag.add('scrap', 5);
  });
  await win.keyboard.press('KeyE'); await sleep(300);
  await win.click('button[data-act="save"]'); await sleep(200);
  await win.screenshot({ path: path.join(out, 'desktop_bell.png') });
  await app.close();
  // restart: fullscreen preference and save should both persist
  ({ app, win } = await launch());
  check('fullscreen preference remembered', !(await win.evaluate(() => window.hollowmereDesktop.isFullscreen())));
  check('Continue offered after restart', await win.evaluate(() => !!document.querySelector('button[data-act="cont"]')));
  await win.click('button[data-act="cont"]'); await sleep(400);
  const r = await win.evaluate(() => ({ room: H.Game.room.id, scrap: H.Game.player.bag.count('scrap') }));
  check('save loads in the desktop app', r.room === 'chapel' && r.scrap === 5, r);
  await win.screenshot({ path: path.join(out, 'desktop_continue.png') });
  // quit button closes the app
  const closed = new Promise((res) => app.process().once('exit', () => res(true)));
  await win.evaluate(() => { H.UI.pause(); });
  await sleep(200);
  await win.click('button[data-act="exit"]').catch(() => {});
  check('Quit to desktop exits', await Promise.race([closed, sleep(5000).then(() => false)]));
  check('no page errors', errors.length === 0, errors.slice(0, 3));
  fs.rmSync(userData, { recursive: true, force: true });
  process.exit(fails ? 1 : 0);
})();
