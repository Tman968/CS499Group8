"""Generates the wizard's animation frames and held weapons in
JumpCraftGame/Assets/Art/Sprites/Wizard from Wizard.png.

Wizard.png has the green staff baked in, so it is split into three layers: the body, the staff,
and the hand that grips it. The staff and hand become their own sprites so WizardAnimator can
swap and swing the weapon. Body frames are drawn by bending the source art (rows of the robe
are resampled with sideways shear, flare and a travelling ripple), then legs and boots are drawn
underneath so they show below the hem.

    python Tools/generate_wizard_frames.py

Each animation is a horizontal strip of FRAME_W x FRAME_H frames. The frame canvas is
Wizard.png padded by PAD_X on each side and PAD_TOP on top, so its bottom-center pivot lands
where Wizard.png's did. Output is deterministic: rerunning produces identical images.

GRIP and CAST_POSES are shared with WizardAnimator.cs; change both together.
"""
import math
from pathlib import Path

from PIL import Image

SPRITES = Path(__file__).resolve().parent.parent / "JumpCraftGame/Assets/Art/Sprites"
OUT = SPRITES / "Wizard"

SRC_W, SRC_H = 34, 50
PAD_X, PAD_TOP = 5, 4
FRAME_W, FRAME_H = SRC_W + 2 * PAD_X, SRC_H + PAD_TOP

OUTLINE = (13, 9, 17, 255)
STAFF_COLORS = {(66, 48, 32), (94, 70, 46), (42, 30, 21)}
HAND_COLORS = {(154, 140, 134), (104, 90, 92)}

# Robe and boot colors sampled from Wizard.png.
ROBE_LIGHT, ROBE_MID, ROBE_DARK, ROBE_DEEP = (80, 55, 98), (54, 36, 72), (33, 22, 46), (17, 12, 24)
BOOT_LIGHT, BOOT_MID, BOOT_DARK = (94, 72, 52), (66, 50, 38), (40, 30, 26)
# Trousers: a touch lighter than the robe's shadows so the legs read under the hem.
LEG_LIGHT, LEG_MID, LEG_DARK = (74, 56, 90), (50, 36, 66), (31, 22, 42)

# Where the hand grips the staff, in source pixels (pixel centers).
GRIP = (28.5, 26.5)
SHOULDER = (22.5, 20.5)

BELT_ROW = 32       # first robe row below the belt; everything above is the upper body
HEM_ROW = 46        # last row of robe cloth
ROBE_CENTER_X = 16.5
HIP_Y = 38
GROUND_Y = 47       # boot sole row
NEAR_HIP_X, FAR_HIP_X = 19.5, 14.5

# Cast (wand attack) poses: (hand x, hand y, weapon angle in degrees, CCW positive) in source
# pixels, plus the body bend for that frame. Mirrored in WizardAnimator.castPoses.
CAST_POSES = [
    dict(hand=(27.5, 27.5), angle=12, lean=-1, drag=0.5, flare=1.04, ripple=0.4, tip=(0, 1)),
    dict(hand=(29.5, 23.5), angle=-18, lean=0, drag=-0.5, flare=1.06, ripple=0.6, tip=(-1, 0)),
    dict(hand=(31.5, 20.5), angle=-44, lean=1, drag=-2.5, flare=1.12, ripple=1.0, tip=(-2, -1)),
    dict(hand=(32.5, 19.5), angle=-52, lean=1, drag=-3.0, flare=1.16, ripple=1.2, tip=(-2, -2)),
    dict(hand=(32.5, 19.5), angle=-50, lean=1, drag=-2.5, flare=1.14, ripple=1.2, tip=(-2, -1)),
    dict(hand=(32.5, 19.5), angle=-48, lean=1, drag=-2.0, flare=1.12, ripple=1.0, tip=(-1, -2)),
    dict(hand=(30.5, 23.5), angle=-22, lean=0, drag=-1.0, flare=1.06, ripple=0.6, tip=(-1, -1)),
    dict(hand=(29.5, 25.5), angle=-8, lean=0, drag=-0.5, flare=1.02, ripple=0.3, tip=(0, 0)),
]


