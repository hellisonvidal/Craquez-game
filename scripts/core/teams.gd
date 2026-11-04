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
static var _keys: Array[String] = []     # "liga/SIGLA" de cada time (chave do option file)
static var override_count := 0
static var option_dir := ""
static var _crests: Dictionary = {}     # chave -> Texture2D (escudo do usuário)

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
				"rating": float(t["rating"]), "roster": t.get("roster", []), "stadium": t.get("stadium", {}),
				"kits": _parse_kits(t),
				"league": String(lg["name"]), "country": String(lg.get("country", "")),
			})
		for t: Dictionary in lg.get("teams", []):
			_keys.append("%s/%s" % [lg["id"], t["short"]])
		_leagues.append({"id": String(lg["id"]), "name": String(lg["name"]), "sport": String(lg.get("sport", "football")), "first": first, "count": _flat.size() - first})
	_post_load()


## (chamado ao fim do carregamento)
static func _post_load() -> void:
	_apply_option_file()


const PATTERN_IDS := {"liso": 0, "listras": 1, "aros": 2, "faixa": 3, "metades": 4, "riscas": 5, "faixa_peito": 6}
const KIT_NAMES := ["Principal", "Reserva", "Terceiro"]


## Uniformes: [{c1, c2, shorts, socks, pattern:int}] x3 (principal, reserva, terceiro).
static func _parse_kits(t: Dictionary) -> Array:
	var out: Array = []
	for k: Dictionary in t.get("kits", []):
		if k.get("c1") is Color:
			out.append(k)
			continue
		out.append({
			"c1": _col(k.get("c1", "#ffffff")), "c2": _col(k.get("c2", "#000000")),
			"shorts": _col(k.get("shorts", k.get("c2", "#000000"))), "socks": _col(k.get("socks", k.get("c1", "#ffffff"))),
			"pattern": int(PATTERN_IDS.get(String(k.get("pattern", "liso")), 0)),
		})
	if out.is_empty():
		var c1 := _col(t.get("c1", "#ffffff"))
		var c2 := _col(t.get("c2", "#000000"))
		out.append({"c1": c1, "c2": c2, "shorts": c2, "socks": c1, "pattern": 0})
		out.append({"c1": c2, "c2": c1, "shorts": c2, "socks": c2, "pattern": 0})
		out.append({"c1": Color("#111827"), "c2": c1, "shorts": Color("#111827"), "socks": Color("#111827"), "pattern": 0})
	return out


## Aceita Color ou texto "#rrggbb".
static func _col(v: Variant) -> Color:
	if v is Color:
		return v
	return Color(str(v))


## Uniformes da partida: casa usa o pedido (padrão principal); visitante usa o pedido
## ou, no automático (-1), o primeiro que não se confunda com o da casa.
static func pick_kits(ta: Dictionary, tb: Dictionary, ka: int = 0, kb: int = -1) -> Array:
	var kits_a: Array = ta["kits"] if ta.has("kits") else _parse_kits(ta)
	var kits_b: Array = tb["kits"] if tb.has("kits") else _parse_kits(tb)
	var a: Dictionary = kits_a[clampi(ka, 0, kits_a.size() - 1)]
	if kb >= 0:
		return [a, kits_b[clampi(kb, 0, kits_b.size() - 1)]]
	for i: int in [1, 0, 2]:
		var b: Dictionary = kits_b[i]
		if _kit_distance(a, b) > 0.28:
			return [a, b]
	return [a, kits_b[2]]


static func _kit_distance(a: Dictionary, b: Dictionary) -> float:
	var x: Color = a["c1"]
	var y: Color = b["c1"]
	return Vector3(x.r - y.r, x.g - y.g, x.b - y.b).length() + absf(x.get_luminance() - y.get_luminance()) * 0.5


# ------------------------------------------------------------------ option file
## Pastas procuradas (a primeira que existir): ao lado do executável e a de dados do usuário.
static func _option_dirs() -> Array[String]:
	var dirs: Array[String] = []
	if not OS.has_feature("editor"):
		dirs.append(OS.get_executable_path().get_base_dir().path_join("option_file"))
	dirs.append(ProjectSettings.globalize_path("user://option_file"))
	return dirs


