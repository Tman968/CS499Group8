"""Generates the skeleton's animation frames and the combat effect sprites:

    JumpCraftGame/Assets/Art/Sprites/Skeleton/  walk, throw, hurt and death strips
    JumpCraftGame/Assets/Art/Sprites/Effects/   wand orbs, orb bursts, sparks, thrown bone, bone chips

    python Tools/generate_combat_sprites.py

Skeleton.png is cut into parts (skull, body, two legs, the free arm and the club arm). Each frame
rotates and moves the parts and draws them back in order, so the art stays the source art. Strips
are FRAME_W x FRAME_H frames laid side by side; the frame canvas is Skeleton.png padded by PAD_X on
each side and PAD_TOP on top, so its bottom-center pivot lands where Skeleton.png's did.
Output is deterministic: rerunning produces identical images.

THROW_RELEASE_HAND is shared with Skeleton.cs; change both together.
"""
import math
from pathlib import Path

from PIL import Image

from generate_wizard_frames import OUTLINE, Grid, add_outline, is_outline, shade

SPRITES = Path(__file__).resolve().parent.parent / "JumpCraftGame/Assets/Art/Sprites"
SKELETON_OUT = SPRITES / "Skeleton"
EFFECTS_OUT = SPRITES / "Effects"

SRC_W, SRC_H = 26, 47
PAD_X, PAD_TOP = 9, 5
FRAME_W, FRAME_H = SRC_W + 2 * PAD_X, SRC_H + PAD_TOP

BONE_LIGHT, BONE_MID, BONE_DARK = (228, 218, 186), (190, 174, 138), (140, 120, 90)
FLASH = (255, 246, 226, 255)

# Joints, in source pixels.
FREE_SHOULDER = (5.5, 18.5)
CLUB_SHOULDER = (21.0, 18.5)
LEFT_HIP = (9.5, 37.5)
RIGHT_HIP = (16.5, 37.5)
NECK = (13.0, 15.5)
FREE_ARM_LENGTH = 18.0

# Throw poses: the free arm's swing in degrees (0 hangs down, + swings forward), whether the
# bone is still in hand, and the body lean. Frame 3 is the release.
THROW_POSES = [
    dict(arm=-40, bone=True, lean=0, club=-6, knees=0),
    dict(arm=-120, bone=True, lean=-1, club=-10, knees=1),
    dict(arm=-165, bone=True, lean=-2, club=-14, knees=1),
    dict(arm=95, bone=False, lean=1, club=10, knees=0),
    dict(arm=60, bone=False, lean=1, club=8, knees=0),
    dict(arm=20, bone=False, lean=0, club=2, knees=0),
]


def hand_at(angle, shoulder=FREE_SHOULDER, length=FREE_ARM_LENGTH):
    a = math.radians(angle)
    return shoulder[0] + math.sin(a) * length, shoulder[1] + math.cos(a) * length


# Where the bone leaves the hand, in frame pixels from the top-left. Mirrored in Skeleton.cs.
THROW_RELEASE_HAND = tuple(round(v + p, 1) for v, p in zip(hand_at(THROW_POSES[3]["arm"]), (PAD_X, PAD_TOP)))


# ---------------------------------------------------------------- splitting Skeleton.png

def part_of(x, y):
    """Which part a source pixel belongs to."""
    if y <= 15:
        return "skull"
    if x <= 6 and 18 <= y <= 38:
        return "free_arm"
    if y >= 18 and (x >= 20 or (x >= 19 and y >= 30)) and not (y >= 44 and x <= 20 and x >= 13):
        return "club_arm"
    if y >= 39 and x <= 12 or (y == 38 and 7 <= x <= 12):
        return "left_leg"
    if y >= 38 and 13 <= x <= 20:
        return "right_leg"
    return "body"


def split(src):
    parts = {name: Grid(SRC_W, SRC_H) for name in ("skull", "body", "left_leg", "right_leg", "free_arm", "club_arm")}
    for (x, y), p in src.px.items():
        parts[part_of(x, y)].px[x, y] = p
    return parts


# ---------------------------------------------------------------- posing