# ---------------------------------------------------------------- image helpers

class Grid:
    """A sparse RGBA pixel grid addressed by (x, y)."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = {}

    @classmethod
    def load(cls, path):
        im = Image.open(path).convert("RGBA")
        g = cls(*im.size)
        for y in range(im.height):
            for x in range(im.width):
                p = im.getpixel((x, y))
                if p[3]:
                    g.px[x, y] = p
        return g

    def get(self, x, y):
        return self.px.get((x, y))

    def put(self, x, y, color):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = color

    def image(self):
        im = Image.new("RGBA", (self.w, self.h))
        for (x, y), p in self.px.items():
            im.putpixel((x, y), p)
        return im


def is_outline(p):
    return p is not None and sum(p[:3]) < 60


def add_outline(g, where=lambda x, y: True, skip=lambda x, y: False):
    """Puts an outline on empty pixels that touch a filled, non-outline pixel."""
    new = []
    for (x, y), p in g.px.items():
        if is_outline(p):
            continue
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if (nx, ny) not in g.px and where(nx, ny) and not skip(nx, ny):
                new.append((nx, ny))
    for x, y in new:
        g.put(x, y, OUTLINE)


def prune_outline(g):
    """Removes outline pixels left floating with no filled pixel around them."""
    for (x, y), p in list(g.px.items()):
        if not is_outline(p):
            continue
        near = [g.get(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
        if not any(q is not None and not is_outline(q) for q in near):
            del g.px[x, y]


def despeckle(g):
    """Drops lone pixels that resampling flicks off the edge of the robe."""
    for (x, y) in list(g.px):
        if sum((x + dx, y + dy) in g.px for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))) <= 1:
            del g.px[x, y]


def shade(color, k):
    return tuple(round(c * k) for c in color[:3]) + (255,)


# ---------------------------------------------------------------- splitting Wizard.png

def split(src):
    """Splits Wizard.png into body, staff and hand grids (all in source coordinates)."""
    body, staff, hand = Grid(SRC_W, SRC_H), Grid(SRC_W, SRC_H), Grid(SRC_W, SRC_H)
    for y in range(SRC_H):
        row = {x: src.get(x, y) for x in range(SRC_W) if src.get(x, y)}
        if y <= 12:
            cut = 24  # the staff's orb sits to the right of the hat
        else:
            hits = [x for x, p in row.items() if x >= 24 and p[:3] in STAFF_COLORS | HAND_COLORS]
            if not hits:
                cut = 26 if y >= 44 else SRC_W
            else:
                cut = min(hits)
                # The staff's own left outline, when there's a gap between it and the robe.
                if is_outline(src.get(cut - 1, y)) and src.get(cut - 2, y) is None:
                    cut -= 1
        for x, p in row.items():
            if x < cut:
                body.px[x, y] = p
            elif p[:3] in HAND_COLORS:
                hand.px[x, y] = p
            elif p[:3] in STAFF_COLORS or is_outline(p) or y <= 12:
                staff.px[x, y] = p

    prune_outline(body)
    add_outline(body, where=lambda x, y: x >= 24 and y >= 13)
    prune_outline(staff)
    add_outline(staff, where=lambda x, y: y >= 13)
    shaft_x = int(GRIP[0])
    add_outline(hand, skip=lambda x, y: x == shaft_x)
    return body, staff, hand


def crop_with_pivot(g, pivot):
    """Crops a grid to its contents. Returns the image and the pivot as a 0-1 fraction from the
    bottom-left, as Unity's sprite importer wants it."""
    xs = [x for x, _ in g.px]
    ys = [y for _, y in g.px]
    left, top, right, bottom = min(xs), min(ys), max(xs) + 1, max(ys) + 1
    im = g.image().crop((left, top, right, bottom))
    w, h = right - left, bottom - top
    return im, ((pivot[0] - left) / w, (bottom - pivot[1]) / h)


