"""Uniformes das seleções (casa e fora) nas combinações tradicionais, próximas das usadas em 2026.
Formato: camisa (c1), detalhe (c2), calção, meião, desenho. Mantém o 3º uniforme do arquivo.
uso: python3 tools/kits_selecoes.py"""
import json, os
P = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Craquez", "Resources", "Data", "leagues.json")
K = lambda c1, c2, sh, so, pat="liso": {"c1": c1, "c2": c2, "shorts": sh, "socks": so, "pattern": pat}
W, N, B = "#f8fafc", "#0b1d3a", "#111111"
T = {
 "Argentina":      (K("#75aadb", W, B, W, "listras"), K("#1e2a5a", "#75aadb", "#1e2a5a", "#1e2a5a")),
 "Espanha":        (K("#c60b1e", "#ffc400", N, "#c60b1e"), K(W, "#c60b1e", W, W)),
 "França":         (K("#1e3a8a", W, W, "#c8102e"), K(W, "#1e3a8a", "#1e3a8a", W, "riscas")),
 "Inglaterra":     (K(W, N, N, W), K("#c8102e", N, "#c8102e", "#c8102e")),
 "Brasil":         (K("#ffdf00", "#009c3b", "#0b3d91", W), K("#0b3d91", "#ffdf00", "#0b3d91", "#0b3d91")),
 "Portugal":       (K("#c8102e", "#006600", "#006600", "#c8102e"), K(W, "#c8102e", W, W)),
 "Holanda":        (K("#f36c21", N, N, "#f36c21"), K(N, "#f36c21", N, N)),
 "Bélgica":        (K("#c8102e", "#fdda24", B, "#c8102e"), K(W, "#c8102e", W, W)),
 "Alemanha":       (K(W, B, B, W), K(B, "#dd0000", B, B)),
 "Croácia":        (K(W, "#c8102e", W, W, "xadrez"), K("#0b1d3a", "#c8102e", "#0b1d3a", "#0b1d3a")),
 "Marrocos":       (K("#c1272d", "#006233", "#006233", "#c1272d"), K(W, "#c1272d", W, W)),
 "Itália":         (K("#1f5fbf", W, W, "#1f5fbf"), K(W, "#1f5fbf", W, W)),
 "Colômbia":       (K("#fcd116", "#003893", "#003893", W), K("#7a1d2e", "#fcd116", "#7a1d2e", "#7a1d2e")),
 "Uruguai":        (K("#5cb3ff", W, B, B), K(W, "#5cb3ff", W, W)),
 "México":         (K("#006847", "#ce1126", W, "#006847"), K(W, "#006847", W, W)),
 "Japão":          (K("#0b2a6b", W, "#0b2a6b", "#0b2a6b"), K(W, "#0b2a6b", W, W)),
 "Estados Unidos": (K(W, "#0a3161", "#0a3161", W), K("#0a3161", "#b31942", "#0a3161", "#0a3161", "listras")),
 "Senegal":        (K(W, "#00853f", W, W), K("#00853f", "#fdef42", "#00853f", "#00853f")),
 "Suíça":          (K("#d52b1e", W, W, "#d52b1e"), K(W, "#d52b1e", W, W)),
 "Dinamarca":      (K("#c8102e", W, W, "#c8102e"), K(W, "#c8102e", W, W)),
 "Coreia do Sul":  (K("#c8102e", B, B, "#c8102e"), K(W, B, W, W)),
 "Equador":        (K("#ffd100", "#034ea2", "#034ea2", "#ffd100"), K("#034ea2", "#ffd100", "#034ea2", "#034ea2")),
 "Áustria":        (K("#c8102e", W, W, "#c8102e"), K(W, "#c8102e", B, W)),
 "Turquia":        (K(W, "#e30a17", W, W), K("#e30a17", W, "#e30a17", "#e30a17")),
 "Austrália":      (K("#ffcd00", "#00843d", "#00843d", "#ffcd00"), K("#00843d", "#ffcd00", "#00843d", "#00843d")),
 "Canadá":         (K("#d80621", W, "#d80621", "#d80621"), K(W, "#d80621", W, W)),
 "Ucrânia":        (K("#ffd500", "#005bbb", "#ffd500", "#ffd500"), K("#005bbb", "#ffd500", "#005bbb", "#005bbb")),
 "Nigéria":        (K("#008751", W, "#008751", "#008751"), K(W, "#008751", W, W)),
 "Polônia":        (K(W, "#dc143c", "#dc143c", W), K("#dc143c", W, "#dc143c", "#dc143c")),
 "Egito":          (K("#ce1126", B, W, "#ce1126"), K(W, "#ce1126", B, W)),
 "Suécia":         (K("#fecc02", "#006aa7", "#006aa7", "#fecc02"), K("#006aa7", "#fecc02", "#006aa7", "#006aa7")),
 "Noruega":        (K("#ba0c2f", "#00205b", W, "#ba0c2f"), K(W, "#00205b", "#00205b", W)),
 "Peru":           (K(W, "#d91023", W, W, "faixa"), K("#d91023", W, "#d91023", "#d91023")),
 "Costa do Marfim":(K("#f77f00", "#009e60", W, "#009e60"), K(W, "#f77f00", W, W)),
 "Chile":          (K("#d52b1e", "#0039a6", "#0039a6", W), K(W, "#d52b1e", W, W)),
 "Paraguai":       (K("#d52b1e", W, "#0038a8", "#0038a8", "listras"), K(W, "#d52b1e", W, W)),
 "Camarões":       (K("#007a5e", "#ce1126", "#ce1126", "#fcd116"), K(W, "#007a5e", W, W)),
 "Arábia Saudita": (K(W, "#006c35", W, W), K("#006c35", W, "#006c35", "#006c35")),
 "Gana":           (K(W, B, W, W), K("#ce1126", "#fcd116", "#ce1126", "#ce1126")),
 "Rússia":         (K(W, "#0039a6", W, W), K("#d52b1e", W, "#d52b1e", "#d52b1e")),
 "Irã":            (K(W, "#239f40", W, W), K("#da0000", W, "#da0000", "#da0000")),
 "Cazaquistão":    (K("#00afca", "#fec50c", "#00afca", "#00afca"), K("#fec50c", "#00afca", "#fec50c", "#fec50c")),
}
d = json.load(open(P, encoding="utf-8"))
n = 0
for lg in d["leagues"]:
    if "national" not in lg.get("sport", ""):
        continue
    for t in lg["teams"]:
        k = T.get(t["name"])
        if not k:
            continue
        kits = t.get("kits", [])
        third = kits[2:] if len(kits) > 2 else []
        t["kits"] = [dict(k[0]), dict(k[1])] + third
        t["c1"], t["c2"] = k[0]["c1"], k[0]["c2"]
        n += 1
json.dump(d, open(P, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
print("seleções atualizadas:", n)