def transform_part(part, pivot, angle=0.0, offset=(0.0, 0.0)):
    """Rotates a part about pivot (degrees, + swings the bottom of the part forward/right) then
    moves it. Returns a new grid in frame coordinates, sampled backwards so there are no holes."""
    out = Grid(FRAME_W, FRAME_H)
    if not part.px:
        return out
    a = math.radians(angle)
    cos, sin = math.cos(a), math.sin(a)
    px, py = pivot
    xs = [x for x, _ in part.px]
    ys = [y for _, y in part.px]
    reach = max(math.hypot(x - px, y - py) for x, y in part.px) + 2
    cx, cy = px + offset[0], py + offset[1]
    for fy in range(int(cy - reach), int(cy + reach) + 1):
        for fx in range(int(cx - reach), int(cx + reach) + 1):
            # Undo the move, then the rotation, to find the source pixel.
            dx, dy = fx + 0.5 - cx, fy + 0.5 - cy
            sx = px + dx * cos - dy * sin
            sy = py + dx * sin + dy * cos
            p = part.get(math.floor(sx), math.floor(sy))
            if p:
                out.put(fx + PAD_X, fy + PAD_TOP, p)
    return out


def draw_bone(g, center, angle, length=7):
    """A small white bone (two knobs and a shaft) drawn at center, in frame coordinates."""
    a = math.radians(angle)
    ux, uy = math.cos(a), math.sin(a)
    half = (length - 1) / 2
    cells = {}
    for i in range(-int(half * 2), int(half * 2) + 1):
        t = i / 2
        x, y = center[0] + ux * t, center[1] + uy * t
        cells[round(x - 0.5), round(y - 0.5)] = BONE_LIGHT if abs(t) < half - 0.5 else BONE_MID
    for end in (-1, 1):
        ex, ey = center[0] + ux * half * end, center[1] + uy * half * end
        for ox, oy in ((-uy, ux), (uy, -ux)):
            cells[round(ex + ox * 0.9 - 0.5), round(ey + oy * 0.9 - 0.5)] = BONE_LIGHT
    for (x, y), c in cells.items():
        g.put(x, y, c + (255,))
    for (x, y) in list(cells):
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if (nx, ny) not in cells and not (g.get(nx, ny) and not is_outline(g.get(nx, ny))):
                g.put(nx, ny, OUTLINE)


def pose(parts, free_arm=0.0, club=0.0, lean=0, skull=(0, 0), skull_tilt=0.0, legs=(0.0, 0.0),
         lift=(0.0, 0.0), bob=0, knees=0, bone_in_hand=False, flash=False):
    """Builds one frame. legs = swing of each leg in degrees (+ forward), lift = how far each foot
    rises, bob = how far the upper body rises, knees = how far the upper body sinks."""
    frame = Grid(FRAME_W, FRAME_H)
    up = -bob + knees
    layers = [
        transform_part(parts["left_leg"], LEFT_HIP, legs[0], (0, -lift[0])),
        transform_part(parts["right_leg"], RIGHT_HIP, legs[1], (0, -lift[1])),
        transform_part(parts["body"], (13, 37), -lean * 3, (0, up)),
        transform_part(parts["skull"], NECK, skull_tilt, (skull[0] + lean, skull[1] + up)),
        transform_part(parts["club_arm"], CLUB_SHOULDER, club, (lean * 0.5, up)),
        transform_part(parts["free_arm"], FREE_SHOULDER, free_arm, (lean * 0.5, up)),
    ]
    for layer in layers:
        frame.px.update(layer.px)
    if bone_in_hand:
        hx, hy = hand_at(free_arm)
        draw_bone(frame, (hx + PAD_X + lean * 0.5, hy + PAD_TOP + up), free_arm + 90)
    if flash:
        for xy, p in frame.px.items():
            if not is_outline(p):
                frame.px[xy] = FLASH
    return frame


# ---------------------------------------------------------------- skeleton animations

