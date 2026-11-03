## Atleta 3D articulado, montado por código (sem modelos externos) e animado
## de forma procedural a partir do estado da simulação: passada proporcional
## à velocidade, inclinação na arrancada e nas curvas, balanço de braços,
## chute com o pé certo, carrinho, queda, mergulho do goleiro, cabeceio.
class_name PlayerRig
extends Node3D

const HIP_Y := 0.95

var pid: int = 0
var lefty: bool = false

# ossos (pivôs)
var hips: Node3D
var spine: Node3D
var neck: Node3D
var thigh: Array[Node3D] = []
var shin: Array[Node3D] = []
var foot: Array[Node3D] = []
var upper_arm: Array[Node3D] = []
var forearm: Array[Node3D] = []
var number_label: Label3D

# estado da animação (só visual)
var phase: float = 0.0
var _yaw: float = 0.0
var _prev_vel: Vector2 = Vector2.ZERO
var _prev_dir: float = 0.0
var _lean_f: float = 0.0
var _bank: float = 0.0
var _kick_t: float = 0.0
var _kicking: bool = false
var _windup_total: float = 0.15
var _prev_windup: float = 0.0
var _kick_side: int = 1      # 1 = direito, 0 = esquerdo
var _kick_power: float = 0.5
var _prev_kick_anim: float = 0.0
var _prev_tackle: float = 0.0
var _tackle_t: float = -1.0
var _head_t: float = -1.0
var _breath: float = 0.0


static var _meshes: Dictionary = {}


static func _capsule(r: float, h: float) -> CapsuleMesh:
	var key := "c%.3f_%.3f" % [r, h]
	if not _meshes.has(key):
		var m := CapsuleMesh.new()
		m.radius = r
		m.height = h
		m.radial_segments = 12
		m.rings = 4
		_meshes[key] = m
	return _meshes[key]


static func _sphere(r: float) -> SphereMesh:
	var key := "s%.3f" % r
	if not _meshes.has(key):
		var m := SphereMesh.new()
		m.radius = r
		m.height = r * 2.0
		m.radial_segments = 16
		m.rings = 8
		_meshes[key] = m
	return _meshes[key]


static func _box(s: Vector3) -> BoxMesh:
	var key := "b%s" % s
	if not _meshes.has(key):
		var m := BoxMesh.new()
		m.size = s
		_meshes[key] = m
	return _meshes[key]


func _part(parent: Node3D, mesh: Mesh, mat: Material, pos: Vector3, scl: Vector3 = Vector3.ONE, rot: Vector3 = Vector3.ZERO) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.position = pos
	mi.scale = scl
	mi.rotation = rot
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(mi)
	return mi


func _pivot(parent: Node3D, pos: Vector3) -> Node3D:
	var n := Node3D.new()
	n.position = pos
	parent.add_child(n)
	return n


