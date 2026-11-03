## Partida em 3D com visual "desenhado": campo com linhas em shader, gols com
## rede, estádio/ginásio/quadra de rua, atletas articulados, câmera de TV.
## Só LÊ a simulação (MatchSim): nada aqui altera o jogo.
class_name MatchView3D
extends Node3D

const LIGHT_DIR := Vector3(-0.35, 0.85, 0.45)
const EYE_COLORS := [Color("#4a2c17"), Color("#2b1a0e"), Color("#3f6e8c"), Color("#5c7a3a"), Color("#6b4423")]
const BOOT_COLORS := [Color("#111318"), Color("#f8fafc"), Color("#16181d"), Color("#facc15"), Color("#ef4444"), Color("#22d3ee"), Color("#111318")]

var sim: MatchSim
var team_colors: Array = [[Color.RED, Color.WHITE], [Color.BLUE, Color.WHITE]]
var human_team: Array[bool] = [true, true]
var cam: Camera3D
var rigs: Array[PlayerRig] = []
var ball_node: Node3D
var ball_mesh: MeshInstance3D
var _ball_shadow: MeshInstance3D
var _shadows: Array = []                 # por atleta: Array[MeshInstance3D] (uma por luz)
var _lights2d: Array[Vector2] = []       # posição (no chão) das fontes de luz que fazem sombra
var _sun_dir: Vector2 = Vector2.ZERO     # luz "infinita" (sol); se != 0 usa uma sombra longa
var _officials: Array = []               # [{rig, p, kind}] árbitro e assistentes (só visual)
var _shadow_mat: ShaderMaterial
var _quad: QuadMesh
var _intro: float = 3.2
var _night: bool = false
var _crowd_mats: Array[ShaderMaterial] = []
var _overlay: MarkerOverlay
var _cam_x: float = 0.0
var _look: Vector3 = Vector3.ZERO
var _shake: float = 0.0
var _excitement: float = 0.2
var _cam_cfg: Dictionary = {}
## >= 0: câmera de perto seguindo esse atleta (replays / screenshots)
var closeup_id: int = -1

const CAMS := {
	"field": {"height": 21.0, "back": 22.0, "fov": 29.0, "clamp": 22.0, "look_z": 0.45},
	"futsal": {"height": 10.5, "back": 10.5, "fov": 36.0, "clamp": 7.0, "look_z": 0.4},
	"street": {"height": 11.0, "back": 9.5, "fov": 38.0, "clamp": 5.0, "look_z": 0.35},
}


func setup(s: MatchSim, colors: Array) -> void:
	sim = s
	team_colors = colors
	var mid: String = sim.mode["id"]
	_cam_cfg = CAMS.get(mid, CAMS["field"])
	_build_environment()
	_build_ground()
	_build_goal(-1.0)
	_build_goal(1.0)
	match String(sim.mode["surface"]):
		"grass":
			_build_stadium()
		"wood":
			_build_gym()
		_:
			_build_street()
	_shadow_mat = ShaderMaterial.new()
	_shadow_mat.shader = preload("res://shaders/blob_shadow.gdshader")
	_shadow_mat.set_shader_parameter("strength", 0.2 if _lights2d.size() > 1 else 0.42)
	_quad = QuadMesh.new()
	_quad.size = Vector2(1.0, 1.0)
	_build_players()
	_build_officials()
	_build_ball()
	cam = Camera3D.new()
	cam.fov = _cam_cfg["fov"]
	cam.near = 0.5
	cam.far = 600.0
	add_child(cam)
	cam.make_current()
	_cam_x = sim.ball.pos.x
	_look = Vector3(sim.ball.pos.x, 0, 0)
	_update_camera(1.0)
	var layer := CanvasLayer.new()
	layer.layer = 1
	add_child(layer)
	_overlay = MarkerOverlay.new()
	_overlay.view = self
	layer.add_child(_overlay)
	if Settings.detail["crowd"]:
		var vig := ColorRect.new()
		var vm := ShaderMaterial.new()
		vm.shader = preload("res://shaders/vignette.gdshader")
		vig.material = vm
		vig.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var vl := CanvasLayer.new()
		vl.layer = 0
		add_child(vl)
		vl.add_child(vig)
		var fit := func() -> void:
			vig.position = Vector2.ZERO
			vig.size = get_viewport().get_visible_rect().size
		fit.call()
		get_viewport().size_changed.connect(fit)


func shake(amount: float) -> void:
	_shake = maxf(_shake, amount * 0.02)


# ------------------------------------------------------------------ construção

func _mesh(mesh: Mesh, mat: Material, pos: Vector3, rot: Vector3 = Vector3.ZERO, scl: Vector3 = Vector3.ONE) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.position = pos
	mi.rotation = rot
	mi.scale = scl
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)
	return mi


func _box(size: Vector3, color: Color, pos: Vector3, outline: bool = true) -> MeshInstance3D:
	var b := BoxMesh.new()
	b.size = size
	return _mesh(b, Toon.mat(color, outline, 0.08), pos)


