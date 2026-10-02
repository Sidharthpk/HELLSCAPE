"""The driving HUD's pictures, written to Assets/UI/Modern/.   py -3.9 Tools/hud/make_driving_hud.py

VanIcon_0..4.png   the Kessler & Vane van in pixel art, side on: as new, scuffed, rusted and cracked, scorched
                   and smoking, burnt out. One per VanDamageLook stage (VanHealthUI swaps them).
Speedo_dial.png    an analogue speedometer face, 0-160 km/h over 270 degrees (0 at lower left).
Speedo_needle.png  its needle, pointing up, with the hub cap on its pivot (SpeedometerUI turns it).
"""
import math
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "UI", "Modern")
FONT = os.path.join(ROOT, "Assets", "Fonts", "PerfectDOSVGA437.ttf")

# ---------------------------------------------------------------- the van

VAN = [
    "..........................",
    "....KKKKKKKKKKKKKKKK......",
    "...KYYYYYYYYYYYYYYYYK.....",
    "...KYYYYYYYYYYYYYKWWWK....",
    "...KYYYYYYYYYYYYYKWWWWK...",
    "...KYSSSSSSSSSSYYKWWWWWK..",
    "...KYYYYYYYYYYYYYKKKKKKKK.",
    "...KYYYYYYYYYYYYYYYYYYYYLK",
    "...KYYYYYYYYYYYYYYYYYYYYYK",
    "...KDDDDDDDDDDDDDDDDDDDDDK",
    "...KDDKKKKDDDDDDDDDKKKKDGK",
    "...KKKTTTTKKKKKKKKKTTTTKKK",
    ".....TTHHTT.......TTHHTT..",
    ".....TTHHTT.......TTHHTT..",
    "......TTTT.........TTTT...",
]
TOP = 6          # empty rows above the van, for the smoke
COLS = {
    "K": (30, 22, 12), "Y": (236, 196, 40), "D": (190, 148, 26), "S": (120, 88, 20), "W": (150, 190, 200),
    "L": (255, 244, 190), "G": (120, 122, 126), "T": (22, 22, 24), "H": (150, 152, 156),
}
SCRATCH, RUST, SOOT = (168, 166, 158), (126, 62, 28), (28, 22, 18)
SMOKE = [(86, 82, 80), (60, 57, 56), (44, 42, 42)]
FLAME = [(255, 140, 20), (255, 214, 70), (226, 70, 16)]


def cells(grid, kinds):
    return [(x, y) for y, row in enumerate(grid) for x, ch in enumerate(row) if ch in kinds]


def patch(rng, px, where, col, count, size):
    """count small clumps of col on the cells in 'where'."""
    ok = set(where)
    for _ in range(count):
        cx, cy = where[rng.integers(0, len(where))]
        for dx in range(size):
            for dy in range(size):
                if (cx + dx, cy + dy) in ok and rng.random() < 0.85:
                    px[(cx + dx, cy + dy)] = col


def van_icons():
    paint = cells(VAN, "YDS")
    glass = cells(VAN, "W")
    for stage in range(5):
        rng = np.random.default_rng(1304)          # the same marks every stage, plus the next lot
        px = {(x, y): COLS[ch] for y, row in enumerate(VAN) for x, ch in enumerate(row) if ch != "."}
        smoke = {}
        if stage >= 1:    # scuffed: bare metal showing, road dirt along the sills
            patch(rng, px, paint, SCRATCH, 5, 1)
            for x, y in cells(VAN, "D"):
                if rng.random() < 0.45:
                    px[(x, y)] = (112, 92, 40)
        if stage >= 2:    # beaten: rust, a cracked screen
            patch(rng, px, paint, RUST, 6, 2)
            for x, y in [(19, 3), (20, 4), (20, 5), (21, 5)]:
                px[(x, y)] = (232, 244, 244)
        if stage >= 3:    # scorched: soot, the lamp gone, smoke off the bonnet
            patch(rng, px, paint, SOOT, 7, 2)
            for x, y in cells(VAN, "L"):
                px[(x, y)] = (40, 34, 30)
            for x, y, c in [(21, -1, 0), (22, -2, 0), (21, -3, 1), (22, -4, 1), (23, -3, 1), (22, -5, 2)]:
                smoke[(x, y + 6)] = SMOKE[c]
        if stage >= 4:    # burnt out: black, glass gone, fire on the bonnet
            for x, y in paint:
                r = rng.random()
                px[(x, y)] = SOOT if r < 0.62 else RUST if r < 0.8 else (52, 40, 30)
            for x, y in glass:
                px[(x, y)] = (12, 14, 14)
            px[(19, 3)] = px[(21, 5)] = (200, 214, 214)
            for x, y, c in [(20, 0, 0), (21, 0, 0), (23, 0, 0), (20, -1, 0), (22, -1, 0), (23, -2, 1), (20, -3, 1),
                            (21, -4, 1), (23, -4, 2), (20, -5, 2), (22, -6, 2)]:
                smoke[(x, y + 6)] = SMOKE[c]
            for x, y, c in [(19, 5, 0), (20, 5, 1), (21, 5, 0), (22, 5, 2), (20, 4, 0), (21, 4, 1), (21, 3, 2)]:
                smoke[(x, y)] = FLAME[c]

        w, h = len(VAN[0]) + 2, len(VAN) + TOP + 1
        im = np.zeros((h, w, 4), np.uint8)
        for (x, y), c in px.items():
            im[y + TOP, x + 1] = (*c, 255)
        for (x, y), c in smoke.items():
            if 0 <= y < h and 0 <= x + 1 < w:
                im[y, x + 1] = (*c, 255)
        path = os.path.join(OUT, f"VanIcon_{stage}.png")
        Image.fromarray(im).resize((w * 4, h * 4), Image.NEAREST).save(path)
        print("wrote", path, f"({w * 4}x{h * 4})")


