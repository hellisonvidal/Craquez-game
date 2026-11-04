## Sons sintetizados em tempo de carregamento (sem arquivos de áudio):
## chute, quique, trave, dividida, apitos, torcida ambiente (loop) e grito de gol.
## Leve: ~1 s de CPU no total, gerado uma vez e reaproveitado.
class_name Sfx
extends Node

const RATE := 22050

static var _bank: Dictionary = {}

var _pool: Array[AudioStreamPlayer] = []
var _crowd: AudioStreamPlayer
var _crowd_target_db := -18.0
var _next := 0


func _ready() -> void:
	if _bank.is_empty():
		_build_bank()
	for i in 10:
		var p := AudioStreamPlayer.new()
		p.bus = "Master"
		add_child(p)
		_pool.append(p)
	_crowd = AudioStreamPlayer.new()
	_crowd.stream = _bank["crowd"]
	_crowd.volume_db = -60.0
	add_child(_crowd)


func start_crowd(mode_id: String) -> void:
	# street/futsal: torcida menor
	_crowd_target_db = -16.0 if mode_id == GameModes.FIELD else -24.0
	_crowd.play()


## Intensidade 0..1 (bola perto do gol = torcida mais animada).
func set_excitement(x: float) -> void:
	var base := _crowd_target_db
	_crowd.volume_db = lerpf(_crowd.volume_db, base + x * 8.0, 0.05)
	_crowd.pitch_scale = lerpf(_crowd.pitch_scale, 1.0 + x * 0.08, 0.05)


func play(name: String, volume: float = 1.0, pitch: float = 1.0) -> void:
	if not _bank.has(name) or volume <= 0.01:
		return
	var p := _pool[_next]
	_next = (_next + 1) % _pool.size()
	p.stream = _bank[name]
	p.volume_db = linear_to_db(clampf(volume * Settings.sfx_volume, 0.0, 1.0))
	p.pitch_scale = pitch
	p.play()


func on_event(e: Dictionary) -> void:
	match e["type"]:
		"kick":
			var pw: float = e.get("power", 0.5)
			var shot: bool = e.get("kind", "") == "shot"
			play("kick", 0.5 + pw * 0.5 if shot else 0.35 + pw * 0.3, 0.85 + randf() * 0.1 if shot else 1.05 + randf() * 0.1)
		"header":
			play("kick", 0.4, 1.4)
		"skill":
			play("kick", 0.18, 1.5 + randf() * 0.2)
		"fooled":
			play("ooh", 0.45)
		"block":
			play("tackle", 0.7, 1.2)
			play("ooh", 0.55)
		"bounce":
			play("bounce", 0.35, 0.9 + randf() * 0.2)
		"post":
			play("post", 0.9)
		"wall":
			play("wall", 0.6)
		"tackle", "slide":
			play("tackle", 0.6, 0.9 + randf() * 0.2)
		"save":
			play("tackle", 0.7, 0.7)
			play("ooh", 0.7)
		"goal":
			play("cheer", 1.0)
			play("whistle_short", 0.6)
		"foul":
			play("whistle_short", 0.9)
		"whistle":
			var k: String = e.get("kind", "")
			if k == "halftime":
				play("whistle_long", 0.9)
			elif k == "fulltime":
				play("whistle_end", 0.9)
			elif k == "penalty" or k == "offside":
				play("whistle_short", 0.9)
			elif k == "kickoff":
				play("whistle_short", 0.7)


# ------------------------------------------------------------------ síntese

static func _to_stream(samples: PackedFloat32Array, loop: bool = false) -> AudioStreamWAV:
	var data := PackedByteArray()
	data.resize(samples.size() * 2)
	for i in samples.size():
		data.encode_s16(i * 2, int(clampf(samples[i], -1.0, 1.0) * 32000.0))
	var s := AudioStreamWAV.new()
	s.format = AudioStreamWAV.FORMAT_16_BITS
	s.mix_rate = RATE
	s.stereo = false
	s.data = data
	if loop:
		s.loop_mode = AudioStreamWAV.LOOP_FORWARD
		s.loop_begin = 0
		s.loop_end = samples.size()
	return s


static func _build_bank() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	_bank["kick"] = _to_stream(_kick(rng))
	_bank["bounce"] = _to_stream(_thud(rng, 0.07, 110.0, 0.5))
	_bank["wall"] = _to_stream(_thud(rng, 0.12, 80.0, 0.9))
	_bank["tackle"] = _to_stream(_noise_burst(rng, 0.14, 0.15))
	_bank["post"] = _to_stream(_metal())
	_bank["whistle_short"] = _to_stream(_whistle([0.35]))
	_bank["whistle_long"] = _to_stream(_whistle([0.3, 0.9]))
	_bank["whistle_end"] = _to_stream(_whistle([0.3, 0.3, 1.1]))
	_bank["crowd"] = _to_stream(_crowd_loop(rng), true)
	_bank["cheer"] = _to_stream(_cheer(rng, 3.0, 1.0))
	_bank["ooh"] = _to_stream(_cheer(rng, 1.2, 0.5))


