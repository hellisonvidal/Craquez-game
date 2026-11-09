#!/usr/bin/env python3
"""Gera o option file "casa" (uso pessoal, sem fins comerciais) com nomes reais de clubes, estádios
e elencos, a partir do conjunto público transfermarkt-datasets (dcaribou, CC0; retrato até jul/2026).

NÃO versionar a saída: o repositório é público. O CI só roda este script no build "casa" (APK que
vai para um rascunho de release, visível apenas para quem administra o repositório).

Uso:  python3 tools/realdata/build_option_file.py SAIDA.json [pasta_cache]
"""
from __future__ import annotations

import csv
import gzip
import json
import math
import sys
import urllib.request
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = "https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/"

# liga do jogo -> competição na base
COMP = {"ita": "IT1", "eng": "GB1", "esp": "ES1", "fra": "FR1", "bra": "BRA1", "mls": "MLS1", "arg": "ARG1",
        "sau": "SA1", "ned": "NL1", "por": "PO1", "rus": "RU1", "ukr": "UKR1"}

# sigla do jogo -> id do clube na base (int) ou (nome real, estádio) para quem não está na base
CLUBS = {
    "ita": {"BGN": 800, "BOL": 1025, "CAG": 1390, "COM": 1047, "FIR": 430, "FRO": ("Frosinone Calcio", "Stadio Benito Stirpe"),
            "GEN": 252, "MIN": 46, "TBN": 506, "RBC": 398, "LEC": 1005, "MRN": 5, "MZA": ("AC Monza", "U-Power Stadium"),
            "NAP": 6195, "PAR": 130, "RGR": 12, "SAS": 6574, "TGR": 416, "UDI": 410, "VEN": ("Venezia FC", "Stadio Pier Luigi Penzo")},
    "eng": {"NLR": 11, "BIC": 405, "DOR": 989, "WLB": 1148, "SUS": 1237, "WLU": 631, "COV": ("Coventry City", "Coventry Building Society Arena"),
            "SLE": 873, "MSB": 29, "FUL": 931, "HUL": ("Hull City", "MKM Stadium"), "IPS": ("Ipswich Town", "Portman Road"),
            "LEE": 399, "MSR": 31, "MCS": 281, "MCR": 985, "TYN": 762, "NOT": 703, "WEA": 289, "NLW": 148},
    "esp": {"ALA": 1108, "BIL": 621, "MAC": 13, "BAR": 131, "VIG": 940, "COR": ("RC Deportivo La Coruña", "Estadio Abanca-Riazor"),
            "ELC": 1531, "ESP": 714, "GET": 3709, "LEV": 3368, "MAL": ("Málaga CF", "Estadio La Rosaleda"), "OSA": 331,
            "RAC": ("Real Racing Club", "Estadio El Sardinero"), "RAY": 367, "BET": 150, "RMA": 418, "RSO": 681, "SEV": 368,
            "VAL": 1049, "VLR": 1050},
    "fra": {"ANG": 1420, "AUX": 290, "BRE": 3911, "HAV": 738, "LMN": ("Le Mans FC", "MMArena"), "LEN": 826, "LIL": 1082,
            "LOR": 1158, "LYO": 1041, "MAR": 244, "MCO": 162, "NIC": 417, "PFC": 10004, "PSG": 583, "REN": 273, "STR": 667,
            "TOU": 415, "TRO": ("ES Troyes AC", "Stade de l'Aube")},
    "bra": {"FUR": 679, "GAL": 330, "BAH": 10010, "EST": 537, "CHA": 17776, "TIM": 199, "CXA": 776, "RAP": 609, "RNC": 614,
            "TRC": 2462, "TRG": 210, "CLR": 6600, "LEA": 3876, "VPA": 1023, "MBR": 8793, "LAZ": 10997, "PEI": 221, "TRP": 585,
            "GIG": 978, "LBA": 2125},
    "mls": {"ATL": 51663, "CLT": 78435, "CHI": 432, "CIN": 51772, "CLB": 813, "DCU": 2440, "MIA": 69261, "MTL": 4078,
            "NSH": 63966, "NER": 626, "NYC": 40058, "NJR": 623, "ORL": 45604, "PHI": 25467, "TOR": 11141, "AUS": 72309,
            "COL": 1247, "DAL": 8816, "HOU": 9168, "LAG": 1061, "LAF": 51828, "MNU": 56089, "POR": 4291, "RSL": 6643,
            "SDF": 114977, "SJE": 218, "SEA": 9636, "SKC": 4284, "STL": 82686, "VAN": 6321},
    "arg": {"ALD": 12301, "ARJ": 1030, "ATU": 14554, "BAN": 830, "BCA": 25184, "BEL": 2417, "XEN": 189, "CCO": 31284,
            "DYJ": 2402, "PIN": 288, "ERC": 14602, "LOB": 1106, "GIM": 14687, "GLO": 2063, "ROJ": 1234, "IRI": 12179,
            "INS": 1829, "GRA": 333, "LEP": 1286, "CAL": 928, "ACA": 1444, "MIL": 209, "CNL": 1418, "RIE": 19775,
            "CIC": 1775, "SAR": 12454, "MAT": 3938, "VIC": 11831, "TAT": 7097, "FTN": 1029},
    "sau": {"HIL": 1114, "NAS": 18544, "ITT": 8023, "AHL": 18487, "QAD": 26069, "ETT": 7732, "TAA": 28844, "SHB": 9840,
            "FAT": 27221, "KHA": 6070, "FAY": 50531, "KHO": 91427, "DAM": 50532, "HAZ": 9131, "NEO": 34911,
            "ABH": ("Abha Club", "Prince Sultan bin Abdul Aziz Stadium"), "FAI": ("Al-Faisaly FC", "Al-Majma'ah Sports City Stadium"),
            "DIR": ("Al-Diriyah Club", "Al-Diriyah Stadium")},
    "ned": {"AMS": 610, "EIN": 383, "ROT": 234, "ALK": 1090, "ENS": 317, "UTR": 200, "DEV": 1435, "FRI": 306, "NIJ": 467,
            "SPA": 468, "SIT": 385, "GRO": 202, "ZWO": 1269, "EXC": 798, "ALM": 1304, "HAA": ("ADO Den Haag", "Bingoal Stadion"),
            "LWD": ("SC Cambuur", "Kooi Stadion"), "TIL": ("Willem II", "Koning Willem II Stadion")},
    "por": {"SLB": 294, "FCP": 720, "SCP": 336, "SCB": 1075, "VSC": 2420, "FAM": 3329, "GIL": 2424, "EST": 1465, "CPA": 3268,
            "MOR": 979, "RAV": 2425, "ARO": 8024, "SCL": 2423, "NAC": 982, "EAM": 2431, "ALV": 2521,
            "MRT": ("CS Marítimo", "Estádio do Marítimo"), "VIS": ("Académico de Viseu", "Estádio do Fontelo")},
    "rus": {"ZEN": 964, "KRA": 16704, "SPM": 232, "CSK": 2410, "LOK": 932, "DMO": 121, "RUB": 2698, "ROS": 1083, "AKH": 3725,
            "KRY": 2696, "BAL": 2741, "NNO": 49702, "AKR": 71985, "DMK": 75231, "ROD": ("FC Rodina Moskva", "Arena Rodina"),
            "FAK": ("Fakel Voronezh", "Central Profsoyuz Stadium")},
    "ukr": {"SHA": 660, "DKY": 338, "POL": 53646, "KRV": 3592, "ZOR": 10690, "LNZ": 63007, "KAR": 85465, "OBO": 39097,
            "KOL": 48332, "VER": 26459, "MET": 55686, "KUD": 87883, "EPI": 84122, "LBK": ("FC Livyi Bereh", "Arena Livyi Bereh"),
            "BUK": ("FC Bukovyna", "Bukovyna Stadium"), "CHO": ("Chornomorets Odesa", "Chornomorets Stadium")},
    # ligas fora da base: só o nome do clube e do estádio (apenas os que dá para afirmar com segurança)
    "brb": {"VOZ": ("Ceará SC", "Arena Castelão"), "PIC": ("Fortaleza EC", "Arena Castelão"), "PAP": ("EC Juventude", "Estádio Alfredo Jaconi"),
            "ILH": ("Sport Recife", "Ilha do Retiro"), "MCC": ("AA Ponte Preta", "Estádio Moisés Lucarelli"), "TUB": ("Londrina EC", "Estádio do Café"),
            "TBU": ("Clube Náutico Capibaribe", "Estádio dos Aflitos"), "SBC": ("São Bernardo FC", "Estádio 1º de Maio"),
            "CRI": ("Criciúma EC", "Estádio Heriberto Hülse"), "ESM": ("Goiás EC", "Estádio Hailé Pinheiro"),
            "NOV": ("Grêmio Novorizontino", "Estádio Jorge Ismael de Biasi"), "CRB": ("CRB", "Estádio Rei Pelé"),
            "AVA": ("Avaí FC", "Estádio da Ressacada"), "DOU": ("Cuiabá EC", "Arena Pantanal"),
            "DRA": ("Atlético Goianiense", "Estádio Antônio Accioly"), "FAN": ("Operário Ferroviário", "Estádio Germano Krüger"),
            "TIG": ("Vila Nova FC", "Estádio Onésio Brasileiro Alvarenga"), "COE": ("América Mineiro", "Arena Independência"),
            "ACO": ("Volta Redonda FC", "Estádio Raulino de Oliveira")},
    "uru": {"CAR": ("CA Peñarol", "Estadio Campeón del Siglo"), "BOL": ("Club Nacional de Football", "Gran Parque Central"),
            "VIO": ("Defensor Sporting", "Estadio Luis Franzini"), "DAN": ("Danubio FC", "Estadio Jardines del Hipódromo"),
            "LVP": ("Liverpool FC", "Estadio Belvedere"), "BOH": ("Montevideo Wanderers", "Parque Alfredo Víctor Viera"),
            "DAR": ("CA River Plate", "Parque Federico Omar Saroldi"), "VLL": ("CA Cerro", "Estadio Luis Tróccoli"),
            "ARA": ("Cerro Largo FC", "Estadio Arquitecto Antonio Ubilla"), "PED": ("Juventud de Las Piedras", "Parque Artigas"),
            "GAU": ("CA Progreso", "Estadio Abraham Paladino")},
    "per": {"CRE": ("Universitario de Deportes", "Estadio Monumental"), "INT": ("Alianza Lima", "Estadio Alejandro Villanueva"),
            "CEL": ("Sporting Cristal", "Estadio Alberto Gallardo"), "DOM": ("FBC Melgar", "Estadio Monumental de la UNSA"),
            "PAP": ("Club Cienciano", "Estadio Inca Garcilaso de la Vega"), "HUA": ("Sport Huancayo", "Estadio Huancayo"),
            "ADT": ("ADT de Tarma", "Estadio Unión Tarma"), "GRA": ("Atlético Grau", "Estadio Campeones del 36"),
            "SUL": ("Alianza Atlético", "Estadio Campeones del 36"), "CUT": ("Comerciantes Unidos", "Estadio Juan Maldonado Gamarra"),
            "GAR": ("Deportivo Garcilaso", "Estadio Inca Garcilaso de la Vega"), "JP2": ("Juan Pablo II College", "Estadio Elías Aguirre"),
            "CHK": ("Los Chankas", "Estadio Los Chankas"), "SBA": ("Sport Boys", "Estadio Miguel Grau"),
            "UTC": ("UTC Cajamarca", "Estadio Héroes de San Ramón")},
    "chi": {"COQ": ("Coquimbo Unido", "Estadio Francisco Sánchez Rumoroso"), "CRU": ("Universidad Católica", "Claro Arena"),
            "OHI": ("O'Higgins", "Estadio El Teniente"), "ITA": ("Audax Italiano", "Estadio Bicentenario de La Florida"),
            "ARB": ("Palestino", "Estadio Municipal de La Cisterna"), "MIN": ("Cobresal", "Estadio El Cobre"),
            "CAC": ("Colo-Colo", "Estadio Monumental David Arellano"), "ACE": ("Huachipato", "Estadio Huachipato-CAP Acero"),
            "CHL": ("Ñublense", "Estadio Nelson Oyarzún"), "LIM": ("Deportes Limache", "Estadio Ángel Navarrete"),
            "CEM": ("Unión La Calera", "Estadio Nicolás Chahuán"), "PAY": ("Deportes La Serena", "Estadio La Portada"),
            "RUL": ("Everton de Viña del Mar", "Estadio Sausalito"), "CAM": ("Universidad de Concepción", "Estadio Ester Roa"),
            "LIL": ("Deportes Concepción", "Estadio Ester Roa")},
    "ecu": {"LDU": ("Liga Deportiva Universitaria", "Estadio Rodrigo Paz Delgado"), "IDV": ("Independiente del Valle", "Estadio Banco Guayaquil"),
            "IDO": ("Barcelona SC", "Estadio Monumental Banco Pichincha"), "BOM": ("CS Emelec", "Estadio George Capwell"),
            "AUC": ("SD Aucas", "Estadio Gonzalo Pozo Ripalda"), "MOR": ("Deportivo Cuenca", "Estadio Alejandro Serrano Aguilar"),
            "BAN": ("Orense SC", "Estadio 9 de Mayo"), "MAC": ("CSD Macará", "Estadio Bellavista"),
            "LIB": ("Libertad FC", "Estadio Reina del Cisne"), "TEC": ("Técnico Universitario", "Estadio Bellavista"),
            "LEO": ("Leones del Norte", "Estadio Olímpico de Ibarra")},
    "fbr": {"TFS": ("Corinthians Futsal", "Ginásio Wlamir Marques"), "JEC": ("JEC Futsal", "Centreventos Cau Hansen"),
            "MAG": ("Magnus Futsal", "Ginásio Gualberto Moreira"), "CBA": ("ACBF Carlos Barbosa", "Centro Municipal de Eventos")},
    "fes": {"BFS": ("FC Barcelona Futsal", "Palau Blaugrana"), "INT": ("Movistar Inter", "Pabellón Jorge Garbajosa"),
            "PAL": ("Illes Balears Palma Futsal", "Palau Municipal d'Esports Son Moix"), "MUR": ("ElPozo Murcia", "Palacio de Deportes de Murcia"),
            "BTF": ("Real Betis Futsal", "Palacio de Deportes San Pablo")},
    "fpt": {"SCP": ("Sporting CP Futsal", "Pavilhão João Rocha"), "SLB": ("SL Benfica Futsal", "Pavilhão Fidelidade"),
            "SCB": ("SC Braga Futsal", "Pavilhão Desportivo do Sporting de Braga")},
    "far": {"XEN": ("Boca Juniors Futsal", "Estadio Cubierto Boca Juniors"), "CIC": ("San Lorenzo Futsal", "Polideportivo Roberto Pando"),
            "MIL": ("River Plate Futsal", "Estadio Cubierto River Plate"), "ROJ": ("Independiente Futsal", "Polideportivo Independiente"),
            "ACA": ("Racing Club Futsal", "Polideportivo Racing Club")},
}

