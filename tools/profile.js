// CPU profile of N frames in a room via the DevTools protocol; prints the top functions by self time.
// Usage: node tools/profile.js [room] [frames]
'use strict';
const { chromium } = require('playwright');
const path = require('path');
const room = process.argv[2] || 'lair';
const frames = +(process.argv[3] || 300);
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  await page.goto('file://' + path.join(__dirname, '..', 'index.html'));
  await page.waitForTimeout(300);
  await page.click('button[data-act="newgame"]'); await page.waitForTimeout(100);
  await page.keyboard.press('Escape'); await page.waitForTimeout(100);
  await page.evaluate((room) => {
    const G = H.Game, r = H.ROOM[room]; G.god = true; H.Tide.setPhase('high', 30); G.enterRoom(r);
    let best = null;
    for (let ty = r.th - 2; ty > 1 && !best; ty--) for (let tx = 2; tx < r.tw - 2; tx++) if (H.World.solid(r.ox + tx, r.oy + ty + 1) === 1 && H.World.solid(r.ox + tx, r.oy + ty) === 0 && H.World.solid(r.ox + tx, r.oy + ty - 1) === 0) { best = [tx, ty]; break; }
    G.player.x = r.px + best[0] * 16 + 3; G.player.y = r.py + (best[1] + 1) * 16 - G.player.h; G.snapCamera();
    for (const e of G.ents) if (e.wake) e.wake(true);
    H.Input.held = (a) => a === 'focus';
  }, room);
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('Profiler.enable');
  await cdp.send('Profiler.setSamplingInterval', { interval: 100 });
  await cdp.send('Profiler.start');
  const ms = await page.evaluate((frames) => { const t = performance.now(); for (let i = 0; i < frames; i++) H.Game._step(1 / 60); return performance.now() - t; }, frames);
  const { profile } = await cdp.send('Profiler.stop');
  const self = new Map();
  const dt = profile.timeDeltas;
  const byId = new Map(profile.nodes.map((n) => [n.id, n]));
  profile.samples.forEach((id, i) => { const n = byId.get(id); const k = `${n.callFrame.functionName || '(anon)'} ${path.basename(n.callFrame.url || '')}:${n.callFrame.lineNumber + 1}`; self.set(k, (self.get(k) || 0) + (dt[i] || 0)); });
  const total = [...self.values()].reduce((a, b) => a + b, 0);
  console.log(`${room}: ${frames} frames in ${ms.toFixed(0)} ms (${(ms / frames).toFixed(2)} ms/frame)`);
  [...self.entries()].sort((a, b) => b[1] - a[1]).slice(0, 18).forEach(([k, v]) => console.log(`${(100 * v / total).toFixed(1).padStart(5)}%  ${k}`));
  await browser.close();
})();
