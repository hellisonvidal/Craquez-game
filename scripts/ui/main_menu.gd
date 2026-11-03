## Menu principal, navegável por mouse, toque, teclado ou controle (foco).
class_name MainMenu
extends Control

signal start_match(cfg: Dictionary)

var mode_i := 0
var team_a := 0
var team_b := 1
var p1_dev := 0
var p2_dev := 0
var minutes_opts := [2.0, 3.0, 5.0, 8.0]
var minutes_i := 1
var skill_opts := [["Fácil", 0.3], ["Normal", 0.6], ["Difícil", 0.9]]
var skill_i := 1

var _buttons: Dictionary = {}
var _faces: HBoxContainer
var _devices: Array[String] = []


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	minutes_i = maxi(0, minutes_opts.find(Settings.half_minutes))
	_refresh_devices()
	_build()
	Input.joy_connection_changed.connect(_on_joy_changed)


func _on_joy_changed(_id: int, _connected: bool) -> void:
	_refresh_devices()
	_refresh()


func _refresh_devices() -> void:
	_devices = InputRouter.available_devices()
	p1_dev = clampi(p1_dev, 0, _devices.size() - 1)
	# P2 padrão: segundo dispositivo, se houver
	if p2_dev == 0 and _devices.size() > 1:
		p2_dev = 2   # índice 0 = CPU, 1.. = dispositivos


func _p2_options() -> Array[String]:
	var o: Array[String] = ["cpu"]
	o.append_array(_devices)
	return o


func _build() -> void:
	var bg := ColorRect.new()
	bg.color = Color("#07111f")
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	var grad := TextureRect.new()
	var gt := GradientTexture2D.new()
	gt.gradient = Gradient.new()
	gt.gradient.set_color(0, Color("#0f5132"))
	gt.gradient.set_color(1, Color(0.03, 0.07, 0.12, 0.0))
	gt.fill = GradientTexture2D.FILL_RADIAL
	gt.fill_from = Vector2(0.5, 1.1)
	gt.fill_to = Vector2(0.5, 0.0)
	grad.texture = gt
	grad.set_anchors_preset(Control.PRESET_FULL_RECT)
	grad.stretch_mode = TextureRect.STRETCH_SCALE
	add_child(grad)

	var scroll := ScrollContainer.new()
	scroll.set_anchors_preset(Control.PRESET_FULL_RECT)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(scroll)
	var center := CenterContainer.new()
	center.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	center.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.add_child(center)
	var vb := VBoxContainer.new()
	vb.custom_minimum_size = Vector2(560, 0)
	vb.add_theme_constant_override("separation", 10)
	center.add_child(vb)

	var title := Label.new()
	title.text = "CRAQUEZ"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 84)
	title.add_theme_color_override("font_color", Color("#facc15"))
	title.add_theme_color_override("font_outline_color", Color("#0f5132"))
	title.add_theme_constant_override("outline_size", 14)
	vb.add_child(title)
	var sub := Label.new()
	sub.text = "Campo · Futsal · Street"
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	sub.modulate = Color("#94a3b8")
	vb.add_child(sub)

	_faces = HBoxContainer.new()
	_faces.alignment = BoxContainer.ALIGNMENT_CENTER
	_faces.add_theme_constant_override("separation", 8)
	vb.add_child(_faces)

	for key in ["mode", "team_a", "team_b", "p1", "p2", "minutes", "skill", "quality"]:
		var b := Button.new()
		b.custom_minimum_size = Vector2(0, 46)
		b.add_theme_font_size_override("font_size", 20)
		b.pressed.connect(_cycle.bind(key, 1))
		b.gui_input.connect(_on_btn_input.bind(key))
		vb.add_child(b)
		_buttons[key] = b
	var play := Button.new()
	play.text = "▶  JOGAR"
	play.custom_minimum_size = Vector2(0, 64)
	play.add_theme_font_size_override("font_size", 28)
	play.pressed.connect(_on_play)
	vb.add_child(play)
	var help := Label.new()
	help.text = "Controle: A/✕ passe · B/◯ chute · X/▢ alto · Y/△ enfiada · RT/R2 correr · LB/L1 trocar\nTeclado P1: WASD · J K L I · Shift · Q    |    P2: setas · Num 1 2 3 5 · Num 0 · Num 4\n◀ ▶ (ou clique) muda a opção"
	help.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	help.add_theme_font_size_override("font_size", 14)
	help.modulate = Color("#94a3b8")
	vb.add_child(help)
	if not OS.has_feature("mobile"):
		var quit := Button.new()
		quit.text = "Sair"
		quit.pressed.connect(func(): get_tree().quit())
		vb.add_child(quit)
	_refresh()
	play.grab_focus()