func _build_environment() -> void:
	var env := Environment.new()
	var surface: String = sim.mode["surface"]
	var hl := sim.half_len
	var hw := sim.half_w
	if surface == "wood":
		env.background_mode = Environment.BG_COLOR
		env.background_color = Color("#070b14")
		# luminárias do ginásio
		for sx: float in [-1.0, 1.0]:
			for sz: float in [-1.0, 1.0]:
				_lights2d.append(Vector2(sx * hl * 0.6, sz * hw * 0.7))
	else:
		env.background_mode = Environment.BG_SKY
		var sky := Sky.new()
		var sm := ProceduralSkyMaterial.new()
		if surface == "grass":
			# jogo noturno
			_night = true
			sm.sky_top_color = Color("#050a1a")
			sm.sky_horizon_color = Color("#1d2d50")
			sm.ground_bottom_color = Color("#05070d")
			sm.ground_horizon_color = Color("#1d2d50")
			for sx: float in [-1.0, 1.0]:
				for sz: float in [-1.0, 1.0]:
					_lights2d.append(Vector2(sx * (hl + 14.0), sz * (hw + 18.0)))
		else:
			# fim de tarde na quadra de rua
			sm.sky_top_color = Color("#3b2a6b")
			sm.sky_horizon_color = Color("#fb923c")
			sm.ground_bottom_color = Color("#1f2937")
			sm.ground_horizon_color = Color("#9a3412")
			_sun_dir = Vector2(0.55, -0.35)
		sky.sky_material = sm
		env.sky = sky
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)


func _glow(pos: Vector3, size: float, color: Color, power: float = 2.2) -> void:
	if not Settings.detail["crowd"]:
		return
	var q := QuadMesh.new()
	q.size = Vector2(size, size)
	var m := ShaderMaterial.new()
	m.shader = preload("res://shaders/glow.gdshader")
	m.set_shader_parameter("color", color)
	m.set_shader_parameter("power", power)
	var mi := _mesh(q, m, pos)
	mi.sorting_offset = 10.0


func _build_ground() -> void:
	var margin := 16.0 if sim.mode["surface"] == "grass" else 8.0
	var pm := PlaneMesh.new()
	pm.size = Vector2((sim.half_len + margin) * 2.0, (sim.half_w + margin) * 2.0)
	var mat := ShaderMaterial.new()
	mat.shader = preload("res://shaders/pitch.gdshader")
	var surface: String = sim.mode["surface"]
	mat.set_shader_parameter("surface", 0 if surface == "grass" else (1 if surface == "wood" else 2))
	mat.set_shader_parameter("hl", sim.half_len)
	mat.set_shader_parameter("hw", sim.half_w)
	mat.set_shader_parameter("box_depth", float(sim.mode["box_depth"]))
	mat.set_shader_parameter("box_half", float(sim.mode["box_half"]))
	mat.set_shader_parameter("center_r", float(sim.mode["center_r"]))
	mat.set_shader_parameter("pen_dist", float(sim.mode.get("penalty_dist", 11.0)))
	mat.set_shader_parameter("goal_half", sim.goal_half)
	var mid: String = sim.mode["id"]
	mat.set_shader_parameter("layout", 0 if mid == GameModes.FIELD else (1 if mid == GameModes.FUTSAL else 2))
	mat.set_shader_parameter("line_w", 0.12 if mid == GameModes.FIELD else 0.08)
	mat.set_shader_parameter("stripes", 1.0 if Settings.detail["stripes"] else 0.0)
	mat.set_shader_parameter("night", 1.0 if _night else 0.0)
	_mesh(pm, mat, Vector3.ZERO)


func _build_goal(side: float) -> void:
	var gx := side * sim.half_len
	var gh := sim.goal_half
	var h := sim.goal_h
	var depth: float = sim.mode["goal_depth"]
	var r := 0.06 if sim.mode["id"] == GameModes.FIELD else 0.045
	var white := Toon.mat(Color("#f8fafc"), true, 0.3, 0.5)
	var post := CylinderMesh.new()
	post.top_radius = r
	post.bottom_radius = r
	post.height = h + r
	post.radial_segments = 12
	for y: float in [-gh - r, gh + r]:
		_mesh(post, white, Vector3(gx + side * r, (h + r) * 0.5, y))
	var bar := CylinderMesh.new()
	bar.top_radius = r
	bar.bottom_radius = r
	bar.height = gh * 2.0 + r * 4.0
	bar.radial_segments = 12
	_mesh(bar, white, Vector3(gx + side * r, h + r * 0.5, 0), Vector3(PI / 2, 0, 0))
	# estrutura de trás (fina)
	var back := gx + side * depth
	var thin := CylinderMesh.new()
	thin.top_radius = 0.025
	thin.bottom_radius = 0.025
	thin.height = gh * 2.0
	var grey := Toon.mat(Color("#cbd5e1"), false)
	_mesh(thin, grey, Vector3(back, 0.03, 0), Vector3(PI / 2, 0, 0))
	_mesh(thin, grey, Vector3(back, h * 0.8, 0), Vector3(PI / 2, 0, 0))
	# redes
	if not Settings.detail["net"]:
		return
	var net_shader := preload("res://shaders/net.gdshader")
	var top_back := Vector3(back, h * 0.8, 0)
	# fundo
	_net_quad(net_shader, Vector3(back, 0, -gh), Vector3(back, 0, gh), Vector3(back, h * 0.8, gh), Vector3(back, h * 0.8, -gh))
	# teto
	_net_quad(net_shader, Vector3(gx, h, -gh), Vector3(gx, h, gh), Vector3(back, h * 0.8, gh), Vector3(back, h * 0.8, -gh))
	# laterais
	for y: float in [-gh, gh]:
		_net_quad(net_shader, Vector3(gx, 0, y), Vector3(back, 0, y), Vector3(back, h * 0.8, y), Vector3(gx, h, y))
	var _unused := top_back


