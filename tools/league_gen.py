#!/usr/bin/env python3
"""Gera data/leagues.json: 8 ligas da temporada 2026/27 com a composição real
de clubes (promovidos/rebaixados), cores reais de uniforme e nomes ALTERADOS
(sem licença: como os times "não licenciados" do PES). Elencos são fictícios,
com nomes e aparência coerentes com a nacionalidade de cada atleta.

Uso:  python3 tools/league_gen.py            (gera faces + ligas)
O JSON pode ser editado à mão (renomear times/jogadores, cores, atributos).
"""
from __future__ import annotations

import json
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(Path(__file__).resolve().parent))
import face_gen  # noqa: E402

SEASON = "2026/27"

# (nome alterado, sigla, cor 1 (camisa), cor 2 (calção), força 0..1)
LEAGUES = [
    {"id": "ita", "name": "Série A Itália", "country": "IT", "foreign": 0.5, "teams": [
        ("Bergamo Nerazzurri", "BGN", "#1e3a8a", "#111827", 0.82), ("Bologna Rossoblù", "BOL", "#8b1e2b", "#1e3a8a", 0.79),
        ("Cagliari Isolani", "CAG", "#9b1c2e", "#1e3a8a", 0.72), ("Como Lariani", "COM", "#2563eb", "#ffffff", 0.80),
        ("Firenze Viola", "FIR", "#6b21a8", "#ffffff", 0.78), ("Frosinone Canarini", "FRO", "#facc15", "#1e40af", 0.69),
        ("Genova Grifone", "GEN", "#9b1c2e", "#1e3a8a", 0.73), ("Milano Nerazzurri", "MIN", "#1d4ed8", "#111827", 0.86),
        ("Torino Bianconeri", "TBN", "#f8fafc", "#111827", 0.84), ("Roma Biancocelesti", "RBC", "#7dd3fc", "#ffffff", 0.79),
        ("Lecce Giallorossi", "LEC", "#facc15", "#dc2626", 0.70), ("Milano Rossoneri", "MRN", "#c8102e", "#111827", 0.84),
        ("Monza Biancorossi", "MZA", "#dc2626", "#ffffff", 0.71), ("Napoli Azzurri", "NAP", "#38bdf8", "#ffffff", 0.85),
        ("Parma Crociati", "PAR", "#f8fafc", "#111827", 0.73), ("Roma Giallorossi", "RGR", "#8b1a1a", "#f59e0b", 0.81),
        ("Sassuolo Neroverdi", "SAS", "#15803d", "#111827", 0.73), ("Torino Granata", "TGR", "#7f1d1d", "#ffffff", 0.75),
        ("Udine Bianconeri", "UDI", "#111827", "#f8fafc", 0.74), ("Venezia Arancioneroverdi", "VEN", "#111827", "#ea580c", 0.70),
    ]},
    {"id": "eng", "name": "Premier League", "country": "EN", "foreign": 0.55, "teams": [
        ("North London Red", "NLR", "#dc2626", "#ffffff", 0.87), ("Birmingham Claret", "BIC", "#7b1e3a", "#93c5fd", 0.80),
        ("Dorset Red-Black", "DOR", "#b91c1c", "#111827", 0.75), ("West London Bees", "WLB", "#e11d48", "#111827", 0.74),
        ("Sussex Seagulls", "SUS", "#1d4ed8", "#ffffff", 0.77), ("West London Blue", "WLU", "#1d4ed8", "#ffffff", 0.84),
        ("Coventry Sky Blue", "COV", "#7dd3fc", "#ffffff", 0.71), ("South London Eagles", "SLE", "#1e3a8a", "#dc2626", 0.76),
        ("Merseyside Blue", "MSB", "#1d4ed8", "#ffffff", 0.74), ("Thames Whites", "FUL", "#f8fafc", "#111827", 0.74),
        ("Humber Tigers", "HUL", "#f59e0b", "#111827", 0.69), ("Suffolk Blue", "IPS", "#1d4ed8", "#ffffff", 0.70),
        ("Yorkshire Whites", "LEE", "#f8fafc", "#1d4ed8", 0.72), ("Merseyside Red", "MSR", "#c8102e", "#c8102e", 0.86),
        ("Manchester Sky", "MCS", "#7dd3fc", "#ffffff", 0.87), ("Manchester Red", "MCR", "#da291c", "#ffffff", 0.80),
        ("Tyneside Magpies", "TYN", "#111827", "#f8fafc", 0.81), ("Nottingham Reds", "NOT", "#b91c1c", "#ffffff", 0.76),
        ("Wearside Red-White", "WEA", "#dc2626", "#111827", 0.71), ("North London White", "NLW", "#f8fafc", "#1e293b", 0.80),
    ]},
    {"id": "esp", "name": "La Liga", "country": "ES", "foreign": 0.4, "teams": [
        ("Vitoria Albiazul", "ALA", "#1d4ed8", "#ffffff", 0.71), ("Bilbao Leones", "BIL", "#dc2626", "#111827", 0.79),
        ("Madrid Colchoneros", "MAC", "#dc2626", "#1d4ed8", 0.84), ("Barcelona Blaugrana", "BAR", "#1e3a8a", "#a50044", 0.88),
        ("Vigo Celestes", "VIG", "#7dd3fc", "#ffffff", 0.75), ("A Coruña Blanquiazul", "COR", "#1d4ed8", "#ffffff", 0.70),
        ("Elche Franjiverde", "ELC", "#f8fafc", "#15803d", 0.70), ("Barcelona Periquitos", "ESP", "#2563eb", "#ffffff", 0.72),
        ("Getafe Azulones", "GET", "#1e40af", "#1e40af", 0.72), ("Valencia Granotas", "LEV", "#7f1d1d", "#1e3a8a", 0.70),
        ("Málaga Boquerones", "MAL", "#38bdf8", "#ffffff", 0.70), ("Pamplona Rojillos", "OSA", "#b91c1c", "#1e3a8a", 0.74),
        ("Santander Verdiblancos", "RAC", "#16a34a", "#ffffff", 0.70), ("Vallecas Franja", "RAY", "#f8fafc", "#dc2626", 0.74),
        ("Sevilla Verdiblancos", "BET", "#15803d", "#ffffff", 0.77), ("Madrid Merengues", "RMA", "#f8fafc", "#f8fafc", 0.88),
        ("Donostia Txuri-Urdin", "RSO", "#2563eb", "#ffffff", 0.77), ("Sevilla Nervión", "SEV", "#f8fafc", "#b91c1c", 0.74),
        ("Valencia Ches", "VAL", "#f8fafc", "#111827", 0.74), ("Vila-real Submarino", "VLR", "#facc15", "#1e3a8a", 0.80),
    ]},
    {"id": "fra", "name": "Ligue 1", "country": "FR", "foreign": 0.45, "teams": [
        ("Angers Noir-Blanc", "ANG", "#111827", "#f8fafc", 0.69), ("Auxerre Bleu-Blanc", "AUX", "#f8fafc", "#1d4ed8", 0.70),
        ("Brest Rouge", "BRE", "#dc2626", "#ffffff", 0.73), ("Havre Ciel", "HAV", "#7dd3fc", "#1e3a8a", 0.69),
        ("Le Mans Rouge-Jaune", "LMN", "#dc2626", "#facc15", 0.67), ("Lens Sang et Or", "LEN", "#facc15", "#dc2626", 0.78),
        ("Lille Dogues", "LIL", "#dc2626", "#1e3a8a", 0.79), ("Lorient Merlus", "LOR", "#f97316", "#111827", 0.69),
        ("Lyon Gones", "LYO", "#f8fafc", "#1d4ed8", 0.79), ("Marseille Olympiens", "MAR", "#f8fafc", "#38bdf8", 0.81),
        ("Monaco Rouge-Blanc", "MCO", "#dc2626", "#ffffff", 0.80), ("Nice Aiglons", "NIC", "#dc2626", "#111827", 0.75),
        ("Paris Bleu", "PFC", "#1e3a8a", "#ffffff", 0.72), ("Paris Capitale", "PSG", "#1e3a8a", "#dc2626", 0.88),
        ("Rennes Rouge-Noir", "REN", "#dc2626", "#111827", 0.76), ("Strasbourg Bleu", "STR", "#1d4ed8", "#ffffff", 0.76),
        ("Toulouse Violet", "TOU", "#7c3aed", "#ffffff", 0.73), ("Troyes Azur", "TRO", "#1d4ed8", "#f8fafc", 0.68),
    ]},
    {"id": "bra", "name": "Série A Brasil", "country": "BR", "foreign": 0.12, "teams": [
        ("Furacão Paranaense", "FUR", "#dc2626", "#111827", 0.74), ("Galo Mineiro", "GAL", "#111827", "#f8fafc", 0.78),
        ("Tricolor Baiano", "BAH", "#1d4ed8", "#ffffff", 0.76), ("Estrela Solitária", "EST", "#111827", "#f8fafc", 0.78),
        ("Verdão do Oeste", "CHA", "#15803d", "#ffffff", 0.68), ("Timão Paulista", "TIM", "#f8fafc", "#111827", 0.76),
        ("Coxa Branca", "CXA", "#f8fafc", "#15803d", 0.70), ("Raposa Celeste", "RAP", "#1d4ed8", "#ffffff", 0.79),
        ("Rubro-Negro Carioca", "RNC", "#c8102e", "#111827", 0.84), ("Tricolor Carioca", "TRC", "#7f1d1d", "#15803d", 0.77),
        ("Tricolor Gaúcho", "TRG", "#38bdf8", "#111827", 0.75), ("Colorado Gaúcho", "CLR", "#dc2626", "#ffffff", 0.76),
        ("Leão Caipira", "LEA", "#facc15", "#15803d", 0.72), ("Verdão Paulista", "VPA", "#166534", "#ffffff", 0.84),
        ("Massa Bruta", "MBR", "#f8fafc", "#dc2626", 0.75), ("Leão Azul", "LAZ", "#1e3a8a", "#ffffff", 0.68),
        ("Peixe Praiano", "PEI", "#f8fafc", "#111827", 0.74), ("Tricolor Paulista", "TRP", "#f8fafc", "#dc2626", 0.77),
        ("Gigante da Colina", "GIG", "#111827", "#f8fafc", 0.74), ("Leão da Barra", "LBA", "#dc2626", "#111827", 0.70),
    ]},
    {"id": "brb", "name": "Série B Brasil", "country": "BR", "foreign": 0.06, "teams": [
        ("Vozão", "VOZ", "#111827", "#f8fafc", 0.70), ("Leão do Pici", "PIC", "#1d4ed8", "#dc2626", 0.72),
        ("Papo Serrano", "PAP", "#15803d", "#ffffff", 0.68), ("Leão da Ilha", "ILH", "#dc2626", "#111827", 0.69),
        ("Macaca Campineira", "MCC", "#111827", "#f8fafc", 0.66), ("Tubarão Paranaense", "TUB", "#38bdf8", "#ffffff", 0.65),
        ("Timbu Recifense", "TBU", "#dc2626", "#ffffff", 0.65), ("Tigre do ABC", "SBC", "#facc15", "#111827", 0.64),
        ("Tigre Catarinense", "CRI", "#facc15", "#111827", 0.67), ("Esmeraldino", "ESM", "#15803d", "#ffffff", 0.68),
        ("Tigre do Vale", "NOV", "#f59e0b", "#111827", 0.68), ("Galo de Alagoas", "CRB", "#dc2626", "#ffffff", 0.66),
        ("Leão do Sul", "AVA", "#1d4ed8", "#ffffff", 0.66), ("Dourado", "DOU", "#facc15", "#15803d", 0.67),
        ("Dragão Goiano", "DRA", "#dc2626", "#111827", 0.67), ("Fantasma", "FAN", "#111827", "#f8fafc", 0.65),
        ("Tigrão Goiano", "TIG", "#dc2626", "#ffffff", 0.66), ("Coelho Mineiro", "COE", "#15803d", "#111827", 0.68),
        ("Esquadrão de Aço", "ACO", "#111827", "#f8fafc", 0.64), ("Pantera Paulista", "PTR", "#dc2626", "#ffffff", 0.64),
    ]},
    {"id": "mls", "name": "MLS", "country": "US", "foreign": 0.5, "teams": [
        ("Atlanta Five Stripes", "ATL", "#b91c1c", "#111827", 0.72), ("Charlotte Crown", "CLT", "#38bdf8", "#111827", 0.72),
        ("Chicago Flame", "CHI", "#dc2626", "#ffffff", 0.70), ("Cincinnati Orange", "CIN", "#f97316", "#1e3a8a", 0.74),
        ("Columbus Gold", "CLB", "#facc15", "#111827", 0.74), ("Capital Black", "DCU", "#111827", "#dc2626", 0.68),
        ("Miami Pink", "MIA", "#f9a8d4", "#111827", 0.77), ("Montréal Bleu", "MTL", "#1e3a8a", "#111827", 0.68),
        ("Nashville Gold", "NSH", "#fde047", "#1e3a8a", 0.72), ("New England Navy", "NER", "#1e3a8a", "#dc2626", 0.69),
        ("New York Sky", "NYC", "#7dd3fc", "#1e3a8a", 0.72), ("New Jersey Red-White", "NJR", "#f8fafc", "#dc2626", 0.70),
        ("Orlando Purple", "ORL", "#6d28d9", "#ffffff", 0.72), ("Philadelphia Navy-Gold", "PHI", "#1e3a8a", "#facc15", 0.73),
        ("Toronto Red", "TOR", "#b91c1c", "#111827", 0.69), ("Austin Verde", "AUS", "#16a34a", "#111827", 0.70),
        ("Colorado Burgundy", "COL", "#7f1d1d", "#7dd3fc", 0.69), ("Dallas Hoops", "DAL", "#dc2626", "#1d4ed8", 0.69),
        ("Houston Orange", "HOU", "#ea580c", "#111827", 0.70), ("Los Angeles Stars", "LAG", "#f8fafc", "#1e3a8a", 0.72),
        ("Los Angeles Black-Gold", "LAF", "#111827", "#d4a017", 0.76), ("Minnesota Loons", "MNU", "#111827", "#7dd3fc", 0.71),
        ("Portland Timber", "POR", "#15803d", "#facc15", 0.70), ("Salt Lake Claret", "RSL", "#9f1239", "#1e3a8a", 0.70),
        ("San Diego Azul", "SDF", "#1e40af", "#7dd3fc", 0.73), ("San Jose Quakes", "SJE", "#1e3a8a", "#111827", 0.68),
        ("Seattle Rave", "SEA", "#65a30d", "#1e3a8a", 0.74), ("Kansas City Sporting", "SKC", "#93c5fd", "#1e3a8a", 0.69),
        ("St. Louis Red", "STL", "#be123c", "#1e3a8a", 0.69), ("Vancouver Whitecap", "VAN", "#f8fafc", "#1e3a8a", 0.74),
    ]},
    {"id": "arg", "name": "Liga Argentina", "country": "AR", "foreign": 0.08, "teams": [
        ("Tiburón Marplatense", "ALD", "#facc15", "#15803d", 0.66), ("Bichos Colorados", "ARJ", "#dc2626", "#ffffff", 0.72),
        ("Decano Tucumano", "ATU", "#7dd3fc", "#ffffff", 0.68), ("Taladro Sur", "BAN", "#15803d", "#ffffff", 0.68),
        ("Guapo de Barracas", "BCA", "#dc2626", "#f8fafc", 0.67), ("Pirata Cordobés", "BEL", "#38bdf8", "#111827", 0.70),
        ("Xeneize Azul y Oro", "XEN", "#1e3a8a", "#facc15", 0.80), ("Ferroviario Santiagueño", "CCO", "#111827", "#f8fafc", 0.66),
        ("Halcón de Varela", "DYJ", "#facc15", "#15803d", 0.70), ("Pincha Platense", "PIN", "#dc2626", "#ffffff", 0.74),
        ("Celeste de Río Cuarto", "ERC", "#7dd3fc", "#ffffff", 0.65), ("Lobo Platense", "LOB", "#f8fafc", "#1e3a8a", 0.69),
        ("Lobo Mendocino", "GIM", "#111827", "#f8fafc", 0.65), ("Globo de Patricios", "GLO", "#f8fafc", "#dc2626", 0.70),
        ("Rojo de Avellaneda", "ROJ", "#dc2626", "#111827", 0.74), ("Azul Mendocino", "IRI", "#1e3a8a", "#ffffff", 0.67),
        ("Gloria Cordobesa", "INS", "#dc2626", "#ffffff", 0.67), ("Granate del Sur", "GRA", "#7f1d1d", "#ffffff", 0.73),
        ("Leprosos Rosarinos", "LEP", "#dc2626", "#111827", 0.70), ("Calamar", "CAL", "#f8fafc", "#7f1d1d", 0.68),
        ("Academia Celeste", "ACA", "#7dd3fc", "#ffffff", 0.77), ("Millonario Banda Roja", "MIL", "#f8fafc", "#dc2626", 0.81),
        ("Canalla Rosarino", "CNL", "#1e3a8a", "#facc15", 0.72), ("Malevo de Bajo Flores", "RIE", "#111827", "#f8fafc", 0.65),
        ("Ciclón de Boedo", "CIC", "#1e3a8a", "#b91c1c", 0.72), ("Verde de Junín", "SAR", "#15803d", "#ffffff", 0.65),
        ("Matador Cordobés", "MAT", "#1e3a8a", "#f8fafc", 0.74), ("Matador de Victoria", "VIC", "#1e3a8a", "#dc2626", 0.69),
        ("Tatengue", "TAT", "#f8fafc", "#dc2626", 0.69), ("Fortín de Liniers", "FTN", "#f8fafc", "#1d4ed8", 0.74),
    ]},
    {"id": "uru", "name": "Liga Uruguaia", "country": "UY", "foreign": 0.08, "teams": [
        ("Carbonero Aurinegro", "CAR", "#facc15", "#111827", 0.78), ("Tricolor Bolso", "BOL", "#f8fafc", "#1e3a8a", 0.78),
        ("Violeta del Parque", "VIO", "#7c3aed", "#f8fafc", 0.70), ("Franja Danubiana", "DAN", "#f8fafc", "#111827", 0.68),
        ("Negriazul de Belvedere", "LVP", "#111827", "#1d4ed8", 0.71), ("Bohemio de Montevideo", "BOH", "#f8fafc", "#111827", 0.67),
        ("Darsenero", "DAR", "#dc2626", "#f8fafc", 0.66), ("Cervecero", "CER", "#15803d", "#f8fafc", 0.66),
        ("Sastre", "SAS", "#dc2626", "#15803d", 0.67), ("Villero", "VLL", "#7dd3fc", "#f8fafc", 0.65),
        ("Arachán", "ARA", "#1d4ed8", "#facc15", 0.66), ("Pedrense", "PED", "#f8fafc", "#dc2626", 0.65),
        ("Gaucho del Pantanoso", "GAU", "#facc15", "#dc2626", 0.64), ("Rojo Fernandino", "FER", "#dc2626", "#15803d", 0.65),
        ("Pionero", "PIO", "#dc2626", "#1e3a8a", 0.64), ("Palermitano", "PAL", "#dc2626", "#1d4ed8", 0.63),
    ]},
    {"id": "per", "name": "Liga Peruana", "country": "PE", "foreign": 0.12, "teams": [
        ("Crema Limeño", "CRE", "#fef3c7", "#111827", 0.74), ("Blanquiazul Íntimo", "INT", "#1e3a8a", "#f8fafc", 0.73),
        ("Celeste Rimense", "CEL", "#38bdf8", "#f8fafc", 0.73), ("Dominó Arequipeño", "DOM", "#dc2626", "#111827", 0.70),
        ("Papá Cusqueño", "PAP", "#dc2626", "#f8fafc", 0.68), ("Dorado Imperial", "DOR", "#facc15", "#111827", 0.68),
        ("Rojo Matador", "HUA", "#dc2626", "#f8fafc", 0.67), ("Tarmeño", "ADT", "#1d4ed8", "#f8fafc", 0.64),
        ("Albo Piurano", "GRA", "#f8fafc", "#111827", 0.65), ("Vendaval Sullanense", "SUL", "#f8fafc", "#1e3a8a", 0.64),
        ("Comerciante Cutervino", "CUT", "#1d4ed8", "#facc15", 0.64), ("Celeste Cusqueño", "GAR", "#7dd3fc", "#f8fafc", 0.65),
        ("Chongoyapano", "JP2", "#facc15", "#1d4ed8", 0.63), ("Andahuaylino", "CHK", "#15803d", "#facc15", 0.64),
        ("Rosado Chalaco", "SBA", "#f9a8d4", "#111827", 0.65), ("Gavilán Cajamarquino", "UTC", "#1e3a8a", "#facc15", 0.65),
        ("Moqueguano", "MOQ", "#facc15", "#111827", 0.62), ("Cajamarquino", "CAJ", "#f8fafc", "#dc2626", 0.62),
    ]},
    {"id": "chi", "name": "Liga Chilena", "country": "CL", "foreign": 0.12, "teams": [
        ("Pirata Coquimbano", "COQ", "#facc15", "#111827", 0.72), ("Cruzado", "CRU", "#f8fafc", "#1d4ed8", 0.73),
        ("Capo de Rancagua", "OHI", "#38bdf8", "#f8fafc", 0.69), ("Romántico Viajero", "ROM", "#1e3a8a", "#f8fafc", 0.73),
        ("Itálico", "ITA", "#15803d", "#f8fafc", 0.68), ("Árabe Tricolor", "ARB", "#15803d", "#dc2626", 0.69),
        ("Minero de El Salvador", "MIN", "#f97316", "#f8fafc", 0.66), ("Cacique Albo", "CAC", "#f8fafc", "#111827", 0.75),
        ("Acerero", "ACE", "#1d4ed8", "#111827", 0.68), ("Diablo Rojo de Chillán", "CHL", "#dc2626", "#f8fafc", 0.66),
        ("Limachino", "LIM", "#dc2626", "#facc15", 0.63), ("Cementero", "CEM", "#dc2626", "#f8fafc", 0.66),
        ("Papayero", "PAY", "#dc2626", "#1e3a8a", 0.64), ("Ruletero Viñamarino", "RUL", "#1e3a8a", "#facc15", 0.66),
        ("Campanil", "CAM", "#facc15", "#1d4ed8", 0.64), ("Lila Penquista", "LIL", "#a855f7", "#f8fafc", 0.63),
    ]},
    {"id": "ecu", "name": "Liga Equatoriana", "country": "EC", "foreign": 0.12, "teams": [
        ("Albo Quiteño", "LDU", "#f8fafc", "#1e3a8a", 0.77), ("Rayado del Valle", "IDV", "#111827", "#1d4ed8", 0.78),
        ("Ídolo del Astillero", "IDO", "#facc15", "#111827", 0.74), ("Bombillo Eléctrico", "BOM", "#1d4ed8", "#f8fafc", 0.71),
        ("Oriental Quiteño", "AUC", "#facc15", "#dc2626", 0.69), ("Camarata", "CAT", "#38bdf8", "#f8fafc", 0.69),
        ("Morlaco", "MOR", "#dc2626", "#f8fafc", 0.67), ("Bananero", "BAN", "#15803d", "#f8fafc", 0.67),
        ("Celeste Ambateño", "MAC", "#7dd3fc", "#f8fafc", 0.66), ("Ponchito", "PON", "#dc2626", "#facc15", 0.65),
        ("Lojano Libre", "LIB", "#dc2626", "#f8fafc", 0.65), ("Atunero", "ATU", "#1d4ed8", "#f8fafc", 0.64),
        ("Cetáceo Riobambeño", "DAQ", "#facc15", "#1d4ed8", 0.64), ("Rodillo Rojo", "TEC", "#dc2626", "#f8fafc", 0.64),
        ("Ciudadano", "CIU", "#38bdf8", "#1e3a8a", 0.64), ("León del Norte", "LEO", "#facc15", "#111827", 0.63),
    ]},
    {"id": "sau", "name": "Liga Saudita", "country": "SA", "foreign": 0.45, "teams": [
        ("Riyadh Azul", "HIL", "#1d4ed8", "#f8fafc", 0.84), ("Riyadh Amarelo", "NAS", "#facc15", "#1d4ed8", 0.83),
        ("Jeddah Tigres", "ITT", "#facc15", "#111827", 0.82), ("Jeddah Real", "AHL", "#15803d", "#f8fafc", 0.81),
        ("Khobar Real", "QAD", "#dc2626", "#facc15", 0.78), ("Dammam Comandos", "ETT", "#15803d", "#dc2626", 0.74),
        ("Buraidah Fênix", "TAA", "#facc15", "#111827", 0.74), ("Riyadh Juventude", "SHB", "#f8fafc", "#111827", 0.74),
        ("Hofuf Conquistador", "FAT", "#1e3a8a", "#f8fafc", 0.71), ("Saihat Golfo", "KHA", "#facc15", "#15803d", 0.70),
        ("Majmaah Laranja", "FAY", "#f97316", "#1e3a8a", 0.70), ("Rass Eterno", "KHO", "#dc2626", "#facc15", 0.68),
        ("Khamis Montanha", "DAM", "#facc15", "#dc2626", 0.69), ("Rass Firme", "HAZ", "#dc2626", "#f8fafc", 0.67),
        ("Tabuk Futuro", "NEO", "#111827", "#facc15", 0.69), ("Abha Serrano", "ABH", "#15803d", "#f8fafc", 0.66),
        ("Harmah Real", "FAI", "#facc15", "#1e3a8a", 0.66), ("Diriyah Histórico", "DIR", "#a16207", "#f8fafc", 0.65),
    ]},
    {"id": "ned", "name": "Liga Holandesa", "country": "NL", "foreign": 0.4, "teams": [
        ("Amsterdam Godenzonen", "AMS", "#f8fafc", "#dc2626", 0.80), ("Eindhoven Boeren", "EIN", "#dc2626", "#f8fafc", 0.82),
        ("Rotterdam Stadionclub", "ROT", "#dc2626", "#111827", 0.81), ("Alkmaar Kaaskoppen", "ALK", "#dc2626", "#f8fafc", 0.77),
        ("Enschede Tukkers", "ENS", "#dc2626", "#f8fafc", 0.76), ("Utrecht Domstedelingen", "UTR", "#dc2626", "#f8fafc", 0.74),
        ("Deventer Eagles", "DEV", "#facc15", "#dc2626", 0.72), ("Friese Pompeblêden", "FRI", "#1d4ed8", "#f8fafc", 0.71),
        ("Nijmegen Groenrood", "NIJ", "#dc2626", "#15803d", 0.73), ("Rotterdam Kasteel", "SPA", "#dc2626", "#111827", 0.70),
        ("Sittard Fortuna", "SIT", "#facc15", "#15803d", 0.70), ("Groningen Trots", "GRO", "#15803d", "#f8fafc", 0.70),
        ("Zwolle Blauwvingers", "ZWO", "#1d4ed8", "#f8fafc", 0.69), ("Kralingen Rood-Zwart", "EXC", "#dc2626", "#111827", 0.68),
        ("Almelo Heraclieden", "ALM", "#111827", "#f8fafc", 0.68), ("Den Haag Ooievaars", "HAA", "#facc15", "#15803d", 0.68),
        ("Leeuwarden Geel-Blauw", "LWD", "#facc15", "#1d4ed8", 0.67), ("Tilburg Tricolores", "TIL", "#dc2626", "#1d4ed8", 0.67),
    ]},
    {"id": "por", "name": "Liga Portuguesa", "country": "PT", "foreign": 0.45, "teams": [
        ("Lisboa Águias", "SLB", "#dc2626", "#f8fafc", 0.84), ("Porto Dragões", "FCP", "#1d4ed8", "#f8fafc", 0.84),
        ("Lisboa Leões", "SCP", "#15803d", "#f8fafc", 0.84), ("Braga Arsenalistas", "SCB", "#dc2626", "#f8fafc", 0.78),
        ("Guimarães Conquistadores", "VSC", "#f8fafc", "#111827", 0.73), ("Famalicão Fama", "FAM", "#1e3a8a", "#f8fafc", 0.72),
        ("Barcelos Galos", "GIL", "#dc2626", "#1e3a8a", 0.71), ("Estoril Canarinhos", "EST", "#facc15", "#1e3a8a", 0.70),
        ("Lisboa Gansos", "CPA", "#111827", "#f8fafc", 0.69), ("Moreira Cónegos", "MOR", "#15803d", "#f8fafc", 0.69),
        ("Vila do Conde Rioavistas", "RAV", "#15803d", "#f8fafc", 0.69), ("Arouca Arouquenses", "ARO", "#facc15", "#1e3a8a", 0.69),
        ("Açores Arquipélago", "SCL", "#dc2626", "#f8fafc", 0.70), ("Madeira Alvinegros", "NAC", "#111827", "#f8fafc", 0.68),
        ("Amadora Tricolor", "EAM", "#dc2626", "#15803d", 0.67), ("Alverca Ribatejanos", "ALV", "#dc2626", "#1d4ed8", 0.66),
        ("Madeira Verde-Rubros", "MRT", "#15803d", "#dc2626", 0.68), ("Viseu Viriatos", "VIS", "#111827", "#f8fafc", 0.66),
    ]},
    {"id": "rus", "name": "Liga Russa", "country": "RU", "foreign": 0.3, "teams": [
        ("São Petersburgo Azul", "ZEN", "#38bdf8", "#f8fafc", 0.82), ("Krasnodar Touros", "KRA", "#15803d", "#111827", 0.80),
        ("Moscou Vermelho-Branco", "SPM", "#dc2626", "#f8fafc", 0.78), ("Moscou Exército", "CSK", "#dc2626", "#1e3a8a", 0.78),
        ("Moscou Ferroviários", "LOK", "#dc2626", "#15803d", 0.77), ("Moscou Azul-Branco", "DMO", "#1d4ed8", "#f8fafc", 0.76),
        ("Kazan Rubi", "RUB", "#7f1d1d", "#15803d", 0.71), ("Rostov Amarelo-Azul", "ROS", "#facc15", "#1e3a8a", 0.71),
        ("Grozny Verde", "AKH", "#15803d", "#f8fafc", 0.69), ("Samara Asas", "KRY", "#1e3a8a", "#f8fafc", 0.70),
        ("Kaliningrado Báltico", "BAL", "#1d4ed8", "#f8fafc", 0.71), ("Níjni Novgorod", "NNO", "#1e3a8a", "#facc15", 0.67),
        ("Togliatti Akron", "AKR", "#dc2626", "#111827", 0.67), ("Makhachkala Dínamo", "DMK", "#1d4ed8", "#f8fafc", 0.68),
        ("Moscou Pátria", "ROD", "#1e3a8a", "#dc2626", 0.66), ("Voronej Tocha", "FAK", "#1e3a8a", "#facc15", 0.66),
    ]},
    {"id": "ukr", "name": "Liga Ucraniana", "country": "UA", "foreign": 0.25, "teams": [
        ("Donetsk Mineiros", "SHA", "#f97316", "#111827", 0.80), ("Kiev Azul-Branco", "DKY", "#f8fafc", "#1d4ed8", 0.79),
        ("Jitomir Lobos", "POL", "#facc15", "#15803d", 0.74), ("Kryvyi Rih Mineiros", "KRV", "#dc2626", "#f8fafc", 0.72),
        ("Luhansk Estrela", "ZOR", "#111827", "#f8fafc", 0.71), ("Cherkasy Cerejas", "LNZ", "#f8fafc", "#dc2626", 0.71),
        ("Lviv Leões Verdes", "KAR", "#15803d", "#f8fafc", 0.71), ("Kiev Obolon", "OBO", "#1d4ed8", "#f8fafc", 0.66),
        ("Kovalivka Espiga", "KOL", "#facc15", "#15803d", 0.67), ("Rivne Urze", "VER", "#15803d", "#f8fafc", 0.67),
        ("Kharkiv Metalurgistas", "MET", "#facc15", "#1d4ed8", 0.70), ("Kudrivka Vermelho", "KUD", "#dc2626", "#111827", 0.65),
        ("Kamianets Epicentro", "EPI", "#f97316", "#f8fafc", 0.66), ("Kiev Margem Esquerda", "LBK", "#111827", "#facc15", 0.65),
        ("Chernivtsi Bucovina", "BUK", "#facc15", "#111827", 0.65), ("Odessa Mar Negro", "CHO", "#1d4ed8", "#111827", 0.66),
    ]},
]

