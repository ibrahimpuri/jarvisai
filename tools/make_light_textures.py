# Writes the light textures used by the Godot build (radial lamp light and the lantern cones).
# Usage: python3 tools/make_light_textures.py
import math, os
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', 'godot', 'textures')

def radial(size, power=1.8):
    im = Image.new('RGBA', (size, size)); px = im.load(); c = size / 2
    for y in range(size):
        for x in range(size):
            d = min(1, math.hypot(x + 0.5 - c, y + 0.5 - c) / c)
            px[x, y] = (255, 255, 255, int(255 * pow(1 - d, power)))
    return im

def smoothstep(a, b, x):
    t = max(0, min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t)

# A cone pointing right from the centre, soft at the edges, with a little spill around the origin.
def cone(size, spread):
    im = Image.new('RGBA', (size, size)); px = im.load(); c = size / 2
    for y in range(size):
        for x in range(size):
            dx, dy = x + 0.5 - c, y + 0.5 - c
            d = math.hypot(dx, dy) / c; v = 0
            if d < 1:
                a = abs(math.atan2(dy, dx))
                edge = 1 - smoothstep(spread * 0.55, spread * 1.05, a)
                fall = pow(1 - d, 1.15) * (1 if d < 0.6 else 1 - (d - 0.6) * 0.6)
                v = max(edge * fall, pow(max(0, 1 - d * 9), 2) * 0.8)
            px[x, y] = (255, 255, 255, int(255 * max(0, min(1, v))))
    return im

os.makedirs(OUT, exist_ok=True)
radial(256).save(os.path.join(OUT, 'light_radial.png'))
cone(512, 0.7).save(os.path.join(OUT, 'light_cone_wide.png'))
cone(512, 0.24).save(os.path.join(OUT, 'light_cone_focus.png'))
print('wrote light textures to', os.path.abspath(OUT))