func _net_quad(shader: Shader, a: Vector3, b: Vector3, c: Vector3, d: Vector3) -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var uvs := [Vector2(0, 1), Vector2(1, 1), Vector2(1, 0), Vector2(0, 0)]
	var pts := [a, b, c, d]
	for i: int in [0, 1, 2, 0, 2, 3]:
		st.set_uv(uvs[i])
		st.add_vertex(pts[i])
	var m := ShaderMaterial.new()
	m.shader = shader
	m.set_shader_parameter("size", Vector2(a.distance_to(b), b.distance_to(c)))
	m.set_shader_parameter("cell", 0.14 if sim.mode["id"] == GameModes.FIELD else 0.1)
	_mesh(st.commit(), m, Vector3.ZERO)


func _crowd_mat(cols: Vector2, bias: float = 0.5) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = preload("res://shaders/crowd.gdshader")
	m.set_shader_parameter("seats", cols)
	m.set_shader_parameter("bias", bias)
	m.set_shader_parameter("light", 0.82 if _night else 1.0)
	m.set_shader_parameter("team_a", team_colors[0][0])
	m.set_shader_parameter("team_b", team_colors[1][0])
	_crowd_mats.append(m)
	return m


## Plano inclinado de arquibancada: começa em `start` (pé), sobe para fora.
func _stand(center: Vector3, length: float, depth: float, rise: float, yaw: float, seats_per_m: float, bias: float = 0.5) -> void:
	var pm := PlaneMesh.new()
	var slope := sqrt(depth * depth + rise * rise)
	pm.size = Vector2(length, slope)
	var m := _crowd_mat(Vector2(length * seats_per_m, slope * 1.6), bias) if Settings.detail["crowd"] else null
	var angle := atan2(rise, depth)
	var node := Node3D.new()
	node.position = center
	node.rotation.y = yaw
	add_child(node)
	var mi := MeshInstance3D.new()
	mi.mesh = pm
	# plano sobe em -Z local (para fora do campo)
	mi.rotation.x = angle
	mi.position = Vector3(0, rise * 0.5, -depth * 0.5)
	if m != null:
		mi.material_override = m
	else:
		mi.material_override = Toon.mat(Color("#334155"), false)
	node.add_child(mi)


