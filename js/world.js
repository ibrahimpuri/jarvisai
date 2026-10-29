// World: the room grid, tile queries in world space, water, and per-room persistence.
'use strict';
H.World = (() => {
  const TS = H.TILE;
  // Room lookup is a flat array over the screen grid; each room keeps a flat live tile array
  // (its map with persistent changes applied) so tile queries never build string keys.
  let grid = [], GRID_W = 1, GRID_H = 1;
  let flags = {};           // global world flags (gates opened, puzzles solved, quest state)
  let states = {};          // per-room persistent state

  function build() {
    for (const r of H.ROOMS) {
      r.w = r.w || 1; r.h = r.h || 1;
      r.tw = r.w * H.SCREEN_W; r.th = r.h * H.SCREEN_H;
      r.ox = r.col * H.SCREEN_W; r.oy = r.row * H.SCREEN_H;
      r.px = r.ox * TS; r.py = r.oy * TS; r.pw = r.tw * TS; r.ph = r.th * TS;
      GRID_W = Math.max(GRID_W, r.col + r.w); GRID_H = Math.max(GRID_H, r.row + r.h);
      // Strip entity characters out of the tile layer.
      r.tiles = r.map.map((row) => row.split('').map((ch) => (r.ents && r.ents[ch] ? ' ' : ch)));
      r.base = [].concat(...r.tiles);
      r.live = r.base.slice();
      r.bellRoom = !!r.bellRoom;
    }
    grid = new Array(GRID_W * GRID_H).fill(null);
    for (const r of H.ROOMS) for (let c = 0; c < r.w; c++) for (let k = 0; k < r.h; k++) grid[(r.row + k) * GRID_W + r.col + c] = r;
  }
  // Rebuild every room's live tiles from its map plus the saved changes.
  function refreshLive() {
    for (const r of H.ROOMS) {
      r.live = r.base.slice();
      const st = states[r.id];
      if (st && st.tiles) for (const k in st.tiles) { const i = k.indexOf(','); r.live[(+k.slice(i + 1)) * r.tw + (+k.slice(0, i))] = st.tiles[k]; }
    }
    if (H.Render) H.Render.markDirty();
  }

  function reset() { flags = {}; states = {}; refreshLive(); }

  function roomAtTile(tx, ty) {
    const c = Math.floor(tx / H.SCREEN_W), r = Math.floor(ty / H.SCREEN_H);
    if (c < 0 || r < 0 || c >= GRID_W || r >= GRID_H) return null;
    return grid[r * GRID_W + c];
  }
  function roomAtPx(x, y) { return roomAtTile(Math.floor(x / TS), Math.floor(y / TS)); }

  function state(id) {
    if (!states[id]) states[id] = { tiles: {}, ents: {}, seen: {}, visited: false, explored: false };
    return states[id];
  }

  function charAt(tx, ty) {
    const r = roomAtTile(tx, ty);
    if (!r) return '#';
    return r.live[(ty - r.oy) * r.tw + (tx - r.ox)];
  }
  function setChar(tx, ty, ch) {
    const r = roomAtTile(tx, ty);
    if (!r) return;
    state(r.id).tiles[(tx - r.ox) + ',' + (ty - r.oy)] = ch;
    r.live[(ty - r.oy) * r.tw + (tx - r.ox)] = ch;
    if (H.Render) H.Render.markDirty();
  }

  // 0 empty, 1 solid, 2 one-way platform.
  function solidity(ch, r) {
    switch (ch) {
      case '#': case '_': case 'T': case 'R': case 'M': case 'P': case 'S': case 'C': case 'G': return 1;
      case '=': case 'b': return 2;
      case 'E': return H.Tide.phase() === 'ebbing' ? 0 : 1;
      case 'L': return H.Tide.phase() === 'low' ? 0 : 1;
      case 'V': return (flags.priorWedged || H.Tide.phase() === 'ebbing') ? 0 : 1;
      case 'K': return flags.seawallOpen ? 0 : 1;
      case 'Z': return flags.skullOpen ? 0 : 1;
      case 'Q': return flags.almonerDead ? 0 : 1;
      case 'Y': return flags.choirDrained ? 0 : 1;
      case 'y': return flags.choirDrained ? 2 : 0;
      case 'h': return (flags.basinFilling && !flags.basinCrossed) ? 1 : 0;
      default: return 0;
    }
  }
  function solid(tx, ty) {
    const s = solidity(charAt(tx, ty));
    // The top rung of a ladder is a platform you can stand on (S climbs down).
    if (s === 0 && isLadder(tx, ty) && !isLadder(tx, ty - 1)) return 2;
    return s;
  }
  function solidPx(x, y) { return solid(Math.floor(x / TS), Math.floor(y / TS)) === 1; }
  function isLadder(tx, ty) {
    const ch = charAt(tx, ty);
    return ch === 'H' || (ch === 'K' && flags.seawallOpen) || (ch === 'Z' && flags.skullOpen);
  }
  function isWallHang(tx, ty) { const ch = charAt(tx, ty); return ch === 'T' || ch === 'R'; }

  // ---- water -------------------------------------------------------------
  // Water surface in world pixels for a room at a world-pixel x, or Infinity for none.
  function roomSurface(r, x) {
    const st = state(r.id);
    if (r.pools) {
      const tx = Math.floor(x / TS) - r.ox;
      for (const p of r.pools) {
        if (tx >= p.x0 && tx <= p.x1) {
          const lvl = st.pool && st.pool[p.id] !== undefined ? st.pool[p.id] : p.empty;
          return (r.oy + lvl) * TS;
        }
      }
    }
    if (r.id === 'choir') {
      const lvl = st.choirLevel !== undefined ? st.choirLevel : 17;
      return (r.oy + lvl) * TS;
    }
    if (r.fixedWater !== undefined) return (r.oy + r.fixedWater) * TS;
    let sea = H.Tide.seaLevel() * TS;
    if (st.flood !== undefined) sea = Math.min(sea, (r.oy + st.flood) * TS);
    if (sea >= r.py + r.ph) return Infinity;
    return sea;
  }
  function surfaceAt(x, y) {
    const r = roomAtPx(x, y);
    return r ? roomSurface(r, x) : Infinity;
  }
  function isWater(x, y) {
    if (solidPx(x, y)) return false;
    return y > surfaceAt(x, y);
  }
  function isBrine(x, y) { const r = roomAtPx(x, y); return !!(r && r.brine && isWater(x, y)); }
  function isDeep(x, y) { const r = roomAtPx(x, y); return !!(r && r.deep && isWater(x, y)); }

  // Is the cell above the high-tide line for its room? (used for hauling bodies clear)
  function aboveTideLine(r, x, y) {
    if (r.fixedWater !== undefined || r.id === 'choir' || r.pools) return !isWater(x, y);
    const hi = (H.Tide.SEA_HIGH - H.Tide.spring) * TS;
    return y < hi;
  }

  return {
    build, reset, roomAtTile, roomAtPx, state, charAt, setChar, solidity, solid, solidPx,
    isLadder, isWallHang, roomSurface, surfaceAt, isWater, isBrine, isDeep, aboveTideLine,
    get flags() { return flags; },
    set flags(v) { flags = v; },
    get states() { return states; },
    set states(v) { states = v; refreshLive(); },
  };
})();
