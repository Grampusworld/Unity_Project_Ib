#!/usr/bin/env python3
"""监控雪花底噪：CRT 静电噪声 + 电流嗡声，3.0 s 无缝循环（Ib 技术，2026-11-01）。

SecurityMonitors 过场里按屏幕雪花密度调 volume / pitch，所以只做干净的中频嘶声，
不要有明显节拍或音调，升速 0.9–1.12 倍都听不出接缝。纯合成，无第三方素材。
"""
import math
import random
import struct
import wave

SR = 44100
CROSS = 0.25          # 首尾交叉淡化的长度
TAIL = 3.0            # 成品长度
random.seed(20261101)

def lowpass(x, a):
    y, v = [], 0.0
    for s in x:
        v += a * (s - v)
        y.append(v)
    return y

def highpass(x, a):
    y, prev_x, prev_y = [], 0.0, 0.0
    for s in x:
        prev_y = a * (prev_y + s - prev_x)
        prev_x = s
        y.append(prev_y)
    return y

n = int((TAIL + CROSS) * SR)
raw = [random.uniform(-1.0, 1.0) for _ in range(n)]
# 静电：先砍掉低频隆隆声，再压掉刺耳的高频，留 800 Hz–7 kHz 的「沙」
f = highpass(lowpass(raw, 0.45), 0.86)
f = lowpass(f, 0.55)

out = []
crackles = {}
t_crack = 0.15
while t_crack < TAIL + CROSS:
    crackles[int(t_crack * SR)] = random.uniform(0.25, 0.6)
    t_crack += random.uniform(0.35, 0.9)

for i in range(n):
    t = i / SR
    # 缓慢起伏，像老监视器的行频抖动
    mod = 0.82 + 0.10 * math.sin(2 * math.pi * 0.37 * t) + 0.06 * math.sin(2 * math.pi * 1.13 * t)
    v = f[i] * 0.55 * mod
    v += 0.012 * math.sin(2 * math.pi * 60 * t) + 0.006 * math.sin(2 * math.pi * 120 * t)
    if i in crackles:
        v += crackles[i]
    out.append(v)

# 把尾巴交叉淡进开头，循环点没有跳变
head = out[:int(CROSS * SR)]
tail = out[int(TAIL * SR):int(TAIL * SR) + int(CROSS * SR)]
for i in range(len(head)):
    k = i / len(head)
    head[i] = tail[i] * (1.0 - k) + head[i] * k
out = out[:int(TAIL * SR)]

peak = max(abs(v) for v in out)
gain = 10 ** (-6.0 / 20.0) / peak        # 峰值 −6 dBFS
out = [v * gain for v in out]

path = "Assets/Resources/Audio/MonitorStatic.wav"
with wave.open(path, "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(b"".join(struct.pack("<h", max(-32768, min(32767, int(v * 32767)))) for v in out))

seam = abs(out[0] - out[-1])
rms = math.sqrt(sum(v * v for v in out) / len(out))
print(f"{path}: {len(out)/SR:.3f}s, peak {20*math.log10(max(abs(v) for v in out)):.1f} dBFS, "
      f"rms {20*math.log10(rms):.1f} dBFS, seam {seam:.4f}")
