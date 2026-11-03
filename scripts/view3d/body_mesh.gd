## Gera, por código, a malha contínua de um atleta (pele única com esqueleto),
## com proporções reais (~1,83 m, cabeça = 1/7,5 da altura), músculos nas
## coxas/panturrilhas/braços, chuteira modelada e cabeça com rosto em shader.
## Sem modelos externos: tudo é paramétrico (porte físico + estilo de cabelo).
class_name BodyMesh
extends RefCounted

enum B { ROOT, HIPS, SPINE, CHEST, NECK, HEAD, THIGH_L, SHIN_L, FOOT_L, THIGH_R, SHIN_R, FOOT_R,
	UARM_L, FARM_L, HAND_L, UARM_R, FARM_R, HAND_R }

# superfícies (cada uma recebe um material)
enum S { SHIRT, SHORTS, SKIN, SOCKS, BOOTS, HAIR, HEAD, ARMS, HANDS, SLEEVES }

const N := 14   # vértices por anel

# posições de repouso (globais, em metros)
const HIP_Y := 0.95
const SPINE_Y := 0.99
const CHEST_Y := 1.25
const NECK_Y := 1.5
const HEAD_Y := 1.62
const LEG_X := 0.095
const KNEE_Y := 0.48
const ANKLE_Y := 0.075
const SHOULDER_X := 0.215
const SHOULDER_Y := 1.46
const ELBOW_Y := 1.18
const WRIST_Y := 0.94
const HEAD_C := Vector3(0, 1.735, 0.005)
const HEAD_R := 0.098

static var _cache: Dictionary = {}


## Esqueleto com os ossos na pose de repouso.
static func make_skeleton() -> Skeleton3D:
	var sk := Skeleton3D.new()
	var defs := [
		["root", -1, Vector3.ZERO],
		["hips", B.ROOT, Vector3(0, HIP_Y, 0)],
		["spine", B.HIPS, Vector3(0, SPINE_Y - HIP_Y, 0)],
		["chest", B.SPINE, Vector3(0, CHEST_Y - SPINE_Y, 0)],
		["neck", B.CHEST, Vector3(0, NECK_Y - CHEST_Y, 0)],
		["head", B.NECK, Vector3(0, HEAD_Y - NECK_Y, 0)],
		["thigh_l", B.HIPS, Vector3(-LEG_X, -0.03, 0)],
		["shin_l", B.THIGH_L, Vector3(0, KNEE_Y - (HIP_Y - 0.03), 0)],
		["foot_l", B.SHIN_L, Vector3(0, ANKLE_Y - KNEE_Y, 0)],
		["thigh_r", B.HIPS, Vector3(LEG_X, -0.03, 0)],
		["shin_r", B.THIGH_R, Vector3(0, KNEE_Y - (HIP_Y - 0.03), 0)],
		["foot_r", B.SHIN_R, Vector3(0, ANKLE_Y - KNEE_Y, 0)],
		["uarm_l", B.CHEST, Vector3(-SHOULDER_X, SHOULDER_Y - CHEST_Y, 0)],
		["farm_l", B.UARM_L, Vector3(0, ELBOW_Y - SHOULDER_Y, 0)],
		["hand_l", B.FARM_L, Vector3(0, WRIST_Y - ELBOW_Y, 0)],
		["uarm_r", B.CHEST, Vector3(SHOULDER_X, SHOULDER_Y - CHEST_Y, 0)],
		["farm_r", B.UARM_R, Vector3(0, ELBOW_Y - SHOULDER_Y, 0)],
		["hand_r", B.FARM_R, Vector3(0, WRIST_Y - ELBOW_Y, 0)],
	]
	for d in defs:
		var i := sk.add_bone(d[0])
		if int(d[1]) >= 0:
			sk.set_bone_parent(i, d[1])
		sk.set_bone_rest(i, Transform3D(Basis.IDENTITY, d[2]))
		sk.set_bone_pose_position(i, d[2])
	return sk


