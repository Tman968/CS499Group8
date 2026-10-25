"""Generates the parallax backdrop layers in JumpCraftGame/Assets/Art/Sprites/Backdrop.

Every layer wraps horizontally (its left and right edges line up) so ParallaxLayer can repeat it
forever. Art is 16 px per unit like the rest of the sprites, and the palette is taken from
Mountain.png. Output is deterministic: rerunning produces identical images.

    python Tools/generate_backdrop.py

Layer heights are tuned against CameraFollow's vertical range (camera y 0 to 3.5, orthographic
size 5) and the parallax factors set by JumpCraft > Build Backdrop: most of the tall peaks in the
main range only come into view once the camera has climbed to the highest platforms.
"""
import math
import random
from pathlib import Path

from PIL import Image

OUT = Path(__file__).resolve().parent.parent / "JumpCraftGame/Assets/Art/Sprites/Backdrop"

# Colors sampled from Mountain.png, darkest to lightest.
ROCK = [(16, 14, 24), (24, 22, 36), (34, 32, 50), (47, 44, 66), (62, 58, 84), (80, 76, 104), (102, 98, 126)]
SNOW = [(150, 156, 182), (190, 196, 214), (222, 226, 236)]
SKY_HORIZON = (44, 37, 66)

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def dither(x, y, t):
    """Ordered dither: True for a fraction t of pixels in a fixed 4x4 pattern."""
    return (BAYER[y & 3][x & 3] + 0.5) / 16 < t


def hash2(x, y):
    """Deterministic per-pixel random value in [0, 1)."""
    n = (x * 374761393 + y * 668265263) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65536


def mix(a, b, t):
    return tuple(round(p + (q - p) * t) for p, q in zip(a, b))


def clamp(v, lo, hi):
    return max(lo, min(hi, v))


class Noise1D:
    """Smooth value noise in [-1, 1] that repeats every `period` pixels."""

    def __init__(self, rng, period, cells):
        self.period, self.cells = period, cells
        self.values = [rng.uniform(-1, 1) for _ in range(cells)]

    def __call__(self, x):
        u = (x % self.period) / self.period * self.cells
        i = int(u)
        f = u - i
        f = f * f * (3 - 2 * f)
        a, b = self.values[i % self.cells], self.values[(i + 1) % self.cells]
        return a + (b - a) * f


def fbm(rng, period, octaves, cells):
    layers = [Noise1D(rng, period, cells * 2 ** o) for o in range(octaves)]
    return lambda x: sum(n(x) / 2 ** o for o, n in enumerate(layers)) / 1.6


