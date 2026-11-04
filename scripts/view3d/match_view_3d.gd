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

signal weather_changed(text: String)

# configuração da partida (definida antes de setup)
var weather: String = "sol"
var tod: String = "noite"          # dia, tarde, noite
var dynamic_weather: bool = false
var home_team: Dictionary = {}
var kits: Array = []               # uniforme escolhido de cada time (Teams.pick_kits)
var visual_seed: int = 1

var weather_fx: WeatherFX
var _env: Environment
var _sky_mat: ProceduralSkyMaterial
var _pitch_mat: ShaderMaterial
var _ball_mat: ShaderMaterial
var _celebrate: float = 0.0
var _wave: float = -1.0
var _wave_cd: float = 25.0
var _flares: Array = []             # [{node, t}]
var _confetti: Array[CPUParticles3D] = []
var _goal_end: float = 1.0          # lado (x) da torcida que comemora
var _stadium_tier: int = 3

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
	_stadium_tier = int(home_team.get("stadium", {}).get("tier", 3))
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
	weather_fx = WeatherFX.new()
	add_child(weather_fx)
	weather_fx.env = _env
	weather_fx.sky_mat = _sky_mat
	if _sky_mat != null:
		weather_fx.sky_top = _sky_mat.sky_top_color
		weather_fx.sky_horizon = _sky_mat.sky_horizon_color
	weather_fx.pitch_mat = _pitch_mat
	weather_fx.ball_mat = _ball_mat
	weather_fx.crowd_mats = _crowd_mats
	weather_fx.crowd_light = 0.82 if _night else 1.0
	weather_fx.cam = cam
	var indoor := String(sim.mode["surface"]) == "wood"
	weather_fx.setup("sol" if indoor else weather, dynamic_weather and not indoor, _night, visual_seed, self)
	weather_fx.changed.connect(func(t: String): weather_changed.emit(t))
	_build_confetti()
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
		if surface == "grass" and tod == "noite":
			# jogo noturno
			_night = true
			sm.sky_top_color = Color("#050a1a")
			sm.sky_horizon_color = Color("#1d2d50")
			sm.ground_bottom_color = Color("#05070d")
			sm.ground_horizon_color = Color("#1d2d50")
			for sx: float in [-1.0, 1.0]:
				for sz: float in [-1.0, 1.0]:
					_lights2d.append(Vector2(sx * (hl + 14.0), sz * (hw + 18.0)))
		elif tod == "dia":
			sm.sky_top_color = Color("#2f6fd6")
			sm.sky_horizon_color = Color("#bcd8f2")
			sm.ground_bottom_color = Color("#1f2937")
			sm.ground_horizon_color = Color("#94a3b8")
			_sun_dir = Vector2(0.35, -0.3)
		else:
			# fim de tarde: céu alaranjado e sombras longas
			sm.sky_top_color = Color("#3b2a6b")
			sm.sky_horizon_color = Color("#fb923c")
			sm.ground_bottom_color = Color("#1f2937")
			sm.ground_horizon_color = Color("#9a3412")
			_sun_dir = Vector2(0.55, -0.35)
		sky.sky_material = sm
		env.sky = sky
		_sky_mat = sm
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	_env = env
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
	_pitch_mat = mat
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
	m.set_shader_parameter("tifo", 0.85 if bias > 0.85 else 0.0)
	if home_team.has("c1"):
		m.set_shader_parameter("seat_color", (home_team["c1"] as Color).darkened(0.72))
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
	# arquibancadas conforme o porte do estádio do time da casa
	# (3 = arena com dois anéis e cobertura total, 2 = clássico, 1 = modesto)
	var base_y := 1.4
	var tier := _stadium_tier
	_box(Vector3(hl * 2.0 + 30.0, base_y, 1.0), Color("#111827"), Vector3(0, base_y * 0.5, -(hw + 7.5)), false)
	var roof := Color("#94a3b8") if _night else Color("#cbd5e1")
	if tier >= 3:
		_stand(Vector3(0, base_y, -(hw + 8.0)), hl * 2.0 + 30.0, 22.0, 15.0, 0.0, 1.6, 0.5)
		_stand(Vector3(0, base_y + 16.5, -(hw + 31.0)), hl * 2.0 + 30.0, 12.0, 9.0, 0.0, 1.6, 0.5)
		_stand(Vector3(-(hl + 9.0), base_y, 0), hw * 2.0 + 16.0, 16.0, 11.0, PI / 2, 1.6, 0.92)
		_stand(Vector3(hl + 9.0, base_y, 0), hw * 2.0 + 16.0, 16.0, 11.0, -PI / 2, 1.6, 0.08)
		_stand(Vector3(0, 0.6, hw + 8.0), hl * 2.0 + 30.0, 6.0, 3.0, PI, 1.6, 0.5)
		_box(Vector3(hl * 2.0 + 30.0, 1.4, 1.0), Color("#1e293b"), Vector3(0, base_y + 15.6, -(hw + 30.6)), false)
		_box(Vector3(hl * 2.0 + 30.0, 0.35, 0.3), Color("#fde68a"), Vector3(0, base_y + 15.0, -(hw + 30.2)), false)
		_box(Vector3(hl * 2.0 + 38.0, 0.8, 18.0), roof, Vector3(0, 33.0, -(hw + 34.0)), true)
		_box(Vector3(hl * 2.0 + 38.0, 0.25, 0.4), Color("#fef9c3"), Vector3(0, 32.5, -(hw + 25.2)), false)
		for sgn: float in [-1.0, 1.0]:
			_box(Vector3(14.0, 0.8, hw * 2.0 + 30.0), roof, Vector3(sgn * (hl + 22.0), 18.0, 0), true)
		# telão no fundo
		_box(Vector3(12.0, 6.5, 0.6), Color("#0b1220"), Vector3(hl + 18.0, 16.0, -hw * 0.6), true)
	elif tier == 2:
		_stand(Vector3(0, base_y, -(hw + 8.0)), hl * 2.0 + 24.0, 18.0, 11.0, 0.0, 1.6, 0.5)
		_stand(Vector3(-(hl + 9.0), base_y, 0), hw * 2.0 + 10.0, 12.0, 7.0, PI / 2, 1.6, 0.92)
		_stand(Vector3(hl + 9.0, base_y, 0), hw * 2.0 + 10.0, 12.0, 7.0, -PI / 2, 1.6, 0.08)
		_stand(Vector3(0, 0.6, hw + 8.0), hl * 2.0 + 24.0, 5.0, 2.5, PI, 1.6, 0.5)
		_box(Vector3(hl * 1.4, 0.7, 12.0), roof, Vector3(0, 21.0, -(hw + 20.0)), true)
	else:
		# estádio modesto: arquibancada só no lado de lá e atrás de um gol; resto é muro e árvores
		_stand(Vector3(0, base_y, -(hw + 8.0)), hl * 1.6, 9.0, 5.0, 0.0, 1.6, 0.5)
		_stand(Vector3(-(hl + 9.0), base_y, 0), hw * 1.4, 6.0, 3.5, PI / 2, 1.6, 0.9)
		_box(Vector3(hl * 0.6, 0.5, 6.0), roof, Vector3(0, 9.5, -(hw + 13.0)), true)
		var rng := RandomNumberGenerator.new()
		rng.seed = visual_seed
		for i in 22:
			var side := -1.0 if i % 2 == 0 else 1.0
			var tp := Vector3(rng.randf_range(-hl - 20.0, hl + 20.0) if i < 14 else (hl + 12.0) * 1.0, 0, -(hw + 22.0 + rng.randf_range(0, 8)) if i < 14 else rng.randf_range(-hw, hw))
			if i >= 14 and side < 0.0:
				continue
			var trunk := CylinderMesh.new()
			trunk.top_radius = 0.25
			trunk.bottom_radius = 0.35
			trunk.height = 4.0
			_mesh(trunk, Toon.mat(Color("#5b3a1e"), true), tp + Vector3(0, 2.0, 0))
			var crown := SphereMesh.new()
			crown.radius = rng.randf_range(2.2, 3.4)
			crown.height = crown.radius * 2.0
			_mesh(crown, Toon.mat(Color("#166534").lerp(Color("#15803d"), rng.randf()), true, 0.1), tp + Vector3(0, 5.5, 0))
	# refletores: torres + painel aceso + brilho
	var mast_h := 38.0 if tier >= 3 else (30.0 if tier == 2 else 22.0)
	for sx: float in [-1.0, 1.0]:
		for sz: float in [-1.0, 1.0]:
			var p := Vector3(sx * (hl + 14.0), 0, sz * (hw + 18.0))
			var mast := CylinderMesh.new()
			mast.top_radius = 0.35
			mast.bottom_radius = 0.6
			mast.height = mast_h
			_mesh(mast, Toon.mat(Color("#64748b"), true), p + Vector3(0, mast_h * 0.5, 0))
			var panel := _box(Vector3(7.0, 4.0, 0.6), Color("#fffbeb"), p + Vector3(0, mast_h + 1.0, 0), false)
			panel.look_at(Vector3(0, 0, 0), Vector3.UP)
			if _night:
				_glow(p + Vector3(0, mast_h + 1.0, 0), 26.0, Color(1.0, 0.95, 0.8), 2.6)
				_glow(p + Vector3(0, mast_h + 1.0, 0), 9.0, Color(1.0, 1.0, 0.95), 1.4)
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


