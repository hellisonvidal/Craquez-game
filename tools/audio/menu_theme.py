"""Trilha sonora original dos menus do Craquez (composta e sintetizada por código).

Eletrônico com percussão de samba, 124 BPM, em lá menor (Am - F - C - G / Dm - F - G - E).
Estrutura: intro (8 compassos) -> A (16) -> B (16) -> A' (16) -> ponte (8), em loop sem emenda.
uso: python3 tools/audio/menu_theme.py saida.wav  (depois ffmpeg -> .ogg)
"""
import sys, numpy as np

SR = 44100
BPM = 124.0
BEAT = 60.0 / BPM
BAR = BEAT * 4
rng = np.random.default_rng(2026)

def note(n):  # MIDI -> Hz
    return 440.0 * 2 ** ((n - 69) / 12.0)

def env(n, a, d, s, r, total):
    t = np.arange(n) / SR
    e = np.ones(n) * s
    ai = int(a * SR); di = int(d * SR); ri = int(r * SR)
    if ai > 0: e[:ai] = np.linspace(0, 1, ai)
    if di > 0: e[ai:ai + di] = np.linspace(1, s, min(di, max(0, n - ai)))[: max(0, min(di, n - ai))]
    rel = max(0, int(total * SR) - ri)
    if rel < n: e[rel:] *= np.linspace(1, 0, n - rel)
    return e

def osc_saw(f, n, detune=0.0):
    t = np.arange(n) / SR
    out = np.zeros(n)
    for d in (-detune, 0, detune):
        ph = (t * f * (1 + d)) % 1.0
        out += 2 * ph - 1
    return out / 3

def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.zeros_like(x); acc = 0.0
    for i in range(len(x)):
        acc = (1 - a) * x[i] + a * acc
        y[i] = acc
    return y

def lp_fast(x, cutoff):
    # filtro de um polo vetorizado em blocos (rápido o bastante para o arquivo todo)
    from scipy.signal import lfilter
    a = np.exp(-2 * np.pi * cutoff / SR)
    return lfilter([1 - a], [1, -a], x)

try:
    import scipy  # noqa
    LP = lp_fast
except ImportError:
    LP = lowpass

PROG_A = [[57, 60, 64], [53, 57, 60], [48, 52, 55], [55, 59, 62]]          # Am F C G
PROG_B = [[50, 53, 57], [53, 57, 60], [55, 59, 62], [52, 56, 59]]          # Dm F G E
ROOT_A = [45, 41, 36, 43]
ROOT_B = [38, 41, 43, 40]
SECTIONS = [("intro", 8), ("A", 16), ("B", 16), ("A2", 16), ("ponte", 8)]
total_bars = sum(b for _, b in SECTIONS)
N = int(total_bars * BAR * SR)
L = np.zeros(N); R = np.zeros(N)

def add(sig, start, pan=0.0, gain=1.0):
    i = int(start * SR)
    if i >= N: return
    sig = sig[: N - i] * gain
    L[i:i + len(sig)] += sig * (1 - max(0, pan))
    R[i:i + len(sig)] += sig * (1 + min(0, pan))

# ---- bateria
def kick():
    n = int(0.35 * SR); t = np.arange(n) / SR
    f = 50 + 110 * np.exp(-t * 35)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9) * 0.5
def clap():
    n = int(0.25 * SR); t = np.arange(n) / SR
    nz = rng.standard_normal(n)
    e = np.exp(-t * 22) + 0.6 * np.exp(-((t - 0.012) ** 2) / 1e-5) + 0.5 * np.exp(-((t - 0.024) ** 2) / 1e-5)
    return (nz - LP(nz, 900)) * e * 0.55
def hat(open_=False):
    n = int((0.22 if open_ else 0.05) * SR); t = np.arange(n) / SR
    nz = rng.standard_normal(n)
    return (nz - LP(nz, 7000)) * np.exp(-t * (12 if open_ else 70)) * 0.18
def surdo(low=True):
    n = int(0.5 * SR); t = np.arange(n) / SR
    f = (62 if low else 82) * (1 + 0.3 * np.exp(-t * 30))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 6) * 0.22
def tamborim():
    n = int(0.08 * SR); t = np.arange(n) / SR
    return np.sin(2 * np.pi * 720 * t) * np.exp(-t * 60) * 0.22 + rng.standard_normal(n) * np.exp(-t * 90) * 0.05
def shaker():
    n = int(0.07 * SR); t = np.arange(n) / SR
    nz = rng.standard_normal(n)
    return (nz - LP(nz, 5000)) * np.sin(np.pi * t / 0.07) * 0.07

K, C, H, HO, SL, SH, TB, SK = kick(), clap(), hat(), hat(True), surdo(True), surdo(False), tamborim(), shaker()
TAMB_PAT = [1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1, 0, 1]   # teleco-teco

bar_t = 0.0
for name, bars in SECTIONS:
    for b in range(bars):
        t0 = bar_t + b * BAR
        full = name != "intro" or b >= 4
        for beat in range(4):
            tb = t0 + beat * BEAT
            if full and name != "ponte": add(K, tb, 0, 0.9)
            if full and beat in (1, 3): add(C, tb, 0.1, 0.8)
            add(H, tb + BEAT * 0.5, 0.3, 0.7 if full else 0.4)
            if beat == 3 and b % 2 == 1: add(HO, tb + BEAT * 0.5, 0.3, 0.6)
            # surdos: marcação (2º tempo forte) e resposta
            if name in ("A", "A2", "B", "ponte"):
                add(SH if beat % 2 == 0 else SL, tb, -0.15, 0.7 if beat % 2 else 0.45)
        for s16 in range(16):
            ts = t0 + s16 * BEAT / 4
            add(SK, ts + (0.006 if s16 % 2 else 0), -0.4, 0.8 if full else 0.4)
            if name in ("B", "A2") and TAMB_PAT[s16]: add(TB, ts, 0.45, 0.8)
    bar_t += bars * BAR