def held_skull_wand():
    """Shrinks the high-res SkullWand.png to the wizard's pixel scale. Returns a grid in source
    coordinates with its grip at GRIP, like the green staff."""
    src = Image.open(SPRITES / "SkullWand.png").convert("RGBA")
    height = 54
    width = max(1, round(src.width * height / src.height))
    small = src.resize((width, height), Image.BOX)
    g = Grid(SRC_W + 20, SRC_H + 20)
    # The skull's eyes sit about 6 rows below its crown; put the grip where the staff's was.
    top = round(GRIP[1]) - 27
    rows = {}
    for y in range(height):
        for x in range(width):
            r, gr, b, a = small.getpixel((x, y))
            if a >= 110:
                rows.setdefault(y, []).append(x)
    grip_row = round(GRIP[1]) - top
    shaft = rows[grip_row]
    left = int(GRIP[0]) - (shaft[0] + shaft[-1]) // 2
    for y in range(height):
        for x in range(width):
            r, gr, b, a = small.getpixel((x, y))
            if a >= 110:
                # Boost contrast a little: downsampling washes the skull out.
                p = tuple(min(255, round((c - 128) * 1.15 + 128 + 6)) for c in (r, gr, b))
                g.put(x + left, y + top, p + (255,))
    add_outline(g)
    return g


# ---------------------------------------------------------------- drawing frames

def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def render_body(body, p):
    """Bends the body by the parameters in p and returns a grid in frame coordinates.

    p keys: lean (px the hat top moves forward), drag (px the hem moves sideways), flare (hem
    width multiplier), lift (rows the hem rises), wave (hem up/down ripple amplitude), ripple
    (sideways ripple amplitude), phase (0-1 ripple phase), tip (hat tip dx, dy).
    """
    out = Grid(FRAME_W, FRAME_H)
    lean, drag, flare = p.get("lean", 0), p.get("drag", 0), p.get("flare", 1)
    lift, wave, ripple, phase = p.get("lift", 0), p.get("wave", 0), p.get("ripple", 0), p.get("phase", 0)
    tip_dx, tip_dy = p.get("tip", (0, 0))
    skirt_len = HEM_ROW - BELT_ROW + 1
    out_len = skirt_len - lift

    for fy in range(FRAME_H):
        y = fy - PAD_TOP
        for fx in range(FRAME_W):
            x = fx - PAD_X
            if y < BELT_ROW:
                sx = x - round(lean * (BELT_ROW - 1 - y) / (BELT_ROW - 1))
                sy = y
                # The floppy hat tip bends more the further out it is.
                k = smooth((14 - sx) / 6) if sy <= 7 else 0
                sx -= round(tip_dx * k)
                sy -= round(tip_dy * k)
            else:
                t = (y - BELT_ROW) / max(1, out_len - 1)
                # Hem ripples up and down across its width, more the lower it hangs.
                w = wave * t * t * math.sin(2 * math.pi * (phase + x / 15))
                sy = BELT_ROW + (y - BELT_ROW + w) * skirt_len / out_len
                sy = round(sy)
                if sy > HEM_ROW + 2:
                    continue
                tt = (sy - BELT_ROW) / skirt_len
                shift = drag * tt ** 1.4 + ripple * tt * math.sin(2 * math.pi * (phase - tt * 0.9))
                scale = 1 + (flare - 1) * tt ** 1.3
                sx = round(ROBE_CENTER_X + (x - ROBE_CENTER_X - shift) / scale)
            c = body.get(sx, sy)
            if c:
                out.px[fx, fy] = c
    return out