## kit: {shirt, shorts, socks, skin, hair, hair_style, number, keeper, gloves}
func build(p: SimPlayer, kit: Dictionary) -> void:
	pid = p.id
	lefty = (p.face_id % 5) == 0
	var shirt: Color = kit["shirt"]
	var shorts: Color = kit["shorts"]
	var socks: Color = kit["socks"]
	var skin: Color = kit["skin"]
	var hair: Color = kit["hair"]
	var boots := Color("#16181d")
	var m_shirt := Toon.mat(shirt)
	var m_shorts := Toon.mat(shorts)
	var m_socks := Toon.mat(socks)
	var m_skin := Toon.mat(skin)
	var m_hair := Toon.mat(hair, true, 0.12)
	var m_boot := Toon.mat(boots, true, 0.3, 0.6)
	var m_hand := Toon.mat(kit.get("gloves", skin))
	var scale_h := 0.96 + 0.08 * float(p.face_id % 7) / 6.0
	scale = Vector3.ONE * scale_h

	hips = _pivot(self, Vector3(0, HIP_Y, 0))
	# calção
	_part(hips, _capsule(0.16, 0.42), m_shorts, Vector3(0, -0.04, 0), Vector3(1.0, 0.62, 0.72), Vector3(0, 0, PI / 2))
	# pernas
	for s in 2:
		var sx := -0.1 if s == 0 else 0.1
		var th := _pivot(hips, Vector3(sx, -0.04, 0))
		_part(th, _capsule(0.1, 0.28), m_shorts, Vector3(0, -0.1, 0))
		_part(th, _capsule(0.086, 0.46), m_skin, Vector3(0, -0.22, 0), Vector3(1.0, 1.0, 1.08))
		var sh := _pivot(th, Vector3(0, -0.44, 0))
		_part(sh, _sphere(0.07), m_skin, Vector3.ZERO)
		_part(sh, _capsule(0.07, 0.44), m_socks, Vector3(0, -0.21, 0.008), Vector3(1.0, 1.0, 1.1))
		var ft := _pivot(sh, Vector3(0, -0.42, 0))
		_part(ft, _capsule(0.05, 0.25), m_boot, Vector3(0, -0.025, -0.05), Vector3(1.0, 1.0, 0.85), Vector3(PI / 2, 0, 0))
		thigh.append(th)
		shin.append(sh)
		foot.append(ft)
	# tronco
	spine = _pivot(hips, Vector3(0, 0.04, 0))
	_part(spine, _capsule(0.17, 0.64), m_shirt, Vector3(0, 0.28, 0), Vector3(1.34, 1.0, 0.8))
	_part(spine, _sphere(0.2), m_shirt, Vector3(0, 0.42, 0), Vector3(1.15, 0.6, 0.72))
	# número nas costas
	number_label = Label3D.new()
	number_label.text = str(p.number)
	number_label.font_size = 64
	number_label.outline_size = 10
	number_label.pixel_size = 0.0042
	number_label.modulate = kit.get("number_color", Color.WHITE)
	number_label.outline_modulate = Color(0, 0, 0, 0.6)
	number_label.position = Vector3(0, 0.33, 0.135)
	number_label.double_sided = false
	number_label.shaded = false
	spine.add_child(number_label)
	# braços
	for s in 2:
		var sx := -0.235 if s == 0 else 0.235
		var ua := _pivot(spine, Vector3(sx, 0.5, 0))
		var m_arm: Material = m_skin if not kit.get("long_sleeves", false) else m_shirt
		_part(ua, _sphere(0.078), m_shirt, Vector3(0, -0.01, 0))
		_part(ua, _capsule(0.072, 0.2), m_shirt, Vector3(0, -0.07, 0))
		_part(ua, _capsule(0.058, 0.3), m_arm, Vector3(0, -0.14, 0))
		var fa := _pivot(ua, Vector3(0, -0.28, 0))
		_part(fa, _sphere(0.054), m_arm, Vector3.ZERO)
		_part(fa, _capsule(0.051, 0.27), m_arm, Vector3(0, -0.12, 0))
		_part(fa, _sphere(0.052), m_hand, Vector3(0, -0.27, 0))
		upper_arm.append(ua)
		forearm.append(fa)
	# pescoço + cabeça
	neck = _pivot(spine, Vector3(0, 0.56, 0))
	_part(neck, _capsule(0.055, 0.14), m_skin, Vector3(0, 0.04, 0))
	var head := _pivot(neck, Vector3(0, 0.15, 0))
	_part(head, _sphere(0.115), m_skin, Vector3.ZERO, Vector3(0.92, 1.1, 1.0))
	# orelhas
	_part(head, _sphere(0.03), m_skin, Vector3(-0.105, 0.0, 0.01), Vector3(0.6, 1.0, 1.0))
	_part(head, _sphere(0.03), m_skin, Vector3(0.105, 0.0, 0.01), Vector3(0.6, 1.0, 1.0))
	# olhos e sobrancelhas (lido de longe como rosto)
	var m_eye := Toon.mat(Color("#14100e"), false, 0.0)
	for ex: float in [-0.04, 0.04]:
		_part(head, _sphere(0.016), m_eye, Vector3(ex, 0.015, -0.103))
		_part(head, _box(Vector3(0.04, 0.009, 0.01)), Toon.mat(hair, false, 0.0), Vector3(ex, 0.048, -0.104))
	_part(head, _sphere(0.02), m_skin, Vector3(0, -0.02, -0.112), Vector3(0.8, 1.0, 1.0))
	var style: String = kit.get("hair_style", "short")
	match style:
		"bald":
			pass
		"buzz":
			_part(head, _sphere(0.118), m_hair, Vector3(0, 0.03, 0.012), Vector3(0.94, 0.95, 0.98))
		"afro":
			_part(head, _sphere(0.17), m_hair, Vector3(0, 0.07, 0.03))
		"long", "dreads":
			_part(head, _sphere(0.125), m_hair, Vector3(0, 0.035, 0.018), Vector3(0.98, 1.0, 1.0))
			_part(head, _box(Vector3(0.22, 0.24, 0.07)), m_hair, Vector3(0, -0.08, 0.08))
		"mohawk":
			_part(head, _box(Vector3(0.04, 0.08, 0.22)), m_hair, Vector3(0, 0.11, 0.02))
		"bun":
			_part(head, _sphere(0.122), m_hair, Vector3(0, 0.03, 0.015), Vector3(0.97, 1.0, 1.0))
			_part(head, _sphere(0.055), m_hair, Vector3(0, 0.1, 0.09))
		"curly":
			_part(head, _sphere(0.135), m_hair, Vector3(0, 0.045, 0.02), Vector3(1.0, 0.95, 1.0))
		_:
			_part(head, _sphere(0.123), m_hair, Vector3(0, 0.035, 0.016), Vector3(0.97, 0.98, 1.0))
	_yaw = atan2(-p.facing.x, -p.facing.y)
	rotation.y = _yaw


