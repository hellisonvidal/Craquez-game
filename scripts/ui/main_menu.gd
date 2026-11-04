## Menu em páginas: Título → Modos de jogo → Configurações → Escolha dos times.
## Navegável por mouse, toque, teclado ou controle (foco). Esc / B volta.
class_name MainMenu
extends Control

signal start_match(cfg: Dictionary)

enum Page { TITLE, MODES, SETUP, TEAMS, OPTIONS }

var mode_i := 0
var online := false
var team_a := 0
var team_b := 1
var p1_dev := 0
var p2_dev := 0
var minutes_opts := [3.0, 5.0, 10.0, 20.0, 45.0]
var minutes_i := 0
const WEATHERS := [["sol", "Ensolarado"], ["nublado", "Nublado"], ["chuva", "Chuva"], ["tempestade", "Tempestade"], ["neve", "Neve"], ["aleatorio", "Aleatório"]]
const TIMES := [["dia", "Dia"], ["tarde", "Entardecer"], ["noite", "Noite"]]
var weather_i := 0
var tod_i := 2
var dyn_weather := true
var venue_i := -1   # -1 aleatório
var _tactics: Array = [{}, {}]   # por lado: {formation, mentality, press, lineup}
var _swap_sel := -1
var _roster_side := 0
const MENTALITIES := ["Defensiva", "Equilibrada", "Ofensiva"]
const PRESSES := ["Baixa", "Média", "Alta"]
var kit_a := 0     # 0 principal, 1 reserva, 2 terceiro
var kit_b := -1    # -1 automático (evita cores parecidas)
var _roster_overlay: Control
var skill_opts := [["Fácil", 0.3], ["Normal", 0.6], ["Difícil", 0.9]]
var skill_i := 1
var page: int = Page.TITLE
## começar direto nos modos (voltando de uma partida)
var start_page: int = Page.TITLE

var _content: Control
var _labels: Dictionary = {}          # chave -> Label do seletor
var _devices: Array[String] = []
var _session: EnetSession
var _ip_edit: LineEdit
var _net_status: Label
var _theme: Theme
var _bg_t := 0.0
var _side_widgets: Array = [{}, {}]   # widgets de cada lado na tela de times
var _first_focus: Control
var _confetti: Array = []     # [pos, vel, cor, giro] (tela título)


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	# o pai é um Node (não Control): no Godot 4.4 as âncoras não dão tamanho
	_fit()
	get_viewport().size_changed.connect(_fit)
	minutes_i = maxi(0, minutes_opts.find(Settings.half_minutes))
	skill_i = 0 if Settings.cpu_skill < 0.45 else (2 if Settings.cpu_skill > 0.75 else 1)
	for lg: Dictionary in Teams.leagues():
		if String(lg["id"]) == "bra":
			team_a = int(lg["first"]) + 8
			team_b = int(lg["first"]) + 13
	team_a = posmod(team_a, Teams.count())
	team_b = posmod(team_b, Teams.count())
	_refresh_devices()
	_theme = _make_theme()
	theme = _theme
	_content = Control.new()
	_content.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_content)
	Input.joy_connection_changed.connect(_on_joy_changed)
	show_page(start_page)


func _fit() -> void:
	position = Vector2.ZERO
	size = get_viewport().get_visible_rect().size


# ------------------------------------------------------------------ visual

func _make_theme() -> Theme:
	var t := Theme.new()
	var normal := StyleBoxFlat.new()
	normal.bg_color = Color(0.07, 0.11, 0.18, 0.92)
	normal.border_color = Color(1, 1, 1, 0.08)
	normal.set_border_width_all(2)
	normal.set_corner_radius_all(14)
	normal.content_margin_left = 18
	normal.content_margin_right = 18
	normal.content_margin_top = 10
	normal.content_margin_bottom = 10
	var hover := normal.duplicate()
	hover.bg_color = Color(0.11, 0.18, 0.28, 0.95)
	var pressed := normal.duplicate()
	pressed.bg_color = Color(0.05, 0.3, 0.18, 0.95)
	var focus := normal.duplicate()
	focus.bg_color = Color(0, 0, 0, 0)
	focus.border_color = Color("#facc15")
	focus.set_border_width_all(4)
	t.set_stylebox("normal", "Button", normal)
	t.set_stylebox("hover", "Button", hover)
	t.set_stylebox("pressed", "Button", pressed)
	t.set_stylebox("focus", "Button", focus)
	t.set_stylebox("disabled", "Button", normal)
	t.set_font_size("font_size", "Button", 26)
	t.set_font_size("font_size", "Label", 24)
	t.set_color("font_color", "Button", Color("#e2e8f0"))
	t.set_color("font_hover_color", "Button", Color.WHITE)
	t.set_color("font_focus_color", "Button", Color.WHITE)
	var panel := StyleBoxFlat.new()
	panel.bg_color = Color(0.04, 0.07, 0.12, 0.88)
	panel.set_corner_radius_all(22)
	panel.border_color = Color(1, 1, 1, 0.07)
	panel.set_border_width_all(2)
	panel.content_margin_left = 26
	panel.content_margin_right = 26
	panel.content_margin_top = 20
	panel.content_margin_bottom = 20
	t.set_stylebox("panel", "PanelContainer", panel)
	return t


## Fundo animado: gramado noturno com faixas, linhas e feixes de refletor.
func _draw() -> void:
	if page == Page.TITLE:
		_draw_title_bg()
		return
	var s := size
	draw_rect(Rect2(Vector2.ZERO, s), Color("#06101d"))
	var gy := s.y * 0.58
	var stripes := 14
	for i in stripes:
		var x0 := s.x * float(i) / stripes
		var c := Color("#0d3b1f") if i % 2 == 0 else Color("#0f4523")
		draw_colored_polygon(PackedVector2Array([
			Vector2(s.x * 0.5 + (x0 - s.x * 0.5) * 0.45, gy), Vector2(s.x * 0.5 + (x0 + s.x / stripes - s.x * 0.5) * 0.45, gy),
			Vector2(s.x * 0.5 + (x0 + s.x / stripes - s.x * 0.5) * 1.9, s.y), Vector2(s.x * 0.5 + (x0 - s.x * 0.5) * 1.9, s.y)]), c)
	var lc := Color(1, 1, 1, 0.12)
	draw_line(Vector2(s.x * 0.05, s.y), Vector2(s.x * 0.28, gy), lc, 3)
	draw_line(Vector2(s.x * 0.95, s.y), Vector2(s.x * 0.72, gy), lc, 3)
	draw_line(Vector2(s.x * 0.28, gy), Vector2(s.x * 0.72, gy), lc, 3)
	draw_arc(Vector2(s.x * 0.5, s.y * 0.86), s.x * 0.16, PI, TAU, 48, lc, 3)
	# feixes de luz dos refletores
	for k in 4:
		var src := Vector2(s.x * (0.08 + k * 0.28), -20)
		var sway := sin(_bg_t * 0.4 + k) * 0.06
		var a := Color(1.0, 0.97, 0.85, 0.05)
		draw_colored_polygon(PackedVector2Array([src, Vector2(s.x * (0.3 + sway + k * 0.13), gy), Vector2(s.x * (0.42 + sway + k * 0.13), gy)]), a)
		draw_circle(src + Vector2(0, 30), 26, Color(1, 0.97, 0.85, 0.5))
	# escurece por cima para o texto ler bem
	draw_rect(Rect2(Vector2.ZERO, s), Color(0.02, 0.04, 0.08, 0.35))