# ---------------------------------------------------------------- nomes por nacionalidade
NAMES = {
    "BR": (["Lucas", "Gabriel", "Matheus", "Pedro", "Rafael", "Gustavo", "Felipe", "João", "Vitor", "Bruno", "Thiago", "Caio",
            "Diego", "Renan", "Igor", "Wesley", "Everton", "Rodrigo", "Danilo", "Luan", "Kauã", "Douglas", "Alan", "Ruan",
            "Marcos", "André", "Leandro", "Hugo", "Yuri", "Murilo"],
           ["Silva", "Santos", "Oliveira", "Souza", "Lima", "Pereira", "Costa", "Ferreira", "Almeida", "Ribeiro", "Carvalho",
            "Gomes", "Martins", "Araújo", "Barbosa", "Rocha", "Moraes", "Nascimento", "Cardoso", "Teixeira", "Freitas",
            "Moreira", "Pinto", "Vieira", "Batista", "Campos", "Mendes", "Duarte", "Fonseca", "Machado"],
           ["Juninho", "Biel", "Netinho", "Tchê", "Pedrinho", "Thiaguinho", "Zé Vitor", "Gui", "Bidu", "Cacá",
            "Joãozinho", "Toninho", "Fumaça", "Pirulito", "Tuta", "Dinho", "Careca", "Baixinho", "Cuca", "Nonato"]),
    "AR": (["Matías", "Santiago", "Nicolás", "Facundo", "Lautaro", "Franco", "Agustín", "Tomás", "Joaquín", "Gonzalo", "Lucas",
            "Ezequiel", "Federico", "Maximiliano", "Rodrigo", "Julián", "Valentín", "Thiago", "Bruno", "Leandro", "Iván",
            "Emiliano", "Alan", "Cristian", "Mauro"],
           ["González", "Rodríguez", "Fernández", "López", "Martínez", "Pérez", "Gómez", "Sánchez", "Romero", "Díaz",
            "Álvarez", "Torres", "Ruiz", "Ramírez", "Flores", "Acosta", "Benítez", "Medina", "Herrera", "Suárez", "Aguirre",
            "Giménez", "Molina", "Castro", "Ortiz", "Rojas", "Sosa", "Paredes", "Ledesma", "Quiroga"], []),
    "IT": (["Alessandro", "Lorenzo", "Matteo", "Andrea", "Francesco", "Davide", "Federico", "Riccardo", "Simone", "Marco",
            "Luca", "Gianluca", "Nicolò", "Tommaso", "Giacomo", "Stefano", "Filippo", "Emanuele", "Daniele", "Pietro"],
           ["Rossi", "Russo", "Ferrari", "Esposito", "Bianchi", "Romano", "Colombo", "Ricci", "Marino", "Greco", "Bruno",
            "Gallo", "Conti", "De Luca", "Costa", "Giordano", "Mancini", "Rizzo", "Lombardi", "Moretti", "Barbieri",
            "Fontana", "Santoro", "Mariani", "Rinaldi", "Caruso", "Ferrara", "Galli", "Martini", "Leone"], []),
    "ES": (["Pablo", "Álvaro", "Hugo", "Adrián", "Sergio", "Javier", "Daniel", "Carlos", "Iker", "Unai", "Mikel", "Marc",
            "Pau", "Raúl", "Rubén", "Jorge", "Alejandro", "Iván", "Aitor", "Óscar", "Víctor", "Nacho", "Dani", "Borja"],
           ["García", "Martínez", "López", "Sánchez", "González", "Gómez", "Fernández", "Moreno", "Jiménez", "Hernández",
            "Muñoz", "Navarro", "Romero", "Torres", "Domínguez", "Vázquez", "Ramos", "Gil", "Serrano", "Blanco", "Molina",
            "Ortega", "Delgado", "Castro", "Ortiz", "Rubio", "Marín", "Sanz", "Iglesias", "Núñez"], []),
    "EN": (["Harry", "Jack", "James", "Oliver", "George", "Charlie", "Thomas", "Jacob", "Alfie", "Callum", "Mason", "Lewis",
            "Ryan", "Jordan", "Ben", "Kieran", "Declan", "Reece", "Marcus", "Ethan", "Tyler", "Josh", "Luke", "Aaron"],
           ["Smith", "Jones", "Taylor", "Brown", "Williams", "Wilson", "Johnson", "Davies", "Robinson", "Wright", "Thompson",
            "Evans", "Walker", "White", "Roberts", "Green", "Hall", "Wood", "Jackson", "Clarke", "Harris", "Lewis", "Young",
            "Hughes", "Edwards", "Turner", "Hill", "Cooper", "Ward", "Morris"], []),
    "FR": (["Hugo", "Lucas", "Théo", "Mathis", "Nathan", "Enzo", "Louis", "Maxime", "Antoine", "Kévin", "Rayan", "Yanis",
            "Benjamin", "Clément", "Jules", "Adrien", "Romain", "Florian", "Moussa", "Ibrahim", "Samuel", "Bastien", "Quentin"],
           ["Martin", "Bernard", "Dubois", "Thomas", "Robert", "Richard", "Petit", "Durand", "Leroy", "Moreau", "Simon",
            "Laurent", "Lefebvre", "Michel", "Garcia", "David", "Bertrand", "Roux", "Vincent", "Fournier", "Morel",
            "Girard", "Diallo", "Traoré", "Camara", "Koné", "Mendy", "Sissoko", "Bakayoko", "Fofana"], []),
    "US": (["Tyler", "Brandon", "Christian", "Josh", "Kevin", "Jordan", "Chris", "Matt", "Ryan", "Cole", "Jesus", "Diego",
            "Brenden", "Sean", "Paxton", "Caden", "Miles", "Gianluca", "DeAndre", "Malik", "Logan", "Aidan"],
           ["Smith", "Johnson", "Miller", "Davis", "Garcia", "Rodriguez", "Martinez", "Anderson", "Taylor", "Thomas",
            "Moore", "Jackson", "Martin", "Lee", "Thompson", "White", "Harris", "Clark", "Lewis", "Robinson", "Walker",
            "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores"], []),
    "PT": (["Duarte", "Henrique", "Tomás", "Rodrigo", "Miguel", "Afonso", "Martim", "Simão", "Salvador", "Gaspar"],
           ["Lopes", "Gonçalves", "Marques", "Alves", "Carvalho", "Teixeira", "Correia", "Mendes", "Nunes", "Soares"], []),
    "NL": (["Daan", "Lars", "Sem", "Jesse", "Bram", "Ruben", "Stijn", "Thijs", "Niels", "Joris"],
           ["Jansen", "de Wit", "Bos", "Vos", "Peters", "Hendriks", "Dekker", "Brouwer", "Kok", "van Leeuwen"], []),
    "DE": (["Jonas", "Felix", "Niklas", "Lukas", "Moritz", "Paul", "Elias", "Finn", "Tobias", "Jannik"],
           ["Schmidt", "Schneider", "Fischer", "Weber", "Wagner", "Becker", "Hoffmann", "Koch", "Richter", "Klein"], []),
    "NG": (["Chinedu", "Emeka", "Tunde", "Segun", "Ifeanyi", "Obinna", "Uche", "Kunle", "Femi", "Chidi"],
           ["Okafor", "Eze", "Adeyemi", "Okonkwo", "Nwosu", "Balogun", "Obi", "Adebayo", "Chukwu", "Okoro"], []),
    "SN": (["Mamadou", "Ousmane", "Abdoulaye", "Aliou", "Babacar", "Modou", "Serigne", "Ibrahima", "Assane", "Malick"],
           ["Ndiaye", "Diop", "Fall", "Seck", "Faye", "Sow", "Mbaye", "Thiam", "Ndour", "Sy"], []),
    "CI": (["Koffi", "Yao", "Kouadio", "Aya", "Brou", "Kouamé", "Didier", "Arsène", "Hervé", "Aboubacar"],
           ["Kouassi", "Yao", "Koffi", "N'Guessan", "Brou", "Kouamé", "Tanoh", "Gnahoré", "Zadi", "Ouattara"], []),
    "GH": (["Kwame", "Kofi", "Kwabena", "Yaw", "Kojo", "Kwaku", "Fiifi", "Nana", "Ebo", "Kwesi"],
           ["Mensah", "Owusu", "Asamoah", "Appiah", "Acheampong", "Darko", "Ofori", "Amoako", "Boakye", "Quaye"], []),
    "MA": (["Youssef", "Mehdi", "Hamza", "Anas", "Omar", "Karim", "Reda", "Ayoub", "Zakaria", "Nabil"],
           ["El Amrani", "Benali", "Haddadi", "Bennani", "El Idrissi", "Tahiri", "Chakir", "Saidi", "Ouazzani", "Lahlou"], []),
    "CO": (["Andrés", "Camilo", "Sebastián", "Esteban", "Julián", "Felipe", "Santiago", "Mateo", "Duván", "Brayan"],
           ["Restrepo", "Ospina", "Valencia", "Cárdenas", "Mosquera", "Rentería", "Zapata", "Palacios", "Quintero", "Ibarra"], []),
    "UY": (["Martín", "Matías", "Bruno", "Gastón", "Joaquín", "Santiago", "Franco", "Diego", "Emiliano", "Ignacio"],
           ["Pereyra", "Silvera", "Méndez", "Correa", "Acosta", "Fagúndez", "Sosa", "De León", "Techera", "Larrosa"], []),
    "MX": (["Alejandro", "Emiliano", "Diego", "Fernando", "Ricardo", "Ángel", "Bryan", "Uriel", "Héctor", "Arturo"],
           ["Ramírez", "Castillo", "Mendoza", "Aguilar", "Rosales", "Navarro", "Salazar", "Cervantes", "Ochoa", "Villalobos"], []),
    "JP": (["Haruto", "Sota", "Yuto", "Riku", "Kaito", "Ren", "Hayato", "Shun", "Kenta", "Daiki"],
           ["Tanaka", "Suzuki", "Takahashi", "Watanabe", "Ito", "Yamamoto", "Nakamura", "Kobayashi", "Saito", "Matsumoto"], []),
    "KR": (["Ji-ho", "Min-seo", "Do-yun", "Seo-jun", "Ha-joon", "Jun-woo", "Hyun-woo", "Ye-jun"],
           ["Kim", "Lee", "Park", "Choi", "Jung", "Kang", "Cho", "Yoon"], []),
    "PE": (["Renato", "José", "Carlos", "Diego", "Alexander", "Luis", "Martín", "Edison", "Kevin", "Bryan", "Jesús", "Aldair"],
           ["Quispe", "Flores", "Huamán", "Chávez", "Ramos", "Mendoza", "Vargas", "Rojas", "Ccori", "Paucar", "Ticona", "Yupanqui"], []),
    "CL": (["Matías", "Benjamín", "Vicente", "Joaquín", "Felipe", "Ignacio", "Cristóbal", "Diego", "Sebastián", "Bastián"],
           ["Muñoz", "Rojas", "Díaz", "Soto", "Contreras", "Silva", "Sepúlveda", "Morales", "Fuentes", "Valenzuela"], []),
    "EC": (["Carlos", "Jorge", "Bryan", "Andrés", "Jordy", "Anthony", "Dixon", "Segundo", "Romario", "Fidel"],
           ["Caicedo", "Estupiñán", "Mina", "Arboleda", "Preciado", "Corozo", "Angulo", "Valencia", "Quiñónez", "Plata"], []),
    "SA": (["Abdullah", "Mohammed", "Faisal", "Salem", "Fahad", "Nasser", "Khalid", "Saud", "Turki", "Yasser", "Hattan", "Ziyad"],
           ["Al-Qahtani", "Al-Harbi", "Al-Otaibi", "Al-Shehri", "Al-Dosari", "Al-Ghamdi", "Al-Zahrani", "Al-Mutairi", "Al-Subaie", "Al-Malki"], []),
    "RU": (["Aleksandr", "Dmitri", "Ivan", "Maksim", "Artyom", "Nikita", "Kirill", "Sergei", "Andrei", "Pavel", "Yegor", "Danil"],
           ["Ivanov", "Smirnov", "Kuznetsov", "Popov", "Sokolov", "Lebedev", "Kozlov", "Novikov", "Morozov", "Volkov", "Orlov", "Belov"], []),
    "UA": (["Oleksandr", "Mykola", "Andriy", "Bohdan", "Taras", "Vitaliy", "Yaroslav", "Denys", "Serhiy", "Oleh", "Artem", "Roman"],
           ["Kovalenko", "Bondarenko", "Tkachenko", "Shevchuk", "Melnyk", "Kravchenko", "Boyko", "Moroz", "Lysenko", "Savchenko", "Marchenko", "Rudenko"], []),
    "CA": (["Liam", "Noah", "Owen", "Ethan", "Nathan", "Logan", "Cole", "Mathieu"],
           ["Tremblay", "Gagnon", "Roy", "Côté", "Bouchard", "MacDonald", "Campbell", "Stewart"], []),
}