def draw_capsule(g, a, b, r0, r1, colors, outline=True):
    """Draws a tapered limb from a to b (frame coordinates). colors = (top, middle, underside)."""
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    length2 = dx * dx + dy * dy or 1
    pad = int(max(r0, r1)) + 2
    cells = {}
    for y in range(int(min(ay, by)) - pad, int(max(ay, by)) + pad + 1):
        for x in range(int(min(ax, bx)) - pad, int(max(ax, bx)) + pad + 1):
            px, py = x + 0.5, y + 0.5
            u = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / length2))
            cx, cy = ax + dx * u, ay + dy * u
            d = math.hypot(px - cx, py - cy)
            r = r0 + (r1 - r0) * u
            if d <= r:
                # Which side of the limb the pixel is on decides its shade.
                side = (px - cx) * -dy + (py - cy) * dx
                side /= math.sqrt(length2)
                cells[x, y] = colors[0] if side < -r * 0.35 else colors[2] if side > r * 0.35 else colors[1]
            elif d <= r + 1 and outline:
                cells.setdefault((x, y), OUTLINE)
    for (x, y), c in cells.items():
        if c == OUTLINE and (x, y) in g.px:
            continue
        g.put(x, y, c if len(c) == 4 else c + (255,))


def draw_boot(g, ankle, dim=1.0, toe=0):
    """A small boot whose top-back corner sits at the ankle; toe tilts the toe down (+) or up (-)."""
    ax, ay = round(ankle[0] - 2), round(ankle[1])
    shape = [
        "0000..",
        "0aab0.",
        "0abbb0",
        "0cccc0" if toe <= 0 else ".0cccc0",
        ".0000." if toe <= 0 else "..0000",
    ]
    palette = {"0": OUTLINE, "a": shade(BOOT_LIGHT, dim), "b": shade(BOOT_MID, dim), "c": shade(BOOT_DARK, dim)}
    for j, row in enumerate(shape):
        for i, ch in enumerate(row):
            if ch != ".":
                existing = g.get(ax + i, ay + j)
                if ch == "0" and existing and not is_outline(existing):
                    continue
                g.put(ax + i, ay + j, palette[ch])


def leg(g, hip, ankle, dim):
    """Draws a leg from the hip to the ankle with the knee bent forward, then the boot."""
    hx, hy = hip
    ax, ay = ankle
    seg = 5.2
    d = math.hypot(ax - hx, ay - hy)
    mx, my = (hx + ax) / 2, (hy + ay) / 2
    bend = math.sqrt(max(0.0, seg * seg - (d / 2) ** 2))
    # Knee points forward (+x), perpendicular to the hip-ankle line.
    nx, ny = (ay - hy) / (d or 1), -(ax - hx) / (d or 1)
    knee = (mx + nx * bend, my + ny * bend)
    colors = (shade(LEG_LIGHT, dim), shade(LEG_MID, dim), shade(LEG_DARK, dim))
    draw_capsule(g, hip, knee, 1.4, 1.3, colors)
    draw_capsule(g, knee, (ax, ay - 1), 1.3, 1.2, colors)
    draw_boot(g, (ax, ay - 1), dim, toe=1 if ay < GROUND_Y - 1 else 0)


def compose(body, p, near_foot, far_foot, sleeve_to=None):
    """One frame: legs under the bent body, plus an outstretched sleeve for casting."""
    frame = Grid(FRAME_W, FRAME_H)
    to_frame = lambda pt: (pt[0] + PAD_X, pt[1] + PAD_TOP)
    leg(frame, to_frame((FAR_HIP_X, HIP_Y)), to_frame(far_foot), 0.72)
    leg(frame, to_frame((NEAR_HIP_X, HIP_Y)), to_frame(near_foot), 1.0)
    bent = render_body(body, p)
    despeckle(bent)
    frame.px.update(bent.px)
    if sleeve_to:
        hx, hy = sleeve_to
        sx, sy = SHOULDER
        d = math.hypot(hx - sx, hy - sy)
        # End the cuff just short of the hand so the fingers show.
        ex, ey = hx - (hx - sx) / d * 1.5, hy - (hy - sy) / d * 1.5
        draw_capsule(frame, to_frame((sx, sy)), to_frame((ex, ey)), 2.2, 2.9,
                     (ROBE_LIGHT, ROBE_MID, ROBE_DARK))
    return frame