ISO = {"Argentina": "AR", "Brazil": "BR", "Spain": "ES", "Portugal": "PT", "Uruguay": "UY", "United States": "US", "Peru": "PE",
       "France": "FR", "Italy": "IT", "Netherlands": "NL", "Chile": "CL", "Ecuador": "EC", "Ukraine": "UA", "Russia": "RU",
       "Colombia": "CO", "England": "EN", "Saudi Arabia": "SA", "Nigeria": "NG", "Senegal": "SN", "Morocco": "MA", "Japan": "JP",
       "Ghana": "GH", "Cote d'Ivoire": "CI", "Korea, South": "KR", "Germany": "DE", "Mexico": "MX", "Canada": "CA",
       "Croatia": "HR", "Poland": "PL", "Egypt": "EG", "Paraguay": "PY", "Belgium": "BE", "Switzerland": "CH", "Denmark": "DK",
       "Austria": "AT", "Türkiye": "TR", "Australia": "AU", "Sweden": "SE", "Norway": "NO", "Cameroon": "CM", "Iran": "IR",
       "Kazakhstan": "KZ", "Venezuela": "VE", "Bolivia": "BO", "Costa Rica": "CR", "Panama": "PA", "Jamaica": "JM",
       "Algeria": "DZ", "Tunisia": "TN", "Qatar": "QA", "New Zealand": "NZ", "China": "CN", "Scotland": "EN", "Wales": "EN"}