# estrangeiros mais comuns em cada liga
FOREIGN_POOL = {
    "IT": ["BR", "AR", "FR", "NL", "DE", "NG", "SN", "ES", "UY", "PT", "MA"],
    "EN": ["BR", "FR", "ES", "PT", "NL", "DE", "NG", "GH", "CI", "AR", "JP", "KR", "UY", "CO"],
    "ES": ["BR", "AR", "FR", "PT", "UY", "CO", "MA", "NL", "SN", "JP"],
    "FR": ["SN", "CI", "MA", "BR", "PT", "NG", "GH", "ES", "AR", "JP", "KR"],
    "US": ["AR", "BR", "CO", "MX", "UY", "CA", "ES", "EN", "GH", "JP", "KR", "FR"],
    "BR": ["AR", "UY", "CO", "PT"],
    "AR": ["UY", "CO", "BR"],
    "PT": ["BR", "ES", "AR", "CI", "SN", "UY", "CO", "NG"],
    "UY": ["AR", "BR", "CO"],
    "PE": ["AR", "UY", "CO", "BR"],
    "CL": ["AR", "UY", "CO", "BR"],
    "EC": ["CO", "AR", "UY", "BR"],
    "SA": ["BR", "PT", "FR", "ES", "NG", "SN", "MA", "AR", "CO", "NL", "EN"],
    "NL": ["BR", "MA", "DE", "EN", "NG", "GH", "PT", "JP", "SN", "UY"],
    "RU": ["BR", "CO", "UY", "AR", "NG", "SN", "PT"],
    "UA": ["BR", "CO", "NG", "GH", "PT"],
}

