"""Generates the elemental wands and the elemental props:

    JumpCraftGame/Assets/Art/Sprites/Wands/   HeldWandFire, HeldWandIce, HeldWandLightning, HeldWandWind
    JumpCraftGame/Assets/Art/Sprites/Props/   Crate, Bramble, PendulumBlade, RuneLift

    python Tools/generate_element_sprites.py

Wands are drawn at the wizard's pixel scale, pointing up, on a WAND_W x WAND_H canvas with a
two-pixel shaft in columns 5 and 6. The hand grips row WAND_GRIP[1], between the shaft's columns,
and each wand's stream pours from its gem WAND_TIPS pixels above the grip. WandBelt.cs builds the
sprites with that pivot; change both together.

RuneLift's runes sit at RUNE_COLUMNS on RUNE_ROW using RUNE_GLYPHS; RuneLift.cs lights the same
pixels when it is charged, so change both together.

Output is deterministic: rerunning produces identical images.
"""
import random
from pathlib import Path

from generate_wizard_frames import OUTLINE, Grid, add_outline

SPRITES = Path(__file__).resolve().parent.parent / "JumpCraftGame/Assets/Art/Sprites"
WANDS = SPRITES / "Wands"
PROPS = SPRITES / "Props"

WAND_W, WAND_H = 11, 27
WAND_GRIP = (6.0, 21.5)                                              # mirrored in WandBelt.GripPixel
WAND_TIPS = {"Fire": 18, "Ice": 20.5, "Lightning": 18.5, "Wind": 17.5}  # mirrored in WandBelt.TipPixels


def rgb(r, g, b, a=255):
    return (r, g, b, a)


def fill(g, pts, color):
    for x, y in pts:
        g.put(x, y, color)


def rect(g, x0, y0, x1, y1, color):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            g.put(x, y, color)


def shaft(g, top, bottom, light, dark, x=5):
    """A two-pixel shaft, lit on the left."""
    for y in range(top, bottom + 1):
        g.put(x, y, light)
        g.put(x + 1, y, dark)


def pommel(g, y, light, mid, dark):
    """A knob at the butt of the wand."""
    fill(g, [(4, y), (5, y), (6, y), (7, y)], mid)
    fill(g, [(5, y + 1), (6, y + 1)], dark)
    g.put(5, y, light)


def grip_wrap(g, top, bottom, a, b):
    """Leather wrapped where the hand holds it, in alternating bands."""
    for y in range(top, bottom + 1):
        c = a if (y - top) % 2 == 0 else b
        g.put(5, y, c)
        g.put(6, y, c)


# ---------------------------------------------------------------- wands

def fire_wand():
    """Charred wood split by glowing cracks, iron claws gripping a burning crystal."""
    g = Grid(WAND_W, WAND_H)
    wood_l, wood_d = rgb(58, 40, 32), rgb(34, 24, 22)
    crack, crack_hot = rgb(214, 86, 28), rgb(255, 178, 64)
    shaft(g, 8, 24, wood_l, wood_d)
    for y, hot in ((10, False), (13, True), (16, False), (23, True)):
        g.put(5 if y % 2 else 6, y, crack_hot if hot else crack)
    grip_wrap(g, 18, 22, rgb(70, 40, 34), rgb(46, 28, 26))
    pommel(g, 25, rgb(120, 110, 108), rgb(80, 72, 74), rgb(48, 42, 46))
    # Iron claws.
    iron_l, iron_d = rgb(120, 112, 118), rgb(70, 64, 72)
    fill(g, [(3, 7), (3, 6), (4, 8), (7, 8), (8, 7), (8, 6), (3, 5), (8, 5)], iron_d)
    fill(g, [(4, 7), (7, 7), (3, 4), (8, 4)], iron_l)
    # The fire crystal: blood red at the rim, white-hot at the heart.
    crystal = {
        (5, 1): rgb(200, 46, 24), (6, 1): rgb(160, 30, 20),
        (4, 2): rgb(200, 46, 24), (5, 2): rgb(255, 150, 40), (6, 2): rgb(255, 126, 34), (7, 2): rgb(150, 28, 20),
        (4, 3): rgb(230, 80, 30), (5, 3): rgb(255, 230, 140), (6, 3): rgb(255, 180, 60), (7, 3): rgb(180, 40, 22),
        (4, 4): rgb(230, 80, 30), (5, 4): rgb(255, 214, 92), (6, 4): rgb(255, 150, 40), (7, 4): rgb(180, 40, 22),
        (4, 5): rgb(200, 46, 24), (5, 5): rgb(255, 126, 34), (6, 5): rgb(230, 80, 30), (7, 5): rgb(150, 28, 20),
        (5, 6): rgb(180, 40, 22), (6, 6): rgb(140, 26, 20),
        (5, 7): rgb(104, 18, 20), (6, 7): rgb(104, 18, 20),
    }
    for (x, y), c in crystal.items():
        g.put(x, y, c)
    g.put(5, 2, rgb(255, 250, 222))
    add_outline(g)
    return g