func _build_stadium() -> void:
	var hl := sim.half_len
	var hw := sim.half_w
	var board_col := Color("#0b1020")
	# placas de LED (estrutura + tela animada no lado de lá)
	for sgn: float in [-1.0, 1.0]:
		_box(Vector3(hl * 2.0 + 10.0, 0.95, 0.25), board_col, Vector3(0, 0.475, sgn * (hw + 4.0)))
		_box(Vector3(0.25, 0.95, hw * 2.0 + 8.0), board_col, Vector3(sgn * (hl + 5.0), 0.475, 0))
	var a: Color = team_colors[0][0]
	var b: Color = team_colors[1][0]
	if Settings.detail["crowd"]:
		var led := QuadMesh.new()
		led.size = Vector2(hl * 2.0 + 9.0, 0.82)
		var lm := ShaderMaterial.new()
		lm.shader = preload("res://shaders/led.gdshader")
		lm.set_shader_parameter("c1", a.darkened(0.2))
		lm.set_shader_parameter("c2", Color("#0b1b4a"))
		_mesh(led, lm, Vector3(0, 0.48, -(hw + 4.0) + 0.135))
		var ads := ["CRAQUEZ", "FUTEBOL DE VERDADE", "CRAQUEZ", "JOGUE LIMPO", "CRAQUEZ", "#CRAQUEZ"]
		for i in 9:
			var l := Label3D.new()
			l.text = ads[i % ads.size()]
			l.font_size = 96
			l.pixel_size = 0.0072
			l.modulate = Color("#f8fafc") if i % 2 == 0 else Color("#facc15")
			l.outline_size = 0
			l.shaded = false
			l.double_sided = false
			l.position = Vector3(-hl + 4.0 + i * (hl * 2.0 - 8.0) / 8.0, 0.48, -(hw + 4.0) + 0.14)
			add_child(l)
	# arquibancadas: lado de lá misto, cada fundo com uma torcida, degrau do lado da câmera
	var base_y := 1.4
	_box(Vector3(hl * 2.0 + 30.0, base_y, 1.0), Color("#111827"), Vector3(0, base_y * 0.5, -(hw + 7.5)), false)
	_stand(Vector3(0, base_y, -(hw + 8.0)), hl * 2.0 + 30.0, 22.0, 15.0, 0.0, 1.6, 0.5)
	_stand(Vector3(0, base_y + 16.5, -(hw + 31.0)), hl * 2.0 + 30.0, 12.0, 9.0, 0.0, 1.6, 0.5)
	_stand(Vector3(-(hl + 9.0), base_y, 0), hw * 2.0 + 16.0, 16.0, 11.0, PI / 2, 1.6, 0.92)
	_stand(Vector3(hl + 9.0, base_y, 0), hw * 2.0 + 16.0, 16.0, 11.0, -PI / 2, 1.6, 0.08)
	_stand(Vector3(0, 0.6, hw + 8.0), hl * 2.0 + 30.0, 6.0, 3.0, PI, 1.6, 0.5)
	# anel entre os dois níveis + coberturas com faixa de luz
	_box(Vector3(hl * 2.0 + 30.0, 1.4, 1.0), Color("#1e293b"), Vector3(0, base_y + 15.6, -(hw + 30.6)), false)
	_box(Vector3(hl * 2.0 + 30.0, 0.35, 0.3), Color("#fde68a"), Vector3(0, base_y + 15.0, -(hw + 30.2)), false)
	var roof := Color("#94a3b8") if _night else Color("#cbd5e1")
	_box(Vector3(hl * 2.0 + 38.0, 0.8, 18.0), roof, Vector3(0, 33.0, -(hw + 34.0)), true)
	_box(Vector3(hl * 2.0 + 38.0, 0.25, 0.4), Color("#fef9c3"), Vector3(0, 32.5, -(hw + 25.2)), false)
	for sgn: float in [-1.0, 1.0]:
		_box(Vector3(14.0, 0.8, hw * 2.0 + 30.0), roof, Vector3(sgn * (hl + 22.0), 18.0, 0), true)
	# refletores: torres + painel aceso + brilho
	for sx: float in [-1.0, 1.0]:
		for sz: float in [-1.0, 1.0]:
			var p := Vector3(sx * (hl + 14.0), 0, sz * (hw + 18.0))
			var mast := CylinderMesh.new()
			mast.top_radius = 0.35
			mast.bottom_radius = 0.6
			mast.height = 38.0
			_mesh(mast, Toon.mat(Color("#64748b"), true), p + Vector3(0, 19.0, 0))
			var panel := _box(Vector3(7.0, 4.0, 0.6), Color("#fffbeb"), p + Vector3(0, 39.0, 0), false)
			panel.look_at(Vector3(0, 0, 0), Vector3.UP)
			if _night:
				_glow(p + Vector3(0, 39.0, 0), 26.0, Color(1.0, 0.95, 0.8), 2.6)
				_glow(p + Vector3(0, 39.0, 0), 9.0, Color(1.0, 1.0, 0.95), 1.4)
	# bandeirinhas de escanteio
	var pole := CylinderMesh.new()
	pole.top_radius = 0.022
	pole.bottom_radius = 0.022
	pole.height = 1.55
	var flag := QuadMesh.new()
	flag.size = Vector2(0.42, 0.3)
	var flag_m := Toon.mat(Color("#facc15"), false, 0.0)
	for sx: float in [-1.0, 1.0]:
		for sz: float in [-1.0, 1.0]:
			_mesh(pole, Toon.mat(Color("#f8fafc"), true), Vector3(sx * hl, 0.775, sz * hw))
			var fm := _mesh(flag, flag_m, Vector3(sx * hl - sx * 0.21, 1.38, sz * hw), Vector3(0, 0.25 * sx, 0))
			fm.material_override = Toon.mat(Color("#ef4444") if sx > 0 else Color("#facc15"), false, 0.0)
	# bancos de reservas (cobertura transparente) e área técnica
	for sx: float in [-1.0, 1.0]:
		var bx := sx * 10.0
		_box(Vector3(8.0, 0.5, 1.4), Color("#1f2937"), Vector3(bx, 0.25, -(hw + 2.7)))
		_box(Vector3(8.0, 1.2, 0.15), Color("#334155"), Vector3(bx, 0.85, -(hw + 3.35)))
		var glass := BoxMesh.new()
		glass.size = Vector3(8.2, 0.08, 1.8)
		var gm := ShaderMaterial.new()
		gm.shader = preload("res://shaders/net.gdshader")
		gm.set_shader_parameter("alpha", 0.25)
		_mesh(glass, gm, Vector3(bx, 1.55, -(hw + 2.6)))
		for k in 6:
			var seat_col: Color = a if sx < 0 else b
			_box(Vector3(0.45, 0.55, 0.4), seat_col.darkened(0.3), Vector3(bx - 3.0 + k * 1.2, 0.8, -(hw + 2.9)))
	# câmeras/fotógrafos atrás dos gols
	for sx: float in [-1.0, 1.0]:
		for k in 5:
			var y := -9.0 + k * 4.5
			if absf(y) < 4.5:
				continue
			_box(Vector3(0.45, 0.9, 0.45), Color("#0f172a"), Vector3(sx * (hl + 3.0), 0.45, y))


