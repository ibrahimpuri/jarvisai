# Paints the surface materials the Godot build lights: one tiling texture per kind of surface (the abbey's ashlar,
# bedrock, fired brick, salt-rotted rock, the Saint's bone, the Gullet's flesh, the shore's basalt), each a detail
# map and a normal map. They are not colour: like the rooms they sit on, they are coloured from the region's
# palette, so the tide still recolours them.
#   <name>_d.png  red: grain (0.5 is the surface itself, darker pits and lighter faces around it)
#                 green: where the region's accent grows on it (moss, salt bloom, weed)
#                 blue: cavity (crevices that hold the dark)
#   <name>_n.png  the normal, in the world shader's convention (x toward the higher side, y up the screen as -y)
# Everything is built from periodic noise (spectral synthesis, wrapped cells), so every texture tiles.
# Usage: python3 tools/paint_materials.py [size]
import math, os, sys
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', 'godot', 'art', 'materials')
N = int(sys.argv[1]) if len(sys.argv) > 1 else 512


# ---- periodic building blocks ---------------------------------------------------------------------------------
def freqs(n):
    f = np.fft.fftfreq(n)
    fx, fy = np.meshgrid(f, f)
    return fx, fy, np.sqrt(fx * fx + fy * fy)

def spectral(rng, beta=2.0, lo=0.0, hi=0.5, stretch=(1.0, 1.0)):
    # fractal noise by filtering white noise in frequency: amplitude ~ 1 / f^(beta/2); tiles by construction
    fx, fy, _ = freqs(N)
    r = np.sqrt((fx * stretch[0]) ** 2 + (fy * stretch[1]) ** 2)
    amp = np.where(r > 0, 1.0 / np.maximum(r, 1e-6) ** (beta / 2), 0)
    amp *= (r >= lo) & (r <= hi)
    spec = np.fft.fft2(rng.standard_normal((N, N))) * amp
    out = np.real(np.fft.ifft2(spec))
    return norm(out)

def blur(a, sigma):
    # periodic gaussian blur in frequency space
    fx, fy, _ = freqs(N)
    g = np.exp(-2 * (math.pi ** 2) * (sigma ** 2) * (fx ** 2 + fy ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * g))

def norm(a):
    a = a - a.mean()
    s = a.std()
    return a / s if s > 0 else a

def cells(rng, count, jitter=1.0):
    # Worley noise on a torus: distance to the nearest and second-nearest point (F1, F2), and the nearest's id
    pts = rng.random((count, 2)) * N
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    f1 = np.full((N, N), 1e9, np.float32); f2 = np.full((N, N), 1e9, np.float32); idx = np.zeros((N, N), np.int32)
    for i, (px, py) in enumerate(pts):
        dx = np.abs(xx - px); dx = np.minimum(dx, N - dx)
        dy = np.abs(yy - py); dy = np.minimum(dy, N - dy)
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < f1
        f2 = np.where(closer, f1, np.minimum(f2, d))
        idx = np.where(closer, i, idx)
        f1 = np.where(closer, d, f1)
    return f1, f2, idx

def warp(a, wx, wy, amt):
    # displace a periodic field by two others (domain warping), with wrap
    yy, xx = np.mgrid[0:N, 0:N]
    sx = (xx + wx * amt).astype(np.int64) % N
    sy = (yy + wy * amt).astype(np.int64) % N
    return a[sy, sx]

def sstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)

def normals(h, strength):
    # the world shader's convention: n = normalize((hl - hr) k, (hu - hd) k, 1), y down the texture
    hl = np.roll(h, 1, axis=1); hr = np.roll(h, -1, axis=1)
    hu = np.roll(h, 1, axis=0); hd = np.roll(h, -1, axis=0)
    nx = (hl - hr) * strength; ny = (hu - hd) * strength
    l = np.sqrt(nx * nx + ny * ny + 1)
    return np.stack([nx / l, ny / l, 1 / l], -1)

