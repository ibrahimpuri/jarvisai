# Paints the far vista for each region: the great space behind the ruined wall, the thing the player glimpses
# through every break in the stone. Each is built back to front in receding layers (atmospheric perspective: the
# further a layer, the paler, closer to the air and softer), with light coming from a source the region would
# have (the rose window, the kilns, the eye sockets of the Skull), rays falling from it, and a brush texture over
# all of it so it reads as painted rather than drawn.
# Like every room layer it is written in the palette encoding (red: value, green: accent, blue: glow) and coloured
# in the game from the region's palette, so it recolours with the tide.
# Usage: python3 tools/paint_vistas.py [region ...] [--preview dir]     (writes godot/art/vistas/<region>.png)
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), '..', 'godot', 'art', 'vistas')
W, H = 2048, 1024
S = 2   # supersampling for the masks
PREVIEW = None   # --preview <dir>: also write each vista coloured as the game would colour it


# ---- layers -----------------------------------------------------------------------------------------------------
class Layer:
    """One depth of the vista: a silhouette mask (and optional accent and glow masks) drawn supersampled."""
    def __init__(self):
        self.m = Image.new('L', (W * S, H * S), 0)
        self.a = Image.new('L', (W * S, H * S), 0)
        self.g = Image.new('L', (W * S, H * S), 0)
        self.d = ImageDraw.Draw(self.m); self.da = ImageDraw.Draw(self.a); self.dg = ImageDraw.Draw(self.g)

    def _xy(self, pts): return [(x * S, y * S) for x, y in pts]
    def poly(self, pts, fill=255, which='m'):
        getattr(self, 'd' if which == 'm' else 'd' + which).polygon(self._xy(pts), fill=fill)
    def rect(self, x0, y0, x1, y1, fill=255, which='m'):
        getattr(self, 'd' if which == 'm' else 'd' + which).rectangle([x0 * S, y0 * S, x1 * S, y1 * S], fill=fill)
    def ellipse(self, cx, cy, rx, ry, fill=255, which='m', width=0):
        dr = getattr(self, 'd' if which == 'm' else 'd' + which)
        box = [(cx - rx) * S, (cy - ry) * S, (cx + rx) * S, (cy + ry) * S]
        if width: dr.ellipse(box, outline=fill, width=max(1, int(width * S)))
        else: dr.ellipse(box, fill=fill)
    def line(self, pts, width, fill=255, which='m'):
        dr = getattr(self, 'd' if which == 'm' else 'd' + which)
        dr.line(self._xy(pts), fill=fill, width=max(1, int(width * S)), joint='curve')
        for (x, y) in (pts[0], pts[-1]):
            r = width * S / 2; dr.ellipse([x * S - r, y * S - r, x * S + r, y * S + r], fill=fill)
    def taper(self, pts, w0, w1, fill=255, which='m'):
        # a stroke that thins from w0 to w1 along its length: ribs, tendrils, kelp
        n = len(pts); left = []; right = []
        for i, (x, y) in enumerate(pts):
            a = pts[max(0, i - 1)]; b = pts[min(n - 1, i + 1)]
            dx, dy = b[0] - a[0], b[1] - a[1]; l = math.hypot(dx, dy) or 1
            nx, ny = -dy / l, dx / l; w = (w0 + (w1 - w0) * i / max(1, n - 1)) / 2
            left.append((x + nx * w, y + ny * w)); right.append((x - nx * w, y - ny * w))
        self.poly(left + right[::-1], fill, which)

    def done(self, blur):
        out = []
        for im in (self.m, self.a, self.g):
            im = im.resize((W, H), Image.LANCZOS)
            if blur > 0: im = im.filter(ImageFilter.GaussianBlur(blur))
            out.append(np.asarray(im, np.float32) / 255)
        return out


def curve(p0, p1, p2, n=24):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1])
            for t in (i / (n - 1) for i in range(n))]

