## Desenha a partida (vista 3/4 "2.5D"): o chão é visto de cima e a altura (z)
## sobe na tela. Tudo vetorial via _draw → nítido em qualquer resolução e leve.
class_name MatchView
extends Node2D

const PX := 20.0           # pixels por metro (coordenada de mundo)
const H := 0.8             # quanto 1 m de altura sobe na tela (em metros de chão)

var sim: MatchSim
var cam: Camera2D
var team_colors: Array = [[Color.RED, Color.WHITE], [Color.BLUE, Color.WHITE]]
var face_meta: Dictionary = {}
var human_team: Array[bool] = [true, true]
var _font: Font
var _crowd: PackedVector2Array = PackedVector2Array()
var _crowd_col: PackedColorArray = PackedColorArray()
var _trail: Array[Vector2] = []
var _shake: float = 0.0


func setup(s: MatchSim, colors: Array) -> void:
	sim = s
	team_colors = colors
	_font = ThemeDB.fallback_font
	for p in sim.players:
		face_meta[p.id] = FaceLibrary.meta(p.face_id)
	cam = Camera2D.new()
	cam.position_smoothing_enabled = true
	cam.position_smoothing_speed = 5.0
	add_child(cam)
	cam.make_current()
	_build_crowd()
	_update_camera(true)


func shake(amount: float) -> void:
	_shake = maxf(_shake, amount)


func _process(delta: float) -> void:
	if sim == null:
		return
	_update_camera(false)
	if _shake > 0.0:
		cam.offset = Vector2(randf_range(-1, 1), randf_range(-1, 1)) * _shake
		_shake = maxf(0.0, _shake - delta * 20.0)
	else:
		cam.offset = Vector2.ZERO
	if Settings.detail["trail"] and sim.ball.speed() > 14.0 and sim.ball.owner < 0:
		_trail.push_front(_ball_screen())
		if _trail.size() > 8:
			_trail.pop_back()
	elif not _trail.is_empty():
		_trail.pop_back()
	queue_redraw()


func _update_camera(snap: bool) -> void:
	var vp := get_viewport_rect().size
	var view_w: float = sim.mode["view_width"]
	var z := vp.x / (view_w * PX)
	# em telas "altas" (celular em pé) não deixa o campo pequeno demais
	z = maxf(z, vp.y / (view_w * 0.62 * PX))
	cam.zoom = Vector2(z, z)
	var look := sim.ball.pos + sim.ball.vel * 0.35
	var target := Vector2(look.x, look.y - sim.ball.z * H) * PX
	var hl := (sim.half_len + 4.0) * PX
	var hw := (sim.half_w + 4.0) * PX
	var half_view := vp / (2.0 * z)
	target.x = clampf(target.x, -hl + half_view.x, hl - half_view.x) if half_view.x < hl else 0.0
	target.y = clampf(target.y, -hw - 2.0 * PX + half_view.y, hw - half_view.y) if half_view.y < hw else 0.0
	cam.position = target
	if snap:
		cam.reset_smoothing()


func _w(v: Vector2, z: float = 0.0) -> Vector2:
	return Vector2(v.x, v.y - z * H) * PX


func _ball_screen() -> Vector2:
	return _w(sim.ball.pos, sim.ball.z)


# ------------------------------------------------------------------ desenho

func _draw() -> void:
	if sim == null:
		return
	_draw_ground()
	_draw_lines()
	_draw_goal_back(-1.0)
	_draw_goal_back(1.0)
	# ordena entidades por y (quem está mais "embaixo" na tela fica na frente)
	var items: Array = []
	for p in sim.players:
		items.append([p.pos.y, p])
	items.append([sim.ball.pos.y + 0.01, null])
	items.sort_custom(func(a, b): return a[0] < b[0])
	if Settings.detail["shadows"]:
		for p in sim.players:
			draw_set_transform(_w(p.pos), 0.0, Vector2(1.0, 0.45))
			draw_circle(Vector2.ZERO, 0.42 * PX, Color(0, 0, 0, 0.28))
		draw_set_transform(Vector2.ZERO)
	for it in items:
		if it[1] == null:
			_draw_ball()
		else:
			_draw_player(it[1])
	_draw_goal_front(-1.0)
	_draw_goal_front(1.0)
	_draw_markers()


