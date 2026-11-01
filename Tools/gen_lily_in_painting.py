"""Lily 出现在别的画框里（布展变化 b1：《蒙娜丽莎》换成 Lily，GDD"她在每一幅画里"）。
把 Lily 肖像的三层（画布、眼白底图、两颗虹膜，Tools/gen_lily_portrait.py 生成）压成一张不透明贴图，
虹膜放在正视位置。输出 Assets/Art/Textures/Paintings/LilyPortrait_Flat.png。

用法：python3 Tools/gen_lily_in_painting.py
"""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
DIR = ROOT / "Assets/Art/Textures/Paintings"

# 与 GrayboxBuilder.EndingArt.cs 同一组数（540×720 坐标系）
IRIS = {"L": ((255.0, 196.0), 27.0), "R": ((343.0, 214.5), 25.0)}


def main():
    canvas = Image.open(DIR / "LilyPortrait_Canvas.png").convert("RGBA")
    back = Image.open(DIR / "LilyPortrait_EyeBack.png").convert("RGBA").resize(canvas.size, Image.LANCZOS)
    scale = canvas.size[0] / 540.0
    out = back.copy()
    for side, ((cx, cy), d) in IRIS.items():
        iris = Image.open(DIR / f"LilyPortrait_Iris_{side}.png").convert("RGBA")
        size = max(1, round(d * scale))
        iris = iris.resize((size, size), Image.LANCZOS)
        out.alpha_composite(iris, (round(cx * scale - size / 2), round(cy * scale - size / 2)))
    out.alpha_composite(canvas)
    out.convert("RGB").save(DIR / "LilyPortrait_Flat.png")
    print("wrote", DIR / "LilyPortrait_Flat.png", out.size)


if __name__ == "__main__":
    main()
