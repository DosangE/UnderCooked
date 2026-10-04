import glob, sys
from PIL import Image, ImageDraw, ImageFont
src, out, W = sys.argv[1], sys.argv[2], int(sys.argv[3])
# Optional 4th arg: served count rec.cs reported at the end ("done frames N served S").
# The goal serve ends the episode, and the timer reset can land between two 0.1 s shots,
# so the last captured frame may still show target-1. The 2 s hold frame then shows S.
final_served = int(sys.argv[4]) if len(sys.argv) > 4 else None
meta = {}
for ln in open(src + "/meta.txt", encoding="utf-8"):
    p = ln.strip().split("|")
    if len(p) >= 6: meta[int(p[0])] = p
F = ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf", 17)
f = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 13)
GREEN, RED, BLUE = (60, 200, 90), (225, 65, 60), (45, 100, 245)
ING = {"GreenSoup": (GREEN, GREEN), "MixSoup": (GREEN, RED), "RedSoup": (RED, RED),
       "BlueSoup": (BLUE, BLUE), "GreenBlueSoup": (GREEN, BLUE), "RedBlueSoup": (RED, BLUE)}
BAND = 58
frames = []
files = sorted(glob.glob(src + "/[0-9]*.png"))
last_served, flash = 0, 0
for n, fp in enumerate(files):
    i = int(fp[-8:-4]); m = meta.get(i)
    if m and final_served is not None and n == len(files) - 1:
        m = m[:1] + [str(max(int(m[1]), final_served)), m[2]] + ["-"] * 3  # episode over: board is reset
    im = Image.open(fp).convert("RGB")
    im = im.resize((W, round(im.height * W / im.width)), Image.LANCZOS)
    c = Image.new("RGB", (W, im.height + BAND), (24, 24, 26)); c.paste(im, (0, BAND))
    d = ImageDraw.Draw(c)
    if m:
        served = int(m[1]); t = float(m[2])
        if served > last_served: flash = 8
        last_served = served
        col = (255, 214, 90) if flash > 0 else (240, 240, 240); flash -= 1
        d.text((14, 8), f"서빙 {served} / 3", font=F, fill=col)
        d.text((14, 33), f"{t:4.1f}초", font=f, fill=(170, 170, 170))
        d.text((150, 8), "주문", font=f, fill=(170, 170, 170))
        x = 150
        for s in m[3:6]:
            box = (x, 28, x + 140, 50)
            if s != "-":
                r, rem, dur = s.split(":"); rem, dur = float(rem), float(dur)
                a, b = ING.get(r, ((128,)*3, (128,)*3))
                d.rounded_rectangle((x, 28, x + 16, 44), 3, fill=a)
                d.rounded_rectangle((x + 19, 28, x + 35, 44), 3, fill=b)
                bx0, bx1 = x + 42, x + 130
                d.rounded_rectangle((bx0, 33, bx1, 39), 3, fill=(60, 60, 64))
                frac = max(0.0, min(1.0, rem / dur))
                bc = (120, 200, 255) if frac > 0.3 else (255, 150, 80)
                if frac > 0.02: d.rounded_rectangle((bx0, 33, bx0 + (bx1 - bx0) * frac, 39), 3, fill=bc)
            x += 150
        if served >= 3:
            d.text((W - 14, 18), "목표 달성", font=F, fill=(255, 214, 90), anchor="ra")
    frames.append(c)
pal = frames[len(frames) // 2].quantize(colors=160, method=Image.MEDIANCUT)
q = [fr.quantize(palette=pal, dither=Image.Dither.NONE) for fr in frames]
durs = [100] * (len(q) - 1) + [2000]
q[0].save(out, save_all=True, append_images=q[1:], duration=durs, loop=0, optimize=True)
frames[-1].save(src + "/../last.png"); frames[len(frames)//2].save(src + "/../mid.png")
print(len(q), "frames")
