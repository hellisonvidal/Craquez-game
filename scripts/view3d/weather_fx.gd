## Clima da partida (só visual): sol, nublado, chuva, tempestade (com raios)
## e neve; com clima dinâmico o tempo muda ao longo do jogo, suavemente.
## Atua no céu, neblina, filtro de cor da cena, gramado molhado/nevado,
## partículas de chuva/neve presas à câmera e cor da bola.
class_name WeatherFX
extends Node

signal changed(text: String)

const PRESETS := {
	"sol": {"cloud": 0.0, "rain": 0.0, "snow": 0.0, "storm": 0.0},
	"nublado": {"cloud": 0.6, "rain": 0.0, "snow": 0.0, "storm": 0.0},
	"chuva": {"cloud": 0.85, "rain": 0.65, "snow": 0.0, "storm": 0.0},
	"tempestade": {"cloud": 1.0, "rain": 1.0, "snow": 0.0, "storm": 1.0},
	"neve": {"cloud": 0.75, "rain": 0.0, "snow": 0.8, "storm": 0.0},
}
const NEXT := {
	"sol": ["nublado"],
	"nublado": ["sol", "chuva", "chuva"],
	"chuva": ["nublado", "tempestade", "nublado"],
	"tempestade": ["chuva"],
	"neve": ["nublado_neve"],
	"nublado_neve": ["neve"],
}
const MESSAGES := {
	"sol": "O tempo abriu", "nublado": "O céu fechou", "chuva": "Começou a chover",
	"tempestade": "Tempestade no estádio!", "neve": "Começou a nevar",
}

var kind := "sol"
var dynamic := false
var night := false
var cur := {"cloud": 0.0, "rain": 0.0, "snow": 0.0, "storm": 0.0}
var wet := 0.0
var snow_cover := 0.0

var env: Environment
var sky_mat: ProceduralSkyMaterial
var sky_top := Color.BLACK
var sky_horizon := Color.BLACK
var pitch_mat: ShaderMaterial
var ball_mat: ShaderMaterial
var crowd_mats: Array[ShaderMaterial] = []
var crowd_light := 1.0
var cam: Camera3D

var _rng := RandomNumberGenerator.new()
var _next_change := 0.0
var _rain: CPUParticles3D
var _snow: CPUParticles3D
var _tint: ColorRect
var _flash: ColorRect
var _flash_a := 0.0
var _thunder_t := 0.0
var _snow_mode := false


func setup(start_kind: String, is_dynamic: bool, is_night: bool, seed_value: int, parent3d: Node3D) -> void:
	kind = start_kind if PRESETS.has(start_kind) else "sol"
	_snow_mode = kind == "neve"
	dynamic = is_dynamic
	night = is_night
	_rng.seed = seed_value * 31 + 7
	cur = PRESETS[kind].duplicate()
	wet = cur["rain"]
	snow_cover = cur["snow"]
	_next_change = _rng.randf_range(50.0, 100.0)
	# partículas presas à câmera (local): chuva = riscos verticais, neve = flocos
	_rain = _make_particles(Vector2(0.025, 0.75), Color(0.82, 0.88, 1.0, 0.45), 1600, 0.8, -32.0, 14.0)
	_snow = _make_particles(Vector2(0.07, 0.07), Color(1, 1, 1, 0.9), 1200, 6.0, -1.6, 1.2)
	cam.add_child(_rain)
	cam.add_child(_snow)
	# filtro de cor (multiplica) e clarão de raio, abaixo do HUD
	var layer := CanvasLayer.new()
	layer.layer = 0
	parent3d.add_child(layer)
	_tint = ColorRect.new()
	var tm := ShaderMaterial.new()
	tm.shader = preload("res://shaders/tint_mul.gdshader")
	_tint.material = tm
	_tint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(_tint)
	_flash = ColorRect.new()
	_flash.color = Color(1, 1, 1, 0)
	_flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	layer.add_child(_flash)
	var fit := func() -> void:
		var sz := parent3d.get_viewport().get_visible_rect().size
		_tint.size = sz
		_flash.size = sz
	fit.call()
	parent3d.get_viewport().size_changed.connect(fit)
	_apply(0.0)


