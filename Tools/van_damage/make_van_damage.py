"""Damage states for the Kessler & Vane van.

Reads the van's paint job (Kesller.png) and writes four progressively wrecked copies next to it
(Kesller_damage1..4.png): scratches, dents, road dirt, rust, claw marks, cracked glass, smashed lamps, soot.
Each stage keeps everything from the one before and adds to it, so the van only ever gets worse.
Everything is drawn on a 4 px grid, like the texture's own pixel art.   python Tools/van_damage/make_van_damage.py

Also writes the HUD heart (three looks) to Assets/UI/Modern/.
"""
import os
import numpy as np
from PIL import Image

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "New Folder", "stylized-pixel-fiat-ducato-gen-i-jacex", "textures", "Kesller.png")
B = 4  # block size in texture pixels

# where things are on the sheet (texture pixels: x0, y0, x1, y1)
ROOF = (50, 20, 600, 290)
LEFT = (20, 300, 770, 590)
RIGHT = (10, 590, 760, 890)
FRONT = (770, 165, 1080, 575)
BACK = (780, 595, 1080, 880)
WINDSCREEN = (780, 185, 1070, 300)
SIDE_WINDOWS = [(490, 330, 620, 415), (155, 635, 285, 715)]
HEADLAMPS = [(790, 475, 835, 520), (1015, 475, 1060, 520), (790, 100, 835, 145), (1020, 100, 1065, 145)]
TAIL_LAMPS = [(690, 80, 730, 125), (732, 80, 770, 125)]
SIDES = [LEFT, RIGHT, BACK]
PANELS = [ROOF, LEFT, RIGHT, FRONT, BACK]


def g(box):
    return tuple(int(round(v / B)) for v in box)


class Sheet:
    def __init__(self, base):
        self.base = base.astype(np.float32)
        h, w, _ = base.shape
        self.gh, self.gw = (h + B - 1) // B, (w + B - 1) // B
        pad = np.zeros((self.gh * B, self.gw * B, 3), np.float32)
        pad[:h, :w] = self.base
        cells = pad.reshape(self.gh, B, self.gw, B, 3).mean(axis=(1, 3))
        r, gr, b = cells[..., 0], cells[..., 1], cells[..., 2]
        self.paint = (r > 150) & (gr > 115) & (b < 175) & (r - b > 55)          # the yellow bodywork
        self.body = (r + gr + b) > 60                                           # anything that isn't the black backdrop
        self.rgb = np.zeros((self.gh, self.gw, 3), np.float32)
        self.a = np.zeros((self.gh, self.gw), np.float32)

    def put(self, x, y, col, alpha, mask=None):
        x, y = int(x), int(y)
        if x < 0 or y < 0 or x >= self.gw or y >= self.gh:
            return
        if mask is not None and not mask[y, x]:
            return
        a0 = self.a[y, x]
        out = alpha + a0 * (1 - alpha)
        if out <= 0:
            return
        self.rgb[y, x] = (np.array(col, np.float32) * alpha + self.rgb[y, x] * a0 * (1 - alpha)) / out
        self.a[y, x] = out

    def render(self):
        rgb = np.kron(self.rgb, np.ones((B, B, 1), np.float32))[: self.base.shape[0], : self.base.shape[1]]
        a = np.kron(self.a, np.ones((B, B), np.float32))[: self.base.shape[0], : self.base.shape[1], None]
        return np.clip(self.base * (1 - a) + rgb * a, 0, 255).astype(np.uint8)


def pick(rng, box, mask, tries=60):
    x0, y0, x1, y1 = g(box)
    for _ in range(tries):
        x, y = rng.integers(x0, max(x0 + 1, x1)), rng.integers(y0, max(y0 + 1, y1))
        if 0 <= y < mask.shape[0] and 0 <= x < mask.shape[1] and mask[y, x]:
            return x, y
    return None


def scratch(s, rng, box):
    p = pick(rng, box, s.paint)
    if p is None:
        return
    x, y = p
    ang = rng.normal(0, 0.35)
    if rng.random() < 0.2:
        ang += 1.3
    length = rng.integers(6, 30)
    metal = rng.random() < 0.45
    col = (168, 166, 158) if metal else (72, 52, 30)
    for i in range(length):
        xx, yy = x + np.cos(ang) * i, y + np.sin(ang) * i
        s.put(xx, yy, col, 0.9, s.paint)
        if not metal:
            s.put(xx, yy + 1, (255, 244, 170), 0.35, s.paint)   # the lip of the gouge catching the light


def dent(s, rng, box):
    p = pick(rng, box, s.paint)
    if p is None:
        return
    x, y = p
    r = rng.integers(3, 8)
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            d = np.hypot(dx, dy)
            if d > r:
                continue
            if d > r - 1.2 and dx + dy < 0:
                s.put(x + dx, y + dy, (255, 250, 200), 0.4, s.paint)      # rim highlight, top-left
            else:
                s.put(x + dx, y + dy, (40, 26, 8), 0.34 * (1 - d / (r + 1)) + 0.1, s.paint)


