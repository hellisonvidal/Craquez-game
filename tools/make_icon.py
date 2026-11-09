"""Ícone do Craquez: o jogador da capa fazendo uma bicicleta, em silhueta com contorno de luz,
com a bola e o rastro, sobre o estádio iluminado. A silhueta e a bola vêm da própria arte da
capa (Assets/Craquez/Resources/UI/capa.jpg). Gera:
  Assets/Craquez/Icons/craquez_icon.png        (1024, fundo + jogador)
  Assets/Craquez/Icons/craquez_icon_fg.png     (1024, só o jogador e a bola — ícone adaptativo do Android)
  Assets/Craquez/Icons/craquez_icon_bg.png     (1024, só o fundo)
  tools/installer/craquez.ico                   (instalador do Windows)
  tools/installer/linux/craquez.png             (atalho do Linux, 256)
uso: python3 tools/make_icon.py"""
import os
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
N = 1024
cover = Image.open(os.path.join(ROOT, "Assets", "Craquez", "Resources", "UI", "capa.jpg")).convert("RGB")
A = np.asarray(cover).astype(np.float32)


def silhouette():
    """Máscara (0..1) do jogador: pixels escuros na região do corpo (o estádio é claro)."""
    x0, y0, x1, y1 = 230, 480, 1265, 975
    reg = A[y0:y1, x0:x1]
    lum = reg.mean(2)
    m = np.clip((70 - lum) / 35, 0, 1)
    img = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MedianFilter(5))
    # fica só o maior bloco (tira pontinhos escuros da torcida/grama)
    a = np.asarray(img) > 128
    from collections import deque
    lab = np.zeros(a.shape, np.int32); best, bl, cur = 0, 0, 0
    H, W = a.shape
    for sy in range(0, H, 3):
        for sx in range(0, W, 3):
            if a[sy, sx] and lab[sy, sx] == 0:
                cur += 1; q = deque([(sy, sx)]); lab[sy, sx] = cur; n = 0
                while q:
                    y, x = q.popleft(); n += 1
                    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        yy, xx = y + dy, x + dx
                        if 0 <= yy < H and 0 <= xx < W and a[yy, xx] and lab[yy, xx] == 0:
                            lab[yy, xx] = cur; q.append((yy, xx))
                if n > best: best, bl = n, cur
    keep = (lab == bl)
    soft = np.asarray(Image.fromarray((keep * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255
    return Image.fromarray((soft * 255).astype(np.uint8))


def ball():
    cx, cy, r = 1345, 440, 104
    crop = cover.crop((cx - r, cy - r, cx + r, cy + r)).convert("RGBA")
    m = Image.new("L", crop.size, 0)
    ImageDraw.Draw(m).ellipse((2, 2, 2 * r - 2, 2 * r - 2), fill=255)
    crop.putalpha(m.filter(ImageFilter.GaussianBlur(1)))
    return crop


def background():
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / N
    top, bot = np.array([10, 40, 120]), np.array([2, 8, 30])
    c = top[None, None] * (1 - yy[..., None]) + bot[None, None] * yy[..., None]
    g = np.exp(-(((xx - 0.5) / 0.45) ** 2 + ((yy - 0.48) / 0.4) ** 2))
    c += g[..., None] * np.array([40, 120, 230])
    # anel de luzes do estádio
    for i in range(26):
        t = i / 25
        lx = 0.04 + 0.92 * t; ly = 0.8 - 0.07 * np.sin(t * np.pi)
        d = ((xx - lx) ** 2 + (yy - ly) ** 2) / 0.00018
        c += np.exp(-d)[..., None] * np.array([255, 245, 220]) * 0.9
    # gramado embaixo (arco)
    arc = yy > 0.82 + 0.05 * ((xx - 0.5) * 2) ** 2
    c[arc] = c[arc] * 0.25 + np.array([30, 150, 50]) * 0.75
    img = Image.fromarray(np.clip(c, 0, 255).astype(np.uint8)).convert("RGBA")
    return img


def foreground():
    fg = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    sil = silhouette()
    w = 880; h = int(sil.height * w / sil.width)
    sil = sil.resize((w, h), Image.LANCZOS).rotate(-6, resample=Image.BICUBIC, expand=True)
    ox, oy = (N - sil.width) // 2 - 30, int(N * 0.56 - sil.height / 2)
    # contorno de luz (ciano) + sombra, depois o corpo escuro
    rim = sil.filter(ImageFilter.MaxFilter(13)).filter(ImageFilter.GaussianBlur(3))
    glow = sil.filter(ImageFilter.MaxFilter(25)).filter(ImageFilter.GaussianBlur(18))
    fg.paste((60, 200, 255, 255), (ox, oy), glow.point(lambda v: v * 0.55))
    fg.paste((150, 235, 255, 255), (ox, oy), rim)
    fg.paste((6, 10, 26, 255), (ox, oy), sil)
    # rastro da bola e a bola (canto superior direito, na frente do pé)
    tr = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(tr)
    bx, by, br = int(N * 0.79), int(N * 0.22), 118
    for k in range(18):
        t = k / 17
        d.ellipse((bx - br * (1 - t) - 300 * t, by - br * (1 - t * 0.6) + 120 * t, bx + br * (1 - t) - 300 * t, by + br * (1 - t * 0.6) + 120 * t), fill=int(150 * (1 - t)))
    tr = tr.filter(ImageFilter.GaussianBlur(22))
    fg.alpha_composite(Image.merge("RGBA", (Image.new("L", (N, N), 255), Image.new("L", (N, N), 230), Image.new("L", (N, N), 150), tr)))
    b = ball().resize((br * 2, br * 2), Image.LANCZOS)
    halo = Image.new("L", (N, N), 0)
    ImageDraw.Draw(halo).ellipse((bx - br - 30, by - br - 30, bx + br + 30, by + br + 30), fill=200)
    fg.alpha_composite(Image.merge("RGBA", (Image.new("L", (N, N), 255), Image.new("L", (N, N), 240), Image.new("L", (N, N), 200), halo.filter(ImageFilter.GaussianBlur(25)))))
    fg.alpha_composite(b, (bx - br, by - br))
    return fg


def main():
    bg = background()
    fg = foreground()
    full = bg.copy(); full.alpha_composite(fg)
    out = os.path.join(ROOT, "Assets", "Craquez", "Icons")
    full.save(os.path.join(out, "craquez_icon.png"))
    bg.save(os.path.join(out, "craquez_icon_bg.png"))
    # adaptativo: o Android corta ~1/3 das bordas — reduz o desenho para a zona segura
    safe = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    small = fg.resize((int(N * 0.74), int(N * 0.74)), Image.LANCZOS)
    safe.alpha_composite(small, ((N - small.width) // 2, (N - small.height) // 2))
    safe.save(os.path.join(out, "craquez_icon_fg.png"))
    full.convert("RGB").save(os.path.join(ROOT, "tools", "installer", "craquez.ico"), sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    full.resize((256, 256), Image.LANCZOS).save(os.path.join(ROOT, "tools", "installer", "linux", "craquez.png"))
    print("ícones gravados")


if __name__ == "__main__":
    main()
