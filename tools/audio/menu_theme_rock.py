"""Trilha de abertura/menus do Craquez: rock pesado original, composto e sintetizado por código.

Mi menor, 150 BPM. Guitarras distorcidas (Karplus-Strong + saturação + "caixa" de guitarra),
riff em palm mute, power chords abertos no refrão, baixo dobrando o riff, bateria (bumbo duplo
nas viradas, caixa, chimbal, prato) e uma guitarra solo no refrão. Loop sem emenda.
Estrutura: intro (4) -> riff (16) -> refrão (16) -> quebra (8) -> riff (16) -> refrão (16).
uso: python3 tools/audio/menu_theme_rock.py saida.wav  (depois ffmpeg -> .ogg)
"""
import sys
import numpy as np
from scipy.signal import lfilter, butter, sosfilt

SR = 44100
BPM = 150.0
BEAT = 60.0 / BPM
BAR = BEAT * 4
EIGHTH = BEAT / 2
SIX = BEAT / 4
rng = np.random.default_rng(1970)


def hz(n):
    return 440.0 * 2 ** ((n - 69) / 12.0)


def sos(kind, f, order=2):
    return butter(order, f, btype=kind, fs=SR, output="sos")


# ------------------------------------------------------------------ cordas (Karplus-Strong)
_cache = {}


def string(n, secs=3.8, bright=0.5):
    secs = 3.8  # um tamanho só no cache (cobre a nota mais longa)
    key = (n, bright)
    if key in _cache:
        return _cache[key]
    f = hz(n)
    N = int(SR / f)
    L = int(secs * SR)
    buf = rng.uniform(-1, 1, N)
    buf = lfilter([1 - bright], [1, -bright], buf)  # palheta: ataque mais ou menos brilhante
    out = np.zeros(L)
    # KS vetorizado por blocos de N amostras
    prev = buf.copy()
    pos = 0
    decay = 0.996
    while pos < L:
        m = min(N, L - pos)
        out[pos:pos + m] = prev[:m]
        nxt = decay * 0.5 * (prev + np.roll(prev, -1))
        prev = nxt
        pos += m
    _cache[key] = out
    return out


def power_chord(root, secs, mute=False, oct_up=True):
    notes = [root, root + 7] + ([root + 12] if oct_up else [])
    L = int(secs * SR)
    x = np.zeros(L)
    for i, n in enumerate(notes):
        s = string(n, max(secs, 0.3) + 0.1, 0.35 if mute else 0.6)[:L]
        # leve atraso entre cordas (palhetada para baixo)
        d = int(i * 0.004 * SR)
        x[d:] += s[: L - d]
    t = np.arange(L) / SR
    if mute:
        x *= np.exp(-t * 16.0)  # palm mute: morre rápido
    else:
        x *= np.minimum(1.0, np.exp(-(t - secs + 0.05).clip(0) * 40))
    x[-min(L, 200):] *= np.linspace(1, 0, min(L, 200))
    return x


def amp(x, gain=14.0, mute=False):
    # distorção: pré-ênfase, saturação assimétrica, "caixa" 4x12 (passa-banda + corte de agudos)
    x = sosfilt(sos("highpass", 110), x)
    y = np.tanh(gain * x + 0.15) - np.tanh(0.15)
    y = sosfilt(sos("lowpass", 3800 if not mute else 2600, 4), y)
    y = sosfilt(sos("highpass", 75), y)
    # realce de médios (peito da guitarra)
    mid = sosfilt(butter(2, [600, 1600], btype="bandpass", fs=SR, output="sos"), y)
    return y + 0.35 * mid


# ------------------------------------------------------------------ bateria
def kick():
    L = int(0.35 * SR)
    t = np.arange(L) / SR
    f = 45 + 90 * np.exp(-t * 35)
    ph = 2 * np.pi * np.cumsum(f) / SR
    body = np.sin(ph) * np.exp(-t * 9)
    click = rng.uniform(-1, 1, L) * np.exp(-t * 400) * 0.5
    return np.tanh((body + sosfilt(sos("highpass", 1500), click)) * 2.2) * 0.9


def snare():
    L = int(0.32 * SR)
    t = np.arange(L) / SR
    tone = np.sin(2 * np.pi * 185 * t) * np.exp(-t * 22) * 0.6
    nz = sosfilt(butter(2, [1500, 9000], btype="bandpass", fs=SR, output="sos"), rng.uniform(-1, 1, L)) * np.exp(-t * 13)
    return np.tanh((tone + nz * 1.6) * 1.8) * 0.8