def ice_wand():
    """Pale bone banded with frost, crowned by a long faceted icicle."""
    g = Grid(WAND_W, WAND_H)
    bone_l, bone_d = rgb(196, 204, 214), rgb(132, 142, 164)
    shaft(g, 9, 24, bone_l, bone_d)
    for y in (11, 15):
        fill(g, [(4, y), (7, y)], rgb(176, 232, 255))
        fill(g, [(5, y), (6, y)], rgb(240, 252, 255))
    grip_wrap(g, 18, 22, rgb(64, 78, 108), rgb(42, 52, 80))
    pommel(g, 25, rgb(240, 252, 255), rgb(176, 232, 255), rgb(96, 184, 236))
    # The icicle: a tall diamond, lit down its left facet.
    rows = [(0, 5, 5), (1, 5, 6), (2, 4, 6), (3, 4, 7), (4, 4, 7), (5, 3, 7), (6, 3, 7), (7, 4, 7), (8, 4, 6)]
    for y, x0, x1 in rows:
        for x in range(x0, x1 + 1):
            if x == x0:
                c = rgb(240, 252, 255)
            elif x == x1:
                c = rgb(42, 88, 168)
            elif x == x0 + 1:
                c = rgb(176, 232, 255)
            else:
                c = rgb(96, 184, 236)
            g.put(x, y, c)
    g.put(5, 3, rgb(255, 255, 255))
    # Small shards breaking off the sides.
    fill(g, [(2, 7), (8, 6)], rgb(176, 232, 255))
    fill(g, [(1, 6), (9, 5)], rgb(240, 252, 255))
    add_outline(g)
    return g


def lightning_wand():
    """A dark iron rod wound with copper, splitting into two tines with a violet spark between."""
    g = Grid(WAND_W, WAND_H)
    iron_l, iron_d = rgb(98, 100, 114), rgb(56, 56, 70)
    shaft(g, 7, 24, iron_l, iron_d)
    copper_l, copper_d = rgb(196, 124, 70), rgb(124, 70, 42)
    for y in range(8, 17):
        if y % 2 == 0:
            fill(g, [(4, y), (5, y)], copper_l)
            fill(g, [(6, y), (7, y)], copper_d)
    grip_wrap(g, 18, 22, rgb(54, 42, 62), rgb(36, 28, 44))
    pommel(g, 25, rgb(196, 124, 70), rgb(124, 70, 42), rgb(80, 44, 30))
    # The fork.
    fill(g, [(4, 6), (3, 5), (3, 4), (2, 3), (2, 2), (2, 1)], iron_l)
    fill(g, [(7, 6), (8, 5), (8, 4), (9, 3), (9, 2), (9, 1)], iron_d)
    fill(g, [(2, 0), (9, 0)], copper_l)
    # The spark held between the tines.
    fill(g, [(5, 2), (6, 2), (5, 4), (6, 4), (4, 3), (7, 3)], rgb(176, 136, 255))
    fill(g, [(5, 3), (6, 3)], rgb(248, 244, 255))
    add_outline(g)
    return g


