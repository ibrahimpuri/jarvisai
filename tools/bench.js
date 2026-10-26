// Frame-time benchmark: in every room, with bodies awake, High tide and the focused beam on,
// time the update and the render separately. Usage: node tools/bench.js [frames]
'use strict';
const { chromium } = require('playwright');
const path = require('path');
const frames = +(process.argv[2] || 150);
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  await page.goto('file://' + path.join(__dirname, '..', 'index.html'));
  await page.waitForTimeout(300);
  await page.click('button[data-act="newgame"]'); await page.waitForTimeout(100);
  await page.keyboard.press('Escape'); await page.waitForTimeout(100);
  const res = await page.evaluate((frames) => {
    const G = H.Game, out = [];
    G.god = true;
    const draw = H.Render.draw;
    let drawMs = 0, dirty = 0;
    const md = H.Render.markDirty;
    H.Render.markDirty = () => { dirty++; md(); };
    H.Render.draw = () => { const t = performance.now(); draw(); drawMs += performance.now() - t; };
    for (const r of H.ROOMS) {
      H.Tide.setPhase('high', 30);
      G.enterRoom(r);
      let best = null;
      for (let ty = r.th - 2; ty > 1 && !best; ty--) for (let tx = 2; tx < r.tw - 2; tx++) if (H.World.solid(r.ox + tx, r.oy + ty + 1) === 1 && H.World.solid(r.ox + tx, r.oy + ty) === 0 && H.World.solid(r.ox + tx, r.oy + ty - 1) === 0) { best = [tx, ty]; break; }
      G.player.x = r.px + best[0] * 16 + 3; G.player.y = r.py + (best[1] + 1) * 16 - G.player.h;
      G.snapCamera();
      for (const e of G.ents) if (e.wake) e.wake(true);
      H.Input.held = (a) => a === 'focus';
      for (let i = 0; i < 20; i++) G._step(1 / 60); // warm up
      drawMs = 0; dirty = 0;
      const times = [];
      for (let i = 0; i < frames; i++) { const t = performance.now(); G._step(1 / 60); times.push(performance.now() - t); }
      times.sort((a, b) => a - b);
      const avg = times.reduce((a, b) => a + b, 0) / frames;
      out.push({ room: r.id, avg, med: times[frames >> 1], p95: times[Math.floor(frames * 0.95)], max: times[frames - 1], draw: drawMs / frames, dirty });
    }
    return out;
  }, frames);
  let ta = 0, td = 0;
  for (const r of res) { ta += r.avg; td += r.draw; console.log(`${r.room.padEnd(14)} frame ${r.avg.toFixed(2)} ms  median ${r.med.toFixed(2)}  p95 ${r.p95.toFixed(2)}  max ${r.max.toFixed(1)}  render ${r.draw.toFixed(2)}  update ${(r.avg - r.draw).toFixed(2)}  rebuilds ${r.dirty}`); }
  console.log(`AVERAGE frame ${(ta / res.length).toFixed(2)} ms (render ${(td / res.length).toFixed(2)}, update ${((ta - td) / res.length).toFixed(2)}); median ${(res.reduce((a, r) => a + r.med, 0) / res.length).toFixed(2)} ms; worst p95 ${Math.max(...res.map((r) => r.p95)).toFixed(2)} ms`);
  await browser.close();
})();
