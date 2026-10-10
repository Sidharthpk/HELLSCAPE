# The prologue TV: turns the two news GIFs in Assets/Resources into flipbook sheets (frame 0 top-left, rows down)
# that PrologueDirector.TvOn plays. Run with py -3.9 from the project root; it prints the numbers the code needs.
from PIL import Image, ImageSequence

def sheet(gif, out, size, step, cols):
    frames = [f.convert("RGB").resize(size, Image.LANCZOS) for i, f in enumerate(ImageSequence.Iterator(Image.open(gif))) if i % step == 0]
    rows = -(-len(frames) // cols)
    img = Image.new("RGB", (size[0] * cols, size[1] * rows))
    for i, f in enumerate(frames):
        img.paste(f, (i % cols * size[0], i // cols * size[1]))
    img.save(out)
    print(out, len(frames), "frames", cols, "x", rows, img.size)

sheet("Assets/Resources/breaking-news-news.gif", "Assets/Resources/TvBreaking.png", (220, 123), 1, 5)   # 10 fps
sheet("Assets/Resources/anchor.gif", "Assets/Resources/TvAnchor.png", (320, 180), 2, 10)              # every 2nd frame: 7.5 fps