def strip(frames):
    im = Image.new("RGBA", (FRAME_W * len(frames), FRAME_H))
    for i, f in enumerate(frames):
        im.alpha_composite(f.image(), (i * FRAME_W, 0))
    return im


# ---------------------------------------------------------------- animations

REST_NEAR = (21.0, GROUND_Y)
REST_FAR = (14.0, GROUND_Y)


def idle(body):
    return [compose(body, {}, REST_NEAR, REST_FAR)]


def run(body, count=8):
    """Eight-frame run: legs stride under the robe, which swings with each step and trails
    behind; the hat tip streams back."""
    frames = []
    stride, lift = 5.0, 3.5
    for i in range(count):
        ph = i / count

        def foot(phase, center):
            phase %= 1
            if phase < 0.5:  # planted, pushing back
                s = phase / 0.5
                return (center + stride * (1 - 2 * s), GROUND_Y)
            s = (phase - 0.5) / 0.5  # swinging forward, heel kicks up first
            x = center - stride + 2 * stride * smooth(s)
            return (x, GROUND_Y - lift * math.sin(math.pi * s ** 0.8))

        p = dict(lean=1, drag=-2.0 + 0.8 * math.sin(4 * math.pi * ph), flare=1.05,
                 lift=3, wave=1.0, ripple=0.8, phase=ph * 2, tip=(-1, round(math.sin(4 * math.pi * ph))))
        frames.append(compose(body, p, foot(ph, 18.5), foot(ph + 0.5, 16.0)))
    return frames


def rise(body, count=4):
    """Rising: the robe is pulled down and narrow, knees tucked, hat tip blown down."""
    frames = []
    for i in range(count):
        ph = i / count
        p = dict(flare=0.94, lift=-1, wave=0.6, ripple=0.6, phase=ph, tip=(0, 2))
        frames.append(compose(body, p, (21.0, GROUND_Y - 2), (14.5, GROUND_Y - 1)))
    return frames


def glide(body, count=8):
    """Falling: the robe billows out and up with a slow travelling ripple, the legs dangle
    below the hem and the hat tip floats."""
    frames = []
    for i in range(count):
        ph = i / count
        sway = math.sin(2 * math.pi * ph)
        p = dict(flare=1.24 + 0.04 * sway, lift=3, wave=1.4, ripple=1.1, phase=ph,
                 tip=(round(sway), -2 - round(0.6 + 0.6 * math.sin(2 * math.pi * (ph + 0.25)))))
        near = (20.0 + 0.8 * sway, GROUND_Y - 0.5)
        far = (14.5 - 0.8 * sway, GROUND_Y - 1.0)
        frames.append(compose(body, p, near, far))
    return frames


def cast(body):
    """Wand attack: brace, raise the arm, then thrust the weapon forward and hold it while the
    robe and hat are blown back."""
    frames = []
    for i, pose in enumerate(CAST_POSES):
        p = dict(lean=pose["lean"], drag=pose["drag"], flare=pose["flare"], lift=1, wave=0.8,
                 ripple=pose["ripple"], phase=i / 4, tip=pose["tip"])
        sleeve = pose["hand"] if i > 0 else None
        frames.append(compose(body, p, (22.5, GROUND_Y), (12.5, GROUND_Y), sleeve_to=sleeve))
    return frames


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    src = Grid.load(SPRITES / "Wizard.png")
    body, staff, hand = split(src)

    for name, frames in [("Idle", idle(body)), ("Run", run(body)), ("Rise", rise(body)),
                         ("Glide", glide(body)), ("Cast", cast(body))]:
        strip(frames).save(OUT / f"Wizard{name}.png")

    pivots = {}
    for name, g in [("WizardHand", hand), ("HeldGreenStaff", staff), ("HeldSkullWand", held_skull_wand())]:
        im, pivot = crop_with_pivot(g, GRIP)
        im.save(OUT / f"{name}.png")
        pivots[name] = pivot
    for name, (px, py) in pivots.items():
        print(f"{name}: pivot ({px:.6f}, {py:.6f})")


if __name__ == "__main__":
    main()