## Abertura: céu em degradê, estádio em silhueta com refletores, feixes
## coloridos girando, confete caindo e bola quicando.
func _draw_title_bg() -> void:
	var s := size
	var t := _bg_t
	# céu em degradê (azul-noite → roxo → laranja no horizonte)
	var bands := 24
	for i in bands:
		var u := float(i) / bands
		var c := Color("#0b1033").lerp(Color("#5b21b6"), clampf(u * 1.4, 0.0, 1.0)).lerp(Color("#f97316"), clampf((u - 0.55) * 2.2, 0.0, 1.0))
		draw_rect(Rect2(0, s.y * 0.62 * u, s.x, s.y * 0.62 / bands + 1.0), c)
	# feixes de luz coloridos
	var beam_cols := [Color(1.0, 0.85, 0.2, 0.16), Color(0.2, 0.9, 0.5, 0.13), Color(0.3, 0.6, 1.0, 0.14), Color(1.0, 0.3, 0.6, 0.12)]
	for k in 4:
		var src := Vector2(s.x * (0.12 + k * 0.25), s.y * 0.66)
		var ang := -PI / 2 + sin(t * 0.5 + k * 1.7) * 0.55
		var dir := Vector2(cos(ang), sin(ang))
		var perp := Vector2(-dir.y, dir.x)
		var far := src + dir * s.y * 1.2
		draw_colored_polygon(PackedVector2Array([src, far + perp * s.x * 0.09, far - perp * s.x * 0.09]), beam_cols[k])
	# estádio em silhueta (arquibancada em arco) com pontinhos de torcida
	var base_y := s.y * 0.62
	var arc := PackedVector2Array([Vector2(0, s.y), Vector2(0, base_y - s.y * 0.06)])
	for i in 33:
		var x := s.x * i / 32.0
		arc.append(Vector2(x, base_y - s.y * 0.06 - sin(PI * i / 32.0) * s.y * 0.07))
	arc.append(Vector2(s.x, s.y))
	draw_colored_polygon(arc, Color("#0a0f1f"))
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	var crowd_cols := [Color("#facc15"), Color("#22c55e"), Color("#3b82f6"), Color("#ef4444"), Color("#f8fafc"), Color("#a855f7")]
	for i in 420:
		var x := rng.randf() * s.x
		var top := base_y - s.y * 0.06 - sin(PI * x / s.x) * s.y * 0.07
		var y := top + rng.randf() * s.y * 0.08
		var jump := maxf(0.0, sin(t * 6.0 + i)) * 2.0
		draw_circle(Vector2(x, y - jump), 2.2, crowd_cols[i % crowd_cols.size()].darkened(0.25))
	# torres de refletor acesas
	for k in 4:
		var px := s.x * (0.08 + k * 0.28)
		var py := base_y - s.y * 0.22
		draw_line(Vector2(px, py), Vector2(px, base_y), Color("#1e293b"), 6.0)
		draw_rect(Rect2(px - 26, py - 16, 52, 18), Color("#fffbeb"))
		draw_circle(Vector2(px, py - 7), 46.0, Color(1, 0.97, 0.8, 0.12))
		draw_circle(Vector2(px, py - 7), 22.0, Color(1, 0.97, 0.85, 0.25))
	# gramado em perspectiva
	var gy := base_y + s.y * 0.04
	for i in 14:
		var x0 := s.x * float(i) / 14.0
		var c2 := Color("#15803d") if i % 2 == 0 else Color("#16a34a")
		draw_colored_polygon(PackedVector2Array([
			Vector2(s.x * 0.5 + (x0 - s.x * 0.5) * 0.5, gy), Vector2(s.x * 0.5 + (x0 + s.x / 14.0 - s.x * 0.5) * 0.5, gy),
			Vector2(s.x * 0.5 + (x0 + s.x / 14.0 - s.x * 0.5) * 2.0, s.y), Vector2(s.x * 0.5 + (x0 - s.x * 0.5) * 2.0, s.y)]), c2)
	draw_rect(Rect2(0, base_y, s.x, gy - base_y), Color("#0f172a"))
	draw_arc(Vector2(s.x * 0.5, s.y * 1.05), s.x * 0.2, PI, TAU, 48, Color(1, 1, 1, 0.35), 4.0)
	# bola quicando
	var bounce := absf(sin(t * 2.6))
	var bp := Vector2(s.x * 0.5 + sin(t * 0.7) * s.x * 0.18, s.y * 0.9 - bounce * s.y * 0.14)
	draw_circle(Vector2(bp.x, s.y * 0.92), 26.0 * (1.0 - bounce * 0.4), Color(0, 0, 0, 0.35))
	draw_circle(bp, 24.0, Color.WHITE)
	for k in 5:
		var a := t * 3.0 + TAU * k / 5.0
		draw_circle(bp + Vector2(cos(a), sin(a)) * 12.0, 5.5, Color("#111827"))
	draw_circle(bp, 6.0, Color("#111827"))
	draw_arc(bp, 24.0, 0, TAU, 32, Color("#111827"), 2.0)
	# confete
	for c in _confetti:
		var pos: Vector2 = c[0]
		var rot: float = c[3]
		var d := Vector2(cos(rot), sin(rot)) * 6.0
		draw_line(pos - d, pos + d, c[2], 5.0)
	# brilho atrás do logotipo
	var lc := Vector2(s.x * 0.5, s.y * 0.3)
	for r in 6:
		draw_circle(lc, s.x * (0.32 - r * 0.045), Color(1.0, 0.85, 0.2, 0.03 + r * 0.012))


func _update_confetti(delta: float) -> void:
	var s := size
	if _confetti.size() < 140:
		var cols := [Color("#facc15"), Color("#22c55e"), Color("#3b82f6"), Color("#ef4444"), Color("#f8fafc"), Color("#ec4899"), Color("#f97316")]
		_confetti.append([Vector2(randf() * s.x, -10.0), Vector2(randf_range(-30, 30), randf_range(60, 140)), cols[randi() % cols.size()], randf() * TAU])
	for c in _confetti:
		c[0] = (c[0] as Vector2) + (c[1] as Vector2) * delta + Vector2(sin(_bg_t * 2.0 + float(c[3])) * 20.0 * delta, 0)
		c[3] = float(c[3]) + delta * 4.0
		if (c[0] as Vector2).y > s.y + 20.0:
			c[0] = Vector2(randf() * s.x, -10.0)


func _process(delta: float) -> void:
	_bg_t += delta
	if page == Page.TITLE:
		_update_confetti(delta)
	queue_redraw()
	if page == Page.TITLE and _labels.has("_press"):
		(_labels["_press"] as Label).modulate.a = 0.55 + 0.45 * sin(_bg_t * 3.0)
	if _session != null:
		_session.poll()
		if _session != null and _net_status != null and _net_status.text.find(_session.status) == -1:
			_update_net_status()


# ------------------------------------------------------------------ páginas

func show_page(p: int) -> void:
	page = p
	_roster_overlay = null
	_labels.clear()
	_first_focus = null
	for c in _content.get_children():
		c.queue_free()
	match p:
		Page.TITLE:
			_page_title()
		Page.MODES:
			_page_modes()
		Page.SETUP:
			_page_setup()
		Page.TEAMS:
			_page_teams()
		Page.OPTIONS:
			_page_options()
	_refresh()
	if _first_focus != null:
		_first_focus.call_deferred("grab_focus")