COUNTRY = {v: k for k, v in ISO.items() if k not in ("Scotland", "Wales")}

# nomes de uso comum (a base traz a razão social)
COMMON = {614: "Flamengo", 1023: "Palmeiras", 199: "Corinthians", 210: "Grêmio", 2462: "Fluminense", 330: "Atlético Mineiro",
          537: "Botafogo", 585: "São Paulo", 609: "Cruzeiro", 6600: "Internacional", 679: "Athletico Paranaense",
          776: "Coritiba", 8793: "RB Bragantino", 978: "Vasco da Gama", 10010: "Bahia", 221: "Santos", 2125: "Vitória",
          17776: "Chapecoense", 3876: "Mirassol", 10997: "Remo", 69261: "Inter Miami CF", 51663: "Atlanta United",
          51772: "FC Cincinnati", 40058: "New York City FC", 623: "New York Red Bulls", 432: "Chicago Fire", 45604: "Orlando City",
          114977: "San Diego FC", 78435: "Charlotte FC", 82686: "St. Louis City SC", 4078: "CF Montréal", 51828: "Los Angeles FC",
          1061: "LA Galaxy", 1444: "Racing Club", 1234: "Independiente", 189: "Boca Juniors", 209: "River Plate",
          1775: "San Lorenzo", 1029: "Vélez Sarsfield", 1030: "Argentinos Juniors", 1286: "Newell's Old Boys",
          1418: "Rosario Central", 288: "Estudiantes de La Plata", 1106: "Gimnasia y Esgrima La Plata", 2063: "Huracán",
          19775: "Deportivo Riestra", 2402: "Defensa y Justicia", 14602: "Estudiantes de Río Cuarto", 932: "Lokomotiv Moskva",
          964: "Zenit São Petersburgo", 398: "Lazio", 12: "Roma", 46: "Inter de Milão", 5: "Milan", 506: "Juventus",
          6195: "Napoli", 800: "Atalanta", 1025: "Bologna", 416: "Torino", 430: "Fiorentina", 410: "Udinese", 1005: "Lecce"}
