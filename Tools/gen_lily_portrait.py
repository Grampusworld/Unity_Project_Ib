"""Lily 肖像（二楼值班室门口 Sketch_LilyLooksAtDoor）：把老大给的 1280x720 横图以小女孩为中心裁成 3:4，
拆成三层给"眼睛跟着玩家"用（方案 A，只动眼珠）：
  LilyPortrait_Canvas.png   画面，两只眼睛的眼裂抠空（alpha=0），材质开 Alpha Clipping
  LilyPortrait_EyeBack.png  同尺寸，虹膜涂掉只剩眼白，贴在画布后面
  LilyPortrait_Iris_L/R.png 两只眼的虹膜（圆，带透明），夹在画布和眼白之间，由脚本平移
用法：python3 -I Tools/gen_lily_portrait.py <Sister.png> <输出目录>
"""
import sys
from PIL import Image, ImageDraw, ImageFilter

SRC, OUT = sys.argv[1], sys.argv[2]
CROP = (300, 0, 840, 720)          # 原图里的裁切框，540x720 = 3:4
SCALE = 2                          # 输出 1080x1440，抠洞边缘不至于锯齿
# 原图坐标：眼裂椭圆中心、半轴；虹膜中心、半径
EYES = {
    "L": dict(open_c=(555, 197), open_ab=(21, 10), iris_c=(555, 196), iris_r=11.5),
    "R": dict(open_c=(644, 215), open_ab=(20.5, 9.5), iris_c=(643, 214.5), iris_r=10.5),
}

src = Image.open(SRC).convert("RGB")
W, H = (CROP[2] - CROP[0]) * SCALE, (CROP[3] - CROP[1]) * SCALE
crop = src.crop(CROP).resize((W, H), Image.LANCZOS)


def to_out(p):
    return ((p[0] - CROP[0]) * SCALE, (p[1] - CROP[1]) * SCALE)


# ---- 画布：眼裂抠空（略小于眼裂，留住睫毛线） ----
hole = Image.new("L", (W, H), 0)
d = ImageDraw.Draw(hole)
for e in EYES.values():
    cx, cy = to_out(e["open_c"])
    a, b = e["open_ab"][0] * SCALE * 0.92, e["open_ab"][1] * SCALE * 0.85
    d.ellipse((cx - a, cy - b, cx + a, cy + b), fill=255)
hole = hole.filter(ImageFilter.GaussianBlur(1.2))
canvas = crop.copy()
canvas.putalpha(hole.point(lambda v: 255 - v))
canvas.save(f"{OUT}/LilyPortrait_Canvas.png")

# ---- 眼白底图：虹膜范围涂成眼白色（取眼裂内、虹膜外的像素平均），上眼睑处压暗一点 ----
back = crop.copy()
px = back.load()
for e in EYES.values():
    cx, cy = to_out(e["open_c"])
    a, b = e["open_ab"][0] * SCALE * 1.05, e["open_ab"][1] * SCALE * 1.1
    ix, iy = to_out(e["iris_c"])
    ir = e["iris_r"] * SCALE
    samples = []
    for y in range(int(cy - b), int(cy + b) + 1):
        for x in range(int(cx - a), int(cx + a) + 1):
            inside = ((x - cx) / a) ** 2 + ((y - cy) / b) ** 2 <= 0.8
            if inside and (x - ix) ** 2 + (y - iy) ** 2 > (ir + 3 * SCALE) ** 2:
                samples.append(px[x, y])
    samples.sort(key=sum)
    samples = samples[len(samples) * 85 // 100:]   # 取最亮的 15%，避开眼角阴影和睫毛
    white = tuple(sum(c[i] for c in samples) // len(samples) for i in range(3))
    for y in range(int(cy - b) - 2, int(cy + b) + 3):
        for x in range(int(cx - a) - 2, int(cx + a) + 3):
            if ((x - cx) / a) ** 2 + ((y - cy) / b) ** 2 > 1.0:
                continue
            k = 0.72 + 0.28 * max(0.0, min(1.0, (y - (cy - b)) / (2 * b)))   # 上暗下亮：眼睑投影
            edge = ((x - cx) / a) ** 2                                       # 两侧眼角也压暗
            k *= 1.0 - 0.25 * edge
            px[x, y] = tuple(int(c * k) for c in white)
back = back.filter(ImageFilter.GaussianBlur(0.6))
back.save(f"{OUT}/LilyPortrait_EyeBack.png")

# ---- 虹膜：从原画抠圆，边缘 1.5 px 羽化；方形贴图，虹膜正好占满 ----
for k, e in EYES.items():
    ix, iy = to_out(e["iris_c"])
    ir = e["iris_r"] * SCALE
    pad = 2 * SCALE
    size = int(round((ir + pad) * 2))
    box = (int(round(ix - size / 2)), int(round(iy - size / 2)))
    patch = crop.crop((box[0], box[1], box[0] + size, box[1] + size))
    m = Image.new("L", (size * 4, size * 4), 0)
    ImageDraw.Draw(m).ellipse((pad * 4, pad * 4, size * 4 - pad * 4, size * 4 - pad * 4), fill=255)
    m = m.resize((size, size), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.8))
    patch.putalpha(m)
    patch = patch.resize((128, 128), Image.LANCZOS)
    patch.save(f"{OUT}/LilyPortrait_Iris_{k}.png")
    print(k, "iris px size", size, "-> 128")
print("canvas", W, H)