func _build_gym() -> void:
	var hl := sim.half_len
	var hw := sim.half_w
	var wall := Color("#1e3a5f")
	_box(Vector3(hl * 2.0 + 16.0, 6.0, 0.4), wall, Vector3(0, 3.0, -(hw + 8.0)), false)
	_box(Vector3(0.4, 6.0, hw * 2.0 + 16.0), wall, Vector3(-(hl + 8.0), 3.0, 0), false)
	_box(Vector3(0.4, 6.0, hw * 2.0 + 16.0), wall, Vector3(hl + 8.0, 3.0, 0), false)
	_stand(Vector3(0, 0.5, -(hw + 3.0)), hl * 2.0 + 6.0, 4.8, 3.6, 0.0, 1.8)
	_box(Vector3(hl * 2.0 + 16.0, 0.9, 0.12), Color("#facc15"), Vector3(0, 6.5, -(hw + 7.8)), false)
	for i in 3:
		var l := Label3D.new()
		l.text = "CRAQUEZ FUTSAL"
		l.font_size = 120
		l.pixel_size = 0.007
		l.modulate = Color("#e2e8f0")
		l.shaded = false
		l.position = Vector3(-hl * 0.7 + i * hl * 0.7, 5.0, -(hw + 7.7))
		add_child(l)
	# luminárias no teto
	for lp in _lights2d:
		_box(Vector3(3.0, 0.2, 1.0), Color("#fffbeb"), Vector3(lp.x, 9.0, lp.y), false)
		_glow(Vector3(lp.x, 9.0, lp.y), 10.0, Color(1.0, 0.97, 0.85), 2.4)
	# protetores atrás dos gols
	for s: float in [-1.0, 1.0]:
		_box(Vector3(0.3, 1.2, hw * 2.0), Color("#334155"), Vector3(s * (hl + 3.5), 0.6, 0))


func _build_street() -> void:
	var hl := sim.half_len
	var hw := sim.half_w
	var gh := sim.goal_half
	var wall := Color("#9ca3af")
	var wh := 1.0
	# muros da "tabela" (os mesmos da simulação)
	for s: float in [-1.0, 1.0]:
		_box(Vector3(hl * 2.0 + 0.6, wh, 0.3), wall, Vector3(0, wh * 0.5, s * (hw + 0.15)))
		var seg := hw - gh
		for sy: float in [-1.0, 1.0]:
			_box(Vector3(0.3, wh, seg), wall, Vector3(s * (hl + 0.15), wh * 0.5, sy * (gh + seg * 0.5)))
	# alambrado acima do muro
	if Settings.detail["net"]:
		var net_shader := preload("res://shaders/net.gdshader")
		var y0 := wh
		var y1 := 4.0
		_net_quad(net_shader, Vector3(-hl, y0, -hw - 0.2), Vector3(hl, y0, -hw - 0.2), Vector3(hl, y1, -hw - 0.2), Vector3(-hl, y1, -hw - 0.2))
		for s: float in [-1.0, 1.0]:
			_net_quad(net_shader, Vector3(s * (hl + 0.2), y0, -hw), Vector3(s * (hl + 0.2), y0, hw), Vector3(s * (hl + 0.2), y1, hw), Vector3(s * (hl + 0.2), y1, -hw))
	# prédios ao fundo
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	var palette := [Color("#b45309"), Color("#7c2d12"), Color("#475569"), Color("#9a3412"), Color("#57534e"), Color("#0f766e")]
	var x := -hl - 14.0
	while x < hl + 14.0:
		var w := rng.randf_range(5.0, 9.0)
		var h := rng.randf_range(7.0, 16.0)
		var col: Color = palette[rng.randi() % palette.size()]
		_box(Vector3(w, h, 6.0), col, Vector3(x + w * 0.5, h * 0.5, -(hw + 9.0)))
		# janelas
		var rows := int(h / 2.6)
		for r in rows:
			for c in int(w / 2.2):
				if rng.randf() < 0.75:
					_box(Vector3(0.9, 1.1, 0.05), Color("#fde68a") if rng.randf() < 0.3 else Color("#1e293b"),
						Vector3(x + 1.2 + c * 2.2, 1.6 + r * 2.6, -(hw + 5.97)), false)
		x += w + 0.3
	# postes de luz
	for sx: float in [-1.0, 1.0]:
		var lp := Vector3(sx * (hl + 2.5), 0, -(hw + 2.5))
		var post := CylinderMesh.new()
		post.top_radius = 0.07
		post.bottom_radius = 0.1
		post.height = 6.0
		_mesh(post, Toon.mat(Color("#27272a"), true), lp + Vector3(0, 3.0, 0))
		_box(Vector3(0.9, 0.18, 0.35), Color("#fde68a"), lp + Vector3(-sx * 0.4, 6.0, 0), false)
		_glow(lp + Vector3(-sx * 0.4, 5.9, 0), 5.0, Color(1.0, 0.85, 0.55), 2.2)
	# grafite no muro
	var g := Label3D.new()
	g.text = "CRAQUEZ STREET"
	g.font_size = 160
	g.pixel_size = 0.006
	g.modulate = Color("#facc15")
	g.outline_modulate = Color("#7c3aed")
	g.outline_size = 24
	g.shaded = false
	g.position = Vector3(0, 0.5, -hw - 0.01)
	add_child(g)


