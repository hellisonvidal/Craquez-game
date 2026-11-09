"""Trilha de abertura/menus do Craquez: pop-rock animado e mais suave, original, sintetizado por código.

Sol maior, 126 BPM. Violão/guitarra com pouca distorção (crunch) batendo acordes, guitarra limpa
dedilhando na estrofe, teclado de fundo, baixo redondo, bateria leve e uma melodia no refrão.
Estrutura: intro (4) -> estrofe (16) -> refrão (16) -> ponte (8) -> estrofe (16) -> refrão (16), em loop.
uso: python3 tools/audio/menu_theme_poprock.py saida.wav  (depois ffmpeg -> .ogg)
"""
import sys, wave
import numpy as np
from scipy.signal import lfilter, butter, sosfilt

SR = 44100
BPM = 126.0
BEAT = 60.0 / BPM
BAR = BEAT * 4
EIGHTH = BEAT / 2
SIX = BEAT / 4
rng = np.random.default_rng(2077)


def hz(n):
    return 440.0 * 2 ** ((n - 69) / 12.0)


def sos(kind, f, order=2):
    return butter(order, f, btype=kind, fs=SR, output="sos")


_cache = {}


def string(n, bright=0.55):
    """Corda dedilhada (Karplus-Strong), 3,5 s, guardada por nota."""
    key = (n, bright)
    if key in _cache:
        return _cache[key]
    f = hz(n)
    N = int(SR / f)
    L = int(3.5 * SR)
    prev = lfilter([1 - bright], [1, -bright], rng.uniform(-1, 1, N))
    out = np.zeros(L)
    pos = 0
    while pos < L:
        m = min(N, L - pos)
        out[pos:pos + m] = prev[:m]
        prev = 0.997 * 0.5 * (prev + np.roll(prev, -1))
        pos += m
    _cache[key] = out
    return out


def chord_notes(root, minor):
    # voicing aberto de violão: fundamental, quinta, oitava, terça, quinta
    third = 3 if minor else 4
    return [root, root + 7, root + 12, root + 12 + third, root + 19]


def strum(root, minor, secs, down=True, vel=1.0):
    L = int(secs * SR)
    x = np.zeros(L)
    notes = chord_notes(root, minor)
    if not down:
        notes = notes[::-1][:3]  # pra cima: só as agudas
    for i, n in enumerate(notes):
        s = string(n, 0.62)[:L]
        d = int(i * 0.007 * SR)
        x[d:] += s[: L - d]
    t = np.arange(L) / SR
    x *= np.minimum(1.0, np.exp(-(t - secs + 0.04).clip(0) * 60))
    return x * vel


def crunch(x, gain=3.2):
    x = sosfilt(sos("highpass", 90), x)
    y = np.tanh(gain * x) / np.tanh(gain)
    y = sosfilt(sos("lowpass", 6500, 2), y)
    return y


def kick():
    L = int(0.3 * SR)
    t = np.arange(L) / SR
    f = 50 + 70 * np.exp(-t * 30)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 11) * 0.9


def snare():
    L = int(0.25 * SR)
    t = np.arange(L) / SR
    tone = np.sin(2 * np.pi * 200 * t) * np.exp(-t * 25) * 0.5
    nz = sosfilt(butter(2, [1800, 8000], btype="bandpass", fs=SR, output="sos"), rng.uniform(-1, 1, L)) * np.exp(-t * 18)
    return (tone + nz * 1.1) * 0.6


def hat(open_=False):
    L = int((0.2 if open_ else 0.05) * SR)
    t = np.arange(L) / SR
    return sosfilt(sos("highpass", 8000, 4), rng.uniform(-1, 1, L)) * np.exp(-t * (12 if open_ else 70)) * 0.22


def crash():
    L = int(2.0 * SR)
    t = np.arange(L) / SR
    return sosfilt(sos("highpass", 5000, 2), rng.uniform(-1, 1, L)) * np.exp(-t * 1.8) * 0.3


K, S, H, HO, C = kick(), snare(), hat(), hat(True), crash()

# progressões (fundamental MIDI, menor?) — um acorde por compasso
VERSE = [(43, False), (50, False), (52, True), (48, False)]        # G D Em C
CHORUS = [(48, False), (43, False), (50, False), (52, True)]       # C G D Em
BRIDGE = [(45, True), (48, False), (43, False), (50, False)]       # Am C G D
MELODY = [  # refrão: (nota, colcheias)
    (76, 2), (74, 2), (72, 2), (74, 2), (71, 4), (67, 4),
    (74, 2), (76, 2), (78, 2), (79, 2), (78, 4), (74, 4),
    (76, 2), (74, 2), (72, 2), (71, 2), (69, 4), (67, 2), (69, 2),
    (71, 3), (72, 1), (74, 4), (71, 8),
]
SECTIONS = [("intro", 4), ("estrofe", 16), ("refrao", 16), ("ponte", 8), ("estrofe", 16), ("refrao", 16)]
LEN = int(sum(b for _, b in SECTIONS) * BAR * SR)
pad = LEN + SR * 3
gL, gR, cln, keys, bass, drums, lead = (np.zeros(pad) for _ in range(7))


def put(buf, x, t, g=1.0):
    i = int(t * SR)
    if i >= len(buf):
        return
    m = min(len(x), len(buf) - i)
    buf[i:i + m] += x[:m] * g


