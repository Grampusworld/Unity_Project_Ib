"""坏结局 CG 的两个音效（Ib 美术 2026-11-01，纯合成，无第三方素材）：
  MainDoorOpen.wav   正门双开门缓慢打开 ~3.4 s：0 s 锁舌"咔"+ 门板闷响，0.2–3.0 s 两扇重木门的低沉吱呀（粘滑摩擦）
                     + 合页高音、门缝里涌出的一股凉气，3.05 s 门开到底轻轻一顿；带一点空库房的混响
  EndingStinger.wav  转身看到天使时的重击 ~4 s：0 s 处就是峰值（低频冲击 + 噪声爆点 + 不协和金属泛音），
                     跟硬切黑屏对齐：播放的同一帧切黑；尾巴在黑屏里慢慢散掉
用法：python3 Tools/gen_ending_sfx.py Assets/Resources/Audio
"""
import sys
import wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
rng = np.random.default_rng(1107)


def write(name, x, peak=0.89):
    x = x / max(1e-6, np.abs(x).max()) * peak
    with wave.open(f"{OUT}/{name}.wav", "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())


def onepole_lp(x, cutoff):
    """一阶低通；cutoff 可以是随时间变化的数组。"""
    c = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    a = 1 - np.exp(-2 * np.pi * c / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a[i] * (x[i] - acc)
        y[i] = acc
    return y


def bandpass(x, lo, hi):
    return onepole_lp(onepole_lp(x, hi) - onepole_lp(onepole_lp(x, hi), lo), hi)


def reverb(x, seconds, wet, damp=2500):
    """指数衰减噪声当冲激响应（够用的小房间混响），FFT 卷积。"""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-6.9 * t / seconds)
    ir = onepole_lp(ir, damp)
    ir[: int(0.012 * SR)] = 0                                  # 12 ms 预延迟
    ir /= np.sqrt((ir ** 2).sum())
    m = len(x) + n
    size = 1 << (m - 1).bit_length()
    y = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[: len(x) + n]
    out = np.zeros(len(y))
    out[: len(x)] += x
    return out + y * wet


def env_ad(n, attack, decay):
    t = np.arange(n) / SR
    return np.minimum(1, t / max(attack, 1e-4)) * np.exp(-t / decay)


def thud(n, f0, f1, decay):
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / 0.06)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * env_ad(n, 0.002, decay)


# ---------------------------------------------------------------- 正门打开
def main_door_open():
    total = 3.4
    n = int(total * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)

    # 锁舌：金属"咔嗒"两下 + 门板一声闷响
    for at, amp in ((0.0, 1.0), (0.07, 0.6)):
        k = int(at * SR)
        m = int(0.05 * SR)
        click = bandpass(rng.standard_normal(m), 1800, 6500) * env_ad(m, 0.0005, 0.008)
        x[k:k + m] += click * 0.9 * amp
    m = int(0.5 * SR)
    x[int(0.09 * SR):int(0.09 * SR) + m] += thud(m, 140, 70, 0.11) * 0.8

    # 两扇门各一条低沉吱呀：粘滑摩擦 = 一串很密的小脉冲，脉冲率（= 音高）慢慢游走，经共鸣带通
    def groan(start, end, rate0, rate1, seed_shift):
        g = np.zeros(n)
        s, e = int(start * SR), int(end * SR)
        tt = t[s:e] - start
        dur = end - start
        rate = rate0 + (rate1 - rate0) * tt / dur + 18 * np.sin(2 * np.pi * (0.7 + seed_shift) * tt) \
            + 9 * np.sin(2 * np.pi * 2.3 * tt + seed_shift * 5)
        ph = np.cumsum(rate) / SR
        pulses = np.diff(np.floor(ph), prepend=0) > 0
        train = pulses.astype(float) * (0.6 + 0.4 * rng.random(len(tt)))
        body = bandpass(train, 120, 900) + 0.5 * bandpass(train, 900, 2400)
        # 用力不均：一阵紧一阵松
        press = 0.55 + 0.45 * np.sin(2 * np.pi * (0.9 + seed_shift) * tt + 1.3) ** 2
        fade = np.minimum(1, tt / 0.25) * np.minimum(1, (dur - tt) / 0.3)
        g[s:e] = body * press * fade
        return g

    x += groan(0.22, 3.0, 85, 60, 0.0) * 2.2
    x += groan(0.35, 3.05, 110, 74, 0.37) * 1.6

    # 合页高音：时有时无的细"吱——"
    s, e = int(0.5 * SR), int(2.7 * SR)
    tt = t[s:e] - 0.5
    f = 640 + 160 * np.sin(2 * np.pi * 0.4 * tt) + 45 * np.sin(2 * np.pi * 3.1 * tt)
    gate = np.clip(np.sin(2 * np.pi * 0.62 * tt + 0.4), 0, 1) ** 1.5
    squeal = np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.3 * np.sin(4 * np.pi * np.cumsum(f) / SR)
    x[s:e] += squeal * gate * np.minimum(1, (2.2 - tt) / 0.4) * 0.12

    # 门缝里涌出的凉气：低频噪声随开度增大
    air = onepole_lp(rng.standard_normal(n), 380 + 500 * np.clip(t / 3.0, 0, 1))
    x += air * np.clip((t - 0.25) / 2.6, 0, 1) ** 1.4 * np.clip((3.35 - t) / 0.35, 0, 1) * 0.9

    # 开到底：门轴一顿
    k = int(3.05 * SR)
    m = n - k
    x[k:] += thud(m, 110, 60, 0.07) * 0.45

    x = reverb(x, 1.1, 0.35, 2200)
    fade = np.ones(len(x))
    tail = int(0.3 * SR)
    fade[-tail:] = np.linspace(1, 0, tail)
    return x * fade