# aparência: pesos por tom de pele (8 tons de face_gen.SKIN_TONES, claro → escuro)
SKIN_W = {
    "BR": [6, 12, 15, 16, 15, 13, 13, 10], "AR": [22, 30, 26, 12, 5, 3, 1, 1], "UY": [22, 30, 24, 12, 6, 3, 2, 1],
    "IT": [16, 30, 26, 12, 6, 4, 4, 2], "ES": [15, 30, 27, 13, 6, 4, 3, 2], "PT": [12, 26, 26, 16, 8, 5, 4, 3],
    "EN": [26, 20, 10, 8, 8, 9, 11, 8], "FR": [18, 15, 10, 9, 10, 12, 14, 12], "US": [18, 18, 14, 13, 10, 9, 10, 8],
    "NL": [30, 24, 10, 6, 8, 8, 8, 6], "DE": [34, 28, 12, 6, 6, 5, 5, 4], "CA": [24, 22, 14, 10, 8, 8, 8, 6],
    "NG": [0, 0, 0, 1, 6, 20, 38, 35], "SN": [0, 0, 0, 1, 5, 18, 36, 40], "CI": [0, 0, 0, 1, 6, 22, 38, 33],
    "GH": [0, 0, 0, 1, 6, 22, 38, 33], "MA": [4, 16, 32, 28, 14, 4, 1, 1], "CO": [8, 18, 26, 22, 12, 7, 4, 3],
    "MX": [6, 16, 30, 28, 12, 5, 2, 1], "JP": [20, 50, 26, 4, 0, 0, 0, 0], "KR": [22, 50, 24, 4, 0, 0, 0, 0],
    "PE": [4, 12, 26, 30, 18, 6, 3, 1], "CL": [10, 24, 30, 22, 10, 3, 1, 0], "EC": [4, 10, 18, 22, 16, 12, 10, 8],
    "SA": [2, 8, 26, 32, 20, 8, 3, 1], "RU": [42, 34, 12, 4, 3, 2, 2, 1], "UA": [44, 34, 12, 4, 2, 2, 1, 1],
}