# ------------------------------------------------------------------ quadras de futsal (8)
## [nome, piso A, piso B, tipo de piso, fora da quadra, área, parede, arquibancadas (0-2), cor da tela, faixa]
const FUTSAL_VENUES := [
	["Ginásio Clássico", "#bd8048", "#d4975c", 0, "#29497a", "#3366b3", "#1e3a5f", 1, "#e2e8f0", "#facc15"],
	["Arena Azul", "#1d4ed8", "#2563eb", 1, "#0f172a", "#f97316", "#0b1220", 2, "#93c5fd", "#38bdf8"],
	["Quadra Verde", "#15803d", "#16a34a", 1, "#334155", "#0ea5e9", "#475569", 0, "#e5e7eb", "#4ade80"],
	["Ginásio Escolar", "#c2410c", "#ea580c", 1, "#fde68a", "#1d4ed8", "#fef3c7", 1, "#f8fafc", "#1d4ed8"],
	["Arena Noturna", "#27272a", "#3f3f46", 1, "#09090b", "#7c3aed", "#18181b", 2, "#a855f7", "#e879f9"],
	["Society Coberto", "#3f8f3f", "#4ea24e", 2, "#1f2937", "#3f8f3f", "#374151", 0, "#f8fafc", "#facc15"],
	["Ginásio Municipal", "#b91c1c", "#dc2626", 1, "#e5e7eb", "#1e3a8a", "#f1f5f9", 1, "#cbd5e1", "#1e3a8a"],
	["Arena Internacional", "#d6a46a", "#e3b884", 0, "#1e293b", "#0f766e", "#0f172a", 2, "#e2e8f0", "#facc15"],
]
## [nome, tipo de piso, linha, muro, cenário]
const STREET_VENUES := [
	["Quadra do Bairro", 0, "#facc15", "#9ca3af", "predios"],
	["Morro Colorido", 6, "#f8fafc", "#e5e7eb", "morro"],
	["Praia", 2, "#1d4ed8", "#d6b98c", "praia"],
	["Terrão", 3, "#f8fafc", "#a16207", "terrao"],
	["Gaiola Urbana", 4, "#f8fafc", "#64748b", "gaiola"],
	["Telhado", 1, "#f97316", "#94a3b8", "telhado"],
	["Debaixo do Viaduto", 6, "#22d3ee", "#6b7280", "viaduto"],
	["Praça", 5, "#f8fafc", "#a8a29e", "praca"],
]
var venue: int = 0


