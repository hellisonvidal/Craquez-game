"""Uniformes dos clubes (casa e fora) próximos dos usados na temporada 2025/26.

Os nomes dos clubes continuam alterados (sem licença); só as cores e os desenhos se
aproximam dos uniformes de verdade. Mantém o 3º uniforme do arquivo.
Formato: K(camisa, detalhe, calção, meião, desenho, mangas=None, acabamento=None)
desenhos: liso, listras, aros, faixa, metades, riscas, faixa_peito, faixa_vertical, xadrez
uso: python3 tools/kits_clubes.py
"""
import json, os

P = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Craquez", "Resources", "Data", "leagues.json")


def K(c1, c2, sh, so, pat="liso", sleeves=None, trim=None):
    k = {"c1": c1, "c2": c2, "shorts": sh, "socks": so, "pattern": pat}
    if sleeves: k["sleeves"] = sleeves
    if trim: k["trim"] = trim
    return k


W, B, N = "#f8fafc", "#111111", "#0b1d3a"   # branco, preto, azul-marinho
RED, ROYAL, SKY, GOLD, GREEN = "#c8102e", "#1e4fa8", "#6cabdd", "#f2c200", "#00843d"

T = {
    # ---------------------------------------------------------------- Itália
    "ita": {
        "BGN": (K("#1e5bb8", B, B, B, "listras"), K(W, "#1e5bb8", W, W)),                 # Atalanta
        "BOL": (K("#a6192e", "#1b2a5e", W, "#1b2a5e", "metades"), K(W, "#a6192e", W, W)),   # Bologna
        "CAG": (K("#a6192e", "#002b5c", "#002b5c", "#002b5c", "metades"), K(W, "#a6192e", W, W)),
        "COM": (K("#1c4e9d", W, "#1c4e9d", "#1c4e9d"), K(W, "#1c4e9d", W, W)),
        "FIR": (K("#5b2a86", W, "#5b2a86", "#5b2a86"), K(W, "#5b2a86", W, W)),             # Fiorentina
        "FRO": (K(GOLD, "#1e4fa8", "#1e4fa8", GOLD), K(W, GOLD, W, W)),
        "GEN": (K("#a6192e", "#1b2a5e", W, "#1b2a5e", "metades"), K(W, "#a6192e", W, W)),
        "MIN": (K("#1f4fa0", B, B, B, "listras", trim=GOLD), K(W, "#1f4fa0", W, W)),        # Inter
        "TBN": (K(W, B, W, W, "listras"), K("#f5d1dc", B, B, B)),                          # Juventus
        "RBC": (K("#87ceeb", W, W, "#87ceeb", trim=N), K(W, "#87ceeb", W, W)),              # Lazio
        "LEC": (K(GOLD, RED, "#1e2f6a", RED, "listras"), K(W, RED, W, W)),
        "MRN": (K(RED, B, W, B, "listras"), K(W, RED, W, W)),                              # Milan
        "MZA": (K(RED, W, W, RED), K(W, RED, W, W, "faixa_peito")),
        "NAP": (K("#12a0d7", W, W, "#12a0d7"), K(W, "#12a0d7", W, W)),                     # Napoli
        "PAR": (K(W, B, W, W, "faixa_vertical"), K(GOLD, "#1e4fa8", "#1e4fa8", GOLD)),
        "RGR": (K("#8e1f2f", "#f0bc42", "#8e1f2f", "#8e1f2f", trim="#f0bc42"), K(W, "#8e1f2f", W, W)),  # Roma
        "SAS": (K("#00a650", B, B, B, "listras"), K(W, "#00a650", W, W)),
        "TGR": (K("#7a1f2b", W, W, "#7a1f2b"), K(W, "#7a1f2b", W, W)),                     # Torino
        "UDI": (K(W, B, B, W, "listras"), K(B, W, B, B)),
        "VEN": (K(B, "#f47b20", B, B, "faixa_peito"), K(W, "#00843d", W, W)),
    },
    # ---------------------------------------------------------------- Inglaterra
    "eng": {
        "NLR": (K("#ef0107", W, W, W, sleeves=W), K("#1a2a4a", "#ef0107", "#1a2a4a", "#1a2a4a")),   # Arsenal
        "BIC": (K("#670e36", "#95bfe5", W, "#670e36", sleeves="#95bfe5"), K(W, "#670e36", W, W)),   # Aston Villa
        "DOR": (K("#da291c", B, B, B, "listras"), K(W, "#da291c", W, W)),                           # Bournemouth
        "WLB": (K("#e30613", W, B, B, "listras"), K("#1d2f6f", "#e30613", "#1d2f6f", "#1d2f6f")),   # Brentford
        "SUS": (K("#0057b8", W, W, W, "listras"), K(GOLD, "#0057b8", "#0057b8", GOLD)),             # Brighton
        "WLU": (K("#034694", W, "#034694", W), K(W, "#034694", W, W)),                              # Chelsea
        "COV": (K("#6cace4", W, "#6cace4", "#6cace4"), K(W, "#6cace4", W, W)),
        "SLE": (K("#1b458f", "#c4122e", "#1b458f", "#1b458f", "listras"), K(W, "#1b458f", W, W)),   # Crystal Palace
        "MSB": (K("#003399", W, W, W), K(W, "#003399", W, W)),                                      # Everton
        "FUL": (K(W, B, B, W), K(B, W, B, B)),                                                      # Fulham
        "HUL": (K("#f5a12d", B, B, "#f5a12d", "listras"), K(W, "#f5a12d", W, W)),
        "IPS": (K("#3a64a3", W, W, "#3a64a3"), K("#e2001a", W, "#e2001a", "#e2001a")),
        "LEE": (K(W, "#1d428a", W, W, trim=GOLD), K("#1d428a", GOLD, "#1d428a", "#1d428a")),         # Leeds
        "MSR": (K("#c8102e", W, "#c8102e", "#c8102e"), K(W, "#c8102e", W, W)),                      # Liverpool
        "MCS": (K(SKY, W, W, SKY, trim=N), K(N, SKY, N, N)),                                        # Man City
        "MCR": (K("#da291c", W, W, B), K(W, "#da291c", W, W)),                                      # Man United
        "TYN": (K(B, W, B, B, "listras"), K(W, B, W, W)),                                           # Newcastle
        "NOT": (K("#dd0000", W, W, "#dd0000"), K(W, "#dd0000", W, W)),                              # Nottingham Forest
        "WEA": (K("#eb172b", W, B, B, "listras"), K(W, "#eb172b", W, W)),                           # Sunderland
        "NLW": (K(W, N, N, W), K(N, W, N, N)),                                                      # Tottenham
    },
    # ---------------------------------------------------------------- Espanha
    "esp": {
        "ALA": (K("#0057b8", W, "#0057b8", "#0057b8", "listras"), K(B, W, B, B)),
        "BIL": (K("#ee2523", W, B, B, "listras"), K(B, "#ee2523", B, B)),                            # Athletic
        "MAC": (K("#cb3524", W, "#262f61", "#cb3524", "listras"), K("#262f61", "#cb3524", "#262f61", "#262f61")),  # Atlético
        "BAR": (K("#a50044", "#004d98", "#004d98", "#004d98", "listras"), K(B, GOLD, B, B)),         # Barcelona
        "VIG": (K("#8ac3ee", W, W, "#8ac3ee"), K(W, "#8ac3ee", W, W)),                               # Celta
        "COR": (K(W, "#0067b1", "#0067b1", W, "listras"), K(B, W, B, B)),
        "ELC": (K(W, "#00843d", W, W, "faixa_peito"), K("#00843d", W, "#00843d", "#00843d")),
        "ESP": (K(W, "#0065b3", "#0065b3", W, "listras"), K("#0065b3", W, "#0065b3", "#0065b3")),    # Espanyol
        "GET": (K("#005999", W, "#005999", "#005999"), K(W, "#005999", W, W)),
        "LEV": (K("#004b9b", "#a6192e", "#004b9b", "#004b9b", "listras"), K(W, "#a6192e", W, W)),
        "MAL": (K("#0066b3", W, W, "#0066b3", "listras"), K(B, W, B, B)),
        "OSA": (K("#d50032", "#0a2240", "#0a2240", "#0a2240"), K(W, "#d50032", W, W)),
        "RAC": (K("#00845d", W, W, "#00845d", "faixa_peito"), K(W, "#00845d", W, W)),
        "RAY": (K(W, "#e30613", W, W, "faixa"), K(B, "#e30613", B, B, "faixa")),                    # Rayo
        "BET": (K("#00954c", W, W, "#00954c", "listras"), K(W, "#00954c", W, W)),                    # Betis
        "RMA": (K(W, GOLD, W, W, trim=GOLD), K(N, GOLD, N, N)),                                     # Real Madrid
        "RSO": (K("#0067b1", W, W, "#0067b1", "listras"), K(W, "#0067b1", W, W)),                    # Real Sociedad
        "SEV": (K(W, "#d4021d", W, W, trim="#d4021d"), K("#d4021d", W, "#d4021d", "#d4021d")),      # Sevilla
        "VAL": (K(W, B, B, W, trim="#f18e00"), K(B, "#f18e00", B, B)),                              # Valencia
        "VLR": (K("#ffe667", "#005187", "#005187", "#ffe667"), K("#005187", "#ffe667", "#005187", "#005187")),  # Villarreal
    },
    # ---------------------------------------------------------------- França
    "fra": {
        "ANG": (K(W, B, B, W, "listras"), K(B, W, B, B)),
        "AUX": (K(W, "#0e4c96", W, W, "faixa_peito"), K("#0e4c96", W, "#0e4c96", "#0e4c96")),
        "BRE": (K("#e30613", W, "#e30613", "#e30613"), K(W, "#e30613", W, W)),
        "HAV": (K("#79b0e0", N, N, "#79b0e0", "metades"), K(W, N, W, W)),
        "LMN": (K("#d21f26", GOLD, GOLD, "#d21f26", "metades"), K(W, "#d21f26", W, W)),
        "LEN": (K(GOLD, "#e30613", B, GOLD, sleeves="#e30613"), K(B, GOLD, B, B)),                  # Lens
        "LIL": (K("#e01e13", N, N, "#e01e13"), K(W, "#e01e13", W, W)),                              # Lille
        "LOR": (K("#f47920", B, B, "#f47920"), K(W, "#f47920", W, W)),
        "LYO": (K(W, "#e30613", W, W, "faixa_peito", trim="#1a3e8c"), K("#1a3e8c", "#e30613", "#1a3e8c", "#1a3e8c")),  # Lyon
        "MAR": (K(W, "#2faee0", W, W, trim="#2faee0"), K("#2faee0", W, "#2faee0", "#2faee0")),     # Marseille
        "MCO": (K("#e51b22", W, W, W, "metades"), K(B, "#e51b22", B, B)),                           # Monaco (diagonal)
        "NIC": (K("#c8102e", B, B, B, "listras"), K(W, "#c8102e", W, W)),
        "PFC": (K("#1a3c7a", "#e30613", "#1a3c7a", "#1a3c7a"), K(W, "#1a3c7a", W, W)),
        "PSG": (K("#004170", "#da291c", "#004170", "#004170", "faixa_vertical"), K(W, "#004170", W, W, "faixa_vertical")),  # PSG
        "REN": (K("#e13327", B, B, "#e13327"), K(W, "#e13327", W, W)),
        "STR": (K("#009fe3", W, W, "#009fe3"), K(W, "#009fe3", W, W)),
        "TOU": (K("#5b2a86", W, "#5b2a86", "#5b2a86"), K(W, "#5b2a86", W, W)),
        "TRO": (K("#1d4a9b", W, W, "#1d4a9b"), K(W, "#1d4a9b", W, W)),
    },
    # ---------------------------------------------------------------- Brasil série A
    "bra": {
        "FUR": (K(RED, B, B, B, "listras"), K(W, RED, W, W)),                                       # Athletico-PR
        "GAL": (K(B, W, B, B, "listras"), K(W, B, W, W)),                                           # Atlético-MG
        "BAH": (K(W, "#0055a5", "#0055a5", W, "faixa_peito", trim=RED), K("#0055a5", RED, "#0055a5", "#0055a5")),  # Bahia
        "EST": (K(B, W, B, B, "faixa_peito"), K(W, B, W, W)),                                       # Botafogo
        "CHA": (K("#00843d", W, W, "#00843d"), K(W, "#00843d", W, W)),                              # Chapecoense
        "TIM": (K(W, B, B, W), K(B, W, B, B)),                                                      # Corinthians
        "CXA": (K(W, "#00843d", "#00843d", W, "faixa_peito"), K("#00843d", W, "#00843d", "#00843d")),  # Coritiba
        "RAP": (K("#1b3d8f", W, W, W), K(W, "#1b3d8f", W, W)),                                      # Cruzeiro
        "RNC": (K(RED, B, W, RED, "aros"), K(W, RED, W, W)),                                        # Flamengo
        "TRC": (K("#7a1f2b", "#00613a", W, W, "listras", trim=W), K(W, "#7a1f2b", W, W)),           # Fluminense
        "TRG": (K("#0d80bf", B, B, W, "listras"), K(W, "#0d80bf", W, W)),                           # Grêmio
        "CLR": (K("#d71920", W, W, "#d71920"), K(W, "#d71920", "#d71920", W)),                      # Internacional
        "LEA": (K(B, "#ff6a00", B, B), K(W, "#ff6a00", W, W)),
        "VPA": (K("#006437", W, W, "#006437"), K(W, "#006437", "#006437", W)),                      # Palmeiras
        "MBR": (K(W, RED, RED, W, "faixa"), K(RED, W, RED, RED)),                                   # Bragantino
        "LAZ": (K("#0b3d91", W, W, "#0b3d91"), K(W, "#0b3d91", W, W)),
        "PEI": (K(W, B, W, W), K(B, W, B, B, "listras")),                                           # Santos
        "TRP": (K(W, RED, W, W, "faixa_peito", trim=B), K(RED, B, B, B, "listras")),                # São Paulo
        "GIG": (K(W, B, B, W, "faixa"), K(B, W, B, B, "faixa")),                                    # Vasco
        "LBA": (K(RED, B, B, RED, "listras"), K(W, RED, W, W)),                                     # Vitória
    },
    # ---------------------------------------------------------------- Portugal
    "por": {
        "SLB": (K("#e10b17", W, W, "#e10b17"), K(B, "#e10b17", B, B)),                              # Benfica
        "FCP": (K("#0033a0", W, "#0033a0", W, "listras"), K("#f6a623", "#0033a0", "#0033a0", "#f6a623")),  # Porto
        "SCP": (K("#008057", W, B, "#008057", "aros"), K(W, "#008057", W, W)),                      # Sporting
        "SCB": (K("#e30613", W, W, "#e30613", sleeves=W), K(W, "#e30613", W, W)),                   # Braga
        "VSC": (K(W, B, B, W), K(B, W, B, B)),
        "FAM": (K("#1e3a8a", W, "#1e3a8a", "#1e3a8a"), K(W, "#1e3a8a", W, W)),
        "GIL": (K(RED, W, W, RED), K(W, RED, W, W)),
        "EST": (K(GOLD, "#0055a5", "#0055a5", GOLD, "listras"), K(W, "#0055a5", W, W)),
        "CPA": (K("#00843d", W, W, "#00843d"), K(W, "#00843d", W, W)),
        "MOR": (K("#00843d", W, W, "#00843d", "listras"), K(W, "#00843d", W, W)),
        "RAV": (K(W, "#00843d", "#00843d", W, "listras"), K(B, W, B, B)),
        "ARO": (K(GOLD, "#0055a5", "#0055a5", GOLD), K(W, "#0055a5", W, W)),
        "SCL": (K(W, "#00843d", "#00843d", W, "listras"), K(B, "#00843d", B, B)),
        "NAC": (K(B, W, W, B, "listras"), K(W, B, W, W)),
        "EAM": (K(RED, "#00843d", W, RED), K(W, RED, W, W)),
        "ALV": (K("#0b3d91", W, W, "#0b3d91"), K(W, "#0b3d91", W, W)),
        "MRT": (K("#00843d", RED, RED, "#00843d", "listras"), K(W, "#00843d", W, W)),
        "VIS": (K(B, W, B, B), K(W, B, W, W)),
    },
    # ---------------------------------------------------------------- Holanda
    "ned": {
        "AMS": (K(W, "#d2122e", W, W, "faixa_vertical"), K(N, "#d2122e", N, N)),                   # Ajax
        "EIN": (K("#ed1c24", W, B, "#ed1c24", "listras"), K(B, "#ed1c24", B, B)),                  # PSV
        "ROT": (K(W, RED, B, B, "metades"), K(B, RED, B, B)),                                       # Feyenoord
        "ALK": (K("#db0021", W, W, "#db0021"), K(W, "#db0021", W, W)),                              # AZ
        "ENS": (K("#e2001a", W, W, "#e2001a"), K(W, "#e2001a", W, W)),                              # Twente
        "UTR": (K(RED, W, W, RED, "metades"), K(W, RED, W, W)),
        "DEV": (K("#ed1c24", W, B, "#ed1c24"), K(W, "#ed1c24", W, W)),
        "FRI": (K("#005aab", W, W, "#005aab", "listras"), K(W, "#005aab", W, W)),
        "NIJ": (K(RED, "#00843d", B, RED, "metades"), K(W, RED, W, W)),
        "SPA": (K(RED, W, B, RED, "listras"), K(W, RED, W, W)),
        "SIT": (K(GOLD, "#00843d", "#00843d", GOLD), K(W, "#00843d", W, W)),
        "GRO": (K(W, "#00843d", "#00843d", W, "faixa_peito"), K("#00843d", W, "#00843d", "#00843d")),
        "ZWO": (K(W, "#00519e", "#00519e", W, "faixa_peito"), K("#00519e", W, "#00519e", "#00519e")),
        "EXC": (K(RED, B, B, B, "listras"), K(W, RED, W, W)),
        "ALM": (K(W, B, B, W, "faixa_peito"), K(B, W, B, B)),
        "HAA": (K(GOLD, "#00843d", "#00843d", GOLD, "metades"), K(W, "#00843d", W, W)),
        "LWD": (K(GOLD, "#0055a5", "#0055a5", GOLD), K(W, "#0055a5", W, W)),
        "TIL": (K(RED, W, W, RED, "listras", trim="#1d428a"), K(W, RED, W, W)),
    },
    # ---------------------------------------------------------------- Argentina
    "arg": {
        "ARJ": (K(RED, W, W, RED), K(W, RED, W, W)),                                                # Argentinos
        "BAN": (K("#00843d", W, W, "#00843d"), K(W, "#00843d", W, W)),                              # Banfield
        "BEL": (K("#87ceeb", W, B, "#87ceeb"), K(B, "#87ceeb", B, B)),                              # Belgrano
        "XEN": (K("#103f79", "#fcc30b", "#103f79", "#103f79", "faixa_peito"), K(W, "#103f79", W, W)),  # Boca
        "PIN": (K(RED, W, B, RED, "listras"), K(W, RED, W, W)),                                     # Estudiantes
        "LOB": (K(W, "#0b2a6b", "#0b2a6b", W, "listras"), K("#0b2a6b", W, "#0b2a6b", "#0b2a6b")),   # Gimnasia LP
        "GLO": (K(W, RED, W, W, "faixa_peito"), K(RED, W, RED, RED)),                               # Huracán
        "ROJ": (K("#d40d12", W, B, "#d40d12"), K(W, "#d40d12", W, W)),                              # Independiente
        "INS": (K(W, RED, RED, W, "listras"), K(RED, W, RED, RED)),
        "GRA": (K("#7a1f2b", W, W, "#7a1f2b"), K(W, "#7a1f2b", W, W)),                              # Lanús
        "LEP": (K(RED, B, B, B, "metades"), K(W, RED, W, W)),                                       # Newell's
        "ACA": (K("#7cb5e5", W, B, "#7cb5e5"), K(W, "#7cb5e5", W, W)),                              # Racing
        "MIL": (K(W, RED, B, W, "faixa"), K(RED, W, RED, RED)),                                     # River
        "CNL": (K("#004a9f", GOLD, "#004a9f", "#004a9f", "listras"), K(GOLD, "#004a9f", "#004a9f", GOLD)),  # Rosario Central
        "CIC": (K("#003a8c", RED, "#003a8c", "#003a8c", "listras"), K(W, "#003a8c", W, W)),         # San Lorenzo
        "FTN": (K(W, "#00843d", W, W, "faixa_peito"), K("#00843d", W, "#00843d", "#00843d")),       # Vélez
        "TAT": (K(RED, W, W, RED, "listras"), K(W, RED, W, W)),                                     # Unión
        "CAL": (K(W, "#d40d12", W, W, "faixa"), K("#d40d12", W, "#d40d12", "#d40d12")),            # Platense
        "SAR": (K("#00843d", W, W, "#00843d"), K(W, "#00843d", W, W)),
        "MAT": (K(W, "#0055a5", "#0055a5", W, "metades"), K("#0055a5", W, "#0055a5", "#0055a5")),
        "VIC": (K(RED, W, B, RED, "listras"), K(W, RED, W, W)),                                     # Tigre
    },
}

d = json.load(open(P, encoding="utf-8"))
n = 0
for lg in d["leagues"]:
    table = T.get(lg["id"])
    if not table: continue
    for t in lg["teams"]:
        k = table.get(t["short"])
        if not k: continue
        kits = t.get("kits", [])
        third = kits[2:] if len(kits) > 2 else []
        t["kits"] = [dict(k[0]), dict(k[1])] + third
        t["c1"], t["c2"] = k[0]["c1"], k[0]["c2"]
        n += 1
json.dump(d, open(P, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
print("clubes atualizados:", n)