func _make_shadows() -> Array:
	var list: Array = []
	if not Settings.detail["shadows"]:
		return list
	var n := _lights2d.size() if (_lights2d.size() > 1 and Settings.detail["crowd"]) else 1
	for i in n:
		var sh := MeshInstance3D.new()
		sh.mesh = _quad
		sh.material_override = _shadow_mat
		sh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(sh)
		list.append(sh)
	return list


## Sombras no chão: uma por refletor (noite = 4 sombras cruzadas), ou uma
## longa na direção do sol.
func _place_shadows(list: Array, pos: Vector2, yaw: float, lying: bool) -> void:
	if list.is_empty():
		return
	if list.size() == 1:
		var sh: MeshInstance3D = list[0]
		var d := _sun_dir if _sun_dir != Vector2.ZERO else Vector2(0.25, -0.2)
		var l := 1.0 + d.length() * 2.2
		var dir := d.normalized()
		sh.position = Vector3(pos.x + dir.x * l * 0.4, 0.015, pos.y + dir.y * l * 0.4)
		sh.rotation = Vector3(-PI / 2, atan2(-dir.x, -dir.y), 0)
		sh.scale = Vector3(1.6 if lying else 0.85, l * (1.4 if lying else 1.0), 1.0)
		if _sun_dir == Vector2.ZERO:
			sh.rotation = Vector3(-PI / 2, yaw, 0)
			sh.scale = Vector3(1.9 if lying else 1.1, 0.9 if lying else 0.8, 1.0)
		return
	for i in list.size():
		var sh: MeshInstance3D = list[i]
		var dir := (pos - _lights2d[i]).normalized()
		var l := 2.1
		sh.position = Vector3(pos.x + dir.x * l * 0.42, 0.015 + 0.001 * i, pos.y + dir.y * l * 0.42)
		sh.rotation = Vector3(-PI / 2, atan2(-dir.x, -dir.y), 0)
		sh.scale = Vector3(1.5 if lying else 0.75, l, 1.0)


func _kit_for(p: SimPlayer) -> Dictionary:
	var m: Dictionary = FaceLibrary.meta(p.face_id)
	var cols: Array = team_colors[p.team] if p.team >= 0 else [Color("#111827"), Color("#111827")]
	var shirt: Color = cols[0]
	var second: Color = cols[1]
	return {
		"shirt": shirt, "shorts": second, "socks": shirt,
		"skin": Color(String(m.get("skin_hex", "#c98e62"))),
		"hair": Color(String(m.get("hair_hex", "#1a1210"))),
		"hair_style": String(m.get("hair_style", "short")),
		"number_color": second if second.get_luminance() > 0.5 or shirt.get_luminance() < 0.5 else Color("#0f172a"),
		"beard": {"none": 0.0, "stubble": 0.45, "mustache": 0.35, "chin": 0.5, "goatee": 0.6, "full": 0.85}.get(String(m.get("beard", "none")), 0.5),
		"eye": EYE_COLORS[int(m.get("eye_color", 0)) % EYE_COLORS.size()],
		"boots": BOOT_COLORS[(p.face_id * 3 + p.number) % BOOT_COLORS.size()],
	}


func _build_players() -> void:
	for p in sim.players:
		var kit := _kit_for(p)
		if p.is_keeper:
			kit["shirt"] = Color("#22c55e") if p.team == 0 else Color("#f59e0b")
			kit["shorts"] = Color("#111827")
			kit["socks"] = kit["shirt"]
			kit["gloves"] = Color("#f8fafc")
			kit["long_sleeves"] = true
			kit["number_color"] = Color("#111827")
		var rig := PlayerRig.new()
		add_child(rig)
		rig.build(p, kit)
		rigs.append(rig)
		_shadows.append(_make_shadows())


## Árbitro e assistentes: só visuais (não interferem na simulação).
func _build_officials() -> void:
	if sim.mode["id"] == GameModes.STREET:
		return
	var kinds := ["ref"]
	if sim.mode["id"] == GameModes.FIELD:
		kinds.append_array(["ar_far", "ar_near"])
	var i := 0
	for k: String in kinds:
		var fp := SimPlayer.new()
		fp.id = 200 + i
		fp.team = -1
		fp.number = 0
		fp.face_id = 37 + i * 5
		fp.facing = Vector2.RIGHT
		match k:
			"ref":
				fp.pos = Vector2(-4.0, 6.0)
			"ar_far":
				fp.pos = Vector2(10.0, -sim.half_w - 0.8)
			_:
				fp.pos = Vector2(-10.0, sim.half_w + 0.8)
		var kit := _kit_for(fp)
		kit["shirt"] = Color("#facc15") if sim.mode["id"] == GameModes.FIELD else Color("#111827")
		kit["shorts"] = Color("#111827")
		kit["socks"] = Color("#111827")
		var rig := PlayerRig.new()
		add_child(rig)
		rig.build(fp, kit)
		rig.number_label.visible = false
		if k != "ref":
			# bandeira do assistente
			var att := BoneAttachment3D.new()
			att.bone_name = "hand_r"
			rig.skel.add_child(att)
			var stick := CylinderMesh.new()
			stick.top_radius = 0.01
			stick.bottom_radius = 0.01
			stick.height = 0.5
			var st := MeshInstance3D.new()
			st.mesh = stick
			st.material_override = Toon.mat(Color("#e5e7eb"), false)
			st.position = Vector3(0, -0.12, -0.15)
			st.rotation.x = PI / 2
			att.add_child(st)
			var fl := MeshInstance3D.new()
			var fq := QuadMesh.new()
			fq.size = Vector2(0.3, 0.22)
			fl.mesh = fq
			fl.material_override = Toon.mat(Color("#ef4444"), false)
			fl.position = Vector3(0, -0.04, -0.32)
			att.add_child(fl)
		_officials.append({"rig": rig, "p": fp, "kind": k, "shadows": _make_shadows()})
		i += 1