func _build_gym() -> void:
	var hl := sim.half_len
	var hw := sim.half_w
	var v: Array = FUTSAL_VENUES[posmod(venue, FUTSAL_VENUES.size())]
	if _pitch_mat != null:
		_pitch_mat.set_shader_parameter("floor_a", Color(String(v[1])))
		_pitch_mat.set_shader_parameter("floor_b", Color(String(v[2])))
		_pitch_mat.set_shader_parameter("floor_kind", int(v[3]))
		_pitch_mat.set_shader_parameter("outside_col", Color(String(v[4])))
		_pitch_mat.set_shader_parameter("area_col", Color(String(v[5])))
	var wall := Color(String(v[6]))
	var stands := int(v[7])
	_box(Vector3(hl * 2.0 + 16.0, 7.0, 0.4), wall, Vector3(0, 3.5, -(hw + 8.0)), false)
	_box(Vector3(0.4, 7.0, hw * 2.0 + 16.0), wall, Vector3(-(hl + 8.0), 3.5, 0), false)
	_box(Vector3(0.4, 7.0, hw * 2.0 + 16.0), wall, Vector3(hl + 8.0, 3.5, 0), false)
	if stands >= 1:
		_stand(Vector3(0, 0.5, -(hw + 3.2)), hl * 2.0 + 6.0, 4.8, 3.6, 0.0, 1.8)
	if stands >= 2:
		_stand(Vector3(-(hl + 3.6), 0.5, 0), hw * 2.0 + 2.0, 4.0, 3.0, PI / 2, 1.8, 0.92)
		_stand(Vector3(hl + 3.6, 0.5, 0), hw * 2.0 + 2.0, 4.0, 3.0, -PI / 2, 1.8, 0.08)
	_box(Vector3(hl * 2.0 + 16.0, 0.9, 0.12), Color(String(v[9])), Vector3(0, 6.6, -(hw + 7.8)), false)
	var l := Label3D.new()
	l.text = String(v[0]).to_upper()
	l.font_size = 120
	l.pixel_size = 0.008
	l.modulate = Color("#e2e8f0")
	l.shaded = false
	l.position = Vector3(0, 5.2, -(hw + 7.7))
	add_child(l)
	# telas de proteção fechando a quadra (lado da câmera bem transparente)
	if Settings.detail["net"]:
		var ns := preload("res://shaders/net.gdshader")
		var nh := 5.5
		var mx := hl + 2.0
		var mz := hw + 2.0
		var nc := Color(String(v[8]))
		_net_wall(ns, Vector3(-mx, 0, -mz), Vector3(mx, 0, -mz), nh, nc, 0.55)
		_net_wall(ns, Vector3(-mx, 0, -mz), Vector3(-mx, 0, mz), nh, nc, 0.5)
		_net_wall(ns, Vector3(mx, 0, -mz), Vector3(mx, 0, mz), nh, nc, 0.5)
		_net_wall(ns, Vector3(-mx, 0, mz), Vector3(mx, 0, mz), nh, nc, 0.12)
		# postes da tela
		for i in 9:
			var x := -mx + i * (mx * 2.0) / 8.0
			for z: float in [-mz, mz]:
				_box(Vector3(0.08, nh, 0.08), Color("#475569"), Vector3(x, nh * 0.5, z), false)
	# luminárias no teto
	for lp in _lights2d:
		_box(Vector3(3.0, 0.2, 1.0), Color("#fffbeb"), Vector3(lp.x, 9.0, lp.y), false)
		_glow(Vector3(lp.x, 9.0, lp.y), 10.0, Color(1.0, 0.97, 0.85), 2.4)
	# protetores atrás dos gols
	for s: float in [-1.0, 1.0]:
		_box(Vector3(0.3, 1.2, hw * 2.0), Color("#334155"), Vector3(s * (hl + 3.5), 0.6, 0))