def keys_chord(root, minor, secs, t):
    L = int(secs * SR)
    tt = np.arange(L) / SR
    x = np.zeros(L)
    for n in [root + 12, root + 16 - (1 if minor else 0), root + 19, root + 24]:
        ph = 2 * np.pi * hz(n) * tt
        x += np.sin(ph) + 0.25 * np.sin(2 * ph)
    env = np.minimum(1, tt * 8) * np.minimum(1, (secs - tt) * 6).clip(0)
    put(keys, x * env * 0.1, t)


def bass_note(n, secs, t):
    L = int(secs * SR)
    tt = np.arange(L) / SR
    x = np.sin(2 * np.pi * hz(n) * tt) + 0.3 * np.sin(4 * np.pi * hz(n) * tt)
    x *= np.minimum(1, tt * 200) * np.exp(-tt * 3)
    put(bass, sosfilt(sos("lowpass", 700), x) * 0.5, t)


STRUM = [(0, True, 1.0), (2, True, 0.7), (3, False, 0.5), (5, False, 0.55), (6, True, 0.75), (7, False, 0.5)]  # colcheias do compasso

t = 0.0
for name, bars in SECTIONS:
    if name in ("estrofe", "refrao"):
        put(drums, C, t, 0.8)
    prog = VERSE if name in ("intro", "estrofe") else CHORUS if name == "refrao" else BRIDGE
    for b in range(bars):
        bt = t + b * BAR
        root, minor = prog[b % 4]
        # bateria
        for s in range(16):
            st = bt + s * SIX
            if name == "intro" and b < 2:
                break
            if name == "ponte":
                if s in (0, 10): put(drums, K, st, 0.8)
                if s == 8: put(drums, S, st, 0.8)
                if s % 4 == 0: put(drums, H, st, 0.6)
                continue
            if s % 2 == 0: put(drums, HO if (name == "refrao" and s % 4 == 2) else H, st, 0.7 if s % 4 == 0 else 0.5)
            if s in (0, 6, 8) or (name == "refrao" and s == 14): put(drums, K, st, 0.85)
            if s in (4, 12): put(drums, S, st, 0.85)
        if b == bars - 1 and name != "ponte":  # virada curta na caixa
            for s in (12, 13, 14, 15):
                put(drums, S, bt + s * SIX, 0.35 + 0.08 * (s - 12))
        # baixo: colcheias na fundamental (oitava na última)
        for e in range(8):
            bass_note(root - 12 + (12 if e == 7 and name == "refrao" else 0), EIGHTH * 0.9, bt + e * EIGHTH)
        keys_chord(root, minor, BAR * 0.98, bt)
        if name == "estrofe" or name == "intro":
            # guitarra limpa dedilhando (arpejo em colcheias) + violão leve
            notes = chord_notes(root, minor)
            for e in range(8):
                n = notes[[0, 2, 3, 4, 3, 2, 1, 3][e]]
                put(cln, string(n + 12, 0.5)[: int(EIGHTH * 2.2 * SR)] * np.exp(-np.arange(int(EIGHTH * 2.2 * SR)) / SR * 2.5), bt + e * EIGHTH, 0.5)
            if name == "estrofe":
                for e, dn, v in STRUM[:2] + STRUM[4:5]:
                    put(gL, crunch(strum(root, minor, EIGHTH * 1.9, dn, v * 0.6), 2.2), bt + e * EIGHTH)
        else:
            for e, dn, v in STRUM:
                x = crunch(strum(root, minor, EIGHTH * 1.9, dn, v * 0.7))
                put(gL, x, bt + e * EIGHTH)
                put(gR, x * 0.92, bt + e * EIGHTH + 0.013)
    if name == "refrao":
        pos = t
        for n, d in MELODY * (bars // 8):
            L = int(d * EIGHTH * SR)
            tt = np.arange(L) / SR
            vib = 1 + 0.004 * np.sin(2 * np.pi * 5 * tt) * np.clip(tt * 2, 0, 1)
            ph = 2 * np.pi * np.cumsum(hz(n) * vib) / SR
            x = (np.sin(ph) + 0.3 * np.sin(2 * ph) + 0.12 * np.sin(3 * ph)) * np.minimum(1, tt * 30) * np.minimum(1, (d * EIGHTH - tt) * 12).clip(0)
            put(lead, x * 0.2, pos)
            pos += d * EIGHTH
    t += bars * BAR


def norm(x):
    return x / (np.max(np.abs(x)) + 1e-9)


dr = norm(drums) * 0.55
room = lfilter([1], [1] + [0] * int(0.021 * SR) + [-0.22], dr)
ld = norm(lead) * 0.3
echo = lfilter([1], [1] + [0] * int(EIGHTH * 3 * SR) + [-0.28], ld)
cl = norm(cln) * 0.22
kb = norm(keys) * 0.18
L = norm(gL) * 0.36 + cl + kb + norm(bass) * 0.4 + room + echo * 0.8
R = norm(gR + gL * 0.25) * 0.36 + np.roll(cl, int(0.009 * SR)) + kb + norm(bass) * 0.4 + room + np.roll(echo, int(0.011 * SR)) * 0.8
tail = int(3 * SR)
for ch in (L, R):
    ch[:tail] += ch[LEN:LEN + tail]
mix = np.stack([L[:LEN], R[:LEN]], 1)
mix = np.tanh(mix * 1.1) / np.tanh(1.1)   # limitador leve (mais dinâmica que a versão pesada)
mix *= 0.85 / np.max(np.abs(mix))
out = sys.argv[1] if len(sys.argv) > 1 else "menu_theme.wav"
with wave.open(out, "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((mix * 32767).astype("<i2").tobytes())
print(out, round(LEN / SR, 1), "s")
