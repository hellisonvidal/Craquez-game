## Atleta 3D: malha contínua com esqueleto (BodyMesh) e animação procedural
## dirigida pela simulação — passada com giro de quadril, arrancada inclinada,
## freada, corte com pé plantado, corrida lateral/de costas, cabeça seguindo
## a bola, chute com armação, dribles, proteção, carrinho, goleiro.
class_name PlayerRig
extends Node3D

const HIP_Y := 0.95
const B := BodyMesh.B

var pid: int = 0
var lefty: bool = false
var skel: Skeleton3D
var body: MeshInstance3D
var number_label: Label3D

# estado da animação (só visual)
var phase: float = 0.0
var _yaw: float = 0.0
var _prev_vel: Vector2 = Vector2.ZERO
var _prev_dir: float = 0.0
var _turn_rate: float = 0.0
var _lean_f: float = 0.0
var _bank: float = 0.0
var _cut: float = 0.0
var _head_yaw: float = 0.0
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
var _rest: Array[Vector3] = []
var _pose: Array[Quaternion] = []     # rotação atual de cada osso (suavizada)
var _blend_k: float = 1.0             # fração do caminho até a pose-alvo neste quadro
var _hip_y_s: float = HIP_Y
var _root_roll_s: float = 0.0
var _prev_yaw: float = 0.0


## kit: {shirt, shorts, socks, skin, hair, hair_style, number, keeper, gloves, beard, eye}
func build(p: SimPlayer, kit: Dictionary) -> void:
	pid = p.id
	lefty = (p.face_id % 5) == 0
	var build_k := float((p.face_id * 7 + p.number) % 3) * 0.5   # 0 magro, 0.5 médio, 1 forte
	if int(kit.get("build", -1)) >= 0:
		build_k = float(kit["build"]) * 0.5
	if p.is_keeper:
		build_k = 1.0
	scale = Vector3.ONE * (0.97 + 0.07 * float(p.face_id % 7) / 6.0)
	skel = BodyMesh.make_skeleton()
	add_child(skel)
	for i in skel.get_bone_count():
		_rest.append(skel.get_bone_rest(i).origin)
		_pose.append(Quaternion.IDENTITY)
	body = MeshInstance3D.new()
	body.mesh = BodyMesh.get_mesh(build_k, String(kit.get("hair_style", "short")))
	body.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	skel.add_child(body)
	body.skeleton = NodePath("..")
	var skin: Color = kit["skin"]
	var shirt_m := Toon.mat(kit["shirt"])
	var arm_m: Material = shirt_m if kit.get("long_sleeves", false) else Toon.mat(skin)
	var mats := {
		BodyMesh.S.SHIRT: shirt_m,
		BodyMesh.S.SLEEVES: shirt_m,
		BodyMesh.S.SHORTS: Toon.mat(kit["shorts"]),
		BodyMesh.S.SKIN: Toon.mat(skin),
		BodyMesh.S.SOCKS: Toon.mat(kit["socks"]),
		BodyMesh.S.BOOTS: Toon.mat(kit.get("boots", Color("#16181d")), true, 0.3, 0.6),
		BodyMesh.S.HAIR: Toon.mat(kit["hair"], true, 0.15),
		BodyMesh.S.ARMS: arm_m,
		BodyMesh.S.HANDS: Toon.mat(kit.get("gloves", skin)),
		BodyMesh.S.HEAD: _face_mat(kit),
	}
	for sidx in mats:
		if sidx < 10:
			body.set_surface_override_material(sidx, mats[sidx])
	# número nas costas, preso ao osso do peito
	var att := BoneAttachment3D.new()
	att.bone_name = "chest"
	skel.add_child(att)
	number_label = Label3D.new()
	number_label.text = str(p.number)
	number_label.font_size = 72
	number_label.outline_size = 8
	number_label.pixel_size = 0.0034
	number_label.modulate = kit.get("number_color", Color.WHITE)
	number_label.outline_modulate = Color(0, 0, 0, 0.5)
	number_label.position = Vector3(0, 0.06, 0.133)
	number_label.double_sided = false
	number_label.shaded = false
	att.add_child(number_label)
	_yaw = atan2(-p.facing.x, -p.facing.y)
	rotation.y = _yaw


static var _face_cache: Dictionary = {}


static func _face_mat(kit: Dictionary) -> ShaderMaterial:
	var key := "%s|%s|%s|%.1f|%s" % [(kit["skin"] as Color).to_html(), (kit["hair"] as Color).to_html(),
		(kit.get("eye", Color("#5b3a1e")) as Color).to_html(), float(kit.get("beard", 0.0)), Settings.detail.get("outline", true)]
	if _face_cache.has(key):
		return _face_cache[key]
	var m := ShaderMaterial.new()
	m.shader = preload("res://shaders/face.gdshader")
	m.set_shader_parameter("skin", kit["skin"])
	m.set_shader_parameter("hair_col", kit["hair"])
	m.set_shader_parameter("eye_col", kit.get("eye", Color("#5b3a1e")))
	m.set_shader_parameter("beard", float(kit.get("beard", 0.0)))
	if Settings.detail.get("outline", true):
		m.next_pass = Toon.outline_mat()
	_face_cache[key] = m
	return m