## Parede de tela entre dois pontos no chão.
func _net_wall(shader: Shader, a: Vector3, b: Vector3, h: float, col: Color, alpha: float) -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var pts := [a, b, b + Vector3(0, h, 0), a + Vector3(0, h, 0)]
	var uvs := [Vector2(0, 1), Vector2(1, 1), Vector2(1, 0), Vector2(0, 0)]
	for i: int in [0, 1, 2, 0, 2, 3]:
		st.set_uv(uvs[i])
		st.add_vertex(pts[i])
	var m := ShaderMaterial.new()
	m.shader = shader
	m.set_shader_parameter("size", Vector2(a.distance_to(b), h))
	m.set_shader_parameter("cell", 0.12)
	m.set_shader_parameter("alpha", alpha)
	m.set_shader_parameter("tint", col)
	_mesh(st.commit(), m, Vector3.ZERO)


# ------------------------------------------------------------------ cenários de rua (8)
func _build_street() -> void:
	var hl := sim.half_len
	var hw := sim.half_w
	var gh := sim.goal_half
	var v: Array = STREET_VENUES[posmod(venue, STREET_VENUES.size())]
	var kind: String = v[4]
	if _pitch_mat != null:
		_pitch_mat.set_shader_parameter("street_kind", int(v[1]))
		_pitch_mat.set_shader_parameter("street_line", Color(String(v[2])))
	var wall := Color(String(v[3]))
	var wh := 1.0
	# muros da "tabela" (os mesmos da simulação)
	for s: float in [-1.0, 1.0]:
		_box(Vector3(hl * 2.0 + 0.6, wh, 0.3), wall, Vector3(0, wh * 0.5, s * (hw + 0.15)))
		var seg := hw - gh
		for sy: float in [-1.0, 1.0]:
			_box(Vector3(0.3, wh, seg), wall, Vector3(s * (hl + 0.15), wh * 0.5, sy * (gh + seg * 0.5)))
	var rng := RandomNumberGenerator.new()
	rng.seed = 7 + venue * 13
	var ns := preload("res://shaders/net.gdshader")
	var fence := kind in ["predios", "gaiola", "telhado", "viaduto"]
	if fence and Settings.detail["net"]:
		var y1 := 7.0 if kind == "gaiola" else 4.0
		_net_wall(ns, Vector3(-hl, wh, -hw - 0.2), Vector3(hl, wh, -hw - 0.2), y1 - wh, Color("#d1d5db"), 0.55)
		for sx: float in [-1.0, 1.0]:
			_net_wall(ns, Vector3(sx * (hl + 0.2), wh, -hw), Vector3(sx * (hl + 0.2), wh, hw), y1 - wh, Color("#d1d5db"), 0.5)
		if kind == "gaiola":
			_net_wall(ns, Vector3(-hl, wh, hw + 0.2), Vector3(hl, wh, hw + 0.2), y1 - wh, Color("#d1d5db"), 0.12)
	match kind:
		"predios", "telhado":
			var palette := [Color("#b45309"), Color("#7c2d12"), Color("#475569"), Color("#9a3412"), Color("#57534e"), Color("#0f766e")]
			var far := -(hw + 9.0) if kind == "predios" else -(hw + 40.0)
			var base := 0.0 if kind == "predios" else -12.0
			var x := -hl - 30.0
			while x < hl + 30.0:
				var w := rng.randf_range(5.0, 9.0)
				var h := rng.randf_range(7.0, 16.0) * (1.0 if kind == "predios" else 3.0)
				var col: Color = palette[rng.randi() % palette.size()]
				_box(Vector3(w, h, 6.0), col, Vector3(x + w * 0.5, base + h * 0.5, far))
				var rows := int(h / 2.6)
				for r in mini(rows, 18):
					for c in int(w / 2.2):
						if rng.randf() < 0.7:
							_box(Vector3(0.9, 1.1, 0.05), Color("#fde68a") if rng.randf() < 0.3 else Color("#1e293b"),
								Vector3(x + 1.2 + c * 2.2, base + 1.6 + r * 2.6, far + 3.03), false)
				x += w + 0.3
			if kind == "telhado":
				# parapeito e caixas d'água
				_box(Vector3(hl * 2.0 + 12.0, 1.1, 0.3), Color("#a8a29e"), Vector3(0, 0.55, -(hw + 5.0)))
				for i in 3:
					var tank := CylinderMesh.new()
					tank.top_radius = 1.2
					tank.bottom_radius = 1.2
					tank.height = 2.2
					_mesh(tank, Toon.mat(Color("#2563eb"), true), Vector3(-hl + i * hl, 1.1, -(hw + 7.0)))
		"morro":
			# casinhas coloridas subindo o morro
			var colors := [Color("#f97316"), Color("#22c55e"), Color("#3b82f6"), Color("#facc15"), Color("#ec4899"), Color("#a855f7"), Color("#f1f5f9"), Color("#ef4444")]
			for row in 6:
				var x := -hl - 26.0 + rng.randf() * 3.0
				while x < hl + 26.0:
					var w := rng.randf_range(3.0, 5.5)
					var h := rng.randf_range(2.6, 4.2)
					var y0 := row * 3.4
					var z := -(hw + 6.0 + row * 4.0)
					_box(Vector3(w, h, 3.5), colors[rng.randi() % colors.size()], Vector3(x + w * 0.5, y0 + h * 0.5, z))
					_box(Vector3(0.8, 1.0, 0.05), Color("#1e293b"), Vector3(x + w * 0.5, y0 + h * 0.55, z + 1.78), false)
					x += w + 0.2
		"praia":
			# mar, guarda-sóis e coqueiros
			var sea := PlaneMesh.new()
			sea.size = Vector2(400, 200)
			_mesh(sea, Toon.mat(Color("#0ea5e9"), false, 0.0), Vector3(0, 0.02, -(hw + 110.0)))
			_box(Vector3(400, 0.05, 6), Color("#bae6fd"), Vector3(0, 0.04, -(hw + 12.0)), false)
			for i in 7:
				var px := -hl - 10.0 + i * (hl * 2.0 + 20.0) / 6.0
				var pole := CylinderMesh.new()
				pole.top_radius = 0.05
				pole.bottom_radius = 0.05
				pole.height = 2.6
				_mesh(pole, Toon.mat(Color("#f8fafc"), true), Vector3(px, 1.3, -(hw + 5.0)))
				var um := CylinderMesh.new()
				um.top_radius = 0.05
				um.bottom_radius = 1.4
				um.height = 0.5
				_mesh(um, Toon.mat([Color("#ef4444"), Color("#facc15"), Color("#22c55e")][i % 3], true), Vector3(px, 2.7, -(hw + 5.0)))
			for i in 5:
				_palm(Vector3(-hl - 6.0 + i * (hl * 2.0 + 12.0) / 4.0, 0, -(hw + 8.0) - rng.randf() * 3.0), rng)
		"terrao":
			# cerca de madeira, árvores e morros ao fundo
			for i in 16:
				_box(Vector3(0.15, 1.6, 0.15), Color("#78350f"), Vector3(-hl - 4.0 + i * (hl * 2.0 + 8.0) / 15.0, 0.8, -(hw + 3.0)))
			_box(Vector3(hl * 2.0 + 8.0, 0.15, 0.08), Color("#92400e"), Vector3(0, 1.3, -(hw + 3.0)))
			for i in 10:
				var tp := Vector3(rng.randf_range(-hl - 20.0, hl + 20.0), 0, -(hw + 9.0 + rng.randf() * 10.0))
				_tree(tp, rng)
			var hill := SphereMesh.new()
			hill.radius = 40.0
			hill.height = 30.0
			_mesh(hill, Toon.mat(Color("#3f6212"), false, 0.0), Vector3(-20, -5, -(hw + 70.0)))
			_mesh(hill, Toon.mat(Color("#4d7c0f"), false, 0.0), Vector3(40, -8, -(hw + 80.0)))
		"gaiola":
			for i in 4:
				var g := Label3D.new()
				g.text = ["CRAQUEZ", "RUA", "STREET", "JOGO BONITO"][i]
				g.font_size = 160
				g.pixel_size = 0.009
				g.modulate = [Color("#f472b6"), Color("#22d3ee"), Color("#facc15"), Color("#4ade80")][i]
				g.outline_size = 24
				g.outline_modulate = Color("#111827")
				g.shaded = false
				g.position = Vector3(-hl * 0.75 + i * hl * 0.5, 2.0, -(hw + 6.0))
				add_child(g)
			_box(Vector3(hl * 2.0 + 20.0, 6.0, 0.5), Color("#374151"), Vector3(0, 3.0, -(hw + 6.3)), false)
		"viaduto":
			# laje do viaduto passando por cima e pilares grafitados
			_box(Vector3(hl * 2.0 + 60.0, 1.6, 14.0), Color("#6b7280"), Vector3(0, 12.0, -hw * 0.3), true)
			for i in 4:
				var x := -hl - 6.0 + i * (hl * 2.0 + 12.0) / 3.0
				_box(Vector3(1.6, 12.0, 1.6), Color("#9ca3af"), Vector3(x, 6.0, -(hw + 3.5)), true)
				var g := Label3D.new()
				g.text = ["CRAQUEZ", "10", "GOL", "RUA"][i]
				g.font_size = 140
				g.pixel_size = 0.008
				g.modulate = [Color("#f472b6"), Color("#22d3ee"), Color("#facc15"), Color("#4ade80")][i]
				g.outline_size = 20
				g.outline_modulate = Color("#111827")
				g.shaded = false
				g.position = Vector3(x, 3.0, -(hw + 2.68))
				add_child(g)
		"praca":
			for i in 12:
				var tp := Vector3(rng.randf_range(-hl - 18.0, hl + 18.0), 0, -(hw + 5.0 + rng.randf() * 12.0))
				_tree(tp, rng)
			for i in 4:
				_box(Vector3(2.0, 0.45, 0.6), Color("#92400e"), Vector3(-hl + i * hl * 0.66, 0.45, -(hw + 2.6)))
			var fountain := CylinderMesh.new()
			fountain.top_radius = 3.0
			fountain.bottom_radius = 3.2
			fountain.height = 0.8
			_mesh(fountain, Toon.mat(Color("#cbd5e1"), true), Vector3(hl + 10.0, 0.4, -(hw + 8.0)))
	# postes de luz (em quase todos)
	if kind != "praia":
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
	var gl := Label3D.new()
	gl.text = String(v[0]).to_upper()
	gl.font_size = 160
	gl.pixel_size = 0.006
	gl.modulate = Color("#facc15")
	gl.outline_modulate = Color("#7c3aed")
	gl.outline_size = 24
	gl.shaded = false
	gl.position = Vector3(0, 0.5, -hw - 0.01)
	add_child(gl)


