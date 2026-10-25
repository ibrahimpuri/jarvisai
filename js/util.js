// Shared helpers and the global namespace.
'use strict';
window.H = window.H || {};

H.TILE = 16;
H.SCREEN_W = 40;      // tiles per screen, horizontally
H.SCREEN_H = 23;      // tiles per screen, vertically
H.VIEW_W = 640;       // internal canvas resolution
H.VIEW_H = 360;
H.M = 16;             // pixels per metre (1 tile = 1 m)

H.clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
H.lerp = (a, b, t) => a + (b - a) * t;
H.sign = (v) => (v < 0 ? -1 : v > 0 ? 1 : 0);
H.dist = (ax, ay, bx, by) => Math.hypot(bx - ax, by - ay);
H.rand = (a, b) => a + Math.random() * (b - a);
H.randi = (a, b) => Math.floor(H.rand(a, b + 1));
H.pick = (arr) => arr[Math.floor(Math.random() * arr.length)];
H.approach = (v, target, step) => (v < target ? Math.min(v + step, target) : Math.max(v - step, target));
H.angleDiff = (a, b) => {
  let d = b - a;
  while (d > Math.PI) d -= Math.PI * 2;
  while (d < -Math.PI) d += Math.PI * 2;
  return d;
};
H.fmtTime = (s) => {
  s = Math.max(0, Math.ceil(s));
  return Math.floor(s / 60) + ':' + String(s % 60).padStart(2, '0');
};
H.rectsOverlap = (a, b) => a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;

// Segment vs circle: returns the parametric t of first hit or -1.
H.segCircle = (x1, y1, x2, y2, cx, cy, r) => {
  const dx = x2 - x1, dy = y2 - y1;
  const fx = x1 - cx, fy = y1 - cy;
  const a = dx * dx + dy * dy;
  if (a === 0) return -1;
  const b = 2 * (fx * dx + fy * dy);
  const c = fx * fx + fy * fy - r * r;
  let disc = b * b - 4 * a * c;
  if (disc < 0) return -1;
  disc = Math.sqrt(disc);
  const t1 = (-b - disc) / (2 * a);
  const t2 = (-b + disc) / (2 * a);
  if (t1 >= 0 && t1 <= 1) return t1;
  if (t2 >= 0 && t2 <= 1) return t2;
  if (c < 0) return 0;
  return -1;
};

H.uid = (() => { let n = 1; return () => n++; })();

// Tiny event log shown as toasts.
H.toasts = [];
H.toast = (text, time = 3.5) => {
  H.toasts.push({ text, t: time, max: time });
  if (H.toasts.length > 5) H.toasts.shift();
};
