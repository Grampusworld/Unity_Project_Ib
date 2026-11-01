"""《美术馆》（The Gallery）画面：把编辑器里拍的画中馆二楼长廊（开馆、人群在、门洞里 Grampus 站在全家福前）
做成油画质感。输入 Assets/Art/Textures/Paintings/Source/TheGallery_Render.png（相机 (22, 11.2, 20.5)，yaw 95°、
pitch 4°、FOV 50，1600×1152 = 北墙画框 3.5×2.52 的比例），输出 Assets/Art/Textures/Paintings/TheGallery.png。
画里没有 Ted —— "观众席少了一个小人"（GDD §3.2 第五段）。

用法：python3 Tools/gen_the_gallery.py
"""
import random
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets/Art/Textures/Paintings/Source/TheGallery_Render.png"
DST = ROOT / "Assets/Art/Textures/Paintings/TheGallery.png"


def brush(img: Image.Image) -> Image.Image:
    # 色块化：众数滤波把平面压成笔触块，再轻微模糊抹开边缘（像没干的油彩被抹过）
    out = img.filter(ImageFilter.ModeFilter(7)).filter(ImageFilter.ModeFilter(5))
    out = out.filter(ImageFilter.SMOOTH_MORE)
    # 短笔触：随机方向的小块位移
    rnd = random.Random(11)
    w, h = out.size
    strokes = out.copy()
    for _ in range(9000):
        x, y = rnd.randrange(w), rnd.randrange(h)
        bw, bh = rnd.randint(6, 18), rnd.randint(2, 5)
        dx, dy = rnd.randint(-4, 4), rnd.randint(-3, 3)
        box = (x, y, min(w, x + bw), min(h, y + bh))
        patch = out.crop(box)
        strokes.paste(patch, (min(w - 1, max(0, x + dx)), min(h - 1, max(0, y + dy))))
    return Image.blend(out, strokes, 0.55)


def canvas_grain(size) -> Image.Image:
    w, h = size
    g = Image.effect_noise((w, h), 22).convert("L")
    weave = Image.new("L", (w, h), 128)
    d = ImageDraw.Draw(weave)
    for x in range(0, w, 3):
        d.line([(x, 0), (x, h)], fill=120)
    for y in range(0, h, 3):
        d.line([(0, y), (w, y)], fill=136)
    return ImageChops.blend(g, weave, 0.5)


def main():
    img = Image.open(SRC).convert("RGB")
    img = brush(img)

    # 暖黄的旧清漆 + 稍提亮（实景偏暗）
    img = ImageEnhance.Brightness(img).enhance(1.25)
    img = ImageEnhance.Color(img).enhance(0.85)
    varnish = Image.new("RGB", img.size, (205, 170, 110))
    img = Image.blend(img, ImageChops.multiply(img, varnish), 0.35)

    # 画布纹理叠加（overlay 近似：soft light）
    grain = canvas_grain(img.size).convert("RGB")
    img = ImageChops.soft_light(img, grain)

    # 四角压暗
    w, h = img.size
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).ellipse((-w * 0.15, -h * 0.2, w * 1.15, h * 1.2), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(min(w, h) * 0.12))
    dark = ImageEnhance.Brightness(img).enhance(0.45)
    img = Image.composite(img, dark, mask)

    img.save(DST)
    print("wrote", DST, img.size)


if __name__ == "__main__":
    main()
