# The Demon King's mark: the first-person arms' skin, reddened, with a pentagram etched into the back of the hand
# (both hands share the one texture). EndingSequence swaps it in under the red flare.
#   py -3.9 Tools/ending/make_marked_arms.py
import math
from PIL import Image, ImageDraw, ImageFilter, ImageChops

SRC = 'Assets/psx-first-person-arms/source/arms_01.png'
OUT = 'Assets/Generated/Ending/arms_marked.png'
CX, CY, R = 153, 213, 50          # the back of the hand in the 512 px sheet

skin = Image.open(SRC).convert('RGBA')
W, H = skin.size
k = W / 512.0
cx, cy, r = CX * k, CY * k, R * k

def sigil(width):
    im = Image.new('L', (W, H), 0)
    d = ImageDraw.Draw(im)
    w = max(1, round(width * k))
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=255, width=w)
    d.ellipse((cx - r * 0.86, cy - r * 0.86, cx + r * 0.86, cy + r * 0.86), outline=255, width=max(1, w // 2))
    pts = [(cx + r * 0.86 * math.sin(math.radians(a)), cy + r * 0.86 * math.cos(math.radians(a))) for a in range(0, 360, 72)]   # point toward the wrist
    for i in range(5):
        d.line((pts[i], pts[(i + 2) % 5]), fill=255, width=w)
    # cracks running off it, down the wrist and up between the knuckles
    for a, n in ((0, 1.9), (150, 1.5), (210, 1.5), (72, 1.35), (288, 1.35)):
        x0, y0 = cx + r * math.sin(math.radians(a)), cy + r * math.cos(math.radians(a))
        x1, y1 = cx + r * n * math.sin(math.radians(a + 7)), cy + r * n * math.cos(math.radians(a + 7))
        d.line((x0, y0, (x0 + x1) / 2 + 4 * k, (y0 + y1) / 2, x1, y1), fill=255, width=max(1, w // 2))
    return im

rgb = skin.convert('RGB')
# the whole arm flushed red, most of all round the mark
flush = Image.new('RGB', (W, H), (150, 28, 22))
heat = Image.new('L', (W, H), 70)
ImageDraw.Draw(heat).ellipse((cx - r * 2.2, cy - r * 2.2, cx + r * 2.2, cy + r * 2.2), fill=150)
heat = heat.filter(ImageFilter.GaussianBlur(40 * k))
rgb = Image.composite(ImageChops.multiply(rgb, Image.new('RGB', (W, H), (255, 150, 140))), rgb, heat)
rgb = Image.composite(flush, rgb, heat.point(lambda v: v // 4))
# the etching: a raw red burn, with the cut itself black down the middle
burn = sigil(9).filter(ImageFilter.GaussianBlur(3 * k))
rgb = Image.composite(Image.new('RGB', (W, H), (200, 16, 8)), rgb, burn.point(lambda v: min(255, v * 2)))
rgb = Image.composite(Image.new('RGB', (W, H), (14, 2, 2)), rgb, sigil(4))

out = rgb.convert('RGBA')
out.putalpha(skin.getchannel('A'))
import os
os.makedirs(os.path.dirname(OUT), exist_ok=True)
out.save(OUT)
print(OUT, out.size)
