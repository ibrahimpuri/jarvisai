// Axis-separated AABB movement against the tile grid and moving platforms.
'use strict';
H.Phys = (() => {
  const TS = H.TILE;
  const W = () => H.World;

  // Dynamic platforms registered each frame by floaters, barges and lifts: {x,y,w,h,oneWay,dy}
  let platforms = [];
  function setPlatforms(list) { platforms = list; }

  function boxSolid(x, y, w, h) {
    const x0 = Math.floor(x / TS), x1 = Math.floor((x + w - 0.01) / TS);
    const y0 = Math.floor(y / TS), y1 = Math.floor((y + h - 0.01) / TS);
    for (let ty = y0; ty <= y1; ty++) for (let tx = x0; tx <= x1; tx++) if (W().solid(tx, ty) === 1) return true;
    for (const p of platforms) if (!p.oneWay && x < p.x + p.w && x + w > p.x && y < p.y + p.h && y + h > p.y) return true;
    return false;
  }

  function move(e, dt) {
    e.onGround = false; e.hitWall = 0; e.hitCeil = false; e.platform = null;
    const total = Math.max(Math.abs(e.vx * dt), Math.abs(e.vy * dt));
    const steps = Math.max(1, Math.ceil(total / 6));
    const sdt = dt / steps;
    for (let i = 0; i < steps; i++) {
      // X axis
      const nx = e.x + e.vx * sdt;
      if (!boxSolid(nx, e.y, e.w, e.h)) e.x = nx;
      else {
        // Step up a small lip (half a tile) when walking on the ground.
        if (e.stepUp && e.wasGround && !boxSolid(nx, e.y - 7, e.w, e.h) ) { e.x = nx; e.y -= 7; }
        else {
          if (e.vx > 0) e.x = Math.floor((nx + e.w) / TS) * TS - e.w - 0.01;
          else if (e.vx < 0) e.x = Math.floor(nx / TS) * TS + TS + 0.01;
          e.hitWall = Math.sign(e.vx); e.vx = 0;
          if (boxSolid(e.x, e.y, e.w, e.h)) e.x -= e.hitWall * 0.5; // safety
        }
      }
      // Y axis
      const ny = e.y + e.vy * sdt;
      if (e.vy >= 0) {
        const prevBottom = e.y + e.h;
        const newBottom = ny + e.h;
        let landY = null;
        const x0 = Math.floor(e.x / TS), x1 = Math.floor((e.x + e.w - 0.01) / TS);
        const ty0 = Math.floor(prevBottom / TS), ty1 = Math.floor((newBottom - 0.01) / TS);
        for (let ty = Math.max(ty0, Math.floor(e.y / TS)); ty <= ty1 && landY === null; ty++) {
          for (let tx = x0; tx <= x1; tx++) {
            const s = W().solid(tx, ty);
            if (s === 1 && ty * TS >= e.y + e.h - 0.5) { landY = ty * TS; break; }
            if (s === 2 && !e.dropping && prevBottom <= ty * TS + 0.5) { landY = ty * TS; break; }
          }
        }
        for (const p of platforms) {
          if (e.x < p.x + p.w && e.x + e.w > p.x && prevBottom <= p.y + 1 && newBottom >= p.y && !(p.oneWay && e.dropping)) {
            if (landY === null || p.y < landY) { landY = p.y; e.platform = p; }
          }
        }
        if (landY !== null && newBottom >= landY) {
          e.y = landY - e.h; e.vy = 0; e.onGround = true;
        } else if (boxSolid(e.x, ny, e.w, e.h)) {
          e.vy = 0;
        } else e.y = ny;
      } else {
        if (!boxSolid(e.x, ny, e.w, e.h)) e.y = ny;
        else { e.y = Math.floor(ny / TS) * TS + TS + 0.01; e.vy = 0; e.hitCeil = true; }
      }
    }
    // Ground probe so standing still still counts as grounded.
    if (!e.onGround && e.vy >= 0) {
      const by = e.y + e.h + 1;
      const x0 = Math.floor(e.x / TS), x1 = Math.floor((e.x + e.w - 0.01) / TS);
      const ty = Math.floor(by / TS);
      for (let tx = x0; tx <= x1; tx++) {
        const s = W().solid(tx, ty);
        if ((s === 1 || (s === 2 && !e.dropping)) && Math.abs(ty * TS - (e.y + e.h)) < 1.5) { e.onGround = true; break; }
      }
      for (const p of platforms) {
        if (e.x < p.x + p.w && e.x + e.w > p.x && Math.abs(p.y - (e.y + e.h)) < 1.5) { e.onGround = true; e.platform = p; }
      }
    }
    e.wasGround = e.onGround;
  }

  // Line of sight between two world points (tiles only).
  function los(x1, y1, x2, y2) {
    const d = Math.hypot(x2 - x1, y2 - y1);
    const n = Math.ceil(d / 6);
    for (let i = 1; i < n; i++) {
      const t = i / n;
      if (W().solidPx(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t)) return false;
    }
    return true;
  }

  // March a ray through tiles; returns hit distance or maxD.
  function ray(x, y, ang, maxD) {
    const dx = Math.cos(ang), dy = Math.sin(ang);
    for (let d = 0; d < maxD; d += 4) {
      if (W().solidPx(x + dx * d, y + dy * d)) return d;
    }
    return maxD;
  }

  return { move, boxSolid, los, ray, setPlatforms, get platforms() { return platforms; } };
})();