def noise(rng, beta=2.0, stretch=(1.0, 1.0), lo=0.0, hi=0.5):
    fy = np.fft.fftfreq(H)[:, None]; fx = np.fft.fftfreq(W)[None, :]
    r = np.sqrt((fx * stretch[0]) ** 2 + (fy * stretch[1]) ** 2)
    amp = np.where(r > 0, 1 / np.maximum(r, 1e-6) ** (beta / 2), 0) * ((r >= lo) & (r <= hi))
    a = np.real(np.fft.ifft2(np.fft.fft2(rng.standard_normal((H, W))) * amp))
    a -= a.mean(); return a / (a.std() or 1)

def sstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


class Vista:
    def __init__(self, rng, top=0.3, haze=0.62, bottom=0.45, haze_at=0.68):
        self.rng = rng
        y = np.linspace(0, 1, H)[:, None] * np.ones((1, W))
        up = sstep(0, haze_at, y); down = sstep(haze_at, 1, y)
        self.v = (top + (haze - top) * up) * (1 - down) + bottom * down
        self.acc = np.zeros((H, W), np.float32); self.glow = np.zeros((H, W), np.float32)
        self.light = np.zeros((H, W), np.float32)

    def add(self, layer, value, blur, accent_v=None, glow_v=1.0):
        m, a, g = layer.done(blur)
        self.v = self.v * (1 - m) + value * m
        if accent_v is not None: self.acc = self.acc * (1 - m) + a * accent_v
        else: self.acc = self.acc * (1 - m) + self.acc * 0 + a * m
        self.glow = np.maximum(self.glow * (1 - m), g * glow_v)

    def source(self, x, y, r, strength=0.35, glow=0.4):
        # the light the space is lit by: a soft bloom in the air around it
        yy, xx = np.mgrid[0:H, 0:W]
        d = np.sqrt(((xx - x) / r) ** 2 + ((yy - y) / r) ** 2)
        b = np.exp(-d * d * 1.6)
        self.v = self.v + b * strength * (1 - self.v)
        self.glow = np.maximum(self.glow, b ** 3 * glow)

    def rays(self, x, y, angles, length, width, strength=0.22, blur=18):
        # beams falling from the source through the dust
        L = Layer()
        for a in angles:
            dx, dy = math.sin(a), math.cos(a); nx, ny = dy, -dx
            w0, w1 = width * 0.4, width * (1.4 + self.rng.random())
            L.poly([(x + nx * w0, y + ny * w0), (x - nx * w0, y - ny * w0), (x + dx * length - nx * w1, y + dy * length - ny * w1), (x + dx * length + nx * w1, y + dy * length + ny * w1)], int(150 + self.rng.random() * 100))
        m, _, _ = L.done(blur)
        fade = np.linspace(1, 0.25, H)[:, None]
        self.light = np.maximum(self.light, m * fade)

    def water(self, y0, reflect=0.45):
        # a drowned floor: the space mirrored in still water, broken into horizontal streaks
        h = H - y0
        src = self.v[max(0, y0 - h):y0][::-1]
        if src.shape[0] < h: src = np.vstack([src, np.repeat(src[-1:], h - src.shape[0], 0)])
        streak = 0.8 + 0.2 * np.sin(np.arange(h)[:, None] * 0.9 + noise(self.rng, 2.2, (0.05, 1))[:h] * 2)
        refl = src * streak
        depth = np.linspace(0, 1, h)[:, None]
        self.v[y0:] = refl * reflect + self.v[y0:] * (1 - reflect) * 0.6 - depth * 0.08
        self.glow[y0:] = np.maximum(self.glow[y0:] * 0.3, self.glow[max(0, y0 - h):y0][::-1][:h] * 0.35 if self.glow[max(0, y0 - h):y0].shape[0] >= h else 0)
        self.v[y0:y0 + 2] += 0.12   # the surface line catching the light

    def finish(self, name, preview_pal):
        rng = self.rng
        v = self.v + self.light * 0.22
        glow = np.maximum(self.glow, self.light * 0.12)
        # the brush: strokes in a few directions, a tooth of paper under them
        brush = noise(rng, 2.0, (0.25, 1.0), 0.004, 0.08) * 0.6 + noise(rng, 2.0, (1.0, 0.3), 0.004, 0.08) * 0.4
        tooth = noise(rng, 1.0, (1, 1), 0.1, 0.5)
        v = v + brush * 0.022 + tooth * 0.008
        # dust hanging in the light
        dust = (rng.random((H, W)) > 0.9993).astype(np.float32)
        dust = np.asarray(Image.fromarray((dust * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)), np.float32) / 255 * 4
        v = v + dust * (0.15 + self.light * 0.6)
        enc = np.stack([np.clip(v, 0, 1), np.clip(self.acc, 0, 1), np.clip(glow, 0, 1)], -1)
        os.makedirs(OUT, exist_ok=True)
        Image.fromarray((enc * 255 + 0.5).astype(np.uint8), 'RGB').save(os.path.join(OUT, name + '.png'))
        # a preview in the region's colours, as the palette shader would colour it
        sh, lt, fg, ac, gl = [np.array([int(c[i:i + 2], 16) / 255 for i in (1, 3, 5)]) for c in preview_pal]
        vv = enc[..., :1]; col = sh + (lt - sh) * vv
        col = col + (ac * (0.45 + 0.8 * vv) - col) * enc[..., 1:2]
        col = col + (fg - col) * 0.3 + gl * enc[..., 2:3]
        if PREVIEW is None: print('wrote', name); return
        prev = PREVIEW
        os.makedirs(prev, exist_ok=True)
        Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).resize((W // 2, H // 2), Image.LANCZOS).save(os.path.join(prev, name + '.png'))
        print('wrote', name)


# ---- architecture ------------------------------------------------------------------------------------------------
def pointed_arch(L, xl, xr, spring, rise, thick, fill=255, which='m', n=20):
    mid = (xl + xr) / 2; apex = spring - rise
    def half(x0, inset):
        pts = []
        for i in range(n):
            t = i / (n - 1); u = 1 - t
            x = x0 + (mid - x0) * t
            y = apex + (spring - apex) * (1 - math.sqrt(max(0, 1 - u))) + inset
            pts.append((x, y))
        return pts
    outer = half(xl, 0) + half(xr, 0)[::-1]
    inner = [(x + (thick if x < mid else -thick) * 0.6, y + thick) for (x, y) in outer]
    L.poly(outer + inner[::-1], fill, which)

def column(L, x, top, bottom, w, fill=255, which='m'):
    L.rect(x - w / 2, top, x + w / 2, bottom, fill, which)
    L.rect(x - w * 0.8, top - w * 0.35, x + w * 0.8, top, fill, which)          # capital
    L.rect(x - w * 0.75, bottom - w * 0.5, x + w * 0.75, bottom, fill, which)   # base

def arcade(L, x0, x1, base, bay, height, colw, rise=None, fill=255):
    x = x0
    while x < x1 + bay:
        column(L, x, base - height, base, colw, fill)
        pointed_arch(L, x - colw * 0.4, x + bay + colw * 0.4, base - height, rise or bay * 0.55, colw * 0.7, fill)
        x += bay
    L.rect(x0 - bay, base - height - (rise or bay * 0.55) - colw * 1.2, x1 + bay * 2, base - height - (rise or bay * 0.55) - colw * 0.2, fill)

def lancet(L, cx, top, w, h, fill=255, which='g'):
    pts = [(cx - w / 2, top + h), (cx - w / 2, top + w * 0.7)]
    for i in range(9):
        t = i / 8; pts.append((cx - w / 2 + w * t, top + w * 0.7 - math.sin(t * math.pi) * w * 0.7 - (w * 0.18 if 0.35 < t < 0.65 else 0)))
    pts.append((cx + w / 2, top + h))
    L.poly(pts, fill, which)

def rose(L, cx, cy, r, which='g'):
    L.ellipse(cx, cy, r, r, 255, which)
    L.ellipse(cx, cy, r, r, 0, which, width=r * 0.06)
    L.ellipse(cx, cy, r * 0.62, r * 0.62, 0, which, width=r * 0.04)
    L.ellipse(cx, cy, r * 0.18, r * 0.18, 0, which)
    for k in range(16):
        a = k / 16 * math.tau
        L.line([(cx + math.cos(a) * r * 0.18, cy + math.sin(a) * r * 0.18), (cx + math.cos(a) * r, cy + math.sin(a) * r)], r * 0.035, 0, which)
    for k in range(16):
        a = (k + 0.5) / 16 * math.tau
        L.ellipse(cx + math.cos(a) * r * 0.8, cy + math.sin(a) * r * 0.8, r * 0.07, r * 0.07, 0, which)

def gear(L, cx, cy, r, teeth, fill=255):
    pts = []
    for i in range(teeth * 4):
        a = i / (teeth * 4) * math.tau
        rr = r if (i % 4) in (1, 2) else r * 0.86
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    L.poly(pts, fill)
    L.ellipse(cx, cy, r * 0.72, r * 0.72, 0)
    for k in range(6):
        a = k / 6 * math.tau
        L.line([(cx, cy), (cx + math.cos(a) * r * 0.75, cy + math.sin(a) * r * 0.75)], r * 0.09, fill)
    L.ellipse(cx, cy, r * 0.16, r * 0.16, fill)

def truss(L, x, top, bottom, w, step, thick, fill=255):
    L.rect(x, top, x + thick, bottom, fill); L.rect(x + w - thick, top, x + w, bottom, fill)
    y = top; flip = False
    while y < bottom - step * 0.5:
        L.line([(x, y), (x + w, y + step)] if not flip else [(x + w, y), (x, y + step)], thick * 0.6, fill)
        L.rect(x, y, x + w, y + thick * 0.8, fill)
        y += step; flip = not flip

def stalactites(L, y, x0, x1, rng, size, fill=255):
    pts = [(x0, y - size * 3)]
    x = x0
    while x < x1:
        w = size * (0.4 + rng.random()); dn = size * (0.5 + rng.random() * 2.5)
        pts += [(x, y), (x + w / 2, y + dn), (x + w, y)]
        x += w + size * rng.random() * 0.6
    pts += [(x1, y), (x1, y - size * 3)]
    L.poly(pts, fill)

def kelp(L, x, base, height, rng, w=6, fill=255, up=True):
    pts = []; n = 14
    for i in range(n):
        t = i / (n - 1)
        pts.append((x + math.sin(t * 5 + x) * 14 * t, base - height * t if up else base + height * t))
    L.taper(pts, w, 1.5, fill)


# ---- the regions -----------------------------------------------------------------------------------------------------
def wrackline(rng):
    # a drowned chapel at the back of a sea cave, its windows still faintly lit, the cave mouth framing it
    V = Vista(rng, 0.26, 0.6, 0.4, 0.7)
    V.source(W * 0.55, H * 0.42, 520, 0.3, 0.3)
    far = Layer()
    # the chapel: a gabled front with three lancets and a bell-cote
    cx = W * 0.55
    far.poly([(cx - 260, H * 0.78), (cx - 260, H * 0.42), (cx, H * 0.2), (cx + 260, H * 0.42), (cx + 260, H * 0.78)])
    far.rect(cx - 40, H * 0.1, cx + 40, H * 0.24); far.poly([(cx - 52, H * 0.1), (cx, H * 0.03), (cx + 52, H * 0.1)])
    for k, dx in enumerate((-150, 0, 150)):
        lancet(far, cx + dx, H * (0.44 if dx else 0.38), 46 if dx else 60, H * (0.28 if dx else 0.34), 255, 'g')
    far.rect(cx - 600, H * 0.66, cx + 640, H * 0.8)
    V.add(far, 0.44, 3.0, glow_v=0.28)
    V.rays(cx, H * 0.38, [-0.35, -0.1, 0.15, 0.4], H * 0.9, 50, blur=22)
    mid = Layer()
    # broken arcade either side of the chapel
    arcade(mid, W * 0.02, W * 0.3, H * 0.74, 120, H * 0.26, 22)
    arcade(mid, W * 0.78, W * 1.0, H * 0.74, 120, H * 0.26, 22)
    for i in range(10): kelp(mid, W * (0.05 + i * 0.1) + rng.random() * 40, H * 0.75, H * (0.12 + rng.random() * 0.2), rng, 7)
    V.add(mid, 0.3, 1.6)
    near = Layer()
    # the cave: a ragged mouth closing in from the top and the sides, hung with stalactites
    near.poly([(0, 0), (W, 0), (W, H * 0.12), (W * 0.82, H * 0.1), (W * 0.62, H * 0.06), (W * 0.4, H * 0.08), (W * 0.18, H * 0.12), (0, H * 0.16)])
    stalactites(near, H * 0.1, 0, W, rng, 34)
    near.poly([(0, 0), (W * 0.1, 0), (W * 0.08, H * 0.4), (W * 0.12, H * 0.7), (W * 0.06, H), (0, H)])
    near.poly([(W, 0), (W * 0.9, 0), (W * 0.93, H * 0.35), (W * 0.88, H * 0.62), (W * 0.94, H), (W, H)])
    V.add(near, 0.12, 0.9)
    V.water(int(H * 0.78), 0.5)
    V.finish('wrackline', ["#0a1113", "#8aa4a0", "#4c6c6e", "#7fb59e", "#d4f2ea"])

def saltworks(rng):
    # the salt halls: terraces of pans stepping down, scaffold towers, the great wheels, chimneys, kiln light below
    V = Vista(rng, 0.3, 0.66, 0.5, 0.62)
    V.source(W * 0.42, H * 0.86, 640, 0.35, 0.45)
    far = Layer()
    for i, x in enumerate([0.12, 0.3, 0.66, 0.84]):
        far.rect(W * x - 34, H * 0.06, W * x + 34, H * 0.7)                    # chimneys
        far.rect(W * x - 46, H * 0.05, W * x + 46, H * 0.09)
    for i in range(5): truss(far, W * (0.05 + i * 0.2), H * (0.3 + rng.random() * 0.15), H * 0.72, 90, 60, 7)
    V.add(far, 0.5, 3.2)
    smoke = Layer()
    for x in [0.12, 0.3, 0.66, 0.84]:
        pts = curve((W * x, H * 0.06), (W * x + 140, H * -0.1), (W * x + 420, H * -0.05), 20)
        smoke.taper(pts, 70, 260, 150)
    m, _, _ = smoke.done(26)
    V.v = V.v + m * 0.16 * (1 - V.v)
    mid = Layer()
    gear(mid, W * 0.24, H * 0.44, 170, 20); gear(mid, W * 0.38, H * 0.3, 90, 14); gear(mid, W * 0.76, H * 0.5, 210, 24)
    for x in [0.08, 0.5, 0.92]: truss(mid, W * x - 50, H * 0.18, H * 0.82, 100, 80, 10)
    mid.line([(0, H * 0.24), (W, H * 0.3)], 9)                       # the bucket line
    for k in range(14):
        bx = k * W / 14 + 40; by = H * 0.24 + (H * 0.06) * bx / W
        mid.line([(bx, by), (bx, by + 40)], 3); mid.poly([(bx - 14, by + 40), (bx + 14, by + 40), (bx + 10, by + 66), (bx - 10, by + 66)])
    V.add(mid, 0.3, 1.6)
    terr = Layer()
    # the pans, stepping down toward the light, their rims bright with salt
    for k in range(5):
        y = H * (0.7 + k * 0.06); x0 = W * (0.0 + k * 0.05); x1 = W * (1.0 - k * 0.04)
        terr.rect(x0, y, x1, H)
        terr.rect(x0, y, x1, y + 5, 255, 'a')
    V.add(terr, 0.2, 1.2, accent_v=0.8)
    V.rays(W * 0.42, H * 0.95, [math.pi - 0.3, math.pi - 0.05, math.pi + 0.2], H * 0.9, 60, 0.16, 30)
    near = Layer()
    near.rect(0, 0, W, H * 0.05)
    for x in [0.02, 0.97]: near.rect(W * x - 40, 0, W * x + 40, H)
    for k in range(4):
        hx = W * (0.15 + k * 0.23); near.line([(hx, 0), (hx, H * (0.2 + rng.random() * 0.2))], 5)
    V.add(near, 0.1, 0.9)
    V.finish('saltworks', ["#130e0a", "#cbbb9e", "#7c6c58", "#f2e8d2", "#ffe0ac"])

def cloister(rng):
    # the cloister garth: arcades receding on every side, a dead tree in the open middle under the sky's light
    V = Vista(rng, 0.34, 0.62, 0.42, 0.6)
    V.source(W * 0.5, H * 0.05, 700, 0.32, 0.2)
    V.rays(W * 0.5, -40, [-0.25, -0.08, 0.06, 0.22], H * 1.1, 70, 0.2, 24)
    for depth, (base, bay, hgt, colw, val, blur) in enumerate([(0.62, 70, 0.16, 10, 0.5, 2.6), (0.7, 110, 0.24, 16, 0.38, 1.8), (0.82, 170, 0.36, 26, 0.24, 1.2)]):
        L = Layer()
        arcade(L, -bay, W + bay, H * base, bay, H * hgt, colw)
        # ivy hanging from the arcade
        for k in range(int(W / bay)):
            x = k * bay + rng.random() * bay
            ytop = H * base - H * hgt - bay * 0.55
            L.taper([(x, ytop), (x + 6, ytop + 40 + rng.random() * 90)], 10, 2, 255, 'a')
        L.rect(-10, H * base, W + 10, H)
        V.add(L, val, blur, accent_v=0.7)
    tree = Layer()
    # the dead tree in the garth
    tx = W * 0.5; base = H * 0.62
    tree.taper([(tx, base), (tx + 6, base - 120), (tx - 4, base - 220)], 30, 12)
    for k in range(9):
        a = -math.pi / 2 + (k - 4) * 0.32; l = 70 + rng.random() * 90
        sx, sy = tx + rng.random() * 8 - 4, base - 140 - rng.random() * 80
        tree.taper(curve((sx, sy), (sx + math.cos(a) * l * 0.6, sy + math.sin(a) * l * 0.4), (sx + math.cos(a) * l, sy + math.sin(a) * l), 10), 9, 1.5)
    V.add(tree, 0.36, 1.6)
    near = Layer()
    near.rect(0, 0, W, H * 0.08)
    for k in range(int(W / 240) + 1):
        x = k * 240 + 60
        near.taper([(x, H * 0.08), (x + 10, H * 0.08 + 80 + rng.random() * 140)], 16, 3, 255, 'a')
    V.add(near, 0.1, 0.9, accent_v=0.5)
    V.finish('cloister', ["#090f0a", "#8fb088", "#456648", "#a2d47c", "#e2f6c2"])

def choir(rng):
    # the nave of the great choir: columns receding to the rose window, the floor drowned and mirroring it
    V = Vista(rng, 0.22, 0.44, 0.34, 0.66)
    cx, cy = W * 0.5, H * 0.3
    V.source(cx, cy, 420, 0.26, 0.45)
    far = Layer()
    rose(far, cx, cy, 150)
    for dx in (-330, -230, 230, 330): lancet(far, cx + dx, H * 0.3, 18, H * 0.22, 120)
    # the organ, under the rose
    for k in range(-9, 10):
        h = 120 + (9 - abs(k)) * 14
        far.rect(cx + k * 16 - 6, H * 0.66 - h, cx + k * 16 + 6, H * 0.66)
    V.add(far, 0.4, 2.4, glow_v=0.32)
    V.rays(cx, cy, [-0.5, -0.25, 0.0, 0.25, 0.5], H * 0.9, 50, 0.16, 20)
    # columns marching away on both sides toward the window
    vp = (cx, H * 0.4)
    for depth in range(4):
        L = Layer()
        s = 0.35 + depth * 0.22
        for side in (-1, 1):
            x = cx + side * (180 + depth * 180) * (1 + depth * 0.4)
            top = vp[1] - H * 0.36 * s; bot = H * 0.72 + depth * 30
            column(L, x, top, bot, 18 + depth * 12)
            pointed_arch(L, x - side * (18 + depth * 12) * 0.3, x - side * (180 * (0.9 + depth * 0.2)), top, 120 * s, 10 + depth * 6)
        if depth == 3:
            L.rect(0, 0, W, H * 0.06)
            pointed_arch(L, W * 0.04, W * 0.96, H * 0.5, H * 0.46, 46)
        V.add(L, 0.42 - depth * 0.08, 2.4 - depth * 0.45)
    V.water(int(H * 0.74), 0.55)
    V.finish('choir', ["#080a15", "#7c8ac4", "#323f78", "#e2be6a", "#ffe8b4"])

def ribvault(rng):
    # inside the giant: ribs arching overhead in pairs, receding into the dust, the spine running along the top
    V = Vista(rng, 0.3, 0.64, 0.48, 0.7)
    V.source(W * 0.5, H * 0.55, 600, 0.3, 0.25)
    V.rays(W * 0.62, -60, [0.18, 0.3, 0.42], H * 1.2, 50, 0.22, 22)
    vx, vy = W * 0.5, H * 0.5
    for depth in range(5):
        L = Layer()
        s = 0.3 + depth * 0.2
        spread = 260 + depth * 300
        for side in (-1, 1):
            for k in range(2):
                x0 = vx + side * spread * (0.6 + k * 0.5)
                top = vy - H * 0.5 * s - 40 * depth
                L.taper(curve((x0, H * 0.9), (x0 + side * 140 * s, vy - H * 0.2 * s), (vx + side * 30 * s, top), 26), 34 * s + 6, 10 * s + 3)
        # the spine
        for k in range(-6, 7):
            L.ellipse(vx + k * 90 * s, vy - H * 0.5 * s - 40 * depth - 6, 30 * s, 22 * s)
        V.add(L, 0.5 - depth * 0.085, 2.8 - depth * 0.45, accent_v=None)
    near = Layer()
    near.rect(0, H * 0.9, W, H)
    for k in range(18):
        x = rng.random() * W; near.ellipse(x, H * 0.9, 20 + rng.random() * 60, 10 + rng.random() * 26)
    V.add(near, 0.12, 0.9)
    V.finish('ribvault', ["#120c06", "#c6a878", "#785c36", "#eed8a8", "#ffd8a0"])

def gullet(rng):
    # the throat: rings of gristle receding into the dark, tendrils, and the things that glow in it
    V = Vista(rng, 0.12, 0.22, 0.14, 0.5)
    cx, cy = W * 0.5, H * 0.52
    V.source(cx, cy, 300, 0.12, 0.25)
    for depth in range(6):
        L = Layer()
        s = 0.25 + depth * 0.22
        rx, ry = W * 0.5 * s * 1.3, H * 0.5 * s * 1.3
        L.ellipse(cx, cy, rx + 60 * s + 30, ry + 60 * s + 30)
        L.ellipse(cx, cy, rx, ry, 0)
        for k in range(int(10 + depth * 3)):
            a = rng.random() * math.tau
            x = cx + math.cos(a) * rx; y = cy + math.sin(a) * ry
            L.taper(curve((x, y), (x + (cx - x) * 0.2, y + 40 * s), (x + (cx - x) * 0.3, y + 120 * s + 60), 12), 14 * s + 3, 1.5)
            if rng.random() < 0.4: L.ellipse(x + (cx - x) * 0.3, y + 120 * s + 60, 5 * s + 2, 5 * s + 2, 255, 'g')
        V.add(L, 0.2 - depth * 0.025, 2.6 - depth * 0.35, glow_v=0.9)
    V.finish('gullet', ["#03070a", "#3c686a", "#1a3e46", "#40e8ce", "#7cf8e8"])

def skull(rng):
    # inside the Skull: a dome of bone, the eye sockets open on the night, the lamp's old machinery, the stair
    V = Vista(rng, 0.26, 0.58, 0.42, 0.6)
    # the sockets open on the night: pale sky in them, not fire, and the stars
    far = Layer()
    for ex in (W * 0.33, W * 0.67):
        far.ellipse(ex, H * 0.32, 150, 105)
        for k in range(14):
            far.ellipse(ex + (rng.random() - 0.5) * 220, H * 0.32 + (rng.random() - 0.5) * 150, 2, 2, 255, 'g')
    V.add(far, 0.62, 2.0, glow_v=0.5)
    V.source(W * 0.5, H * 0.9, 520, 0.2, 0.25)
    V.rays(W * 0.33, H * 0.32, [-0.2, 0.1, 0.4], H * 0.8, 50, 0.14, 22)
    V.rays(W * 0.67, H * 0.32, [-0.4, -0.1, 0.2], H * 0.8, 50, 0.14, 22)
    mid = Layer()
    # the dome, its sutures, the sockets cut through it
    mid.poly([(0, 0), (W, 0), (W, H * 0.7)] + [(W * (1 - t), H * 0.7 - math.sin(t * math.pi) * H * 0.62) for t in np.linspace(0, 1, 40)] + [(0, H * 0.7)])
    for ex in (W * 0.33, W * 0.67): mid.ellipse(ex, H * 0.32, 140, 96, 0)
    # the spiral stair
    for k in range(26):
        t = k / 25; x = W * 0.5 + math.sin(t * math.pi * 3) * 160; y = H * 0.92 - t * H * 0.6
        mid.rect(x - 50, y, x + 50, y + 10)
    mid.rect(W * 0.5 - 12, H * 0.3, W * 0.5 + 12, H)
    V.add(mid, 0.28, 1.4)
    # the lamp's clockwork, hanging in the middle
    gearL = Layer()
    gear(gearL, W * 0.5, H * 0.22, 80, 16); gear(gearL, W * 0.56, H * 0.32, 40, 10)
    gearL.line([(W * 0.5, 0), (W * 0.5, H * 0.16)], 6)
    V.add(gearL, 0.2, 1.0)
    V.finish('skull', ["#110803", "#c28452", "#683c1a", "#ffaa52", "#ffc674"])


REGIONS = {'wrackline': wrackline, 'saltworks': saltworks, 'cloister': cloister, 'choir': choir, 'ribvault': ribvault, 'gullet': gullet, 'skull': skull}
if __name__ == '__main__':
    args = sys.argv[1:]
    if '--preview' in args:
        i = args.index('--preview'); PREVIEW = args[i + 1]; args = args[:i] + args[i + 2:]
    names = args or list(REGIONS)
    for i, n in enumerate(names):
        REGIONS[n](np.random.default_rng(2000 + list(REGIONS).index(n)))