func _center_box(width: float) -> VBoxContainer:
	var center := CenterContainer.new()
	center.set_anchors_preset(Control.PRESET_FULL_RECT)
	_content.add_child(center)
	var vb := VBoxContainer.new()
	vb.custom_minimum_size = Vector2(width, 0)
	vb.add_theme_constant_override("separation", 14)
	center.add_child(vb)
	return vb


func _title_label(parent: Control, text: String, size_px: int, col: Color = Color("#facc15")) -> Label:
	var l := Label.new()
	l.text = text
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	l.add_theme_font_size_override("font_size", size_px)
	l.add_theme_color_override("font_color", col)
	l.add_theme_color_override("font_outline_color", Color("#052e16"))
	l.add_theme_constant_override("outline_size", maxi(4, size_px / 7))
	parent.add_child(l)
	return l


func _small(parent: Control, text: String, col: Color = Color("#94a3b8"), size_px: int = 20) -> Label:
	var l := Label.new()
	l.text = text
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.add_theme_font_size_override("font_size", size_px)
	l.add_theme_color_override("font_color", col)
	parent.add_child(l)
	return l


func _button(parent: Control, text: String, cb: Callable, h: float = 64.0) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(0, h)
	b.pressed.connect(cb)
	parent.add_child(b)
	if _first_focus == null:
		_first_focus = b
	return b


## Seletor ◀ valor ▶ (toque nas setas, clique no meio avança, ←/→ no teclado/controle).
func _cycler(parent: Control, key: String, caption: String) -> HBoxContainer:
	var hb := HBoxContainer.new()
	hb.add_theme_constant_override("separation", 8)
	parent.add_child(hb)
	if caption != "":
		var cap := Label.new()
		cap.text = caption
		cap.custom_minimum_size = Vector2(230, 0)
		cap.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
		cap.add_theme_color_override("font_color", Color("#94a3b8"))
		hb.add_child(cap)
	var left := Button.new()
	left.text = "◀"
	left.focus_mode = Control.FOCUS_NONE
	left.custom_minimum_size = Vector2(64, 60)
	left.pressed.connect(_cycle.bind(key, -1))
	hb.add_child(left)
	var mid := Button.new()
	mid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mid.custom_minimum_size = Vector2(0, 60)
	mid.clip_text = true
	mid.pressed.connect(_cycle.bind(key, 1))
	mid.gui_input.connect(_on_cycler_input.bind(key))
	hb.add_child(mid)
	var right := Button.new()
	right.text = "▶"
	right.focus_mode = Control.FOCUS_NONE
	right.custom_minimum_size = Vector2(64, 60)
	right.pressed.connect(_cycle.bind(key, 1))
	hb.add_child(right)
	_labels[key] = mid
	if _first_focus == null:
		_first_focus = mid
	return hb


func _on_cycler_input(ev: InputEvent, key: String) -> void:
	if ev.is_action_pressed("ui_left"):
		_cycle(key, -1)
		accept_event()
	elif ev.is_action_pressed("ui_right"):
		_cycle(key, 1)
		accept_event()


func _nav_row(parent: Control, back: Callable, next_text: String, next: Callable) -> Button:
	var hb := HBoxContainer.new()
	hb.add_theme_constant_override("separation", 16)
	parent.add_child(hb)
	var b := Button.new()
	b.text = "◀  Voltar"
	b.custom_minimum_size = Vector2(220, 70)
	b.pressed.connect(back)
	hb.add_child(b)
	var n := Button.new()
	n.text = next_text
	n.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	n.custom_minimum_size = Vector2(0, 70)
	n.add_theme_font_size_override("font_size", 32)
	n.pressed.connect(next)
	hb.add_child(n)
	return n


func _page_title() -> void:
	var vb := _center_box(900)
	var logo := Control.new()
	logo.custom_minimum_size = Vector2(900, 230)
	vb.add_child(logo)
	# camadas de sombra coloridas atrás do nome
	for layer in [[Vector2(10, 12), Color("#7c3aed")], [Vector2(6, 7), Color("#16a34a")], [Vector2(0, 0), Color("#facc15")]]:
		var l := Label.new()
		l.text = "CRAQUEZ"
		l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		l.size = Vector2(900, 230)
		l.position = layer[0]
		l.add_theme_font_size_override("font_size", 190)
		l.add_theme_color_override("font_color", layer[1])
		l.add_theme_color_override("font_outline_color", Color("#0b1033"))
		l.add_theme_constant_override("outline_size", 22)
		logo.add_child(l)
	var sub := _small(vb, "TEMPORADA 2026/27  ·  CAMPO  ·  FUTSAL  ·  STREET", Color("#f8fafc"), 30)
	sub.add_theme_color_override("font_outline_color", Color("#0b1033"))
	sub.add_theme_constant_override("outline_size", 8)
	var gap := Control.new()
	gap.custom_minimum_size = Vector2(0, 70)
	vb.add_child(gap)
	var press := _small(vb, "Toque na tela ou aperte qualquer botão", Color("#facc15"), 30)
	_labels["_press"] = press
	_small(vb, "%d ligas · %d clubes" % [Teams.leagues().size(), Teams.count()], Color("#64748b"), 20)


func _page_modes() -> void:
	var vb := _center_box(1180)
	_title_label(vb, "MODOS DE JOGO", 64)
	var grid := GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 18)
	grid.add_theme_constant_override("v_separation", 18)
	vb.add_child(grid)
	var cards := [
		["CAMPO  11 × 11", "Estádio, 8 ligas de futebol 2026/27, impedimento e pênalti", GameModes.FIELD, false],
		["FUTSAL  5 × 5", "Ginásio, ligas de futsal do Brasil, Espanha, Portugal, Itália e Argentina", GameModes.FUTSAL, false],
		["STREET  3 × 3", "Quadra de rua com tabela no muro, sem goleiro", GameModes.STREET, false],
		["ONLINE", "Partida contra um amigo pela rede (crie ou entre numa sala)", "", true],
	]
	for c in cards:
		var b := Button.new()
		b.custom_minimum_size = Vector2(570, 150)
		b.text = "%s\n%s" % [c[0], c[1]]
		b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		b.add_theme_font_size_override("font_size", 24)
		b.pressed.connect(_pick_mode.bind(String(c[2]), bool(c[3])))
		grid.add_child(b)
		if _first_focus == null:
			_first_focus = b
	var hb := HBoxContainer.new()
	hb.add_theme_constant_override("separation", 16)
	vb.add_child(hb)
	var opt := Button.new()
	opt.text = "Opções"
	opt.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	opt.custom_minimum_size = Vector2(0, 60)
	opt.pressed.connect(show_page.bind(Page.OPTIONS))
	hb.add_child(opt)
	if not OS.has_feature("mobile"):
		var quit := Button.new()
		quit.text = "Sair"
		quit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		quit.custom_minimum_size = Vector2(0, 60)
		quit.pressed.connect(func(): get_tree().quit())
		hb.add_child(quit)


