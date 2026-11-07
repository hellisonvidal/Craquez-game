"""Sons reais de torcida (gravações com licença livre da Wikimedia Commons) para o Craquez.

Baixa as gravações, escolhe os trechos certos pela análise de volume e grava em
Assets/Craquez/Resources/Audio/ (OGG):
  crowd_loop.ogg   ambiente de estádio (loop sem emenda, ~40 s)
  crowd_loop2.ogg  segunda camada de ambiente (multidão grande, ~30 s)
  cheer.ogg        explosão de gol (aplauso + gritos + cornetas)
  applause.ogg     aplauso curto (boa jogada, defesa)
  boo.ogg          vaia
  hurray.ogg       grito curto de comemoração
Fontes e licenças em CREDITS.md. Precisa de ffmpeg e numpy.
uso: python3 tools/audio/fetch_crowd.py
"""
import hashlib, os, subprocess, sys, time, urllib.parse, urllib.request, wave
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Craquez", "Resources", "Audio")
TMP = os.path.join(ROOT, "Temp", "crowd_src")
UA = {"User-Agent": "CraquezGame/1.0 (https://github.com/hellisonvidal/craquez-game) asset-fetch"}
SR = 44100

SOURCES = {
    # arquivo local: (nome na Commons, licença)
    "football": ("WWS FootballAustriavs.Sweden.ogg", "CC BY 4.0"),
    "large": ("360703 eguobyte large-crowd-medium-distance-stereo.wav", "CC0"),
    "crowd2": ("211146 unfa another-crowd.flac", "CC0"),
    "cheer": ("F1-Race-Crowd-Applause-Air-Horns.wav", "CC BY 4.0"),
    "applause": ("Sound Effects - Applause after a concert.ogg", "CC0"),
    "boo": ("Soundgoats - Audience Booing.wav", "Public domain"),
    "hurray": ("Clapping hurray.ogg", "Public domain"),
}


def url_of(name):
    n = name.replace(" ", "_")
    h = hashlib.md5(n.encode()).hexdigest()
    return f"https://upload.wikimedia.org/wikipedia/commons/{h[0]}/{h[:2]}/{urllib.parse.quote(n)}"


def fetch(key):
    name, _ = SOURCES[key]
    ext = os.path.splitext(name)[1]
    path = os.path.join(TMP, key + ext)
    if os.path.exists(path) and os.path.getsize(path) > 1000:
        return path
    for i in range(6):
        try:
            data = urllib.request.urlopen(urllib.request.Request(url_of(name), headers=UA), timeout=180).read()
            open(path, "wb").write(data)
            print("baixado", key, len(data) // 1024, "KB", flush=True)
            time.sleep(2)
            return path
        except Exception as e:
            print("tentativa", i + 1, key, str(e)[:80], flush=True)
            time.sleep(10 * (i + 1))
    raise SystemExit("não consegui baixar " + name)


def decode(path):
    """Decodifica para estéreo 44,1 kHz float."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "2", "-ar", str(SR), "-f", "s16le", "-"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, np.int16).reshape(-1, 2).astype(np.float32) / 32768.0


def rms_track(x, win=0.5):
    n = int(win * SR)
    m = x.mean(1)
    k = len(m) // n
    return np.sqrt((m[: k * n].reshape(k, n) ** 2).mean(1) + 1e-12), n


def steadiest(x, seconds):
    """Trecho de 'seconds' com volume mais constante (e não muito baixo): bom para loop."""
    r, n = rms_track(x)
    w = int(seconds * SR / n)
    if len(r) <= w:
        return x
    best, bi = 1e9, 0
    med = np.median(r)
    for i in range(0, len(r) - w):
        seg = r[i:i + w]
        score = seg.std() / (seg.mean() + 1e-6) + (0.5 if seg.mean() < med * 0.6 else 0)
        if score < best:
            best, bi = score, i
    return x[bi * n: bi * n + int(seconds * SR)]


def loudest(x, seconds):
    r, n = rms_track(x, 0.25)
    w = max(1, int(seconds * SR / n))
    c = np.convolve(r, np.ones(w), "valid")
    i = int(np.argmax(c))
    s = max(0, i * n - int(0.3 * SR))
    return x[s: s + int(seconds * SR)]


def seamless(x, fade=2.0):
    """Loop sem emenda: o fim se funde no começo (crossfade de potência constante)."""
    f = int(fade * SR)
    a, b = x[:f], x[-f:]
    t = np.linspace(0, 1, f)[:, None]
    mixed = b * np.cos(t * np.pi / 2) + a * np.sin(t * np.pi / 2)
    return np.concatenate([mixed, x[f:-f]])


def normalize(x, peak=0.89, rms=None):
    if rms is not None:
        cur = np.sqrt((x ** 2).mean()) + 1e-9
        x = x * (rms / cur)
    m = np.abs(x).max() + 1e-9
    if m > peak:
        x = x * (peak / m)
    return x


def envelope(x, fin=0.02, fout=0.4):
    n = len(x)
    e = np.ones(n)
    a, b = int(fin * SR), int(fout * SR)
    if a: e[:a] = np.linspace(0, 1, a)
    if b: e[-b:] = np.linspace(1, 0, b)
    return x * e[:, None]


def highpass(x, hz=60):
    """Tira o ronco grave (vento/manuseio do microfone)."""
    from scipy.signal import lfilter
    a = np.exp(-2 * np.pi * hz / SR)
    return lfilter([1, -1], [1, -a], x, axis=0).astype(np.float32)


def write_ogg(name, x):
    os.makedirs(OUT, exist_ok=True)
    wav = os.path.join(TMP, name + ".wav")
    w = wave.open(wav, "wb"); w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes()); w.close()
    out = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", wav, "-c:a", "libvorbis", "-q:a", "4", out], check=True)
    print("gravado", out, os.path.getsize(out) // 1024, "KB", flush=True)


def main():
    os.makedirs(TMP, exist_ok=True)
    # ambiente: partida de futebol real + multidão grande ao fundo
    fb = highpass(decode(fetch("football")))
    lg = highpass(decode(fetch("large")))
    bed = steadiest(fb, 44.0)
    layer = steadiest(lg, 44.0)
    n = min(len(bed), len(layer))
    amb = normalize(bed[:n], rms=0.12) * 0.8 + normalize(layer[:n], rms=0.12) * 0.45
    write_ogg("crowd_loop", normalize(seamless(amb), rms=0.14))
    c2 = highpass(decode(fetch("crowd2")))
    write_ogg("crowd_loop2", normalize(seamless(steadiest(c2, 34.0)), rms=0.13))
    # explosão de gol: trecho mais alto do aplauso com cornetas
    ch = decode(fetch("cheer"))
    write_ogg("cheer", envelope(normalize(loudest(ch, 6.5), rms=0.3), 0.05, 1.8))
    ap = decode(fetch("applause"))
    write_ogg("applause", envelope(normalize(loudest(ap, 3.2), rms=0.22), 0.08, 1.0))
    bo = decode(fetch("boo"))
    write_ogg("boo", envelope(normalize(loudest(bo, 3.5), rms=0.22), 0.15, 1.0))
    hu = decode(fetch("hurray"))
    write_ogg("hurray", envelope(normalize(loudest(hu, 2.5), rms=0.25), 0.02, 0.6))


if __name__ == "__main__":
    main()