def hat(open_=False):
    L = int((0.25 if open_ else 0.06) * SR)
    t = np.arange(L) / SR
    nz = sosfilt(sos("highpass", 7000, 4), rng.uniform(-1, 1, L))
    return nz * np.exp(-t * (9 if open_ else 60)) * 0.35


def crash():
    L = int(2.2 * SR)
    t = np.arange(L) / SR
    nz = sosfilt(sos("highpass", 4500, 2), rng.uniform(-1, 1, L))
    return nz * np.exp(-t * 1.6) * 0.45


def tom(f0):
    L = int(0.4 * SR)
    t = np.arange(L) / SR
    f = f0 * (1 + 0.6 * np.exp(-t * 20))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 8) * 0.8


K, S, H, HO, C = kick(), snare(), hat(), hat(True), crash()

# ------------------------------------------------------------------ composição
# riff (mi menor): E E G E A E Bb A  — palm mute com acentos abertos
RIFF = [  # (nota, duração em colcheias, mudo?)
    (40, 1, True), (40, 1, True), (43, 1, False), (40, 1, True), (40, 1, True), (45, 1, False), (40, 1, True), (46, 1, False),
    (40, 1, True), (40, 1, True), (43, 1, False), (40, 1, True), (47, 2, False), (45, 2, False),
]
RIFF_B = [
    (40, 1, True), (40, 1, True), (43, 1, False), (40, 1, True), (40, 1, True), (45, 1, False), (40, 1, True), (46, 1, False),
    (40, 1, True), (40, 1, True), (50, 2, False), (48, 2, False), (47, 2, False),
]
CHORUS = [(40, 8), (36, 8), (43, 8), (38, 8)]  # E5 C5 G5 D5 (um compasso cada)
CHORUS_END = [(40, 8), (36, 8), (38, 8), (47, 8)]  # ... D5 B5 (volta para o riff)
BREAK = [(40, 16), (43, 16), (45, 16), (47, 16)]  # notas longas, meio tempo
LEAD = [  # solo do refrão (nota MIDI, colcheias)
    (76, 2), (74, 1), (71, 1), (74, 2), (76, 2), (79, 3), (78, 1), (76, 4),
    (72, 2), (74, 2), (76, 2), (74, 2), (71, 4), (67, 4),
    (79, 2), (78, 1), (76, 1), (74, 2), (76, 2), (78, 2), (79, 2), (81, 4),
    (83, 3), (81, 1), (79, 2), (78, 2), (76, 8),
]

SECTIONS = [("intro", 4), ("riff", 16), ("refrao", 16), ("quebra", 8), ("riff", 16), ("refrao", 16)]
total_bars = sum(b for _, b in SECTIONS)
LEN = int(total_bars * BAR * SR)
gtrL = np.zeros(LEN + SR * 3)
gtrR = np.zeros_like(gtrL)
bass = np.zeros_like(gtrL)
drums = np.zeros_like(gtrL)
lead = np.zeros_like(gtrL)


def put(buf, x, t, g=1.0):
    i = int(t * SR)
    if i >= len(buf):
        return
    m = min(len(x), len(buf) - i)
    buf[i:i + m] += x[:m] * g


def guitar_note(n, secs, mute, t):
    pc = power_chord(n, secs, mute, oct_up=not mute)
    a = amp(pc * 0.6, 16 if not mute else 12, mute)
    # duas guitarras (esquerda/direita) com pequenas diferenças de tempo = parede de guitarra
    put(gtrL, a, t)
    put(gtrR, a * 0.95, t + 0.011)
    b = np.sin(2 * np.pi * hz(n - 12) * np.arange(int(secs * SR)) / SR)
    b = np.tanh(b * 2.5) * np.exp(-np.arange(len(b)) / SR * (10 if mute else 2.2))
    put(bass, sosfilt(sos("lowpass", 900), b) * 0.55, t)


