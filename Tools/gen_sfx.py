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
print("ok")
