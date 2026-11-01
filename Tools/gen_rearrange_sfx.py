"""布展声（GDD §3.3 / Q16，2026-11-01，纯合成，无第三方素材）：馆藏在视线外挪东西时偶尔放一声。

  HangingHook.wav   1.600 s。挂画钩在铜轨上滑一下、咔嗒落位：0.05 s 起一串细碎金属滑擦，
                    0.55 s 一记钩子落槽的清脆金属声（几个不谐和泛音快速衰减），再一声很轻的回弹，展厅混响收尾。
  FrameScrape.wav   1.900 s。木画框 / 底座贴着墙面或地板被拖动：粘滑（stick-slip）调制的低通噪声，
                    两段拖动中间停一下，最后一记闷响放稳。

单声道 44.1 kHz / 16-bit PCM。用法：/usr/bin/python3 Tools/gen_rearrange_sfx.py Assets/Resources/Audio
"""
import sys
import wave

import numpy as np

SR = 44100
OUT = sys.argv[1]
rng = np.random.default_rng(20261102)


def write(name, x, peak):
    x = np.asarray(x, dtype=float)
    x = x - x.mean()
    x = np.clip(x / max(1e-9, np.abs(x).max()) * peak, -1, 1)
    with wave.open(f"{OUT}/{name}.wav", "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    print(f"{name}.wav  {len(x) / SR:.3f}s")


def t(seconds):
    return np.arange(int(seconds * SR)) / SR


def lowpass(x, cutoff):
    # 一阶 IIR，够用
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def reverb(x, seconds, mix):
    n = int(seconds * SR)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / SR * 6.9 / seconds)
    ir = lowpass(ir, 3500)
    wet = np.convolve(x, ir)[: len(x)]
    wet /= max(1e-9, np.abs(wet).max())
    return x / max(1e-9, np.abs(x).max()) * (1 - mix) + wet * mix


def place(buf, start, sig):
    i = int(start * SR)
    j = min(len(buf), i + len(sig))
    buf[i:j] += sig[: j - i]


def metal_hit(seconds, base, partials, decay):
    tt = t(seconds)
    s = np.zeros_like(tt)
    for k, ratio in enumerate(partials):
        s += np.sin(2 * np.pi * base * ratio * tt + rng.uniform(0, 6.28)) * np.exp(-tt * decay * (1 + 0.6 * k)) / (1 + k * 0.5)
    click = rng.standard_normal(len(tt)) * np.exp(-tt * 400)
    return s + highpass(click, 2000) * 0.6


def hanging_hook():
    total = 1.6
    buf = np.zeros(int(total * SR))
    # 滑擦：高通噪声 × 快速颤动的包络
    slide = t(0.45)
    env = (0.5 + 0.5 * np.sin(2 * np.pi * 38 * slide) ** 8) * np.sin(np.pi * slide / 0.45) ** 0.7
    place(buf, 0.05, highpass(rng.standard_normal(len(slide)), 3000) * env * 0.25)
    # 落槽
    place(buf, 0.55, metal_hit(0.9, 1870, [1.0, 2.76, 5.40, 8.93], 9.0))
    place(buf, 0.68, metal_hit(0.5, 2210, [1.0, 2.9, 6.1], 16.0) * 0.25)
    write("HangingHook", reverb(buf, 1.3, 0.35), 0.7)


def frame_scrape():
    total = 1.9
    buf = np.zeros(int(total * SR))
    for start, dur in ((0.0, 0.55), (0.75, 0.75)):
        tt = t(dur)
        # 粘滑：不规则的脉冲串调制噪声
        rate = 22 + 10 * rng.random()
        phase = np.cumsum(rate * (1 + 0.4 * rng.standard_normal(len(tt)) * 0.05)) / SR
        stick = (np.mod(phase, 1.0) < 0.35).astype(float)
        stick = lowpass(stick, 60)
        noise = lowpass(rng.standard_normal(len(tt)), 1400) - lowpass(rng.standard_normal(len(tt)), 120) * 0.3
        env = np.sin(np.pi * tt / dur) ** 0.5
        place(buf, start, noise * (0.35 + stick) * env)
    # 放稳：低频闷响
    th = t(0.35)
    thump = np.sin(2 * np.pi * 85 * th) * np.exp(-th * 18) + lowpass(rng.standard_normal(len(th)), 400) * np.exp(-th * 30) * 0.5
    place(buf, 1.55, thump * 1.6)
    write("FrameScrape", reverb(buf, 1.2, 0.3), 0.7)


hanging_hook()
frame_scrape()
