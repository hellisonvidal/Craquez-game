## Partida em 3D com visual "desenhado": campo com linhas em shader, gols com
## rede, estádio/ginásio/quadra de rua, atletas articulados, câmera de TV.
## Só LÊ a simulação (MatchSim): nada aqui altera o jogo.
class_name MatchView3D
extends Node3D

const LIGHT_DIR := Vector3(-0.35, 0.85, 0.45)

var sim: MatchSim
var team_colors: Array = [[Color.RED, Color.WHITE], [Color.BLUE, Color.WHITE]]
var human_team: Array[bool] = [true, true]
var cam: Camera3D
var rigs: Array[PlayerRig] = []
var ball_node: Node3D
var ball_mesh: MeshInstance3D
var _ball_shadow: MeshInstance3D
var _shadows: Array[MeshInstance3D] = []
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
	_build_players()
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
	if surface == "wood":
		env.background_mode = Environment.BG_COLOR
		env.background_color = Color("#0b1220")
	else:
		env.background_mode = Environment.BG_SKY
		var sky := Sky.new()
		var sm := ProceduralSkyMaterial.new()
		if surface == "grass":
			sm.sky_top_color = Color("#3d6fb8")
			sm.sky_horizon_color = Color("#a9c8e8")
		else:
			sm.sky_top_color = Color("#f59e0b").lerp(Color("#7c3aed"), 0.6)
			sm.sky_horizon_color = Color("#fdba74")
		sm.ground_bottom_color = Color("#1f2937")
		sm.ground_horizon_color = Color("#64748b")
		sky.sky_material = sm
		env.sky = sky
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)


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


func _crowd_mat(cols: Vector2) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = preload("res://shaders/crowd.gdshader")
	m.set_shader_parameter("seats", cols)
	m.set_shader_parameter("team_a", team_colors[0][0])
	m.set_shader_parameter("team_b", team_colors[1][0])
	_crowd_mats.append(m)
	return m


## Plano inclinado de arquibancada: começa em `start` (pé), sobe para fora.
func _stand(center: Vector3, length: float, depth: float, rise: float, yaw: float, seats_per_m: float) -> void:
	var pm := PlaneMesh.new()
	var slope := sqrt(depth * depth + rise * rise)
	pm.size = Vector2(length, slope)
	var m := _crowd_mat(Vector2(length * seats_per_m, slope * 1.6)) if Settings.detail["crowd"] else null
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
	# placas de publicidade
	var board_col := Color("#0f172a")
	for s: float in [-1.0, 1.0]:
		_box(Vector3(hl * 2.0 + 10.0, 0.9, 0.2), board_col, Vector3(0, 0.45, s * (hw + 4.0)))
		_box(Vector3(0.2, 0.9, hw * 2.0 + 8.0), board_col, Vector3(s * (hl + 5.0), 0.45, 0))
	var ads := ["CRAQUEZ", "FUTEBOL DE RUA", "CRAQUEZ", "JOGUE LIMPO", "CRAQUEZ"]
	if Settings.detail["crowd"]:
		# só o lado de lá fica de frente para a câmera
		for i in 9:
			var l := Label3D.new()
			l.text = ads[i % ads.size()]
			l.font_size = 96
			l.pixel_size = 0.0075
			l.modulate = Color("#facc15") if i % 2 == 0 else Color("#38bdf8")
			l.outline_size = 0
			l.shaded = false
			l.double_sided = false
			l.position = Vector3(-hl + 4.0 + i * (hl * 2.0 - 8.0) / 8.0, 0.47, -(hw + 4.0) + 0.11)
			add_child(l)
	# arquibancadas (lado de lá, fundos e um degrau baixo do lado da câmera)
	var base_y := 1.4
	_box(Vector3(hl * 2.0 + 30.0, base_y, 1.0), Color("#1e293b"), Vector3(0, base_y * 0.5, -(hw + 7.5)), false)
	_stand(Vector3(0, base_y, -(hw + 8.0)), hl * 2.0 + 30.0, 22.0, 15.0, 0.0, 1.6)
	_stand(Vector3(-(hl + 9.0), base_y, 0), hw * 2.0 + 16.0, 16.0, 11.0, PI / 2, 1.6)
	_stand(Vector3(hl + 9.0, base_y, 0), hw * 2.0 + 16.0, 16.0, 11.0, -PI / 2, 1.6)
	_stand(Vector3(0, 0.6, hw + 8.0), hl * 2.0 + 30.0, 6.0, 3.0, PI, 1.6)
	# cobertura do lado de lá
	_box(Vector3(hl * 2.0 + 34.0, 0.6, 14.0), Color("#cbd5e1"), Vector3(0, 23.0, -(hw + 25.0)), true)
	# refletores
	for sx: float in [-1.0, 1.0]:
		for sz: float in [-1.0, 1.0]:
			var p := Vector3(sx * (hl + 14.0), 0, sz * (hw + 18.0))
			var mast := CylinderMesh.new()
			mast.top_radius = 0.3
			mast.bottom_radius = 0.5
			mast.height = 34.0
			_mesh(mast, Toon.mat(Color("#94a3b8"), true), p + Vector3(0, 17.0, 0))
			_box(Vector3(5.0, 3.0, 0.6), Color("#f1f5f9"), p + Vector3(0, 34.5, 0))


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


func _build_players() -> void:
	var shadow_mat := ShaderMaterial.new()
	shadow_mat.shader = preload("res://shaders/blob_shadow.gdshader")
	var quad := QuadMesh.new()
	quad.size = Vector2(1.0, 1.0)
	for p in sim.players:
		var m: Dictionary = FaceLibrary.meta(p.face_id)
		var cols: Array = team_colors[p.team]
		var shirt: Color = cols[0]
		var second: Color = cols[1]
		var kit := {
			"shirt": shirt, "shorts": second, "socks": shirt,
			"skin": Color(String(m.get("skin_hex", "#c98e62"))),
			"hair": Color(String(m.get("hair_hex", "#1a1210"))),
			"hair_style": String(m.get("hair_style", "short")),
			"number_color": second if second.get_luminance() > 0.5 or shirt.get_luminance() < 0.5 else Color("#0f172a"),
		}
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
		if Settings.detail["shadows"]:
			var sh := MeshInstance3D.new()
			sh.mesh = quad
			sh.material_override = shadow_mat
			sh.rotation.x = -PI / 2
			sh.scale = Vector3(1.0, 0.75, 1.0)
			add_child(sh)
			_shadows.append(sh)


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
			var sh := _shadows[i]
			var lying := p.slide > 0.0 or p.stun > 0.3
			sh.position = Vector3(p.pos.x + 0.25, 0.015, p.pos.y - 0.2)
			sh.scale = Vector3(1.9 if lying else 1.15, 0.9 if lying else 0.8, 1.0)
			sh.rotation = Vector3(-PI / 2, rigs[i].rotation.y, 0)
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