def blob(s, rng, box, cells, cols, alpha, mask, edge=0.55):
    """A solid patch: discs laid along a short wander, soft at the rim."""
    p = pick(rng, box, mask)
    if p is None:
        return
    x, y = float(p[0]), float(p[1])
    r = max(1.5, np.sqrt(cells) * 0.42)
    seen = {}
    for _ in range(max(3, int(cells ** 0.5))):
        rr = r * rng.uniform(0.6, 1.15)
        for dy in range(int(-rr - 1), int(rr + 2)):
            for dx in range(int(-rr - 1), int(rr + 2)):
                d = np.hypot(dx, dy) + rng.uniform(-0.6, 0.6)   # a ragged edge
                if d <= rr:
                    k = (int(x) + dx, int(y) + dy)
                    seen[k] = max(seen.get(k, 0.0), 1.0 if d < rr - 1.3 else edge)
        ang = rng.uniform(0, 2 * np.pi)
        x += np.cos(ang) * rr * 0.9
        y += np.sin(ang) * rr * 0.9
    for (cx, cy), k in seen.items():
        s.put(cx, cy, cols[rng.integers(0, len(cols))], alpha * k, mask)


def dirt(s, rng, box, strength):
    x0, y0, x1, y1 = g(box)
    h = int((y1 - y0) * 0.45)
    for x in range(x0, x1):
        reach = h * (0.55 + 0.45 * rng.random())
        for dy in range(int(reach)):
            y = y1 - 1 - dy
            a = strength * (1 - dy / max(reach, 1)) * (0.6 + 0.4 * rng.random())
            s.put(x, y, (46, 36, 24), a, s.body)


def claws(s, rng, box):
    p = pick(rng, box, s.paint)
    if p is None:
        return
    x, y = p
    n, length = rng.integers(3, 5), rng.integers(12, 24)
    slope = rng.choice([-1, 1]) * rng.uniform(0.25, 0.6)
    for k in range(n):
        for i in range(length):
            xx, yy = x + i, int(round(y + k * 4 + i * slope))
            s.put(xx, yy, (132, 10, 14), 0.9, s.body)
            s.put(xx, yy + 1, (88, 4, 8), 0.85, s.body)
        if rng.random() < 0.85:                                         # a run of it, down the panel
            dx = rng.integers(2, length)
            for d in range(rng.integers(4, 14)):
                s.put(x + dx, int(round(y + k * 4 + dx * slope)) + 2 + d, (108, 8, 12), max(0.25, 0.85 - d * 0.05), s.body)


def crack(s, rng, box, rays, hole=False):
    x0, y0, x1, y1 = g(box)
    cx, cy = rng.integers(x0 + 3, max(x0 + 4, x1 - 3)), rng.integers(y0 + 3, max(y0 + 4, y1 - 3))
    inbox = np.zeros_like(s.body)
    inbox[y0:y1, x0:x1] = True
    for _ in range(rays):
        ang = rng.uniform(0, 2 * np.pi)
        for i in range(rng.integers(6, 30)):
            ang += rng.normal(0, 0.12)
            s.put(cx + np.cos(ang) * i, cy + np.sin(ang) * i, (212, 230, 228), 0.82, inbox)
    for r in (3, 6):                                                    # the spider's web round the hit
        for a in np.linspace(0, 2 * np.pi, 9 * r, endpoint=False):
            if rng.random() < 0.6:
                s.put(cx + np.cos(a) * r, cy + np.sin(a) * r, (190, 212, 210), 0.6, inbox)
    if hole:
        blob(s, rng, (cx * B - 20, cy * B - 20, cx * B + 20, cy * B + 20), 60, [(8, 10, 10), (16, 18, 18)], 0.97, inbox)


def smash(s, rng, box):
    x0, y0, x1, y1 = g(box)
    for y in range(y0, y1):
        for x in range(x0, x1):
            s.put(x, y, (26, 22, 20) if rng.random() < 0.85 else (150, 148, 138), 0.92, s.body)


def tint(s, strength):
    """The whole paint job dulled: smoke-stained, the shine gone."""
    ys, xs = np.nonzero(s.paint)
    for x, y in zip(xs, ys):
        s.put(x, y, (58, 44, 26), strength, s.paint)