PREFIX = ("Club Atlético ", "Asociación Atlética ", "Club Social y Deportivo ", "Club Sportivo ", "Instituto Atlético ",
          "Racing Club Asociación Civil de ", "Club de Gimnasia y Esgrima de ", "Club Estudiantes de ")
SUFFIX = (" Saudi Football Club", " Football Club", " Saudi Club", " Sport Club", " Sports Club", " Club", " Soccer Club")


def common_name(cid: int, name: str) -> str:
    if cid in COMMON:
        return COMMON[cid]
    for p in PREFIX:
        if name.startswith(p):
            name = name[len(p):]
    for s in SUFFIX:
        if name.endswith(s) and len(name) > len(s) + 3:
            name = name[: -len(s)]
    return name.strip()


POS = {"Goalkeeper": "GK", "Defender": "DF", "Midfield": "MF", "Attack": "FW"}


def fetch(cache: Path, name: str) -> Path:
    p = cache / f"{name}.csv.gz"
    if not p.exists():
        cache.mkdir(parents=True, exist_ok=True)
        req = urllib.request.Request(BASE + f"{name}.csv.gz", headers={"User-Agent": "craquez-option-file/1.0"})
        with urllib.request.urlopen(req, timeout=300) as r, open(p, "wb") as f:
            f.write(r.read())
    return p


