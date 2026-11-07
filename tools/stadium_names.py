"""Nomes de estádios e ginásios mais condizentes com os de verdade (fictícios, no estilo de cada país).

Ex.: "Stadio Comunale di Bergamo", "Estadio Municipal de Vigo", "Stade de la Ville de Lens",
"Arena Porto Alegre", "Estadio El Pirata", "Ginásio Municipal de Carlos Barbosa".
Sem marcas nem nomes reais de estádios. Clubes da mesma cidade recebem nomes diferentes.
uso: python3 tools/stadium_names.py
"""
import json, os, re

P = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Craquez", "Resources", "Data", "leagues.json")

T = {
    "ita": ["Stadio Comunale di {c}", "Stadio Città di {c}", "Arena {c}", "Stadio Olimpico di {c}", "Stadio del Littorale {c}"],
    "eng": ["{c} Park", "{c} Road", "{c} Community Stadium", "{c} Lane", "{c} Grounds"],
    "esp": ["Estadio Municipal de {c}", "Nuevo Estadio de {c}", "Estadio Ciudad de {c}", "Estadio de la Ribera de {c}"],
    "fra": ["Stade Municipal de {c}", "Stade de la Ville de {c}", "Stade {c} Métropole", "Parc des Sports de {c}"],
    "ned": ["{c} Stadion", "Stadion {c}-Noord", "{c} Arena", "Sportpark {c}"],
    "por": ["Estádio Municipal de {c}", "Estádio Cidade de {c}", "Estádio do Bonfim de {c}", "Estádio Novo de {c}"],
    "bra": ["Arena {c}", "Estádio Municipal de {c}", "Estádio Estadual de {c}", "Estádio do {n}", "Arena do {n}"],
    "mls": ["{c} Soccer Stadium", "{c} Field", "{c} Park", "{c} Community Stadium"],
    "sau": ["Estádio Internacional de {c}", "Estádio Municipal de {c}", "Cidade Esportiva de {c}"],
    "rus": ["Arena {c}", "Estádio Central de {c}", "Estádio Municipal de {c}", "Estádio do Trabalho de {c}"],
    "sam": ["Estadio {n}", "Estadio Municipal {n}", "Estadio El {n}", "Estadio Parque {n}"],
    "fpt": ["Pavilhão Municipal de {c}", "Pavilhão Desportivo de {c}", "Pavilhão {c}"],
    "fbr": ["Ginásio Municipal de {c}", "Ginásio Poliesportivo de {c}", "Arena {c}"],
    "fes": ["Pabellón Municipal de {c}", "Palacio de Deportes de {c}", "Pabellón {c} Arena"],
    "fit": ["Palazzetto dello Sport di {c}", "PalaComunale {c}", "Palasport {c}"],
    "far": ["Polideportivo {n}", "Estadio Cubierto {n}", "Microestadio {n}"],
}
STYLE = {"brb": "bra", "arg": "sam", "uru": "sam", "per": "sam", "chi": "sam", "ecu": "sam", "ukr": "rus", "sel": None, "fsel": None}

# clubes brasileiros: cidade de cada um (o nome do time é um apelido)
BR_CITY = {
    "FUR": "Curitiba", "GAL": "Belo Horizonte", "BAH": "Salvador", "EST": "Rio de Janeiro", "CHA": "Chapecó",
    "TIM": "São Paulo", "CXA": "Curitiba", "RAP": "Belo Horizonte", "RNC": "Rio de Janeiro", "TRC": "Rio de Janeiro",
    "TRG": "Porto Alegre", "CLR": "Porto Alegre", "LEA": "Mirassol", "VPA": "São Paulo", "MBR": "Bragança Paulista",
    "LAZ": "Fortaleza", "PEI": "Santos", "TRP": "São Paulo", "GIG": "Rio de Janeiro", "LBA": "Salvador",
    "VOZ": "Fortaleza", "PIC": "Fortaleza", "PAP": "Caxias do Sul", "ILH": "Recife", "MCC": "Campinas",
    "TUB": "Londrina", "TBU": "Recife", "SBC": "São Bernardo do Campo", "CRI": "Criciúma", "ESM": "Goiânia",
    "NOV": "Novo Hamburgo", "CRB": "Maceió", "AVA": "Florianópolis", "DOU": "Cuiabá", "DRA": "Goiânia",
    "FAN": "Ponta Grossa", "TIG": "Goiânia", "COE": "Belo Horizonte", "ACO": "Volta Redonda", "PTR": "Campinas",
}


def city_of(name):
    toks = name.split()
    if len(toks) <= 1:
        return name
    # "Lens Sang et Or", "Le Mans Rouge-Jaune", "A Coruña Blanquiazul", "Den Haag Ooievaars"
    if any(t in ("et", "de", "do", "da", "del") for t in toks[1:-1]) and len(toks) >= 3 and toks[1] in ("Sang",):
        return toks[0]
    for suf in ("Futsal", "FS", "MG"):
        if toks[-1] == suf and len(toks) >= 2:
            toks = toks[:-1]
            return " ".join(toks) if len(toks) <= 3 else " ".join(toks[:2])
    if "et" in toks:
        return " ".join(toks[: toks.index("et") - 1]) or toks[0]
    return " ".join(toks[:-1])


def nick_of(name):
    # "Carbonero Aurinegro" -> "Carbonero"; "Diablo Rojo de Chillán" -> "Diablo Rojo"
    toks = re.split(r"\s+(?:de|del|da|do)\s+", name)[0].split()
    return toks[0] if len(toks[0]) > 3 or len(toks) == 1 else " ".join(toks[:2])


d = json.load(open(P, encoding="utf-8"))
n = 0
for lg in d["leagues"]:
    lid = lg["id"]
    style = STYLE.get(lid, lid)
    if style is None or style not in T:
        continue
    templ = T[style]
    used, per_city = set(), {}
    for t in lg["teams"]:
        c = BR_CITY.get(t["short"]) if style == "bra" else city_of(t["name"])
        nk = nick_of(t["name"])
        k = per_city.get(c, 0)
        name = None
        for j in range(len(templ)):
            cand = templ[(hash_i := (sum(map(ord, t["short"])) + k + j)) % len(templ)].format(c=c, n=nk)
            if cand not in used:
                name = cand
                break
        if name is None:
            name = templ[0].format(c=c, n=nk) + " " + t["short"]
        per_city[c] = k + 1
        used.add(name)
        t.setdefault("stadium", {})["name"] = name
        n += 1
json.dump(d, open(P, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
print("estádios renomeados:", n)
for lg in d["leagues"]:
    if lg["id"] in ("ita", "eng", "bra", "arg", "fbr", "fes"):
        print(lg["id"], [t["stadium"]["name"] for t in lg["teams"][:6]])