func _pick_mode(mode_id: String, is_online: bool) -> void:
	online = is_online
	if not is_online:
		mode_i = maxi(0, GameModes.ALL.find(mode_id))
	_ensure_teams_fit_mode()
	show_page(Page.SETUP)


func _page_setup() -> void:
	var vb := _center_box(1000)
	_title_label(vb, ("ONLINE" if online else GameModes.display_name(GameModes.ALL[mode_i]).to_upper()) + "  ·  CONFIGURAÇÕES", 50)
	var panel := PanelContainer.new()
	vb.add_child(panel)
	var inner := VBoxContainer.new()
	inner.add_theme_constant_override("separation", 12)
	panel.add_child(inner)
	if online:
		_cycler(inner, "mode", "Modo")
	_cycler(inner, "p1", "Jogador 1")
	if not online:
		_cycler(inner, "p2", "Jogador 2")
		_cycler(inner, "skill", "Dificuldade CPU")
	_cycler(inner, "minutes", "Tempo de jogo")
	if GameModes.ALL[mode_i] in [GameModes.FUTSAL, GameModes.STREET]:
		_cycler(inner, "venue", "Local")
	if GameModes.ALL[mode_i] != GameModes.FUTSAL or online:
		_cycler(inner, "weather", "Clima")
		_cycler(inner, "tod", "Horário")
		_cycler(inner, "dyn", "Clima dinâmico")
	_labels["_help"] = _small(inner, "", Color("#94a3b8"), 17)
	_nav_row(vb, show_page.bind(Page.MODES), "Escolher times  ▶", show_page.bind(Page.TEAMS))


func _controls_help() -> String:
	return "A/✕ passe · B/◯ chute · X/▢ alto · Y/△ enfiada · RT/R2 correr · LB/L1 trocar · LT/L2 proteger · RB/R1 colocado · analógico direito: dribles"


func _page_teams() -> void:
	var root := VBoxContainer.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.offset_left = 30
	root.offset_right = -30
	root.offset_top = 20
	root.offset_bottom = -20
	root.add_theme_constant_override("separation", 12)
	_content.add_child(root)
	_title_label(root, "ESCOLHA OS TIMES", 40)
	var row := HBoxContainer.new()
	row.size_flags_vertical = Control.SIZE_EXPAND_FILL
	row.add_theme_constant_override("separation", 20)
	root.add_child(row)
	for side in 2:
		var panel := PanelContainer.new()
		panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(panel)
		var inner := VBoxContainer.new()
		inner.add_theme_constant_override("separation", 10)
		panel.add_child(inner)
		var head := _small(inner, "CASA" if side == 0 else "VISITANTE", Color("#facc15") if side == 0 else Color("#38bdf8"), 26)
		head.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		var lrow := _cycler(inner, "league_a" if side == 0 else "league_b", "")
		var licon := Crest.new()
		licon.custom_minimum_size = Vector2(60, 60)
		lrow.add_child(licon)
		lrow.move_child(licon, 0)
		_cycler(inner, "team_a" if side == 0 else "team_b", "")
		(_labels["team_a" if side == 0 else "team_b"] as Button).add_theme_font_size_override("font_size", 30)
		_cycler(inner, "kit_a" if side == 0 else "kit_b", "")
		(_labels["kit_a" if side == 0 else "kit_b"] as Button).add_theme_font_size_override("font_size", 20)
		var badges := HBoxContainer.new()
		badges.alignment = BoxContainer.ALIGNMENT_CENTER
		badges.add_theme_constant_override("separation", 24)
		inner.add_child(badges)
		var crest := Crest.new()
		crest.custom_minimum_size = Vector2(100, 100)
		badges.add_child(crest)
		var kit := KitBadge.new()
		kit.custom_minimum_size = Vector2(130, 100)
		badges.add_child(kit)
		var info := _small(inner, "", Color("#94a3b8"), 18)
		var roster_btn := Button.new()
		roster_btn.text = "Táticas e elenco"
		roster_btn.custom_minimum_size = Vector2(0, 44)
		roster_btn.add_theme_font_size_override("font_size", 20)
		roster_btn.pressed.connect(_show_roster.bind(side))
		inner.add_child(roster_btn)
		var faces := HBoxContainer.new()
		faces.alignment = BoxContainer.ALIGNMENT_CENTER
		faces.add_theme_constant_override("separation", 10)
		inner.add_child(faces)
		_side_widgets[side] = {"kit": kit, "faces": faces, "crest": crest, "licon": licon, "info": info}
		if side == 0:
			var vs := Label.new()
			vs.text = "VS"
			vs.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
			vs.add_theme_font_size_override("font_size", 56)
			vs.add_theme_color_override("font_color", Color("#e2e8f0"))
			row.add_child(vs)
	if online:
		_build_online(root)
	else:
		var play := _nav_row(root, show_page.bind(Page.SETUP), "▶  JOGAR", _on_play)
		play.add_theme_color_override("font_color", Color("#4ade80"))


func _page_options() -> void:
	var vb := _center_box(900)
	_title_label(vb, "OPÇÕES", 60)
	var panel := PanelContainer.new()
	vb.add_child(panel)
	var inner := VBoxContainer.new()
	inner.add_theme_constant_override("separation", 12)
	panel.add_child(inner)
	_cycler(inner, "quality", "Gráficos")
	_cycler(inner, "sound", "Som")
	_cycler(inner, "vibration", "Vibração")
	var of_lbl := _small(inner, "", Color("#94a3b8"), 18)
	of_lbl.text = ("Option file ativo: %d times personalizados\n%s" % [Teams.override_count, Teams.option_dir]) if Teams.override_count > 0 \
		else "Option file: nenhum. Exporte o modelo, edite e salve como option_file.json\n(Windows: pasta option_file ao lado do Craquez.exe)"
	_labels["_of"] = of_lbl
	var hb := HBoxContainer.new()
	hb.add_theme_constant_override("separation", 12)
	inner.add_child(hb)
	var exp := Button.new()
	exp.text = "Exportar modelo do option file"
	exp.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	exp.custom_minimum_size = Vector2(0, 56)
	exp.pressed.connect(_export_option)
	hb.add_child(exp)
	var rel := Button.new()
	rel.text = "Recarregar"
	rel.custom_minimum_size = Vector2(200, 56)
	rel.pressed.connect(_reload_option)
	hb.add_child(rel)
	var back := Button.new()
	back.text = "◀  Voltar"
	back.custom_minimum_size = Vector2(0, 66)
	back.pressed.connect(show_page.bind(Page.MODES))
	vb.add_child(back)