func _tree(p: Vector3, rng: RandomNumberGenerator) -> void:
	var trunk := CylinderMesh.new()
	trunk.top_radius = 0.22
	trunk.bottom_radius = 0.32
	trunk.height = 3.6
	_mesh(trunk, Toon.mat(Color("#5b3a1e"), true), p + Vector3(0, 1.8, 0))
	var crown := SphereMesh.new()
	crown.radius = rng.randf_range(1.8, 3.0)
	crown.height = crown.radius * 2.0
	_mesh(crown, Toon.mat(Color("#166534").lerp(Color("#22c55e"), rng.randf() * 0.5), true, 0.1), p + Vector3(0, 4.6, 0))


func _palm(p: Vector3, rng: RandomNumberGenerator) -> void:
	var trunk := CylinderMesh.new()
	trunk.top_radius = 0.15
	trunk.bottom_radius = 0.25
	trunk.height = 7.0
	var t := _mesh(trunk, Toon.mat(Color("#a16207"), true), p + Vector3(0, 3.5, 0))
	t.rotation.z = rng.randf_range(-0.15, 0.15)
	for i in 6:
		var leaf := BoxMesh.new()
		leaf.size = Vector3(3.2, 0.08, 0.7)
		var lm := _mesh(leaf, Toon.mat(Color("#15803d"), true), p + Vector3(0, 7.0, 0))
		lm.rotation = Vector3(0, TAU * i / 6.0, -0.35)


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
	var m: Dictionary = FaceLibrary.player_meta(p)
	var cols: Array = team_colors[p.team] if p.team >= 0 else [Color("#111827"), Color("#111827")]
	var shirt: Color = cols[0]
	var second: Color = cols[1]
	var shorts := second
	var socks := shirt
	var pattern := 0
	if p.team >= 0 and p.team < kits.size():
		var k: Dictionary = kits[p.team]
		shorts = k["shorts"]
		socks = k["socks"]
		pattern = int(k["pattern"])
	return {
		"shirt": shirt, "shorts": shorts, "socks": socks, "trim": second, "pattern": pattern,
		"skin": Color(String(m.get("skin_hex", "#c98e62"))),
		"hair": Color(String(m.get("hair_hex", "#1a1210"))),
		"hair_style": String(m.get("hair_style", "short")),
		"number_color": second if second.get_luminance() > 0.5 or shirt.get_luminance() < 0.5 else Color("#0f172a"),
		"beard": {"none": 0.0, "stubble": 0.45, "mustache": 0.35, "chin": 0.5, "goatee": 0.6, "full": 0.85}.get(String(m.get("beard", "none")), 0.5),
		"eye": EYE_COLORS[int(m.get("eye_color", 0)) % EYE_COLORS.size()],
		"boots": BOOT_COLORS[(p.face_id * 3 + p.number) % BOOT_COLORS.size()],
		"build": int(m.get("build", -1)),
	}


