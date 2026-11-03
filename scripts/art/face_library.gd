## Biblioteca de rostos: carrega os SVGs de assets/faces e rasteriza na
## resolução do preset de qualidade (128px no Baixo, 1024px no Ultra).
class_name FaceLibrary
extends RefCounted

const COUNT := 48
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


static func clear_cache() -> void:
	_cache.clear()