# ---- harmonia, baixo e melodia
def pad(chord, dur):
    n = int(dur * SR); out = np.zeros(n)
    for m in chord:
        out += osc_saw(note(m), n, 0.004) + 0.5 * osc_saw(note(m + 12), n, 0.006)
    out = LP(out, 2600) * env(n, 0.25, 0.4, 0.7, 0.3, dur)
    return out * 0.2
def bass(root, dur, pattern):
    out = np.zeros(int(dur * SR))
    for (pos, length, off) in pattern:
        n = int(length * BEAT * SR)
        f = note(root + off)
        t = np.arange(n) / SR
        s = np.sign(np.sin(2 * np.pi * f * t)) * 0.5 + np.sin(2 * np.pi * f * t)
        s = (0.6 * LP(s, 1400) + 0.4 * LP(s, 500)) * env(n, 0.005, 0.1, 0.7, 0.04, length * BEAT)
        i = int(pos * BEAT * SR)
        out[i:i + n] += s[: len(out) - i]
    return out * 0.13
BASS_PAT = [(0, 0.75, 0), (0.75, 0.25, 12), (1.5, 0.5, 0), (2, 0.75, 7), (2.75, 0.25, 12), (3.5, 0.5, 10)]
def lead(m, dur, vib=True):
    n = int(dur * SR); t = np.arange(n) / SR
    f = note(m) * (1 + (0.004 * np.sin(2 * np.pi * 5.5 * t) * np.clip(t / 0.2 - 0.5, 0, 1) if vib else 0))
    ph = np.cumsum(f) / SR
    s = 0.6 * (2 * (ph % 1) - 1) + 0.4 * np.sign(np.sin(2 * np.pi * ph))
    return LP(s, 3200) * env(n, 0.01, 0.12, 0.6, 0.08, dur) * 0.34
# gancho (motivo de 2 compassos) e variação
HOOK = [(0, 0.5, 76), (0.5, 0.5, 74), (1, 1, 72), (2, 0.5, 74), (2.5, 0.5, 76), (3, 1, 79),
        (4, 0.5, 77), (4.5, 0.5, 76), (5, 0.5, 74), (5.5, 0.5, 72), (6, 1.5, 74), (7.5, 0.5, 71)]
HOOK_B = [(0, 1, 74), (1, 0.5, 77), (1.5, 0.5, 81), (2, 1.5, 79), (3.5, 0.5, 77),
          (4, 0.5, 76), (4.5, 0.5, 77), (5, 0.5, 79), (5.5, 0.5, 76), (6, 2, 76)]

bar_t = 0.0
for name, bars in SECTIONS:
    prog, roots = (PROG_B, ROOT_B) if name == "B" else (PROG_A, ROOT_A)
    for b in range(bars):
        t0 = bar_t + b * BAR
        ch = prog[b % 4]; rt = roots[b % 4]
        add(pad(ch, BAR), t0, 0.0, 1.0 if name != "intro" else 0.7)
        if name != "intro" or b >= 4: add(bass(rt, BAR, BASS_PAT), t0, 0.0)
        # arpejo em semicolcheias (na ponte e no B)
        if name in ("B", "ponte", "A2"):
            arp = ch + [ch[0] + 12]
            for s16 in range(16):
                m = arp[(s16 * 3) % len(arp)] + 12
                add(lead(m, BEAT / 4 * 0.9, False) * 0.55, t0 + s16 * BEAT / 4, 0.35 if s16 % 2 else -0.35)
        if name in ("A", "A2", "B") and b % 2 == 0:
            for (pos, ln, m) in (HOOK_B if name == "B" else HOOK):
                add(lead(m, ln * BEAT * 0.95), t0 + pos * BEAT, -0.05)
                add(lead(m - 12, ln * BEAT * 0.95) * 0.35, t0 + pos * BEAT, 0.1)
    bar_t += bars * BAR

# ---- mixagem: eco curto, compressão suave e loop sem emenda
def delay(x, t, fb, mix):
    d = int(t * SR); y = x.copy()
    for k in range(1, 4):
        y[d * k:] += x[: -d * k] * (fb ** k) * mix
    return y
L = delay(L, BEAT * 0.75, 0.35, 0.6); R = delay(R, BEAT * 0.5, 0.35, 0.6)
mix = np.stack([L, R], 1)
mix = np.tanh(mix * 1.6) / np.tanh(1.6)
mix /= np.max(np.abs(mix)) * 1.05
# emenda: o fim da ponte entra no começo (cauda do eco mistura com o início)
fade = int(0.03 * SR)
mix[:fade] *= np.linspace(0, 1, fade)[:, None]
mix[-fade:] *= np.linspace(1, 0, fade)[:, None]
import wave
w = wave.open(sys.argv[1], "wb"); w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
w.writeframes((mix * 32767).astype(np.int16).tobytes()); w.close()
print("duração %.1f s" % (N / SR))