static func get_mesh(build: float, hair: String) -> ArrayMesh:
	var key := "%.1f|%s" % [build, hair]
	if not _cache.has(key):
		_cache[key] = _build(build, hair)
	return _cache[key]


# ------------------------------------------------------------------ construção

## Peso entre dois ossos ao longo de y (junta em `yj`, faixa de mistura `bw`).
static func _blend(y: float, upper: int, lower: int, yj: float, bw: float) -> Array:
	if y >= yj + bw:
		return [upper, 1.0, lower, 0.0]
	if y <= yj - bw:
		return [lower, 1.0, upper, 0.0]
	var t := (y - (yj - bw)) / (2.0 * bw)
	t = t * t * (3.0 - 2.0 * t)
	return [upper, t, lower, 1.0 - t]


static func _vtx(st: SurfaceTool, p: Vector3, w: Array, uv: Vector2 = Vector2.ZERO) -> void:
	st.set_uv(uv)
	st.set_bones(PackedInt32Array([int(w[0]), int(w[2]), 0, 0]))
	st.set_weights(PackedFloat32Array([float(w[1]), float(w[3]), 0.0, 0.0]))
	st.add_vertex(p)


## Tubo vertical por anéis elípticos. rings: [y, rx, rz, cz(, cx_off)].
## weight_fn(y) -> [b1, w1, b2, w2]
static func _loft(st: SurfaceTool, base: int, cx: float, rings: Array, weight_fn: Callable, cap_top: bool, cap_bottom: bool) -> int:
	var count := 0
	for r in rings:
		var y: float = r[0]
		var w: Array = weight_fn.call(y)
		var ox: float = r[4] if r.size() > 4 else 0.0
		for i in N:
			var a := TAU * i / N
			var p := Vector3(cx + ox + cos(a) * float(r[1]), y, float(r[3]) + sin(a) * float(r[2]))
			_vtx(st, p, w)
			count += 1
	for k in rings.size() - 1:
		for i in N:
			var a0 := base + k * N + i
			var a1 := base + k * N + (i + 1) % N
			var b0 := a0 + N
			var b1 := a1 + N
			# anéis de cima para baixo: ordem para normais para fora
			st.add_index(a0); st.add_index(b0); st.add_index(a1)
			st.add_index(a1); st.add_index(b0); st.add_index(b1)
	var top: Array = rings[0]
	var bot: Array = rings[rings.size() - 1]
	if cap_top:
		_vtx(st, Vector3(cx + (top[4] if top.size() > 4 else 0.0), float(top[0]) + 0.01, float(top[3])), weight_fn.call(float(top[0])))
		var c := base + count
		count += 1
		for i in N:
			st.add_index(c); st.add_index(base + i); st.add_index(base + (i + 1) % N)
	if cap_bottom:
		_vtx(st, Vector3(cx + (bot[4] if bot.size() > 4 else 0.0), float(bot[0]) - 0.008, float(bot[3])), weight_fn.call(float(bot[0])))
		var c := base + count
		count += 1
		var last := base + (rings.size() - 1) * N
		for i in N:
			st.add_index(c); st.add_index(last + (i + 1) % N); st.add_index(last + i)
	return count


static func _new_st() -> SurfaceTool:
	var st := SurfaceTool.new()
	st.set_skin_weight_count(SurfaceTool.SKIN_4_WEIGHTS)
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	return st


