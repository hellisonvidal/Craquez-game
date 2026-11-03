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
	_test_penalty()
	_test_offside()
	_test_windup()
	_test_skills()
	_test_finesse()
	_test_shield()
	_test_online()
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
	f.buttons = InputFrame.PASS | InputFrame.SPRINT | InputFrame.FINESSE
	f.rstick = Vector2(-1.0, 0.25)
	var g := InputFrame.from_bytes(f.to_bytes())
	check(g.buttons == f.buttons, "InputFrame: botões sobrevivem à serialização")
	check(g.move.distance_to(f.move) < 0.01, "InputFrame: direção quantizada ~igual")
	check(g.rstick.distance_to(f.rstick) < 0.01, "InputFrame: analógico direito sobrevive")
	check(f.to_bytes().size() == 6, "InputFrame: 6 bytes por tick")


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


func _test_penalty() -> void:
	var sim := MatchSim.new()
	sim.setup(GameModes.FIELD, Teams.get_team(0), Teams.get_team(1), 11, 120.0)
	sim.state = MatchSim.State.PLAYING
	var victim: SimPlayer = sim.players[9]          # atacante do time 0 (ataca +x no 1º tempo)
	var offender: SimPlayer = sim.players[11 + 2]   # zagueiro do time 1
	victim.pos = Vector2(sim.half_len - 6.0, 2.0)
	offender.pos = victim.pos + Vector2(-0.5, 0)
	sim._foul(offender, victim)
	check(sim.penalty_team == 0, "Pênalti: falta na área marca pênalti")
	check(sim.ball.pos.distance_to(Vector2(sim.half_len - 11.0, 0)) < 0.01, "Pênalti: bola na marca dos 11 m")
	var empty := InputFrame.new()
	var shoot := InputFrame.new()
	shoot.buttons = InputFrame.SHOOT
	shoot.move = Vector2(1, -0.6)
	var shots := 0
	for i in 200:
		var f0 := empty
		if i >= 100 and i < 125:
			f0 = shoot
		# defensor tentando andar: não pode
		var f1 := InputFrame.new()
		f1.move = Vector2(-1, 0)
		sim.step([f0, f1])
		for e in sim.events:
			if e["type"] == "kick" and e.get("kind", "") == "shot":
				shots += 1
	check(shots == 1, "Pênalti: batedor cobra com o botão de chute")
	check(sim.penalty_team == -1, "Pênalti: lance volta ao normal depois da cobrança")


func _test_offside() -> void:
	var sim := MatchSim.new()
	sim.setup(GameModes.FIELD, Teams.get_team(0), Teams.get_team(1), 12, 120.0)
	sim.state = MatchSim.State.PLAYING
	sim.protect_t = 0.0
	for p in sim.players:
		if p.team == 1:
			p.pos = Vector2(10.0, 25.0 if p.index % 2 == 0 else -25.0)
			if p.is_keeper:
				p.pos = Vector2(sim.half_len - 1.0, 0)
		else:
			p.pos = Vector2(-20.0, float(p.index) * 3.0 - 15.0)
	var passer: SimPlayer = sim.players[5]
	var receiver: SimPlayer = sim.players[9]
	passer.pos = Vector2(0, 0)
	receiver.pos = Vector2(28.0, 0.0)
	sim.ball.pos = passer.pos + Vector2(0.5, 0)
	sim.ball.owner = passer.id
	sim._pass(passer, Vector2(1, 0), 0.5, false, false)
	check(receiver.id in sim.offside_ids, "Impedimento: atacante à frente da penúltima linha marcado no passe")
	var called := false
	for i in 400:
		sim.step([InputFrame.new(), InputFrame.new()])
		for e in sim.events:
			if e["type"] == "offside":
				called = true
		if called:
			break
	check(called, "Impedimento: apitado quando o impedido recebe")
	check(sim.ball.owner >= 0 and sim.players[sim.ball.owner].team == 1, "Impedimento: tiro livre para a defesa")
	var fut := MatchSim.new()
	fut.setup(GameModes.FUTSAL, Teams.get_team(0), Teams.get_team(1), 12, 120.0)
	check(not bool(fut.mode["offside"]), "Impedimento: não existe no futsal")