## Desenho do uniforme (camisa + calção) e força do time.
class KitBadge:
	extends Control
	var c1 := Color.WHITE
	var c2 := Color.BLACK
	var shorts := Color.BLACK
	var pattern := 0
	var rating := 0.7
	var short_name := ""

	func _draw() -> void:
		var cx := size.x * 0.5
		var h := size.y
		var w := h * 0.9
		var top := Vector2(cx - w * 0.5, 4)
		# camisa
		var shirt := PackedVector2Array([
			top + Vector2(w * 0.3, 0), top + Vector2(w * 0.7, 0), top + Vector2(w, w * 0.22), top + Vector2(w * 0.86, w * 0.36),
			top + Vector2(w * 0.76, w * 0.3), top + Vector2(w * 0.76, h * 0.68), top + Vector2(w * 0.24, h * 0.68),
			top + Vector2(w * 0.24, w * 0.3), top + Vector2(w * 0.14, w * 0.36), top + Vector2(0, w * 0.22)])
		draw_colored_polygon(shirt, c1)
		# padrão do uniforme recortado pela camisa
		var bands: Array = []
		var x0 := top.x
		match pattern:
			1:
				for i in 7:
					if i % 2 == 1:
						bands.append(Rect2(Vector2(x0 + w * i / 7.0, top.y), Vector2(w / 7.0, h)))
			2:
				for i in 6:
					if i % 2 == 1:
						bands.append(Rect2(Vector2(x0, top.y + h * 0.68 * i / 6.0), Vector2(w, h * 0.68 / 6.0)))
			4:
				bands.append(Rect2(Vector2(x0 + w * 0.5, top.y), Vector2(w * 0.26, h)))
			5:
				for i in 12:
					bands.append(Rect2(Vector2(x0 + w * i / 12.0, top.y), Vector2(w * 0.012, h)))
			6:
				bands.append(Rect2(Vector2(x0, top.y + h * 0.28), Vector2(w, h * 0.1)))
		for b: Rect2 in bands:
			var poly := PackedVector2Array([b.position, b.position + Vector2(b.size.x, 0), b.end, b.position + Vector2(0, b.size.y)])
			for part in Geometry2D.intersect_polygons(shirt, poly):
				draw_colored_polygon(part, c2)
		if pattern == 3:
			var sash := PackedVector2Array([top + Vector2(w * 0.2, 0), top + Vector2(w * 0.38, 0), top + Vector2(w * 0.8, h * 0.68), top + Vector2(w * 0.62, h * 0.68)])
			for part in Geometry2D.intersect_polygons(shirt, sash):
				draw_colored_polygon(part, c2)
		shirt.append(shirt[0])
		draw_polyline(shirt, Color(0, 0, 0, 0.8), 3)
		draw_rect(Rect2(top + Vector2(w * 0.26, h * 0.7), Vector2(w * 0.48, h * 0.24)), shorts)
		draw_rect(Rect2(top + Vector2(w * 0.26, h * 0.7), Vector2(w * 0.48, h * 0.24)), Color(0, 0, 0, 0.8), false, 3)
		var f := ThemeDB.fallback_font
		draw_string(f, top + Vector2(0, h * 0.5), short_name, HORIZONTAL_ALIGNMENT_CENTER, w, int(h * 0.2), c2 if c2.get_luminance() != c1.get_luminance() else Color.BLACK)
		# força (estrelas)
		var stars := clampi(roundi((rating - 0.62) / 0.05), 1, 5)
		for i in 5:
			draw_circle(Vector2(cx + w * 0.75, h - 14 - i * h * 0.19), h * 0.065, Color("#facc15") if i < stars else Color(1, 1, 1, 0.15))


# ------------------------------------------------------------------ entrada / navegação

func _unhandled_input(event: InputEvent) -> void:
	if page == Page.TITLE:
		var press: bool = (event is InputEventKey and event.pressed and not event.echo) \
			or (event is InputEventJoypadButton and event.pressed) \
			or (event is InputEventMouseButton and event.pressed) \
			or (event is InputEventScreenTouch and event.pressed)
		if press:
			get_viewport().set_input_as_handled()
			show_page(Page.MODES)
		return
	if event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		if _roster_overlay != null:
			_swap_sel = -1
			_close_roster()
			return
		match page:
			Page.MODES:
				show_page(Page.TITLE)
			Page.SETUP, Page.OPTIONS:
				show_page(Page.MODES)
			Page.TEAMS:
				_reset_session()
				show_page(Page.SETUP)


func _on_joy_changed(_id: int, _connected: bool) -> void:
	_refresh_devices()
	_refresh()


func _refresh_devices() -> void:
	_devices = InputRouter.available_devices()
	# padrão conforme a plataforma: controle conectado > toque (celular) > teclado
	p1_dev = ControlsHelp.default_device(_devices)
	if p2_dev == 0 and _devices.size() > 1 and not OS.has_feature("mobile"):
		p2_dev = 2   # índice 0 = CPU, 1.. = dispositivos


func _p2_options() -> Array[String]:
	var o: Array[String] = ["cpu"]
	o.append_array(_devices)
	return o


## Times do modo: futsal usa as ligas de futsal; campo/street as de futebol.
func _leagues_for_mode() -> Array:
	var want := "futsal" if GameModes.ALL[mode_i] == GameModes.FUTSAL else "football"
	var out: Array = []
	for lg: Dictionary in Teams.leagues():
		if String(lg.get("sport", "football")) == want:
			out.append(lg)
	if out.is_empty():
		out = Teams.leagues()
	return out


func _ensure_teams_fit_mode() -> void:
	var lgs := _leagues_for_mode()
	if lgs.is_empty():
		return
	var ok_a := false
	var ok_b := false
	for lg: Dictionary in lgs:
		var f := int(lg["first"])
		var n := int(lg["count"])
		ok_a = ok_a or (team_a >= f and team_a < f + n)
		ok_b = ok_b or (team_b >= f and team_b < f + n)
	if not ok_a or not ok_b:
		var lg0: Dictionary = lgs[0]
		team_a = int(lg0["first"])
		team_b = int(lg0["first"]) + 1


func _cycle(key: String, d: int) -> void:
	var n := Teams.count()
	match key:
		"mode":
			mode_i = posmod(mode_i + d, GameModes.ALL.size())
			_ensure_teams_fit_mode()
		"league_a":
			team_a = _league_step(team_a, d, team_b)
			_tactics[0] = {}
		"league_b":
			team_b = _league_step(team_b, d, team_a)
			_tactics[1] = {}
		"team_a":
			team_a = _team_step(team_a, d, team_b, n)
			_tactics[0] = {}
		"team_b":
			team_b = _team_step(team_b, d, team_a, n)
			_tactics[1] = {}
		"form0", "form1":
			var sd := 0 if key == "form0" else 1
			var names := GameModes.formations(GameModes.ALL[mode_i]).keys()
			var cur := names.find(String(_tactics[sd].get("formation", names[0])))
			_tactics[sd]["formation"] = names[posmod(cur + d, names.size())]
			_tactics[sd]["lineup"] = []
			_ensure_tactics(sd)
			_show_roster(sd)
			return
		"ment0", "ment1":
			var sd2 := 0 if key == "ment0" else 1
			_tactics[sd2]["mentality"] = posmod(int(_tactics[sd2].get("mentality", 1)) + d, 3)
		"press0", "press1":
			var sd3 := 0 if key == "press0" else 1
			_tactics[sd3]["press"] = posmod(int(_tactics[sd3].get("press", 1)) + d, 3)
		"p1":
			p1_dev = posmod(p1_dev + d, _devices.size())
		"p2":
			p2_dev = posmod(p2_dev + d, _p2_options().size())
		"minutes":
			minutes_i = posmod(minutes_i + d, minutes_opts.size())
			Settings.half_minutes = minutes_opts[minutes_i]
		"weather":
			weather_i = posmod(weather_i + d, WEATHERS.size())
		"tod":
			tod_i = posmod(tod_i + d, TIMES.size())
		"dyn":
			dyn_weather = not dyn_weather
		"venue":
			venue_i = posmod(venue_i + 1 + d, _venues().size() + 1) - 1
		"kit_a":
			kit_a = posmod(kit_a + d, 3)
		"kit_b":
			kit_b = posmod(kit_b + 1 + d, 4) - 1
		"skill":
			skill_i = posmod(skill_i + d, skill_opts.size())
			Settings.cpu_skill = skill_opts[skill_i][1]
		"quality":
			Settings.quality = posmod(Settings.quality + d, Settings.QUALITY_NAMES.size())
			Settings.apply()
			FaceLibrary.clear_cache()
		"sound":
			Settings.sfx_volume = snappedf(posmod(roundi(Settings.sfx_volume * 10.0) + d * 2, 12) / 10.0, 0.1)
			if Settings.sfx_volume > 1.0:
				Settings.sfx_volume = 0.0
		"vibration":
			Settings.vibration = not Settings.vibration
	Settings.save_settings()
	_refresh()


