"""12 trilhas originais dos menus do Craquez, compostas e sintetizadas por código.

Cada faixa tem estilo, tom, andamento, progressão, instrumentos e bateria próprios; a melodia é
composta a partir das notas dos acordes (semente fixa por faixa, sempre o mesmo resultado).
O jogo sorteia uma delas cada vez que abre.
uso: python3 tools/audio/menu_tracks.py PASTA [índice]   (gera PASTA/track_XX.wav)
"""
import sys, os, wave
import numpy as np
from scipy.signal import lfilter, butter, sosfilt

SR = 44100

# nome, bpm, tônica MIDI, modo, progressão (graus), bateria, instrumentos, swing
TRACKS = [
    ("Arquibancada",       126, 43, "maior", [1, 5, 6, 4], "rock",   ["crunch", "keys", "lead"], 0.0),
    ("Batucada Elétrica",  132, 50, "menor", [1, 6, 7, 1], "samba",  ["synth", "pluck", "lead"], 0.12),
    ("Fim de Tarde",        96, 45, "maior", [1, 3, 4, 5], "pop",    ["clean", "piano", "lead"], 0.0),
    ("Contra-Ataque",      140, 40, "menor", [1, 7, 6, 7], "rock",   ["crunch", "synth", "lead"], 0.0),
    ("Neon na Quadra",     118, 48, "menor", [6, 4, 1, 5], "house",  ["synth", "pluck", "lead"], 0.0),
    ("Domingo no Campo",   104, 41, "maior", [1, 4, 2, 5], "reggae", ["clean", "keys", "lead"], 0.0),
    ("Toque de Bola",      122, 47, "maior", [1, 6, 2, 5], "funk",   ["clean", "keys", "lead"], 0.08),
    ("Grito de Gol",       150, 45, "maior", [1, 5, 4, 5], "rock",   ["crunch", "keys", "lead"], 0.0),
    ("Concentração",        90, 52, "menor", [1, 6, 3, 7], "halftime", ["piano", "synth", "lead"], 0.0),
    ("Rua de Terra",       112, 43, "menor", [1, 4, 1, 5], "baiao",  ["pluck", "clean", "lead"], 0.0),
    ("Vestiário",          100, 46, "maior", [4, 5, 3, 6], "pop",    ["piano", "keys", "lead"], 0.1),
    ("Volta Olímpica",     128, 38, "maior", [1, 5, 6, 4], "house",  ["crunch", "synth", "lead"], 0.0),
]

MAJ = [0, 2, 4, 5, 7, 9, 11]
MIN = [0, 2, 3, 5, 7, 8, 10]


def hz(n):
    return 440.0 * 2 ** ((n - 69) / 12.0)


def sos(kind, f, order=2):
    return butter(order, f, btype=kind, fs=SR, output="sos")