## Campo vazio com um atleta do time 0 dono da bola no meio-campo, olhando para +x.
func _solo(seed_value: int = 21) -> Array:
	var sim := MatchSim.new()
	sim.setup(GameModes.FIELD, Teams.get_team(0), Teams.get_team(1), seed_value, 300.0)
	sim.state = MatchSim.State.PLAYING
	sim.protect_t = 0.0
	for p in sim.players:
		p.pos = Vector2(-40.0 if p.team == 0 else 40.0, float(p.index) * 4.0 - 20.0)
		p.vel = Vector2.ZERO
	var me: SimPlayer = sim.players[9]
	me.pos = Vector2(0, 0)
	me.facing = Vector2.RIGHT
	sim.ball.pos = me.pos + Vector2(0.45, 0)
	sim.ball.vel = Vector2.ZERO
	sim.ball.owner = me.id
	sim.ctrl[0].player = me.id
	return [sim, me]


func _run(sim: MatchSim, f0: InputFrame, ticks: int) -> Array:
	var ev: Array = []
	for i in ticks:
		sim.step([f0, InputFrame.new()])
		ev.append_array(sim.events)
	return ev


func _test_windup() -> void:
	var r := _solo()
	var sim: MatchSim = r[0]
	var me: SimPlayer = r[1]
	var hold := InputFrame.new()
	hold.buttons = InputFrame.SHOOT
	hold.move = Vector2(1, 0)
	_run(sim, hold, 20)
	var rel := InputFrame.new()
	rel.move = Vector2(1, 0)
	sim.step([rel, InputFrame.new()])
	var kicked_now := false
	for e in sim.events:
		if e["type"] == "kick":
			kicked_now = true
	check(not kicked_now and me.windup > 0.0, "Chute: soltar o botão arma a perna antes (sem chute instantâneo)")
	var ev := _run(sim, rel, 20)
	var kicked := false
	for e in ev:
		if e["type"] == "kick" and e.get("kind", "") == "shot":
			kicked = true
	check(kicked and sim.ball.owner == -1, "Chute: bola sai ao fim da armação")


func _test_skills() -> void:
	# puxada: flick para trás → vira ~180° com a bola
	var r := _solo()
	var sim: MatchSim = r[0]
	var me: SimPlayer = r[1]
	var flick := InputFrame.new()
	flick.rstick = Vector2(-1, 0)
	sim.step([flick, InputFrame.new()])
	check(me.skill == SimPlayer.Skill.DRAG, "Drible: analógico direito para trás = puxada")
	_run(sim, InputFrame.new(), 40)
	check(me.facing.dot(Vector2.LEFT) > 0.8, "Puxada: atleta termina virado para trás (%.2f)" % me.facing.dot(Vector2.LEFT))
	check(sim.ball.owner == me.id, "Puxada: bola continua dominada")
	# toque longo: flick para frente → bola vai à frente e atleta arranca
	r = _solo()
	sim = r[0]
	me = r[1]
	flick = InputFrame.new()
	flick.rstick = Vector2(1, 0)
	sim.step([flick, InputFrame.new()])
	check(sim.ball.owner == -1 and sim.ball.vel.x > 4.0, "Toque longo: bola empurrada à frente")
	check(me.burst > 0.0, "Toque longo: arrancada")
	# corte lateral: flick para o lado → bola e atleta deslocam de lado
	r = _solo()
	sim = r[0]
	me = r[1]
	flick = InputFrame.new()
	flick.rstick = Vector2(0, 1)
	sim.step([flick, InputFrame.new()])
	_run(sim, InputFrame.new(), 30)
	check(me.pos.y > 0.6 and sim.ball.owner == me.id, "Corte lateral: desloca para o lado com a bola (y=%.2f)" % me.pos.y)
	# pedalada: LT + lado engana o marcador de frente (estatisticamente)
	var fooled := 0
	for sd in 20:
		r = _solo(100 + sd)
		sim = r[0]
		me = r[1]
		var def: SimPlayer = sim.players[11 + 3]
		def.pos = Vector2(2.0, 0.0)
		def.facing = Vector2.LEFT
		flick = InputFrame.new()
		flick.rstick = Vector2(0, -1)
		flick.buttons = InputFrame.SHIELD
		sim.step([flick, InputFrame.new()])
		if def.off_balance > 0.0:
			fooled += 1
	check(fooled >= 4 and fooled <= 18, "Pedalada: desequilibra o marcador às vezes (%d/20)" % fooled)