func _draw_ground() -> void:
	var hl := sim.half_len
	var hw := sim.half_w
	var surface: String = sim.mode["surface"]
	var outer := Rect2(Vector2(-hl - 14, -hw - 14) * PX, Vector2(hl * 2 + 28, hw * 2 + 28) * PX)
	match surface:
		"grass":
			draw_rect(outer, Color("#2c6e2f"))
			draw_rect(Rect2(Vector2(-hl - 4, -hw - 4) * PX, Vector2(hl * 2 + 8, hw * 2 + 8) * PX), Color("#3b8a3e"))
			if Settings.detail["stripes"]:
				var n := 20
				var sw := (hl * 2.0) / n
				for i in n:
					if i % 2 == 0:
						draw_rect(Rect2(Vector2(-hl + i * sw, -hw) * PX, Vector2(sw, hw * 2) * PX), Color("#43964a"))
			if Settings.detail["crowd"]:
				draw_rect(Rect2(Vector2(-hl - 14, -hw - 14) * PX, Vector2(hl * 2 + 28, 9) * PX), Color("#1f2937"))
				draw_rect(Rect2(Vector2(-hl - 14, hw + 5) * PX, Vector2(hl * 2 + 28, 9) * PX), Color("#1f2937"))
				for i in _crowd.size():
					draw_circle(_crowd[i], 0.32 * PX, _crowd_col[i])
		"wood":
			draw_rect(outer, Color("#334155"))
			draw_rect(Rect2(Vector2(-hl - 1.5, -hw - 1.5) * PX, Vector2(hl * 2 + 3, hw * 2 + 3) * PX), Color("#c08a4f"))
			if Settings.detail["stripes"]:
				var plank := 0.9
				var rows := int(hw * 2.0 / plank)
				for i in rows:
					if i % 2 == 0:
						draw_rect(Rect2(Vector2(-hl, -hw + i * plank) * PX, Vector2(hl * 2, plank) * PX), Color("#b98048"))
			draw_rect(Rect2(Vector2(-hl, -hw) * PX, Vector2(hl * 2, hw * 2) * PX), Color(0.1, 0.35, 0.75, 0.10))
		_:
			draw_rect(outer, Color("#3f3f46"))
			draw_rect(Rect2(Vector2(-hl, -hw) * PX, Vector2(hl * 2, hw * 2) * PX), Color("#52525b"))
			if Settings.detail["stripes"]:
				# manchas e grafites simples no asfalto
				draw_circle(Vector2(-hl * 0.5, hw * 0.3) * PX, 3.5 * PX, Color(0.3, 0.3, 0.33, 0.6))
				draw_circle(Vector2(hl * 0.4, -hw * 0.4) * PX, 2.5 * PX, Color(0.3, 0.3, 0.33, 0.6))
				draw_string(_font, Vector2(-hl * 0.35, -hw - 1.2) * PX, "CRAQUEZ STREET", HORIZONTAL_ALIGNMENT_LEFT, -1, int(1.4 * PX), Color("#facc15"))
			# muros (tabela)
			var wall := Color("#a1a1aa")
			var t := 0.5 * PX
			var gh: float = sim.goal_half
			draw_rect(Rect2(Vector2(-hl - 0.5, -hw - 0.5 - 1.0 * H) * PX, Vector2(hl * 2 + 1, 0.5 + 1.0 * H) * PX), Color("#71717a"))
			draw_line(Vector2(-hl, hw) * PX, Vector2(hl, hw) * PX, wall, t)
			for s: float in [-1.0, 1.0]:
				draw_line(Vector2(s * hl, -hw) * PX, Vector2(s * hl, -gh) * PX, wall, t)
				draw_line(Vector2(s * hl, gh) * PX, Vector2(s * hl, hw) * PX, wall, t)