static func _build(build: float, hair: String) -> ArrayMesh:
	var mesh := ArrayMesh.new()
	var m := build    # 0 = magro, 1 = forte
	var mus := lerpf(0.94, 1.1, m)

	# ---------- camisa (tronco)
	var st := _new_st()
	var torso := [
		[1.555, 0.072, 0.066, 0.012],
		[1.53, 0.12, 0.08, 0.012],
		[1.5, 0.185 * mus, 0.098, 0.006],
		[1.455, 0.212 * mus, 0.112, 0.0],
		[1.39, 0.205 * mus, 0.122, -0.008],
		[1.3, 0.19 * mus, 0.128, -0.014],
		[1.2, 0.17 * mus, 0.118, -0.01],
		[1.1, 0.158, 0.11, -0.004],
		[1.0, 0.16, 0.112, 0.0],
		[0.9, 0.168, 0.118, 0.0],
	]
	var tw := func(y: float) -> Array:
		if y > 1.12:
			return _blend(y, B.CHEST, B.SPINE, 1.2, 0.08)
		return _blend(y, B.SPINE, B.HIPS, 0.99, 0.06)
	_loft(st, 0, 0.0, torso, tw, false, true)
	st.generate_normals()
	mesh = st.commit(mesh)

	# ---------- calção (pélvis + pernas)
	st = _new_st()
	var v := 0
	v += _loft(st, v, 0.0, [[1.0, 0.172, 0.122, 0.0], [0.93, 0.182, 0.13, 0.004], [0.86, 0.18, 0.125, 0.006]], func(_y: float) -> Array: return [B.HIPS, 1.0, 0, 0.0], false, false)
	for side: float in [-1.0, 1.0]:
		var th: int = B.THIGH_L if side < 0 else B.THIGH_R
		v += _loft(st, v, side * LEG_X, [[0.9, 0.1, 0.105, 0.0, side * 0.01], [0.8, 0.098 * mus, 0.102 * mus, -0.004], [0.7, 0.094 * mus, 0.098 * mus, -0.006]],
			func(y: float) -> Array: return _blend(y, B.HIPS, th, 0.86, 0.05), false, false)
	st.generate_normals()
	mesh = st.commit(mesh)

	# ---------- pele: coxas, joelhos, pescoço, orelhas
	st = _new_st()
	v = 0
	for side: float in [-1.0, 1.0]:
		var th: int = B.THIGH_L if side < 0 else B.THIGH_R
		var sh: int = B.SHIN_L if side < 0 else B.SHIN_R
		v += _loft(st, v, side * LEG_X, [
			[0.76, 0.088 * mus, 0.094 * mus, -0.006],
			[0.66, 0.08 * mus, 0.088 * mus, -0.012],
			[0.56, 0.066 * mus, 0.07 * mus, -0.006],
			[0.49, 0.055, 0.058, -0.004],
			[0.43, 0.054, 0.058, 0.004],
			[0.37, 0.06 * mus, 0.066 * mus, 0.014],
		], func(y: float) -> Array: return _blend(y, th, sh, KNEE_Y, 0.04), false, false)
	v += _loft(st, v, 0.0, [[1.69, 0.05, 0.052, 0.018], [1.6, 0.054, 0.056, 0.012], [1.5, 0.062, 0.06, 0.008]],
		func(y: float) -> Array: return _blend(y, B.HEAD, B.CHEST, 1.6, 0.06), false, false)
	for side: float in [-1.0, 1.0]:
		var ec := HEAD_C + Vector3(side * HEAD_R * 0.9, -0.012, 0.012)
		v += _loft(st, v, ec.x, [[ec.y + 0.03, 0.006, 0.012, ec.z], [ec.y, 0.012, 0.02, ec.z], [ec.y - 0.028, 0.007, 0.012, ec.z]],
			func(_y: float) -> Array: return [B.HEAD, 1.0, 0, 0.0], true, true)
	st.generate_normals()
	mesh = st.commit(mesh)

	# ---------- meiões (canela + panturrilha)
	st = _new_st()
	v = 0
	for side: float in [-1.0, 1.0]:
		var sh: int = B.SHIN_L if side < 0 else B.SHIN_R
		var ft: int = B.FOOT_L if side < 0 else B.FOOT_R
		v += _loft(st, v, side * LEG_X, [
			[0.4, 0.062 * mus, 0.068 * mus, 0.014],
			[0.31, 0.06 * mus, 0.068 * mus, 0.018],
			[0.2, 0.046, 0.05, 0.008],
			[0.11, 0.037, 0.04, 0.002],
			[0.06, 0.038, 0.043, 0.004],
		], func(y: float) -> Array: return _blend(y, sh, ft, 0.1, 0.03), false, false)
	st.generate_normals()
	mesh = st.commit(mesh)

	# ---------- chuteiras (anéis ao longo do pé: calcanhar → bico)
	st = _new_st()
	v = 0
	for side: float in [-1.0, 1.0]:
		var ft: int = B.FOOT_L if side < 0 else B.FOOT_R
		var cx := side * LEG_X
		var prof := [
			[0.07, 0.07, 0.032, 0.04],
			[0.045, 0.06, 0.04, 0.052],
			[0.0, 0.05, 0.044, 0.046],
			[-0.06, 0.036, 0.046, 0.034],
			[-0.12, 0.026, 0.042, 0.024],
			[-0.17, 0.02, 0.034, 0.018],
			[-0.195, 0.017, 0.02, 0.012],
		]   # [z, cy, rx, ry]
		var w := [ft, 1.0, 0, 0.0]
		for r in prof:
			for i in N:
				var a := TAU * i / N
				var y := maxf(float(r[1]) + sin(a) * float(r[3]), 0.006)
				_vtx(st, Vector3(cx + cos(a) * float(r[2]) + side * 0.004 * (1.0 - float(r[0]) * 4.0), y, float(r[0])), w)
		for k in prof.size() - 1:
			for i in N:
				var a0 := v + k * N + i
				var a1 := v + k * N + (i + 1) % N
				st.add_index(a0); st.add_index(a1); st.add_index(b_idx(a0))
				st.add_index(a1); st.add_index(a1 + N); st.add_index(b_idx(a0))
		var c0 := v + prof.size() * N
		_vtx(st, Vector3(cx, 0.06, 0.075), w)
		_vtx(st, Vector3(cx, 0.014, -0.2), w)
		for i in N:
			st.add_index(c0); st.add_index(v + (i + 1) % N); st.add_index(v + i)
			var last := v + (prof.size() - 1) * N
			st.add_index(c0 + 1); st.add_index(last + i); st.add_index(last + (i + 1) % N)
		v += prof.size() * N + 2
	st.generate_normals()
	mesh = st.commit(mesh)

	# ---------- cabelo
	st = _new_st()
	_hair(st, hair)
	st.generate_normals()
	mesh = st.commit(mesh)

	# ---------- cabeça (com UV para o rosto)
	st = _new_st()
	_head(st)
	st.generate_normals()
	mesh = st.commit(mesh)

	# ---------- braços (pele ou manga comprida no goleiro) — manga curta vai na camisa
	st = _new_st()
	v = 0
	for side: float in [-1.0, 1.0]:
		var ua: int = B.UARM_L if side < 0 else B.UARM_R
		var fa: int = B.FARM_L if side < 0 else B.FARM_R
		var hd: int = B.HAND_L if side < 0 else B.HAND_R
		var cx := side * SHOULDER_X
		v += _loft(st, v, cx, [
			[1.36, 0.052 * mus, 0.054 * mus, 0.0],
			[1.28, 0.054 * mus, 0.06 * mus, -0.008],
			[1.2, 0.042, 0.045, 0.0],
			[1.14, 0.047 * mus, 0.05 * mus, -0.004],
			[1.06, 0.043, 0.044, -0.002],
			[0.97, 0.031, 0.025, 0.0],
			[0.945, 0.03, 0.022, 0.0],
		], func(y: float) -> Array:
			if y > 1.05:
				return _blend(y, ua, fa, ELBOW_Y, 0.04)
			return _blend(y, fa, hd, WRIST_Y, 0.02), false, false)
	st.generate_normals()
	mesh = st.commit(mesh)

	# ---------- mãos
	st = _new_st()
	v = 0
	for side: float in [-1.0, 1.0]:
		var hd: int = B.HAND_L if side < 0 else B.HAND_R
		v += _loft(st, v, side * SHOULDER_X, [
			[0.955, 0.03, 0.022, 0.0],
			[0.92, 0.038, 0.018, -0.004],
			[0.88, 0.036, 0.016, -0.008],
			[0.85, 0.026, 0.012, -0.01],
		], func(_y: float) -> Array: return [hd, 1.0, 0, 0.0], false, true)
	st.generate_normals()
	mesh = st.commit(mesh)

	# ---------- mangas curtas (parte da camisa, superfície própria não é preciso: vão na SHIRT)
	# adicionadas ao final da superfície da camisa seria ideal; como surfaces já foram
	# fechadas, usamos uma superfície extra com o mesmo material (índice 9).
	st = _new_st()
	v = 0
	for side: float in [-1.0, 1.0]:
		var ua: int = B.UARM_L if side < 0 else B.UARM_R
		v += _loft(st, v, side * SHOULDER_X, [
			[1.52, 0.03, 0.035, 0.0, -side * 0.025],
			[1.5, 0.066, 0.07, 0.0, -side * 0.01],
			[1.44, 0.074 * mus, 0.074 * mus, 0.0],
			[1.36, 0.068 * mus, 0.068 * mus, 0.0],
			[1.31, 0.066 * mus, 0.066 * mus, 0.0],
		], func(y: float) -> Array: return _blend(y, B.CHEST, ua, 1.5, 0.03), false, false)
	st.generate_normals()
	mesh = st.commit(mesh)
	return mesh