# ---------------------------------------------------------------- futsal pelo mundo
FUTSAL_LEAGUES = [
    {"id": "fbr", "name": "Liga Nacional de Futsal (BR)", "country": "BR", "foreign": 0.05, "teams": [
        ("Carlos Barbosa Futsal", "CBA", "#111827", "#f8fafc", 0.84), ("Sorocaba Magnus", "MAG", "#1e3a8a", "#facc15", 0.85),
        ("Timão Futsal", "TFS", "#f8fafc", "#111827", 0.80), ("Joinville Tricolor", "JEC", "#dc2626", "#111827", 0.80),
        ("Pato Branco Futsal", "PAT", "#16a34a", "#f8fafc", 0.79), ("Cascavel Serpente", "CAS", "#facc15", "#111827", 0.79),
        ("Erechim Atlântico", "ATL", "#1d4ed8", "#f8fafc", 0.78), ("Jaraguá Futsal", "JAR", "#1e3a8a", "#dc2626", 0.77),
        ("Praia Futsal MG", "PRA", "#facc15", "#1e3a8a", 0.77), ("Beltrão Marreco", "MRC", "#dc2626", "#f8fafc", 0.76),
        ("Foz Cataratas", "FOZ", "#0ea5e9", "#f8fafc", 0.75), ("Minas Futsal", "MIN", "#f8fafc", "#1e3a8a", 0.75),
        ("São José Futsal", "SJF", "#1e40af", "#f8fafc", 0.74), ("Campo Mourão Futsal", "CMF", "#15803d", "#facc15", 0.73),
        ("Tubarão Futsal", "TUB", "#7c3aed", "#f8fafc", 0.72), ("Dourados Futsal", "DOU", "#facc15", "#15803d", 0.72),
    ]},
    {"id": "fes", "name": "Liga de Futsal (ESP)", "country": "ES", "foreign": 0.35, "teams": [
        ("Barcelona Futsal", "BFS", "#1e3a8a", "#a50044", 0.86), ("Madrid Interiores", "INT", "#1d4ed8", "#f8fafc", 0.84),
        ("Palma Futsal", "PAL", "#f97316", "#111827", 0.84), ("Murcia Futsal", "MUR", "#dc2626", "#f8fafc", 0.82),
        ("Cartagena Futsal", "CAR", "#111827", "#facc15", 0.82), ("Valdepeñas Futsal", "VAL", "#7f1d1d", "#f8fafc", 0.77),
        ("Jaén Futsal", "JAE", "#facc15", "#111827", 0.76), ("Pamplona Futsal", "OSA", "#b91c1c", "#1e3a8a", 0.75),
        ("Sevilla Verdiblancos FS", "BTF", "#16a34a", "#f8fafc", 0.74), ("Santa Coloma Futsal", "STC", "#dc2626", "#111827", 0.75),
        ("Peñíscola Futsal", "PEN", "#0ea5e9", "#f8fafc", 0.74), ("Burela Futsal", "BUR", "#1d4ed8", "#f8fafc", 0.72),
        ("Manzanares Futsal", "MZN", "#16a34a", "#111827", 0.72), ("Córdoba Futsal", "COR", "#15803d", "#f8fafc", 0.72),
        ("Tudela Futsal", "RIB", "#facc15", "#1e3a8a", 0.71), ("La Nucía Futsal", "NUC", "#1e3a8a", "#f8fafc", 0.71),
    ]},
    {"id": "fpt", "name": "Liga de Futsal (POR)", "country": "PT", "foreign": 0.3, "teams": [
        ("Lisboa Leões Futsal", "SCP", "#15803d", "#f8fafc", 0.86), ("Lisboa Águias Futsal", "SLB", "#dc2626", "#f8fafc", 0.85),
        ("Braga Arsenalistas", "SCB", "#dc2626", "#f8fafc", 0.79), ("Porto Salvo Leões", "LPS", "#16a34a", "#facc15", 0.74),
        ("Ponte de Sor Eléctrico", "ELE", "#facc15", "#1e3a8a", 0.73), ("Lombos Futsal", "QDL", "#1d4ed8", "#facc15", 0.72),
        ("Fundão Futsal", "FUN", "#111827", "#facc15", 0.72), ("Burinhosa Futsal", "BRN", "#1e3a8a", "#f8fafc", 0.71),
        ("Modicus Futsal", "MOD", "#7c3aed", "#f8fafc", 0.71), ("Torres Vedras Futsal", "TOR", "#1d4ed8", "#f8fafc", 0.70),
        ("Caxinas Futsal", "CAX", "#0ea5e9", "#f8fafc", 0.70), ("Vila do Conde Futsal", "RVA", "#15803d", "#f8fafc", 0.69),
    ]},
    {"id": "fit", "name": "Serie A Futsal (ITA)", "country": "IT", "foreign": 0.55, "teams": [
        ("Roma Active Futsal", "ACT", "#111827", "#facc15", 0.80), ("Eboli Futsal", "EBO", "#1e3a8a", "#facc15", 0.79),
        ("Sala Consilina Futsal", "SCO", "#dc2626", "#f8fafc", 0.75), ("Pomezia Futsal", "POM", "#1d4ed8", "#f8fafc", 0.74),
        ("Capurso Futsal", "CAP", "#f8fafc", "#dc2626", 0.75), ("Torino L84", "L84", "#111827", "#f8fafc", 0.77),
        ("Napoli Futsal", "NAP", "#38bdf8", "#f8fafc", 0.77), ("Benevento Futsal", "BEN", "#facc15", "#dc2626", 0.73),
        ("Pescara Abate", "ABA", "#0ea5e9", "#f8fafc", 0.78), ("Giovinazzo Futsal", "GIO", "#16a34a", "#f8fafc", 0.72),
        ("Padova Petrarca", "PET", "#dc2626", "#f8fafc", 0.73), ("Treviso Futsal", "TRE", "#facc15", "#1d4ed8", 0.73),
        ("Catania Meta Futsal", "CAT", "#ea580c", "#1e3a8a", 0.79), ("Roma 1927 Futsal", "R27", "#7f1d1d", "#f59e0b", 0.74),
    ]},
    {"id": "far", "name": "Primera A Futsal (ARG)", "country": "AR", "foreign": 0.05, "teams": [
        ("Xeneize Futsal", "XEN", "#1e3a8a", "#facc15", 0.79), ("Ciclón Futsal", "CIC", "#1e3a8a", "#b91c1c", 0.80),
        ("Pinocho Futsal", "PIN", "#16a34a", "#f8fafc", 0.77), ("Kimberley Futsal", "KIM", "#111827", "#f8fafc", 0.74),
        ("Ferro Oeste Futsal", "FER", "#15803d", "#f8fafc", 0.74), ("Academia Futsal", "ACA", "#7dd3fc", "#f8fafc", 0.75),
        ("Millonario Futsal", "MIL", "#f8fafc", "#dc2626", 0.76), ("Rojo Futsal", "ROJ", "#dc2626", "#111827", 0.74),
        ("Hebraica Futsal", "HEB", "#1d4ed8", "#f8fafc", 0.72), ("Newbery Futsal", "JNB", "#facc15", "#111827", 0.71),
        ("Glorias Futsal", "GLO", "#1e3a8a", "#f8fafc", 0.70), ("Villa La Ñata Futsal", "VLN", "#dc2626", "#facc15", 0.70),
    ]},
]
FUTSAL_POSITIONS = ["GK", "GK", "DF", "DF", "DF", "MF", "MF", "MF", "MF", "MF", "FW", "FW", "FW", "FW"]
POSITIONS = ["GK", "GK", "DF", "DF", "DF", "DF", "DF", "DF", "MF", "MF", "MF", "MF", "MF", "MF", "FW", "FW", "FW", "FW"]
NUMBERS = {"GK": [1, 12, 23], "DF": [2, 3, 4, 5, 6, 13, 14, 15, 22, 24], "MF": [8, 10, 16, 17, 18, 20, 21, 25, 26],
           "FW": [7, 9, 11, 19, 27, 29, 30]}


