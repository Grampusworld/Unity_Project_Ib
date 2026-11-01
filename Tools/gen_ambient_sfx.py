"""人群声 / 环境底噪 / 二楼厕所异响（Ib 美术 2026-11-01，纯合成，无第三方素材）

  CrowdMurmur.wav            24.000 s 无缝循环。15 个"听不清字"的游客声线（不同音高 / 共振峰 /
                             音节率 / 换气停顿）叠在一起，加零星石材地面脚步、两三声轻咳、远处椅子
                             挪动；整体低通 5 kHz，带 1.8 s 展厅混响，像隔着一层空气的循环录音。
  AmbientRoomTone.wav        40.000 s 无缝循环。空调低频（50 Hz 电流嗡声 + 风管低频 + 极轻的气流），
                             缓慢起伏；没有事件音，停电前全程铺底。
  AmbientRoomTone_PowerOut.wav 40.000 s 无缝循环。断电后的阴冷版：没有电流嗡声，只剩更低的下潜
                             和风管气流，偶尔一声远处水管/金属轻响，更空、更冷。
  ToiletNoise.wav            3.200 s。金属/瓷器撞击 + 闷响 + 水管咕噜：0 s 起三下瓷器碰撞，0.5 s 一记
                             闷响，0.9–2.6 s 管道咕噜冒泡，瓷砖厕所混响收尾。

循环做法：底噪用 FFT 整形噪声（本身以文件长度为周期）+ 整数周期正弦，混响用循环卷积，首尾严格
相等；人群声是事件性的，先生成 24 s + 3 s 尾巴，再把尾巴用等功率交叉淡化卷回开头，接缝连续。
所有电平均为单声道 44.1 kHz / 16-bit PCM。

用法：python3 Tools/gen_ambient_sfx.py Assets/Resources/Audio
"""
import sys
import wave
import numpy as np

SR = 44100
OUT = sys.argv[1]
rng = np.random.default_rng(20261101)


# ---------------------------------------------------------------- 基础工具
def write(name, x, peak):
    x = np.asarray(x, dtype=float)
    x = x - x.mean()
    x = np.clip(x / max(1e-9, np.abs(x).max()) * peak, -1, 1)
    with wave.open(f"{OUT}/{name}.wav", "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    rms = np.sqrt(np.mean(x ** 2))
    seam = abs(x[0] - x[-1]) / max(1e-9, rms)
    print(f"{name}.wav  {len(x) / SR:.3f}s  peak {20 * np.log10(np.abs(x).max()):.1f} dBFS  "
          f"rms {20 * np.log10(rms):.1f} dBFS  seam {seam:.4f}·rms")


def fft_filter(x, gain):
    """按频响 gain(f) 做循环滤波（长度不变）。"""
    n = len(x)
    f = np.fft.rfftfreq(n, 1 / SR)
    return np.fft.irfft(np.fft.rfft(x) * gain(f), n)


def lp(f, fc, order=2):
    return 1 / np.sqrt(1 + (f / fc) ** (2 * order))


def hp(f, fc, order=2):
    return 1 / np.sqrt(1 + (fc / np.maximum(f, 1e-6)) ** (2 * order))


def bp(f, lo, hi, order=2):
    return hp(f, lo, order) * lp(f, hi, order)


def peak(f, fc, q, amp):
    """洛伦兹共振峰。"""
    return amp / (1 + (q * (f - fc) / fc) ** 2)


def noise_band(n, lo, hi, order=2):
    return fft_filter(rng.standard_normal(n), lambda f: bp(f, lo, hi, order))


def smooth_control(n, rate, win_s):
    """rate Hz 的随机控制点，平滑成缓变的 0..1 包络。"""
    m = int(n / SR * rate) + 3
    a = rng.random(m)
    k = max(1, int(win_s * rate))
    a = np.convolve(a, np.ones(k) / k, mode="same")
    return np.interp(np.arange(n), np.linspace(0, n, m), a)


def fade(x, fin, fout):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b) ** 2
    return x


def reverb(x, seconds, wet, damp=3200, predelay=0.012):
    """指数衰减噪声 IR，线性卷积；用于非循环音效。"""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = noise_band(n, 80, damp) * np.exp(-6.9 * t / seconds)
    ir[: int(predelay * SR)] = 0
    ir /= np.sqrt((ir ** 2).sum())
    m = len(x) + n
    size = 1 << (m - 1).bit_length()
    y = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[: len(x)]
    return x + y * wet