def wind_wand():
    """Twisted pale driftwood ending in an open loop that holds a wisp, with ribbons trailing."""
    g = Grid(WAND_W, WAND_H)
    wood_l, wood_d = rgb(150, 138, 112), rgb(96, 86, 72)
    for y in range(9, 25):
        # The twist: the light side swaps every few pixels.
        if (y // 3) % 2 == 0:
            g.put(5, y, wood_l)
            g.put(6, y, wood_d)
        else:
            g.put(5, y, wood_d)
            g.put(6, y, wood_l)
    grip_wrap(g, 18, 22, rgb(72, 98, 98), rgb(48, 66, 68))
    pommel(g, 25, rgb(150, 138, 112), rgb(96, 86, 72), rgb(60, 54, 46))
    # The loop.
    loop = [(4, 1), (5, 0), (6, 0), (7, 1), (8, 2), (8, 3), (8, 4), (8, 5), (7, 6), (6, 7), (5, 7), (4, 6),
            (3, 5), (3, 4), (3, 3), (3, 2)]
    for i, (x, y) in enumerate(loop):
        g.put(x, y, wood_l if x <= 5 else wood_d)
    fill(g, [(5, 8), (6, 8)], wood_d)
    # The wisp in the loop.
    fill(g, [(5, 3), (6, 4), (5, 5)], rgb(148, 186, 176))
    fill(g, [(6, 3), (5, 4), (6, 5)], rgb(214, 238, 226))
    # Ribbons trailing from the neck.
    fill(g, [(7, 9), (8, 10), (9, 11), (9, 12)], rgb(176, 222, 200))
    fill(g, [(4, 10), (3, 11), (2, 12)], rgb(120, 160, 150))
    add_outline(g)
    return g


# ---------------------------------------------------------------- props

def crate():
    """A rotten wooden crate bound with iron."""
    g = Grid(16, 16)
    rnd = random.Random(7)
    plank_l, plank_m, plank_d = rgb(92, 66, 46), rgb(70, 50, 36), rgb(46, 32, 26)
    for y in range(1, 15):
        for x in range(1, 15):
            board = (y - 1) // 4
            c = plank_m if board % 2 == 0 else plank_l
            if (y - 1) % 4 == 3:
                c = plank_d
            if rnd.random() < 0.08:
                c = plank_d
            g.put(x, y, c)
    # A cross brace.
    for i in range(1, 15):
        g.put(i, i, rgb(104, 76, 52))
        g.put(min(14, i + 1), i, plank_d)
    # Iron corners and rivets.
    iron, rivet = rgb(66, 62, 70), rgb(132, 126, 134)
    for x, y in ((1, 1), (2, 1), (1, 2), (14, 1), (13, 1), (14, 2), (1, 14), (2, 14), (1, 13), (14, 14), (13, 14), (14, 13)):
        g.put(x, y, iron)
    for x, y in ((2, 2), (13, 2), (2, 13), (13, 13)):
        g.put(x, y, rivet)
    add_outline(g)
    return g


def bramble():
    """A thicket of dead, thorned vines."""
    w, h = 30, 20
    g = Grid(w, h)
    rnd = random.Random(11)
    vine_l, vine_d = rgb(74, 50, 70), rgb(44, 30, 44)
    thorn = rgb(150, 132, 120)
    leaf, leaf_d = rgb(98, 72, 44), rgb(58, 44, 30)
    # Vines arch up from the ground and tangle.
    for v in range(18):
        x = 2 + v * (w - 5) / 17 + rnd.uniform(-1, 1)
        y = h - 1
        dx = rnd.uniform(-0.9, 0.9)
        height = rnd.uniform(9, h - 1) if 3 <= v <= 14 else rnd.uniform(5, 11)
        for step in range(int(height * 1.6)):
            xi, yi = int(round(x)), int(round(y))
            g.put(xi, yi, vine_l if step % 3 else vine_d)
            g.put(xi + 1, yi, vine_d)
            if rnd.random() < 0.18:
                g.put(xi + (2 if rnd.random() < 0.5 else -1), yi - 1, thorn)
            if rnd.random() < 0.05:
                g.put(xi - 1, yi, leaf)
                g.put(xi - 1, yi + 1, leaf_d)
            x += dx + rnd.uniform(-0.6, 0.6)
            y -= 0.62
            dx *= 0.97
            x = max(1, min(w - 3, x))
            if y < 1:
                break
    add_outline(g)
    return g


def pendulum_blade():
    """A rusted crescent blade hung from a ring at the top center."""
    w, h = 22, 17
    g = Grid(w, h)
    cx = 10.5
    steel_l, steel_m, steel_d = rgb(196, 196, 206), rgb(132, 132, 146), rgb(76, 74, 88)
    rust = rgb(128, 64, 40)
    rnd = random.Random(5)
    # The ring.
    for x, y in ((10, 0), (11, 0), (9, 1), (12, 1), (9, 2), (12, 2), (10, 3), (11, 3)):
        g.put(x, y, rgb(90, 86, 96))
    # The arm down to the blade.
    for y in range(4, 8):
        g.put(10, y, steel_m)
        g.put(11, y, steel_d)
    # Crescent: the band between two circles.
    for y in range(6, h):
        for x in range(w):
            dx, dy = x + 0.5 - cx, y + 0.5 - 3.5
            outer = (dx / 10.5) ** 2 + (dy / 13) ** 2 <= 1
            inner = (dx / 8.5) ** 2 + ((dy + 3.5) / 11) ** 2 <= 1
            if outer and not inner:
                edge = (dx / 10.5) ** 2 + (dy / 13) ** 2 > 0.82
                c = steel_l if edge else (steel_m if dx < 0 else steel_d)
                if not edge and rnd.random() < 0.12:
                    c = rust
                g.put(x, y, c)
    add_outline(g)
    return g


RUNE_ROW = 3
RUNE_COLUMNS = [7, 14, 20, 26, 33]
RUNE_GLYPHS = [
    ["x.x", ".x.", "x.x"],
    [".x.", "xxx", ".x."],
    ["xx.", ".x.", ".xx"],
    ["x..", "xxx", "..x"],
    [".xx", "x..", "xx."],
]


def rune_lift():
    """A slab of carved stone with five runes, and a copper coil and crystal hanging beneath."""
    w, h = 40, 15
    g = Grid(w, h)
    rnd = random.Random(3)
    stone_l, stone_m, stone_d = rgb(96, 88, 104), rgb(66, 60, 76), rgb(44, 40, 52)
    for y in range(0, 8):
        for x in range(0, w):
            c = stone_m
            if y == 0:
                c = stone_l
            elif y >= 6:
                c = stone_d
            elif rnd.random() < 0.1:
                c = stone_d if rnd.random() < 0.6 else stone_l
            g.put(x, y, c)
    # Carved runes (dark grooves that glow when the lift is charged).
    groove = rgb(26, 22, 34)
    for col, glyph in zip(RUNE_COLUMNS, RUNE_GLYPHS):
        for gy, row in enumerate(glyph):
            for gx, ch in enumerate(row):
                if ch == "x":
                    g.put(col - 1 + gx, RUNE_ROW - 1 + gy, groove)
    # The coil and crystal hanging under the middle.
    copper_l, copper_d = rgb(196, 124, 70), rgb(124, 70, 42)
    for y in range(8, 12):
        g.put(19, y, copper_l if y % 2 == 0 else copper_d)
        g.put(20, y, copper_d if y % 2 == 0 else copper_l)
    fill(g, [(19, 12), (20, 12)], rgb(176, 136, 255))
    fill(g, [(19, 13)], rgb(248, 244, 255))
    fill(g, [(20, 13)], rgb(98, 118, 255))
    add_outline(g)
    return g


def save(g, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    g.image().save(path)


def main():
    for name, make in [("Fire", fire_wand), ("Ice", ice_wand), ("Lightning", lightning_wand), ("Wind", wind_wand)]:
        save(make(), WANDS / f"HeldWand{name}.png")
    for name, make in [("Crate", crate), ("Bramble", bramble), ("PendulumBlade", pendulum_blade), ("RuneLift", rune_lift)]:
        save(make(), PROPS / f"{name}.png")
    print(f"wand grip {WAND_GRIP}, gems {WAND_TIPS} px above it")


if __name__ == "__main__":
    main()