func _update_officials(delta: float) -> void:
	var b := sim.ball
	for o in _officials:
		var fp: SimPlayer = o["p"]
		var target: Vector2
		match String(o["kind"]):
			"ref":
				# acompanha a jogada em diagonal, sem ficar no caminho da bola
				var off := Vector2(-6.0 if b.pos.x > 0.0 else 6.0, 7.0 if b.pos.y < 0.0 else -7.0)
				if sim.mode["id"] == GameModes.FUTSAL:
					off *= 0.45
				target = b.pos + off
				target.x = clampf(target.x, -sim.half_len + 2.0, sim.half_len - 2.0)
				target.y = clampf(target.y, -sim.half_w + 1.0, sim.half_w - 1.0)
			"ar_far":
				target = Vector2(clampf(b.pos.x, 0.0, sim.half_len), -sim.half_w - 0.8)
			_:
				target = Vector2(clampf(b.pos.x, -sim.half_len, 0.0), sim.half_w + 0.8)
		var to := target - fp.pos
		var want := to.limit_length(1.0) * (7.0 if to.length() > 1.0 else to.length() * 4.0)
		fp.vel = fp.vel.move_toward(want, 9.0 * delta)
		fp.pos += fp.vel * delta
		var look := b.pos - fp.pos
		if look.length() > 0.1:
			fp.facing = fp.facing.slerp(look.normalized(), minf(1.0, delta * 6.0)).normalized()
		(o["rig"] as PlayerRig).animate(fp, b.pos, b.z, delta)
		_place_shadows(o["shadows"], fp.pos, (o["rig"] as PlayerRig).rotation.y, false)


func _build_ball() -> void:
	ball_node = Node3D.new()
	add_child(ball_node)
	var sm := SphereMesh.new()
	sm.radius = sim.ball.radius
	sm.height = sim.ball.radius * 2.0
	sm.radial_segments = 24
	sm.rings = 12
	var mat := ShaderMaterial.new()
	mat.shader = preload("res://shaders/ball.gdshader")
	if Settings.detail.get("outline", true):
		mat.next_pass = Toon.outline_mat()
	ball_mesh = MeshInstance3D.new()
	ball_mesh.mesh = sm
	ball_mesh.material_override = mat
	ball_node.add_child(ball_mesh)
	var shadow_mat := ShaderMaterial.new()
	shadow_mat.shader = preload("res://shaders/blob_shadow.gdshader")
	shadow_mat.set_shader_parameter("strength", 0.55)
	var quad := QuadMesh.new()
	quad.size = Vector2(1.0, 1.0)
	_ball_shadow = MeshInstance3D.new()
	_ball_shadow.mesh = quad
	_ball_shadow.material_override = shadow_mat
	_ball_shadow.rotation.x = -PI / 2
	add_child(_ball_shadow)


# ------------------------------------------------------------------ quadro a quadro

func _process(delta: float) -> void:
	if sim == null:
		return
	var b := sim.ball
	for i in rigs.size():
		var p: SimPlayer = sim.players[i]
		rigs[i].animate(p, b.pos, b.z, delta)
		if i < _shadows.size():
			_place_shadows(_shadows[i], p.pos, rigs[i].rotation.y, p.slide > 0.0 or p.stun > 0.3)
	_update_officials(delta)
	# bola: posição + rotação de rolamento
	ball_node.position = Vector3(b.pos.x, b.z + b.radius, b.pos.y)
	var v3 := Vector3(b.vel.x, 0, b.vel.y)
	var sp := v3.length()
	if sp > 0.05:
		var axis := Vector3.UP.cross(v3 / sp)
		ball_mesh.rotate(axis.normalized(), sp / b.radius * delta)
	var hs := clampf(1.0 - b.z * 0.08, 0.35, 1.0)
	_ball_shadow.position = Vector3(b.pos.x + b.z * 0.4, 0.016, b.pos.y - b.z * 0.5)
	_ball_shadow.scale = Vector3.ONE * 0.42 * (2.0 - hs)
	(_ball_shadow.material_override as ShaderMaterial).set_shader_parameter("strength", 0.6 * hs)
	# empolgação da torcida: bola perto do gol
	var near_goal := 1.0 - clampf((sim.half_len - absf(b.pos.x)) / (sim.half_len * 0.5), 0.0, 1.0)
	var target := 0.15 + 0.6 * near_goal
	if sim.state == MatchSim.State.GOAL:
		target = 1.0
	_excitement = lerpf(_excitement, target, minf(1.0, delta * 2.0))
	for m in _crowd_mats:
		m.set_shader_parameter("excitement", _excitement)
	_update_camera(delta)
	_overlay.queue_redraw()