func _build_players() -> void:
	for p in sim.players:
		var kit := _kit_for(p)
		if p.is_keeper:
			kit["shirt"] = Color("#22c55e") if p.team == 0 else Color("#f59e0b")
			kit["shorts"] = Color("#111827")
			kit["socks"] = kit["shirt"]
			kit["gloves"] = Color("#f8fafc")
			kit["pattern"] = 0
			kit["trim"] = Color("#111827")
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


## Confete nas arquibancadas atrás de cada gol (dispara no gol).
func _build_confetti() -> void:
	if String(sim.mode["surface"]) != "grass" or not Settings.detail["crowd"]:
		return
	for side: float in [-1.0, 1.0]:
		var p := CPUParticles3D.new()
		var q := QuadMesh.new()
		q.size = Vector2(0.35, 0.22)
		var m := StandardMaterial3D.new()
		m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		m.vertex_color_use_as_albedo = true
		m.cull_mode = BaseMaterial3D.CULL_DISABLED
		q.material = m
		p.mesh = q
		p.amount = 400
		p.lifetime = 4.0
		p.one_shot = true
		p.explosiveness = 0.85
		p.emitting = false
		p.emission_shape = CPUParticles3D.EMISSION_SHAPE_BOX
		p.emission_box_extents = Vector3(3, 2, sim.half_w)
		p.position = Vector3(side * (sim.half_len + 16.0), 12.0, 0)
		p.direction = Vector3(-side * 0.5, 1, 0)
		p.spread = 50.0
		p.initial_velocity_min = 6.0
		p.initial_velocity_max = 12.0
		p.gravity = Vector3(0, -3.5, 0)
		p.angular_velocity_min = -360.0
		p.angular_velocity_max = 360.0
		var g := Gradient.new()
		p.color_initial_ramp = g
		add_child(p)
		_confetti.append(p)