## Aplica nomes, uniformes, estádio, jogadores e escudos fornecidos pelo usuário.
static func _apply_option_file() -> void:
	override_count = 0
	_crests.clear()
	for dir in _option_dirs():
		var path := dir.path_join("option_file.json")
		if not FileAccess.file_exists(path):
			continue
		option_dir = dir
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
		if not (parsed is Dictionary):
			return
		var teams: Dictionary = (parsed as Dictionary).get("teams", {})
		for i in _flat.size():
			var key := _keys[i]
			if not teams.has(key):
				continue
			var o: Dictionary = teams[key]
			var t := _flat[i]
			for f in ["name", "short"]:
				if o.has(f):
					t[f] = String(o[f])
			if o.has("kits"):
				t["kits"] = _parse_kits({"kits": o["kits"], "c1": "#ffffff", "c2": "#000000"})
				t["c1"] = t["kits"][0]["c1"]
				t["c2"] = t["kits"][0]["c2"]
			if o.has("stadium"):
				var st: Dictionary = t.get("stadium", {}).duplicate()
				st.merge(o["stadium"], true)
				t["stadium"] = st
			if o.has("players"):
				var roster: Array = (t.get("roster", []) as Array).duplicate(true)
				for pl: Dictionary in o["players"]:
					var idx := int(pl.get("index", -1))
					if idx < 0 or idx >= roster.size():
						continue
					var e: Dictionary = roster[idx]
					for f in ["n", "num", "pos", "nat", "fid", "t"]:
						if pl.has(f):
							e[f] = pl[f]
				t["roster"] = roster
			if o.has("crest"):
				var img := Image.new()
				if img.load(dir.path_join(String(o["crest"]))) == OK:
					_crests[i] = ImageTexture.create_from_image(img)
			override_count += 1
		return


static func crest_texture(i: int) -> Texture2D:
	_ensure()
	return _crests.get(posmod(i, maxi(1, _flat.size())))


## Grava um modelo com todos os times (valores atuais) para o usuário editar.
static func export_option_template() -> String:
	_ensure()
	var dir := ProjectSettings.globalize_path("user://option_file")
	DirAccess.make_dir_recursive_absolute(dir.path_join("crests"))
	var teams := {}
	for i in _flat.size():
		var t := _flat[i]
		var kits: Array = []
		for k: Dictionary in t.get("kits", []):
			var pat := "liso"
			for pn in PATTERN_IDS:
				if int(PATTERN_IDS[pn]) == int(k["pattern"]):
					pat = pn
			kits.append({"c1": "#" + (k["c1"] as Color).to_html(false), "c2": "#" + (k["c2"] as Color).to_html(false),
				"shorts": "#" + (k["shorts"] as Color).to_html(false), "socks": "#" + (k["socks"] as Color).to_html(false), "pattern": pat})
		var players: Array = []
		var roster: Array = t.get("roster", [])
		for pi in roster.size():
			var e: Dictionary = roster[pi]
			players.append({"index": pi, "n": e.get("n", ""), "num": e.get("num", 0), "pos": e.get("pos", "")})
		teams[_keys[i]] = {"name": t["name"], "short": t["short"], "kits": kits,
			"stadium": {"name": t.get("stadium", {}).get("name", ""), "cap": t.get("stadium", {}).get("cap", 0)},
			"crest": "crests/%s.png" % String(_keys[i]).replace("/", "_"), "players": players}
	var doc := {"formato": "craquez-option-file-1",
		"ajuda": "Edite os valores e salve como option_file.json nesta pasta. Escudos: PNG quadrado em crests/. Apague os times que não quiser alterar.",
		"teams": teams}
	var path := dir.path_join("option_file_modelo.json")
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify(doc, "\t"))
	return path


static func reload() -> void:
	_loaded = false
	_leagues.clear()
	_flat.clear()
	_keys.clear()
	_ensure()


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
