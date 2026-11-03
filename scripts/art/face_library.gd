## Biblioteca de rostos: carrega os SVGs de assets/faces e rasteriza na
## resolução do preset de qualidade (128px no Baixo, 1024px no Ultra).
class_name FaceLibrary
extends RefCounted

const COUNT := 360

# mesmas paletas de tools/face_gen.py
const SKIN_TONES := ["#f6d7c3", "#efc3a4", "#e0ac86", "#c98e62", "#a8714a", "#8a5634", "#6b3f26", "#4e2c1a"]
const HAIR_COLORS := ["#1a1210", "#2b1d16", "#4a2f1f", "#6e4a2c", "#a0703f", "#d9b26a", "#e8d3a0", "#8c2f1b", "#b9b9b9"]
const HAIR_STYLES := ["buzz", "short", "fade", "curly", "afro", "long", "mohawk", "bald", "dreads", "fringe", "bun"]
const BEARDS := ["none", "none", "stubble", "goatee", "full", "mustache", "chin"]
const DIR := "res://assets/faces/"

static var _cache: Dictionary = {}
static var _meta: Array = []


static func texture(face_id: int, size: int = -1) -> Texture2D:
	if size <= 0:
		size = int(Settings.detail.get("face_px", 512))
	var id := posmod(face_id, COUNT)
	var key := "%d@%d" % [id, size]
	if _cache.has(key):
		return _cache[key]
	var path := DIR + "face_%03d.svg" % id
	var tex: Texture2D = null
	var f := FileAccess.open(path, FileAccess.READ)
	if f != null:
		var img := Image.new()
		if img.load_svg_from_string(f.get_as_text(), float(size) / 512.0) == OK:
			img.generate_mipmaps()
			tex = ImageTexture.create_from_image(img)
	if tex == null and ResourceLoader.exists(path):
		tex = load(path)   # fallback: textura importada pelo editor
	_cache[key] = tex
	return tex


## Metadados (cores de pele/cabelo) para desenhar a cabeça do jogador em campo.
static func meta(face_id: int) -> Dictionary:
	if _meta.is_empty():
		var f := FileAccess.open(DIR + "faces.json", FileAccess.READ)
		if f != null:
			var parsed: Variant = JSON.parse_string(f.get_as_text())
			if parsed is Array:
				_meta = parsed
	if _meta.is_empty():
		return {"skin_hex": "#c98e62", "skin_dark_hex": "#9a6440", "hair_hex": "#1a1210", "hair_style": "short"}
	return _meta[posmod(face_id, _meta.size())]


## Aparência do atleta: dados próprios do elenco (se houver) ou a face da biblioteca.
static func player_meta(p: SimPlayer) -> Dictionary:
	if p.face.size() >= 6:
		var f := p.face
		return {
			"skin_hex": SKIN_TONES[clampi(int(f[0]), 0, SKIN_TONES.size() - 1)],
			"hair_hex": HAIR_COLORS[clampi(int(f[1]), 0, HAIR_COLORS.size() - 1)],
			"hair_style": HAIR_STYLES[clampi(int(f[2]), 0, HAIR_STYLES.size() - 1)],
			"beard": BEARDS[clampi(int(f[3]), 0, BEARDS.size() - 1)],
			"eye_color": int(f[4]),
			"build": int(f[5]),
		}
	return meta(p.face_id)


static func clear_cache() -> void:
	_cache.clear()