func _make_particles(sz: Vector2, col: Color, amount: int, lifetime: float, gravity: float, speed: float) -> CPUParticles3D:
	var p := CPUParticles3D.new()
	var q := QuadMesh.new()
	q.size = sz
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.albedo_color = col
	m.billboard_mode = BaseMaterial3D.BILLBOARD_FIXED_Y if sz.y > sz.x * 2.0 else BaseMaterial3D.BILLBOARD_ENABLED
	m.no_depth_test = false
	q.material = m
	p.mesh = q
	p.amount = amount
	p.lifetime = lifetime
	p.preprocess = lifetime
	p.local_coords = false
	p.emission_shape = CPUParticles3D.EMISSION_SHAPE_BOX
	p.emission_box_extents = Vector3(30, 2, 22)
	p.position = Vector3(0, 14, -24)
	p.direction = Vector3(0.08, -1, 0)
	p.spread = 6.0 if gravity < -10.0 else 35.0
	p.initial_velocity_min = speed
	p.initial_velocity_max = speed * 1.3
	p.gravity = Vector3(0.6, gravity, 0)
	p.emitting = false
	return p


func update(delta: float) -> void:
	if dynamic:
		_next_change -= delta
		if _next_change <= 0.0:
			_next_change = _rng.randf_range(45.0, 110.0)
			var opts: Array = NEXT.get(kind if not (_snow_mode and kind == "nublado") else "nublado_neve", ["sol"])
			var nk: String = opts[_rng.randi() % opts.size()]
			if nk == "nublado_neve":
				nk = "nublado"
			if nk != kind:
				kind = nk
				changed.emit(MESSAGES.get(kind, ""))
	_apply(delta)


func _apply(delta: float) -> void:
	var target: Dictionary = PRESETS[kind]
	var k := 1.0 if delta == 0.0 else minf(1.0, delta * 0.12)   # transição de ~15 s
	for key in cur:
		cur[key] = lerpf(float(cur[key]), float(target[key]), k)
	# gramado molha rápido e seca devagar; neve acumula aos poucos
	wet = lerpf(wet, float(cur["rain"]), minf(1.0, delta * (0.15 if cur["rain"] > wet else 0.03)) if delta > 0.0 else 1.0)
	snow_cover = lerpf(snow_cover, float(cur["snow"]), minf(1.0, delta * 0.05) if delta > 0.0 else 1.0)
	var cloud: float = cur["cloud"]
	var rain: float = cur["rain"]
	var snow: float = cur["snow"]
	var storm: float = cur["storm"]
	# partículas
	_rain.emitting = rain > 0.08
	_rain.speed_scale = 0.6 + 0.6 * rain
	_snow.emitting = snow > 0.08
	if _rain.emitting:
		(_rain.mesh.material as StandardMaterial3D).albedo_color.a = 0.15 + 0.4 * rain
	if _snow.emitting:
		(_snow.mesh.material as StandardMaterial3D).albedo_color.a = 0.3 + 0.65 * snow
	# céu e neblina
	if sky_mat != null:
		var grey := Color(0.36, 0.39, 0.45) if not night else Color(0.08, 0.09, 0.12)
		sky_mat.sky_top_color = sky_top.lerp(grey, cloud * 0.85)
		sky_mat.sky_horizon_color = sky_horizon.lerp(grey.lightened(0.15), cloud * 0.85)
	if env != null:
		env.fog_enabled = rain + snow > 0.05
		env.fog_light_color = Color(0.55, 0.6, 0.68) if not night else Color(0.12, 0.13, 0.17)
		env.fog_density = 0.002 + 0.006 * rain + 0.009 * snow
	# filtro geral: tempo fechado escurece e esfria a imagem
	var dim := cloud * (0.22 if not night else 0.1) + storm * 0.08
	var t := Color(1.0 - dim, 1.0 - dim * 0.9, 1.0 - dim * 0.7, 1.0)
	(_tint.material as ShaderMaterial).set_shader_parameter("tint", t)
	if pitch_mat != null:
		pitch_mat.set_shader_parameter("wet", wet)
		pitch_mat.set_shader_parameter("snow", snow_cover)
	if ball_mat != null:
		ball_mat.set_shader_parameter("base_color", Color(0.98, 0.98, 0.98).lerp(Color("#f97316"), clampf(snow_cover * 1.6, 0.0, 1.0)))
	for m in crowd_mats:
		m.set_shader_parameter("light", crowd_light * (1.0 - cloud * 0.18))
	# raios na tempestade
	if storm > 0.5 and delta > 0.0:
		_thunder_t -= delta
		if _thunder_t <= 0.0:
			_thunder_t = _rng.randf_range(6.0, 16.0)
			_flash_a = 0.75
	_flash_a = maxf(0.0, _flash_a - delta * 2.8)
	var flicker := _flash_a * (0.6 + 0.4 * sin(_flash_a * 40.0))
	_flash.color = Color(0.9, 0.93, 1.0, flicker * 0.55)


func label() -> String:
	return {"sol": "Ensolarado", "nublado": "Nublado", "chuva": "Chuva", "tempestade": "Tempestade", "neve": "Neve"}.get(kind, "")