## Próximo time dentro da mesma liga (evita repetir o adversário).
func _team_step(cur: int, d: int, other: int, n: int) -> int:
	var lgs := Teams.leagues()
	if lgs.is_empty():
		var v := posmod(cur + d, n)
		return posmod(v + d, n) if v == other else v
	var lg: Dictionary = lgs[Teams.league_of(cur)]
	var first := int(lg["first"])
	var cnt := int(lg["count"])
	var v := first + posmod(cur - first + d, cnt)
	if v == other:
		v = first + posmod(v - first + d, cnt)
	return v


## Próxima liga (só as do esporte do modo escolhido).
func _league_step(cur: int, d: int, other: int) -> int:
	var lgs := _leagues_for_mode()
	if lgs.is_empty():
		return cur
	var li := 0
	for i in lgs.size():
		var lg: Dictionary = lgs[i]
		if cur >= int(lg["first"]) and cur < int(lg["first"]) + int(lg["count"]):
			li = i
	li = posmod(li + d, lgs.size())
	var v := int(lgs[li]["first"])
	if v == other:
		v += 1
	return v


func _set_label(key: String, text: String) -> void:
	if _labels.has(key):
		(_labels[key] as Button).text = text


func _refresh() -> void:
	var ta := Teams.get_team(team_a)
	var tb := Teams.get_team(team_b)
	var p2: String = _p2_options()[clampi(p2_dev, 0, _p2_options().size() - 1)]
	_set_label("mode", GameModes.display_name(GameModes.ALL[mode_i]))
	_set_label("league_a", "Liga: " + String(ta.get("league", "—")))
	_set_label("league_b", "Liga: " + String(tb.get("league", "—")))
	_set_label("team_a", String(ta["name"]))
	_set_label("team_b", String(tb["name"]))
	_set_label("p1", InputRouter.device_label(_devices[p1_dev]))
	if _labels.has("_help"):
		(_labels["_help"] as Label).text = ControlsHelp.text(_devices[p1_dev])
	_set_label("p2", InputRouter.device_label(p2))
	_set_label("minutes", "%d min cada tempo" % int(minutes_opts[minutes_i]))
	_set_label("weather", WEATHERS[weather_i][1])
	_set_label("tod", TIMES[tod_i][1])
	_set_label("venue", "Aleatório" if venue_i < 0 else String(_venues()[venue_i][0]))
	for sd in 2:
		if not _tactics[sd].is_empty():
			_set_label("form%d" % sd, "Formação: " + String(_tactics[sd].get("formation", "")))
			_set_label("ment%d" % sd, "Mentalidade: " + MENTALITIES[int(_tactics[sd].get("mentality", 1))])
			_set_label("press%d" % sd, "Pressão: " + PRESSES[int(_tactics[sd].get("press", 1))])
	_set_label("kit_a", "Uniforme: " + Teams.KIT_NAMES[kit_a])
	_set_label("kit_b", "Uniforme: " + ("Automático" if kit_b < 0 else Teams.KIT_NAMES[kit_b]))
	_set_label("dyn", "Sim — o tempo pode mudar durante o jogo" if dyn_weather else "Não")
	_set_label("skill", skill_opts[skill_i][0])
	var q: String = Settings.QUALITY_NAMES[Settings.quality]
	if Settings.quality == Settings.Quality.AUTO:
		q += "  (%s)" % Settings.QUALITY_NAMES[Settings.effective_quality]
	_set_label("quality", q)
	_set_label("sound", "desligado" if Settings.sfx_volume <= 0.01 else "%d%%" % roundi(Settings.sfx_volume * 100.0))
	_set_label("vibration", "ligada" if Settings.vibration else "desligada")
	if page == Page.TEAMS:
		for side in 2:
			var w: Dictionary = _side_widgets[side]
			if w.is_empty():
				continue
			var t := ta if side == 0 else tb
			(w["crest"] as Crest).setup_team(t, Teams.crest_texture(team_a if side == 0 else team_b))
			var li := Teams.league_of(team_a if side == 0 else team_b)
			var lg_d: Dictionary = Teams.leagues()[li] if not Teams.leagues().is_empty() else {}
			(w["licon"] as Crest).setup_league(String(lg_d.get("name", "")), String(lg_d.get("id", "")).to_upper(), li * 7919 + 13)
			var st: Dictionary = t.get("stadium", {})
			(w["info"] as Label).text = "%s · %s lugares" % [st.get("name", "Estádio"), _thousands(int(st.get("cap", 0)))] if not st.is_empty() else ""
			var kit: KitBadge = w["kit"]
			var chosen: Array = Teams.pick_kits(ta, tb, kit_a, kit_b)
			var kd: Dictionary = chosen[side]
			kit.c1 = kd["c1"]
			kit.c2 = kd["c2"]
			kit.shorts = kd["shorts"]
			kit.pattern = int(kd["pattern"])
			kit.rating = float(t.get("rating", 0.7))
			kit.short_name = String(t.get("short", ""))
			kit.queue_redraw()
			var faces: HBoxContainer = w["faces"]
			for c in faces.get_children():
				c.queue_free()
			var roster: Array = t.get("roster", [])
			var picks := [9, 14, 15] if roster.size() >= 18 else [5, 6, 10]
			for k in picks:
				if k >= roster.size():
					continue
				var e: Dictionary = roster[k]
				var col := VBoxContainer.new()
				var tr := TextureRect.new()
				tr.custom_minimum_size = Vector2(64, 64)
				tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
				tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
				tr.texture = FaceLibrary.texture(int(e.get("fid", 0)), 256)
				col.add_child(tr)
				var nm := Label.new()
				nm.text = "%s\n%d · %s" % [e.get("n", ""), int(e.get("num", 0)), e.get("pos", "")]
				nm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
				nm.add_theme_font_size_override("font_size", 13)
				nm.add_theme_color_override("font_color", Color("#cbd5e1"))
				col.add_child(nm)
				faces.add_child(col)


func _on_play() -> void:
	var p2: String = _p2_options()[p2_dev]
	var p1: String = _devices[p1_dev]
	if p2 == p1:
		p2 = "cpu"
	start_match.emit({
		"mode": GameModes.ALL[mode_i], "team_a": team_a, "team_b": team_b,
		"devices": [p1, p2], "seed": int(Time.get_unix_time_from_system()) & 0xFFFFFF,
		"half_minutes": minutes_opts[minutes_i],
		"weather": _weather_pick(), "tod": TIMES[tod_i][0], "dynamic_weather": dyn_weather,
		"kit_a": kit_a, "kit_b": kit_b, "venue": _venue_pick(), "tactics": _tactics_cfg(),
	})


