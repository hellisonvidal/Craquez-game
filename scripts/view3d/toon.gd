## Fábrica de materiais "desenhados" (cel-shading + contorno), com cache.
class_name Toon
extends RefCounted

static var _toon: Shader = preload("res://shaders/toon.gdshader")
static var _outline_shader: Shader = preload("res://shaders/outline.gdshader")
static var _cache: Dictionary = {}
static var _outline: ShaderMaterial


static func outline_mat() -> ShaderMaterial:
	if _outline == null:
		_outline = ShaderMaterial.new()
		_outline.shader = _outline_shader
	return _outline


## Material cel-shading para uma cor. `outline` adiciona o contorno preto
## (desligado no preset Baixo para economizar desenho).
static func mat(color: Color, outline: bool = true, rim: float = 0.22, spec: float = 0.0) -> ShaderMaterial:
	var use_outline: bool = outline and bool(Settings.detail.get("outline", true))
	var key := "%s|%s|%.2f|%.2f" % [color.to_html(), use_outline, rim, spec]
	if _cache.has(key):
		return _cache[key]
	var m := ShaderMaterial.new()
	m.shader = _toon
	m.set_shader_parameter("albedo", color)
	m.set_shader_parameter("rim", rim)
	m.set_shader_parameter("spec", spec)
	if use_outline:
		m.next_pass = outline_mat()
	_cache[key] = m
	return m


static func clear_cache() -> void:
	_cache.clear()
