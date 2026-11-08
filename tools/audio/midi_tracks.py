"""12 músicas originais do Craquez com instrumentos reais (amostras do banco General MIDI FluidR3)
em vez de síntese pura: compõe cada faixa em MIDI (bateria, baixo, harmonia, pad e melodia, com
humanização de tempo e dinâmica), renderiza com o FluidSynth e mixa (compressão, saturação leve,
abertura estéreo e limitador), para soar como música produzida, não como trilha de jogo antigo.

uso: python3 tools/audio/midi_tracks.py pasta_saida [soundfont.sf2]
     (precisa de fluidsynth e ffmpeg; gera track_01.ogg .. track_12.ogg)
"""
import os, sys, struct, subprocess, random, wave
import numpy as np

OUT = sys.argv[1] if len(sys.argv) > 1 else "tracks"
SF2 = sys.argv[2] if len(sys.argv) > 2 else "/usr/share/sounds/sf2/FluidR3_GM.sf2"
TPB = 480
os.makedirs(OUT, exist_ok=True)


# ------------------------------------------------------------------ MIDI
def vlq(n):
    b = [n & 0x7F]
    n >>= 7
    while n:
        b.append((n & 0x7F) | 0x80)
        n >>= 7
    return bytes(reversed(b))


class Track:
    def __init__(self):
        self.ev = []   # (tick, ordem, bytes)

    def add(self, tick, data, order=1):
        self.ev.append((max(0, int(tick)), order, data))

    def note(self, ch, tick, dur, note, vel):
        if not 0 <= note <= 127:
            return
        vel = max(1, min(127, int(vel)))
        self.add(tick, bytes([0x90 | ch, note, vel]), 2)
        self.add(tick + max(10, int(dur)), bytes([0x80 | ch, note, 0]), 0)

    def bytes(self):
        out = b""
        last = 0
        for t, _, d in sorted(self.ev, key=lambda e: (e[0], e[1])):
            out += vlq(t - last) + d
            last = t
        out += vlq(0) + b"\xff\x2f\x00"
        return b"MTrk" + struct.pack(">I", len(out)) + out


def write_midi(path, tracks):
    data = b"MThd" + struct.pack(">IHHH", 6, 1, len(tracks), TPB)
    for t in tracks:
        data += t.bytes()
    open(path, "wb").write(data)


# ------------------------------------------------------------------ teoria
SCALES = {"maj": [0, 2, 4, 5, 7, 9, 11], "min": [0, 2, 3, 5, 7, 8, 10], "dor": [0, 2, 3, 5, 7, 9, 10], "mix": [0, 2, 4, 5, 7, 9, 10]}


def chord(root, quality):
    q = {"M": [0, 4, 7], "m": [0, 3, 7], "M7": [0, 4, 7, 11], "m7": [0, 3, 7, 10], "7": [0, 4, 7, 10], "sus": [0, 5, 7], "add9": [0, 4, 7, 14], "m9": [0, 3, 7, 10, 14]}[quality]
    return [root + i for i in q]