static func _kick(rng: RandomNumberGenerator) -> PackedFloat32Array:
	var n := int(RATE * 0.16)
	var out := PackedFloat32Array()
	out.resize(n)
	var ph := 0.0
	for i in n:
		var t := float(i) / RATE
		var f := lerpf(190.0, 55.0, minf(1.0, t / 0.08))
		ph += TAU * f / RATE
		var body := sin(ph) * exp(-t * 28.0)
		var click := rng.randf_range(-1.0, 1.0) * exp(-t * 300.0) * 0.6
		out[i] = (body + click) * 0.9
	return out


static func _thud(rng: RandomNumberGenerator, dur: float, f0: float, amp: float) -> PackedFloat32Array:
	var n := int(RATE * dur)
	var out := PackedFloat32Array()
	out.resize(n)
	for i in n:
		var t := float(i) / RATE
		out[i] = (sin(TAU * f0 * t) * 0.8 + rng.randf_range(-0.3, 0.3)) * exp(-t / dur * 5.0) * amp
	return out


static func _noise_burst(rng: RandomNumberGenerator, dur: float, smooth: float) -> PackedFloat32Array:
	var n := int(RATE * dur)
	var out := PackedFloat32Array()
	out.resize(n)
	var y := 0.0
	for i in n:
		var t := float(i) / RATE
		y = lerpf(y, rng.randf_range(-1.0, 1.0), smooth + 0.3)
		out[i] = y * exp(-t / dur * 4.0) * 0.8
	return out


static func _metal() -> PackedFloat32Array:
	var n := int(RATE * 0.9)
	var out := PackedFloat32Array()
	out.resize(n)
	var parts := [[523.0, 1.0], [1312.0, 0.6], [2210.0, 0.4], [3107.0, 0.25]]
	for i in n:
		var t := float(i) / RATE
		var v := 0.0
		for pr in parts:
			v += sin(TAU * float(pr[0]) * t) * float(pr[1]) * exp(-t * (3.0 + float(pr[0]) / 900.0))
		out[i] = v * 0.4
	return out


## Apito de juiz: tom ~2,9 kHz com "trinado" (modulação rápida da ervilha).
static func _whistle(blows: Array) -> PackedFloat32Array:
	var out := PackedFloat32Array()
	var ph := 0.0
	for b in blows:
		var dur: float = b
		var n := int(RATE * dur)
		for i in n:
			var t := float(i) / RATE
			var trill := 0.5 + 0.5 * sin(TAU * 32.0 * t)
			var f := 2850.0 + 90.0 * trill
			ph += TAU * f / RATE
			var env := minf(1.0, t / 0.02) * minf(1.0, (dur - t) / 0.04)
			out.append((sin(ph) * 0.7 + sin(ph * 2.0) * 0.12) * env * (0.65 + 0.35 * trill) * 0.5)
		for i in int(RATE * 0.12):
			out.append(0.0)
	return out


## Torcida: ruído filtrado com "ondas" lentas; ponta do loop sem estalo (crossfade).
static func _crowd_loop(rng: RandomNumberGenerator) -> PackedFloat32Array:
	var dur := 4.0
	var fade := int(RATE * 0.5)
	var n := int(RATE * dur)
	var raw := PackedFloat32Array()
	raw.resize(n + fade)
	var lp1 := 0.0
	var lp2 := 0.0
	for i in raw.size():
		var t := float(i) / RATE
		var w := rng.randf_range(-1.0, 1.0)
		lp1 = lerpf(lp1, w, 0.12)
		lp2 = lerpf(lp2, lp1, 0.3)
		var swell := 0.75 + 0.25 * sin(TAU * t / dur * 2.0) * sin(TAU * t / dur * 3.0 + 1.0)
		raw[i] = (lp1 - lp2 * 0.5) * swell * 1.6
	var out := PackedFloat32Array()
	out.resize(n)
	for i in n:
		out[i] = raw[i]
	for i in fade:
		var a := float(i) / fade
		out[i] = raw[n + i] * (1.0 - a) + raw[i] * a
	return out


static func _cheer(rng: RandomNumberGenerator, dur: float, amp: float) -> PackedFloat32Array:
	var n := int(RATE * dur)
	var out := PackedFloat32Array()
	out.resize(n)
	var lp := 0.0
	var lp2 := 0.0
	for i in n:
		var t := float(i) / RATE
		lp = lerpf(lp, rng.randf_range(-1.0, 1.0), 0.25)
		lp2 = lerpf(lp2, lp, 0.2)
		var env := minf(1.0, t / 0.25) * clampf((dur - t) / (dur * 0.6), 0.0, 1.0)
		out[i] = (lp - lp2) * env * amp * 2.2
	return out
