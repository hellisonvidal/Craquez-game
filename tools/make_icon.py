"""Ícone original do Craquez: bola de futebol que também é um globo (meridianos e
paralelos), com uma órbita dourada — "o futebol no mundo". Gera:
  Assets/Craquez/Icons/craquez_icon.png        (1024, fundo + bola)
  Assets/Craquez/Icons/craquez_icon_fg.png     (1024, só a bola, fundo transparente — ícone adaptativo)
  Assets/Craquez/Icons/craquez_icon_bg.png     (1024, só o fundo)
  tools/installer/craquez.ico                   (instalador do Windows)
uso: python3 tools/make_icon.py"""
import math, os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
N = 1024
S = 3  # supersampling
W = N * S

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))

def background():
    img = Image.new("RGBA", (W, W))
    px = img.load()
    top, bot = (6, 22, 48), (2, 8, 20)
    for y in range(W):
        c = lerp(top, bot, y / W)
        for x in range(W):
            d = math.hypot(x - W * 0.5, y - W * 0.42) / (W * 0.7)
            g = max(0.0, 1 - d) ** 2
            px[x, y] = (min(255, int(c[0] + 0 * g)), min(255, int(c[1] + 150 * g * 0.5)), min(255, int(c[2] + 110 * g * 0.5)), 255)
    return img

def ball(r_frac=0.33):
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    cx, cy, R = W * 0.5, W * 0.5, W * r_frac
    px = img.load()
    # esfera: verde neon -> azul petróleo, luz do alto à esquerda, faixas chapadas (cel)
    lx, ly, lz = -0.45, -0.55, 0.7
    ln = math.sqrt(lx * lx + ly * ly + lz * lz); lx, ly, lz = lx / ln, ly / ln, lz / ln
    for y in range(int(cy - R) - 2, int(cy + R) + 2):
        for x in range(int(cx - R) - 2, int(cx + R) + 2):
            nx, ny = (x - cx) / R, (y - cy) / R
            rr = nx * nx + ny * ny
            if rr > 1: continue
            nz = math.sqrt(1 - rr)
            dif = max(0.0, nx * lx + ny * ly + nz * lz)
            band = 1.0 if dif > 0.55 else (0.78 if dif > 0.2 else 0.6)
            base = lerp((0, 230, 158), (8, 120, 160), min(1, (ny + 1) / 2 * 0.9 + nx * 0.15))
            c = tuple(int(v * band) for v in base)
            # meridianos e paralelos (globo) em branco
            lat = math.asin(-ny)                      # -pi/2..pi/2
            lon = math.atan2(nx, nz)                  # -pi/2..pi/2 visível
            on = False
            for k in range(-2, 3):
                if abs(lat - k * math.pi / 6) < 0.035 / max(0.3, nz): on = True
            for k in range(-3, 4):
                if abs(lon - k * math.pi / 7) < 0.03 / max(0.25, math.cos(lat)) * 0.9 and abs(lat) < 1.35: on = True
            if on: c = lerp(c, (240, 255, 248), 0.85)
            px[x, y] = (*c, 255)
    d = ImageDraw.Draw(img)
    # pentágono central (bola de futebol) e pentágonos parciais ao redor
    def pent(px_, py_, rad, rot=-90, fill=(10, 16, 30, 255)):
        pts = [(px_ + rad * math.cos(math.radians(rot + i * 72)), py_ + rad * math.sin(math.radians(rot + i * 72))) for i in range(5)]
        d.polygon(pts, fill=fill)
    pent(cx - R * 0.08, cy - R * 0.06, R * 0.27)
    for ang in (-90, -18, 54, 126, 198):
        a = math.radians(ang)
        ox, oy = cx - R * 0.08 + math.cos(a) * R * 0.72, cy - R * 0.06 + math.sin(a) * R * 0.72
        if math.hypot(ox - cx, oy - cy) < R * 0.86: pent(ox, oy, R * 0.17, ang + 180)
    # contorno grosso (estilo do jogo)
    d.ellipse([cx - R, cy - R, cx + R, cy + R], outline=(6, 10, 22, 255), width=int(W * 0.018))
    # recorta o que saiu da esfera
    mask = Image.new("L", (W, W), 0)
    ImageDraw.Draw(mask).ellipse([cx - R - W * 0.009, cy - R - W * 0.009, cx + R + W * 0.009, cy + R + W * 0.009], fill=255)
    out = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    out.paste(img, (0, 0), mask)
    # órbita dourada (anel inclinado) passando atrás/na frente da bola
    box = [cx - R * 1.45, cy - R * 0.45, cx + R * 1.45, cy + R * 0.45]
    def arc(a0, a1):
        im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
        rd = ImageDraw.Draw(im)
        rd.arc(box, a0, a1, fill=(6, 10, 22, 255), width=int(W * 0.05))
        rd.arc(box, a0 + 1, a1 - 1, fill=(250, 204, 21, 255), width=int(W * 0.026))
        return im.rotate(22, resample=Image.BICUBIC, center=(cx, cy))
    back = arc(180, 360)    # metade de cima do anel: passa atrás da bola
    front = arc(-4, 184)    # metade de baixo: na frente (sobrepõe as emendas)
    res = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    res.alpha_composite(back)
    res.alpha_composite(out)
    res.alpha_composite(front)
    # estrela de brilho
    sd = ImageDraw.Draw(res)
    sx, sy, sr = cx + R * 1.05, cy - R * 0.95, R * 0.16
    pts = []
    for i in range(8):
        rad = sr if i % 2 == 0 else sr * 0.28
        a = math.radians(i * 45 - 90)
        pts.append((sx + rad * math.cos(a), sy + rad * math.sin(a)))
    sd.polygon(pts, fill=(255, 245, 200, 255))
    return res

def main():
    out_dir = os.path.join(ROOT, "Assets", "Craquez", "Icons")
    os.makedirs(out_dir, exist_ok=True)
    bg = background()
    fg = ball(0.30)   # adaptativo: a bola cabe na zona segura (66%)
    full_fg = ball(0.33)
    full = bg.copy(); full.alpha_composite(full_fg)
    def save(img, name):
        img.resize((N, N), Image.LANCZOS).save(os.path.join(out_dir, name))
    save(full, "craquez_icon.png")
    save(fg, "craquez_icon_fg.png")
    save(bg, "craquez_icon_bg.png")
    inst = os.path.join(ROOT, "tools", "installer")
    os.makedirs(inst, exist_ok=True)
    full.resize((256, 256), Image.LANCZOS).save(os.path.join(inst, "craquez.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print("ok")

if __name__ == "__main__":
    main()