# ---------------------------------------------------------------- the speedometer

SIZE, SS = 256, 4          # final pixels; drawn SS times larger and scaled down for clean edges
MAX_KMH, SWEEP, ZERO = 160, 270.0, 225.0   # 0 km/h at 225 degrees (lower left), clockwise from there
FACE, BEZEL, RIM = (10, 11, 14, 232), (44, 46, 52, 255), (255, 204, 92, 120)
TICK, MINOR, REDLINE = (236, 228, 206, 255), (150, 148, 140, 255), (226, 56, 44, 255)
RED_FROM = 130


def polar(r, kmh):
    a = math.radians(ZERO - SWEEP * kmh / MAX_KMH)
    c = SIZE * SS / 2
    return (c + math.cos(a) * r * SS, c - math.sin(a) * r * SS)


def dial():
    big = Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    c = SIZE * SS / 2

    def disc(r, col):
        d.ellipse([c - r * SS, c - r * SS, c + r * SS, c + r * SS], fill=col)

    disc(126, BEZEL)
    disc(121, FACE)
    d.ellipse([c - 119 * SS, c - 119 * SS, c + 119 * SS, c + 119 * SS], outline=RIM, width=SS)
    # the red end of the scale
    a0, a1 = ZERO - SWEEP * RED_FROM / MAX_KMH, ZERO - SWEEP
    d.arc([c - 113 * SS, c - 113 * SS, c + 113 * SS, c + 113 * SS], start=-a0, end=-a1, fill=REDLINE, width=3 * SS)
    for kmh in range(0, MAX_KMH + 1, 10):
        major = kmh % 20 == 0
        col = REDLINE if kmh >= RED_FROM else (TICK if major else MINOR)
        d.line([polar(96 if major else 103, kmh), polar(110, kmh)], fill=col, width=(4 if major else 2) * SS)
    face = big.resize((SIZE, SIZE), Image.LANCZOS)

    # numerals and the unit in the pixel font, drawn at final size with no smoothing so they stay crisp
    font = ImageFont.truetype(FONT, 16)
    d = ImageDraw.Draw(face)
    d.fontmode = "1"
    for kmh in range(0, MAX_KMH + 1, 20):
        x, y = polar(78, kmh)
        d.text((round(x / SS), round(y / SS)), str(kmh), font=font, anchor="mm",
               fill=REDLINE[:3] + (255,) if kmh >= RED_FROM else TICK)
    d.text((SIZE // 2, 190), "km/h", font=font, anchor="mm", fill=MINOR)
    path = os.path.join(OUT, "Speedo_dial.png")
    face.save(path)
    print("wrote", path)


def needle():
    w, h, pivot = 40, 152, 28            # pivot: pixels up from the bottom edge to the hub's centre
    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    cx, cy = w * SS / 2, (h - pivot) * SS
    tip = cy - 112 * SS
    d.polygon([(cx - 3.2 * SS, cy + 16 * SS), (cx + 3.2 * SS, cy + 16 * SS), (cx + 1.1 * SS, tip), (cx - 1.1 * SS, tip)],
              fill=(232, 62, 40, 255))
    d.ellipse([cx - 11 * SS, cy - 11 * SS, cx + 11 * SS, cy + 11 * SS], fill=(30, 32, 38, 255))
    d.ellipse([cx - 11 * SS, cy - 11 * SS, cx + 11 * SS, cy + 11 * SS], outline=(255, 204, 92, 200), width=int(1.5 * SS))
    d.ellipse([cx - 3 * SS, cy - 3 * SS, cx + 3 * SS, cy + 3 * SS], fill=(232, 62, 40, 255))
    path = os.path.join(OUT, "Speedo_needle.png")
    big.resize((w, h), Image.LANCZOS).save(path)
    print("wrote", path, f"pivot y = {pivot / h:.4f}")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    # (van_icons() is the old pixel-art van: the HUD now uses renders of the real van, made in Unity by
    #  HellScape > UI > Render HUD Icons. Calling it here would overwrite those.)
    dial()
    needle()