## Pé do chute: o do lado da bola (ou o preferido, se a bola estiver no meio).
func _foot_for(side_ball: float) -> int:
	if absf(side_ball) <= 0.06:
		return 0 if lefty else 1
	return 1 if side_ball > 0.0 else 0


static func _wrap(a: float) -> float:
	return wrapf(a, -PI, PI)


func _rot(n: Node3D, x: float, y: float = 0.0, z: float = 0.0) -> void:
	n.rotation = Vector3(x, y, z)


## Atualiza pose a partir do atleta da simulação. `ball_rel` = bola em
## relação ao atleta (metros, plano do chão).
func animate(p: SimPlayer, ball_pos: Vector2, ball_z: float, dt: float) -> void:
	position = Vector3(p.pos.x, 0.0, p.pos.y)
	var target_yaw := atan2(-p.facing.x, -p.facing.y)
	_yaw = _yaw + _wrap(target_yaw - _yaw) * minf(1.0, dt * 14.0)
	rotation = Vector3(0, _yaw, 0)

	var spd := p.vel.length()
	var sn := clampf(spd / 8.5, 0.0, 1.0)
	# aceleração frontal e lateral (para inclinar o corpo)
	var acc := (p.vel - _prev_vel) / maxf(dt, 0.001)
	_prev_vel = p.vel
	var fwd := p.facing.normalized() if p.facing.length() > 0.01 else Vector2.RIGHT
	var a_f := acc.dot(fwd)
	var a_l := acc.dot(Vector2(-fwd.y, fwd.x))
	_lean_f = lerpf(_lean_f, clampf(a_f * 0.025, -0.25, 0.3), minf(1.0, dt * 6.0))
	_bank = lerpf(_bank, clampf(a_l * 0.035, -0.4, 0.4) * sn, minf(1.0, dt * 6.0))

	# passada: comprimento cresce com a velocidade
	var step_len := lerpf(0.55, 1.95, sn)
	phase = fmod(phase + PI * spd / step_len * dt, TAU)
	_breath += dt * (2.0 + sn * 3.0)

	# gatilhos de ação (bordas dos timers da simulação)
	var rel := ball_pos - p.pos
	var side_ball := rel.dot(Vector2(-fwd.y, fwd.x))
	if p.windup > 0.0 and _prev_windup <= 0.0:
		# começou a armar o chute: a bola sai quando o pé chegar nela
		_kicking = true
		_kick_t = -p.windup
		_windup_total = p.windup
		_kick_side = _foot_for(side_ball)
		_kick_power = clampf(p.windup_power, 0.25, 1.0) if p.windup_kind == InputFrame.SHOOT else 0.35
	_prev_windup = p.windup
	if p.kick_anim > _prev_kick_anim + 0.1 and (not _kicking or _kick_t > 0.12):
		_kicking = true
		_kick_t = 0.0
		_kick_side = _foot_for(side_ball)
		_kick_power = 0.4
		if ball_z > 1.2:
			_head_t = 0.0
			_kicking = false
	_prev_kick_anim = p.kick_anim
	if p.tackle_anim > _prev_tackle + 0.1:
		_tackle_t = 0.0
	_prev_tackle = p.tackle_anim

	var root_y := 0.0
	var root_pitch := 0.0
	var root_roll := _bank

	# ---------- base: corrida / parado
	var amp := lerpf(0.12, 0.95, sn) if spd > 0.15 else 0.0
	var knee_amp := lerpf(0.25, 1.9, sn)
	var leg_x: Array[float] = [0.0, 0.0]
	var knee_x: Array[float] = [0.0, 0.0]
	var foot_x: Array[float] = [0.0, 0.0]
	var leg_z_extra: Array[float] = [0.0, 0.0]
	for s in 2:
		var ph := phase + (PI if s == 1 else 0.0)
		leg_x[s] = amp * sin(ph) + 0.08 * sn
		knee_x[s] = -(0.12 + knee_amp * maxf(0.0, cos(ph)) * (0.35 + 0.65 * sn) + 0.25 * sn * maxf(0.0, -sin(ph)))
		foot_x[s] = -0.6 * (leg_x[s] + knee_x[s]) * 0.5 + 0.1
	if spd <= 0.15:
		# posição de expectativa: joelhos levemente flexionados
		for s in 2:
			leg_x[s] = 0.14
			knee_x[s] = -0.26
			foot_x[s] = 0.12
	var bob := 0.045 * sn * absf(cos(phase))
	var hip_y := HIP_Y - 0.025 - 0.05 * sn + bob + sin(_breath) * 0.004
	var spine_x := -(0.06 + 0.32 * sn) - _lean_f
	var spine_y := 0.18 * sn * sin(phase)
	var arm_x: Array[float] = [-0.75 * amp * sin(phase + PI) * 1.1, -0.75 * amp * sin(phase) * 1.1]
	for s in 2:
		arm_x[s] = 0.75 * amp * sin(phase + (0.0 if s == 1 else PI))
	var arm_z: Array[float] = [-0.12 - 0.05 * sn, 0.12 + 0.05 * sn]
	var elbow_x: Array[float] = [0.35 + 1.0 * sn, 0.35 + 1.0 * sn]
	var neck_x := 0.12 + 0.2 * sn

	# goleiro: postura de base
	if p.is_keeper and spd < 3.0 and p.dive <= 0.0 and p.hold <= 0.0:
		for s in 2:
			leg_x[s] = maxf(leg_x[s], 0.45)
			knee_x[s] = minf(knee_x[s], -0.8)
			foot_x[s] = 0.35
		hip_y -= 0.14
		spine_x = -0.35
		arm_z = [-0.55, 0.55]
		arm_x = [0.45, 0.45]
		elbow_x = [0.6, 0.6]

	# ---------- chute (armação → contato em t=0 → finalização)
	if _kicking:
		_kick_t += dt
		var k := _kick_side
		var o := 1 - k
		var t := _kick_t
		var dur := 0.42
		if t > dur:
			_kicking = false
		else:
			var swing := 0.0
			var knee := 0.0
			if t < 0.0:
				# perna vai para trás, pé de apoio planta ao lado da bola
				var u := clampf(1.0 + t / maxf(_windup_total, 0.01), 0.0, 1.0)
				var e := u * u * (3.0 - 2.0 * u)
				swing = lerpf(leg_x[k], -0.55 - 0.35 * _kick_power, e)
				knee = lerpf(knee_x[k], -1.5, e)
				spine_x = lerpf(spine_x, -0.12, e)
			elif t < 0.06:
				swing = lerpf(-0.5, 0.9, t / 0.06)
				knee = lerpf(-1.3, -0.15, t / 0.06)
			elif t < 0.2:
				var u := (t - 0.06) / 0.14
				swing = lerpf(0.9, 0.9 + 0.7 * _kick_power, sin(u * PI * 0.5))
				knee = lerpf(-0.15, -0.35, u)
			else:
				var u := (t - 0.2) / (dur - 0.2)
				swing = lerpf(0.9 + 0.7 * _kick_power, leg_x[k], u)
				knee = lerpf(-0.35, knee_x[k], u)
			leg_x[k] = swing
			knee_x[k] = knee
			foot_x[k] = -0.3 if t >= 0.0 else 0.2
			leg_x[o] = -0.08
			knee_x[o] = -0.32
			foot_x[o] = 0.1
			if t >= 0.0:
				spine_x = 0.12 * _kick_power - 0.05
			spine_y = (0.25 if k == 1 else -0.25) * (1.0 - absf(t) / dur)
			arm_z = [-0.75, 0.75]
			arm_x[o] = 0.6
			arm_x[k] = -0.3
			hip_y -= 0.03

	# ---------- dribles (analógico direito)
	if p.skill != SimPlayer.Skill.NONE and p.skill != SimPlayer.Skill.KNOCK:
		var dur_s: float = SimPlayer.SKILL_TIME[p.skill]
		var u := clampf(p.skill_t / dur_s, 0.0, 1.0)
		var k := 1 if p.skill_side < 0.0 else 0
		var o := 1 - k
		match p.skill:
			SimPlayer.Skill.DRAG:
				# sola na bola puxando para trás
				leg_x[k] = lerpf(0.55, -0.1, u)
				knee_x[k] = lerpf(-0.25, -0.9, u)
				foot_x[k] = 0.55
				leg_x[o] = -0.05
				knee_x[o] = -0.45
				spine_x = 0.05
				arm_z = [-0.6, 0.6]
			SimPlayer.Skill.ROLL:
				leg_x[k] = 0.35
				knee_x[k] = -0.5
				foot_x[k] = 0.4
				leg_z_extra[k] = (0.45 if k == 0 else -0.45) * sin(u * PI)
				arm_z = [-0.55, 0.55]
				spine_x = -0.15
			SimPlayer.Skill.STEPOVER:
				# perna passa por cima da bola em arco
				var e := sin(u * PI)
				leg_x[k] = 0.25 + 0.45 * e
				knee_x[k] = -0.4 - 1.0 * e
				leg_z_extra[k] = (-0.55 if k == 0 else 0.55) * sin(u * TAU)
				leg_x[o] = 0.0
				knee_x[o] = -0.4
				root_roll = (0.18 if k == 0 else -0.18) * sin(u * TAU)
				spine_y = 0.35 * sin(u * TAU)
				arm_z = [-0.7, 0.7]
				hip_y -= 0.05
			SimPlayer.Skill.ROULETTE:
				leg_x[k] = 0.3
				knee_x[k] = -0.6
				foot_x[k] = 0.5
				leg_x[o] = -0.1
				knee_x[o] = -0.4
				arm_z = [-1.0, 1.0]
				arm_x = [0.2, 0.2]
				spine_x = -0.1
				hip_y -= 0.04

	# ---------- proteção / contenção / desequilíbrio / domínio
	if p.shielding and p.skill == SimPlayer.Skill.NONE and not _kicking and p.slide <= 0.0:
		hip_y -= 0.08
		arm_z = [-0.75, 0.75]
		arm_x = [0.15, 0.15]
		elbow_x = [0.5, 0.5]
		spine_x = -0.2 if spd < 2.0 else spine_x
		for s in 2:
			knee_x[s] = minf(knee_x[s], -0.45)
			leg_z_extra[s] = -0.12 if s == 0 else 0.12
	if p.off_balance > 0.0:
		var e := clampf(p.off_balance / 0.65, 0.0, 1.0)
		root_roll += 0.35 * e * (1.0 if int(p.id) % 2 == 0 else -1.0)
		arm_z = [-1.4 * e, 1.4 * e]
		spine_x -= 0.2 * e
	if p.touch_t > 0.0 and not _kicking and p.skill == SimPlayer.Skill.NONE:
		var e := sin(clampf(p.touch_t / 0.3, 0.0, 1.0) * PI)
		var k := 0 if lefty else 1
		leg_x[k] = lerpf(leg_x[k], 0.55, e)
		knee_x[k] = lerpf(knee_x[k], -0.5, e)
		foot_x[k] = lerpf(foot_x[k], 0.3, e)

	# ---------- cabeceio
	if _head_t >= 0.0:
		_head_t += dt
		var t := _head_t / 0.5
		if t >= 1.0:
			_head_t = -1.0
		else:
			root_y = sin(t * PI) * 0.35
			spine_x = lerpf(0.25, -0.45, clampf(t * 2.0, 0.0, 1.0))
			neck_x = lerpf(0.35, -0.3, clampf(t * 2.0, 0.0, 1.0))
			for s in 2:
				leg_x[s] = 0.25
				knee_x[s] = -0.7
			arm_z = [-1.0, 1.0]
			arm_x = [0.3, 0.3]

	# ---------- dividida em pé (bote)
	if _tackle_t >= 0.0 and p.slide <= 0.0:
		_tackle_t += dt
		var t := _tackle_t / 0.35
		if t >= 1.0:
			_tackle_t = -1.0
		else:
			var e := sin(t * PI)
			var k := 1 if not lefty else 0
			leg_x[k] = lerpf(leg_x[k], 1.15, e)
			knee_x[k] = lerpf(knee_x[k], -0.1, e)
			leg_x[1 - k] = lerpf(leg_x[1 - k], -0.2, e)
			knee_x[1 - k] = lerpf(knee_x[1 - k], -0.9, e)
			hip_y -= 0.22 * e
			spine_x = lerpf(spine_x, -0.1, e)
			arm_z = [-0.8, 0.8]

	# ---------- carrinho
	if p.slide > 0.0:
		var k := 1 if not lefty else 0
		root_pitch = 0.0
		hip_y = 0.32
		spine_x = 1.0
		leg_x[k] = 1.45
		knee_x[k] = -0.05
		foot_x[k] = 0.4
		leg_x[1 - k] = 1.1
		knee_x[1 - k] = -1.9
		arm_z = [-1.0, 1.0]
		arm_x = [-0.6, -0.6]
		neck_x = -0.6
	elif p.stun > 0.3 and p.tackle_anim <= 0.0:
		# caído no chão
		hip_y = 0.16
		root_pitch = -1.45
		root_y = 0.0
		for s in 2:
			leg_x[s] = 0.05
			knee_x[s] = -0.25 - 0.4 * s
		arm_z = [-1.2, 1.2]
		arm_x = [0.2, 0.2]
		root_roll = 0.0

	# ---------- goleiro: mergulho e bola presa
	if p.is_keeper and p.dive > 0.0:
		var lat := p.vel.dot(Vector2(-fwd.y, fwd.x))
		var dir_s := signf(lat) if absf(lat) > 0.2 else 1.0
		var e := clampf(p.dive / 0.6, 0.0, 1.0)
		var reach := sin((1.0 - e) * PI * 0.5 + 0.3)
		root_roll = -dir_s * 1.25 * reach
		root_y = 0.25 * sin((1.0 - e) * PI)
		hip_y = 0.8
		arm_z = [-2.8, 2.8]
		arm_x = [0.0, 0.0]
		elbow_x = [0.1, 0.1]
		spine_x = 0.0
		for s in 2:
			leg_x[s] = 0.1 + 0.25 * s
			knee_x[s] = -0.3
	elif p.is_keeper and p.hold > 0.0:
		arm_x = [1.25, 1.25]
		arm_z = [-0.1, 0.1]
		elbow_x = [1.2, 1.2]
		spine_x = -0.25

	# ---------- aplica
	hips.position.y = hip_y + root_y
	hips.rotation = Vector3(root_pitch, 0, 0)
	rotation = Vector3(0, _yaw, root_roll)
	_rot(spine, spine_x, spine_y, 0.0)
	_rot(neck, neck_x, -spine_y * 0.6, 0.0)
	for s in 2:
		_rot(thigh[s], leg_x[s], 0.0, (-0.04 if s == 0 else 0.04) + leg_z_extra[s])
		_rot(shin[s], knee_x[s])
		_rot(foot[s], foot_x[s])
		_rot(upper_arm[s], arm_x[s], 0.0, arm_z[s])
		_rot(forearm[s], elbow_x[s])