func _draw_lines() -> void:
	var hl := sim.half_len
	var hw := sim.half_w
	var lw := 0.12 * PX
	var c := Color(1, 1, 1, 0.92)
	if sim.mode["surface"] == "asphalt":
		c = Color(1, 1, 1, 0.75)
	draw_rect(Rect2(Vector2(-hl, -hw) * PX, Vector2(hl * 2, hw * 2) * PX), c, false, lw)
	draw_line(Vector2(0, -hw) * PX, Vector2(0, hw) * PX, c, lw)
	var cr: float = sim.mode["center_r"]
	draw_arc(Vector2.ZERO, cr * PX, 0, TAU, 64, c, lw)
	draw_circle(Vector2.ZERO, 0.18 * PX, c)
	var bd: float = sim.mode["box_depth"]
	var bh: float = sim.mode["box_half"]
	for s: float in [-1.0, 1.0]:
		if bd <= 0.0:
			draw_arc(Vector2(s * hl, 0) * PX, 3.0 * PX, PI / 2 if s > 0 else -PI / 2, PI * 1.5 if s > 0 else PI / 2, 24, c, lw)
			continue
		if sim.mode["id"] == GameModes.FUTSAL:
			# área do futsal: dois quartos de círculo de 6 m ligados por reta
			var gy := sim.goal_half * 0.55
			var a0 := PI / 2 if s > 0 else -PI / 2
			draw_arc(Vector2(s * hl, -gy) * PX, bd * PX, a0, a0 + PI / 2 * s, 20, c, lw)
			draw_arc(Vector2(s * hl, gy) * PX, bd * PX, a0 - PI / 2 * s, a0, 20, c, lw)
			draw_line(Vector2(s * (hl - bd), -gy) * PX, Vector2(s * (hl - bd), gy) * PX, c, lw)
			draw_circle(Vector2(s * (hl - 6.0), 0) * PX, 0.12 * PX, c)
			draw_circle(Vector2(s * (hl - 10.0), 0) * PX, 0.12 * PX, c)
		else:
			var x0 := s * hl
			draw_rect(Rect2(Vector2(minf(x0, x0 - s * bd), -bh) * PX, Vector2(bd, bh * 2) * PX), c, false, lw)
			draw_rect(Rect2(Vector2(minf(x0, x0 - s * 5.5), -9.16) * PX, Vector2(5.5, 18.32) * PX), c, false, lw)
			draw_circle(Vector2(s * (hl - 11.0), 0) * PX, 0.15 * PX, c)
			var mid := PI if s > 0 else 0.0
			draw_arc(Vector2(s * (hl - 11.0), 0) * PX, 9.15 * PX, mid - 0.93, mid + 0.93, 24, c, lw)
			for cy: float in [-hw, hw]:
				draw_arc(Vector2(s * hl, cy) * PX, 1.0 * PX, 0, TAU, 16, c, lw)


func _draw_goal_back(side: float) -> void:
	var gx := side * sim.half_len
	var gh := sim.goal_half
	var depth: float = sim.mode["goal_depth"]
	var h := sim.goal_h
	var net := Color(1, 1, 1, 0.35)
	var back := gx + side * depth
	# fundo e chão da rede
	var pts := PackedVector2Array([_w(Vector2(gx, -gh), h), _w(Vector2(back, -gh), h * 0.8),
		_w(Vector2(back, gh), h * 0.8), _w(Vector2(gx, gh), h), _w(Vector2(gx, gh)), _w(Vector2(back, gh)),
		_w(Vector2(back, -gh)), _w(Vector2(gx, -gh))])
	draw_colored_polygon(PackedVector2Array([_w(Vector2(gx, -gh)), _w(Vector2(back, -gh)), _w(Vector2(back, gh)), _w(Vector2(gx, gh))]), Color(0, 0, 0, 0.15))
	if Settings.detail["net"]:
		var steps := int(gh * 2.0 / 0.4)
		for i in steps + 1:
			var y := -gh + i * 0.4
			draw_line(_w(Vector2(back, y)), _w(Vector2(back, y), h * 0.8), net, 1.0)
		for k in 6:
			var z := h * 0.8 * k / 5.0
			draw_line(_w(Vector2(back, -gh), z), _w(Vector2(back, gh), z), net, 1.0)
	draw_polyline(pts, net, 1.5)
	# trave de cima ("lado de lá")
	draw_line(_w(Vector2(gx, -gh)), _w(Vector2(gx, -gh), h), Color.WHITE, 0.14 * PX)


func _draw_goal_front(side: float) -> void:
	var gx := side * sim.half_len
	var gh := sim.goal_half
	var h := sim.goal_h
	draw_line(_w(Vector2(gx, gh)), _w(Vector2(gx, gh), h), Color.WHITE, 0.14 * PX)
	draw_line(_w(Vector2(gx, -gh), h), _w(Vector2(gx, gh), h), Color.WHITE, 0.14 * PX)