func _test_finesse() -> void:
	var r := _solo()
	var sim: MatchSim = r[0]
	var me: SimPlayer = r[1]
	me.pos = Vector2(sim.half_len - 18.0, 6.0)
	sim.ball.pos = me.pos + Vector2(0.45, 0)
	sim._shoot(me, Vector2(1, -0.6), 0.6, true)
	check(absf(sim.ball.spin) > 1.5, "Colocado: bola sai com efeito (%.2f)" % sim.ball.spin)
	var y0 := sim.ball.vel.normalized().y
	for i in 30:
		sim.ball.step(MatchSim.DT, sim.mode)
	var y1 := sim.ball.vel.normalized().y
	check(absf(y1 - y0) > 0.02, "Colocado: trajetória faz curva (%.3f → %.3f)" % [y0, y1])


func _test_shield() -> void:
	var r := _solo()
	var sim: MatchSim = r[0]
	var me: SimPlayer = r[1]
	var f := InputFrame.new()
	f.move = Vector2(1, 0)
	f.buttons = InputFrame.SHIELD
	_run(sim, f, 90)
	check(me.vel.length() < me.jog_speed() * 0.65, "Proteção: condução lenta e curta (%.1f m/s)" % me.vel.length())
	check(sim.ball.owner == me.id and me.pos.distance_to(sim.ball.pos) < 0.8, "Proteção: bola colada no pé")


## Duas sessões ENet reais no mesmo processo (localhost): handshake + 600 ticks em lockstep.
func _test_online() -> void:
	var h := EnetSession.new()
	var c := EnetSession.new()
	check(h.host(27999) == OK, "Online: anfitrião abre a porta")
	check(c.join("127.0.0.1", 27999) == OK, "Online: convidado inicia conexão")
	var got: Dictionary = {}
	var ready_flag: Array[bool] = [false]
	c.config_received.connect(func(x: Dictionary): got.merge(x))
	h.ready_received.connect(func(): ready_flag[0] = true)
	for i in 400:
		h.poll()
		c.poll()
		if h.connected and c.connected:
			break
		OS.delay_msec(5)
	check(h.connected and c.connected, "Online: conexão estabelecida")
	h.send_config({"mode": "futsal", "team_a": 2, "team_b": 5, "seed": 4242, "half_minutes": 1.0})
	for i in 400:
		h.poll()
		c.poll()
		if not got.is_empty():
			break
		OS.delay_msec(5)
	check(int(got.get("seed", 0)) == 4242, "Online: configuração da partida chega ao convidado")
	c.send_ready()
	for i in 200:
		h.poll()
		c.poll()
		if ready_flag[0]:
			break
		OS.delay_msec(5)
	check(ready_flag[0], "Online: anfitrião recebe READY")

	var sim_h := MatchSim.new()
	sim_h.setup("futsal", Teams.get_team(2), Teams.get_team(5), 4242, 60.0)
	var sim_c := MatchSim.new()
	sim_c.setup(String(got["mode"]), Teams.get_team(int(got["team_a"])), Teams.get_team(int(got["team_b"])), int(got["seed"]), 60.0)
	var ls_h := Lockstep.new(h, sim_h, h.local_team(), 3)
	var ls_c := Lockstep.new(c, sim_c, c.local_team(), 3)
	var desyncs: Array[int] = []
	ls_h.desync.connect(func(t: int): desyncs.append(t))
	ls_c.desync.connect(func(t: int): desyncs.append(t))
	var bh := CpuBrain.new(0, 0.8)
	var bc := CpuBrain.new(1, 0.6)
	const TARGET := 600
	for i in 6000:
		h.poll()
		c.poll()
		ls_h.push_local(bh.make_input(sim_h))
		ls_c.push_local(bc.make_input(sim_c))
		ls_h.advance(Callable(), mini(3, TARGET - sim_h.tick))
		ls_c.advance(Callable(), mini(3, TARGET - sim_c.tick))
		if sim_h.tick >= TARGET and sim_c.tick >= TARGET:
			break
		OS.delay_msec(1)
	# troca dos últimos hashes
	for i in 50:
		h.poll()
		c.poll()
		ls_h.advance(Callable(), 0)
		ls_c.advance(Callable(), 0)
		OS.delay_msec(2)
	check(sim_h.tick == TARGET and sim_c.tick == TARGET, "Online: as duas máquinas avançam (%d / %d ticks)" % [sim_h.tick, sim_c.tick])
	check(StateHash.of(sim_h) == StateHash.of(sim_c), "Online: estados idênticos nas duas máquinas (lockstep)")
	check(desyncs.is_empty(), "Online: nenhuma dessincronia detectada pelos hashes")
	h.close()
	c.close()