func _on_btn_input(ev: InputEvent, key: String) -> void:
	if ev.is_action_pressed("ui_left"):
		_cycle(key, -1)
		accept_event()
	elif ev.is_action_pressed("ui_right"):
		_cycle(key, 1)
		accept_event()


func _cycle(key: String, d: int) -> void:
	var n := Teams.LIST.size()
	match key:
		"mode":
			mode_i = posmod(mode_i + d, GameModes.ALL.size())
		"team_a":
			team_a = posmod(team_a + d, n)
			if team_a == team_b:
				team_a = posmod(team_a + d, n)
		"team_b":
			team_b = posmod(team_b + d, n)
			if team_a == team_b:
				team_b = posmod(team_b + d, n)
		"p1":
			p1_dev = posmod(p1_dev + d, _devices.size())
		"p2":
			p2_dev = posmod(p2_dev + d, _p2_options().size())
		"minutes":
			minutes_i = posmod(minutes_i + d, minutes_opts.size())
			Settings.half_minutes = minutes_opts[minutes_i]
		"skill":
			skill_i = posmod(skill_i + d, skill_opts.size())
			Settings.cpu_skill = skill_opts[skill_i][1]
		"quality":
			Settings.quality = posmod(Settings.quality + d, Settings.QUALITY_NAMES.size())
			Settings.apply()
			FaceLibrary.clear_cache()
	Settings.save_settings()
	_refresh()


func _refresh() -> void:
	var ta := Teams.get_team(team_a)
	var tb := Teams.get_team(team_b)
	var p2: String = _p2_options()[clampi(p2_dev, 0, _p2_options().size() - 1)]
	_buttons["mode"].text = "Modo:  ◀ %s ▶" % GameModes.display_name(GameModes.ALL[mode_i])
	_buttons["team_a"].text = "Casa:  ◀ %s ▶" % ta["name"]
	_buttons["team_b"].text = "Visitante:  ◀ %s ▶" % tb["name"]
	_buttons["p1"].text = "Jogador 1:  ◀ %s ▶" % InputRouter.device_label(_devices[p1_dev])
	_buttons["p2"].text = "Jogador 2:  ◀ %s ▶" % InputRouter.device_label(p2)
	_buttons["minutes"].text = "Tempo:  ◀ %d min cada ▶" % int(minutes_opts[minutes_i])
	_buttons["skill"].text = "CPU:  ◀ %s ▶" % skill_opts[skill_i][0]
	_buttons["skill"].visible = p2 == "cpu"
	_buttons["quality"].text = "Gráficos:  ◀ %s ▶" % Settings.QUALITY_NAMES[Settings.quality]
	if Settings.quality == Settings.Quality.AUTO:
		_buttons["quality"].text += "  (%s)" % Settings.QUALITY_NAMES[Settings.effective_quality]
	# vitrine de rostos do elenco
	for c in _faces.get_children():
		c.queue_free()
	for i in 6:
		var tr := TextureRect.new()
		tr.custom_minimum_size = Vector2(72, 72)
		tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		var team_idx := team_a if i < 3 else team_b
		tr.texture = FaceLibrary.texture((team_idx * 13 + (i % 3) * 5) % FaceLibrary.COUNT, 256)
		_faces.add_child(tr)


func _on_play() -> void:
	var p2: String = _p2_options()[p2_dev]
	var p1: String = _devices[p1_dev]
	if p2 == p1:
		p2 = "cpu"
	start_match.emit({
		"mode": GameModes.ALL[mode_i], "team_a": team_a, "team_b": team_b,
		"devices": [p1, p2], "seed": int(Time.get_unix_time_from_system()) & 0xFFFFFF,
		"half_minutes": minutes_opts[minutes_i],
	})