def drum_bar(t, kind, bar_i, last):
    for s in range(16):
        tt = t + s * SIX
        if kind == "quebra":  # meio tempo: bumbo no 1, caixa no 3, prato de condução
            if s == 0:
                put(drums, K, tt)
            if s == 8:
                put(drums, S, tt)
            if s % 4 == 0:
                put(drums, HO, tt, 0.6)
            continue
        if kind == "intro":
            if bar_i < 2:
                continue  # só guitarra
            if s % 4 == 0:
                put(drums, tom(110 if s < 8 else 80), tt)
            continue
        if s % 2 == 0:
            put(drums, H if kind == "riff" else HO, tt, 0.8 if s % 4 == 0 else 0.55)
        if s in (4, 12):
            put(drums, S, tt)
        if kind == "riff" and s in (0, 3, 6, 8, 10, 11, 14):
            put(drums, K, tt)
        if kind == "refrao" and s % 2 == 0:
            put(drums, K, tt, 0.85)  # bumbo em colcheias no refrão
    if last:  # virada no fim da seção: bumbo duplo + caixa em semicolcheias
        for s in range(8, 16):
            put(drums, S, t + s * SIX, 0.55 + 0.05 * (s - 8))
            put(drums, K, t + s * SIX, 0.6)


t = 0.0
for name, bars in SECTIONS:
    sec_start = t
    if name in ("riff", "refrao"):
        put(drums, C, t, 0.9)
    for b in range(bars):
        bt = sec_start + b * BAR
        drum_bar(bt, name, b, b == bars - 1 and name != "quebra")
    if name in ("intro", "riff"):
        pos = 0.0
        bar_pairs = bars // 2
        for pair in range(bar_pairs):
            seq = RIFF if pair % 2 == 0 else RIFF_B
            pos = sec_start + pair * 2 * BAR
            for n, d, m in seq:
                guitar_note(n, d * EIGHTH * 0.97, m, pos)
                pos += d * EIGHTH
    elif name == "refrao":
        for rep in range(bars // 4):
            seq = CHORUS if rep < bars // 4 - 1 else CHORUS_END
            pos = sec_start + rep * 4 * BAR
            for n, d in seq:
                # power chord aberto + oitavas em colcheias (palhetada contínua)
                guitar_note(n, d * EIGHTH * 0.98, False, pos)
                for e in range(d):
                    put(gtrR, amp(power_chord(n + 12, EIGHTH * 0.8, True, False) * 0.25, 10, True) * 0.4, pos + e * EIGHTH)
                pos += d * EIGHTH
        # solo
        pos = sec_start
        for n, d in LEAD * (bars // 8):
            L = int(d * EIGHTH * SR)
            tt = np.arange(L) / SR
            vib = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * tt) * np.clip(tt * 3, 0, 1)
            ph = 2 * np.pi * np.cumsum(hz(n) * vib) / SR
            x = (np.sin(ph) + 0.5 * np.sin(2 * ph) + 0.25 * np.sin(3 * ph)) * np.minimum(1, tt * 60) * np.exp(-tt * 0.8)
            x = np.tanh(x * 4.0)
            x = sosfilt(sos("lowpass", 4200, 2), x)
            put(lead, x * 0.22, pos)
            pos += d * EIGHTH
    elif name == "quebra":
        for i, (n, d) in enumerate(BREAK):
            guitar_note(n, d * EIGHTH * 0.98, False, sec_start + i * 2 * BAR)
    t = sec_start + bars * BAR

# ------------------------------------------------------------------ mixagem
def norm(x):
    return x / (np.max(np.abs(x)) + 1e-9)


gL = norm(gtrL) * 0.55
gR = norm(gtrR) * 0.55
bs = norm(bass) * 0.45
dr = norm(drums) * 0.75
ld = norm(lead) * 0.32
# "sala" curta na bateria e no solo
room = lfilter([1], [1] + [0] * int(0.023 * SR) + [-0.25], dr)
echo = lfilter([1], [1] + [0] * int(EIGHTH * 3 * SR) + [-0.3], ld)
L = gL + bs + 0.9 * room + echo * 0.8
R = gR + bs + 0.9 * room + np.roll(echo, int(0.012 * SR)) * 0.8
# loop sem emenda: o rabo (pratos/eco) volta somado ao começo
tail = int(3 * SR)
for ch in (L, R):
    ch[:tail] += ch[LEN:LEN + tail]
L, R = L[:LEN], R[:LEN]
mix = np.stack([L, R], 1)
mix = np.tanh(mix * 1.4) / np.tanh(1.4)  # limitador suave
mix *= 0.89 / np.max(np.abs(mix))

out = sys.argv[1] if len(sys.argv) > 1 else "menu_theme.wav"
import wave
with wave.open(out, "wb") as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes((mix * 32767).astype("<i2").tobytes())
print(out, round(LEN / SR, 1), "s")