def circ_reverb(x, seconds, wet, damp=3200):
    """循环卷积混响：输出仍以 len(x) 为周期，保证无缝循环。"""
    n = len(x)
    t = np.arange(n) / SR
    ir = noise_band(n, 80, damp) * np.exp(-6.9 * t / seconds)
    ir[int(0.012 * SR):] /= np.sqrt((ir[int(0.012 * SR):] ** 2).sum())
    ir[: int(0.012 * SR)] = 0
    return np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(ir), n) * wet + x


def place_wrap(buf, x, at):
    i = int(at * SR) % len(buf)
    idx = (i + np.arange(len(x))) % len(buf)
    np.add.at(buf, idx, x)


# ---------------------------------------------------------------- 人群声
def crowd_voice(n, f0, formants):
    t = np.arange(n) / SR
    ctrl = int(n / SR * 3) + 3
    drift = np.interp(t, np.linspace(0, n / SR, ctrl),
                      rng.normal(0, 0.035, ctrl))
    drift = np.convolve(drift, np.ones(200) / 200, mode="same")
    f = f0 * (1 + drift)
    phase = 2 * np.pi * np.cumsum(f) / SR
    saw = 2 * np.mod(phase / (2 * np.pi), 1) - 1
    src = saw * 0.85 + rng.standard_normal(n) * 0.12

    # 音节率本身也缓慢变化，避免像节拍器
    fs0 = rng.uniform(2.7, 5.4)
    fsv = fs0 * (1 + 0.22 * (smooth_control(n, 0.6, 0.8) - 0.5))
    syl_phase = 2 * np.pi * np.cumsum(fsv) / SR + rng.uniform(0, 2 * np.pi)
    syl = np.abs(np.sin(syl_phase)) ** 1.5
    g = smooth_control(n, 6.0, 0.22)                # 换气 / 停顿
    gate = np.clip((g - 0.36) * 4.2, 0, 1)
    env = syl * gate

    breath = noise_band(n, 1800, 6500) * (env ** 2) * 0.055
    # 音节起音处的辅音爆点（很轻，但让"说话"更像说话）
    onset = np.maximum(0, np.diff(env, prepend=0))
    breath += noise_band(n, 1200, 5200) * onset * 0.10
    src = src * env + breath

    # 三个共振峰组合缓慢交替，模拟换元音；不会出现固定不变的"电子管风琴"音色
    outs = []
    for k, (F1, F2, F3) in enumerate(formants):
        def gain(fr, F1=F1, F2=F2, F3=F3):
            return (0.025
                    + peak(fr, F1, 5.5, 1.0)
                    + peak(fr, F2, 7.0, 0.65)
                    + peak(fr, F3, 9.0, 0.30)) * lp(fr, 5200) * hp(fr, 110)
        outs.append(fft_filter(src, gain))
    w = []
    for k in range(3):
        rate = (0.11, 0.083, 0.067)[k]
        w.append(0.35 + 0.65 * (0.5 + 0.5 * np.sin(2 * np.pi * rate * t + rng.uniform(0, 2 * np.pi))))
    wsum = sum(w)
    voice = sum(o * wk for o, wk in zip(outs, w)) / wsum
    return voice / 3.0


def stone_step():
    n = int(0.19 * SR)
    t = np.arange(n) / SR
    click = noise_band(n, 900, 4200) * np.exp(-t / 0.018)
    thump = np.sin(2 * np.pi * (92 * t - 55 * t ** 2)) * np.exp(-t / 0.05)
    return (click * 0.55 + thump * 0.8) * 0.55


def cough():
    n = int(0.42 * SR)
    t = np.arange(n) / SR
    env = (1 - np.exp(-t / 0.006)) * np.exp(-t / 0.075)
    env2 = (1 - np.exp(-np.maximum(t - 0.16, 0) / 0.006)) * np.exp(-np.maximum(t - 0.16, 0) / 0.05)
    env = env + 0.65 * env2 * (t > 0.16)
    body = noise_band(n, 260, 2100) * env
    body += np.sin(2 * np.pi * 115 * t) * np.exp(-t / 0.09) * 0.35
    return body * 0.55