func _update_camera(delta: float) -> void:
	var b := sim.ball
	if closeup_id >= 0:
		var p: SimPlayer = sim.players[closeup_id]
		var at := Vector3(p.pos.x, 0.95, p.pos.y)
		cam.fov = 40.0
		cam.position = at + Vector3(2.6, 1.0, 4.2)
		cam.look_at(at, Vector3.UP)
		return
	cam.fov = _cam_cfg["fov"]
	var cfg := _cam_cfg
	var lead := b.pos + b.vel * 0.35
	var lim := maxf(0.0, sim.half_len - float(cfg["clamp"]))
	var k := minf(1.0, delta * 3.0)
	_cam_x = lerpf(_cam_x, clampf(lead.x, -lim, lim), k)
	var look_target := Vector3(clampf(lead.x, -sim.half_len, sim.half_len), b.z * 0.3, lead.y * float(cfg["look_z"]))
	_look = _look.lerp(look_target, minf(1.0, delta * 4.0))
	var pos := Vector3(_cam_x, float(cfg["height"]), sim.half_w + float(cfg["back"]))
	if _shake > 0.0:
		pos += Vector3(randf_range(-1, 1), randf_range(-1, 1), 0) * _shake
		_shake = maxf(0.0, _shake - delta * 0.6)
	if _intro > 0.0:
		# abertura: câmera sobrevoa o estádio antes do apito inicial
		_intro -= delta
		var t := clampf(1.0 - _intro / 3.2, 0.0, 1.0)
		var e := t * t * (3.0 - 2.0 * t)
		var r := sim.half_len * 1.35 + 10.0
		var ang := lerpf(-1.25, -0.15, e)
		var orbit := Vector3(sin(ang) * r, sim.half_len * 0.55 + 8.0, cos(ang) * r)
		pos = orbit.lerp(pos, e * e)
		cam.position = pos
		cam.look_at(Vector3.ZERO.lerp(_look, e), Vector3.UP)
		return
	cam.position = pos
	cam.look_at(_look, Vector3.UP)


## Desenho 2D por cima do 3D: seta do jogador controlado, barra de força, fôlego.
class MarkerOverlay:
	extends Control
	var view: MatchView3D
	var _font: Font

	func _ready() -> void:
		set_anchors_preset(Control.PRESET_FULL_RECT)
		mouse_filter = Control.MOUSE_FILTER_IGNORE
		_font = ThemeDB.fallback_font

	func _draw() -> void:
		if view == null or view.sim == null or view.cam == null:
			return
		var sim := view.sim
		for t in 2:
			if not view.human_team[t]:
				continue
			var p: SimPlayer = sim.controlled_player(t)
			var col: Color = Color("#facc15") if t == 0 else Color("#38bdf8")
			var head := Vector3(p.pos.x, 2.25, p.pos.y)
			if view.cam.is_position_behind(head):
				continue
			var top := view.cam.unproject_position(head)
			var u := clampf(18.0 * get_viewport_rect().size.y / 1080.0, 10.0, 30.0)
			draw_colored_polygon(PackedVector2Array([top + Vector2(-u * 0.6, -u), top + Vector2(u * 0.6, -u), top]), col)
			draw_polyline(PackedVector2Array([top + Vector2(-u * 0.6, -u), top + Vector2(u * 0.6, -u), top, top + Vector2(-u * 0.6, -u)]), Color(0, 0, 0, 0.7), 2.0)
			draw_string(_font, top + Vector2(-u * 1.5, -u * 1.25), "P%d" % (t + 1), HORIZONTAL_ALIGNMENT_CENTER, u * 3.0, int(u * 1.05), col)
			# anel no chão
			var ring := PackedVector2Array()
			for i in 25:
				var a := TAU * i / 24.0
				ring.append(view.cam.unproject_position(Vector3(p.pos.x + cos(a) * 0.6, 0.03, p.pos.y + sin(a) * 0.6)))
			draw_polyline(ring, col, maxf(2.0, u * 0.18), true)
			var feet := view.cam.unproject_position(Vector3(p.pos.x, 0.0, p.pos.y))
			var ch := sim.charge_of(t)
			if ch > 0.0:
				var w := u * 4.0
				var bp := feet + Vector2(-w * 0.5, u * 0.9)
				draw_rect(Rect2(bp - Vector2(2, 2), Vector2(w + 4, u * 0.5 + 4)), Color(0, 0, 0, 0.7))
				draw_rect(Rect2(bp, Vector2(w * ch, u * 0.5)), Color("#22c55e").lerp(Color("#ef4444"), ch))
			if p.stamina < 0.6:
				var w2 := u * 3.0
				var sp := feet + Vector2(-w2 * 0.5, u * 1.7)
				draw_rect(Rect2(sp, Vector2(w2, u * 0.18)), Color(0, 0, 0, 0.5))
				draw_rect(Rect2(sp, Vector2(w2 * p.stamina, u * 0.18)), Color("#a3e635"))