## Pé do chute: o do lado da bola (ou o preferido, se a bola estiver no meio).
func _foot_for(side_ball: float) -> int:
	if absf(side_ball) <= 0.06:
		return 0 if lefty else 1
	return 1 if side_ball > 0.0 else 0


static func _wrap(a: float) -> float:
	return wrapf(a, -PI, PI)


## Leva o osso em direção à pose-alvo (suaviza entradas/saídas de chute, drible, queda).
func _bone(i: int, x: float, y: float = 0.0, z: float = 0.0) -> void:
	var target := Quaternion.from_euler(Vector3(x, y, z))
	_pose[i] = _pose[i].slerp(target, _blend_k)
	skel.set_bone_pose_rotation(i, _pose[i])


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
	_lean_f = lerpf(_lean_f, clampf(a_f * 0.03, -0.3, 0.32), minf(1.0, dt * 7.0))
	_bank = lerpf(_bank, clampf(a_l * 0.04, -0.45, 0.45) * sn, minf(1.0, dt * 7.0))
	# taxa de giro da direção da corrida → corte
	if spd > 2.5:
		var d := p.vel.angle()
		_turn_rate = lerpf(_turn_rate, absf(_wrap(d - _prev_dir)) / maxf(dt, 0.001), minf(1.0, dt * 10.0))
		_prev_dir = d
	else:
		_turn_rate = lerpf(_turn_rate, 0.0, minf(1.0, dt * 6.0))
		_prev_dir = p.vel.angle() if spd > 0.3 else _prev_dir
	_cut = lerpf(_cut, clampf((_turn_rate - 1.6) / 3.0, 0.0, 1.0) * clampf(spd / 5.0, 0.0, 1.0), minf(1.0, dt * 9.0))

	# passada: comprimento cresce com a velocidade
	# comprimento do passo = quanto o pé realmente percorre com a amplitude atual
	# da coxa (perna ~0,9 m): o pé de apoio fica "plantado", sem patinar
	var amp_now := lerpf(0.16, 1.0, sn) if spd > 0.15 else 0.0
	var step_len := maxf(0.35, 2.0 * 0.9 * sin(amp_now) * 0.92 + 0.25 * sn) * (1.0 - 0.25 * _cut)
	# girando parado: passinhos para acompanhar o corpo
	var yaw_rate := absf(_wrap(_yaw - _prev_yaw)) / maxf(dt, 0.001)
	_prev_yaw = _yaw
	if spd < 1.0 and yaw_rate > 1.5:
		phase = fmod(phase + yaw_rate * 1.2 * dt, TAU)
	phase = fmod(phase + PI * spd / step_len * dt, TAU)
	# rápido o bastante para não amortecer a passada (~3 Hz), lento o bastante para não "estalar"
	_blend_k = 1.0 - exp(-dt * 34.0)
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

	# ---------- base: locomoção
	# velocidade no referencial do corpo: frente / lado (corrida de lado e de costas)
	var right := Vector2(-fwd.y, fwd.x)
	var v_f := p.vel.dot(fwd)
	var v_r := p.vel.dot(right)
	var fwd_k := v_f / maxf(spd, 0.01)
	var side_k := v_r / maxf(spd, 0.01)
	var amp := lerpf(0.16, 1.0, sn) if spd > 0.15 else 0.0
	var knee_amp := lerpf(0.35, 2.0, sn)
	var leg_x: Array[float] = [0.0, 0.0]
	var knee_x: Array[float] = [0.0, 0.0]
	var foot_x: Array[float] = [0.0, 0.0]
	var leg_z_extra: Array[float] = [0.0, 0.0]
	for s in 2:
		var ph := phase + (PI if s == 1 else 0.0)
		var swing_v := sin(ph)
		# perna de trás estica (impulsão), a da frente recolhe e estende antes de pisar
		leg_x[s] = amp * swing_v * fwd_k + 0.1 * sn * fwd_k
		knee_x[s] = -(0.1 + knee_amp * maxf(0.0, cos(ph)) * (0.3 + 0.7 * sn) + 0.3 * sn * maxf(0.0, -swing_v))
		foot_x[s] = clampf(-(leg_x[s] + knee_x[s]) * 0.55, -0.6, 0.6) + (0.35 * sn * maxf(0.0, -swing_v))
		# passos laterais: pernas abrem/fecham juntas
		leg_z_extra[s] = 0.45 * amp * side_k * swing_v * (1.0 if s == 0 else -1.0) * 0.5 + 0.18 * side_k * amp
	if spd <= 0.15:
		# expectativa: joelhos flexionados, peso alternando
		var sway := sin(_breath * 0.4) * 0.04
		for s in 2:
			leg_x[s] = 0.12 + (sway if s == 0 else -sway)
			knee_x[s] = -0.24
			foot_x[s] = 0.12
	var bob := 0.05 * sn * absf(cos(phase))
	var hip_y := HIP_Y - 0.02 - 0.06 * sn + bob + sin(_breath) * 0.004
	# arrancada inclina para frente, freada joga o corpo para trás
	var spine_x := -(0.05 + 0.3 * sn * maxf(0.0, fwd_k)) - _lean_f
	var spine_y := 0.22 * sn * sin(phase) * fwd_k
	var hip_yaw := -0.16 * sn * sin(phase) * fwd_k
	var arm_x: Array[float] = [0.0, 0.0]
	for s in 2:
		arm_x[s] = 0.85 * amp * sin(phase + (0.0 if s == 1 else PI)) * (0.4 + 0.6 * absf(fwd_k))
	var arm_z: Array[float] = [-0.1 - 0.06 * sn - 0.25 * absf(side_k) * sn, 0.1 + 0.06 * sn + 0.25 * absf(side_k) * sn]
	var elbow_x: Array[float] = [0.3 + 1.25 * sn, 0.3 + 1.25 * sn]
	var neck_x := 0.08 + 0.22 * sn * maxf(0.0, fwd_k)
	# corte / mudança brusca de direção: abaixa, inclina e planta o pé de fora
	if _cut > 0.05:
		hip_y -= 0.1 * _cut
		var outside := 0 if _bank > 0.0 else 1
		leg_z_extra[outside] += (-0.4 if outside == 0 else 0.4) * _cut
		knee_x[outside] = minf(knee_x[outside], -0.5 * _cut)
		knee_x[1 - outside] = minf(knee_x[1 - outside], -0.7 * _cut)
		arm_z[outside] += (-0.5 if outside == 0 else 0.5) * _cut
		spine_x -= 0.15 * _cut
	# freada forte: corpo para trás, pernas à frente
	if _lean_f < -0.12 and spd > 1.0:
		var b := clampf(-_lean_f / 0.25, 0.0, 1.0)
		for s in 2:
			leg_x[s] = lerpf(leg_x[s], 0.35, b * 0.6)
			knee_x[s] = minf(knee_x[s], -0.45 * b)
		hip_y -= 0.06 * b
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

	# ---------- cabeça acompanha a bola
	var to_ball := ball_pos - p.pos
	var look := 0.0
	if to_ball.length() > 0.5:
		look = clampf(-atan2(to_ball.dot(right), to_ball.dot(fwd)), -1.1, 1.1)
	_head_yaw = lerpf(_head_yaw, look, minf(1.0, dt * 6.0))
	var look_down := clampf(0.25 - to_ball.length() * 0.02, 0.0, 0.25) if ball_z < 1.0 else -0.15

	# ---------- aplica
	_hip_y_s = lerpf(_hip_y_s, hip_y + root_y, _blend_k)
	_root_roll_s = lerp_angle(_root_roll_s, root_roll, _blend_k)
	skel.set_bone_pose_position(B.HIPS, Vector3(0, _hip_y_s, 0))
	_bone(B.HIPS, root_pitch, hip_yaw, 0.0)
	rotation = Vector3(0, _yaw, _root_roll_s)
	_bone(B.SPINE, spine_x * 0.5, spine_y * 0.5 - hip_yaw * 0.5)
	_bone(B.CHEST, spine_x * 0.5, spine_y * 0.5 - hip_yaw * 0.5)
	# +X inclina a cabeça para trás: compensa a inclinação do tronco; olhar para baixo é negativo
	var nx := clampf(neck_x * 0.5 - spine_x * 0.3, -0.3, 0.16) - look_down
	_bone(B.NECK, nx * 0.5, _head_yaw * 0.4 - spine_y * 0.4)
	_bone(B.HEAD, nx * 0.5, _head_yaw * 0.6 - spine_y * 0.2)
	var th: Array[int] = [B.THIGH_L, B.THIGH_R]
	var sh: Array[int] = [B.SHIN_L, B.SHIN_R]
	var ft: Array[int] = [B.FOOT_L, B.FOOT_R]
	var ua: Array[int] = [B.UARM_L, B.UARM_R]
	var fa: Array[int] = [B.FARM_L, B.FARM_R]
	for s in 2:
		_bone(th[s], leg_x[s], -hip_yaw * 0.3, (-0.03 if s == 0 else 0.03) + leg_z_extra[s])
		_bone(sh[s], knee_x[s])
		_bone(ft[s], foot_x[s])
		_bone(ua[s], arm_x[s], 0.0, arm_z[s])
		_bone(fa[s], elbow_x[s], (0.25 if s == 0 else -0.25) * minf(1.0, elbow_x[s]))
