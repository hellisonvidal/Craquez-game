## Nó da partida: roda a simulação em passo fixo, coleta entradas dos
## provedores (dispositivo local / CPU / rede), alimenta a view e o HUD.
class_name MatchGame
extends Node2D

signal exit_to_menu

## config: {mode, team_a, team_b, devices: [dev0, dev1] ("cpu" = computador, "remote" = online),
##          seed, half_minutes, session (opcional: EnetSession)}
var config: Dictionary = {}
var sim: MatchSim
var view: MatchView3D
var hud: Hud
var touch: TouchControls
var sfx: Sfx
var session: NetSession = LocalSession.new()
var replay := ReplayBuffer.new()
var brains: Array = [null, null]
var paused := false
var lockstep: Lockstep
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
	if cfg.get("session") is NetSession and (cfg["session"] as NetSession).is_online():
		session = cfg["session"]
		lockstep = Lockstep.new(session, sim, session.local_team(), 3)
		lockstep.desync.connect(func(t): hud.show_message("Dessincronia", "tick %d — a partida pode divergir" % t, 3.0))
		session.peer_disconnected.connect(_on_peer_lost)
	var colors := [[ta["c1"], ta["c2"]], [tb["c1"], tb["c2"]]]
	# uniformes parecidos? o visitante usa as cores invertidas
	if (ta["c1"] as Color).is_equal_approx(tb["c1"]) or _similar(ta["c1"], tb["c1"]):
		colors[1] = [tb["c2"], tb["c1"]]
	view = MatchView3D.new()
	add_child(view)
	view.human_team = [brains[0] == null, brains[1] == null]
	view.setup(sim, colors)
	sfx = Sfx.new()
	add_child(sfx)
	sfx.start_crowd(cfg["mode"])
	hud = Hud.new()
	add_child(hud)
	hud.setup(sim, [String(ta["short"]), String(tb["short"])], colors)
	hud.resume_requested.connect(func(): _set_paused(false))
	hud.quit_requested.connect(_quit)
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
	if lockstep != null:
		_physics_online()
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
	_after_step()


## Online: a partida nunca pausa (o menu só abre na tela de quem apertou).
func _physics_online() -> void:
	session.poll()
	var lt := lockstep.local_team
	var f := InputRouter.read(config["devices"][lt])
	var pz := f.has(InputFrame.PAUSE)
	if pz and not _prev_pause[lt] and sim.state != MatchSim.State.FULLTIME:
		_set_paused(not paused)
	_prev_pause[lt] = pz
	if paused or hud.is_menu_open():
		f = InputFrame.new()
	lockstep.push_local(f)
	lockstep.advance(_after_step)
	if lockstep.stalled_frames == 90:
		hud.show_message("Conexão lenta", "aguardando o adversário…", 1.5)


func _on_peer_lost() -> void:
	if sim.state == MatchSim.State.FULLTIME:
		return
	hud.show_message("Adversário saiu", "voltando ao menu…", 3.0)
	get_tree().create_timer(3.0).timeout.connect(_quit)


func _quit() -> void:
	if session != null:
		session.close()
	exit_to_menu.emit()


func _exit_tree() -> void:
	if session != null:
		session.close()


func _after_step() -> void:
	for e in sim.events:
		_on_event(e)
		hud.on_event(e)
		sfx.on_event(e)
	# torcida reage à bola perto do gol e ao gol
	var near := clampf((absf(sim.ball.pos.x) / sim.half_len - 0.55) / 0.45, 0.0, 1.0)
	if sim.state == MatchSim.State.GOAL:
		near = 1.0
	sfx.set_excitement(near)


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
	if lockstep != null:
		_quit()   # revanche online: nova sala pelo menu
		return
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
		get_viewport().set_input_as_handled()