def stage(s, n):
    rng = np.random.default_rng(1304 + n)
    soot = [(20, 16, 14), (30, 24, 20), (14, 12, 12)]
    rust = [(126, 62, 28), (96, 44, 22), (150, 84, 36), (70, 34, 18)]
    if n == 1:   # a rough night: scuffs, a couple of knocks, road dirt, the first hands on it
        for _ in range(18): scratch(s, rng, PANELS[rng.integers(0, 5)])
        for _ in range(5): dent(s, rng, PANELS[rng.integers(1, 5)])
        for b in SIDES: dirt(s, rng, b, 0.4)
        claws(s, rng, LEFT); claws(s, rng, BACK)
    elif n == 2:  # beaten: rust showing, a cracked screen, a lamp gone
        for _ in range(22): scratch(s, rng, PANELS[rng.integers(0, 5)])
        for _ in range(7): dent(s, rng, PANELS[rng.integers(0, 5)])
        for _ in range(9): blob(s, rng, PANELS[rng.integers(1, 5)], rng.integers(30, 90), rust, 0.95, s.paint)
        for b in SIDES + [FRONT]: dirt(s, rng, b, 0.3)
        for b in (RIGHT, BACK, FRONT, LEFT): claws(s, rng, b)
        tint(s, 0.08)
        crack(s, rng, WINDSCREEN, 7)
        smash(s, rng, HEADLAMPS[1]); smash(s, rng, HEADLAMPS[3])
    elif n == 3:  # barely holding together: scorched panels, both lamps, every window starred
        for _ in range(24): scratch(s, rng, PANELS[rng.integers(0, 5)])
        for _ in range(9): dent(s, rng, PANELS[rng.integers(0, 5)])
        for _ in range(11): blob(s, rng, PANELS[rng.integers(0, 5)], rng.integers(70, 200), rust, 0.95, s.paint)
        for b in (FRONT, LEFT, RIGHT, BACK): blob(s, rng, b, rng.integers(350, 700), soot, 0.84, s.body, 0.4)
        for b in (LEFT, RIGHT, BACK, ROOF, FRONT): claws(s, rng, b)
        tint(s, 0.12)
        crack(s, rng, WINDSCREEN, 10)
        for w in SIDE_WINDOWS: crack(s, rng, w, 6)
        smash(s, rng, HEADLAMPS[0]); smash(s, rng, HEADLAMPS[2]); smash(s, rng, TAIL_LAMPS[0])
    else:         # a wreck that still rolls: burnt black, glass out
        for _ in range(20): scratch(s, rng, PANELS[rng.integers(0, 5)])
        for _ in range(10): blob(s, rng, PANELS[rng.integers(0, 5)], rng.integers(120, 300), rust, 0.95, s.paint)
        for b in (ROOF, FRONT, LEFT, RIGHT, BACK, FRONT, LEFT, RIGHT, ROOF, BACK): blob(s, rng, b, rng.integers(700, 1500), soot, 0.9, s.body, 0.45)
        for b in SIDES + [FRONT]: dirt(s, rng, b, 0.45)
        for b in (LEFT, RIGHT, FRONT, BACK): claws(s, rng, b)
        tint(s, 0.2)
        crack(s, rng, WINDSCREEN, 9, hole=True)
        for w in SIDE_WINDOWS: crack(s, rng, w, 7, hole=True)
        smash(s, rng, TAIL_LAMPS[1])


def van():
    base = np.array(Image.open(SRC).convert("RGB"))
    s = Sheet(base)
    for n in range(1, 5):
        stage(s, n)
        out = os.path.join(os.path.dirname(SRC), f"Kesller_damage{n}.png")
        Image.fromarray(s.render()).save(out)
        print("wrote", out)


# ---------------------------------------------------------------- the HUD heart

HEART = [
    "..KKK...KKK..",
    ".KRRRK.KRRRK.",
    "KRWWRRKRRRRRK",
    "KRWRRRRRRRRDK",
    "KRRRRRRRRRRDK",
    "KRRRRRRRRRDDK",
    ".KRRRRRRRRDK.",
    "..KRRRRRRDK..",
    "...KRRRRDK...",
    "....KRRDK....",
    ".....KDK.....",
    "......K......",
]
LOOKS = {
    "healthy": dict(R=(214, 32, 40), D=(140, 14, 30), W=(255, 176, 176), cracks=[]),
    "hurt": dict(R=(178, 26, 34), D=(112, 10, 24), W=(232, 140, 140), cracks=[(6, 2), (6, 3), (7, 4), (6, 5), (7, 6)]),
    "critical": dict(R=(122, 20, 28), D=(76, 8, 18), W=(172, 100, 100),
                     cracks=[(6, 2), (6, 3), (7, 4), (6, 5), (7, 6), (6, 7), (5, 8), (8, 5), (9, 6), (4, 4), (3, 5), (5, 6)]),
}


def heart():
    out_dir = os.path.join(ROOT, "Assets", "UI", "Modern")
    os.makedirs(out_dir, exist_ok=True)
    K = (40, 6, 10)
    for name, look in LOOKS.items():
        im = np.zeros((16, 16, 4), np.uint8)
        for y, row in enumerate(HEART):
            for x, ch in enumerate(row):
                if ch == ".":
                    continue
                col = K if ch == "K" else look[ch]
                im[y + 2, x + 1] = (*col, 255)
        for (x, y) in look["cracks"]:
            im[y + 2, x + 1] = (*K, 255)
        path = os.path.join(out_dir, f"Heart_{name}.png")
        Image.fromarray(im).resize((64, 64), Image.NEAREST).save(path)
        print("wrote", path)


if __name__ == "__main__":
    van()
    # (heart() is the old pixel-art HUD heart: the HUD now uses renders of the anatomical heart model, made in
    #  Unity by HellScape > UI > Render HUD Icons. Calling it here would overwrite those.)