def face_for(r: random.Random, nat: str) -> list[int]:
    skin = r.choices(range(8), weights=SKIN_W.get(nat, [1] * 8))[0]
    nhc = len(face_gen.HAIR_COLORS)
    if skin >= 4 or nat in ("JP", "KR", "MX", "MA"):
        hc = r.choices(range(nhc), weights=[45, 35, 12, 4, 1, 0, 0, 1, 2])[0]
    elif nat in ("DE", "NL", "EN", "US", "CA", "FR"):
        hc = r.choices(range(nhc), weights=[10, 16, 18, 18, 14, 12, 6, 4, 2])[0]
    else:
        hc = r.choices(range(nhc), weights=[24, 28, 20, 12, 7, 3, 2, 2, 2])[0]
    styles = face_gen.HAIR_STYLES   # buzz short fade curly afro long mohawk bald dreads fringe bun
    if skin >= 5:
        sw = [18, 8, 22, 12, 9, 2, 3, 10, 12, 1, 3]
    elif skin >= 3:
        sw = [14, 18, 20, 16, 4, 6, 4, 6, 6, 4, 2]
    else:
        sw = [10, 30, 18, 6, 0, 10, 3, 5, 1, 14, 3]
    style = r.choices(range(len(styles)), weights=sw)[0]
    beard = r.choices(range(len(face_gen.BEARDS)), weights=[30, 0, 26, 12, 14, 8, 10])[0]
    if nat in ("JP", "KR"):
        beard = 0 if r.random() < 0.85 else 2
    eye = r.choices(range(len(face_gen.EYE_COLORS)), weights=[30, 30, 6, 8 if skin < 3 else 1, 6, 20])[0]
    build = r.choice([0, 1, 1, 2])
    return [skin, hc, style, beard, eye, build]


