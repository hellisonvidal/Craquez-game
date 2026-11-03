## Times fictícios (nenhuma marca/licença real).
class_name Teams
extends RefCounted

const LIST: Array[Dictionary] = [
	{"name": "Trovão FC", "short": "TRV", "c1": Color("#d62828"), "c2": Color("#ffffff"), "rating": 0.78},
	{"name": "Atlético Maré", "short": "MAR", "c1": Color("#1d4ed8"), "c2": Color("#ffffff"), "rating": 0.76},
	{"name": "Canarinhos EC", "short": "CAN", "c1": Color("#facc15"), "c2": Color("#14532d"), "rating": 0.8},
	{"name": "Verdão da Vila", "short": "VIL", "c1": Color("#16a34a"), "c2": Color("#ffffff"), "rating": 0.74},
	{"name": "Pantera Negra", "short": "PAN", "c1": Color("#111827"), "c2": Color("#f97316"), "rating": 0.77},
	{"name": "Real Aurora", "short": "AUR", "c1": Color("#f8fafc"), "c2": Color("#1e3a8a"), "rating": 0.79},
	{"name": "Roxo Imperial", "short": "IMP", "c1": Color("#7c3aed"), "c2": Color("#fde68a"), "rating": 0.72},
	{"name": "Laranja Mecânica FC", "short": "LMC", "c1": Color("#ea580c"), "c2": Color("#111827"), "rating": 0.75},
]

const NAMES: Array[String] = [
	"Zé Raio", "Dudu", "Tico", "Bira", "Neném", "Pepeu", "Juninho", "Careca", "Gaúcho", "Baixinho",
	"Toninho", "Kaká B.", "Rafinha", "Leléu", "Mãozinha", "Fumaça", "Pirulito", "Danilo", "Serginho",
	"Marquinhos", "Tatu", "Boca", "Feijão", "Cacau", "Didi", "Nonato", "Paçoca", "Biel", "Formiga",
	"Thiaguinho", "Pelezinho", "Robertão", "Alemão", "Cuca", "Vavá", "Zinho", "Bebeto C.", "Tuta",
]


static func get_team(i: int) -> Dictionary:
	return LIST[posmod(i, LIST.size())]