class Canvas:
    """RGBA image addressed with y measured up from the bottom; x wraps around the width."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.px = self.img.load()

    def set(self, x, y, c):
        if 0 <= y < self.h:
            self.px[x % self.w, self.h - 1 - y] = c if len(c) == 4 else (*c, 255)

    def get(self, x, y):
        return self.px[x % self.w, self.h - 1 - y]

    def save(self, name):
        OUT.mkdir(parents=True, exist_ok=True)
        self.img.save(OUT / name)
        print(f"wrote {OUT / name} ({self.w}x{self.h})")


def apply_mist(c, y0, y1, mist, strength):
    """Fades every opaque pixel toward `mist`, fully below y0 and not at all above y1."""
    for y in range(min(y1, c.h)):
        t = strength * clamp((y1 - y) / (y1 - y0), 0, 1)
        for x in range(c.w):
            p = c.get(x, y)
            if p[3] and dither(x, y, t):
                c.set(x, y, mix(p[:3], mist, 0.5))


# ---------------------------------------------------------------------------------------------
# Sky and moon


def sky():
    c = Canvas(320, 192)
    rng = random.Random(1)
    bands = [(10, 8, 19), (13, 11, 24), (17, 14, 30), (21, 18, 36), (26, 22, 44), (32, 27, 53), (39, 33, 61), SKY_HORIZON]
    for y in range(c.h):
        t = ((c.h - 1 - y) / (c.h - 1)) ** 1.15 * (len(bands) - 1)
        i = min(int(t), len(bands) - 2)
        edge = clamp((t - i - 0.55) / 0.45, 0, 1)  # solid bands, dithered only at the transition
        for x in range(c.w):
            c.set(x, y, bands[i + 1] if dither(x, y, edge) else bands[i])

    # Thin cloud wisps across the middle of the sky.
    wisp = fbm(rng, c.w, 3, 5)
    for x in range(c.w):
        for band_y, thickness in ((104, 3), (88, 2), (122, 2)):
            n = wisp(x + band_y * 7)
            if n > 0.15:
                top = band_y + round(n * 3)
                for y in range(top - thickness, top):
                    base = c.get(x, y)[:3]
                    c.set(x, y, mix(base, (70, 62, 98), 0.35 if y == top - 1 else 0.2))

    # Stars, denser toward the top.
    for _ in range(170):
        x = rng.randrange(c.w)
        y = c.h - 1 - int(rng.random() ** 1.6 * c.h * 0.8)
        roll = rng.random()
        if roll < 0.65:
            c.set(x, y, (64, 62, 92))
        elif roll < 0.93:
            c.set(x, y, (150, 154, 186))
        else:
            c.set(x, y, (226, 230, 244))
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                c.set(x + dx, y + dy, (96, 98, 132))
    c.save("Backdrop_Sky.png")


def moon():
    c = Canvas(48, 48)
    cx = cy = 23.5
    for y in range(c.h):
        for x in range(c.w):
            d = math.hypot(x - cx, y - cy)
            if d <= 9.5:
                # Lit from the upper left, a soft terminator on the lower right.
                shade = (x - cx) * 0.55 - (y - cy) * 0.35 + hash2(x, y) * 2
                col = SNOW[2] if shade < 2 else SNOW[1] if shade < 4.5 else SNOW[0]
                c.set(x, y, col)
            elif d <= 13 and dither(x, y, 0.5 * (13 - d) / 3.5):
                c.set(x, y, (120, 124, 168, 70))
            elif d <= 19 and dither(x, y, 0.35 * (19 - d) / 6):
                c.set(x, y, (90, 92, 140, 40))
    for x, y, r in ((21, 27, 2), (26, 22, 1.5), (19, 21, 1), (25, 28, 1)):
        for yy in range(int(y - r), int(y + r) + 1):
            for xx in range(int(x - r), int(x + r) + 1):
                if math.hypot(xx - x, yy - y) <= r:
                    c.set(xx, yy, SNOW[1] if (xx - x) < 0 else SNOW[0])
    c.save("Backdrop_Moon.png")


# ---------------------------------------------------------------------------------------------
# Mountain ranges


def mountain_range(name, w, h, seed, peaks, rock, snow, snowline, lit_base, mist, mist_band, trees=None):
    """peaks: (x, height, left slope, right slope), all in pixels. Light comes from the upper left.

    Each peak gets a few lower shoulders drawn in front of it, so the faces break up into
    overlapping facets the way they do in Mountain.png.
    """
    rng = random.Random(seed)
    facets = []
    for x, height, left, right in peaks:
        facets.append((x, height, left, right))
        for _ in range(rng.randint(1, 3)):
            offset = rng.choice((-1, 1)) * rng.randint(16, 46)
            facets.append((x + offset, height * rng.uniform(0.55, 0.8), left * rng.uniform(0.9, 1.4), right * rng.uniform(0.9, 1.4)))
    facets.sort(key=lambda f: -f[1])  # tallest at the back

    jag = fbm(rng, w, 3, 24)
    fine = fbm(rng, w, 2, 110)
    strata = fbm(rng, 4096, 2, 300)
    wobble = fbm(rng, 4096, 2, 60)
    snow_edge = fbm(rng, w, 3, 30)
    c = Canvas(w, h)

    for i, (px, height, left, right) in enumerate(facets):
        reach = int(height / min(left, right)) + 8
        for x in range(round(px) - reach, round(px) + reach + 1):
            d = x - px
            top = round(height - abs(d) * (left if d < 0 else right) + jag(x + i * 97) * 4 + fine(x) * 1.2)
            if top < 0:
                continue
            teeth = abs((x + i * 7) % 10 - 5) * 2  # zigzag lower edge of the snow cap
            local_snow = snowline + snow_edge(x + i * 53) * 12 + teeth
            for y in range(min(top, h - 1) + 1):
                depth = top - y
                # The lit/shadow split is a ridge that leans right as it runs down from the summit.
                terminator = (height - y) * 0.3 + wobble(y * 2 + i * 300) * 5
                lit = d < terminator
                idx = lit_base if lit else lit_base - 2
                shade = depth / 45
                idx -= min(2, int(shade) + (1 if dither(x, y, shade % 1) else 0))
                s = strata(x * 1.6 + (y if lit else -y) * 1.1 + i * 400)
                idx += 1 if s > 0.4 else -1 if s < -0.45 else 0
                if depth == 0:
                    idx += 1 if lit else -1
                elif lit and d > terminator - 2:
                    idx += 1  # bright crest along the ridge line
                col = rock[clamp(idx, 0, len(rock) - 1)]

                if height > snowline + 10 and y > local_snow:
                    if lit:
                        col = snow[2] if depth == 0 or d > terminator - 2 else snow[1]
                    else:
                        col = rock[clamp(lit_base, 0, len(rock) - 1)] if s > 0.55 else snow[0]
                c.set(x, y, col)

    if trees:
        # Stands of small pines climbing the lower slopes, drawn top-down so lower trees overlap.
        stands = []
        for _ in range(w // 40):
            sx, sy = rng.randrange(w), int(trees[1] * rng.random() ** 1.5)
            stands += [(sx + rng.randint(-14, 14), sy + rng.randint(-8, 4)) for _ in range(rng.randint(4, 9))]
        for x, y in sorted(stands, key=lambda s: -s[1]):
            if y > 0 and c.get(x, y)[3] and c.get(x, y + 14)[3]:
                pine(c, x, y, rng.randint(9, 15), trees[0])

    apply_mist(c, mist_band[0], mist_band[1], mist, 0.85)
    c.save(name)


def far_range():
    rock = [mix(col, SKY_HORIZON, 0.55) for col in ROCK]
    snow = [mix(col, SKY_HORIZON, 0.5) for col in SNOW]
    rng = random.Random(20)
    w = 800
    peaks = []
    x = 0
    while x < w - 40:
        tall = rng.random() < 0.6
        peaks.append((x, rng.randint(178, 200) if tall else rng.randint(140, 166), rng.uniform(1.1, 1.6), rng.uniform(1.0, 1.5)))
        x += rng.randint(60, 110)
    mountain_range("Backdrop_FarRange.png", w, 208, 21, peaks, rock, snow, 158, 4, (58, 52, 86), (60, 140))


def main_range():
    rock = [mix(col, SKY_HORIZON, 0.1) for col in ROCK]
    snow = [mix(col, (60, 56, 90), 0.18) for col in SNOW]
    pines = [(13, 16, 24), (17, 22, 31), (22, 29, 38), (28, 37, 46), (36, 47, 56)]
    # 60 units wide: five times the 12-unit Mountain.png it replaces. With the camera at its
    # lowest the top of the screen cuts this layer at 168 px, so only the three short summits
    # show; the rest come into view as the camera climbs toward the highest platforms.
    peaks = [
        (40, 198, 1.35, 1.10),
        (128, 156, 1.30, 1.50),
        (214, 206, 1.15, 1.30),
        (318, 186, 1.40, 1.20),
        (402, 148, 1.60, 1.60),
        (486, 202, 1.20, 1.10),
        (590, 180, 1.30, 1.45),
        (676, 162, 1.50, 1.30),
        (762, 200, 1.10, 1.25),
        (864, 188, 1.35, 1.30),
    ]
    mountain_range("Backdrop_MainRange.png", 960, 216, 31, peaks, rock, snow, 166, 3, (48, 43, 72), (70, 120), trees=(pines, 130))


# ---------------------------------------------------------------------------------------------
# Trees and props


def pine(c, x0, y0, height, ramp):
    """Tiered pine, lit from the left. ramp: dark to light, at least 5 colors."""
    trunk = max(2, height // 9)
    for y in range(trunk):
        c.set(x0, y0 + y, ramp[1])
        if height > 40:
            c.set(x0 + 1, y0 + y, ramp[0])
    tiers = max(2, height // 11)
    foliage = height - trunk
    half = max(2.0, height * 0.24)
    for i in range(foliage):
        y = y0 + trunk + i
        t = i / foliage
        tier = (t * tiers) % 1.0
        width = half * (1 - t) ** 0.9 * (0.6 + 0.4 * (1 - tier))
        width += 1 if hash2(x0 * 7 + i, y) > 0.7 else 0
        w = int(width)
        if t > 0.93:
            w = 0
        for dx in range(-w, w + 1):
            r = dx / (w + 0.5)
            if dx == -w and w > 1:
                col = ramp[4] if hash2(x0 + dx, y) > 0.4 else ramp[3]
            elif r < -0.3:
                col = ramp[3]
            elif r < 0.25:
                col = ramp[2]
            elif dx == w:
                col = ramp[0]
            else:
                col = ramp[1]
            c.set(x0 + dx, y, col)


def dead_tree(c, x0, y0, height, col, rng):
    def branch(x, y, angle, length, depth):
        for _ in range(int(length)):
            x += math.cos(angle)
            y += math.sin(angle)
            c.set(round(x), round(y), col)
        if depth:
            for _ in range(rng.randint(1, 2)):
                branch(x, y, angle + rng.uniform(-0.8, 0.8), length * rng.uniform(0.45, 0.7), depth - 1)

    for y in range(height // 2):
        c.set(x0, y0 + y, col)
        c.set(x0 + 1, y0 + y, col)
    branch(x0, y0 + height // 2, math.pi / 2 + rng.uniform(-0.2, 0.2), height / 2, 3)
    branch(x0, y0 + height // 3, math.pi / 2 + rng.choice((-0.9, 0.9)), height / 3, 2)


def rock(c, x0, y0, w, hgt, ramp):
    for dx in range(-w, w + 1):
        top = int(hgt * math.sqrt(max(0.0, 1 - (dx / (w + 0.5)) ** 2)))
        for y in range(top + 1):
            col = ramp[3] if (y == top and dx < 0) else ramp[2] if dx < 0 else ramp[1]
            c.set(x0 + dx, y0 + y, col)


def ruined_tower(c, x0, y0, stone):
    """A small broken watchtower with one lit window."""
    w, hgt = 15, 44
    for y in range(hgt):
        # Slight taper toward the top.
        inset = 1 if y > hgt * 0.6 else 0
        for dx in range(inset, w - inset):
            if y > hgt - 9 and dx > 8 and y > hgt - 9 + (dx - 8) * 1.5:
                continue  # crumbled corner
            col = stone[2] if dx < 5 else stone[1] if dx < w - 1 - inset else stone[0]
            if (y % 4 == 0 and dx % 6 != 3) or (y % 4 != 0 and (dx + (y // 4) * 3) % 7 == 0):
                col = stone[0]
            c.set(x0 + dx, y0 + y, col)
    for dx in range(1, 9, 3):  # battlements on the side that's still standing
        c.set(x0 + dx, y0 + hgt, stone[2] if dx < 5 else stone[1])
        c.set(x0 + dx + 1, y0 + hgt, stone[2] if dx < 5 else stone[1])
        c.set(x0 + dx, y0 + hgt + 1, stone[2] if dx < 5 else stone[1])
    for y in range(1, 7):  # doorway
        for dx in range(6, 9):
            c.set(x0 + dx, y0 + y, (8, 7, 12))
    win_x, win_y = x0 + 6, y0 + 28
    for y in range(5):
        for dx in range(3):
            c.set(win_x + dx, win_y + y, (255, 214, 120) if dx < 2 and y < 4 else (196, 140, 72))
    c.set(win_x + 1, win_y + 5, (255, 232, 170))
    # Faint glow around the window.
    for y in range(-3, 9):
        for dx in range(-3, 6):
            px, py = win_x + dx, win_y + y
            if c.get(px, py)[3] and not (0 <= dx < 3 and 0 <= y < 6) and dither(px, py, 0.3):
                c.set(px, py, mix(c.get(px, py)[:3], (196, 140, 72), 0.35))


def hills():
    w, h = 640, 128
    rng = random.Random(40)
    ground = fbm(rng, w, 3, 5)
    clearing = fbm(rng, w, 2, 7)
    soil = [(12, 13, 20), (16, 18, 27), (21, 24, 34), (28, 32, 44), (38, 42, 56)]
    back_trees = [mix(col, (40, 40, 64), 0.35) for col in [(10, 14, 20), (15, 22, 28), (21, 31, 36), (29, 42, 46), (40, 56, 56)]]
    front_trees = [(9, 12, 17), (13, 19, 24), (18, 28, 31), (25, 39, 39), (36, 54, 50)]
    c = Canvas(w, h)

    def gh(x):
        return round(68 + ground(x) * 14)

    for x in range(w):
        top = gh(x)
        for y in range(top + 1):
            idx = 3 if y == top else 2 if top - y < 3 else 1 if top - y < 20 else 0
            if idx == 2 and hash2(x, y) < 0.3:
                idx = 1
            c.set(x, y, soil[idx])

    tower_x = 236
    x = 0
    while x < w:
        if clearing(x) < 0.25:
            pine(c, x, gh(x) + 4, rng.randint(12, 22), back_trees)
        x += rng.randint(2, 5)
    ruined_tower(c, tower_x, gh(tower_x + 5) - 3, [(20, 20, 30), (34, 33, 48), (52, 50, 70)])
    x = 0
    while x < w:
        near_tower = tower_x - 10 < x < tower_x + 20
        if clearing(x) < 0.1 and not near_tower:
            pine(c, x, gh(x) - 2, rng.randint(18, 34), front_trees)
        x += rng.randint(4, 9)

    apply_mist(c, 30, 64, (40, 44, 64), 0.8)
    c.save("Backdrop_Hills.png")


def near_forest():
    w, h = 480, 112
    rng = random.Random(50)
    ground = fbm(rng, w, 3, 6)
    soil = [(6, 7, 10), (9, 10, 14), (13, 15, 20), (19, 22, 28), (28, 34, 38)]
    trees = [(6, 8, 11), (9, 13, 16), (13, 19, 22), (19, 28, 28), (30, 44, 38)]
    c = Canvas(w, h)

    def gh(x):
        return round(9 + ground(x) * 4)

    for x in range(w):
        top = gh(x)
        for y in range(top + 1):
            c.set(x, y, soil[2] if y == top else soil[1] if top - y < 3 else soil[0])
        if hash2(x, 99) < 0.35:  # grass tufts
            for y in range(1, 1 + int(hash2(x, 98) * 3) + 1):
                c.set(x, top + y, trees[3] if y == 1 else trees[2])

    # Clusters of tall pines with open gaps between them, so the hills show through.
    x = 10
    while x < w - 20:
        for _ in range(rng.randint(2, 5)):
            pine(c, x + rng.randint(-6, 6), gh(x) - 1, rng.randint(44, 92), trees)
            x += rng.randint(8, 16)
        x += rng.randint(44, 90)
    for x in (rng.randrange(w) for _ in range(3)):
        dead_tree(c, x, gh(x), rng.randint(28, 40), trees[1], rng)
    for x in (rng.randrange(w) for _ in range(7)):
        rock(c, x, gh(x) - 1, rng.randint(2, 5), rng.randint(2, 4), soil)
    c.save("Backdrop_NearForest.png")


if __name__ == "__main__":
    sky()
    moon()
    far_range()
    main_range()
    hills()
    near_forest()
