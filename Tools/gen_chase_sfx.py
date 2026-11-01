"""二楼名画人物"破框尖叫" + 被追击心跳（Ib 美术 2026-11-01）。
  PaintingScream.wav  ~2.3 s，0 s 起音就接近峰值：玻璃/画框炸裂的噪声爆点 + 两段 CC0 女声尖叫叠层
                      （原声 + 降 5 个半音的"非人"层 + 升 3 个半音的刺耳层 + 轻微失真），尾巴是走廊混响。
  ChaseHeartbeat.wav  正好 1.000 s = 60 BPM 的一拍"咚-咚"（lub 0.02 s、dub 0.30 s），首尾循环无缝。
                      代码用 pitch 1.0–1.8 提速 → 60–108 BPM；基频 46/58 Hz，1.8 倍时 83/104 Hz，
                      另有 2 次谐波失真让笔记本小喇叭也听得到。
录音来源（CC0，见 Assets/Resources/Audio/LICENSES.md），原文件不进工程：
  female_scream_1.ogg  https://opengameart.org/content/female-high-pitched-scream-sfx (WuxiaScrub)
  screams_0.ogg        https://opengameart.org/content/female-scream-1 (Nocturnal_Vanguard)
用法：python3 Tools/gen_chase_sfx.py <放两个 ogg 的文件夹> Assets/Resources/Audio
"""
import subprocess
import sys
import wave
import numpy as np

SR = 44100
SRC, OUT = sys.argv[1], sys.argv[2]
rng = np.random.default_rng(2611)


def load(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(SR),
                          "-f", "f32le", "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype="<f4").astype(float)


def write(name, x, peak=0.89):
    x = x / max(1e-6, np.abs(x).max()) * peak
    with wave.open(f"{OUT}/{name}.wav", "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())


def lp(x, fc):
    a = 1 - np.exp(-2 * np.pi * fc / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a * (x[i] - acc)
        y[i] = acc
    return y


def hp(x, fc):
    return x - lp(x, fc)


def resample(x, ratio):
    """ratio>1 → 音调升高、变短。"""
    n = int(len(x) / ratio)
    return np.interp(np.arange(n) * ratio, np.arange(len(x)), x)


def fade(x, fin, fout):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b) ** 2
    return x


def reverb(x, seconds, wet, damp=3000):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = lp(rng.standard_normal(n), damp) * np.exp(-6.9 * t / seconds)
    ir[: int(0.015 * SR)] = 0
    ir /= np.sqrt((ir ** 2).sum())
    size = 1 << (len(x) + n - 1).bit_length()
    y = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[: len(x) + n]
    y[: len(x)] += x / max(wet, 1e-3)
    return y * wet


def place(buf, x, at):
    i = int(at * SR)
    buf[i:i + len(x)] += x[: len(buf) - i]


def scream():
    a = load(f"{SRC}/female_scream_1.ogg")[int(0.46 * SR):int(1.36 * SR)]   # 0.9 s 高音尖叫
    b = load(f"{SRC}/screams_0.ogg")[int(0.0 * SR):int(1.40 * SR)]          # 1.4 s 第一声
    a, b = fade(a, 0.004, 0.15), fade(b, 0.004, 0.35)
    a /= np.abs(a).max()
    b /= np.abs(b).max()
    dur = 1.9
    buf = np.zeros(int(dur * SR))
    # 主体：b 原调 + a 升 3 半音紧跟其后（刺耳的第二口气）
    place(buf, b * 0.8, 0.0)
    place(buf, resample(a, 2 ** (3 / 12)) * 0.75, 0.05)
    # 非人层：b 降 5 半音、略迟 30 ms，像两个喉咙同时叫
    place(buf, resample(b, 2 ** (-5 / 12))[: int(1.85 * SR)] * 0.55, 0.03)
    # 失真 + 去掉 150 Hz 以下浑浊
    buf = np.tanh(hp(buf, 150) * 2.2) * 0.7
    # 起音爆点：画框木头/玻璃炸开 80 ms 的宽带噪声 + 低频冲击
    t = np.arange(int(0.35 * SR)) / SR
    crack = hp(rng.standard_normal(len(t)), 1500) * np.exp(-t / 0.035) * 0.9
    thump = np.sin(2 * np.pi * (70 * t - 40 * t ** 2)) * np.exp(-t / 0.09) * 0.8
    place(buf, crack + thump, 0.0)
    out = reverb(buf, 1.1, 0.35, damp=4000)
    out = fade(out[: int(2.35 * SR)], 0.0, 0.4)
    write("PaintingScream", out, peak=0.95)


def heartbeat():
    n = SR                                         # 正好 1 s = 60 BPM
    buf = np.zeros(n + SR)

    def thump(f0, f1, decay, amp):
        t = np.arange(int(0.45 * SR)) / SR
        f = f1 + (f0 - f1) * np.exp(-t / 0.04)
        ph = 2 * np.pi * np.cumsum(f) / SR
        env = np.minimum(1, t / 0.006) * np.exp(-t / decay)
        body = np.sin(ph) * env
        body = np.tanh(body * 2.5) / np.tanh(2.5)          # 2/3 次谐波，小喇叭也听得到
        knock = lp(rng.standard_normal(len(t)), 220) * np.exp(-t / 0.015) * 3.0   # 瓣膜"扑"一下
        return (body + knock * 0.25) * amp

    place(buf, thump(78, 46, 0.085, 1.0), 0.02)    # lub
    place(buf, thump(90, 58, 0.060, 0.72), 0.30)   # dub
    loop = buf[:n].copy()
    loop[: len(buf) - n] += buf[n:][: n]           # 尾巴卷回开头 → 无缝
    loop = lp(loop, 900)
    loop -= loop.mean()
    write("ChaseHeartbeat", loop, peak=0.9)


scream()
heartbeat()
print("ok")