static func b_idx(a: int) -> int:
	return a + N


## Cabeça: esfera deformada (crânio, maxilar, queixo, nariz), UV com o rosto no centro.
static func _head(st: SurfaceTool) -> void:
	var rows := 14
	var cols := 18
	var w := [B.HEAD, 1.0, 0, 0.0]
	for j in rows + 1:
		var v := float(j) / rows
		var th := v * PI
		for i in cols + 1:
			var u := float(i) / cols
			var ph := (u - 0.5) * TAU          # 0 = frente (-Z)
			var p := _head_point(th, ph, 1.0)
			_vtx(st, p, w, Vector2(u, v))
	for j in rows:
		for i in cols:
			var a := j * (cols + 1) + i
			var b := a + cols + 1
			st.add_index(a); st.add_index(b); st.add_index(a + 1)
			st.add_index(a + 1); st.add_index(b); st.add_index(b + 1)


## Ponto da superfície da cabeça. th: 0 topo → PI queixo; ph: 0 frente.
static func _head_point(th: float, ph: float, scale_r: float) -> Vector3:
	var r := HEAD_R * scale_r
	var sy := 1.18
	var x := sin(th) * sin(ph) * r * 0.9
	var z := -sin(th) * cos(ph) * r
	var y := cos(th) * r * sy
	var front := maxf(0.0, cos(ph))
	# maxilar mais estreito embaixo
	if th > PI * 0.55:
		var k := (th - PI * 0.55) / (PI * 0.45)
		x *= lerpf(1.0, 0.72, k)
		z = lerpf(z, z * 0.8 - front * 0.012, k)
	# crânio maior atrás
	if cos(ph) < 0.0 and th < PI * 0.6:
		z += -cos(ph) * 0.012 * sin(th)
	# nariz e queixo (só na frente)
	if scale_r <= 1.0:
		var nose := exp(-pow((th - PI * 0.56) / 0.12, 2.0)) * exp(-pow(ph / 0.2, 2.0))
		z -= nose * 0.02
		var chin := exp(-pow((th - PI * 0.86) / 0.1, 2.0)) * exp(-pow(ph / 0.45, 2.0))
		z -= chin * 0.008
		var brow := exp(-pow((th - PI * 0.42) / 0.06, 2.0)) * exp(-pow(ph / 0.6, 2.0))
		z -= brow * 0.005
	return HEAD_C + Vector3(x, y, z)


