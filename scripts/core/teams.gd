## Times e ligas. Carrega data/leagues.json (8 ligas da temporada 2026/27,
## nomes alterados — sem licença) e cai para a lista interna se faltar.
## Índice de time = índice global (todas as ligas em sequência): é o que vai
## na configuração da partida (inclusive online).
class_name Teams
extends RefCounted

const DB_PATH := "res://data/leagues.json"

static var _loaded := false
static var _leagues: Array = []        # [{id, name, country, first, count}]
static var _flat: Array[Dictionary] = []

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


static func _ensure() -> void:
	if _loaded:
		return
	_loaded = true
	var f := FileAccess.open(DB_PATH, FileAccess.READ)
	if f == null:
		return
	var parsed: Variant = JSON.parse_string(f.get_as_text())
	if not (parsed is Dictionary):
		return
	for lg: Dictionary in parsed.get("leagues", []):
		var first := _flat.size()
		for t: Dictionary in lg.get("teams", []):
			_flat.append({
				"name": String(t["name"]), "short": String(t["short"]),
				"c1": Color(String(t["c1"])), "c2": Color(String(t["c2"])),
				"rating": float(t["rating"]), "roster": t.get("roster", []),
				"league": String(lg["name"]), "country": String(lg.get("country", "")),
			})
		_leagues.append({"id": String(lg["id"]), "name": String(lg["name"]), "sport": String(lg.get("sport", "football")), "first": first, "count": _flat.size() - first})


static func count() -> int:
	_ensure()
	return _flat.size() if not _flat.is_empty() else LIST.size()


static func get_team(i: int) -> Dictionary:
	_ensure()
	if _flat.is_empty():
		return LIST[posmod(i, LIST.size())]
	return _flat[posmod(i, _flat.size())]


## Ligas: [{id, name, first, count}]. Vazio se o banco não carregou.
static func leagues() -> Array:
	_ensure()
	return _leagues


static func league_of(i: int) -> int:
	_ensure()
	for li in _leagues.size():
		var lg: Dictionary = _leagues[li]
		if i >= int(lg["first"]) and i < int(lg["first"]) + int(lg["count"]):
			return li
	return 0
