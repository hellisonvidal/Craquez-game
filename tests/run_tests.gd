## Testes headless:  godot --headless --path . -s tests/run_tests.gd
extends SceneTree

var _fails := 0
var _count := 0


func _initialize() -> void:
	_test_input_frame()
	_test_ball_roll()
	_test_ball_bounce()
	_test_magnus_curve()
	_test_goal_detection()
	_test_determinism()
	for m in GameModes.ALL:
		_test_full_match(m)
	print("\n%d verificações, %d falhas" % [_count, _fails])
	quit(1 if _fails > 0 else 0)


func check(cond: bool, msg: String) -> void:
	_count += 1
	if cond:
		print("  ok   ", msg)
	else:
		_fails += 1
		printerr("  FAIL ", msg)


func _test_input_frame() -> void:
	var f := InputFrame.new()
	f.move = Vector2(0.5, -1.0)
	f.buttons = InputFrame.PASS | InputFrame.SPRINT
	var g := InputFrame.from_bytes(f.to_bytes())
	check(g.buttons == f.buttons, "InputFrame: botões sobrevivem à serialização")
	check(g.move.distance_to(f.move) < 0.01, "InputFrame: direção quantizada ~igual")
	check(f.to_bytes().size() == 3, "InputFrame: 3 bytes por tick")


func _test_ball_roll() -> void:
	var mode := GameModes.get_mode(GameModes.FIELD)
	var b := SimBall.new()
	b.vel = Vector2(10, 0)
	for i in 60 * 30:
		b.step(1.0 / 60.0, mode)
	check(b.speed() < 0.01, "Bola: rasteira para sozinha (atrito)")
	check(b.pos.x > 15.0 and b.pos.x < 45.0, "Bola: passe de 10 m/s rola %.1f m no gramado" % b.pos.x)
	var fut := GameModes.get_mode(GameModes.FUTSAL)
	var b2 := SimBall.new()
	b2.vel = Vector2(10, 0)
	for i in 60 * 30:
		b2.step(1.0 / 60.0, fut)
	check(b2.pos.x > b.pos.x, "Bola: rola mais na quadra de madeira que na grama")


func _test_ball_bounce() -> void:
	var mode := GameModes.get_mode(GameModes.STREET)
	var b := SimBall.new()
	b.z = 3.0
	var max_after := 0.0
	var bounced := false
	for i in 60 * 6:
		if b.step(1.0 / 60.0, mode):
			bounced = true
		if bounced:
			max_after = maxf(max_after, b.z)
	check(bounced, "Bola: quica ao cair")
	check(max_after < 3.0 and max_after > 0.3, "Bola: quique perde energia (%.2f m)" % max_after)
	check(b.z == 0.0, "Bola: acaba parada no chão")


func _test_magnus_curve() -> void:
	var mode := GameModes.get_mode(GameModes.FIELD)
	var a := SimBall.new()
	a.vel = Vector2(25, 0)
	a.vz = 5.0
	a.spin = 6.0
	for i in 60:
		a.step(1.0 / 60.0, mode)
	check(a.pos.y > 1.0, "Bola: efeito (Magnus) curva a trajetória (%.2f m)" % a.pos.y)


func _test_goal_detection() -> void:
	var sim := MatchSim.new()
	sim.setup(GameModes.FIELD, Teams.get_team(0), Teams.get_team(1), 7, 60.0)
	sim.state = MatchSim.State.PLAYING
	sim.ball.owner = -1
	sim.protect_t = 0.0
	# tira o goleiro do caminho
	for p in sim.players:
		p.pos = Vector2(0, 30)
	sim.ball.pos = Vector2(sim.half_len - 3.0, 0.5)
	sim.ball.vel = Vector2(20, 0)
	sim.ball.last_touch_team = 0
	for i in 30:
		sim.step([InputFrame.new(), InputFrame.new()])
	check(sim.score[0] == 1, "Gol: bola cruzando a linha entre as traves conta")
	check(sim.state == MatchSim.State.GOAL, "Gol: estado de comemoração")


func _run(seed_value: int, ticks: int) -> MatchSim:
	var sim := MatchSim.new()
	sim.setup(GameModes.FUTSAL, Teams.get_team(2), Teams.get_team(3), seed_value, 30.0)
	var a := CpuBrain.new(0, 0.7)
	var b := CpuBrain.new(1, 0.5)
	for i in ticks:
		sim.step([a.make_input(sim), b.make_input(sim)])
	return sim


func _test_determinism() -> void:
	var s1 := _run(99, 1500)
	var s2 := _run(99, 1500)
	check(StateHash.of(s1) == StateHash.of(s2), "Determinismo: mesma semente + entradas = mesmo estado")
	var s3 := _run(100, 1500)
	check(StateHash.of(s1) != StateHash.of(s3), "Determinismo: semente diferente muda a partida")


func _test_full_match(mode_id: String) -> void:
	var sim := MatchSim.new()
	sim.setup(mode_id, Teams.get_team(0), Teams.get_team(4), 5, 25.0)
	var a := CpuBrain.new(0, 0.8)
	var b := CpuBrain.new(1, 0.8)
	var ok := true
	var kicks := 0
	for i in 60 * 150:
		sim.step([a.make_input(sim), b.make_input(sim)])
		for e in sim.events:
			if e["type"] == "kick":
				kicks += 1
		if is_nan(sim.ball.pos.x) or absf(sim.ball.pos.x) > sim.half_len + 5.0 or absf(sim.ball.pos.y) > sim.half_w + 5.0:
			ok = false
			break
		if sim.state == MatchSim.State.FULLTIME:
			break
	check(ok, "%s: bola sempre em posição válida" % mode_id)
	check(sim.state == MatchSim.State.FULLTIME, "%s: partida CPU x CPU chega ao fim" % mode_id)
	check(kicks > 0, "%s: houve chutes/passes (%d), placar %d x %d" % [mode_id, kicks, sim.score[0], sim.score[1]])