func _draw_player(p: SimPlayer) -> void:
	var cols: Array = team_colors[p.team]
	var c1: Color = cols[0]
	var c2: Color = cols[1]
	if p.is_keeper:
		c1 = Color("#22c55e") if p.team == 0 else Color("#f59e0b")
		c2 = Color("#111827")
	var m: Dictionary = face_meta.get(p.id, {})
	var skin := Color(String(m.get("skin_hex", "#c98e62")))
	var hair := Color(String(m.get("hair_hex", "#1a1210")))
	var detail: int = Settings.detail["player_detail"]
	var base := _w(p.pos)
	var flip := -1.0 if p.facing.x < 0 else 1.0
	var u := PX  # 1 metro

	if p.slide > 0.0 or (p.stun > 0.3 and p.tackle_anim <= 0.0):
		# deitado (carrinho / caído)
		var ang := p.facing.angle()
		draw_set_transform(base + Vector2(0, -0.25 * u), ang, Vector2.ONE)
		draw_rect(Rect2(Vector2(-0.9, -0.22) * u, Vector2(1.2, 0.44) * u), c1)
		draw_rect(Rect2(Vector2(0.3, -0.18) * u, Vector2(0.6, 0.36) * u), Color("#111827"))
		draw_circle(Vector2(-1.05, 0) * u, 0.2 * u, skin)
		draw_set_transform(Vector2.ZERO)
		return

	var spd := p.vel.length()
	var swing := sin(p.run_phase) * clampf(spd / 7.0, 0.0, 1.0)
	var lean := clampf(p.vel.x / 9.0, -1.0, 1.0) * 0.12 * u
	var hip := base + Vector2(0, -0.85 * H * u)
	var neck := hip + Vector2(lean, -0.62 * H * u)
	var head := neck + Vector2(lean * 0.4, -0.2 * u)
	# pernas
	var leg_c := skin.darkened(0.1)
	var sock := c2
	for s: float in [-1.0, 1.0]:
		var foot := base + Vector2(flip * swing * s * 0.32 * u + s * 0.09 * u, 0)
		if p.kick_anim > 0.0 and s > 0:
			foot = base + Vector2(p.facing.x * 0.55, p.facing.y * 0.3 - 0.25) * u
		draw_line(hip + Vector2(s * 0.1 * u, 0), foot + Vector2(0, -0.12 * u), leg_c, 0.16 * u)
		if detail > 0:
			draw_line(foot + Vector2(0, -0.35 * u), foot + Vector2(0, -0.1 * u), sock, 0.17 * u)
		draw_line(foot + Vector2(-0.04 * u, -0.04 * u), foot + Vector2(flip * 0.14 * u, -0.04 * u), Color("#0f0f0f"), 0.12 * u)
	# calção
	draw_rect(Rect2(hip + Vector2(-0.2 * u, -0.08 * u), Vector2(0.4, 0.26) * u), c2.darkened(0.15) if c2.get_luminance() > 0.8 else c2)
	# tronco
	var torso := PackedVector2Array([hip + Vector2(-0.21 * u, 0), hip + Vector2(0.21 * u, 0),
		neck + Vector2(0.25 * u, 0), neck + Vector2(-0.25 * u, 0)])
	draw_colored_polygon(torso, c1)
	if detail > 1:
		draw_line(neck + Vector2(-0.25 * u, 0), hip + Vector2(-0.21 * u, 0), c1.darkened(0.25), 0.05 * u)
	# braços
	for s: float in [-1.0, 1.0]:
		var sh := neck + Vector2(s * 0.25 * u, 0.04 * u)
		var hand := sh + Vector2(s * 0.08 * u - flip * swing * s * 0.22 * u, 0.42 * u)
		if p.is_keeper and p.dive > 0.0:
			hand = sh + Vector2(s * 0.3 * u, -0.35 * u)
		draw_line(sh, hand, c1 if detail > 0 else skin, 0.13 * u)
		draw_circle(hand, 0.07 * u, Color("#f8fafc") if p.is_keeper else skin)
	# número nas costas / peito
	if detail > 0:
		draw_string(_font, (hip + neck) * 0.5 + Vector2(-0.22 * u, 0.12 * u), str(p.number), HORIZONTAL_ALIGNMENT_CENTER, 0.44 * u, int(0.32 * u), c2)
	# cabeça
	draw_circle(head, 0.2 * u, skin)
	var style: String = m.get("hair_style", "short")
	var facing_camera := p.facing.y > 0.3
	if style != "bald":
		if style == "afro":
			draw_circle(head + Vector2(0, -0.08 * u), 0.26 * u, hair)
		elif style == "long" or style == "dreads":
			draw_circle(head + Vector2(0, -0.04 * u), 0.22 * u, hair)
			if not facing_camera:
				draw_rect(Rect2(head + Vector2(-0.2 * u, 0), Vector2(0.4, 0.28) * u), hair)
		else:
			draw_arc(head + Vector2(0, 0.02 * u), 0.17 * u, PI, TAU, 12, hair, 0.12 * u)
	if facing_camera and detail > 0:
		draw_circle(head + Vector2(-0.07 * u, 0.0), 0.03 * u, Color("#111"))
		draw_circle(head + Vector2(0.07 * u, 0.0), 0.03 * u, Color("#111"))
	elif not facing_camera and style != "bald" and style != "buzz" and p.facing.y < -0.3:
		draw_circle(head, 0.18 * u, hair)


