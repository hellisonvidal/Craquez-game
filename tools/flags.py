"""Bandeiras dos países (ligas, nacionalidades e seleções) em PNG para o jogo.

Fonte: pacote flag-icons (MIT, https://github.com/lipis/flag-icons), versão 7.2.3.
Também grava o código ISO de cada seleção em leagues.json ("iso").
uso: npm pack flag-icons@7.2.3 && tar xzf flag-icons-7.2.3.tgz
     python3 tools/flags.py package/flags/4x3
"""
import json, os, sys
import cairosvg

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Craquez", "Resources", "Flags")
DATA = os.path.join(ROOT, "Assets", "Craquez", "Resources", "Data", "leagues.json")
SVG = sys.argv[1]

NAMES = {
    "Argentina": "AR", "Espanha": "ES", "França": "FR", "Inglaterra": "EN", "Brasil": "BR", "Portugal": "PT",
    "Holanda": "NL", "Bélgica": "BE", "Alemanha": "DE", "Croácia": "HR", "Marrocos": "MA", "Itália": "IT",
    "Colômbia": "CO", "Uruguai": "UY", "México": "MX", "Japão": "JP", "Estados Unidos": "US", "Senegal": "SN",
    "Suíça": "CH", "Dinamarca": "DK", "Coreia do Sul": "KR", "Equador": "EC", "Áustria": "AT", "Turquia": "TR",
    "Austrália": "AU", "Canadá": "CA", "Ucrânia": "UA", "Nigéria": "NG", "Polônia": "PL", "Egito": "EG",
    "Suécia": "SE", "Noruega": "NO", "Peru": "PE", "Costa do Marfim": "CI", "Chile": "CL", "Paraguai": "PY",
    "Camarões": "CM", "Arábia Saudita": "SA", "Gana": "GH", "Rússia": "RU", "Irã": "IR", "Cazaquistão": "KZ",
}
FILE = {"EN": "gb-eng"}   # Inglaterra: cruz de São Jorge

d = json.load(open(DATA, encoding="utf-8"))
codes = set(NAMES.values())
for lg in d["leagues"]:
    if lg.get("country"): codes.add(lg["country"])
    for t in lg["teams"]:
        if "national" in lg.get("sport", "") and t["name"] in NAMES:
            t["iso"] = NAMES[t["name"]]
        for r in t.get("roster", []):
            if r.get("nat"): codes.add(r["nat"])
json.dump(d, open(DATA, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
os.makedirs(OUT, exist_ok=True)
n = 0
for c in sorted(codes):
    src = os.path.join(SVG, FILE.get(c, c.lower()) + ".svg")
    if not os.path.exists(src):
        print("sem bandeira:", c); continue
    cairosvg.svg2png(url=src, write_to=os.path.join(OUT, c + ".png"), output_width=160, output_height=120)
    n += 1
print("bandeiras:", n)