# combinações que coincidem com jogadores conhecidos: nunca usar
FAMOUS = {
    "Sergio Ramos", "Lautaro Martínez", "Julián Álvarez", "Emiliano Martínez", "Leandro Paredes", "Nicolás González",
    "Rodrigo Hernández", "Raúl García", "Javier Hernández", "Raúl Jiménez", "Thiago Silva", "Everton Ribeiro",
    "Diego Costa", "João Gomes", "André Silva", "Gabriel Barbosa", "Marcos Rocha", "Douglas Santos", "Matheus Pereira",
    "Callum Wilson", "Ben White", "Harry Wilson", "Moussa Sissoko", "Benjamin Mendy", "Miles Robinson", "Sean Johnson",
    "Gonzalo Martínez", "Lucas Hernández", "Ángel Correa", "Federico Fernández", "Lucas Paquetá", "Danilo Pereira",
    "Rodrigo Moreno", "Pablo Marín", "Jorge Molina", "Sergio Gómez", "James Ward", "Alan Ruiz", "Lucas Torres",
    "Salem Al-Dosari", "Carlos Gruezo", "Bryan Angulo", "Jordy Caicedo", "Anthony Valencia", "Romario Caicedo",
    "Luis Ramos", "Alexander Callens", "Matías Fernández", "Felipe Mora", "Diego Valdés", "Aleksandr Kokorin",
}