# ---------------------------------------------------------------- 重击
def ending_stinger():
    total = 4.2
    n = int(total * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)

    # 低频冲击：48 Hz 往 31 Hz 掉，1.2 s 衰减，加 2 次谐波让小喇叭也听得到
    f = 31 + 17 * np.exp(-t / 0.35)
    ph = 2 * np.pi * np.cumsum(f) / SR
    x += (np.sin(ph) + 0.35 * np.sin(2 * ph)) * np.exp(-t / 1.2) * 1.4

    # 爆点：宽带噪声 0.25 s，迅速变暗
    burst = rng.standard_normal(n) * np.exp(-t / 0.05)
    x += onepole_lp(burst, 5000 * np.exp(-t / 0.15) + 200) * 2.2

    # 不协和金属泛音（像一口被重重敲了一下的大锣 + 走音钢琴低音区），慢慢散开
    for freq, amp, dec in ((73.4, 0.5, 2.4), (87.3, 0.45, 2.0), (97.9, 0.4, 2.2), (138.6, 0.3, 1.6),
                           (196.0, 0.22, 1.3), (277.2, 0.18, 1.1), (415.3, 0.12, 0.9), (622.3, 0.08, 0.7),
                           (933.0, 0.06, 0.5), (1397.0, 0.04, 0.35)):
        detune = 1 + 0.004 * np.sin(2 * np.pi * 0.3 * t + freq)
        x += np.sin(2 * np.pi * freq * np.cumsum(detune) / SR) * amp * 0.5 * np.exp(-t / dec) * (1 + 1.5 * np.exp(-t / 0.08))

    # 尾巴里一层很低的嗡鸣（黑屏里还"有东西在"），2.5 s 后淡出
    hum = bandpass(rng.standard_normal(n), 40, 160) * np.clip((t - 0.2) / 0.6, 0, 1) * np.clip((total - t) / 1.6, 0, 1)
    x += hum * 0.15

    # 混响只给中高频（噪声冲激响应的低频部分起伏很大，会让尾巴一鼓一鼓的）
    low = onepole_lp(x, 220)
    x = low + reverb(x - low, 2.4, 0.5, 1800)[:n]
    tail = int(0.4 * SR)
    x[-tail:] *= np.linspace(1, 0, tail)
    # 峰值必须落在最开头（和切黑同一帧），开头 3 ms 起音避免爆音
    x[: int(0.003 * SR)] *= np.linspace(0, 1, int(0.003 * SR))
    return x


write("MainDoorOpen", main_door_open(), 0.85)
write("EndingStinger", ending_stinger(), 0.95)
print("ok")
