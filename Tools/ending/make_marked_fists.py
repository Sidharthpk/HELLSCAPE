# The Demon King's mark: the first-person arms' skin (Fists 2025), reddened, with the Sign of Judgement etched into the
# palm (both hands share the one texture). EndingSequence swaps it in under the red flare; the Inspect clip shows it.
# The sign is the user's own drawing: Tools/ending/Judgement.png (black on white/transparent).
#   py -3.9 Tools/ending/make_marked_fists.py
import math
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageOps

SRC = 'Assets/Fists 2025 [First Person Animations] by 1Matzh/textures/arms/FBX/body_C.tga'
SIGN = 'Tools/ending/Judgement.png'
OUT = 'Assets/Generated/Ending/arms_marked.png'
CX, CY, R = 298, 149, 12          # the middle of the palm, in 512 px sheet units
TURN = 90                         # turned so it reads upright with the fingers up (the palm's fingers point left on the sheet)

skin = Image.open(SRC).convert('RGBA')
W, H = skin.size
k = W / 512.0
cx, cy, r = CX * k, CY * k, R * k
ks = k * R / 50.0                 # stroke widths go with the size of the sign (they were tuned at R = 50)

# the drawing as a mask: strokes 255, everything else 0, cropped to the strokes
raw = Image.open(SIGN).convert('RGBA')
flat = Image.new('RGBA', raw.size, (255, 255, 255, 255))
flat.alpha_composite(raw)
base = ImageOps.invert(flat.convert('L')).point(lambda v: 255 if v > 90 else 0)
base = base.crop(base.getbbox())
height = int(2.15 * r)                                   # a little taller than the old pentagram was wide
base = base.resize((max(1, int(base.width * height / base.height)), height), Image.LANCZOS)

def sigil(width):
    im = Image.new('L', (W, H), 0)
    im.paste(base, (int(cx - base.width / 2), int(cy - base.height / 2)))
    # thicker for the burn, thinner for the black cut down the middle of it
    grow = max(0, round((width - 5) * ks / 2))
    if grow > 0:
        im = im.filter(ImageFilter.MaxFilter(2 * grow + 1))
    elif width < 5:
        im = im.filter(ImageFilter.MinFilter(3))
    d = ImageDraw.Draw(im)
    # cracks running off it from the ends of the strokes: down the wrist from the foot, up between the knuckles from the cup
    top, bottom = cy - base.height / 2, cy + base.height / 2
    left, right = cx - base.width / 2, cx + base.width / 2
    w = max(1, round(width * ks / 2))
    for (x0, y0, x1, y1, bend) in ((cx, bottom, cx + 6 * ks, bottom + r * 0.95, 5), (left + 3 * ks, top, left - 4 * ks, top - r * 0.55, -4), (right - 3 * ks, top, right + 5 * ks, top - r * 0.5, 4)):
        d.line((x0, y0, (x0 + x1) / 2 + bend * ks, (y0 + y1) / 2, x1, y1), fill=255, width=w)
    return im.rotate(TURN, center=(cx, cy))

rgb = skin.convert('RGB')
# the whole arm flushed red, most of all round the mark
flush = Image.new('RGB', (W, H), (150, 28, 22))
heat = Image.new('L', (W, H), 70)
ImageDraw.Draw(heat).ellipse((cx - r * 2.2, cy - r * 2.2, cx + r * 2.2, cy + r * 2.2), fill=150)
heat = heat.filter(ImageFilter.GaussianBlur(40 * k))
rgb = Image.composite(ImageChops.multiply(rgb, Image.new('RGB', (W, H), (255, 150, 140))), rgb, heat)
rgb = Image.composite(flush, rgb, heat.point(lambda v: v // 4))
# the etching: a raw red burn, with the cut itself black down the middle
burn = sigil(9).filter(ImageFilter.GaussianBlur(3 * ks))
rgb = Image.composite(Image.new('RGB', (W, H), (200, 16, 8)), rgb, burn.point(lambda v: min(255, v * 2)))
rgb = Image.composite(Image.new('RGB', (W, H), (14, 2, 2)), rgb, sigil(4))

out = rgb.convert('RGBA')
out.putalpha(skin.getchannel('A'))
import os
os.makedirs(os.path.dirname(OUT), exist_ok=True)
out.save(OUT)
print(OUT, out.size)
