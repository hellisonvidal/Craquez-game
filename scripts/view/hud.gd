## Placar, relógio, banners (GOL!, intervalo) com o rosto do autor do gol, pausa e fim de jogo.
class_name Hud
extends CanvasLayer

signal resume_requested
signal quit_requested
signal rematch_requested

var sim: MatchSim
var names: Array[String] = ["", ""]
var colors: Array = []

var _score_lbl: Label
var _clock_lbl: Label
var _banner: PanelContainer
var _banner_lbl: Label
var _banner_face: TextureRect
var _banner_sub: Label
var _banner_t := 0.0
var _pause: PanelContainer
var _end: PanelContainer
var _end_lbl: Label


func setup(s: MatchSim, team_names: Array[String], team_colors: Array) -> void:
	sim = s
	names = team_names
	colors = team_colors
	layer = 10
	_build()


func _panel(bg: Color) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = bg
	sb.set_corner_radius_all(12)
	sb.content_margin_left = 18
	sb.content_margin_right = 18
	sb.content_margin_top = 8
	sb.content_margin_bottom = 8
	return sb


func _build() -> void:
	var top := PanelContainer.new()
	top.add_theme_stylebox_override("panel", _panel(Color(0.04, 0.06, 0.1, 0.82)))
	top.set_anchors_preset(Control.PRESET_CENTER_TOP)
	top.position = Vector2(-220, 10)
	top.custom_minimum_size = Vector2(440, 0)
	add_child(top)
	var hb := HBoxContainer.new()
	hb.alignment = BoxContainer.ALIGNMENT_CENTER
	hb.add_theme_constant_override("separation", 14)
	top.add_child(hb)
	for t in 2:
		var sw := ColorRect.new()
		sw.color = colors[t][0]
		sw.custom_minimum_size = Vector2(10, 26)
		if t == 0:
			hb.add_child(sw)
		var nl := Label.new()
		nl.text = names[t]
		nl.add_theme_font_size_override("font_size", 22)
		hb.add_child(nl)
		if t == 0:
			_score_lbl = Label.new()
			_score_lbl.add_theme_font_size_override("font_size", 28)
			hb.add_child(_score_lbl)
		else:
			hb.add_child(sw)
	_clock_lbl = Label.new()
	_clock_lbl.add_theme_font_size_override("font_size", 18)
	_clock_lbl.modulate = Color("#facc15")
	hb.add_child(_clock_lbl)

	_banner = PanelContainer.new()
	_banner.add_theme_stylebox_override("panel", _panel(Color(0.02, 0.03, 0.06, 0.88)))
	_banner.set_anchors_preset(Control.PRESET_CENTER)
	_banner.position = Vector2(-260, -90)
	_banner.custom_minimum_size = Vector2(520, 180)
	_banner.visible = false
	add_child(_banner)
	var bh := HBoxContainer.new()
	bh.add_theme_constant_override("separation", 18)
	_banner.add_child(bh)
	_banner_face = TextureRect.new()
	_banner_face.custom_minimum_size = Vector2(160, 160)
	_banner_face.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_banner_face.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	bh.add_child(_banner_face)
	var bv := VBoxContainer.new()
	bv.alignment = BoxContainer.ALIGNMENT_CENTER
	bh.add_child(bv)
	_banner_lbl = Label.new()
	_banner_lbl.add_theme_font_size_override("font_size", 54)
	_banner_lbl.modulate = Color("#facc15")
	bv.add_child(_banner_lbl)
	_banner_sub = Label.new()
	_banner_sub.add_theme_font_size_override("font_size", 22)
	bv.add_child(_banner_sub)

	_pause = _menu_panel("PAUSA", [["Continuar", resume_requested], ["Sair para o menu", quit_requested]])
	_end = _menu_panel("FIM DE JOGO", [["Revanche", rematch_requested], ["Menu principal", quit_requested]])
	_end_lbl = _end.get_child(0).get_child(0) as Label