## Gol: torcida comemora, confete nas cores do time e sinalizadores (à noite).
func on_goal(team: int) -> void:
	_celebrate = 1.0
	_wave = -1.0
	var attack_dir := sim.team_dir(team)
	_goal_end = attack_dir
	if _confetti.size() == 2:
		var p: CPUParticles3D = _confetti[0 if attack_dir < 0.0 else 1]
		var g := Gradient.new()
		g.set_color(0, team_colors[team][0])
		g.set_color(1, team_colors[team][1] if (team_colors[team][1] as Color).get_luminance() < 0.9 else Color("#facc15"))
		g.add_point(0.5, Color.WHITE)
		p.color_initial_ramp = g
		p.restart()
		p.emitting = true
	if (_night or tod == "tarde") and String(sim.mode["surface"]) == "grass" and Settings.detail["crowd"]:
		for i in 6:
			var pos := Vector3(attack_dir * (sim.half_len + 12.0 + randf() * 8.0), 4.0 + randf() * 6.0, randf_range(-sim.half_w, sim.half_w))
			var q := QuadMesh.new()
			q.size = Vector2(5, 5)
			var m := ShaderMaterial.new()
			m.shader = preload("res://shaders/glow.gdshader")
			m.set_shader_parameter("color", Color(1.0, 0.25, 0.1))
			var mi := _mesh(q, m, pos)
			_flares.append({"node": mi, "t": 7.0 + randf() * 3.0, "mat": m})