class Song:
    def __init__(self, idx):
        name, bpm, tonic, mode, prog, drum, inst, swing = TRACKS[idx]
        self.name, self.bpm, self.tonic, self.drum, self.inst, self.swing = name, bpm, tonic, drum, inst, swing
        self.scale = MAJ if mode == "maior" else MIN
        self.prog = prog
        self.rng = np.random.default_rng(1000 + idx)
        self.BEAT = 60.0 / bpm
        self.BAR = self.BEAT * 4
        self.E = self.BEAT / 2
        self.S = self.BEAT / 4
        # estrutura: intro 4, A 8, B 8, A 8, ponte 4, B 8 (~40 compassos, 1min15 a 1min50)
        self.sections = [("intro", 4), ("A", 8), ("B", 8), ("A", 8), ("ponte", 4), ("B", 8)]
        self.bars = sum(b for _, b in self.sections)
        self.LEN = int(self.bars * self.BAR * SR)
        pad = self.LEN + SR * 3
        self.tr = {k: np.zeros(pad) for k in ("drums", "bass", "harm", "harm2", "lead")}
        self._ks = {}
        self.K, self.SN, self.H, self.HO, self.C = self.kit()

    # ------------------------------------------------------------ teoria
    def chord(self, degree):
        i = degree - 1
        notes = [self.tonic + self.scale[(i + k) % 7] + 12 * ((i + k) // 7) for k in (0, 2, 4)]
        return notes

    # ------------------------------------------------------------ fontes
    def string(self, n, bright=0.55, secs=3.0):
        key = (n, bright)
        if key in self._ks:
            return self._ks[key]
        N = int(SR / hz(n))
        L = int(secs * SR)
        prev = lfilter([1 - bright], [1, -bright], self.rng.uniform(-1, 1, N))
        out = np.zeros(L)
        pos = 0
        while pos < L:
            m = min(N, L - pos)
            out[pos:pos + m] = prev[:m]
            prev = 0.997 * 0.5 * (prev + np.roll(prev, -1))
            pos += m
        self._ks[key] = out
        return out

    def kit(self):
        r = self.rng
        def env(L, k): return np.exp(-np.arange(L) / SR * k)
        L = int(0.32 * SR); t = np.arange(L) / SR
        f = 48 + (90 if self.drum in ("house", "funk") else 70) * np.exp(-t * 32)
        K = np.sin(2 * np.pi * np.cumsum(f) / SR) * env(L, 10) * 0.9
        L = int(0.25 * SR); t = np.arange(L) / SR
        SN = (np.sin(2 * np.pi * 195 * t) * env(L, 24) * 0.5 + sosfilt(butter(2, [1700, 8500], btype="bandpass", fs=SR, output="sos"), r.uniform(-1, 1, L)) * env(L, 17)) * 0.6
        L = int(0.05 * SR)
        H = sosfilt(sos("highpass", 8000, 4), r.uniform(-1, 1, L)) * env(L, 70) * 0.22
        L = int(0.22 * SR)
        HO = sosfilt(sos("highpass", 7500, 4), r.uniform(-1, 1, L)) * env(L, 12) * 0.2
        L = int(2.0 * SR)
        C = sosfilt(sos("highpass", 5000, 2), r.uniform(-1, 1, L)) * env(L, 1.8) * 0.28
        return K, SN, H, HO, C

    def put(self, name, x, t, g=1.0):
        buf = self.tr[name]
        i = int(t * SR)
        if i >= len(buf) or i < 0:
            return
        m = min(len(x), len(buf) - i)
        buf[i:i + m] += x[:m] * g

    def tone(self, n, secs, kind):
        L = int(secs * SR)
        t = np.arange(L) / SR
        f = hz(n)
        if kind == "synth":   # serra suave com filtro
            ph = (t * f) % 1.0
            x = (2 * ph - 1) + 0.5 * (2 * ((t * f * 1.005) % 1.0) - 1)
            x = sosfilt(sos("lowpass", 1800), x) * 0.5
            env = np.minimum(1, t * 40) * np.minimum(1, (secs - t) * 10).clip(0)
        elif kind == "piano":
            x = sum(np.sin(2 * np.pi * f * h * t) / h ** 1.3 for h in (1, 2, 3, 4))
            env = np.exp(-t * 2.2) * np.minimum(1, t * 300)
        elif kind == "keys":  # órgão / piano elétrico
            x = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t) + 0.15 * np.sin(2 * np.pi * f * 3 * t)
            env = np.minimum(1, t * 20) * np.minimum(1, (secs - t) * 8).clip(0) * (0.75 + 0.25 * np.exp(-t * 3))
        else:  # pluck
            s = self.string(n + 12, 0.45)[:L]
            return np.pad(s, (0, max(0, L - len(s))))[:L] * np.exp(-t * 3.5) * 0.8
        return x * env

    # ------------------------------------------------------------ partes
    def drums(self, bt, kind, b, last):
        d, S = self.drum, self.S
        def sw(s): return bt + s * S + (self.swing * S if s % 2 == 1 else 0)
        if kind == "intro" and b < 2:
            return
        for s in range(16):
            tt = sw(s)
            if kind == "ponte":
                if s in (0, 8): self.put("drums", self.K, tt, 0.7)
                if s % 4 == 0: self.put("drums", self.H, tt, 0.6)
                continue
            if d == "rock":
                if s % 2 == 0: self.put("drums", self.H, tt, 0.7 if s % 4 == 0 else 0.5)
                if s in (0, 6, 8, 10): self.put("drums", self.K, tt, 0.85)
                if s in (4, 12): self.put("drums", self.SN, tt, 0.85)
            elif d == "pop":
                if s % 2 == 0: self.put("drums", self.H, tt, 0.6)
                if s in (0, 7, 8): self.put("drums", self.K, tt, 0.8)
                if s in (4, 12): self.put("drums", self.SN, tt, 0.75)
            elif d == "house":
                if s % 4 == 0: self.put("drums", self.K, tt, 0.95)
                if s % 4 == 2: self.put("drums", self.HO, tt, 0.6)
                if s in (4, 12): self.put("drums", self.SN, tt, 0.6)
                if s % 2 == 1: self.put("drums", self.H, tt, 0.35)
            elif d == "samba":
                if s in (0, 3, 4, 7, 8, 11, 12, 15): self.put("drums", self.K, tt, 0.6 if s % 4 else 0.85)   # surdo
                self.put("drums", self.H, tt, 0.55 if s % 4 in (0, 3) else 0.3)                            # ganzá
                if s in (2, 6, 9, 13): self.put("drums", self.SN, tt, 0.45)                                # tamborim
            elif d == "funk":
                if s in (0, 3, 8, 10): self.put("drums", self.K, tt, 0.9)
                if s in (4, 12): self.put("drums", self.SN, tt, 0.8)
                if s in (7, 14): self.put("drums", self.SN, tt, 0.3)
                self.put("drums", self.H, tt, 0.45 if s % 2 == 0 else 0.25)
            elif d == "reggae":
                if s == 8: self.put("drums", self.K, tt, 0.9); self.put("drums", self.SN, tt, 0.7)
                if s % 2 == 0: self.put("drums", self.H, tt, 0.4)
            elif d == "halftime":
                if s in (0, 10): self.put("drums", self.K, tt, 0.85)
                if s == 8: self.put("drums", self.SN, tt, 0.85)
                self.put("drums", self.H, tt, 0.35 if s % 2 == 0 else 0.2)
            elif d == "baiao":
                if s in (0, 3, 8, 11): self.put("drums", self.K, tt, 0.85)       # zabumba
                if s in (4, 12): self.put("drums", self.SN, tt, 0.5)
                self.put("drums", self.H, tt, 0.45 if s % 2 == 0 else 0.3)       # triângulo
        if last and kind != "intro":
            for s in (12, 13, 14, 15):
                self.put("drums", self.SN, bt + s * S, 0.3 + 0.08 * (s - 12))

    def harmony(self, bt, notes, kind, inst, track):
        E, BAR = self.E, self.BAR
        if inst == "crunch":
            pat = [(0, 1.0), (2, 0.7), (3, 0.5), (5, 0.55), (6, 0.75), (7, 0.5)] if kind != "intro" else [(0, 0.9)]
            for e, v in pat:
                x = np.zeros(int(E * 1.9 * SR))
                for i, n in enumerate([notes[0] - 12, notes[0] - 5, notes[0], notes[1], notes[2]]):
                    s = self.string(n + 12, 0.62)[:len(x)]
                    d = int(i * 0.007 * SR)
                    x[d:] += s[:len(x) - d]
                x = np.tanh(3.0 * sosfilt(sos("highpass", 90), x)) / np.tanh(3.0)
                self.put(track, sosfilt(sos("lowpass", 6000), x) * v * 0.55, bt + e * E)
        elif inst == "clean":
            order = [0, 1, 2, 1, 0, 2, 1, 2]
            for e in range(8):
                n = notes[order[e]] + 12
                s = self.string(n, 0.5)[: int(E * 2.2 * SR)]
                self.put(track, s * np.exp(-np.arange(len(s)) / SR * 2.5) * 0.5, bt + e * E + (self.swing * E * 0.5 if e % 2 else 0))
        elif inst in ("synth", "keys"):
            if self.drum == "reggae" and inst == "keys":   # skank: contratempos
                for e in (1, 3, 5, 7):
                    for n in notes: self.put(track, self.tone(n + 12, E * 0.5, "keys") * 0.12, bt + e * E)
            elif self.drum in ("house", "samba") and inst == "synth":
                for e in (1, 3, 4, 6):
                    for n in notes: self.put(track, self.tone(n + 12, E * 0.8, "synth") * 0.12, bt + e * E)
            else:
                for n in notes: self.put(track, self.tone(n + 12, BAR * 0.98, inst) * 0.1, bt)
        elif inst == "piano":
            for e, n in enumerate([notes[0], notes[1] + 12, notes[2] + 12, notes[1] + 12] * 2):
                self.put(track, self.tone(n, E * 3, "piano") * 0.16, bt + e * E)
        elif inst == "pluck":
            for e in range(8):
                n = notes[(e * 2) % 3] + (12 if e % 4 == 3 else 0)
                self.put(track, self.tone(n, E * 1.5, "pluck") * 0.45, bt + e * E)

    def bassline(self, bt, root, kind):
        E = self.E
        d = self.drum
        if d == "reggae":
            hits = [(0, 2, 0), (3, 1, 7), (4, 2, 0), (7, 1, 12)]
        elif d in ("funk", "baiao"):
            hits = [(0, 1, 0), (1.5, 0.5, 0), (3, 1, 12), (4, 1, 0), (6, 1, 7), (7, 1, 10 if self.scale is MIN else 9)]
        elif d == "halftime" or kind == "ponte":
            hits = [(0, 4, 0), (5, 3, 7)]
        else:
            hits = [(e, 0.9, 12 if e == 7 else 0) for e in range(8)]
        for e, dur, off in hits:
            L = int(dur * E * SR)
            t = np.arange(L) / SR
            n = root - 12 + off
            x = np.sin(2 * np.pi * hz(n) * t) + 0.3 * np.sin(4 * np.pi * hz(n) * t)
            x *= np.minimum(1, t * 200) * np.exp(-t * 2.5) * np.minimum(1, (dur * E - t) * 30).clip(0)
            self.put("bass", sosfilt(sos("lowpass", 700), x) * 0.5, bt + e * E)

    def melody(self, t0, bars):
        """Frase de 4 compassos sobre os acordes, repetida com variação."""
        r = self.rng
        E = self.E
        rhythms = [[2, 2, 2, 2], [3, 1, 2, 2], [1, 1, 2, 4], [2, 1, 1, 4], [4, 2, 2]]
        phrase = []
        prev = self.tonic + 24
        for b in range(4):
            notes = self.chord(self.prog[b])
            rh = rhythms[r.integers(len(rhythms))]
            for i, d in enumerate(rh):
                if i == 0 or r.random() < 0.55:
                    cands = [n + 24 for n in notes] + [n + 36 for n in notes]
                else:
                    cands = [self.tonic + 24 + self.scale[k] + o for k in range(7) for o in (0, 12)]
                n = min(cands, key=lambda c: abs(c - prev) + r.random() * 3)
                phrase.append((n, d))
                prev = n
        pos = t0
        for rep in range(bars // 4):
            for j, (n, d) in enumerate(phrase):
                if rep % 2 == 1 and j == len(phrase) - 1:
                    n = self.tonic + 24   # resolve na tônica na repetição
                L = int(d * E * SR)
                t = np.arange(L) / SR
                vib = 1 + 0.004 * np.sin(2 * np.pi * 5 * t) * np.clip(t * 2, 0, 1)
                ph = 2 * np.pi * np.cumsum(hz(n) * vib) / SR
                x = (np.sin(ph) + 0.3 * np.sin(2 * ph) + 0.12 * np.sin(3 * ph)) * np.minimum(1, t * 30) * np.minimum(1, (d * E - t) * 12).clip(0)
                self.put("lead", x * 0.2, pos)
                pos += d * E

    def render(self):
        t = 0.0
        for name, bars in self.sections:
            if name in ("A", "B"):
                self.put("drums", self.C, t, 0.7)
            for b in range(bars):
                bt = t + b * self.BAR
                deg = self.prog[b % 4] if name != "ponte" else [4, 5, 4, 5][b % 4]
                notes = self.chord(deg)
                self.drums(bt, name, b, b == bars - 1)
                if name != "intro" or b >= 2:
                    self.bassline(bt, notes[0], name)
                self.harmony(bt, notes, name, self.inst[0] if name != "A" or self.inst[0] != "crunch" else "clean", "harm")
                if name in ("B", "ponte", "A"):
                    self.harmony(bt, notes, name, self.inst[1], "harm2")
            if name == "B":
                self.melody(t, bars)
            t += bars * self.BAR
        def norm(x): return x / (np.max(np.abs(x)) + 1e-9)
        tr = self.tr
        dr = norm(tr["drums"]) * 0.55
        room = lfilter([1], [1] + [0] * int(0.021 * SR) + [-0.22], dr)
        ld = norm(tr["lead"]) * 0.3
        echo = lfilter([1], [1] + [0] * int(self.E * 3 * SR) + [-0.28], ld)
        h1, h2, bs = norm(tr["harm"]) * 0.34, norm(tr["harm2"]) * 0.26, norm(tr["bass"]) * 0.42
        L = h1 + h2 * 0.8 + bs + room + echo
        R = np.roll(h1, int(0.012 * SR)) * 0.9 + h2 + bs + room + np.roll(echo, int(0.011 * SR))
        tail = int(3 * SR)
        for ch in (L, R):
            ch[:tail] += ch[self.LEN:self.LEN + tail]
        mix = np.stack([L[:self.LEN], R[:self.LEN]], 1)
        mix = np.tanh(mix * 1.1) / np.tanh(1.1)
        return mix * 0.85 / np.max(np.abs(mix))


def write(path, mix):
    with wave.open(path, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((mix * 32767).astype("<i2").tobytes())


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    which = [int(sys.argv[2])] if len(sys.argv) > 2 else range(len(TRACKS))
    for i in which:
        s = Song(i)
        mix = s.render()
        p = os.path.join(out, "track_%02d.wav" % (i + 1))
        write(p, mix)
        print(p, s.name, round(len(mix) / SR, 1), "s", flush=True)