func _tactics_cfg() -> Array:
	for sd in 2:
		_ensure_tactics(sd)
	return [_tactics[0].duplicate(true), _tactics[1].duplicate(true)]


func _venues() -> Array:
	return MatchView3D.FUTSAL_VENUES if GameModes.ALL[mode_i] == GameModes.FUTSAL else MatchView3D.STREET_VENUES


func _venue_pick() -> int:
	return venue_i if venue_i >= 0 else randi() % 8


## "Aleatório" sorteia um clima (é sorteado aqui, no menu, para valer igual nos dois lados do online).
func _weather_pick() -> String:
	var w: String = WEATHERS[weather_i][0]
	if w == "aleatorio":
		var opts := ["sol", "sol", "nublado", "chuva", "tempestade", "neve"]
		w = opts[randi() % opts.size()]
	return w


func _thousands(n: int) -> String:
	var s := str(n)
	var out := ""
	while s.length() > 3:
		out = "." + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return s + out


# ------------------------------------------------------------------ elenco (atributos e habilidades)

const ATTR_NAMES := ["VEL", "ACE", "FIN", "PAS", "DRI", "DEF"]


func _show_roster(side: int) -> void:
	for k in ["form0", "form1", "ment0", "ment1", "press0", "press1"]:
		_labels.erase(k)
	if _roster_overlay != null:
		_roster_overlay.queue_free()
	var t := Teams.get_team(team_a if side == 0 else team_b)
	var dim := ColorRect.new()
	dim.color = Color(0, 0, 0, 0.7)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	_content.add_child(dim)
	_roster_overlay = dim
	var panel := PanelContainer.new()
	panel.set_anchors_preset(Control.PRESET_FULL_RECT)
	panel.offset_left = 60
	panel.offset_right = -60
	panel.offset_top = 40
	panel.offset_bottom = -40
	dim.add_child(panel)
	var vb := VBoxContainer.new()
	vb.add_theme_constant_override("separation", 8)
	panel.add_child(vb)
	var head := HBoxContainer.new()
	head.add_theme_constant_override("separation", 16)
	vb.add_child(head)
	var crest := Crest.new()
	crest.custom_minimum_size = Vector2(70, 70)
	crest.setup_team(t, Teams.crest_texture(team_a if side == 0 else team_b))
	head.add_child(crest)
	var title := Label.new()
	title.text = "%s — táticas e elenco" % t["name"]
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title.add_theme_font_size_override("font_size", 34)
	title.add_theme_color_override("font_color", Color("#facc15"))
	head.add_child(title)
	var close := Button.new()
	close.text = "Fechar"
	close.custom_minimum_size = Vector2(160, 56)
	close.pressed.connect(_close_roster)
	head.add_child(close)
	_roster_side = side
	_ensure_tactics(side)
	var tac: Dictionary = _tactics[side]
	var trow := HBoxContainer.new()
	trow.add_theme_constant_override("separation", 10)
	vb.add_child(trow)
	for k in ["form", "ment", "press"]:
		var box := VBoxContainer.new()
		box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		trow.add_child(box)
		_cycler(box, "%s%d" % [k, side], "")
		(_labels["%s%d" % [k, side]] as Button).add_theme_font_size_override("font_size", 20)
	var legend := _small(vb, "Toque em dois jogadores para trocá-los (titular ↔ reserva ou de posição) · VEL velocidade · ACE aceleração · FIN finalização · PAS passe · DRI drible · DEF defesa", Color("#64748b"), 15)
	legend.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	vb.add_child(scroll)
	var list := VBoxContainer.new()
	list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	list.add_theme_constant_override("separation", 6)
	scroll.add_child(list)
	var roster: Array = t.get("roster", [])
	var lineup: Array = tac.get("lineup", [])
	var forms := GameModes.formations(GameModes.ALL[mode_i])
	var form: Array = forms.get(String(tac.get("formation", "")), [])
	var has_gk := GameModes.ALL[mode_i] != GameModes.STREET
	# titulares primeiro (na ordem da formação), depois reservas
	var order: Array = []
	for slot in lineup.size():
		order.append([int(lineup[slot]), slot])
	for i in roster.size():
		if not lineup.has(i):
			order.append([i, -1])
	var sep_done := false
	for it in order:
		var ri: int = it[0]
		var slot: int = it[1]
		if slot < 0 and not sep_done:
			sep_done = true
			var sl := _small(list, "RESERVAS", Color("#94a3b8"), 18)
			sl.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
		var role := GameModes.role_for(form[slot], slot, has_gk) if slot >= 0 and slot < form.size() else ""
		list.add_child(_player_row(roster[ri], ri, role))
	close.call_deferred("grab_focus")


## Formação/mentalidade/pressão/titulares padrão do lado (escalação automática).
func _ensure_tactics(side: int) -> void:
	var tac: Dictionary = _tactics[side]
	var forms := GameModes.formations(GameModes.ALL[mode_i])
	if not forms.has(String(tac.get("formation", ""))):
		tac["formation"] = forms.keys()[0]
		tac["lineup"] = []
	if not tac.has("mentality"):
		tac["mentality"] = 1
	if not tac.has("press"):
		tac["press"] = 1
	var form: Array = forms[tac["formation"]]
	var lineup: Array = tac.get("lineup", [])
	if lineup.size() != form.size():
		var t := Teams.get_team(team_a if side == 0 else team_b)
		var roster: Array = t.get("roster", [])
		var used := {}
		lineup = []
		var has_gk := GameModes.ALL[mode_i] != GameModes.STREET
		for slot in form.size():
			MatchSim._pick_from_roster(roster, GameModes.role_for(form[slot], slot, has_gk), used, not has_gk)
			var picked := -1
			for k in used:
				if not lineup.has(int(k)):
					picked = int(k)
			lineup.append(picked)
		tac["lineup"] = lineup
	_tactics[side] = tac


func _on_player_pick(ri: int) -> void:
	if _swap_sel < 0:
		_swap_sel = ri
		_show_roster(_roster_side)
		return
	var tac: Dictionary = _tactics[_roster_side]
	var lineup: Array = tac["lineup"]
	var a := lineup.find(_swap_sel)
	var b := lineup.find(ri)
	if a >= 0 and b >= 0:
		lineup[a] = ri
		lineup[b] = _swap_sel
	elif a >= 0:
		lineup[a] = ri
	elif b >= 0:
		lineup[b] = _swap_sel
	_swap_sel = -1
	_show_roster(_roster_side)


func _close_roster() -> void:
	for k in ["form0", "form1", "ment0", "ment1", "press0", "press1"]:
		_labels.erase(k)
	if _roster_overlay != null:
		_roster_overlay.queue_free()
		_roster_overlay = null


