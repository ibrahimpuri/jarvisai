// Keyboard, mouse and gamepad input with per-frame edge detection.
// Gamepads use the browser's standard mapping, where buttons are positional: button 0 is the
// bottom face button on every controller (Xbox A, PlayStation cross, Switch B).
'use strict';
H.Input = (() => {
  const down = new Set();
  const pressed = new Set();
  const released = new Set();
  const mouse = { x: 0, y: 0, l: false, r: false, lp: false, rp: false, lr: false, rr: false, wheel: 0 };
  let canvas = null;

  const BIND = {
    left: ['KeyA', 'ArrowLeft'],
    right: ['KeyD', 'ArrowRight'],
    up: ['KeyW', 'ArrowUp'],
    down: ['KeyS', 'ArrowDown'],
    jump: ['Space'],
    sneak: ['ShiftLeft', 'ShiftRight'],
    dash: ['KeyC'],
    gaff: ['KeyF'],
    swing: ['KeyQ'],
    interact: ['KeyE'],
    reload: ['KeyR'],
    shutter: ['KeyT'],
    burn: ['KeyX'],
    poultice: ['KeyZ'],
    stitch: ['KeyG'],
    quickcraft: ['KeyV'],
    boots: ['KeyB'],
    map: ['Tab', 'KeyM'],
    satchel: ['KeyI'],
    pause: ['Escape', 'KeyP'],
    w1: ['Digit1'], w2: ['Digit2'], w3: ['Digit3'], w4: ['Digit4'],
    fire: ['KeyJ'],          // keyboard fallback for firing
    focus: ['KeyK'],         // keyboard fallback for focused beam
    debug: ['F9'],
  };

  // ---- gamepad layout: the left stick moves, the d-pad holds the tools ----------------------------
  const B = { A: 0, B: 1, X: 2, Y: 3, LB: 4, RB: 5, LT: 6, RT: 7, BACK: 8, START: 9, L3: 10, R3: 11, UP: 12, DOWN: 13, LEFT: 14, RIGHT: 15, TOUCH: 17 };
  const PAD = { jump: B.A, dash: B.B, gaff: B.X, swing: B.LB, shutter: B.R3, boots: B.DOWN, wprev: B.LEFT, wnext: B.RIGHT, pause: B.START, journal: B.BACK, map: B.TOUCH };
  const HOLD = 0.4;
  // One button, two actions: a tap does the first, holding it does the second.
  const DUAL = [[B.Y, 'interact', 'burn'], [B.RB, 'reload', 'quickcraft'], [B.UP, 'poultice', 'stitch']];
  const holdT = [0, 0, 0], holdFired = [false, false, false];
  const pad = { using: false, kind: 'xbox', name: '', index: -1, stick: { x: 0, y: 0 }, aim: { x: 0, y: 0 }, sneakLatch: false, lastRumble: null };
  let padDown = new Set(), padPrev = new Set(), padBtn = [], padBtnPrev = [];
  const suppress = new Set();
  let suppressNext = false;
  const CONTINUOUS = new Set(['left', 'right', 'up', 'down', 'sneak', 'focus', 'swing']);
  let fake = null;   // tests: { id, buttons: [bool...], axes: [x, y, rx, ry] }

  function classify(id) {
    const s = String(id).toLowerCase();
    if (/054c|playstation|dualsense|dualshock|wireless controller|ps[345]/.test(s)) return 'playstation';
    if (/057e|nintendo|switch|joy-con|pro controller/.test(s)) return 'switch';
    return 'xbox';
  }
  function setUsing(v) { pad.using = v; }

  function init(c) {
    canvas = c;
    window.addEventListener('keydown', (e) => {
      if (['Space', 'Tab', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'F9'].includes(e.code)) e.preventDefault();
      if (!down.has(e.code)) pressed.add(e.code);
      down.add(e.code);
      setUsing(false);
    });
    window.addEventListener('keyup', (e) => {
      down.delete(e.code);
      released.add(e.code);
    });
    window.addEventListener('blur', () => { down.clear(); mouse.l = mouse.r = false; });
    canvas.addEventListener('mousemove', (e) => {
      const r = canvas.getBoundingClientRect();
      mouse.x = (e.clientX - r.left) * (H.VIEW_W / r.width);
      mouse.y = (e.clientY - r.top) * (H.VIEW_H / r.height);
      if (Math.abs(e.movementX) + Math.abs(e.movementY) > 3) setUsing(false);
    });
    canvas.addEventListener('mousedown', (e) => {
      if (e.button === 0) { mouse.l = true; mouse.lp = true; }
      if (e.button === 2) { mouse.r = true; mouse.rp = true; }
      setUsing(false);
      H.Audio && H.Audio.resume();
    });
    window.addEventListener('mouseup', (e) => {
      if (e.button === 0) { mouse.l = false; mouse.lr = true; }
      if (e.button === 2) { mouse.r = false; mouse.rr = true; }
    });
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    canvas.addEventListener('wheel', (e) => { mouse.wheel += Math.sign(e.deltaY); e.preventDefault(); }, { passive: false });
    window.addEventListener('keydown', () => H.Audio && H.Audio.resume(), { once: true });
    window.addEventListener('gamepadconnected', (e) => { if (pad.index < 0) select(e.gamepad); if (onNotice) onNotice(e.gamepad.id.split('(')[0].trim() + ' connected.'); });
    window.addEventListener('gamepaddisconnected', (e) => disconnected(e.gamepad.index));
  }
  function disconnected(index) {
    if (index !== pad.index) return;
    const was = pad.using;
    pad.index = -1; setUsing(false);
    if (fake && fake.index === index) fake = null;
    if (was && onNotice) onNotice('disconnected');
  }
  let onNotice = null;
  function select(gp) { pad.index = gp.index; pad.name = gp.id; pad.kind = classify(gp.id); }
  function pads() {
    if (fake) return [fake];
    try { return Array.from(navigator.getGamepads ? navigator.getGamepads() : []).filter(Boolean); } catch (e) { return []; }
  }
  const dead = (x, y, dz) => { const l = Math.hypot(x, y); if (l < dz) return { x: 0, y: 0 }; const k = Math.min(1, (l - dz) / (1 - dz)) / l; return { x: x * k, y: y * k }; };

  // Reads the pad once per frame, before the game steps.
  function poll(dt) {
    padPrev = padDown; padDown = new Set(); padBtnPrev = padBtn;
    const list = pads();
    // whichever pad is pressed becomes the active one
    for (const gp of list) {
      const active = gp.buttons.some((b) => (b.pressed !== undefined ? b.pressed : b)) || gp.axes.some((a) => Math.abs(a) > 0.5);
      if (active) { if (gp.index !== pad.index || fake) select(gp); setUsing(true); break; }
    }
    const gp = list.find((g) => g.index === pad.index);
    pad.stick = { x: 0, y: 0 }; pad.aim = { x: 0, y: 0 };
    if (!gp || !(fake || document.hasFocus())) { padBtn = []; return; }
    const btn = (i) => { const b = gp.buttons[i]; return !!b && (b.pressed !== undefined ? b.pressed : !!b); };
    const val = (i) => { const b = gp.buttons[i]; return !b ? 0 : b.value !== undefined ? b.value : b ? 1 : 0; };
    padBtn = gp.buttons.map((_, i) => btn(i));
    for (const a in PAD) if (btn(PAD[a])) padDown.add(a);
    DUAL.forEach(([b, tap, hold], i) => {
      if (btn(b)) { holdT[i] += dt; if (holdT[i] >= HOLD && !holdFired[i]) { holdFired[i] = true; padDown.add(hold); } }
      else { if (holdT[i] > 0 && !holdFired[i]) padDown.add(tap); holdT[i] = 0; holdFired[i] = false; }
    });
    const ax = gp.axes || [];
    pad.stick = dead(ax[0] || 0, ax[1] || 0, 0.2);
    if (pad.stick.x < -0.25) padDown.add('left');
    if (pad.stick.x > 0.25) padDown.add('right');
    if (pad.stick.y < -0.5) padDown.add('up');
    if (pad.stick.y > 0.5) padDown.add('down');
    if (btn(B.L3) && !(padBtnPrev[B.L3])) pad.sneakLatch = !pad.sneakLatch;
    if (pad.sneakLatch || (Math.abs(pad.stick.x) > 0 && Math.abs(pad.stick.x) < 0.55 && Math.abs(pad.stick.y) < 0.5)) padDown.add('sneak');
    pad.aim = dead(ax[2] || 0, ax[3] || 0, 0.25);
    if (val(B.LT) > 0.35) padDown.add('focus');
    if (val(B.RT) > 0.35) padDown.add('fire');
    // whatever was held across a menu opening or closing stays dead until let go
    if (suppressNext) { for (const a of padDown) if (!CONTINUOUS.has(a)) suppress.add(a); suppressNext = false; }
    for (const a of [...suppress]) if (!padDown.has(a)) suppress.delete(a);
    for (const a of suppress) padDown.delete(a);
  }
  // A face or menu button pressed this frame (menus use these directly).
  function padHit(i) { return !!padBtn[i] && !padBtnPrev[i]; }

  // ---- rumble ----------------------------------------------------------------------------------
  let rumbleUntil = 0, rumbleLevel = 0;
  function rumble(weak, strong, dur) {
    pad.lastRumble = { weak, strong, dur };
    const v = H.Settings.vibration;
    if (!pad.using || v <= 0) return;
    const now = performance.now() / 1000, level = Math.max(weak, strong);
    if (now < rumbleUntil && level < rumbleLevel) return;
    rumbleLevel = level; rumbleUntil = now + dur;
    const gp = pads().find((g) => g.index === pad.index);
    const act = gp && gp.vibrationActuator;
    if (act && act.playEffect) act.playEffect('dual-rumble', { duration: dur * 1000, weakMagnitude: Math.min(1, weak * v), strongMagnitude: Math.min(1, strong * v) }).catch(() => {});
  }
  // Switch controllers confirm with A (the right button) in menus.
  function confirmButton() {
    const st = H.Settings.prompts !== 'auto' ? H.Settings.prompts : pad.kind;
    return st === 'switch' && H.Settings.nintendoConfirm ? B.B : B.A;
  }

  const any = (codes, set) => codes.some((c) => set.has(c));
  return {
    init, poll, mouse, pad, rumble, padHit, confirmButton, classify, B, disconnected,
    held: (a) => (BIND[a] ? any(BIND[a], down) : false) || padDown.has(a),
    hit: (a) => (BIND[a] ? any(BIND[a], pressed) : false) || (padDown.has(a) && !padPrev.has(a)),
    up: (a) => (BIND[a] ? any(BIND[a], released) : false) || (!padDown.has(a) && padPrev.has(a)),
    key: (code) => down.has(code),
    endFrame() {
      pressed.clear(); released.clear();
      mouse.lp = mouse.rp = mouse.lr = mouse.rr = false;
      mouse.wheel = 0;
    },
    clear() {
      down.clear(); pressed.clear(); released.clear(); mouse.l = mouse.r = false;
      // so the Start that closed the pause menu does not open it again this very frame
      for (const a of [...padDown]) if (!CONTINUOUS.has(a)) { suppress.add(a); padDown.delete(a); }
      suppressNext = true;
      DUAL.forEach(([b], i) => { holdT[i] = 0; holdFired[i] = !!padBtn[b]; });
    },
    set notice(fn) { onNotice = fn; },
    set fakePad(v) { fake = v; if (v) { v.index = v.index || 0; select(v); } else { pad.index = -1; setUsing(false); } },
    BIND,
  };
})();