def rows(p: Path):
    with gzip.open(p, "rt", encoding="utf-8") as f:
        yield from csv.DictReader(f)


def ovr_of(mv: float, pos: str) -> int:
    # valor de mercado -> nota (100 mil = 58, 1 mi = 67, 10 mi = 76, 100 mi = 85; teto 92)
    o = 58 + 9 * math.log10(max(mv, 5e4) / 1e5)
    if pos == "GK":
        o += 2   # goleiros valem menos no mercado
    return int(max(48, min(92, round(o))))


def squad(cands, layout):
    """Escolhe os melhores (por valor) para cada posição do elenco do jogo; falta numa posição, completa com outras."""
    by = defaultdict(list)
    for c in sorted(cands, key=lambda c: -c["mv"]):
        by[c["pos"]].append(c)
    used, out = set(), []
    for pos in layout:
        pick = next((c for c in by[pos] if c["id"] not in used), None)
        if pick is None:
            pick = next((c for p in ("MF", "DF", "FW", "GK") if p != "GK" or pos == "GK" for c in by[p] if c["id"] not in used), None)
        out.append(pick)
        if pick:
            used.add(pick["id"])
    return out


def scaled(a, old, new):
    k = new / max(40, old)
    return [round(min(0.97, max(0.25, x * k)), 3) for x in a]


