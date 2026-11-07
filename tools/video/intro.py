"""Vídeo de abertura do Craquez (gerado por código a partir da arte da capa).

Linha do tempo (30 qps, ~6,5 s):
  0,0-1,2  escuro -> estádio surge desfocado, faixa de luz atravessa a tela
  1,2-3,6  câmera avança sobre a arte (zoom 1,3 -> 1,0), brilho sobe, riscos de luz
  3,6-4,6  clarão na logo + faíscas
  4,6-6,5  assenta exatamente na capa parada (o jogo segura este quadro até apertar um botão)
uso: python3 tools/video/intro.py  (grava Assets/Craquez/Resources/UI/abertura.mp4; precisa de ffmpeg)
"""
import os, subprocess, numpy as np
from PIL import Image, ImageFilter, ImageEnhance

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Assets", "Craquez", "Resources", "UI", "capa_wide.jpg")
OUT = os.path.join(ROOT, "Assets", "Craquez", "Resources", "UI", "abertura.mp4")
W, H, FPS, DUR = 1920, 1080, 30, 6.5
rng = np.random.default_rng(7)

art = Image.open(SRC).convert("RGB")
aw, ah = art.size
# enquadramento final = o que o jogo mostra em 16:9 (capa panorâmica cortada nas laterais)
cw = ah * W / H
final_box = ((aw - cw) / 2, 0, (aw + cw) / 2, ah)
blur_art = art.filter(ImageFilter.GaussianBlur(18))
# área da logo na arte (para o clarão): terço superior, centro
logo_c = (aw * 0.5, ah * 0.2)


def smooth(x):
    x = min(1, max(0, x)); return x * x * (3 - 2 * x)


def frame(t):
    # zoom e leve deriva até o enquadramento final
    z = 1.0 + 0.3 * (1 - smooth((t - 0.6) / 3.6))
    cx = aw / 2 + (1 - smooth((t - 0.6) / 3.6)) * aw * 0.03
    cy = ah / 2 - (1 - smooth((t - 0.6) / 3.6)) * ah * 0.04
    bw, bh = cw / z, ah / z
    box = (cx - bw / 2, cy - bh / 2, cx + bw / 2, cy + bh / 2)
    sharp = smooth((t - 0.8) / 1.4)
    src = Image.blend(blur_art, art, sharp) if sharp < 1 else art
    img = src.resize((W, H), Image.BICUBIC, box=box)
    a = np.asarray(img).astype(np.float32) / 255.0
    # brilho: sobe do escuro
    a *= 0.08 + 0.92 * smooth(t / 1.6)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # faixa de luz diagonal atravessando (0,3 s - 1,6 s) e de novo, mais fraca, no clarão
    for t0, t1, k in ((0.3, 1.6, 1.0), (3.4, 4.4, 0.6)):
        if t0 <= t <= t1:
            u = (t - t0) / (t1 - t0)
            pos = -0.3 * W + u * 1.6 * W
            d = (xx + yy * 0.45) - pos
            band = np.exp(-(d / (W * 0.05)) ** 2) * k * np.sin(u * np.pi)
            a += band[..., None] * np.array([0.55, 0.75, 1.0])
    # riscos de luz horizontais (velocidade) durante o avanço
    if 1.0 < t < 4.0:
        s = np.sin((t - 1.0) / 3.0 * np.pi)
        for i in range(10):
            y0 = (i * 0.097 + 0.05) % 1 * H
            ph = ((t * (1.4 + i * 0.13) + i * 0.31) % 1.0)
            x0 = ph * 1.4 * W - 0.2 * W
            g = np.exp(-((yy - y0) / 2.2) ** 2) * np.exp(-((xx - x0) / (W * 0.12)) ** 2)
            a += g[..., None] * 0.35 * s * np.array([0.7, 0.85, 1.0])
    # clarão na logo
    lx = (logo_c[0] - final_box[0]) / cw * W
    ly = logo_c[1] / ah * H
    fl = np.exp(-((t - 3.95) / 0.28) ** 2)
    if fl > 0.01:
        r = np.sqrt(((xx - lx) / (W * 0.42)) ** 2 + ((yy - ly) / (H * 0.25)) ** 2)
        a += (np.exp(-r * r * 2.5) * fl * 0.9)[..., None]
    # faíscas saindo da logo
    if 3.8 < t < 5.4:
        age = t - 3.8
        for i in range(70):
            ang = rng.uniform(0, 2 * np.pi) if False else (i * 2.399) % (2 * np.pi)
            sp = 280 + (i * 37 % 400)
            px = lx + np.cos(ang) * sp * age * 1.2
            py = ly + np.sin(ang) * sp * age * 0.7 + 260 * age * age
            life = max(0.0, 1 - age / 1.6)
            if 0 <= px < W and 0 <= py < H and life > 0:
                x0, y0 = int(px), int(py)
                ys, xs = slice(max(0, y0 - 3), min(H, y0 + 4)), slice(max(0, x0 - 3), min(W, x0 + 4))
                a[ys, xs] += np.array([1.0, 0.85, 0.5]) * life
    a = np.clip(a, 0, 1)
    # vinheta suave que some no fim (o quadro final é a capa pura)
    v = 1 - smooth((t - 4.6) / 1.4)
    if v > 0:
        r = np.sqrt(((xx - W / 2) / (W * 0.7)) ** 2 + ((yy - H / 2) / (H * 0.7)) ** 2)
        a *= (1 - np.clip(r - 0.6, 0, 1) * 0.6 * v)[..., None]
    return (a * 255).astype(np.uint8)


n = int(DUR * FPS)
cmd = ["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
       "-c:v", "libx264", "-profile:v", "main", "-pix_fmt", "yuv420p", "-preset", "slow", "-crf", "20", "-movflags", "+faststart", OUT]
p = subprocess.Popen(cmd, stdin=subprocess.PIPE)
for i in range(n):
    p.stdin.write(frame(i / FPS).tobytes())
p.stdin.close(); p.wait()
print("gravado", OUT, os.path.getsize(OUT) // 1024, "KB")
