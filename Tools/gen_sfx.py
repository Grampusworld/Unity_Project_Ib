"""Synthesize Ib sound effects as 16-bit mono WAVs (no numpy)."""
import math
import random
import struct
import sys
import wave

SR = 44100
OUT = sys.argv[1]
random.seed(7)


def write(name, samples):
    peak = max(1e-6, max(abs(s) for s in samples))
    scale = 0.89 / peak
    with wave.open(f"{OUT}/{name}.wav", "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767)) for s in samples))


def silence(sec):
    return [0.0] * int(sec * SR)


def lowpass(x, cutoff):
    a = 1 - math.exp(-2 * math.pi * cutoff / SR)
    y, out = 0.0, []
    for s in x:
        y += a * (s - y)
        out.append(y)
    return out


def highpass(x, cutoff):
    lp = lowpass(x, cutoff)
    return [a - b for a, b in zip(x, lp)]


# ---- old electro-mechanical bell: hammer at 20 Hz between two bells ----
def bell_burst(sec):
    n = int(sec * SR)
    partials = [(1180, 1.0), (1415, 0.8), (2950, 0.35), (3540, 0.25), (5100, 0.12)]
    out = []
    strike_period = SR // 20
    for i in range(n):
        t = i / SR
        k = i % strike_period
        env = math.exp(-k / SR * 28) * 0.7 + 0.3  # each strike re-excites, ring sustains
        side = (i // strike_period) % 2           # alternate bells
        v = 0.0
        for f, a in partials:
            f2 = f * (1.0 if side == 0 else 1.06)
            v += a * math.sin(2 * math.pi * f2 * t)
        click = (random.uniform(-1, 1) * math.exp(-k / SR * 900)) * 0.6
        out.append((v * env + click) * 0.35)
    # 10 ms fades
    f = int(0.01 * SR)
    for i in range(f):
        out[i] *= i / f
        out[-1 - i] *= i / f
    return out


ring = bell_burst(0.4) + silence(0.2) + bell_burst(0.4) + silence(2.0)
write("PhoneRing", ring)

# ---- line hiss: quiet band-limited noise + faint hum, then hang-up click ----
n = int(4.0 * SR)
noise = [random.uniform(-1, 1) for _ in range(n)]
hiss = lowpass(highpass(noise, 300), 3400)
out = []
for i, s in enumerate(hiss):
    t = i / SR
    env = min(1, t / 0.3) * min(1, (4.0 - t) / 0.4)
    hum = 0.05 * math.sin(2 * math.pi * 50 * t) + 0.02 * math.sin(2 * math.pi * 150 * t)
    crackle = (random.uniform(-1, 1) * 0.8) if random.random() < 0.0004 else 0.0
    out.append((s * 0.6 + hum + crackle) * env * 0.5)
click = [random.uniform(-1, 1) * math.exp(-i / SR * 400) for i in range(int(0.08 * SR))]
write("LineHiss", out + silence(0.05) + [c * 0.9 for c in click] + silence(0.2))

# ---- power clunk: breaker thud + metallic release click ----
n = int(1.0 * SR)
out = []
for i in range(n):
    t = i / SR
    thud = math.sin(2 * math.pi * 55 * t) * math.exp(-t * 9) + 0.5 * math.sin(2 * math.pi * 110 * t) * math.exp(-t * 14)
    out.append(thud)
burst = lowpass([random.uniform(-1, 1) for _ in range(n)], 900)
out = [o + b * math.exp(-i / SR * 25) * 1.5 for i, (o, b) in enumerate(zip(out, burst))]
metal = [0.0] * n
start = int(0.03 * SR)
for i in range(n - start):
    t = i / SR
    metal[start + i] = (math.sin(2 * math.pi * 2300 * t) + 0.6 * math.sin(2 * math.pi * 3700 * t)) * math.exp(-t * 60) * 0.5
write("PowerClunk", [a + b for a, b in zip(out, metal)])


# ---- small metallic transient ----
def tick(freqs, decay, noise_amt, sec=0.12):
    n = int(sec * SR)
    res = []
    for i in range(n):
        t = i / SR
        v = sum(a * math.sin(2 * math.pi * f * t) for f, a in freqs) * math.exp(-t * decay)
        v += random.uniform(-1, 1) * noise_amt * math.exp(-t * 700)
        res.append(v)
    return res


def place(buf, clip, at):
    s = int(at * SR)
    for i, c in enumerate(clip):
        if s + i < len(buf):
            buf[s + i] += c


# key: slide-in scrape, click, turn click
buf = [0.0] * int(0.5 * SR)
scrape = highpass([random.uniform(-1, 1) for _ in range(int(0.12 * SR))], 2500)
place(buf, [s * 0.25 * math.sin(math.pi * i / len(scrape)) for i, s in enumerate(scrape)], 0.0)
place(buf, tick([(3200, 1), (5400, 0.6)], 90, 0.8), 0.13)
place(buf, tick([(2600, 1), (4100, 0.7)], 70, 0.6), 0.30)
write("KeyClick", buf)

# locked door rattle: handle jiggle against latch, three knocks + low thump
buf = [0.0] * int(0.7 * SR)
for k, at in enumerate([0.0, 0.11, 0.2, 0.34]):
    place(buf, tick([(1700 + 150 * k, 1), (2900, 0.5), (420, 0.8)], 45, 1.0, 0.2), at)
thump = [math.sin(2 * math.pi * 90 * i / SR) * math.exp(-i / SR * 20) for i in range(int(0.3 * SR))]
place(buf, thump, 0.34)
write("LockRattle", buf)

# stone scrape: heavy statue shifting on a stone floor, low grinding noise swelling and dying out
n = int(1.6 * SR)
grind = lowpass([random.uniform(-1, 1) for _ in range(n)], 420)
grit = highpass([random.uniform(-1, 1) for _ in range(n)], 1800)
buf = []
for i in range(n):
    t = i / SR
    env = math.sin(math.pi * min(1.0, t / 1.4)) ** 1.5 if t < 1.4 else 0.0
    wobble = 0.7 + 0.3 * math.sin(2 * math.pi * 7 * t + math.sin(2 * math.pi * 1.3 * t))
    buf.append((grind[i] * 2.2 + grit[i] * 0.25 * wobble) * env)
thud = [math.sin(2 * math.pi * 48 * i / SR) * math.exp(-i / SR * 12) * 0.8 for i in range(int(0.5 * SR))]
place(buf, thud, 1.25)
write("StoneScrape", buf)

# ---- angel scream: jump scare, ~1.5 s. 起音低频重击 + 噪声爆破，多层失谐锯齿/方波
# 尖啸快速上滑带颤音，硬削波失真，0.9 s 后尾部快速衰减。必须放在最后：前面的
# 音效都依赖 random.seed(7) 的固定消耗顺序，动了会把已有 wav 全改掉。
def angel_scream():
    n = int(1.5 * SR)
    out = []
    for i in range(n):
        t = i / SR
        out.append(math.sin(2 * math.pi * 42 * t) * math.exp(-t * 14) * 1.5)
    burst = lowpass([random.uniform(-1, 1) for _ in range(n)], 2400)
    out = [o + b * math.exp(-i / SR * 22) * 1.7 for i, (o, b) in enumerate(zip(out, burst))]

    layers = [(620.0, 1.0), (661.0, 0.8), (935.0, 0.6), (1245.0, 0.45)]
    phases = [0.0] * len(layers)
    for i in range(n):
        t = i / SR
        v = 0.0
        for j, (f, a) in enumerate(layers):
            glide = f * (1.0 + 1.1 * min(1.0, t / 0.35))                   # 半秒内蹿到两倍多
            vib = 1.0 + 0.035 * math.sin(2 * math.pi * (11 + 2 * j) * t)   # 颤音
            phases[j] += 2 * math.pi * glide * vib / SR
            saw = 2.0 * ((phases[j] / (2 * math.pi)) % 1.0) - 1.0
            square = 1.0 if math.sin(phases[j]) >= 0.0 else -1.0
            v += a * (0.6 * saw + 0.4 * square)
        env = min(1.0, t / 0.015) * math.exp(-max(0.0, t - 0.9) * 9.0)
        out[i] += v * env * 0.24

    # 硬削波失真
    return [max(-0.8, min(0.8, s * 2.4)) for s in out]


write("AngelScream", angel_scream())


# ---- paper flip: picking up / turning a dry old document, 0.4-0.8s ----
# Appended last and after AngelScream so it cannot shift the random-draw
# order the earlier effects rely on (see note above angel_scream).
def bandpass(x, lo, hi):
    return highpass(lowpass(x, hi), lo)


def paper_flip(sec, variant):
    n = int(sec * SR)
    body = bandpass([random.uniform(-1, 1) for _ in range(n)], 900, 5200)
    crinkle = bandpass([random.uniform(-1, 1) for _ in range(n)], 2500, 9000)
    out = []
    for i in range(n):
        t = i / SR
        attack = min(1.0, t / 0.02)
        hump1 = math.exp(-((t - 0.12) ** 2) / (2 * 0.05 ** 2))
        hump2 = math.exp(-((t - sec * 0.5) ** 2) / (2 * 0.07 ** 2))
        sustain = 0.35 + 0.55 * hump1 + 0.45 * hump2
        tail = math.exp(-max(0.0, t - sec * 0.65) * 6)
        env = attack * sustain * tail
        crinkle_env = env * (0.6 + 0.4 * math.sin(2 * math.pi * 35 * t + variant))
        out.append(body[i] * 0.6 * env + crinkle[i] * 0.5 * crinkle_env)
    f = int(0.005 * SR)
    for i in range(f):
        out[i] *= i / f
        out[-1 - i] *= i / f
    return out


write("PaperFlip", paper_flip(0.55, 0))
write("PaperFlip_0", paper_flip(0.45, 1))
write("PaperFlip_1", paper_flip(0.6, 2))
write("PaperFlip_2", paper_flip(0.75, 3))


# ---- duty-room door slam: heavy wooden leaf hits the frame, ~0.8 s ----
# 同样接在最后：前面所有 wav 都依赖 random.seed(7) 的固定消耗顺序。
def door_slam():
    buf = [0.0] * int(0.8 * SR)
    # 低频冲击
    for i in range(int(0.55 * SR)):
        t = i / SR
        buf[i] += math.sin(2 * math.pi * 52 * t) * math.exp(-t * 15) * 1.5
        buf[i] += math.sin(2 * math.pi * 97 * t) * math.exp(-t * 22) * 0.7
    # 撞上去那一下的木头噪声
    thump = lowpass([random.uniform(-1, 1) for _ in range(int(0.3 * SR))], 1100)
    place(buf, [s * math.exp(-i / SR * 28) * 1.6 for i, s in enumerate(thump)], 0.0)
    # 短混响尾：门框和走廊的几次反射
    for k, at in enumerate([0.04, 0.085, 0.14]):
        tail = lowpass([random.uniform(-1, 1) for _ in range(int(0.2 * SR))], 650)
        place(buf, [s * math.exp(-i / SR * 20) * (0.4 / (k + 1)) for i, s in enumerate(tail)], at)
    # 门框震颤：几下很快衰减的小响
    rattle = [random.uniform(-1, 1) for _ in range(int(0.12 * SR))]
    for k, at in enumerate([0.02, 0.055, 0.1]):
        place(buf, [r * math.exp(-i / SR * (90 + 30 * k)) * 0.5 for i, r in enumerate(rattle)], at)
    f = int(0.004 * SR)
    for i in range(f):
        buf[i] *= i / f
        buf[-1 - i] *= i / f
    return buf


write("DoorSlam", door_slam())


# ---- duty-room door creak: old wooden hinges, ~2.5 s ----
def door_creak():
    n = int(2.5 * SR)
    grit = bandpass([random.uniform(-1, 1) for _ in range(n)], 700, 3800)
    buf = []
    for i in range(n):
        t = i / SR
        env = min(1.0, t / 0.25) * min(1.0, (2.5 - t) / 0.35)
        slow = 0.5 + 0.5 * math.sin(2 * math.pi * 0.55 * t + 0.7)
        # 粘滑摩擦：一紧一松的断续感，频率慢慢变低
        stick = 0.55 + 0.45 * math.sin(2 * math.pi * (24 - 4 * t) * t)
        buf.append(grit[i] * env * slow * stick * 1.1)

    # 合页的高音"吱"：音高缓慢游走，只在用力的那半段响
    phase = 0.0
    for i in range(n):
        t = i / SR
        freq = 760 + 220 * math.sin(2 * math.pi * 0.33 * t) + 70 * math.sin(2 * math.pi * 1.9 * t)
        phase += 2 * math.pi * freq / SR
        env = min(1.0, t / 0.2) * min(1.0, (2.5 - t) / 0.5)
        gate = max(0.0, math.sin(2 * math.pi * 0.55 * t + 0.7))
        buf[i] += math.sin(phase) * env * gate * 0.22
    return buf


write("DoorCreak", door_creak())

print("ok")