func _update_crowd(delta: float) -> void:
	_celebrate = maxf(0.0, _celebrate - delta * 0.11)
	# "ola" de vez em quando, com o jogo parado ou morno
	_wave_cd -= delta
	if _wave < 0.0 and _wave_cd <= 0.0 and _excitement < 0.4 and _celebrate <= 0.05:
		_wave = 0.0
		_wave_cd = randf_range(35.0, 70.0)
	if _wave >= 0.0:
		_wave += delta * 0.16
		if _wave > 1.1:
			_wave = -1.0
	for m in _crowd_mats:
		m.set_shader_parameter("celebrate", _celebrate)
		m.set_shader_parameter("wave", _wave)
	var keep: Array = []
	for f in _flares:
		f["t"] = float(f["t"]) - delta
		var mat: ShaderMaterial = f["mat"]
		var fl := 0.75 + 0.25 * sin(Time.get_ticks_msec() * 0.03 + float(f["t"]) * 7.0)
		mat.set_shader_parameter("color", Color(1.0, 0.25 + 0.15 * fl, 0.08) * clampf(float(f["t"]), 0.0, 1.0) * fl)
		if float(f["t"]) <= 0.0:
			(f["node"] as Node).queue_free()
		else:
			keep.append(f)
	_flares = keep


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
	_ball_mat = mat
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
		rigs[i].animate(p, b.pos, b.z, delta, b.owner == p.id)
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
	_update_crowd(delta)
	if weather_fx != null:
		weather_fx.update(delta)
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
		size = get_viewport().get_visible_rect().size
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
