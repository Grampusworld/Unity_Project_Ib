"""配电室铁门打开（Ib 美术 2026-11-01，纯合成，无第三方素材）

PowerRoomDoorOpen.wav  2.200 s / 单声道 44.1 kHz / 16-bit PCM

声音顺序（和 1.5–2.5 s 的开门动画对齐）：
  0.00 s  锁舌"咔"一声，接着门板被推开一条缝的低频"咚"
  0.10 s  门轴/密封条开始摩擦，金属吱呀从低到高、断断续续
  1.55 s  门开到最大，门板撞到限位，一记闷响 + 金属余振
  1.85 s  把手/锁舌回弹的两下轻响
  2.20 s  走廊混响收尾

用法：python3 Tools/gen_power_room_door.py Assets/Resources/Audio
"""
import sys
import wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
rng = np.random.default_rng(20261101)
DUR = 2.2
N = int(DUR * SR)
t = np.arange(N) / SR
x = np.zeros(N)


def add(sig, at, gain=1.0):
    i = int(at * SR)
    if i >= N:
        return
    n = min(len(sig), N - i)
    x[i:i + n] += sig[:n] * gain


def env_exp(n, attack, decay, power=1.0):
    e = np.ones(n)
    a = max(1, int(attack * SR))
    e[:a] = np.linspace(0, 1, a) ** 0.6
    d = max(1, int(decay * SR))
    tail = np.exp(-np.linspace(0, 6.9, d) * power)
    e[a:a + len(tail)] *= tail[:max(0, n - a)]
    return e


def bp_noise(n, lo, hi):
    y = rng.standard_normal(n)
    spec = np.fft.rfft(y)
    f = np.fft.rfftfreq(n, 1 / SR)
    gain = 1 / np.sqrt(1 + (lo / np.maximum(f, 1e-6)) ** 4)
    gain *= 1 / np.sqrt(1 + (f / hi) ** 4)
    return np.fft.irfft(spec * gain, n)


def thump(freq, dur, decay=0.08):
    n = int(dur * SR)
    tt = np.arange(n) / SR
    y = np.sin(2 * np.pi * freq * tt) * np.exp(-tt / decay)
    y += 0.35 * np.sin(2 * np.pi * freq * 1.47 * tt) * np.exp(-tt / (decay * 0.6))
    return y


def metal_hit(dur=0.5):
    n = int(dur * SR)
    tt = np.arange(n) / SR
    y = np.zeros(n)
    for f, a, d in [(315, 0.55, 0.20), (742, 0.35, 0.12), (1180, 0.22, 0.09),
                    (1860, 0.14, 0.06), (2740, 0.08, 0.04)]:
        y += a * np.sin(2 * np.pi * f * tt + rng.uniform(0, 6.28)) * np.exp(-tt / d)
    return y


# 1) 锁舌 + 门缝第一下
click = bp_noise(int(0.055 * SR), 1600, 7800) * env_exp(int(0.055 * SR), 0.001, 0.05, 2.2)
add(click, 0.00, 0.55)
add(thump(92, 0.42, 0.11), 0.015, 0.62)
add(metal_hit(0.35), 0.018, 0.16)

# 2) 门轴摩擦：低频摩擦 + 断断续续的高频吱呀
n = int(1.55 * SR)
tt = np.arange(n) / SR
friction = bp_noise(n, 180, 1100)
slow = 0.35 + 0.65 * np.clip(np.sin(2 * np.pi * 0.9 * tt - 1.2), 0, 1)
add(friction * slow * env_exp(n, 0.08, 1.7, 1.4), 0.20, 0.30)

# 逐段"吱"：频率和起始时间都带随机，像铁门一卡一卡地转
starts = [0.28, 0.46, 0.63, 0.82, 0.99, 1.14, 1.29, 1.44]
for k, st in enumerate(starts):
    dur = rng.uniform(0.16, 0.30)
    m = int(dur * SR)
    u = np.arange(m) / SR
    f0 = rng.uniform(520, 760)
    f1 = f0 * rng.uniform(1.35, 1.85)
    phase = 2 * np.pi * (f0 * u + 0.5 * (f1 - f0) / max(dur, 1e-3) * u * u)
    # 带一点金属谐波和颤音
    vib = 1 + 0.012 * np.sin(2 * np.pi * 7.5 * u + rng.uniform(0, 6.28))
    y = np.sin(phase * vib) + 0.38 * np.sin(2 * phase * vib + 1.1)
    y *= np.sin(np.pi * np.clip(u / dur, 0, 1)) ** 0.7
    add(y * 0.12, st, rng.uniform(0.35, 0.62))

# 3) 门开到限位：闷响 + 金属余振 + 门框轻碰
add(thump(68, 0.65, 0.16), 1.53, 0.75)
add(metal_hit(0.75), 1.54, 0.34)
add(bp_noise(int(0.20 * SR), 900, 4200) * env_exp(int(0.20 * SR), 0.002, 0.18, 2.0), 1.55, 0.12)

# 4) 把手 / 锁舌回弹
for i, at in enumerate([1.83, 1.92]):
    n2 = int(0.10 * SR)
    add(bp_noise(n2, 1300, 6000) * env_exp(n2, 0.001, 0.09, 2.4), at, 0.22 - i * 0.05)
    add(thump(150, 0.12, 0.035), at, 0.16)

# 5) 小房间混响：指数衰减噪声 IR
ir_n = int(0.55 * SR)
ir_t = np.arange(ir_n) / SR
ir = bp_noise(ir_n, 120, 4200) * np.exp(-ir_t / 0.13)
ir[:int(0.012 * SR)] = 0
ir /= np.sqrt((ir ** 2).sum())
size = 1 << (N + ir_n - 1).bit_length()
wet = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[:N]
x = x + wet * 0.55

# 6) 高通去直流、轻微软限幅、归一化
spec = np.fft.rfft(x)
f = np.fft.rfftfreq(N, 1 / SR)
spec *= 1 / np.sqrt(1 + (38 / np.maximum(f, 1e-6)) ** 4)
x = np.fft.irfft(spec, N)
x = np.tanh(x * 1.15) / np.tanh(1.15)
x -= x.mean()
x = np.clip(x / np.max(np.abs(x)) * 0.86, -1, 1)

with wave.open(f"{OUT}/PowerRoomDoorOpen.wav", "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes((x * 32767).astype("<i2").tobytes())

print(f"PowerRoomDoorOpen.wav  {len(x) / SR:.3f}s  peak {20 * np.log10(np.abs(x).max()):.1f} dBFS  "
      f"rms {20 * np.log10(np.sqrt(np.mean(x ** 2))):.1f} dBFS")