def player_name(r: random.Random, nat: str, used: set) -> str:
    first, last, nick = NAMES.get(nat, NAMES["EN"])
    for _ in range(40):
        if nick and r.random() < 0.28:
            n = r.choice(nick)
        elif nat in ("BR",) and r.random() < 0.35:
            n = f"{r.choice(first)} {r.choice(last)[0]}."
        else:
            n = f"{r.choice(first)[0]}. {r.choice(last)}" if r.random() < 0.15 else f"{r.choice(first)} {r.choice(last)}"
        if n not in used and n not in FAMOUS:
            used.add(n)
            return n
    return n


def attrs(r: random.Random, rating: float, pos: str) -> list[float]:
    def g(bias: float = 0.0, s: float = 0.07) -> float:
        return round(min(0.99, max(0.3, rating + bias + r.gauss(0, s))), 2)
    if pos == "GK":
        return [g(-0.12), g(-0.1), g(-0.3), g(-0.05), g(-0.05), g(0.08)]
    if pos == "DF":
        return [g(-0.02), g(-0.02), g(-0.1), g(-0.02), g(-0.05), g(0.08)]
    if pos == "MF":
        return [g(0.0), g(0.0), g(0.0), g(0.06), g(0.05), g(-0.02)]
    return [g(0.05), g(0.05), g(0.08), g(-0.02), g(0.04), g(-0.12)]


# habilidades especiais (afetam a simulação; ver SimPlayer.Trait)
TRAITS = {
    "GK": ["Goleiro Seguro", "Reflexo", "Líder"],
    "DF": ["Muralha", "Carrinho", "Cabeceador", "Líder", "Resistência", "Velocista"],
    "MF": ["Maestro", "Resistência", "Longa Distância", "Chute Colocado", "Driblador", "Líder"],
    "FW": ["Finalizador", "Velocista", "Driblador", "Cabeceador", "Chute Colocado", "Longa Distância"],
}


def traits_for(r: random.Random, pos: str, a: list[float], star: float) -> list[str]:
    n = 1 + (1 if r.random() < 0.35 + star * 4 else 0) + (1 if star > 0 and r.random() < 0.3 else 0)
    pool = list(TRAITS[pos])
    # quem tem o atributo alto puxa a habilidade correspondente
    if a[0] > 0.8 and "Velocista" in pool:
        pool += ["Velocista"] * 2
    if a[2] > 0.8 and "Finalizador" in pool:
        pool += ["Finalizador"] * 2
    if a[3] > 0.8 and "Maestro" in pool:
        pool += ["Maestro"] * 2
    if a[4] > 0.8 and "Driblador" in pool:
        pool += ["Driblador"] * 2
    out: list[str] = []
    while len(out) < n and pool:
        t = r.choice(pool)
        pool = [x for x in pool if x != t]
        out.append(t)
    return out


def overall(pos: str, a: list[float]) -> int:
    w = {"GK": [0.05, 0.05, 0.0, 0.15, 0.15, 0.6], "DF": [0.15, 0.1, 0.05, 0.15, 0.1, 0.45],
         "MF": [0.12, 0.1, 0.15, 0.3, 0.25, 0.08], "FW": [0.2, 0.15, 0.35, 0.1, 0.18, 0.02]}[pos]
    return int(round(30 + 63 * sum(x * y for x, y in zip(a, w))))


STADIUM_WORDS = ["Arena", "Estádio", "Estádio", "Parque", "Arena", "Coliseu"]


def nearest_face(lib: list, f: list[int]) -> int:
    best, best_s = 0, 1e9
    for i, p in enumerate(lib):
        s = abs(p.skin - f[0]) * 3 + (0 if p.hair_color == f[1] else 2) + (0 if face_gen.HAIR_STYLES[f[2]] == p.hair_style else 2.5) \
            + (0 if face_gen.BEARDS[f[3]] == p.beard else 1)
        if s < best_s:
            best, best_s = i, s
    return best


def main() -> None:
    lib_n = 360
    lib = face_gen.write_library(lib_n, 2026, ROOT / "assets" / "faces")
    out = {"season": SEASON, "note": "Nomes alterados (sem licença). Edite à vontade.", "leagues": []}
    total = 0
    all_leagues = [(L, "football") for L in LEAGUES] + [(L, "futsal") for L in FUTSAL_LEAGUES]
    for L, sport in all_leagues:
        positions = FUTSAL_POSITIONS if sport == "futsal" else POSITIONS
        lg = {"id": L["id"], "name": L["name"], "country": L["country"], "sport": sport, "teams": []}
        for ti, (name, short, c1, c2, rating) in enumerate(L["teams"]):
            r = random.Random(hash((L["id"], ti, 2026)) & 0xFFFFFFFF)
            r.seed(f"{L['id']}-{ti}-{name}")
            used: set = set()
            nums: dict = {k: list(v) for k, v in NUMBERS.items()}
            roster = []
            for pi, pos in enumerate(positions):
                nat = L["country"] if r.random() > L["foreign"] else r.choice(FOREIGN_POOL[L["country"]])
                f = face_for(r, nat)
                num = nums[pos].pop(0) if nums[pos] else 30 + pi
                star = 0.05 if (pi in (8, 9, 14, 15) if sport == "football" else pi in (5, 6, 10)) else 0.0
                a = attrs(r, rating + star, pos)
                roster.append({"n": player_name(r, nat, used), "num": num, "pos": pos, "nat": nat,
                               "f": f, "fid": nearest_face(lib, f), "a": a, "ovr": overall(pos, a),
                               "t": traits_for(r, pos, a, star)})
                total += 1
            city = name.split()[0]
            tier = 3 if rating >= 0.8 else (2 if rating >= 0.72 else 1)
            stadium = {"name": f"{r.choice(STADIUM_WORDS)} {city}" if sport == "football" else f"Ginásio {city}",
                       "tier": tier, "cap": int(r.uniform(*{3: (55, 82), 2: (28, 50), 1: (8, 24)}[tier])) * 1000}
            lg["teams"].append({"name": name, "short": short, "c1": c1, "c2": c2, "rating": rating,
                                "stadium": stadium, "roster": roster})
        out["leagues"].append(lg)
    p = ROOT / "data" / "leagues.json"
    p.parent.mkdir(exist_ok=True)
    p.write_text(json.dumps(out, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"{len(out['leagues'])} ligas, {sum(len(l['teams']) for l in out['leagues'])} times, {total} jogadores -> {p} ({p.stat().st_size // 1024} KB)")
    print(f"{lib_n} rostos 2D -> assets/faces")


if __name__ == "__main__":
    main()