func _menu_panel(title: String, items: Array) -> PanelContainer:
	var pc := PanelContainer.new()
	pc.add_theme_stylebox_override("panel", _panel(Color(0.03, 0.05, 0.09, 0.94)))
	pc.set_anchors_preset(Control.PRESET_CENTER)
	pc.position = Vector2(-200, -130)
	pc.custom_minimum_size = Vector2(400, 260)
	pc.visible = false
	add_child(pc)
	var vb := VBoxContainer.new()
	vb.alignment = BoxContainer.ALIGNMENT_CENTER
	vb.add_theme_constant_override("separation", 14)
	pc.add_child(vb)
	var tl := Label.new()
	tl.text = title
	tl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	tl.add_theme_font_size_override("font_size", 34)
	vb.add_child(tl)
	for it in items:
		var b := Button.new()
		b.text = it[0]
		b.custom_minimum_size = Vector2(0, 52)
		b.add_theme_font_size_override("font_size", 22)
		var sig: Signal = it[1]
		b.pressed.connect(func(): sig.emit())
		vb.add_child(b)
	return pc


func show_pause(on: bool) -> void:
	_pause.visible = on
	if on:
		(_pause.get_child(0).get_child(1) as Button).grab_focus()


func show_end() -> void:
	_end.visible = true
	_end_lbl.text = "FIM  %s %d x %d %s" % [names[0], sim.score[0], sim.score[1], names[1]]
	(_end.get_child(0).get_child(1) as Button).grab_focus()


func is_menu_open() -> bool:
	return _pause.visible or _end.visible


func on_event(e: Dictionary) -> void:
	match e["type"]:
		"goal":
			var scorer: int = e.get("scorer", -1)
			var t: int = e["team"]
			_banner_lbl.text = "GOOOL!"
			if scorer >= 0:
				var p: SimPlayer = sim.players[scorer]
				_banner_face.texture = FaceLibrary.texture(p.face_id)
				var who := "%s (%d)" % [p.name, p.number]
				_banner_sub.text = ("Gol contra de " + who) if e.get("own_goal", false) else (who + " — " + names[t])
			else:
				_banner_face.texture = null
				_banner_sub.text = names[t]
			_flash(2.6)
		"whistle":
			var kind: String = e.get("kind", "")
			if kind == "halftime":
				_banner_lbl.text = "INTERVALO"
				_banner_sub.text = "Troca de lado"
				_banner_face.texture = null
				_flash(2.8)
			elif kind == "penalty":
				_banner_lbl.text = "PÊNALTI!"
				_banner_sub.text = names[int(e.get("team", 0))] + " cobra"
				_banner_face.texture = null
				_flash(1.6)
			elif kind in ["corner", "freekick", "goalkick", "throwin", "offside"]:
				var txt := {"corner": "Escanteio", "freekick": "Falta", "goalkick": "Tiro de meta", "throwin": "Lateral", "offside": "Impedimento"}
				_banner_lbl.text = txt[kind]
				_banner_sub.text = names[int(e.get("team", 0))]
				_banner_face.texture = null
				_flash(0.9)


func show_message(title: String, sub: String, t: float) -> void:
	_banner_lbl.text = title
	_banner_sub.text = sub
	_banner_face.texture = null
	_flash(t)


func _flash(t: float) -> void:
	_banner.visible = true
	_banner_t = t


func _process(delta: float) -> void:
	if sim == null:
		return
	_score_lbl.text = "%d  x  %d" % [sim.score[0], sim.score[1]]
	var match_min := 45.0
	if sim.mode["id"] != GameModes.FIELD:
		match_min = 20.0
	var game_t := sim.clock / sim.half_seconds * match_min + (sim.half - 1) * match_min
	_clock_lbl.text = "%02d:%02d  %dºT" % [int(game_t), int(fmod(game_t, 1.0) * 60.0), sim.half]
	if _banner_t > 0.0:
		_banner_t -= delta
		if _banner_t <= 0.0:
			_banner.visible = false