## Cabelo moldado sobre o crânio, com linha do cabelo por estilo.
static func _hair(st: SurfaceTool, style: String) -> void:
	if style == "bald":
		# superfície vazia não é permitida: um triângulo minúsculo escondido
		var w := [B.HEAD, 1.0, 0, 0.0]
		_vtx(st, HEAD_C, w); _vtx(st, HEAD_C + Vector3(0.001, 0, 0), w); _vtx(st, HEAD_C + Vector3(0, 0.001, 0), w)
		return
	var thick := 1.06
	var front_line := 0.3
	var side_line := 0.47
	var back_line := 0.62
	var lift := 0.0
	match style:
		"buzz":
			thick = 1.02
			front_line = 0.27
		"fade":
			thick = 1.05
			side_line = 0.36
			back_line = 0.5
		"afro":
			thick = 1.42
			lift = 0.03
			side_line = 0.5
			back_line = 0.66
		"curly":
			thick = 1.16
			lift = 0.012
		"long", "dreads":
			thick = 1.1
			side_line = 0.62
			back_line = 0.72
		"fringe":
			front_line = 0.38
			thick = 1.08
		"mohawk":
			thick = 1.12
			side_line = 0.2
			back_line = 0.55
			front_line = 0.26
		"bun":
			thick = 1.06
	var rows := 9
	var cols := 20
	var w := [B.HEAD, 1.0, 0, 0.0]
	var base := 0
	for j in rows + 1:
		var s := float(j) / rows
		for i in cols + 1:
			var ph := (float(i) / cols - 0.5) * TAU
			var c := cos(ph)
			# linha do cabelo: frente (c=1) → lado (c=0) → nuca (c=-1)
			var line := lerpf(side_line, front_line, c) if c > 0.0 else lerpf(side_line, back_line, -c)
			if style == "mohawk":
				line = lerpf(0.12, line, clampf(absf(sin(ph)) * -1.0 + 1.0, 0.0, 1.0)) if absf(sin(ph)) > 0.35 else line
			var th := s * line * PI
			var k := thick
			if style == "curly" or style == "afro":
				k += 0.035 * sin(ph * 9.0) * sin(th * 7.0)
			var p := _head_point(th, ph, k)
			p.y += lift * (1.0 - s)
			# mais fino perto da borda (encosta na pele)
			var edge := smoothstep(0.75, 1.0, s)
			p = p.lerp(_head_point(th, ph, 1.005), edge * 0.8)
			_vtx(st, p, w)
	for j in rows:
		for i in cols:
			var a := base + j * (cols + 1) + i
			var b := a + cols + 1
			st.add_index(a); st.add_index(b); st.add_index(a + 1)
			st.add_index(a + 1); st.add_index(b); st.add_index(b + 1)
	base += (rows + 1) * (cols + 1)
	if style == "long" or style == "dreads":
		# mecha caindo até os ombros (atrás)
		var top := HEAD_C + Vector3(0, -0.02, HEAD_R * 0.95)
		var wn := func(y: float) -> Array: return _blend(y, B.HEAD, B.NECK, 1.62, 0.04)
		_loft(st, base, 0.0, [[top.y, 0.085, 0.03, top.z], [top.y - 0.1, 0.08, 0.028, top.z + 0.006], [top.y - 0.19, 0.07, 0.022, top.z + 0.01]], wn, false, true)
	elif style == "bun":
		var c := HEAD_C + Vector3(0, HEAD_R * 0.9, HEAD_R * 0.75)
		_loft(st, base, 0.0, [[c.y + 0.035, 0.02, 0.02, c.z], [c.y + 0.015, 0.04, 0.04, c.z], [c.y - 0.02, 0.035, 0.035, c.z]], func(_y: float) -> Array: return [B.HEAD, 1.0, 0, 0.0], true, true)
