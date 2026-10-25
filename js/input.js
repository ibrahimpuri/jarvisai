// Keyboard and mouse input with per-frame edge detection.
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

  function init(c) {
    canvas = c;
    window.addEventListener('keydown', (e) => {
      if (['Space', 'Tab', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'F9'].includes(e.code)) e.preventDefault();
      if (!down.has(e.code)) pressed.add(e.code);
      down.add(e.code);
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
    });
    canvas.addEventListener('mousedown', (e) => {
      if (e.button === 0) { mouse.l = true; mouse.lp = true; }
      if (e.button === 2) { mouse.r = true; mouse.rp = true; }
      H.Audio && H.Audio.resume();
    });
    window.addEventListener('mouseup', (e) => {
      if (e.button === 0) { mouse.l = false; mouse.lr = true; }
      if (e.button === 2) { mouse.r = false; mouse.rr = true; }
    });
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    canvas.addEventListener('wheel', (e) => { mouse.wheel += Math.sign(e.deltaY); e.preventDefault(); }, { passive: false });
    window.addEventListener('keydown', () => H.Audio && H.Audio.resume(), { once: true });
  }

  const any = (codes, set) => codes.some((c) => set.has(c));
  return {
    init, mouse,
    held: (a) => any(BIND[a], down),
    hit: (a) => any(BIND[a], pressed),
    up: (a) => any(BIND[a], released),
    key: (code) => down.has(code),
    endFrame() {
      pressed.clear(); released.clear();
      mouse.lp = mouse.rp = mouse.lr = mouse.rr = false;
      mouse.wheel = 0;
    },
    clear() { down.clear(); pressed.clear(); released.clear(); mouse.l = mouse.r = false; },
    BIND,
  };
})();
