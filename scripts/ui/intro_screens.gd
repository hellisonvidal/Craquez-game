## Telas de abertura: apresentação "Feito com Claude" e o aviso de que o jogo
## foi desenvolvido inteiramente com IA (e por isso é gratuito).
## Qualquer tecla, botão ou toque avança.
class_name IntroScreens
extends Control

signal finished

const PAGES := [
	{"bg": Color("#f4efe6"), "dur": 3.2},
	{"bg": Color("#07111f"), "dur": 5.5},
]

var page: int = -1
var _t: float = 0.0
var _fade: float = 0.0          # 0..1 entrada, depois saída
var _leaving := false
var _layer: Control
var _bg: ColorRect


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	_fit()
	get_viewport().size_changed.connect(_fit)
	_bg = ColorRect.new()
	_bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_bg)
	_layer = Control.new()
	_layer.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_layer)
	show_page(0)


func _fit() -> void:
	position = Vector2.ZERO
	size = get_viewport().get_visible_rect().size


func show_page(i: int) -> void:
	page = i
	_t = 0.0
	_leaving = false
	for c in _layer.get_children():
		c.queue_free()
	if i >= PAGES.size():
		finished.emit()
		return
	_bg.color = PAGES[i]["bg"]
	var center := CenterContainer.new()
	center.set_anchors_preset(Control.PRESET_FULL_RECT)
	_layer.add_child(center)
	var vb := VBoxContainer.new()
	vb.alignment = BoxContainer.ALIGNMENT_CENTER
	vb.add_theme_constant_override("separation", 14)
	center.add_child(vb)
	if i == 0:
		_label(vb, "desenvolvido com", 30, Color("#5b544a"))
		_label(vb, "Claude", 132, Color("#1f1d1a"))
		var line := ColorRect.new()
		line.color = Color("#d97757")
		line.custom_minimum_size = Vector2(260, 5)
		line.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		vb.add_child(line)
		_label(vb, "inteligência artificial da Anthropic", 26, Color("#5b544a"))
	else:
		_label(vb, "Feito 100% com inteligência artificial", 50, Color("#facc15"))
		var msg := _label(vb, "Todo o Craquez — código, física, gráficos, animações, sons e times —\nfoi desenvolvido por IA, a partir das ideias de um apaixonado por futebol.", 30, Color("#e2e8f0"))
		msg.custom_minimum_size = Vector2(1100, 0)
		var free := _label(vb, "Por isso o jogo é e sempre será GRATUITO.", 40, Color("#4ade80"))
		free.add_theme_constant_override("outline_size", 6)
		free.add_theme_color_override("font_outline_color", Color("#052e16"))
		_label(vb, "Divirta-se!", 30, Color("#94a3b8"))
	_layer.modulate.a = 0.0


func _label(parent: Control, text: String, size_px: int, col: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.add_theme_font_size_override("font_size", size_px)
	l.add_theme_color_override("font_color", col)
	parent.add_child(l)
	return l


func _process(delta: float) -> void:
	if page < 0 or page >= PAGES.size():
		return
	_t += delta
	var dur: float = PAGES[page]["dur"]
	if not _leaving and _t >= dur:
		_leave()
	if _leaving:
		_fade -= delta * 2.5
		if _fade <= 0.0:
			show_page(page + 1)
			return
	else:
		_fade = minf(1.0, _t * 1.8)
	_layer.modulate.a = clampf(_fade, 0.0, 1.0)


func _leave() -> void:
	if not _leaving:
		_leaving = true
		_fade = minf(_fade, 1.0)


func _input(event: InputEvent) -> void:
	if page < 0 or page >= PAGES.size() or _t < 0.6:
		return
	var press: bool = (event is InputEventKey and event.pressed and not event.echo) \
		or (event is InputEventJoypadButton and event.pressed) \
		or (event is InputEventMouseButton and event.pressed) \
		or (event is InputEventScreenTouch and event.pressed)
	if press:
		_leave()
		get_viewport().set_input_as_handled()