def crowd(L=24.0):
    tail, rev = 3.0, 2.0
    n = int((L + tail + rev) * SR)
    mix = np.zeros(n)

    voice_specs = [(105, 480, 1350, 2500), (122, 560, 1500, 2650),
                   (138, 430, 1180, 2400), (156, 620, 1700, 2800),
                   (171, 500, 1420, 2550), (184, 660, 1850, 2950),
                   (198, 540, 1550, 2700), (212, 700, 1950, 3050),
                   (96, 450, 1250, 2450), (232, 720, 2050, 3150),
                   (147, 590, 1620, 2750), (164, 470, 1280, 2380),
                   (189, 640, 1780, 2900), (220, 680, 1880, 3000),
                   (112, 515, 1450, 2600)]
    for f0, F1, F2, F3 in voice_specs:
        sets = []
        for _ in range(3):
            sets.append((F1 * rng.uniform(0.86, 1.14),
                         F2 * rng.uniform(0.90, 1.10),
                         F3 * rng.uniform(0.92, 1.08)))
        v = crowd_voice(n, f0 * rng.uniform(0.94, 1.06), sets)
        mix += v * rng.uniform(0.35, 0.85)

    # 脚步：大约每秒 1.6 步，分散在人群里
    for _ in range(int(L * 1.6)):
        place_wrap(mix, stone_step() * rng.uniform(0.05, 0.12), rng.uniform(0, L + tail))
    # 两三声轻咳
    for at in (5.7, 13.9, 21.2):
        place_wrap(mix, cough() * rng.uniform(0.10, 0.16), at + rng.uniform(-0.4, 0.4))
    # 远处椅子/画框轻碰
    for at in (9.4, 18.3):
        m = int(0.32 * SR)
        x = noise_band(m, 300, 1600) * np.exp(-np.arange(m) / SR / 0.09)
        place_wrap(mix, x * 0.05, at)

    mix = fft_filter(mix, lambda f: hp(f, 65) * lp(f, 5400))
    mix = reverb(mix, 1.8, 0.30, damp=3800)

    # 等功率交叉淡化：尾巴卷回开头，保证 24 s 循环接缝连续
    T = int(2.5 * SR)
    out = mix[: int(L * SR)].copy()
    w = np.linspace(0, 1, T)
    fi, fo = np.sin(np.pi / 2 * w), np.cos(np.pi / 2 * w)
    out[:T] = mix[:T] * fi + mix[int(L * SR): int(L * SR) + T] * fo
    write("CrowdMurmur", out, 0.55)


# ---------------------------------------------------------------- 环境底噪
def periodic_noise(n, shape):
    X = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    y = np.fft.irfft(X * shape(f), n)
    return y / max(1e-9, np.std(y))


def room_tone(power_out=False, L=40.0):
    n = int(L * SR)
    t = np.arange(n) / SR
    two_pi = 2 * np.pi

    # 低频：风管 / 建筑体本身的低频，两种略微不同的呼吸
    rumble = periodic_noise(n, lambda f: lp(f, 95, 2) * hp(f, 18, 2)
                            * (1 + 0.8 * peak(f, 52, 3, 1.0)))
    rumble *= 0.90 + 0.10 * np.sin(two_pi * 2 * t / L + 0.7)

    # 极轻的气流：600 Hz–5 kHz，缓慢起伏
    air = periodic_noise(n, lambda f: bp(f, 230, 5200, 1) * (1 / np.sqrt(np.maximum(f, 100))))
    air *= 0.55 + 0.45 * np.sin(two_pi * 3 * t / L + 2.1)

    if power_out:
        # 断电：没有 50 Hz，只剩更低的余响和风管
        sub = periodic_noise(n, lambda f: lp(f, 46, 2) * hp(f, 14, 2)
                             * (1 + 1.2 * peak(f, 33, 2.5, 1.0)))
        layer = sub * 1.15 + rumble * 0.55 + air * 0.38
        layer *= 0.9 + 0.1 * np.sin(two_pi * 1 * t / L)
        layer = circ_reverb(layer, 2.4, 0.16, damp=2200) * 0.55

        # 远处水滴（很轻，两次）
        for at, fc in ((9.6, 1150), (28.4, 880)):
            m = int(0.55 * SR)
            tt = np.arange(m) / SR
            drop = np.sin(two_pi * (fc * tt - 260 * tt ** 2)) * np.exp(-tt / 0.10)
            drop += noise_band(m, 1500, 6000) * np.exp(-tt / 0.012) * 0.25
            place_wrap(layer, drop * 0.020, at)
        # 远处金属/管道轻响
        m = int(0.9 * SR)
        tt = np.arange(m) / SR
        clang = sum(np.sin(two_pi * fc * tt) * np.exp(-tt / d) * a
                    for fc, d, a in ((430, 0.5, 0.5), (712, 0.35, 0.3), (1280, 0.2, 0.18)))
        place_wrap(layer, clang * 0.012, 33.2)
        write("AmbientRoomTone_PowerOut", layer, 0.42)
        return

    layer = rumble * 1.0 + air * 0.28

    # 50 Hz 电流嗡声 + 谐波（频率都是 1/40 s 的整数倍，循环严格对齐）
    hum = np.zeros(n)
    for k, amp in ((1, 0.050), (2, 0.024), (3, 0.011), (5, 0.005), (7, 0.003)):
        hum += amp * np.sin(two_pi * 50 * k * t + rng.uniform(0, two_pi))
    hum *= 1 + 0.22 * np.sin(two_pi * 3 * t / L)
    layer += hum

    # 极轻的一次建筑热胀冷缩（20 s 附近）
    m = int(0.7 * SR)
    tt = np.arange(m) / SR
    creak = noise_band(m, 220, 900) * np.minimum(1, tt / 0.12) * np.exp(-tt / 0.22)
    place_wrap(layer, creak * 0.018, 19.8)

    layer = circ_reverb(layer, 2.0, 0.12, damp=2400)
    write("AmbientRoomTone", layer, 0.50)