def degree_chord(key, mode, deg, seventh=False):
    sc = SCALES[mode]
    r = key + sc[deg % 7] + 12 * (deg // 7)
    third = sc[(deg + 2) % 7] - sc[deg % 7]
    if third < 0:
        third += 12
    fifth = sc[(deg + 4) % 7] - sc[deg % 7]
    if fifth < 0:
        fifth += 12
    notes = [r, r + third, r + fifth]
    if seventh:
        sv = sc[(deg + 6) % 7] - sc[deg % 7]
        if sv < 0:
            sv += 12
        notes.append(r + sv)
    return notes


# ------------------------------------------------------------------ estilos
# bateria GM: 36 bumbo, 38 caixa, 39 palmas, 42 chimbal fechado, 46 aberto, 49 prato, 51 condução,
# 41/43/45 tons, 54 pandeiro, 69 cabasa, 70 maracas, 56 cowbell, 37 aro, 75 claves, 64/63 congas
def P(s):
    """'x.x.' -> lista de velocidades (x forte, o médio, . nada) em semicolcheias"""
    return [{"x": 110, "X": 125, "o": 75, "g": 45, ".": 0}[c] for c in s]


STYLES = [
    dict(name="Arquibancada", bpm=128, key=52, mode="maj", prog=[0, 4, 5, 3], chorus=[3, 0, 4, 5], swing=0,
         drums={36: P("x.......x.x....."), 38: P("....x.......x..."), 42: P("o.o.o.o.o.o.o.o."), 49: None},
         bass=dict(prog=34, pat="8th"), comp=dict(prog=29, pat="power8", vol=92, oct=0), comp2=dict(prog=0, pat="block", vol=60),
         pad=dict(prog=48, vol=55), lead=dict(prog=30, vol=88, oct=12), reverb=60),
    dict(name="Ginga", bpm=112, key=50, mode="dor", prog=[0, 3, 0, 4], chorus=[3, 4, 0, 0], swing=0.06,
         drums={36: P("x.....x...x....."), 38: P("....x.......x..o"), 42: P("oxoxoxoxoxoxoxox"), 54: P("..o...o...o...o.")},
         bass=dict(prog=36, pat="funk"), comp=dict(prog=27, pat="chop", vol=80, oct=12), comp2=dict(prog=61, pat="stab", vol=82),
         pad=dict(prog=4, vol=58), lead=dict(prog=65, vol=90, oct=12), reverb=45),
    dict(name="Noite na Cidade", bpm=124, key=53, mode="min", prog=[0, 5, 2, 6], chorus=[5, 6, 0, 4], swing=0,
         drums={36: P("x...x...x...x..."), 39: P("....x.......x..."), 42: P("..o...o...o...o."), 46: P("..g...g...g...g."), 69: P("gggggggggggggggg")},
         bass=dict(prog=38, pat="offbeat"), comp=dict(prog=0, pat="house", vol=82, oct=12), comp2=dict(prog=89, pat="pad", vol=60),
         pad=dict(prog=50, vol=50), lead=dict(prog=81, vol=72, oct=12), reverb=70),
    dict(name="Calor", bpm=96, key=55, mode="min", prog=[0, 5, 2, 6], chorus=[5, 2, 6, 0], swing=0,
         drums={36: P("x..x..x.x..x..x."), 38: P("...x..x....x..x."), 42: P("o.o.o.o.o.o.o.o."), 75: P("..o..o....o..o..")},
         bass=dict(prog=38, pat="dembow"), comp=dict(prog=24, pat="arp", vol=78, oct=12), comp2=dict(prog=5, pat="block", vol=55),
         pad=dict(prog=91, vol=50), lead=dict(prog=80, vol=72, oct=12), reverb=55),
    dict(name="Garagem", bpm=142, key=57, mode="maj", prog=[0, 5, 3, 4], chorus=[3, 4, 5, 0], swing=0,
         drums={36: P("x.....x.x......."), 38: P("....x.......x..."), 42: P("x.x.x.x.x.x.x.x."), 49: None},
         bass=dict(prog=33, pat="8th"), comp=dict(prog=29, pat="power8", vol=88, oct=0), comp2=dict(prog=27, pat="arp", vol=65),
         pad=dict(prog=49, vol=45), lead=dict(prog=27, vol=82, oct=12), reverb=50),
    dict(name="Concentração", bpm=88, key=49, mode="min", prog=[0, 5, 3, 4], chorus=[5, 3, 0, 4], swing=0.1,
         drums={36: P("x......x..x....."), 38: P("....x.......x..."), 42: P("oooooooooooooooo"), 46: P("..........g.....")},
         bass=dict(prog=38, pat="long"), comp=dict(prog=0, pat="lofi", vol=78, oct=12), comp2=dict(prog=49, pat="pad", vol=58),
         pad=dict(prog=88, vol=45), lead=dict(prog=11, vol=80, oct=24), reverb=75),
    dict(name="Roda", bpm=100, key=48, mode="maj", prog=[0, 5, 1, 4], chorus=[3, 4, 2, 5], swing=0.08,
         drums={41: P("x.......x......."), 43: P("......o.......o."), 54: P("xoxoxoxoxoxoxoxo"), 65: P("x.xx.x.xx.x.x.xx"), 38: P("....g.......g...")},
         bass=dict(prog=32, pat="samba"), comp=dict(prog=24, pat="chop", vol=82, oct=12), comp2=dict(prog=105, pat="arp", vol=60),
         pad=dict(prog=61, vol=55), lead=dict(prog=73, vol=80, oct=12), reverb=50),
    dict(name="Festival", bpm=128, key=51, mode="min", prog=[5, 3, 0, 4], chorus=[5, 3, 0, 4], swing=0,
         drums={36: P("x...x...x...x..."), 39: P("....x.......x..."), 42: P("..x...x...x...x."), 46: P("..o...o...o...o.")},
         bass=dict(prog=39, pat="offbeat"), comp=dict(prog=81, pat="pluck", vol=70, oct=12), comp2=dict(prog=90, pat="pad", vol=62),
         pad=dict(prog=50, vol=55), lead=dict(prog=81, vol=78, oct=24), reverb=70),
    dict(name="Contra-Ataque", bpm=172, key=52, mode="maj", prog=[0, 4, 5, 3], chorus=[3, 0, 4, 5], swing=0,
         drums={36: P("x.....x.x.....x."), 38: P("....x.......x..."), 42: P("x.x.x.x.x.x.x.x."), 49: None},
         bass=dict(prog=34, pat="8th"), comp=dict(prog=30, pat="power8", vol=90, oct=0), comp2=dict(prog=29, pat="power8", vol=70),
         pad=dict(prog=49, vol=40), lead=dict(prog=29, vol=84, oct=12), reverb=45),
    dict(name="Horizonte", bpm=100, key=54, mode="maj", prog=[3, 4, 2, 5], chorus=[3, 4, 5, 0], swing=0,
         drums={36: P("x.......x.x....."), 38: P("........x......."), 42: P("o.o.o.o.o.o.o.o."), 39: P("........x.......")},
         bass=dict(prog=38, pat="long"), comp=dict(prog=4, pat="block", vol=70, oct=12), comp2=dict(prog=90, pat="pad", vol=66),
         pad=dict(prog=95, vol=55), lead=dict(prog=82, vol=74, oct=12), reverb=80),
    dict(name="Raça", bpm=104, key=40, mode="min", prog=[0, 5, 6, 4], chorus=[0, 6, 5, 4], swing=0,
         drums={36: P("x.x...x.x.x....."), 38: P("....x.......x..."), 42: P("x.x.x.x.x.x.x.x."), 49: None},
         bass=dict(prog=34, pat="8th"), comp=dict(prog=30, pat="power8", vol=95, oct=0), comp2=dict(prog=30, pat="power8", vol=78),
         pad=dict(prog=48, vol=50), lead=dict(prog=30, vol=86, oct=12), reverb=55),
    dict(name="Sol", bpm=108, key=55, mode="maj", prog=[0, 3, 4, 3], chorus=[5, 3, 0, 4], swing=0.05,
         drums={36: P("x.....x...x....."), 38: P("......x.......x."), 42: P("o.oo.o.oo.oo.o.o"), 70: P("gggggggggggggggg"), 63: P("..o..o.o..o..o.o")},
         bass=dict(prog=33, pat="funk"), comp=dict(prog=12, pat="arp", vol=80, oct=12), comp2=dict(prog=26, pat="chop", vol=70),
         pad=dict(prog=89, vol=50), lead=dict(prog=56, vol=80, oct=12), reverb=55),
]

FORM = [("intro", 4), ("verso", 8), ("pre", 4), ("refrao", 8), ("verso", 8), ("refrao", 8), ("ponte", 8), ("refrao", 8), ("final", 4)]


def compose(st, seed):
    rnd = random.Random(seed)
    bar = TPB * 4
    six = TPB // 4
    tr = {k: Track() for k in ("meta", "drums", "bass", "comp", "comp2", "pad", "lead")}
    tr["meta"].add(0, b"\xff\x51\x03" + struct.pack(">I", int(60_000_000 / st["bpm"]))[1:])
    chans = {"bass": 1, "comp": 2, "comp2": 3, "pad": 4, "lead": 5}
    progs = {"bass": st["bass"]["prog"], "comp": st["comp"]["prog"], "comp2": st["comp2"]["prog"], "pad": st["pad"]["prog"], "lead": st["lead"]["prog"]}
    vols = {"bass": 100, "comp": st["comp"]["vol"], "comp2": st["comp2"]["vol"], "pad": st["pad"]["vol"], "lead": st["lead"]["vol"]}
    pans = {"bass": 64, "comp": 40, "comp2": 88, "pad": 64, "lead": 70}
    for k, ch in chans.items():
        t = tr[k]
        t.add(0, bytes([0xC0 | ch, progs[k]]))
        t.add(0, bytes([0xB0 | ch, 7, vols[k]]))
        t.add(0, bytes([0xB0 | ch, 10, pans[k]]))
        t.add(0, bytes([0xB0 | ch, 91, st["reverb"]]))
        t.add(0, bytes([0xB0 | ch, 93, 30 if k in ("pad", "comp2") else 10]))
    tr["drums"].add(0, bytes([0xB9, 7, 112]))
    tr["drums"].add(0, bytes([0xB9, 91, 35]))
    key, mode = st["key"], st["mode"]
    sc = SCALES[mode]
    scale_notes = [key + 12 * o + s for o in range(-2, 4) for s in sc]

    def hum(t, amt=8):   # humanização: ~±8 ms
        return t + rnd.randint(-amt, amt)

    def swung(step):
        t = step * six
        if st["swing"] and step % 2 == 1:
            t += int(six * st["swing"] * 2)
        return t

    # motivo da melodia (ritmo + contorno), reaproveitado com variações: dá identidade à música
    def motif(length_steps, dense):
        rhythm = []
        s = 0
        while s < length_steps:
            d = rnd.choice([2, 2, 4, 4, 6, 8] if not dense else [1, 2, 2, 2, 3, 4])
            rhythm.append((s, min(d, length_steps - s)))
            s += d
        contour = [rnd.choice([-2, -1, -1, 0, 1, 1, 2, 3]) for _ in rhythm]
        return rhythm, contour

    verse_m = motif(32, False)
    chorus_m = motif(32, False)
    bridge_m = motif(32, True)
    t0 = 0
    total_bars = sum(b for _, b in FORM)
    bar_i = 0
    for sec, bars in FORM:
        prog = st["chorus"] if sec == "refrao" else st["prog"] if sec != "ponte" else [st["prog"][2], st["prog"][3], st["prog"][0], st["prog"][1]]
        energy = {"intro": 0.55, "verso": 0.7, "pre": 0.8, "refrao": 1.0, "ponte": 0.75, "final": 0.6}[sec]
        for b in range(bars):
            bt = t0 + b * bar
            deg = prog[b % len(prog)]
            ch_notes = degree_chord(key, mode, deg, seventh=st["name"] in ("Noite na Cidade", "Concentração", "Horizonte", "Ginga", "Sol"))
            root = ch_notes[0]
            last_bar = b == bars - 1
            # ---------------- bateria
            if not (sec == "intro" and b < 2 and st["name"] not in ("Roda",)):
                for inst, pat in st["drums"].items():
                    if pat is None:
                        continue
                    for s in range(16):
                        v = pat[s]
                        if not v:
                            continue
                        if sec == "ponte" and inst in (36,) and s % 8 != 0:
                            continue
                        vv = v * (0.75 + 0.25 * energy) * rnd.uniform(0.88, 1.05)
                        tr["drums"].note(9, hum(bt + swung(s), 6), six, inst, vv)
                # prato no começo das seções e virada no fim
                if b == 0 and sec in ("refrao", "verso", "final", "pre"):
                    tr["drums"].note(9, bt, TPB * 2, 49, 105)
                if 49 in st["drums"] and sec == "refrao":
                    for s in range(0, 16, 2):
                        tr["drums"].note(9, hum(bt + swung(s), 5), six, 51, 70 * rnd.uniform(0.9, 1.05))
                if last_bar and sec != "final":
                    for i, s in enumerate(range(12, 16)):
                        tr["drums"].note(9, hum(bt + s * six, 5), six, [45, 43, 41, 38][i] if rnd.random() < 0.6 else 38, 80 + i * 10)
            # ---------------- baixo
            bp = st["bass"]["pat"]
            bn = root - 24 if root - 24 >= 28 else root - 12
            fifth = bn + 7
            if bp == "8th":
                for e in range(8):
                    n = bn if e < 6 or rnd.random() < 0.6 else (fifth if e == 6 else bn + 12)
                    tr["bass"].note(1, hum(bt + e * TPB // 2), TPB // 2 - 30, n, 95 * energy + rnd.randint(-6, 6))
            elif bp == "funk":
                for s, n, v in [(0, bn, 110), (3, bn + 12, 80), (6, bn, 95), (8, fifth, 90), (10, bn + 10, 85), (11, bn + 12, 75), (14, bn, 90)]:
                    tr["bass"].note(1, hum(bt + swung(s)), six * 1.6, n, v * energy)
            elif bp == "offbeat":
                for e in range(4):
                    tr["bass"].note(1, hum(bt + e * TPB + TPB // 2, 4), TPB // 2 - 20, bn + (12 if e == 3 and rnd.random() < 0.4 else 0), 100 * energy)
            elif bp == "dembow":
                for s in (0, 3, 6, 8, 11, 14):
                    tr["bass"].note(1, hum(bt + s * six), six * 2.5, bn, 100 * energy)
            elif bp == "samba":
                for s, n in [(0, bn), (3, bn), (4, fifth), (8, bn), (11, bn), (12, fifth - 12)]:
                    tr["bass"].note(1, hum(bt + swung(s)), six * 2, n, 95 * energy)
            else:   # long
                tr["bass"].note(1, hum(bt), TPB * 2.5, bn, 100 * energy)
                tr["bass"].note(1, hum(bt + TPB * 3 + TPB // 2), TPB // 2, bn + 7, 80 * energy)
            # ---------------- harmonia
            for part in ("comp", "comp2"):
                cfg = st[part]
                pat = cfg["pat"]
                if part == "comp2" and sec in ("intro", "verso") and pat not in ("pad",):
                    continue
                ch = chans[part]
                voicing = [n + 12 for n in ch_notes] if cfg.get("oct", 0) else ch_notes
                while voicing[0] < 52:
                    voicing = [n + 12 for n in voicing]
                while voicing[0] > 66:
                    voicing = [n - 12 for n in voicing]
                if pat == "power8":
                    pw = [root - 12 + 12 * (root < 52), root - 12 + 12 * (root < 52) + 7, root + 12 * (root < 52)]
                    for e in range(8):
                        if sec == "verso" and part == "comp" and e % 2 == 1:
                            continue
                        for i, n in enumerate(pw):
                            tr[part].note(ch, hum(bt + e * TPB // 2 + i * 6), TPB // 2 - 40, n, (95 if e % 2 == 0 else 80) * energy)
                elif pat == "block":
                    for beat in (0, 2):
                        for n in voicing:
                            tr[part].note(ch, hum(bt + beat * TPB), TPB * 2 - 40, n, 70 * energy)
                elif pat == "chop":
                    for s in (2, 6, 10, 14) if rnd.random() < 0.8 else (2, 5, 10, 13):
                        for i, n in enumerate(voicing):
                            tr[part].note(ch, hum(bt + swung(s) + i * 4), six * 0.9, n, 85 * energy)
                elif pat == "stab":
                    if sec in ("refrao", "ponte", "pre"):
                        for s in ((0, 3, 10) if b % 2 == 0 else (6, 12)):
                            for n in voicing:
                                tr[part].note(ch, hum(bt + swung(s)), six * 1.8, n, 95 * energy)
                elif pat == "house":
                    for s in (2, 5, 8, 11, 14) if b % 2 else (2, 6, 10, 14):
                        for n in voicing:
                            tr[part].note(ch, hum(bt + s * six, 4), six * 1.4, n, 88 * energy)
                elif pat == "arp":
                    seq = voicing + [voicing[1] + 12]
                    for s in range(16 if energy > 0.7 else 8):
                        step = s if energy > 0.7 else s * 2
                        tr[part].note(ch, hum(bt + swung(step)), six * (1.8 if energy > 0.7 else 3.5), seq[(s * 2 + b) % len(seq)], (78 if s % 4 == 0 else 62) * energy)
                elif pat == "pluck":
                    for s in range(0, 16, 2):
                        tr[part].note(ch, hum(bt + s * six, 3), six * 1.2, voicing[(s // 2) % len(voicing)] + 12, (90 if s % 4 == 0 else 70) * energy)
                elif pat == "lofi":
                    for i, n in enumerate(voicing + [voicing[0] + 12]):
                        tr[part].note(ch, hum(bt + i * 25, 12), TPB * 3.6, n, 62 * energy + rnd.randint(-8, 8))
                else:   # pad
                    for n in voicing:
                        tr[part].note(ch, bt, bar - 20, n, 60 * energy)
            # pad longo por baixo (cola tudo)
            if sec in ("refrao", "ponte", "final", "pre"):
                for n in ch_notes:
                    tr["pad"].note(4, bt, bar - 20, n + (12 if n < 55 else 0), 55 * energy)
        # ---------------- melodia: motivo do verso/refrão/ponte, frase a cada 2 compassos
        if sec in ("verso", "refrao", "ponte"):
            mot = verse_m if sec == "verso" else chorus_m if sec == "refrao" else bridge_m
            rhythm, contour = mot
            base_oct = st["lead"]["oct"] + (12 if sec == "refrao" else 0)
            for phrase in range(bars // 2):
                pt = t0 + phrase * 2 * bar
                deg0 = prog[(phrase * 2) % len(prog)]
                ch0 = degree_chord(key, mode, deg0)
                # começa numa nota do acorde e segue o contorno pela escala
                start = ch0[rnd.choice([0, 1, 2])] + base_oct
                idx = min(range(len(scale_notes)), key=lambda i: abs(scale_notes[i] - start))
                for k, (s, d) in enumerate(rhythm):
                    if phrase % 2 == 1 and k >= len(rhythm) - 2 and rnd.random() < 0.6:
                        continue   # resposta: deixa respirar no fim
                    idx = max(0, min(len(scale_notes) - 1, idx + contour[k] + (1 if phrase % 4 == 2 and k == 0 else 0)))
                    n = scale_notes[idx]
                    # nos tempos fortes, puxa para uma nota do acorde do momento
                    beat_bar = (phrase * 2 + s // 16) % len(prog)
                    chn = degree_chord(key, mode, prog[beat_bar])
                    if s % 4 == 0:
                        n = min([c + 12 * o for c in chn for o in range(-1, 4)], key=lambda c: abs(c - n))
                    vel = (96 if s % 4 == 0 else 82) + rnd.randint(-6, 6)
                    tr["lead"].note(5, hum(pt + swung(s % 16) + (s // 16) * bar), d * six - 25, n, vel)
        t0 += bars * bar
        bar_i += bars
    length_ticks = t0 + 2 * bar
    return [tr["meta"], tr["drums"], tr["bass"], tr["comp"], tr["comp2"], tr["pad"], tr["lead"]], length_ticks * 60.0 / (st["bpm"] * TPB)


# ------------------------------------------------------------------ mixagem
def read_wav(p):
    with wave.open(p) as w:
        n, ch, sr = w.getnframes(), w.getnchannels(), w.getframerate()
        x = np.frombuffer(w.readframes(n), dtype="<i2").astype(np.float32) / 32768.0
    return x.reshape(-1, ch), sr


def master(x, sr):
    from scipy.signal import butter, sosfilt
    x = sosfilt(butter(2, 30, "highpass", fs=sr, output="sos"), x, axis=0)
    # compressor de barramento (cola a mixagem): detector RMS lento
    env = np.sqrt(sosfilt(butter(1, 8, "lowpass", fs=sr, output="sos"), (x ** 2).mean(1)) + 1e-9)
    thr = np.percentile(env, 60)
    gain = np.where(env > thr, (thr / env) ** (1 - 1 / 2.5), 1.0)
    x = x * gain[:, None]
    # saturação de fita leve + brilho
    x = np.tanh(x * 1.25) / 1.25
    air = sosfilt(butter(2, 9000, "highpass", fs=sr, output="sos"), x, axis=0)
    x = x + air * 0.08
    # abertura estéreo (mid/side)
    m, s = (x[:, 0] + x[:, 1]) / 2, (x[:, 0] - x[:, 1]) / 2
    x = np.stack([m + s * 1.35, m - s * 1.35], 1)
    # limitador: normaliza e corta picos
    x = x / (np.percentile(np.abs(x), 99.9) + 1e-9) * 0.82
    x = np.tanh(x * 1.2) / np.tanh(1.2)
    # fade no fim
    f = int(sr * 3)
    x[-f:] *= np.linspace(1, 0, f)[:, None]
    return x


for i, st in enumerate(STYLES):
    tracks, secs = compose(st, 1000 + i * 77)
    mid = os.path.join(OUT, f"track_{i + 1:02d}.mid")
    wav = os.path.join(OUT, f"track_{i + 1:02d}.wav")
    write_midi(mid, tracks)
    subprocess.run(["fluidsynth", "-ni", "-g", "0.6", "-r", "44100", "-F", wav, SF2, mid], check=True, capture_output=True)
    x, sr = read_wav(wav)
    x = master(x, sr)
    with wave.open(wav, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())
    ogg = os.path.join(OUT, f"track_{i + 1:02d}.ogg")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", wav, "-c:a", "libvorbis", "-q:a", "3", ogg], check=True)
    os.remove(wav)
    print(f"{ogg}  {st['name']}  {st['bpm']} bpm  {secs:.0f} s", flush=True)