def walk(parts, count=8):
    """A stiff, rattling march: legs scissor, arms swing against them, the skull lags the body's
    bounce by a frame so it clatters on its neck."""
    frames = []
    for i in range(count):
        ph = i / count
        s = math.sin(2 * math.pi * ph)
        lift_l = max(0.0, math.sin(2 * math.pi * ph)) * 2
        lift_r = max(0.0, -math.sin(2 * math.pi * ph)) * 2
        bob = round(abs(math.cos(2 * math.pi * ph)))
        lag = round(abs(math.cos(2 * math.pi * (ph - 0.15))))
        frames.append(pose(parts, free_arm=-14 * s, club=10 * s, legs=(18 * s, -18 * s),
                           lift=(lift_l, lift_r), bob=bob, skull=(0, bob - lag),
                           skull_tilt=3 * math.sin(2 * math.pi * (ph - 0.15))))
    return frames


def throw(parts):
    return [pose(parts, free_arm=p["arm"], club=p["club"], lean=p["lean"], knees=p["knees"],
                 bone_in_hand=p["bone"], skull_tilt=-4 * p["lean"]) for p in THROW_POSES]


def hurt(parts):
    """Struck: a white flash as the bones jolt back, then a rattling recoil."""
    return [
        pose(parts, free_arm=-35, club=-25, lean=-2, skull=(-2, -2), skull_tilt=-14, flash=True),
        pose(parts, free_arm=-30, club=-22, lean=-2, skull=(-2, -1), skull_tilt=-12, knees=1),
        pose(parts, free_arm=-12, club=-10, lean=-1, skull=(-1, 0), skull_tilt=-5, knees=1),
        pose(parts, free_arm=-4, club=-3, lean=0, skull=(0, 0), skull_tilt=2),
    ]


def death(parts, count=8):
    """The skeleton comes apart: the skull drops and rolls back, the ribs fold down onto the
    legs, the arms clatter to either side, and everything settles into a heap."""
    frames = []
    ground = SRC_H - 1
    for i in range(count):
        t = i / (count - 1)
        fall = smooth(t)
        frame = Grid(FRAME_W, FRAME_H)

        def drop(part, pivot, angle, dx, rest_bottom, amount=fall, lift=0.0):
            # Turn the part, then let it fall until its lowest pixel reaches the ground.
            turned = transform_part(parts[part], pivot, angle * amount, (dx * amount, 0))
            bottom = max(y for _, y in turned.px)
            target = (ground if rest_bottom is None else rest_bottom) + PAD_TOP
            return shifted(turned, round((target - bottom) * amount - lift))

        layers = [
            drop("left_leg", LEFT_HIP, -70, -3, None),
            drop("right_leg", RIGHT_HIP, 70, 3, None),
            drop("body", (13, 37), -85, -3, None),
            drop("club_arm", CLUB_SHOULDER, 80, 5, None),
            drop("free_arm", FREE_SHOULDER, -80, -5, None),
        ]
        # The skull falls a little behind the rest, bounces off the heap, then rolls forward.
        st = smooth(max(0.0, (t - 0.15) / 0.85))
        bounce = 3 * math.sin((st - 0.7) / 0.3 * math.pi) if st > 0.7 else 0.0
        layers.append(drop("skull", NECK, 110, 9, None, amount=st, lift=bounce))
        for layer in layers:
            frame.px.update(layer.px)
        frames.append(frame)
    return frames


def shifted(g, dy):
    out = Grid(g.w, g.h)
    for (x, y), p in g.px.items():
        out.put(x, y + dy, p)
    return out


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def strip(frames, w, h):
    im = Image.new("RGBA", (w * len(frames), h))
    for i, f in enumerate(frames):
        im.alpha_composite(f.image(), (i * w, 0))
    return im


# ---------------------------------------------------------------- effects