# ---------------------------------------------------------------- 厕所异响
def toilet():
    dur = 3.2
    n = int((dur + 0.8) * SR)
    buf = np.zeros(n)

    def impact(at, amp, bright=1.0):
        """瓷器/金属撞击：一串快速衰减的非谐模态 + 宽带噪声，不做成"钟声"。"""
        m = int(0.55 * SR)
        tt = np.arange(m) / SR
        x = np.zeros(m)
        for _ in range(9):
            fc = rng.uniform(700, 6200) * bright
            dec = rng.uniform(0.035, 0.20)
            a = rng.uniform(0.06, 0.26) * (900 / fc) ** 0.35
            # 轻微下滑，避免纯正弦的"音高感"
            bend = rng.uniform(-0.02, 0.005)
            x += a * np.sin(2 * np.pi * fc * (1 + bend * tt) * tt + rng.uniform(0, 2 * np.pi)) * np.exp(-tt / dec)
        x += noise_band(m, 1200, 9500) * np.exp(-tt / 0.010) * 0.55
        x += noise_band(m, 300, 1400) * np.exp(-tt / 0.025) * 0.22
        x += np.sin(2 * np.pi * (98 * tt - 40 * tt ** 2)) * np.exp(-tt / 0.045) * 0.45
        # 碎片/弹跳的小碎响
        for dt in (0.035, 0.075, 0.14):
            k = int(dt * SR)
            mm = m - k
            if mm > 0:
                x[k:] += noise_band(mm, 2500, 10000) * np.exp(-np.arange(mm) / SR / 0.008) * 0.10 * rng.uniform(0.4, 1)
        place_wrap(buf, x * amp, at)

    # 一下大、两下小：像架子上的瓷罐被碰掉，弹了两下再碎响
    impact(0.03, 0.62, 1.00)
    impact(0.21, 0.34, 1.10)
    impact(0.37, 0.20, 0.90)
    impact(0.55, 0.10, 1.18)

    # 闷响：撞到隔板
    m = int(0.8 * SR)
    tt = np.arange(m) / SR
    thud = np.sin(2 * np.pi * (72 * tt - 30 * tt ** 2)) * np.exp(-tt / 0.16)
    thud += noise_band(m, 40, 260) * np.exp(-tt / 0.08) * 0.5
    place_wrap(buf, thud * 0.55, 0.52)

    # 水管咕噜：一串气泡，音高上滑、很短
    for _ in range(14):
        at = rng.uniform(0.92, 2.55)
        m = int(0.11 * SR)
        tt = np.arange(m) / SR
        f0 = rng.uniform(170, 420)
        sweep = rng.uniform(260, 700)
        bub = np.sin(2 * np.pi * (f0 * tt + 0.5 * sweep * tt ** 2)) * np.exp(-tt / 0.030)
        bub *= np.minimum(1, tt / 0.004)
        place_wrap(buf, bub * rng.uniform(0.08, 0.22), at)
    # 管内流水
    m = int(1.9 * SR)
    tt = np.arange(m) / SR
    water = noise_band(m, 260, 1600) * (0.4 + 0.6 * np.abs(np.sin(2 * np.pi * 3.2 * tt)))
    water *= np.minimum(1, tt / 0.25) * np.minimum(1, (1.9 - tt) / 0.4)
    place_wrap(buf, water * 0.10, 0.95)
    # 管道低频涌动
    m = int(1.8 * SR)
    tt = np.arange(m) / SR
    pipe = np.sin(2 * np.pi * (58 * tt + 6 * np.sin(2 * np.pi * 1.3 * tt))) * 0.25
    pipe *= np.minimum(1, tt / 0.3) * np.minimum(1, (1.8 - tt) / 0.5)
    place_wrap(buf, pipe * 0.18, 1.0)

    out = reverb(buf, 0.85, 0.34, damp=4200)
    out = fade(out[: int(dur * SR)], 0.003, 0.35)
    write("ToiletNoise", out, 0.85)


crowd()
room_tone(False)
room_tone(True)
toilet()
print("ok")
