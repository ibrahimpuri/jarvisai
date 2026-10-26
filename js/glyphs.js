// Button prompts drawn for whatever the player is holding: keycaps and mouse buttons, or the face
// buttons, shoulders, sticks and d-pad of an Xbox, PlayStation or Switch controller.
// Text can carry {action} tokens ("{interact} Open the locker"); they become glyphs when drawn,
// on the canvas (HUD) or as small images inside the HTML menus.
'use strict';
H.Glyphs = (() => {
  const S = () => H.Settings;
  let force = null;   // tests and screenshots

  function padStyle() {
    const p = S().prompts;
    if (p === 'xbox' || p === 'playstation' || p === 'switch') return p;
    const k = H.Input.pad.kind;
    return k === 'playstation' || k === 'switch' ? k : 'xbox';
  }
  function current() { return force || (H.Input.pad.using ? padStyle() : 'keyboard'); }

  // ---- which controls perform an action -----------------------------------------------------------
  const KEYS = {
    move: ['K:A', 'K:D'], left: ['K:A'], right: ['K:D'], up: ['K:W'], down: ['K:S'],
    jump: ['K:Space'], dash: ['K:C'], gaff: ['K:F'], swing: ['K:Q:hold'], interact: ['K:E'], burn: ['K:X'], reload: ['K:R'],
    shutter: ['K:T'], poultice: ['K:Z'], stitch: ['K:G'], quickcraft: ['K:V'], boots: ['K:B'], map: ['K:Tab'], satchel: ['K:I'],
    journal: ['K:I'], pause: ['K:Esc'], sneak: ['K:Shift:hold'], fire: ['M:L'], focus: ['M:R:hold'], aim: ['M:move'],
    weapon: ['M:wheel'], drop: ['K:S', '+', 'K:Space'], walljump: ['K:A', '/', 'K:D', '+', 'K:Space'],
    confirm: ['K:E'], cancel: ['K:Esc'], tabs: [], skip: ['K:Esc'], mash: ['K:F', '/', 'K:E', '/', 'K:Space'],
  };
  function padControls(a) {
    const In = H.Input;
    switch (a) {
      case 'move': case 'left': case 'right': return ['LS'];
      case 'up': return ['LS:up']; case 'down': return ['LS:down'];
      case 'jump': return ['A']; case 'dash': return ['B']; case 'gaff': return ['X']; case 'interact': return ['Y']; case 'burn': return ['Y:hold'];
      case 'swing': return ['LB:hold']; case 'reload': return ['RB']; case 'quickcraft': return ['RB:hold'];
      case 'poultice': return ['DU']; case 'stitch': return ['DU:hold']; case 'boots': return ['DD'];
      case 'weapon': return ['DLR']; case 'wprev': return ['DL']; case 'wnext': return ['DR'];
      case 'shutter': return ['R3']; case 'sneak': return ['L3']; case 'fire': return ['RT']; case 'focus': return ['LT:hold']; case 'aim': return ['RS'];
      case 'map': case 'satchel': case 'journal': return ['BACK']; case 'pause': return ['START'];
      case 'drop': return ['LS:down', '+', 'A']; case 'walljump': return ['LS', '+', 'A'];
      case 'confirm': return [In.confirmButton() === 0 ? 'A' : 'B']; case 'cancel': return [In.confirmButton() === 0 ? 'B' : 'A'];
      case 'skip': return ['START']; case 'tabs': return ['LB', '/', 'RB']; case 'mash': return ['X', '/', 'Y', '/', 'A'];
      default: return [];
    }
  }
  function controls(action, st) { return st === 'keyboard' ? (KEYS[action] || []) : padControls(action); }

  // ---- palettes --------------------------------------------------------------------------------
  const BODY = '#202326', EDGE = '#8d8370', LABEL = '#efe6cf', LIT = '#f0d890';
  const FACE = {
    xbox: { A: '#7dc055', B: '#e05a48', X: '#4b90d8', Y: '#e9c24c' },
    playstation: { A: '#93b4ea', B: '#ea736c', X: '#da92cb', Y: '#5dcaa9' },
  };
  // Labels by position: A = bottom, B = right, X = left, Y = top.
  const faceLabel = (st, b) => (st === 'switch' ? { A: 'B', B: 'A', X: 'Y', Y: 'X' }[b] : b);
  const shoulderLabel = (st, id) => (st === 'playstation' ? { LB: 'L1', RB: 'R1', LT: 'L2', RT: 'R2' }[id] : st === 'switch' ? { LB: 'L', RB: 'R', LT: 'ZL', RT: 'ZR' }[id] : id);
  const keyLabel = (k) => k;
  const px = (v) => Math.max(4, Math.round(v));

  function split(token) { const hold = token.endsWith(':hold'); return { id: hold ? token.slice(0, -5) : token, hold }; }
  function baseWidth(c, id, st, s) {
    if (id === '+' || id === '/') return s * 0.55;
    if (id.startsWith('K:')) { const f = c.font; c.font = px(s * 0.58) + 'px monospace'; const w = Math.max(s, c.measureText(keyLabel(id.slice(2))).width + s * 0.5); c.font = f; return w; }
    if (id.startsWith('M:')) return s * 0.72;
    if (id === 'LB' || id === 'RB') return s * 1.45;
    if (id === 'LT' || id === 'RT') return s * 1.3;
    if (id === 'TOUCH') return s * 1.7;
    if (id === 'BACK' || id === 'START') return st === 'switch' ? s * 0.8 : s * 1.15;
    return s;
  }
  function width(c, token, st, s) {
    const { id, hold } = split(token);
    let w = baseWidth(c, id, st, s);
    if (hold) { const f = c.font; c.font = px(s * 0.5) + 'px monospace'; w += 2 + c.measureText('hold').width; c.font = f; }
    return w;
  }
  function rr(c, x, y, w, h, r) {
    r = Math.min(r, w / 2, h / 2);
    c.beginPath();
    c.moveTo(x + r, y); c.arc(x + w - r, y + r, r, -Math.PI / 2, 0); c.arc(x + w - r, y + h - r, r, 0, Math.PI / 2);
    c.arc(x + r, y + h - r, r, Math.PI / 2, Math.PI); c.arc(x + r, y + r, r, Math.PI, Math.PI * 1.5); c.closePath();
  }
  function ring(c, x, y, r) { c.beginPath(); c.arc(x, y, r, 0, Math.PI * 2); }
  function pill(c, x, y, w, h) { rr(c, x, y, w, h, h / 2); c.fillStyle = BODY; c.fill(); c.strokeStyle = EDGE; c.lineWidth = 1; c.stroke(); }
  function keyCap(c, label, x, y, w, s) {
    rr(c, x, y + s * 0.08, w, s * 0.92, s * 0.2); c.fillStyle = '#141210'; c.fill();
    rr(c, x, y, w, s * 0.84, s * 0.2); c.fillStyle = '#2c2822'; c.fill(); c.strokeStyle = '#b7a682'; c.lineWidth = 1; c.stroke();
    c.font = px(s * 0.58) + 'px monospace'; c.fillStyle = LABEL; c.fillText(label, x + w / 2, y + s * 0.62);
  }
  function mouse(c, which, cx, cy, s) {
    const w = s * 0.66, h = s, x = cx - w / 2, y = cy - h / 2;
    rr(c, x, y, w, h, w * 0.45); c.fillStyle = BODY; c.fill(); c.strokeStyle = EDGE; c.lineWidth = 1; c.stroke();
    if (which === 'L' || which === 'R') {
      c.save(); rr(c, x, y, w, h, w * 0.45); c.clip();
      c.fillStyle = LIT; c.fillRect(which === 'L' ? x : cx, y, w / 2, h * 0.42); c.restore();
    }
    c.strokeStyle = EDGE; c.beginPath(); c.moveTo(cx, y + 1); c.lineTo(cx, y + h * 0.42); c.moveTo(x + 1, y + h * 0.42); c.lineTo(x + w - 1, y + h * 0.42); c.stroke();
    if (which === 'wheel') { c.fillStyle = LIT; c.fillRect(cx - 1, y + h * 0.14, 2, h * 0.22); }
    if (which === 'move') {
      c.strokeStyle = LABEL; c.beginPath();
      c.moveTo(x - 1, cy - 2); c.lineTo(x - 3, cy); c.lineTo(x - 1, cy + 2);
      c.moveTo(x + w + 1, cy - 2); c.lineTo(x + w + 3, cy); c.lineTo(x + w + 1, cy + 2); c.stroke();
    }
  }
  function face(c, st, b, cx, cy, s) {
    const r = s / 2;
    ring(c, cx, cy, r); c.fillStyle = BODY; c.fill();
    const col = st === 'switch' ? LABEL : FACE[st][b];
    ring(c, cx, cy, r - 0.5); c.strokeStyle = st === 'switch' ? EDGE : col; c.globalAlpha *= 0.75; c.lineWidth = 1; c.stroke(); c.globalAlpha /= 0.75;
    if (st === 'playstation') {
      c.strokeStyle = col; c.lineWidth = Math.max(1, s * 0.1);
      const q = s * 0.21;
      c.beginPath();
      if (b === 'A') { c.moveTo(cx - q, cy - q); c.lineTo(cx + q, cy + q); c.moveTo(cx - q, cy + q); c.lineTo(cx + q, cy - q); }
      else if (b === 'B') c.arc(cx, cy, q * 1.05, 0, Math.PI * 2);
      else if (b === 'X') c.rect(cx - q * 0.9, cy - q * 0.9, q * 1.8, q * 1.8);
      else { c.moveTo(cx, cy - q * 1.15); c.lineTo(cx + q * 1.05, cy + q * 0.75); c.lineTo(cx - q * 1.05, cy + q * 0.75); c.closePath(); }
      c.stroke();
      return;
    }
    c.font = 'bold ' + px(s * 0.64) + 'px monospace'; c.fillStyle = col; c.fillText(faceLabel(st, b), cx, cy + s * 0.23);
  }
  function shoulder(c, st, id, x, y, w, s) {
    const trig = id === 'LT' || id === 'RT';
    if (trig) {
      const r = s * 0.42;
      c.beginPath(); c.moveTo(x, y + s); c.arc(x + r, y + r, r, Math.PI, Math.PI * 1.5); c.arc(x + w - r, y + r, r, -Math.PI / 2, 0); c.lineTo(x + w, y + s); c.closePath();
    } else rr(c, x, y + s * 0.18, w, s * 0.64, s * 0.28);
    c.fillStyle = BODY; c.fill(); c.strokeStyle = EDGE; c.lineWidth = 1; c.stroke();
    c.font = px(s * (trig ? 0.52 : 0.5)) + 'px monospace'; c.fillStyle = LABEL;
    c.fillText(shoulderLabel(st, id), x + w / 2, y + s * (trig ? 0.68 : 0.66));
  }
  function stick(c, id, cx, cy, s) {
    const r = s / 2;
    ring(c, cx, cy, r); c.fillStyle = BODY; c.fill();
    c.strokeStyle = EDGE; c.lineWidth = 1; ring(c, cx, cy, r - 0.5); c.stroke(); ring(c, cx, cy, r * 0.55); c.stroke();
    const press = id === 'L3' || id === 'R3';
    if (press) { c.fillStyle = LIT; ring(c, cx, cy, r * 0.3); c.fill(); }
    else { c.font = px(s * 0.42) + 'px monospace'; c.fillStyle = LABEL; c.fillText(id[0], cx, cy + s * 0.15); }
    if (id.endsWith(':up') || id.endsWith(':down')) {
      const up = id.endsWith(':up'), ty = up ? cy - r - 1 : cy + r + 1, d = up ? -1 : 1;
      c.fillStyle = LIT; c.beginPath(); c.moveTo(cx - s * 0.2, ty); c.lineTo(cx + s * 0.2, ty); c.lineTo(cx, ty + d * s * 0.22); c.closePath(); c.fill();
    }
  }
  function dpad(c, id, cx, cy, s) {
    const arm = s * 0.34, half = s / 2;
    c.fillStyle = BODY; c.fillRect(cx - arm / 2, cy - half, arm, s); c.fillRect(cx - half, cy - arm / 2, s, arm);
    c.fillStyle = LIT;
    if (id === 'DU') c.fillRect(cx - arm / 2 + 0.5, cy - half + 0.5, arm - 1, half - arm / 2);
    if (id === 'DD') c.fillRect(cx - arm / 2 + 0.5, cy + arm / 2, arm - 1, half - arm / 2 - 0.5);
    if (id === 'DL' || id === 'DLR') c.fillRect(cx - half + 0.5, cy - arm / 2 + 0.5, half - arm / 2, arm - 1);
    if (id === 'DR' || id === 'DLR') c.fillRect(cx + arm / 2, cy - arm / 2 + 0.5, half - arm / 2 - 0.5, arm - 1);
    c.strokeStyle = EDGE; c.lineWidth = 1; c.beginPath();
    const a = arm / 2;
    c.moveTo(cx - a, cy - half); c.lineTo(cx + a, cy - half); c.lineTo(cx + a, cy - a); c.lineTo(cx + half, cy - a); c.lineTo(cx + half, cy + a);
    c.lineTo(cx + a, cy + a); c.lineTo(cx + a, cy + half); c.lineTo(cx - a, cy + half); c.lineTo(cx - a, cy + a); c.lineTo(cx - half, cy + a);
    c.lineTo(cx - half, cy - a); c.lineTo(cx - a, cy - a); c.closePath(); c.stroke();
  }
  function menuButton(c, st, id, cx, cy, w, s) {
    const start = id === 'START';
    if (st === 'switch') {
      const r = s * 0.4;
      ring(c, cx, cy, r); c.fillStyle = BODY; c.fill(); c.strokeStyle = EDGE; c.lineWidth = 1; c.stroke();
      c.fillStyle = LABEL; c.fillRect(cx - r * 0.5, cy - 0.6, r, 1.2); if (start) c.fillRect(cx - 0.6, cy - r * 0.5, 1.2, r);
      return;
    }
    pill(c, cx - w / 2, cy - s * 0.32, w, s * 0.64);
    c.fillStyle = LABEL; c.strokeStyle = LABEL; c.lineWidth = 1;
    if (st === 'xbox' && !start) { c.strokeRect(cx - s * 0.2, cy - s * 0.14, s * 0.26, s * 0.2); c.strokeRect(cx - s * 0.06, cy - s * 0.04, s * 0.26, s * 0.2); }
    else if (st === 'playstation' && !start) for (let i = -1; i <= 1; i++) c.fillRect(cx + i * s * 0.14 - 0.5, cy - s * 0.14, 1, s * 0.28);
    else for (let i = -1; i <= 1; i++) c.fillRect(cx - s * 0.2, cy + i * s * 0.11 - 0.5, s * 0.4, 1);
  }

  // Draws a control centred on yMid starting at x; returns its width.
  function draw(c, token, st, x, yMid, s, alpha = 1) {
    const { id, hold } = split(token);
    let w = baseWidth(c, id, st, s);
    c.save();
    c.globalAlpha *= alpha; c.textAlign = 'center';
    const cx = x + w / 2, top = yMid - s / 2;
    if (id === '+' || id === '/') { c.font = px(s * 0.6) + 'px monospace'; c.fillStyle = LABEL; c.globalAlpha *= 0.7; c.fillText(id, cx, yMid + s * 0.2); }
    else if (id.startsWith('K:')) keyCap(c, keyLabel(id.slice(2)), x, top, w, s);
    else if (id.startsWith('M:')) mouse(c, id.slice(2), cx, yMid, s);
    else if ('ABXY'.includes(id) && id.length === 1) face(c, st, id, cx, yMid, s);
    else if (id === 'LB' || id === 'RB' || id === 'LT' || id === 'RT') shoulder(c, st, id, x, top, w, s);
    else if (id.startsWith('LS') || id.startsWith('RS') || id === 'L3' || id === 'R3') stick(c, id, cx, yMid, s);
    else if (id.startsWith('D')) dpad(c, id, cx, yMid, s);
    else if (id === 'BACK' || id === 'START') menuButton(c, st, id, cx, yMid, w, s);
    else if (id === 'TOUCH') pill(c, x, top + s * 0.2, w, s * 0.6);
    if (hold) {
      c.textAlign = 'left'; c.font = px(s * 0.5) + 'px monospace'; c.fillStyle = LABEL; c.globalAlpha *= 0.65;
      c.fillText('hold', x + w + 2, yMid + s * 0.18);
      w += 2 + c.measureText('hold').width;
    }
    c.restore();
    return w;
  }

  function actionWidth(c, action, st, s) { const ctl = controls(action, st); let w = 0; ctl.forEach((t, i) => { w += width(c, t, st, s) + (i > 0 ? 1 : 0); }); return w; }
  function drawAction(c, action, st, x, yMid, s, alpha = 1) {
    const ctl = controls(action, st); let w = 0;
    ctl.forEach((t, i) => { if (i > 0) w += 1; w += draw(c, t, st, x + w, yMid, s, alpha); });
    return w;
  }
  function parse(text) {
    const parts = []; let i = 0;
    while (i < text.length) {
      const a = text.indexOf('{', i), b = a >= 0 ? text.indexOf('}', a) : -1;
      if (a < 0 || b < 0) { parts.push([false, text.slice(i)]); break; }
      if (a > i) parts.push([false, text.slice(i, a)]);
      parts.push([true, text.slice(a + 1, b)]);
      i = b + 1;
    }
    return parts;
  }
  const fontSize = (c) => { const m = /(\d+(?:\.\d+)?)px/.exec(c.font); return m ? parseFloat(m[1]) : 10; };
  const glyphSize = (c) => Math.round(fontSize(c) * 1.35 + 1);
  function measure(c, text, st = current()) {
    const s = glyphSize(c); let w = 0;
    for (const [g, t] of parse(text)) w += g ? actionWidth(c, t, st, s) + 2 : c.measureText(t).width;
    return w;
  }
  // Rich text with its baseline at y, honouring the context's textAlign; returns the width.
  function drawText(c, text, x, y, alpha = 1, st = current()) {
    const total = measure(c, text, st), s = glyphSize(c);
    const align = c.textAlign;
    let x0 = align === 'center' ? x - total / 2 : align === 'right' || align === 'end' ? x - total : x;
    const mid = y - fontSize(c) * 0.33;
    c.textAlign = 'left';
    for (const [g, t] of parse(text)) {
      if (g) { x0 += 1; x0 += drawAction(c, t, st, x0, mid, s, alpha) + 1; } else { c.fillText(t, x0, y); x0 += c.measureText(t).width; }
    }
    c.textAlign = align;
    return total;
  }
  function wrap(c, text, maxW) {
    const lines = []; let line = '';
    for (const word of text.split(' ')) {
      const trial = line ? line + ' ' + word : word;
      if (line && measure(c, trial) > maxW) { lines.push(line); line = word; } else line = trial;
    }
    if (line) lines.push(line);
    return lines;
  }

  // Friendly names, for text that cannot hold a picture.
  function name(id, st) {
    if (id.startsWith('K:')) return keyLabel(id.slice(2));
    if (id.startsWith('M:')) return { L: 'left click', R: 'right click', wheel: 'the mouse wheel' }[id.slice(2)] || 'the mouse';
    if ('ABXY'.includes(id) && id.length === 1) return st === 'playstation' ? { A: 'Cross', B: 'Circle', X: 'Square', Y: 'Triangle' }[id] : faceLabel(st, id);
    if (['LB', 'RB', 'LT', 'RT'].includes(id)) return shoulderLabel(st, id);
    if (id.startsWith('LS') || id.startsWith('RS')) return (id[0] === 'L' ? 'the left stick' : 'the right stick') + (id.endsWith(':up') ? ' up' : id.endsWith(':down') ? ' down' : '');
    if (id === 'L3' || id === 'R3') return st === 'xbox' ? (id === 'L3' ? 'LS click' : 'RS click') : id;
    return { DU: 'd-pad up', DD: 'd-pad down', DL: 'd-pad left', DR: 'd-pad right', DLR: 'd-pad left / right', TOUCH: 'the touchpad',
      BACK: st === 'playstation' ? 'Create' : st === 'switch' ? '-' : 'View', START: st === 'playstation' ? 'Options' : st === 'switch' ? '+' : 'Menu' }[id] || id;
  }
  function plain(text, st = current()) {
    return parse(text).map(([g, t]) => {
      if (!g) return t;
      return controls(t, st).map((tok) => { const { id, hold } = split(tok); return (id === '+' || id === '/' ? ' ' + id + ' ' : name(id, st)) + (hold ? ' (hold)' : ''); }).join('');
    }).join('');
  }

  // ---- HTML: each glyph is drawn once to a small canvas and reused as an image --------------------
  const cache = new Map();
  const SCALE = 3;
  function img(action, st, size = 15) {
    const key = st + '|' + action + '|' + size;
    if (cache.has(key)) return cache.get(key);
    const cv = document.createElement('canvas');
    const c = cv.getContext('2d');
    const s = size;
    const w = Math.ceil(actionWidth(c, action, st, s)) + 2, h = s + 4;
    cv.width = w * SCALE; cv.height = h * SCALE;
    c.scale(SCALE, SCALE);
    drawAction(c, action, st, 1, h / 2, s);
    const html = w > 2 ? `<img class="glyph" alt="${name(action, st)}" src="${cv.toDataURL()}" style="width:${w}px;height:${h}px">` : '';
    cache.set(key, html);
    return html;
  }
  const esc = (s) => String(s).replace(/[&<>"]/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[ch]));
  function html(text, st = current(), size) { return parse(text).map(([g, t]) => (g ? img(t, st, size) : esc(t))).join(''); }

  return {
    current, padStyle, controls, draw, drawAction, actionWidth, measure, drawText, wrap, plain, html, parse,
    set force(v) { force = v; }, get force() { return force; },
  };
})();