def orb_frames(core, glow, rim, size=11, count=4):
    """A glowing orb with a bright core that pulses and a crackling rim."""
    frames = []
    c = (size - 1) / 2
    for i in range(count):
        g = Grid(size, size)
        pulse = 0.4 * math.sin(2 * math.pi * i / count)
        for y in range(size):
            for x in range(size):
                d = math.hypot(x - c, y - c)
                ang = math.atan2(y - c, x - c)
                edge = 4.2 + 0.35 * math.sin(5 * ang + i * math.pi / 2) + pulse * 0.4
                if d <= 1.6 + pulse * 0.4:
                    g.put(x, y, (255, 255, 240, 255))
                elif d <= 2.8 + pulse * 0.3:
                    g.put(x, y, core + (255,))
                elif d <= edge - 0.8:
                    g.put(x, y, glow + (255,))
                elif d <= edge:
                    g.put(x, y, rim + (220,))
        frames.append(g)
    return frames


def burst_frames(core, glow, rim, size=21, count=5):
    """An expanding ring of magic that thins out and scatters."""
    frames = []
    c = (size - 1) / 2
    for i in range(count):
        g = Grid(size, size)
        t = (i + 1) / count
        radius = 2 + t * 8
        width = 2.6 * (1 - t) + 0.8
        for y in range(size):
            for x in range(size):
                d = math.hypot(x - c, y - c)
                ang = math.atan2(y - c, x - c)
                if i == 0 and d <= 3:
                    g.put(x, y, (255, 255, 240, 255))
                gap = math.sin(ang * 7 + i * 1.3) > 0.55 + 0.4 * (1 - t)
                if abs(d - radius) <= width / 2 and not (t > 0.4 and gap):
                    col = core if abs(d - radius) < width / 4 else glow
                    g.put(x, y, col + (255,))
                elif abs(d - radius) <= width / 2 + 1 and t < 0.8 and not gap:
                    g.put(x, y, rim + (200,))
        frames.append(g)
    return frames


def spark(core, glow):
    g = Grid(3, 3)
    g.put(1, 1, (255, 255, 240, 255))
    for x, y in ((0, 1), (2, 1), (1, 0), (1, 2)):
        g.put(x, y, glow + (255,))
    return g


def thrown_bone():
    g = Grid(11, 7)
    draw_bone(g, (5.5, 3.5), 0, length=8)
    return g


def bone_chips(count=4):
    """Little shards of bone knocked off by a hit."""
    shapes = [[(0, 0), (1, 0)], [(0, 0), (0, 1), (1, 1)], [(0, 0)], [(0, 0), (1, 0), (2, 1)]]
    frames = []
    for i in range(count):
        g = Grid(5, 5)
        for x, y in shapes[i]:
            g.put(x + 1, y + 1, (BONE_LIGHT if (x + y) % 2 == 0 else BONE_MID) + (255,))
        add_outline(g)
        frames.append(g)
    return frames


# Orb palettes: the green staff's emerald orb and the skull wand's cursed violet-green soul orb.
STAFF_ORB = dict(core=(208, 255, 168), glow=(110, 220, 90), rim=(44, 120, 50))
SKULL_ORB = dict(core=(236, 210, 255), glow=(160, 92, 230), rim=(70, 190, 110))


def main():
    SKELETON_OUT.mkdir(parents=True, exist_ok=True)
    EFFECTS_OUT.mkdir(parents=True, exist_ok=True)
    parts = split(Grid.load(SPRITES / "Skeleton.png"))
    for name, frames in [("Idle", [pose(parts)]), ("Walk", walk(parts)), ("Throw", throw(parts)),
                         ("Hurt", hurt(parts)), ("Death", death(parts))]:
        strip(frames, FRAME_W, FRAME_H).save(SKELETON_OUT / f"Skeleton{name}.png")

    for name, pal in [("Staff", STAFF_ORB), ("Skull", SKULL_ORB)]:
        strip(orb_frames(**pal), 11, 11).save(EFFECTS_OUT / f"Orb{name}.png")
        strip(burst_frames(**pal), 21, 21).save(EFFECTS_OUT / f"OrbBurst{name}.png")
        spark(pal["core"], pal["glow"]).image().save(EFFECTS_OUT / f"Spark{name}.png")
    thrown_bone().image().save(EFFECTS_OUT / "ThrownBone.png")
    strip(bone_chips(), 5, 5).save(EFFECTS_OUT / "BoneChips.png")
    print(f"Throw release hand (frame px): {THROW_RELEASE_HAND}")


if __name__ == "__main__":
    main()