def main():
    out = Path(sys.argv[1])
    cache = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / ".cache" / "realdata"
    leagues = json.load(open(ROOT / "Assets/Craquez/Resources/Data/leagues.json", encoding="utf-8"))["leagues"]
    clubs = {int(r["club_id"]): r for r in rows(fetch(cache, "clubs"))}
    players = defaultdict(list)
    national = defaultdict(list)
    for r in rows(fetch(cache, "players")):
        if (r["last_season"] or "0") < "2025" or r["position"] not in POS:
            continue
        mv = float(r["market_value_in_eur"] or 0)
        c = {"id": r["player_id"], "n": r["name"].strip(), "pos": POS[r["position"]], "mv": mv,
             "nat": ISO.get(r["country_of_citizenship"], "")}
        if r["current_club_id"]:
            players[int(r["current_club_id"])].append(c)
        if c["nat"] and (r["international_caps"] or "0") not in ("", "0"):
            national[c["nat"]].append(c)
    teams = {}
    nfill = 0
    for lg in leagues:
        lid = lg["id"]
        for t in lg["teams"]:
            key = f"{lid}/{t['short']}"
            m = CLUBS.get(lid, {}).get(t["short"])
            entry = {}
            cands = None
            if lid == "sel":
                cands = national.get(t["iso"], [])
            elif isinstance(m, tuple):
                entry["name"] = m[0]
                entry["stadium"] = {"name": m[1]}
            elif isinstance(m, int) and m in clubs:
                c = clubs[m]
                entry["name"] = common_name(m, c["name"])
                st = {"name": c["stadium_name"]}
                if c["stadium_seats"]:
                    st["cap"] = int(c["stadium_seats"])
                entry["stadium"] = st
                cands = players.get(m, [])
            if cands:
                layout = [r["pos"] for r in t["roster"]]
                picks = squad(cands, layout)
                pl = []
                for i, (r, p) in enumerate(zip(t["roster"], picks)):
                    if p is None:
                        continue
                    o = ovr_of(p["mv"], r["pos"])
                    d = {"index": i, "n": p["n"], "ovr": o, "a": scaled(r["a"], r.get("ovr", 70), o)}
                    if p["nat"]:
                        d["nat"] = p["nat"]
                    pl.append(d)
                    nfill += 1
                if pl:
                    entry["players"] = pl
                    top = sorted((x["ovr"] for x in pl), reverse=True)[:11]
                    entry["rating"] = round(min(0.95, max(0.5, (sum(top) / len(top)) / 100 + 0.04)), 3)
            if entry:
                teams[key] = entry
    doc = {"formato": "craquez-option-file-2",
           "ajuda": "Option file CASA (uso pessoal): nomes reais de clubes, estádios e elencos (base pública transfermarkt-datasets, jul/2026).",
           "teams": teams}
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(doc, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"[casa] {len(teams)} times, {nfill} jogadores reais -> {out} ({out.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
