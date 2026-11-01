"""正门钥匙上挂的纸签（老大 2026-11-01）：旧米黄卡纸、左边打孔加铜扣眼，正面 "MAIN DOOR"，
下面一行小字 "No. 07"（坏结局"门上的编号和钥匙上的一样"，GDD §6.9）。字体用项目里的 Cinzel（OFL）。
输出 Assets/Art/Textures/Props/KeyTag_MainDoor.png（1024×512，签子 2:1）。

用法：python3 Tools/gen_key_tag.py
"""
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONT = ROOT / "Assets/Art/Fonts/Cinzel-Regular.ttf"
OUT = ROOT / "Assets/Art/Textures/Props/KeyTag_MainDoor.png"
W, H = 1024, 512


def main():
    rnd = random.Random(7)
    paper = Image.new("RGB", (W, H), (226, 211, 176))
    # 纸纹：低频斑驳 + 高频颗粒
    blot = Image.effect_noise((W // 16, H // 16), 40).convert("L").resize((W, H), Image.BICUBIC)
    grain = Image.effect_noise((W, H), 18).convert("L")
    tone = Image.merge("RGB", [blot.point(lambda v: v)] * 3)
    paper = Image.blend(paper, Image.composite(paper, Image.new("RGB", (W, H), (196, 172, 128)), blot), 0.6)
    paper = Image.blend(paper, Image.merge("RGB", [grain] * 3), 0.06)

    d = ImageDraw.Draw(paper)
    # 旧水渍
    for _ in range(5):
        x, y, r = rnd.randrange(W), rnd.randrange(H), rnd.randint(40, 110)
        d.ellipse((x - r, y - r, x + r, y + r), outline=(200, 176, 132), width=2)
    paper = paper.filter(ImageFilter.GaussianBlur(2.2))
    d = ImageDraw.Draw(paper)

    # 边框：一条红褐细线
    m = 26
    d.rectangle((m, m, W - m, H - m), outline=(128, 52, 38), width=6)
    d.rectangle((m + 14, m + 14, W - m - 14, H - m - 14), outline=(128, 52, 38), width=2)

    # 左边打孔 + 铜扣眼
    cx, cy = 110, H // 2
    d.ellipse((cx - 46, cy - 46, cx + 46, cy + 46), fill=(150, 112, 52), outline=(96, 68, 28), width=6)
    d.ellipse((cx - 24, cy - 24, cx + 24, cy + 24), fill=(20, 16, 12))

    ink = (34, 28, 40)
    big = ImageFont.truetype(str(FONT), 108)
    small = ImageFont.truetype(str(FONT), 60)
    text = "MAIN DOOR"
    tw = d.textlength(text, font=big)
    x0 = 190 + (W - 190 - 50 - tw) / 2
    d.text((x0, 165), text, font=big, fill=ink)
    sub = "No. 07"
    sw = d.textlength(sub, font=small)
    d.text((190 + (W - 190 - 50 - sw) / 2, 318), sub, font=small, fill=(88, 40, 32))
    d.line((x0, 300, x0 + tw, 300), fill=ink, width=3)

    paper.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
