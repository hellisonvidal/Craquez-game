"""12 trilhas originais dos menus do Craquez, compostas e sintetizadas por código.

Cada faixa tem estilo, tom, andamento, progressão, instrumentos e bateria próprios; a melodia é
composta a partir das notas dos acordes (semente fixa por faixa, sempre o mesmo resultado).
O jogo sorteia uma delas cada vez que abre.
uso: python3 tools/audio/menu_tracks.py PASTA [índice]   (gera PASTA/track_XX.wav)
"""
import sys, os, wave
import numpy as np
from scipy.signal import lfilter, butter, sosfilt, fftconvolve

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
        self.tr = {k: np.zeros(pad) for k in ("drums", "bass", "harm", "harm2", "lead", "leadR")}
        self._ks = {}
        self.kicks = []
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
        """Bateria de produção atual: bumbo com sub e clique saturado, caixa + palmas em camadas, chimbais finos."""
        r = self.rng
        def env(L, k): return np.exp(-np.arange(L) / SR * k)
        L = int(0.45 * SR); t = np.arange(L) / SR
        f = 45 + 110 * np.exp(-t * 38)
        body = np.sin(2 * np.pi * np.cumsum(f) / SR) * env(L, 7)
        click = sosfilt(sos("bandpass", [2000, 7000]), r.uniform(-1, 1, L)) * env(L, 300) * 0.35
        K = np.tanh((body + click) * 1.8) * 0.95
        L = int(0.35 * SR); t = np.arange(L) / SR
        nz = sosfilt(butter(2, [900, 9000], btype="bandpass", fs=SR, output="sos"), r.uniform(-1, 1, L))
        snare = np.sin(2 * np.pi * 185 * t) * env(L, 30) * 0.4 + nz * env(L, 14) * 0.8
        clap = np.zeros(L)
        for k, d in enumerate((0.0, 0.011, 0.022)):   # palmas: três estalos quase juntos + cauda
            i = int(d * SR)
            clap[i:] += sosfilt(sos("bandpass", [1000, 2600]), r.uniform(-1, 1, L - i)) * env(L - i, 60 if k < 2 else 11)
        SN = np.tanh((snare * 0.7 + clap * 0.6) * 1.5) * 0.7
        L = int(0.06 * SR)
        H = sosfilt(sos("highpass", 9000, 4), r.uniform(-1, 1, L)) * env(L, 80) * 0.25
        L = int(0.3 * SR)
        HO = sosfilt(sos("highpass", 8000, 4), r.uniform(-1, 1, L)) * env(L, 9) * 0.2
        L = int(2.5 * SR)
        C = sosfilt(sos("highpass", 5500, 2), r.uniform(-1, 1, L)) * env(L, 1.5) * 0.25
        return K, SN, H, HO, C

    def put(self, name, x, t, g=1.0):
        if x is self.__dict__.get("K"):
            self.kicks.append((t, g))
        buf = self.tr[name]
        i = int(t * SR)
        if i >= len(buf) or i < 0:
            return
        m = min(len(x), len(buf) - i)
        buf[i:i + m] += x[:m] * g

    def supersaw(self, f, L, voices=7, spread=0.012):
        t = np.arange(L) / SR
        x = np.zeros(L)
        for v in range(voices):
            d = (v - (voices - 1) / 2) / ((voices - 1) / 2) * spread
            ph = (t * f * (1 + d) + self.rng.random()) % 1.0
            x += 2 * ph - 1
        return x / voices

    def tone(self, n, secs, kind):
        L = int(secs * SR)
        t = np.arange(L) / SR
        f = hz(n)
        if kind == "synth":   # pad supersaw com filtro que abre devagar
            x = self.supersaw(f, L)
            x = sosfilt(sos("lowpass", 2400), x) * 0.7 + sosfilt(sos("lowpass", 900), x) * 0.3
            env = np.minimum(1, t * 6) * np.minimum(1, (secs - t) * 6).clip(0)
        elif kind == "keys":  # piano elétrico (FM, timbre de Rhodes)
            idx = 2.2 * np.exp(-t * 4) + 0.25
            x = np.sin(2 * np.pi * f * t + idx * np.sin(2 * np.pi * f * t))
            x += 0.12 * np.sin(2 * np.pi * f * 14 * t) * np.exp(-t * 18)   # "tine"
            env = np.exp(-t * 1.6) * np.minimum(1, t * 400) * np.minimum(1, (secs - t) * 12).clip(0)
        elif kind == "piano":  # piano: parciais levemente inarmônicos, os agudos morrem antes
            x = sum(np.sin(2 * np.pi * f * h * (1 + 0.0004 * h * h) * t) * np.exp(-t * (1.2 + h * 0.9)) / h ** 1.1 for h in (1, 2, 3, 4, 5, 6))
            x += sosfilt(sos("highpass", 2000), self.rng.uniform(-1, 1, L)) * np.exp(-t * 80) * 0.05   # martelo
            env = np.minimum(1, t * 600)
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
            # sub (seno) + serra filtrada com envelope (baixo de produção atual)
            saw = 2 * ((t * hz(n)) % 1.0) - 1
            x = np.sin(2 * np.pi * hz(n) * t) + 0.45 * sosfilt(sos("lowpass", 500), saw) * (0.5 + 0.5 * np.exp(-t * 8))
            x = np.tanh(x * 1.4)
            x *= np.minimum(1, t * 200) * np.exp(-t * 1.5) * np.minimum(1, (dur * E - t) * 30).clip(0)
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
                # lead atual: serras desafinadas filtradas, ataque macio e um vibrato tardio
                vib = 1 + 0.003 * np.sin(2 * np.pi * 5.2 * t) * np.clip(t * 1.5 - 0.2, 0, 1)
                fr = hz(n) * vib
                x = np.zeros(L)
                for dt_ in (-0.007, 0.0, 0.007):
                    ph = np.cumsum(fr * (1 + dt_)) / SR + self.rng.random()
                    x += 2 * (ph % 1.0) - 1
                x = sosfilt(sos("lowpass", 2600), x / 3) + 0.25 * np.sin(2 * np.pi * np.cumsum(fr) / SR)
                x *= np.minimum(1, t * 25) * np.minimum(1, (d * E - t) * 10).clip(0)
                self.put("lead", x * 0.16, pos)
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
        n = len(tr["drums"])
        # sidechain: harmonia e baixo "respiram" a cada bumbo (o bombeado do pop/eletrônico atual)
        duck = np.ones(n)
        rel = int(0.22 * SR)
        shape = 1 - 0.55 * np.exp(-np.arange(rel) / SR / 0.07)
        for t0, g in self.kicks:
            i = int(t0 * SR)
            if i >= n: continue
            m = min(rel, n - i)
            duck[i:i + m] = np.minimum(duck[i:i + m], 1 - (1 - shape[:m]) * min(1.0, g))
        # reverb: sala grande sintética (ruído com decaimento, diferente em cada canal)
        Lr = int(1.6 * SR)
        tt = np.arange(Lr) / SR
        irL = self.rng.normal(0, 1, Lr) * np.exp(-tt * 3.2)
        irR = self.rng.normal(0, 1, Lr) * np.exp(-tt * 3.2)
        irL[: int(0.012 * SR)] = 0; irR[: int(0.017 * SR)] = 0
        irL /= np.sqrt((irL ** 2).sum()); irR /= np.sqrt((irR ** 2).sum())
        dr = norm(tr["drums"]) * 0.62
        h1, h2 = norm(tr["harm"]) * 0.3 * duck, norm(tr["harm2"]) * 0.24 * duck
        bs = norm(sosfilt(sos("lowpass", 900), tr["bass"])) * 0.44 * (0.4 + 0.6 * duck)
        ld = norm(tr["lead"]) * 0.26
        dly = int(self.E * 3 * SR)
        echoL = lfilter([1], [1] + [0] * dly + [-0.3], ld)
        echoR = np.roll(echoL, int(self.E * SR))
        send = h2 * 0.8 + ld * 0.9 + sosfilt(sos("highpass", 300), dr) * 0.25 + h1 * 0.3
        rvL = fftconvolve(send, irL)[:n] * 0.22
        rvR = fftconvolve(send, irR)[:n] * 0.22
        # estéreo: harmonias abertas (atraso curto entre canais), baixo e bumbo no centro
        L = dr + bs + h1 + np.roll(h2, int(0.009 * SR)) + ld + echoL * 0.35 + rvL
        R = dr + bs + np.roll(h1, int(0.013 * SR)) + h2 + ld + echoR * 0.35 + rvR
        tail = int(3 * SR)
        for ch in (L, R):
            ch[:tail] += ch[self.LEN:self.LEN + tail]
        mix = np.stack([L[:self.LEN], R[:self.LEN]], 1)
        # master: brilho (prateleira de agudos) e cola (saturação leve)
        for c in range(2):
            mix[:, c] += sosfilt(sos("highpass", 6000), mix[:, c]) * 0.25
        mix = np.tanh(mix * 1.25) / np.tanh(1.25)
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
