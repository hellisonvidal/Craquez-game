## Nó da partida: roda a simulação em passo fixo, coleta entradas dos
## provedores (dispositivo local / CPU / rede), alimenta a view e o HUD.
class_name MatchGame
extends Node2D

signal exit_to_menu

## config: {mode, team_a, team_b, devices: [dev0, dev1] ("cpu" = computador), seed, half_minutes}
var config: Dictionary = {}
var sim: MatchSim
var view: MatchView
var hud: Hud
var touch: TouchControls
var session: NetSession = LocalSession.new()
var replay := ReplayBuffer.new()
var brains: Array = [null, null]
var paused := false
var _prev_pause := [false, false]


func start(cfg: Dictionary) -> void:
	config = cfg
	var ta: Dictionary = Teams.get_team(cfg["team_a"]).duplicate()
	ta["index"] = cfg["team_a"]
	var tb: Dictionary = Teams.get_team(cfg["team_b"]).duplicate()
	tb["index"] = cfg["team_b"]
	sim = MatchSim.new()
	var seed_value: int = cfg.get("seed", 1234)
	sim.setup(cfg["mode"], ta, tb, seed_value, float(cfg.get("half_minutes", 3.0)) * 60.0)
	replay.seed_value = seed_value
	replay.mode_id = cfg["mode"]
	var devices: Array = cfg["devices"]
	for t in 2:
		if devices[t] == "cpu":
			brains[t] = CpuBrain.new(t, Settings.cpu_skill)
	var colors := [[ta["c1"], ta["c2"]], [tb["c1"], tb["c2"]]]
	# uniformes parecidos? o visitante usa as cores invertidas
	if (ta["c1"] as Color).is_equal_approx(tb["c1"]) or _similar(ta["c1"], tb["c1"]):
		colors[1] = [tb["c2"], tb["c1"]]
	view = MatchView.new()
	add_child(view)
	view.human_team = [brains[0] == null, brains[1] == null]
	view.setup(sim, colors)
	hud = Hud.new()
	add_child(hud)
	hud.setup(sim, [String(ta["short"]), String(tb["short"])], colors)
	hud.resume_requested.connect(func(): _set_paused(false))
	hud.quit_requested.connect(func(): exit_to_menu.emit())
	hud.rematch_requested.connect(_rematch)
	if "touch" in devices or Settings.show_touch:
		var layer := CanvasLayer.new()
		layer.layer = 5
		add_child(layer)
		touch = TouchControls.new()
		layer.add_child(touch)
		touch.visible = "touch" in devices


func _similar(a: Color, b: Color) -> bool:
	return Vector3(a.r - b.r, a.g - b.g, a.b - b.b).length() < 0.35


func _frame_for(t: int) -> InputFrame:
	if brains[t] != null:
		return (brains[t] as CpuBrain).make_input(sim)
	return InputRouter.read(config["devices"][t]).quantized()


func _physics_process(_delta: float) -> void:
	if sim == null:
		return
	var frames: Array = [_frame_for(0), _frame_for(1)]
	# Start / Esc pausa (borda de subida)
	for t in 2:
		var pz: bool = (frames[t] as InputFrame).has(InputFrame.PAUSE)
		if pz and not _prev_pause[t] and brains[t] == null and sim.state != MatchSim.State.FULLTIME:
			_set_paused(not paused)
		_prev_pause[t] = pz
	if paused or hud.is_menu_open():
		return
	session.poll()
	replay.record(frames[0], frames[1])
	sim.step(frames)
	for e in sim.events:
		_on_event(e)
		hud.on_event(e)


func _on_event(e: Dictionary) -> void:
	var devs: Array = config["devices"]
	match e["type"]:
		"goal":
			view.shake(10.0)
			for t in 2:
				InputRouter.rumble(devs[t], 0.6, 0.9 if t == int(e["team"]) else 0.2, 0.5)
		"kick":
			var t: int = e.get("team", 0)
			if e.get("kind", "") == "shot":
				view.shake(2.0 + 4.0 * float(e.get("power", 0.0)))
				InputRouter.rumble(devs[t], 0.3, 0.6 * float(e.get("power", 0.0)), 0.15)
			else:
				InputRouter.rumble(devs[t], 0.25, 0.0, 0.06)
		"tackle":
			InputRouter.rumble(devs[int(e["team"])], 0.4, 0.4, 0.12)
		"post":
			view.shake(4.0)
		"whistle":
			if e.get("kind", "") == "fulltime":
				hud.show_end()


func _set_paused(on: bool) -> void:
	paused = on
	hud.show_pause(on)


func _rematch() -> void:
	var cfg := config.duplicate(true)
	cfg["seed"] = int(cfg.get("seed", 1)) + 1
	for c in get_children():
		c.queue_free()
	brains = [null, null]
	paused = false
	replay = ReplayBuffer.new()
	start(cfg)


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel") and not hud.is_menu_open() and sim.state != MatchSim.State.FULLTIME:
		# Esc/Voltar no Android também pausa
		_set_paused(true)