func _draw_ball() -> void:
	var b := sim.ball
	var ground := _w(b.pos)
	var scr := _ball_screen()
	var r := b.radius * PX * (1.0 + b.z * 0.06) * 1.3
	# sombra encolhe/esmaece com a altura
	var sh_a := clampf(0.45 - b.z * 0.05, 0.12, 0.45)
	draw_set_transform(ground, 0.0, Vector2(1.0, 0.5))
	draw_circle(Vector2.ZERO, r * (1.0 + b.z * 0.04), Color(0, 0, 0, sh_a))
	draw_set_transform(Vector2.ZERO)
	for i in _trail.size():
		draw_circle(_trail[i], r * (1.0 - i * 0.1), Color(1, 1, 1, 0.18 - i * 0.02))
	draw_circle(scr, r, Color.WHITE)
	# gomos girando (sensação de rotação)
	var a := b.roll_angle
	var dirv := b.vel.normalized() if b.vel.length() > 0.1 else Vector2.RIGHT
	for k in 3:
		var off := dirv.rotated(PI / 2) * cos(a + k * 2.1) * r * 0.55 + dirv * sin(a + k * 2.1) * r * 0.25
		if sin(a + k * 2.1) > -0.3:
			draw_circle(scr + off, r * 0.28, Color("#111827"))
	draw_arc(scr, r, 0, TAU, 16, Color(0, 0, 0, 0.5), 1.0)


func _draw_markers() -> void:
	for t in 2:
		if not human_team[t]:
			continue
		var p: SimPlayer = sim.controlled_player(t)
		var cols: Array = team_colors[t]
		var col: Color = Color("#facc15") if t == 0 else Color("#38bdf8")
		var top := _w(p.pos) + Vector2(0, -2.25 * PX)
		draw_colored_polygon(PackedVector2Array([top + Vector2(-0.35, -0.5) * PX, top + Vector2(0.35, -0.5) * PX, top]), col)
		draw_string(_font, top + Vector2(-0.6, -0.65) * PX, "P%d" % (t + 1), HORIZONTAL_ALIGNMENT_CENTER, 1.2 * PX, int(0.55 * PX), col)
		# anel no chão
		draw_set_transform(_w(p.pos), 0.0, Vector2(1.0, 0.5))
		draw_arc(Vector2.ZERO, 0.7 * PX, 0, TAU, 24, col, 0.08 * PX)
		draw_set_transform(Vector2.ZERO)
		# barra de força
		var ch := sim.charge_of(t)
		if ch > 0.0:
			var bar_pos := _w(p.pos) + Vector2(-0.8, 0.45) * PX
			draw_rect(Rect2(bar_pos, Vector2(1.6, 0.22) * PX), Color(0, 0, 0, 0.6))
			draw_rect(Rect2(bar_pos, Vector2(1.6 * ch, 0.22) * PX), Color("#22c55e").lerp(Color("#ef4444"), ch))
		# stamina (fina) quando cansado
		if p.stamina < 0.6:
			var sp := _w(p.pos) + Vector2(-0.6, 0.75) * PX
			draw_rect(Rect2(sp, Vector2(1.2, 0.1) * PX), Color(0, 0, 0, 0.5))
			draw_rect(Rect2(sp, Vector2(1.2 * p.stamina, 0.1) * PX), Color("#a3e635"))


func _build_crowd() -> void:
	_crowd.clear()
	_crowd_col.clear()
	if sim.mode["surface"] != "grass":
		return
	var rng := RandomNumberGenerator.new()
	rng.seed = 42
	var hl := sim.half_len
	var hw := sim.half_w
	var palette := [Color("#ef4444"), Color("#f8fafc"), Color("#3b82f6"), Color("#facc15"), Color("#22c55e"), Color("#a855f7")]
	var x := -hl - 13.0
	while x < hl + 13.0:
		for row in 7:
			for s: float in [-1.0, 1.0]:
				var y := (hw + 6.0 + row * 1.1) * s if s > 0 else -(hw + 6.0 + row * 1.1)
				if rng.randf() < 0.85:
					_crowd.append(Vector2(x + rng.randf_range(-0.3, 0.3), y) * PX)
					_crowd_col.append(palette[rng.randi() % palette.size()].darkened(rng.randf_range(0.0, 0.4)))
		x += 1.0
