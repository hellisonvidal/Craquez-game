## Controle virtual para telas de toque (Android): analógico à esquerda,
## botões à direita. Multitoque. Escreve em InputRouter.touch_frame.
class_name TouchControls
extends Control

const BTNS := [
	{"label": "PASSE", "bit": InputFrame.PASS, "pos": Vector2(-150, -110), "r": 52, "col": Color("#22c55e")},
	{"label": "CHUTE", "bit": InputFrame.SHOOT, "pos": Vector2(-60, -190), "r": 52, "col": Color("#ef4444")},
	{"label": "ALTO", "bit": InputFrame.LOB, "pos": Vector2(-250, -70), "r": 44, "col": Color("#3b82f6")},
	{"label": "ENFIADA", "bit": InputFrame.THROUGH, "pos": Vector2(-160, -240), "r": 40, "col": Color("#eab308")},
	{"label": "CORRER", "bit": InputFrame.SPRINT, "pos": Vector2(-60, -75), "r": 44, "col": Color("#a855f7")},
	{"label": "TROCAR", "bit": InputFrame.SWITCH, "pos": Vector2(-260, -190), "r": 36, "col": Color("#64748b")},
]

var _stick_touch := -1
var _stick_origin := Vector2.ZERO
var _stick_pos := Vector2.ZERO
var _btn_touch: Dictionary = {}   # índice do toque -> bit
var _font: Font


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_font = ThemeDB.fallback_font
	# o primeiro desenho acontece com tamanho 0: redesenha quando a tela ganha tamanho/gira
	resized.connect(queue_redraw)


func _btn_center(b: Dictionary) -> Vector2:
	return size + Vector2(b["pos"])


func _input(event: InputEvent) -> void:
	if not visible:
		return
	if event is InputEventScreenTouch:
		var t := event as InputEventScreenTouch
		if t.pressed:
			if t.position.x < size.x * 0.45 and _stick_touch == -1:
				_stick_touch = t.index
				_stick_origin = t.position
				_stick_pos = t.position
			else:
				for b in BTNS:
					if t.position.distance_to(_btn_center(b)) < float(b["r"]) * 1.25:
						_btn_touch[t.index] = int(b["bit"])
						break
		else:
			if t.index == _stick_touch:
				_stick_touch = -1
			_btn_touch.erase(t.index)
		_update()
	elif event is InputEventScreenDrag:
		var d := event as InputEventScreenDrag
		if d.index == _stick_touch:
			_stick_pos = d.position
			_update()


func _update() -> void:
	var f := InputFrame.new()
	if _stick_touch != -1:
		var v := (_stick_pos - _stick_origin) / 70.0
		f.move = v.limit_length(1.0) if v.length() > 0.15 else Vector2.ZERO
	for k in _btn_touch:
		f.buttons |= int(_btn_touch[k])
	InputRouter.touch_frame = f
	queue_redraw()


func _draw() -> void:
	if _stick_touch != -1:
		draw_circle(_stick_origin, 75, Color(1, 1, 1, 0.12))
		draw_circle(_stick_origin + (_stick_pos - _stick_origin).limit_length(70), 34, Color(1, 1, 1, 0.35))
	else:
		var c := Vector2(150, size.y - 150)
		draw_arc(c, 75, 0, TAU, 32, Color(1, 1, 1, 0.2), 3)
	var held := InputRouter.touch_frame.buttons
	for b in BTNS:
		var on := (held & int(b["bit"])) != 0
		var col: Color = b["col"]
		var r: float = b["r"]
		draw_circle(_btn_center(b), r, col if on else Color(col, 0.45))
		draw_string(_font, _btn_center(b) + Vector2(-r, 6), b["label"], HORIZONTAL_ALIGNMENT_CENTER, r * 2, 16, Color.WHITE)