def cavity_of(h, sigma=3.0, k=4.0):
    # crevices at the scale of the form, not of the grain
    hs = blur(h, 1.6)
    return np.clip((blur(h, sigma * 1.8) - hs) * k * 0.6, 0, 1)

def save(name, grain, accent, cav, h, strength):
    os.makedirs(OUT, exist_ok=True)
    g = np.clip(0.5 + grain * 0.13, 0, 1)
    d = np.stack([g, np.clip(accent, 0, 1), np.clip(cav, 0, 1)], -1)
    Image.fromarray((d * 255 + 0.5).astype(np.uint8), 'RGB').save(os.path.join(OUT, name + '_d.png'))
    n = normals(h, strength)
    Image.fromarray(((n * 0.5 + 0.5) * 255 + 0.5).astype(np.uint8), 'RGB').save(os.path.join(OUT, name + '_n.png'))
    print('wrote', name)


# ---- the surfaces ---------------------------------------------------------------------------------------------
# Each surface is mostly mid-scale form (chips, swells, strata) with only a little fine grain on top: stone seen
# from a few metres away, not sandpaper.
def grain_of(rng, broad=0.8, mid=0.55, fine=0.05):
    return (spectral(rng, 3.0, 0.002, 0.03) * broad + spectral(rng, 2.6, 0.01, 0.1) * mid
            + blur(spectral(rng, 1.8, 0.08, 0.35), 0.6) * fine)

def pits_of(rng, lo=2.1, hi=2.9, size=0.18, density=0.0025, radius=1.6):
    # small round pits, a few px across: scattered points, softened into dents, clustered where the stone is weaker
    seeds = (rng.random((N, N)) < density).astype(np.float64)
    seeds *= sstep(-0.6, 0.8, spectral(rng, 2.4, 0.003, 0.05))
    dents = blur(seeds, radius)
    return np.clip(dents / max(1e-6, dents.max()) * 2.2, 0, 1)

def cracks_of(rng, count, width=1.6, keep=0.45, warp_amt=10):
    # the edges between cells, warped so they wander, and only some of them open
    f1, f2, _ = cells(rng, count)
    e = f2 - f1
    e = warp(e, spectral(rng, 2.8, 0.001, 0.05), spectral(rng, 2.8, 0.001, 0.05), warp_amt)
    line = sstep(width, 0.0, e)
    mask = sstep(keep - 0.3, keep + 0.3, spectral(rng, 2.6, 0.001, 0.04) * 0.5 + 0.5)
    return line * mask

def patches(rng, lo, hi, soft=0.6):
    m = sstep(lo, hi, spectral(rng, 2.6, 0.002, 0.05))
    return m * sstep(-soft, soft, spectral(rng, 2.0, 0.02, 0.2))

def ashlar(rng):
    # dressed limestone: broad swells, chisel striations, pitting, a few hairline veins, lichen where it is damp
    yy = np.mgrid[0:N, 0:N][0].astype(np.float32)
    strie = np.sin(yy * (2 * math.pi * 40 / N) + spectral(rng, 3.0, 0.001, 0.02) * 1.2) * 0.12
    pits = pits_of(rng)
    veins = cracks_of(rng, 10, 1.0, 0.35, 14)
    h = grain_of(rng) + strie - pits * 1.6 - veins * 1.0
    lichen = patches(rng, 0.9, 1.7)
    save('ashlar', norm(h), lichen * 0.75, cavity_of(h) * 0.7 + pits * 0.5, blur(h, 1.1), 0.22)

def rock(rng):
    # the island's bedrock: strata warped by old pressure, a few open cracks, chips
    yy = np.mgrid[0:N, 0:N][0].astype(np.float32)
    w = spectral(rng, 3.0, 0.001, 0.03)
    strata = np.sin((yy + w * 26) * (2 * math.pi * 7 / N)) * 0.5 + np.sin((yy + w * 34) * (2 * math.pi * 19 / N)) * 0.2
    cracks = cracks_of(rng, 14, 2.0, 0.5, 16)
    chips = sstep(0.6, 1.6, spectral(rng, 2.2, 0.01, 0.1))
    h = grain_of(rng, 0.9, 0.7, 0.1) + strata * 0.8 - cracks * 1.8 - chips * 0.6
    moss = patches(rng, 1.0, 1.8) * (1 - cracks)
    save('rock', norm(h), moss * 0.65, cavity_of(h, 4) * 0.8 + cracks * 0.6, blur(h, 1.2), 0.2)

