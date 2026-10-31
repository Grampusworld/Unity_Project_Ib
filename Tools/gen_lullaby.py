"""Synthesize the main-menu music box: Brahms, Wiegenlied Op. 49 No. 4 (public domain).

Music-box timbre: a struck comb tooth = a few slightly inharmonic sine partials with fast
exponential decay, plus a tiny click. Quiet bass on the downbeats, a simple feedback-delay
room, 16-bit mono WAV. Usage: python3 -I gen_lullaby.py <out_dir>
"""
import math
import random
import struct
import sys
import wave

SR = 32000
OUT = sys.argv[1]
random.seed(3)

EIGHTH = 0.34   # slow, a little uneven feel comes from the music box itself
A4 = 440.0
NOTE = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}


def freq(name):
    letter, octave = name[0], int(name[1:])
    return A4 * 2 ** ((NOTE[letter] + 12 * (octave - 4)) / 12)


# (note, length in eighths). Transposed up two octaves at render time, like a real music box.
MELODY = [
    ("E4", 1), ("E4", 1), ("G4", 4), ("E4", 1), ("E4", 1), ("G4", 4),
    ("E4", 1), ("G4", 1), ("C5", 2), ("B4", 3), ("A4", 1), ("A4", 2), ("G4", 2),
    ("D4", 1), ("E4", 1), ("F4", 2), ("D4", 2), ("D4", 1), ("E4", 1), ("F4", 4),
    ("D4", 1), ("F4", 1), ("B4", 1), ("A4", 1), ("G4", 2), ("B4", 2), ("C5", 6),
    ("C4", 1), ("C4", 1), ("C5", 4), ("A4", 1), ("F4", 1), ("G4", 4),
    ("E4", 1), ("C4", 1), ("F4", 2), ("G4", 2), ("A4", 2), ("G4", 4),
    ("C4", 1), ("C4", 1), ("C5", 4), ("A4", 1), ("F4", 1), ("G4", 4),
    ("E4", 1), ("C4", 1), ("F4", 2), ("E4", 2), ("D4", 2), ("C4", 6),
]

# bass on bar downbeats (every 6 eighths after the 2-eighth pickup)
BASS = ["C3", "C3", "G2", "G2", "G2", "C3", "C3", "F2", "C3", "C3", "F2", "C3", "C3", "F2", "G2", "C3"]


def tooth(f, amp, length):
    n = int(length * SR)
    partials = [(1.0, 1.0, 3.2), (2.0, 0.35, 6.0), (3.01, 0.12, 9.0), (4.17, 0.06, 14.0)]
    detune = 1.0 + random.uniform(-0.002, 0.002)
    out = []
    for i in range(n):
        t = i / SR
        v = 0.0
        for mult, a, decay in partials:
            v += a * math.sin(2 * math.pi * f * mult * detune * t) * math.exp(-t * decay)
        if i < 60:
            v += random.uniform(-1, 1) * 0.3 * (1 - i / 60)
        out.append(v * amp)
    return out


total = sum(d for _, d in MELODY) * EIGHTH + 2.5
buf = [0.0] * int(total * SR)


def place(clip, at):
    s = int(at * SR)
    for i, c in enumerate(clip):
        if s + i < len(buf):
            buf[s + i] += c


t = 0.0
for name, length in MELODY:
    jitter = random.uniform(-0.012, 0.012)
    place(tooth(freq(name) * 4, 0.55, 2.2), max(0.0, t + jitter))
    t += length * EIGHTH

bar = 6 * EIGHTH
for k, name in enumerate(BASS):
    place(tooth(freq(name) * 4, 0.22, 2.6), 2 * EIGHTH + k * bar)

# small room: two feedback delays
for delay, gain in ((0.113, 0.28), (0.171, 0.2)):
    d = int(delay * SR)
    for i in range(d, len(buf)):
        buf[i] += buf[i - d] * gain

# gentle low-pass to take the edge off
a = 1 - math.exp(-2 * math.pi * 5200 / SR)
y = 0.0
for i, s in enumerate(buf):
    y += a * (s - y)
    buf[i] = y

peak = max(abs(s) for s in buf)
with wave.open(f"{OUT}/MenuLullaby.wav", "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(b"".join(struct.pack("<h", int(s / peak * 0.8 * 32767)) for s in buf))
print("ok", round(total, 1), "s")