func _player_row(e: Dictionary, ri: int = -1, role: String = "") -> Control:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 12)
	var pick := Button.new()
	pick.text = (role if role != "" else "RES") + ("  ✓" if ri == _swap_sel else "")
	pick.custom_minimum_size = Vector2(110, 56)
	pick.add_theme_font_size_override("font_size", 18)
	if role != "":
		pick.add_theme_color_override("font_color", Color("#4ade80"))
	if ri == _swap_sel:
		pick.add_theme_color_override("font_color", Color("#facc15"))
	pick.pressed.connect(_on_player_pick.bind(ri))
	row.add_child(pick)
	var face := TextureRect.new()
	face.custom_minimum_size = Vector2(56, 56)
	face.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	face.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	face.texture = FaceLibrary.texture(int(e.get("fid", 0)), 128)
	row.add_child(face)
	var ovr := Label.new()
	ovr.text = str(int(e.get("ovr", 70)))
	ovr.custom_minimum_size = Vector2(56, 0)
	ovr.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ovr.add_theme_font_size_override("font_size", 30)
	var o := int(e.get("ovr", 70))
	ovr.add_theme_color_override("font_color", Color("#4ade80") if o >= 82 else (Color("#facc15") if o >= 72 else Color("#e2e8f0")))
	row.add_child(ovr)
	var name_l := Label.new()
	name_l.text = "%d  %s\n%s · %s" % [int(e.get("num", 0)), e.get("n", ""), e.get("pos", ""), e.get("nat", "")]
	name_l.custom_minimum_size = Vector2(300, 0)
	name_l.add_theme_font_size_override("font_size", 19)
	row.add_child(name_l)
	var bars := AttrBars.new()
	bars.values = e.get("a", [])
	bars.custom_minimum_size = Vector2(420, 52)
	row.add_child(bars)
	var traits := HFlowContainer.new()
	traits.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	traits.add_theme_constant_override("h_separation", 6)
	for tn in e.get("t", []):
		var chip := Label.new()
		chip.text = " %s " % tn
		chip.add_theme_font_size_override("font_size", 16)
		chip.add_theme_color_override("font_color", Color("#0f172a"))
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color("#facc15")
		sb.set_corner_radius_all(8)
		sb.content_margin_left = 6
		sb.content_margin_right = 6
		chip.add_theme_stylebox_override("normal", sb)
		traits.add_child(chip)
	row.add_child(traits)
	return row


## Barras dos 6 atributos (0-99).
class AttrBars:
	extends Control
	var values: Array = []

	func _draw() -> void:
		var f := ThemeDB.fallback_font
		var n := 6
		var w := size.x / n
		for i in n:
			var v := float(values[i]) if i < values.size() else 0.0
			var x := i * w
			draw_string(f, Vector2(x, 16), "%s %d" % [MainMenu.ATTR_NAMES[i], roundi(v * 99.0)], HORIZONTAL_ALIGNMENT_LEFT, w - 6, 15, Color("#cbd5e1"))
			draw_rect(Rect2(Vector2(x, 26), Vector2(w - 10, 10)), Color(1, 1, 1, 0.1))
			var col := Color("#ef4444").lerp(Color("#4ade80"), clampf((v - 0.45) / 0.45, 0.0, 1.0))
			draw_rect(Rect2(Vector2(x, 26), Vector2((w - 10) * clampf(v, 0.0, 1.0), 10)), col)


# ------------------------------------------------------------------ online

func _build_online(parent: VBoxContainer) -> void:
	var title := _small(parent, "ONLINE (mesma rede ou IP direto · porta %d UDP)" % EnetSession.PORT, Color("#38bdf8"), 20)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var hb := HBoxContainer.new()
	hb.add_theme_constant_override("separation", 10)
	parent.add_child(hb)
	var back := Button.new()
	back.text = "◀  Voltar"
	back.custom_minimum_size = Vector2(180, 64)
	back.pressed.connect(_online_back)
	hb.add_child(back)
	var host := Button.new()
	host.text = "Criar sala"
	host.custom_minimum_size = Vector2(200, 64)
	host.pressed.connect(_on_host)
	hb.add_child(host)
	_ip_edit = LineEdit.new()
	_ip_edit.placeholder_text = "IP do anfitrião (ex.: 192.168.0.10)"
	_ip_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_ip_edit.text_submitted.connect(func(_t): _on_join())
	hb.add_child(_ip_edit)
	var join := Button.new()
	join.text = "Entrar"
	join.custom_minimum_size = Vector2(160, 64)
	join.pressed.connect(_on_join)
	hb.add_child(join)
	_net_status = _small(parent, "Quem cria a sala escolhe o modo e os times.", Color("#94a3b8"), 20)


func _export_option() -> void:
	var path := Teams.export_option_template()
	(_labels["_of"] as Label).text = "Modelo salvo em:\n%s\nEdite e salve como option_file.json na mesma pasta." % path


func _reload_option() -> void:
	Teams.reload()
	show_page(Page.OPTIONS)


func _online_back() -> void:
	_reset_session()
	show_page(Page.SETUP)


func _update_net_status() -> void:
	var txt := _session.status
	if _session.is_host and not _session.connected:
		var ips := EnetSession.local_ips()
		txt += "\nSeu IP: " + (", ".join(PackedStringArray(ips)) if not ips.is_empty() else "(sem rede local)")
	_net_status.text = txt


func _reset_session() -> void:
	if _session != null:
		_session.close()
	_session = null


func _on_host() -> void:
	_reset_session()
	_session = EnetSession.new()
	if _session.host() != OK:
		_net_status.text = _session.status
		_session = null
		return
	_session.peer_connected.connect(_host_send_config)
	_session.ready_received.connect(_host_start)
	_update_net_status()


func _on_join() -> void:
	var ip := _ip_edit.text.strip_edges()
	if ip.is_empty():
		_net_status.text = "Digite o IP de quem criou a sala"
		return
	_reset_session()
	_session = EnetSession.new()
	if _session.join(ip) != OK:
		_net_status.text = _session.status
		_session = null
		return
	_session.config_received.connect(_client_got_config)
	_update_net_status()


func _net_cfg() -> Dictionary:
	return {
		"mode": GameModes.ALL[mode_i], "team_a": team_a, "team_b": team_b,
		"seed": int(Time.get_unix_time_from_system()) & 0xFFFFFF, "half_minutes": minutes_opts[minutes_i],
		"weather": _weather_pick(), "tod": TIMES[tod_i][0], "dynamic_weather": dyn_weather,
		"venue": _venue_pick(), "kit_a": kit_a, "kit_b": kit_b, "tactics": _tactics_cfg(),
	}


var _host_cfg: Dictionary = {}


func _host_send_config() -> void:
	_host_cfg = _net_cfg()
	_session.send_config(_host_cfg)


func _host_start() -> void:
	var cfg := _host_cfg.duplicate()
	cfg["devices"] = [_devices[p1_dev], "remote"]
	cfg["session"] = _session
	_session = null   # a partida assume a sessão
	start_match.emit(cfg)


func _client_got_config(c: Dictionary) -> void:
	_session.send_ready()
	var cfg := {
		"mode": String(c["mode"]), "team_a": int(c["team_a"]), "team_b": int(c["team_b"]),
		"seed": int(c["seed"]), "half_minutes": float(c["half_minutes"]),
		"weather": String(c.get("weather", "sol")), "tod": String(c.get("tod", "noite")), "dynamic_weather": bool(c.get("dynamic_weather", false)),
		"venue": int(c.get("venue", 0)), "kit_a": int(c.get("kit_a", 0)), "kit_b": int(c.get("kit_b", -1)),
		"tactics": c.get("tactics", []),
		"devices": ["remote", _devices[p1_dev]], "session": _session,
	}
	_session = null
	start_match.emit(cfg)


func _exit_tree() -> void:
	_reset_session()