def brick(rng):
    # fired brick: coarse clay with fire pocks, and salt blooming white out of it
    pocks = pits_of(rng, density=0.004, radius=1.4)
    craters = pits_of(rng, density=0.0006, radius=3.5)
    salt = patches(rng, 0.8, 1.7)
    h = grain_of(rng, 0.6, 0.7, 0.15) - pocks * 1.3 - craters * 0.8 + salt * 0.9
    save('brick', norm(h), salt * 0.9, cavity_of(h) * 0.8 + pocks * 0.5, blur(h, 1.0), 0.24)

def saltrock(rng):
    # rock the salt has got into: faceted crystal clusters pushing out of the stone in a few places
    f1, f2, idx = cells(rng, 110)
    facet = (rng.random(110)[idx] - 0.5) * 1.0 - (f1 / max(1, f1.max())) * 0.6
    crystals = sstep(1.1, 1.8, spectral(rng, 2.6, 0.003, 0.06))
    edges = sstep(1.4, 0.2, f2 - f1)
    base = grain_of(rng, 0.9, 0.6, 0.1)
    h = base * (1 - crystals) + (facet * 1.4 + 1.0) * crystals - edges * crystals * 0.6
    save('saltrock', norm(h), crystals * 0.85, cavity_of(h) * 0.8 + edges * crystals * 0.3, blur(h, 0.8), 0.22)

def bone(rng):
    # the Saint's bone: fibres running one way, warped, pitted with pores
    fib = spectral(rng, 2.4, 0.004, 0.2, stretch=(0.15, 1.0))
    fib = warp(fib, spectral(rng, 3.0, 0.001, 0.03), spectral(rng, 3.0, 0.001, 0.03), 22)
    pores = pits_of(rng, density=0.003, radius=1.3)
    h = fib * 0.55 + spectral(rng, 3.0, 0.001, 0.03) * 0.8 - pores * 1.6
    stain = patches(rng, 0.9, 1.8)
    save('bone', norm(h), stain * 0.45, cavity_of(h) * 0.8 + pores * 0.5, blur(h, 1.0), 0.2)

def flesh(rng):
    # the Gullet's wall: soft swollen folds, veins wandering between them carrying the accent
    folds = spectral(rng, 3.2, 0.001, 0.05)
    swell = spectral(rng, 2.6, 0.008, 0.1)
    veins = cracks_of(rng, 22, 1.4, 0.55, 22)
    h = folds * 1.0 + swell * 0.6 - veins * 0.8 + spectral(rng, 1.8, 0.08, 0.3) * 0.06
    save('flesh', norm(h), veins * 0.9, cavity_of(h, 4) * 0.7 + veins * 0.2, blur(h, 1.4), 0.2)

def shore(rng):
    # sea-worn basalt: rounded pits and clusters of barnacles, weed in the hollows
    pits = pits_of(rng, density=0.0015, radius=2.6)
    f1, _, _ = cells(rng, 160)
    clus = sstep(0.8, 1.5, spectral(rng, 2.4, 0.003, 0.06))
    ring = np.exp(-((f1 - 3.0) ** 2) / 1.0) * clus
    h = grain_of(rng, 0.9, 0.6, 0.1) - pits * 1.3 + ring * 1.4
    weed = patches(rng, 0.9, 1.7) * (0.4 + pits)
    save('shore', norm(h), np.clip(weed, 0, 1) * 0.75, cavity_of(h) * 0.8 + pits * 0.4, blur(h, 0.9), 0.24)


if __name__ == '__main__':
    for i, fn in enumerate([ashlar, rock, brick, saltrock, bone, flesh, shore]):
        fn(np.random.default_rng(1000 + i))
