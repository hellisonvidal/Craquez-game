## Simulação completa de uma partida, em passo fixo (60 Hz).
##
## Regras de ouro (para o online futuro):
##  * nada aqui lê Input, tempo real, randf() ou nós da engine;
##  * a única entrada externa é `step(frames)`, um InputFrame por time;
##  * toda aleatoriedade vem de `rng` (SimRng com semente).
## Assim a mesma semente + mesma sequência de entradas = mesma partida.
class_name MatchSim
extends RefCounted

enum State { KICKOFF, PLAYING, GOAL, RESTART, HALFTIME, FULLTIME }

const DT := 1.0 / 60.0
const CHARGE_TIME := 0.85        # segundos para carregar 100% da força
const PENDING_WINDOW := 0.55     # janela para chute/passe de primeira
const AUTO_SWITCH_EVERY := 0.35


## Controle humano/CPU de um time (qual atleta está sendo comandado etc.).
class Ctrl:
	var player: int = 0
	var prev: int = 0
	var charge: float = 0.0
	var charging: int = 0
	var aim: Vector2 = Vector2.ZERO
	var pending: int = 0
	var pending_power: float = 0.0
	var pending_t: float = 0.0
	var switch_cd: float = 0.0
	var auto_t: float = 0.0
	var help_t: float = 0.0       # 2º marcador chamado
	var shoot_held: bool = false
	var finesse_held: bool = false
	var prev_rs: Vector2 = Vector2.ZERO


var mode: Dictionary = {}
var half_len: float = 52.5
var half_w: float = 34.0
var goal_half: float = 3.66
var goal_h: float = 2.44

var ball: SimBall = SimBall.new()
var players: Array[SimPlayer] = []
var team_info: Array[Dictionary] = []
var ctrl: Array[Ctrl] = []
var score: Array[int] = [0, 0]
var rng: SimRng

var tick: int = 0
var clock: float = 0.0
var half: int = 1
var half_seconds: float = 180.0
var state: int = State.KICKOFF
var state_timer: float = 0.0
var kickoff_team: int = 0
var protect_team: int = -1
var protect_t: float = 0.0
var pass_receiver: int = -1
var pass_receiver_t: float = 0.0
# pênalti
var penalty_team: int = -1
var penalty_taker: int = -1
var penalty_t: float = 0.0
# impedimento: quem estava impedido no momento do último passe
var offside_team: int = -1
var offside_ids: Array[int] = []
var no_offside_kick: bool = false
var last_scorer: int = -1
var last_goal_team: int = -1
var stats_shots: Array[int] = [0, 0]
var team_mentality: Array[int] = [1, 1]   # 0 defensiva, 1 equilibrada, 2 ofensiva
var team_press: Array[int] = [1, 1]       # 0 baixa, 1 média, 2 alta

## Eventos do último passo (para som, câmera, HUD). Ex.: {"type": "goal", "team": 0}
var events: Array[Dictionary] = []


func setup(mode_id: String, team_a: Dictionary, team_b: Dictionary, seed_value: int, seconds_per_half: float) -> void:
	mode = GameModes.get_mode(mode_id)
	half_len = float(mode["length"]) * 0.5
	half_w = float(mode["width"]) * 0.5
	goal_half = float(mode["goal_width"]) * 0.5
	goal_h = mode["goal_height"]
	half_seconds = seconds_per_half
	rng = SimRng.new(seed_value)
	team_info = [team_a, team_b]
	players.clear()
	ctrl = [Ctrl.new(), Ctrl.new()]
	var default_formation: Array = mode["formation"]
	var forms := GameModes.formations(mode_id)
	team_mentality = [1, 1]
	team_press = [1, 1]
	var name_pool: Array = Teams.NAMES.duplicate()
	for t in 2:
		var rating: float = team_info[t]["rating"]
		var roster: Array = team_info[t].get("roster", [])
		var used := {}
		# táticas do time (formação, mentalidade, pressão, titulares)
		var tac: Dictionary = team_info[t].get("tactics", {})
		var formation: Array = forms.get(String(tac.get("formation", "")), default_formation)
		team_mentality[t] = clampi(int(tac.get("mentality", 1)), 0, 2)
		team_press[t] = clampi(int(tac.get("press", 1)), 0, 2)
		var lineup: Array = tac.get("lineup", [])
		for i in formation.size():
			var p := SimPlayer.new()
			p.id = players.size()
			p.team = t
			p.index = i
			p.base = formation[i]
			p.is_keeper = bool(mode["keeper"]) and i == 0
			p.number = i + 1
			p.name = name_pool[rng.next_u32() % name_pool.size()]
			name_pool.erase(p.name)
			if name_pool.is_empty():
				name_pool = Teams.NAMES.duplicate()
			p.face_id = (int(team_info[t].get("index", t)) * 13 + i * 5) % 48
			p.pace = clampf(rating + rng.gauss() * 0.08, 0.35, 0.99)
			p.accel = clampf(rating + rng.gauss() * 0.08, 0.35, 0.99)
			p.shot = clampf(rating + rng.gauss() * 0.1 + (0.06 if p.base.x > -0.3 else -0.05), 0.3, 0.99)
			p.passing = clampf(rating + rng.gauss() * 0.08, 0.35, 0.99)
			p.control = clampf(rating + rng.gauss() * 0.08, 0.35, 0.99)
			p.defense = clampf(rating + rng.gauss() * 0.1 + (0.08 if p.base.x < -0.6 else -0.04), 0.3, 0.99)
			if not roster.is_empty():
				# escala o elenco real do time: melhor disponível em cada posição
				var want := "GK" if p.is_keeper else ("DF" if p.base.x < -0.6 else ("MF" if p.base.x < -0.3 else "FW"))
				var e: Dictionary = {}
				if lineup.size() == formation.size() and int(lineup[i]) >= 0 and int(lineup[i]) < roster.size() and not used.has(int(lineup[i])):
					e = roster[int(lineup[i])]
					used[int(lineup[i])] = true
				else:
					e = _pick_from_roster(roster, want, used, not bool(mode["keeper"]))
				if not e.is_empty():
					p.name = String(e["n"])
					p.number = int(e["num"])
					p.face_id = int(e.get("fid", p.face_id))
					p.face = e.get("f", [])
					var a: Array = e.get("a", [])
					if a.size() >= 6:
						p.pace = float(a[0])
						p.accel = float(a[1])
						p.shot = float(a[2])
						p.passing = float(a[3])
						p.control = float(a[4])
						p.defense = float(a[5])
					for tn in e.get("t", []):
						p.traits |= int(SimPlayer.TRAIT_NAMES.get(String(tn), 0))
					if p.has_trait(SimPlayer.Trait.VELOCISTA):
						p.pace = minf(0.99, p.pace + 0.04)
						p.accel = minf(0.99, p.accel + 0.03)
			players.append(p)
	kickoff_team = 0
	_reset_for_kickoff(0)


## Escolhe do elenco o melhor atleta ainda livre para a posição (média dos atributos).
static func _pick_from_roster(roster: Array, want: String, used: Dictionary, no_keepers: bool) -> Dictionary:
	var best: Dictionary = {}
	var best_i := -1
	var best_s := -1.0
	for pass_i in 2:
		for i in roster.size():
			if used.has(i):
				continue
			var e: Dictionary = roster[i]
			var pos := String(e.get("pos", ""))
			if no_keepers and pos == "GK":
				continue
			if pass_i == 0 and pos != want:
				continue
			var sc := 0.0
			for v in e.get("a", []):
				sc += float(v)
			if sc > best_s:
				best_s = sc
				best = e
				best_i = i
		if not best.is_empty():
			break
	if best_i >= 0:
		used[best_i] = true
	return best


# ---------------------------------------------------------------- utilidades

## +1 se o time ataca para +x neste tempo, −1 caso contrário (troca no 2º tempo).
func team_dir(t: int) -> float:
	var d := 1.0 if t == 0 else -1.0
	return d if half == 1 else -d


func controlled_player(t: int) -> SimPlayer:
	return players[ctrl[t].player]


func owner_player() -> SimPlayer:
	return players[ball.owner] if ball.owner >= 0 else null


func team_has_ball(t: int) -> bool:
	return ball.owner >= 0 and players[ball.owner].team == t


func charge_of(t: int) -> float:
	return ctrl[t].charge if ctrl[t].charging != 0 else 0.0


func nearest_opponent(p: SimPlayer) -> SimPlayer:
	var best: SimPlayer = null
	var bd := INF
	for o in players:
		if o.team == p.team:
			continue
		var d := o.pos.distance_squared_to(p.pos)
		if d < bd:
			bd = d
			best = o
	return best


func nearest_to_ball(t: int, exclude: int = -1, allow_keeper: bool = false) -> SimPlayer:
	var best: SimPlayer = null
	var bd := INF
	for p in players:
		if p.team != t or p.id == exclude or (p.is_keeper and not allow_keeper):
			continue
		var d := p.pos.distance_squared_to(ball.pos)
		if d < bd:
			bd = d
			best = p
	return best


func in_own_box(p: SimPlayer) -> bool:
	if not bool(mode["keeper"]):
		return false
	var gx := -half_len * team_dir(p.team)
	return absf(p.pos.x - gx) < float(mode["box_depth"]) + 0.5 and absf(p.pos.y) < float(mode["box_half"]) + 0.5


func _emit(type: String, data: Dictionary = {}) -> void:
	var e := data.duplicate()
	e["type"] = type
	events.append(e)


static func _rotate_toward(a: Vector2, b: Vector2, max_angle: float) -> Vector2:
	if b.length_squared() < 0.0001:
		return a
	var ang := a.angle_to(b)
	return a.rotated(clampf(ang, -max_angle, max_angle))


# ---------------------------------------------------------------- reinícios

func _formation_world(p: SimPlayer) -> Vector2:
	return Vector2(p.base.x * half_len * team_dir(p.team), p.base.y * half_w * 0.9)


func _reset_for_kickoff(t: int) -> void:
	ball.pos = Vector2.ZERO
	ball.vel = Vector2.ZERO
	ball.z = 0.0
	ball.vz = 0.0
	ball.spin = 0.0
	ball.owner = -1
	pass_receiver = -1
	penalty_team = -1
	offside_ids.clear()
	offside_team = -1
	for p in players:
		p.pos = _formation_world(p)
		var center_r: float = mode["center_r"]
		if p.team != t and p.pos.length() < center_r + 0.5:
			p.pos = p.pos.normalized() * (center_r + 0.5) if p.pos.length() > 0.1 else Vector2(-team_dir(p.team) * (center_r + 0.5), 0)
		p.vel = Vector2.ZERO
		p.facing = Vector2(team_dir(p.team), 0)
		p.stun = 0.0
		p.slide = 0.0
		p.hold = 0.0
		p.dive = 0.0
		p.kick_cd = 0.0
	# batedor: o atacante mais adiantado do time
	var kicker: SimPlayer = null
	for p in players:
		if p.team == t and not p.is_keeper and (kicker == null or p.base.x > kicker.base.x):
			kicker = p
	kicker.pos = Vector2(-team_dir(t) * 0.45, 0.0)
	ball.owner = kicker.id
	ball.last_touch_team = t
	ctrl[t].player = kicker.id
	var other := nearest_to_ball(1 - t)
	ctrl[1 - t].player = other.id
	for c in ctrl:
		c.charging = 0
		c.pending = 0
	protect_team = t
	protect_t = 1.5
	state = State.KICKOFF
	state_timer = 0.9


## Bola parada: lateral, escanteio, tiro de meta, falta.
func _setup_restart(t: int, spot: Vector2, kind: String) -> void:
	spot.x = clampf(spot.x, -half_len, half_len)
	spot.y = clampf(spot.y, -half_w, half_w)
	ball.pos = spot
	ball.vel = Vector2.ZERO
	ball.z = 0.0
	ball.vz = 0.0
	ball.spin = 0.0
	pass_receiver = -1
	var taker: SimPlayer = null
	if kind == "goalkick" and bool(mode["keeper"]):
		for p in players:
			if p.team == t and p.is_keeper:
				taker = p
	if taker == null:
		var bd := INF
		for p in players:
			if p.team == t and not p.is_keeper:
				var d := p.pos.distance_squared_to(spot)
				if d < bd:
					bd = d
					taker = p
	var inward := (Vector2(team_dir(t) * half_len * 0.3, 0) - spot).normalized()
	taker.pos = spot - inward * 0.5
	taker.vel = Vector2.ZERO
	taker.facing = inward
	taker.stun = 0.0
	taker.slide = 0.0
	ball.owner = taker.id
	ball.last_touch_team = t
	if taker.is_keeper:
		taker.hold = 1.6
	else:
		ctrl[t].player = taker.id
	var keep_away: float = mode["restart_dist"]
	for p in players:
		if p.team != t:
			var d := p.pos - spot
			if d.length() < keep_away:
				p.pos = spot + (d.normalized() if d.length() > 0.1 else -inward) * keep_away
		p.stun = minf(p.stun, 0.2)
	protect_team = t
	protect_t = 2.5
	offside_ids.clear()
	offside_team = -1
	no_offside_kick = kind in ["throwin", "corner", "goalkick"]
	state = State.RESTART
	state_timer = 0.7
	_emit("whistle", {"kind": kind, "team": t})


# ---------------------------------------------------------------- passo principal

func step(frames: Array) -> void:
	tick += 1
	events.clear()
	match state:
		State.KICKOFF, State.RESTART:
			state_timer -= DT
			# durante a pausa os jogadores ainda podem se ajeitar (sem ações)
			for t in 2:
				var f: InputFrame = frames[t] if t < frames.size() and frames[t] != null else InputFrame.new()
				ctrl[t].prev = f.buttons
			_ai_all(true)
			for p in players:
				p.intent *= 0.3
				_move_player(p)
			_dribble()
			if state_timer <= 0.0:
				state = State.PLAYING
				if tick > 1 and clock == 0.0:
					_emit("whistle", {"kind": "kickoff"})
		State.PLAYING:
			_step_playing(frames)
		State.GOAL:
			state_timer -= DT
			_ai_all(false)
			for p in players:
				p.intent *= 0.5
				_move_player(p)
			ball.step(DT, mode)
			_keep_ball_in_net()
			if state_timer <= 0.0:
				_reset_for_kickoff(kickoff_team)
		State.HALFTIME:
			state_timer -= DT
			if state_timer <= 0.0:
				half = 2
				clock = 0.0
				kickoff_team = 1
				_reset_for_kickoff(1)
		State.FULLTIME:
			pass


func _step_playing(frames: Array) -> void:
	clock += DT
	protect_t = maxf(0.0, protect_t - DT)
	pass_receiver_t -= DT
	if pass_receiver_t <= 0.0:
		pass_receiver = -1
	if penalty_team >= 0:
		penalty_t -= DT
		if penalty_t <= 0.0 and ball.owner == penalty_taker:
			var tk: SimPlayer = players[penalty_taker]
			_shoot(tk, Vector2(team_dir(tk.team), 0.7 if rng.chance(0.5) else -0.7), 0.55)
	if clock >= half_seconds and not _ball_in_danger():
		if half == 1:
			state = State.HALFTIME
			state_timer = 3.0
			_emit("whistle", {"kind": "halftime"})
		else:
			state = State.FULLTIME
			_emit("whistle", {"kind": "fulltime"})
		return
	for t in 2:
		var f: InputFrame = frames[t] if t < frames.size() and frames[t] != null else InputFrame.new()
		_process_controller(t, f)
	_ai_all(false)
	for p in players:
		_move_player(p)
	_resolve_collisions()
	_slides()
	_dribble()
	_possession()
	_ball_physics()


## Não encerra o tempo com a bola viajando para o gol (acréscimo "justo").
func _ball_in_danger() -> bool:
	return ball.owner < 0 and ball.speed() > 10.0 and absf(ball.pos.x) > half_len * 0.6


# ---------------------------------------------------------------- controle (humano ou CPU)

func _process_controller(t: int, f: InputFrame) -> void:
	var c: Ctrl = ctrl[t]
	var pressed := f.buttons & ~c.prev
	var released := c.prev & ~f.buttons
	c.prev = f.buttons
	c.switch_cd = maxf(0.0, c.switch_cd - DT)
	c.help_t = maxf(0.0, c.help_t - DT)
	c.shoot_held = f.has(InputFrame.SHOOT)

	if (pressed & InputFrame.SWITCH) != 0 and c.switch_cd <= 0.0 and not team_has_ball(t):
		_switch_player(t, f.move)
	_auto_switch(t)

	var p: SimPlayer = players[c.player]
	p.intent = f.move.limit_length(1.0)
	p.want_sprint = f.has(InputFrame.SPRINT)
	p.shielding = f.has(InputFrame.SHIELD)
	p.face_ball = false
	for q in players:
		if q.team == t and q.id != p.id:
			q.shielding = false
	c.finesse_held = f.has(InputFrame.FINESSE)
	if f.move.length() > 0.25:
		c.aim = f.move.normalized()
	elif c.aim == Vector2.ZERO:
		c.aim = p.facing
	if p.windup > 0.0:
		# ainda dá para corrigir a mira enquanto arma a perna
		p.windup_aim = c.aim
	# "flick" do analógico direito = drible
	var rs := f.rstick
	if rs.length() > 0.7 and c.prev_rs.length() < 0.45 and ball.owner == p.id and penalty_team < 0:
		_start_skill(p, rs.normalized())
	c.prev_rs = rs
	if penalty_team >= 0:
		# na cobrança ninguém anda; só o batedor mira e chuta
		p.intent = Vector2.ZERO
		p.want_sprint = false
		if t != penalty_team:
			return

	# goleiro com a bola nas mãos: o humano pode repor com passe
	var keeper_hold := ball.owner >= 0 and players[ball.owner].is_keeper and players[ball.owner].team == t and players[ball.owner].hold > 0.0
	for b: int in [InputFrame.PASS, InputFrame.SHOOT, InputFrame.LOB, InputFrame.THROUGH]:
		if (pressed & b) != 0:
			if keeper_hold and (b == InputFrame.PASS or b == InputFrame.LOB):
				players[ball.owner].hold = 0.0
				_pass(players[ball.owner], c.aim, 0.6, false, b == InputFrame.LOB)
			else:
				_on_press(t, p, b)
		if (released & b) != 0 and c.charging == b:
			_on_release(t, p, b)
	if c.charging != 0:
		c.charge = minf(1.0, c.charge + DT / CHARGE_TIME)
	if c.pending != 0:
		c.pending_t -= DT
		if ball.owner == p.id:
			var kind := c.pending
			c.pending = 0
			# de primeira: sem tempo de armar
			_do_kick(p, kind, c.pending_power, c.aim, c.finesse_held)
		elif c.pending_t <= 0.0:
			c.pending = 0


func _on_press(t: int, p: SimPlayer, b: int) -> void:
	var c: Ctrl = ctrl[t]
	var opp_has := ball.owner >= 0 and players[ball.owner].team != t
	if opp_has:
		match b:
			InputFrame.PASS:
				_standing_tackle(p)
			InputFrame.SHOOT:
				_start_slide(p)
			InputFrame.THROUGH:
				c.help_t = 1.2
		return
	c.charging = b
	c.charge = 0.0


func _on_release(t: int, p: SimPlayer, b: int) -> void:
	var c: Ctrl = ctrl[t]
	var power := c.charge
	c.charging = 0
	c.charge = 0.0
	if ball.owner == p.id:
		_begin_windup(p, b, power, c.aim, c.finesse_held)
	else:
		# guarda a ação: se a bola chegar logo, executa de primeira
		c.pending = b
		c.pending_power = power
		c.pending_t = PENDING_WINDOW


func _switch_player(t: int, prefer: Vector2) -> void:
	var c: Ctrl = ctrl[t]
	var best: SimPlayer = null
	var best_s := INF
	var target := ball.pos + ball.vel * 0.3
	for p in players:
		if p.team != t or p.is_keeper or p.id == c.player:
			continue
		var to := target - p.pos
		var s := to.length()
		# o analógico "puxa" a escolha para quem está naquela direção
		if prefer.length() > 0.3:
			s -= (p.pos - players[c.player].pos).normalized().dot(prefer.normalized()) * 6.0
		if s < best_s:
			best_s = s
			best = p
	if best != null:
		c.player = best.id
		c.switch_cd = 0.15
		c.charging = 0
		c.pending = 0
		_emit("switch", {"team": t})


func _auto_switch(t: int) -> void:
	var c: Ctrl = ctrl[t]
	c.auto_t -= DT
	if c.auto_t > 0.0 or team_has_ball(t) or c.charging != 0:
		return
	c.auto_t = AUTO_SWITCH_EVERY
	var cur: SimPlayer = players[c.player]
	var n := nearest_to_ball(t)
	if n != null and n.id != cur.id and cur.pos.distance_to(ball.pos) > n.pos.distance_to(ball.pos) + 7.0:
		c.player = n.id
		_emit("switch", {"team": t})


# ---------------------------------------------------------------- ações com bola

## Começa o movimento do chute/passe: a bola só sai depois da "armação"
## (como no EA FC/eFootball), e o atleta desacelera enquanto isso.
func _begin_windup(p: SimPlayer, kind: int, power: float, aim: Vector2, finesse: bool) -> void:
	if p.windup > 0.0 or p.skill != SimPlayer.Skill.NONE:
		return
	match kind:
		InputFrame.SHOOT:
			p.windup = lerpf(0.12, 0.2, power)
		InputFrame.LOB:
			p.windup = 0.13
		_:
			p.windup = 0.08
	p.windup_total = p.windup
	p.windup_kind = kind
	p.kick_kind = kind
	p.windup_power = power
	p.windup_aim = aim
	p.windup_finesse = finesse
	_emit("windup", {"team": p.team, "id": p.id, "kind": kind})


func _do_kick(p: SimPlayer, kind: int, power: float, aim: Vector2, finesse: bool = false) -> void:
	if ball.owner != p.id:
		return
	match kind:
		InputFrame.PASS:
			_pass(p, aim, power, false, false)
		InputFrame.THROUGH:
			_pass(p, aim, power, true, false)
		InputFrame.LOB:
			_pass(p, aim, power, false, true)
		InputFrame.SHOOT:
			_shoot(p, aim, power, finesse)
			_try_block(p)


## Chute travado: defensor colado na frente de quem chuta pode bloquear a bola.
func _try_block(kicker: SimPlayer) -> void:
	var dirv := ball.vel.normalized()
	for o in players:
		if o.team == kicker.team or o.is_keeper or o.stun > 0.0:
			continue
		var to := o.pos - kicker.pos
		var d := to.length()
		if d < 0.3 or d > 1.7 or to.normalized().dot(dirv) < 0.82:
			continue
		var chance := 0.25 + 0.35 * o.defense + (0.1 if o.has_trait(SimPlayer.Trait.MURALHA) else 0.0) - 0.15 * (d - 0.3)
		if rng.chance(chance):
			var spd := ball.vel.length()
			var side := Vector2(-dirv.y, dirv.x) * (1.0 if rng.chance(0.5) else -1.0)
			ball.vel = (-dirv * 0.35 + side * 0.65).normalized() * spd * rng.range_f(0.25, 0.45)
			ball.vz = rng.range_f(1.0, 4.0)
			ball.spin = 0.0
			ball.last_touch_team = o.team
			ball.last_touch_id = o.id
			_emit("block", {"team": o.team, "id": o.id})
		return


func _release_ball(p: SimPlayer) -> void:
	ball.owner = -1
	ball.last_touch_team = p.team
	ball.last_touch_id = p.id
	p.kick_cd = 0.28
	p.kick_anim = 0.25
	p.hold = 0.0
	protect_t = 0.0
	_mark_offside(p)
	penalty_team = -1


func _pass(p: SimPlayer, aim: Vector2, power: float, through: bool, lofted: bool) -> void:
	p.kick_kind = InputFrame.LOB if lofted else (InputFrame.THROUGH if through else InputFrame.PASS)
	if aim.length_squared() < 0.01:
		aim = p.facing
	aim = aim.normalized()
	var dir := team_dir(p.team)
	var best: SimPlayer = null
	var best_s := -INF
	var max_d := half_len * (1.5 if lofted else 1.1)
	for m in players:
		if m.team != p.team or m == p:
			continue
		var to := m.pos - ball.pos
		var d := to.length()
		if d < 2.5 or d > max_d:
			continue
		var cosang := aim.dot(to / d)
		if cosang < 0.55:
			continue
		var s := cosang * 2.2 - d / max_d
		if m.is_keeper:
			s -= 1.0
		if not lofted:
			for o in players:
				if o.team != p.team and Geometry2D.get_closest_point_to_segment(o.pos, ball.pos, m.pos).distance_to(o.pos) < 1.3:
					s -= 0.5
		if s > best_s:
			best_s = s
			best = m
	var roll: float = mode["roll_decel"]
	var target: Vector2
	if best != null:
		target = best.pos
		if through:
			var run_dir := (aim * 0.5 + Vector2(dir, 0) * 0.5).normalized()
			target = best.pos + run_dir * lerpf(4.0, 12.0, power)
		else:
			# antecipa o movimento do companheiro
			for _i in 2:
				var tt := ball.pos.distance_to(target) / lerpf(10.0, 18.0, power)
				target = best.pos + best.vel * tt
		pass_receiver = best.id
		pass_receiver_t = 3.0
	else:
		target = ball.pos + aim * lerpf(8.0, 32.0, power)
	target.x = clampf(target.x, -half_len - 2.0, half_len + 2.0)
	target.y = clampf(target.y, -half_w + 0.5, half_w - 0.5)
	var to_t := target - ball.pos
	var d := maxf(to_t.length(), 0.5)
	var dirv := to_t / d
	var err := rng.gauss() * 0.03 * (1.25 - p.passing) * (0.6 + power) * (1.6 if p.sprinting else 1.0) * (0.65 if p.has_trait(SimPlayer.Trait.MAESTRO) else 1.0)
	dirv = dirv.rotated(err)
	if lofted:
		var flight := 0.75 + d / 30.0 + power * 0.25
		ball.vz = 0.5 * SimBall.G * flight
		ball.vel = dirv * (d * 0.86 / flight)
		ball.spin = rng.gauss() * 0.8
	else:
		var v_end := 2.5 + power * 4.5
		var v0 := clampf(sqrt(v_end * v_end + 2.0 * roll * d) * 1.06, 5.0, 30.0)
		ball.vel = dirv * v0
		ball.vz = 0.6 if v0 > 22.0 else 0.0
		ball.spin = 0.0
	if p.hold > 0.0 or ball.z > 0.5:
		ball.z = maxf(ball.z, 0.3)
	_release_ball(p)
	_emit("kick", {"power": power, "kind": "lob" if lofted else "pass", "team": p.team})


func _shoot(p: SimPlayer, aim: Vector2, power: float, finesse: bool = false) -> void:
	p.kick_kind = InputFrame.SHOOT
	var dir := team_dir(p.team)
	var gx := half_len * dir
	var aim_y: float
	if absf(aim.y) > 0.2:
		aim_y = clampf(aim.y * 1.4, -1.0, 1.0) * goal_half * 0.8
	else:
		# sem direção: busca o canto oposto ao lado em que está
		aim_y = -signf(p.pos.y if absf(p.pos.y) > 0.5 else 1.0) * goal_half * 0.55
	if finesse:
		# colocado: sempre no canto, com curva para dentro
		var side := signf(aim_y) if absf(aim_y) > 0.1 else 1.0
		aim_y = side * goal_half * 0.84
	var target := Vector2(gx + dir * 0.5, aim_y)
	var to := target - ball.pos
	var d := maxf(to.length(), 1.0)
	var dirv := to / d
	var spd := lerpf(13.0, 31.0, power) * (0.78 + 0.3 * p.shot)
	if finesse:
		spd *= 0.8
	# pressão: adversário perto atrapalha
	var opp := nearest_opponent(p)
	var pressure := 0.0
	if opp != null:
		pressure = clampf(1.0 - opp.pos.distance_to(p.pos) / 3.0, 0.0, 1.0)
	var err := (0.02 + 0.07 * power * power) * (1.3 - p.shot) * (1.0 + pressure * 0.8) * (1.4 if p.sprinting else 1.0)
	if p.has_trait(SimPlayer.Trait.FINALIZADOR):
		err *= 0.8
	if finesse and p.has_trait(SimPlayer.Trait.COLOCADO):
		err *= 0.75
	if p.has_trait(SimPlayer.Trait.LONGA_DISTANCIA) and d > 20.0:
		spd *= 1.1
		err *= 0.85
	# corpo virado para longe do alvo = chute pior (precisa ajeitar o corpo)
	var body := absf(p.facing.angle_to(dirv))
	err *= 1.0 + maxf(0.0, body - 0.6) * 0.9
	if finesse:
		err *= 0.65
	dirv = dirv.rotated(rng.gauss() * err)
	var curl := 0.0
	if finesse:
		# sai por fora do alvo e faz a curva para o meio do gol
		var perp := Vector2(-dirv.y, dirv.x)
		curl = signf(perp.dot(Vector2(gx, 0.0) - target)) if absf(target.y) > 0.1 else 1.0
		dirv = dirv.rotated(-curl * clampf(0.9 / d, 0.02, 0.09))
	ball.vel = dirv * spd
	var t_flight := d / (spd * 0.9)
	var h := lerpf(0.15, 2.7, pow(power, 1.8)) + rng.gauss() * 0.25 * power
	if finesse:
		h = lerpf(0.6, 1.9, power) + rng.gauss() * 0.15
	h = maxf(h, 0.0)
	ball.vz = clampf((h + 0.5 * SimBall.G * t_flight * t_flight) / t_flight, 0.0, 14.0)
	ball.spin = rng.gauss() * 1.2
	if finesse:
		ball.spin = curl * (2.2 + 0.8 * p.shot) * (1.15 if p.has_trait(SimPlayer.Trait.COLOCADO) else 1.0)
	stats_shots[p.team] += 1
	_release_ball(p)
	_emit("kick", {"power": power, "kind": "shot", "team": p.team})


# ---------------------------------------------------------------- defesa

func _standing_tackle(p: SimPlayer) -> void:
	if p.tackle_anim > 0.0 or p.stun > 0.0 or p.slide > 0.0 or p.off_balance > 0.0:
		return
	p.tackle_anim = 0.35
	p.vel += p.facing * 1.5
	_emit("tackle_try", {"team": p.team})
	if ball.owner < 0:
		return
	var o: SimPlayer = players[ball.owner]
	if o.team == p.team or (protect_t > 0.0 and protect_team == o.team) or o.hold > 0.0:
		return
	var d := p.pos.distance_to(ball.pos)
	if d > 1.5:
		p.stun = 0.25
		return
	var from_behind := o.facing.dot((p.pos - o.pos).normalized()) < -0.45
	if from_behind and rng.chance(0.3):
		_foul(p, o)
		return
	var chance := 0.42 + 0.4 * p.defense - 0.32 * o.control + (0.0 if from_behind else 0.12)
	if p.has_trait(SimPlayer.Trait.MURALHA):
		chance += 0.1
	if o.has_trait(SimPlayer.Trait.DRIBLADOR):
		chance -= 0.06
	if o.shielding:
		chance -= 0.12 + 0.12 * o.control
	if o.skill == SimPlayer.Skill.ROULETTE:
		chance -= 0.3
	if p.shielding:
		chance += 0.08   # bote depois de conter é mais preciso
	if rng.chance(chance):
		ball.owner = -1
		var away := (p.facing * 0.6 + (ball.pos - o.pos).normalized() * 0.4).normalized()
		ball.vel = away * rng.range_f(3.0, 6.5)
		ball.last_touch_team = p.team
		ball.last_touch_id = p.id
		o.stun = 0.3
		_emit("tackle", {"team": p.team, "ok": true})
	else:
		p.stun = 0.45
		_emit("tackle", {"team": p.team, "ok": false})


func _start_slide(p: SimPlayer) -> void:
	if p.slide > 0.0 or p.stun > 0.0 or p.is_keeper or p.off_balance > 0.0:
		return
	p.slide = 0.6
	p.slide_hit_ball = false
	p.vel = p.facing * maxf(p.vel.length() + 2.0, 7.5)
	_emit("slide", {"team": p.team})


func _slides() -> void:
	for p in players:
		if p.slide <= 0.0:
			continue
		if not p.slide_hit_ball and ball.z < 0.5 and p.pos.distance_to(ball.pos) < 0.95:
			if ball.owner >= 0:
				players[ball.owner].stun = 0.35
			ball.owner = -1
			ball.vel = (p.facing * 7.0 + p.vel * 0.3)
			ball.last_touch_team = p.team
			ball.last_touch_id = p.id
			p.slide_hit_ball = true
			_emit("tackle", {"team": p.team, "ok": true})
		for o in players:
			if o.team == p.team or o.stun > 0.0 or o.is_keeper and o.hold > 0.0:
				continue
			if p.pos.distance_to(o.pos) < 0.8:
				if p.slide_hit_ball:
					o.stun = 0.8   # derrubou depois de tocar na bola: lance legal
				else:
					_foul(p, o)
					return


func _foul(offender: SimPlayer, victim: SimPlayer) -> void:
	offender.stun = 1.0
	offender.slide = 0.0
	victim.stun = 0.9
	_emit("foul", {"team": offender.team})
	var spot := victim.pos
	spot.x = clampf(spot.x, -half_len + 1.0, half_len - 1.0)
	spot.y = clampf(spot.y, -half_w + 1.0, half_w - 1.0)
	# falta dentro da própria área = pênalti
	var gx := -half_len * team_dir(offender.team)
	if float(mode.get("penalty_dist", 0.0)) > 0.0 and absf(spot.x - gx) < float(mode["box_depth"]) and absf(spot.y) < float(mode["box_half"]):
		_setup_penalty(victim.team, victim)
		return
	_setup_restart(victim.team, spot, "freekick")


func _setup_penalty(t: int, taker: SimPlayer) -> void:
	var dir := team_dir(t)
	var gx := half_len * dir
	var spot := Vector2(gx - dir * float(mode["penalty_dist"]), 0.0)
	ball.pos = spot
	ball.vel = Vector2.ZERO
	ball.z = 0.0
	ball.vz = 0.0
	ball.spin = 0.0
	ball.owner = taker.id
	ball.last_touch_team = t
	pass_receiver = -1
	offside_ids.clear()
	offside_team = -1
	# todos fora da área, atrás da bola; goleiro na linha
	var line_x := gx - dir * (float(mode["box_depth"]) + 2.0)
	var k := 0
	for p in players:
		p.vel = Vector2.ZERO
		p.stun = 0.0
		p.slide = 0.0
		p.hold = 0.0
		if p == taker:
			p.pos = spot - Vector2(dir * 0.7, 0.0)
			p.facing = Vector2(dir, 0.0)
		elif p.is_keeper and p.team != t:
			p.pos = Vector2(gx - dir * 0.3, 0.0)
			p.facing = Vector2(-dir, 0.0)
		elif p.is_keeper:
			continue
		else:
			var row := k / 6
			p.pos = Vector2(line_x - dir * (row * 1.5 + 0.5), -half_w * 0.5 + float(k % 6) * half_w * 0.2)
			p.facing = Vector2(dir, 0.0)
			k += 1
	ctrl[t].player = taker.id
	for c in ctrl:
		c.charging = 0
		c.pending = 0
	penalty_team = t
	penalty_taker = taker.id
	penalty_t = 8.0
	protect_team = t
	protect_t = 999.0
	state = State.RESTART
	state_timer = 1.4
	_emit("whistle", {"kind": "penalty", "team": t})


func _mark_offside(p: SimPlayer) -> void:
	offside_ids.clear()
	offside_team = -1
	if not bool(mode.get("offside", false)) or no_offside_kick or penalty_team >= 0:
		no_offside_kick = false
		return
	var dir := team_dir(p.team)
	var rels: Array[float] = []
	for o in players:
		if o.team != p.team:
			rels.append(o.pos.x * dir)
	rels.sort()
	var second_last: float = rels[rels.size() - 2] if rels.size() >= 2 else half_len
	var line := maxf(second_last, ball.pos.x * dir)
	for m in players:
		if m.team == p.team and m != p:
			var r := m.pos.x * dir
			if r > 0.0 and r > line + 0.3:
				offside_ids.append(m.id)
	if not offside_ids.is_empty():
		offside_team = p.team


## true = lance parado por impedimento.
func _check_offside(p: SimPlayer) -> bool:
	if offside_team < 0:
		return false
	if p.team == offside_team and p.id in offside_ids:
		_emit("offside", {"team": p.team})
		var spot := p.pos
		spot.x = clampf(spot.x, -half_len + 1.0, half_len - 1.0)
		spot.y = clampf(spot.y, -half_w + 1.0, half_w - 1.0)
		offside_ids.clear()
		offside_team = -1
		_setup_restart(1 - p.team, spot, "offside")
		return true
	# adversário tocou ou companheiro em posição legal recebeu: nova fase
	offside_ids.clear()
	offside_team = -1
	return false


# ---------------------------------------------------------------- IA sem bola

func _ai_all(frozen: bool) -> void:
	for p in players:
		var human := ctrl[p.team].player == p.id
		if human and state == State.PLAYING:
			continue
		if penalty_team >= 0 and not p.is_keeper:
			p.intent = Vector2.ZERO
			p.want_sprint = false
			continue
		if p.is_keeper:
			_ai_keeper(p, frozen)
		else:
			_ai_outfield(p, frozen)


func _formation_target(p: SimPlayer, attacking: bool) -> Vector2:
	var dir := team_dir(p.team)
	var bx_rel := ball.pos.x * dir
	var base_x := p.base.x * half_len
	var tx := base_x * 0.55 + bx_rel * 0.55 + (half_len * 0.24 if attacking else -half_len * 0.06)
	tx += float(team_mentality[p.team] - 1) * half_len * 0.09
	if not attacking:
		tx = minf(tx, bx_rel + 1.0)   # fica entre a bola e o próprio gol
	tx = clampf(tx, -half_len * 0.9, half_len * 0.86)
	var ty := p.base.y * half_w * (0.9 if attacking else 0.62) + ball.pos.y * 0.3
	ty = clampf(ty, -half_w * 0.92, half_w * 0.92)
	return Vector2(tx * dir, ty)


func _ai_outfield(p: SimPlayer, frozen: bool) -> void:
	var t := p.team
	var attacking := team_has_ball(t)
	var target := _formation_target(p, attacking)
	var urgent := false
	if not frozen and state == State.PLAYING:
		if pass_receiver == p.id and ball.owner < 0:
			target = _intercept_point(p)
			urgent = true
		elif ball.owner < 0 and nearest_to_ball(t) == p and ctrl[t].player != p.id:
			var human := players[ctrl[t].player]
			if human.pos.distance_to(ball.pos) > p.pos.distance_to(ball.pos) + 3.0:
				target = _intercept_point(p)
				urgent = true
		elif not attacking and ball.owner >= 0:
			# segundo homem: cobre entre a bola e o gol; com "chamar marcador" pressiona
			var helper := nearest_to_ball(t, ctrl[t].player)
			if helper == p:
				var own_goal := Vector2(-half_len * team_dir(t), 0)
				if ctrl[t].help_t > 0.0:
					target = ball.pos
					urgent = true
				elif team_press[t] == 2 and p.pos.distance_to(ball.pos) < half_len * 0.45:
					# pressão alta: o segundo homem vai no portador
					target = ball.pos
					urgent = true
				elif p.pos.distance_to(ball.pos) < half_len * (0.3 + 0.15 * team_press[t]):
					target = ball.pos + (own_goal - ball.pos).normalized() * (4.5 - 1.5 * team_press[t])
	_steer_to(p, target, urgent)


func _intercept_point(p: SimPlayer) -> Vector2:
	var my_speed := p.top_speed()
	var best := ball.pos
	var sim_pos := ball.pos
	var v := ball.vel
	var roll: float = mode["roll_decel"]
	for i in 30:
		var t := (i + 1) * 0.1
		var s := v.length()
		if s > 0.0:
			v = v * (maxf(0.0, s - roll * 0.1) / s)
		sim_pos += v * 0.1
		best = sim_pos
		if p.pos.distance_to(sim_pos) / my_speed <= t:
			break
	return best


func _steer_to(p: SimPlayer, target: Vector2, urgent: bool) -> void:
	var to := target - p.pos
	var d := to.length()
	# histerese: parado só sai do lugar se o alvo se afastar de verdade
	var moving := p.vel.length() > 0.5
	if d < (0.35 if moving else 0.9):
		p.intent = Vector2.ZERO
		if ball.pos.distance_to(p.pos) > 0.1:
			p.facing = _rotate_toward(p.facing, (ball.pos - p.pos).normalized(), 0.05)
	else:
		# chegada suave: desacelera nos últimos metros
		p.intent = (to / d) * clampf(d / 4.0, 0.15, 1.0)
	# pique com histerese (não liga/desliga a cada passo)
	p.want_sprint = urgent or d > (7.0 if p.sprinting else 11.0)
	# sem urgência e perto do lance: acompanha a jogada de frente para a bola
	p.face_ball = not urgent and d < 10.0 and ball.pos.distance_to(p.pos) < 35.0


func _ai_keeper(p: SimPlayer, frozen: bool) -> void:
	var dir := team_dir(p.team)
	var gx := -half_len * dir
	var goal := Vector2(gx, 0)
	if penalty_team >= 0 and p.team != penalty_team:
		_steer_to(p, Vector2(gx + dir * 0.3, 0.0), false)
		return
	if ball.owner == p.id:
		p.intent = Vector2.ZERO
		p.facing = Vector2(dir, 0)
		if not frozen and p.hold > 0.0:
			p.hold -= DT
			if p.hold <= 0.0:
				# reposição automática para o companheiro mais livre à frente
				_pass(p, Vector2(dir, rng.range_f(-0.6, 0.6)).normalized(), 0.55, false, rng.chance(0.4))
		elif not frozen and state == State.PLAYING:
			_pass(p, Vector2(dir, 0), 0.5, false, true)
		return
	var target: Vector2
	var urgent := false
	var to_ball := ball.pos - goal
	var shot_threat := false
	if ball.owner < 0 and ball.vel.x * dir < -5.0:
		var t_cross := (gx - ball.pos.x) / ball.vel.x
		if t_cross > 0.0 and t_cross < 2.2:
			var y_cross := ball.pos.y + ball.vel.y * t_cross
			if absf(y_cross) < goal_half + 1.5:
				shot_threat = true
				target = Vector2(gx + dir * 0.7, clampf(y_cross, -goal_half, goal_half))
				urgent = true
				if p.dive <= 0.0 and p.pos.distance_to(target) > 1.1 and t_cross < 0.7:
					p.dive = 0.6
					_emit("dive", {"team": p.team})
	if not shot_threat:
		var out := clampf(to_ball.length() * 0.12, 0.6, float(mode["box_depth"]) * 0.3)
		target = goal + to_ball.normalized() * out if to_ball.length() > 0.1 else goal + Vector2(dir, 0)
		target.y = clampf(target.y, -goal_half - 0.5, goal_half + 0.5)
		# sai do gol em bola solta dentro da área
		if ball.owner < 0 and absf(ball.pos.x - gx) < float(mode["box_depth"]) and absf(ball.pos.y) < float(mode["box_half"]):
			var n := nearest_to_ball(1 - p.team)
			if n == null or n.pos.distance_to(ball.pos) > p.pos.distance_to(ball.pos) - 1.0:
				target = _intercept_point(p)
				urgent = true
	_steer_to(p, target, urgent)


# ---------------------------------------------------------------- movimento

func _move_player(p: SimPlayer) -> void:
	p.kick_cd = maxf(0.0, p.kick_cd - DT)
	p.tackle_anim = maxf(0.0, p.tackle_anim - DT)
	p.kick_anim = maxf(0.0, p.kick_anim - DT)
	p.dive = maxf(0.0, p.dive - DT)
	p.skill_cd = maxf(0.0, p.skill_cd - DT)
	p.burst = maxf(0.0, p.burst - DT)
	p.off_balance = maxf(0.0, p.off_balance - DT)
	p.touch_t = maxf(0.0, p.touch_t - DT)
	if p.windup > 0.0:
		p.windup -= DT
		if p.windup <= 0.0:
			p.windup = 0.0
			if ball.owner == p.id and state == State.PLAYING:
				_do_kick(p, p.windup_kind, p.windup_power, p.windup_aim, p.windup_finesse)
	if p.skill != SimPlayer.Skill.NONE:
		if p.stun > 0.0 or p.slide > 0.0 or (p.skill != SimPlayer.Skill.KNOCK and ball.owner != p.id):
			p.skill = SimPlayer.Skill.NONE   # perdeu a bola / derrubado: cancela o drible
		elif p.skill == SimPlayer.Skill.KNOCK:
			p.skill_t += DT
			if p.skill_t >= SimPlayer.SKILL_TIME[SimPlayer.Skill.KNOCK]:
				p.skill = SimPlayer.Skill.NONE
				p.skill_cd = 0.25
	if p.skill != SimPlayer.Skill.NONE and p.skill != SimPlayer.Skill.KNOCK:
		_skill_move(p)
		_clamp_player(p)
		return
	if p.stun > 0.0:
		p.stun -= DT
		p.vel = p.vel.move_toward(Vector2.ZERO, 14.0 * DT)
		p.pos += p.vel * DT
		p.sprinting = false
		_clamp_player(p)
		return
	if p.slide > 0.0:
		p.slide -= DT
		p.vel = p.vel.move_toward(Vector2.ZERO, 9.0 * DT)
		p.pos += p.vel * DT
		if p.slide <= 0.0:
			p.stun = 0.45   # levantando
		_clamp_player(p)
		return
	var has_ball := ball.owner == p.id
	var intent := p.intent.limit_length(1.0)
	p.sprinting = p.want_sprint and p.stamina > 0.05 and intent.length() > 0.3
	var max_s := p.top_speed() if p.sprinting else p.jog_speed()
	max_s *= 0.8 + 0.2 * p.stamina
	if has_ball:
		max_s *= lerpf(0.86, 0.94, p.control)
		if p.shielding:
			# condução curta / proteção: lento, bola colada
			max_s = minf(max_s, p.jog_speed() * 0.6)
	elif p.shielding and ball.owner >= 0 and players[ball.owner].team != p.team:
		# contenção (jockey): de frente para a bola, passos curtos
		max_s = minf(max_s, p.jog_speed() * 0.78)
	if p.windup > 0.0:
		max_s *= 0.55
	if p.face_ball and not has_ball and intent.length() > 0.1:
		# andar de costas/de lado é mais lento que de frente
		var back := clampf(-intent.normalized().dot(p.facing), 0.0, 1.0)
		var side := 1.0 - absf(intent.normalized().dot(p.facing))
		max_s *= 1.0 - 0.4 * back - 0.15 * side
	if p.burst > 0.0:
		max_s *= 1.07
	if p.off_balance > 0.0:
		max_s *= 0.3
	if p.is_keeper and p.dive > 0.0:
		max_s = 9.5
	if p.hold > 0.0:
		max_s *= 0.4
	var target_v := intent * max_s
	var spd := p.vel.length()
	var acc := lerpf(5.0, 8.5, p.accel) * (1.6 if p.burst > 0.0 else 1.0) * (0.4 if p.off_balance > 0.0 else 1.0)
	var brake := 12.0
	var rate := acc
	if target_v.length() < spd - 0.2 or target_v.dot(p.vel) < 0.0:
		rate = brake
	# mudança brusca de direção em alta velocidade custa mais (inércia)
	if spd > 3.0 and target_v.length() > 0.1:
		var turn := absf(p.vel.angle_to(target_v))
		if turn > 0.8:
			rate *= lerpf(1.0, 0.55, clampf((spd - 3.0) / 5.0, 0.0, 1.0))
	p.vel += (target_v - p.vel).limit_length(rate * DT)
	p.pos += p.vel * DT
	var face_target := p.vel if p.vel.length() > 0.4 else intent
	if p.face_ball and not has_ball and not p.sprinting and ctrl[p.team].player != p.id:
		face_target = ball.pos - p.pos
	var turn_rate := lerpf(11.0, 4.0, clampf(p.vel.length() / 9.0, 0.0, 1.0)) * (0.8 if has_ball else 1.0)
	if p.shielding and not p.is_keeper:
		if has_ball:
			var opp := nearest_opponent(p)
			if opp != null and opp.pos.distance_to(p.pos) < 2.0 and intent.length() < 0.5:
				# de costas para o marcador
				face_target = p.pos - opp.pos
				turn_rate = 7.0
		elif ball.owner >= 0 and players[ball.owner].team != p.team:
			face_target = ball.pos - p.pos
			turn_rate = 12.0
	if p.windup > 0.0:
		turn_rate *= 0.4
	p.facing = _rotate_toward(p.facing, face_target.normalized(), turn_rate * DT).normalized()
	if p.sprinting:
		p.stamina = maxf(0.0, p.stamina - 0.055 * DT * (0.65 if p.has_trait(SimPlayer.Trait.RESISTENCIA) else 1.0))
	else:
		p.stamina = minf(1.0, p.stamina + 0.03 * DT)
	p.run_phase += p.vel.length() * DT * 2.3
	_clamp_player(p)


func _clamp_player(p: SimPlayer) -> void:
	var m: float = mode["run_margin"]
	var r := SimPlayer.RADIUS
	p.pos.x = clampf(p.pos.x, -half_len - m + r, half_len + m - r)
	p.pos.y = clampf(p.pos.y, -half_w - m + r, half_w + m - r)


func _resolve_collisions() -> void:
	var n := players.size()
	var min_d := SimPlayer.RADIUS * 2.0
	for i in n:
		var a: SimPlayer = players[i]
		for j in range(i + 1, n):
			var b: SimPlayer = players[j]
			var d := b.pos - a.pos
			var l := d.length()
			if l < min_d and l > 0.0001:
				var push := d / l * (min_d - l) * 0.5
				# corpo a corpo: quem está mais rápido empurra mais
				var wa := 0.5 + clampf((b.vel.length() - a.vel.length()) * 0.05, -0.3, 0.3)
				a.pos -= push * 2.0 * wa
				b.pos += push * 2.0 * (1.0 - wa)


# ---------------------------------------------------------------- bola

func _dribble() -> void:
	if ball.owner < 0:
		return
	var o: SimPlayer = players[ball.owner]
	if o.is_keeper and o.hold > 0.0:
		ball.pos = o.pos + o.facing * 0.35
		ball.pos.x = clampf(ball.pos.x, -half_len + ball.radius, half_len - ball.radius)
		ball.z = 1.0
		ball.vz = 0.0
		ball.vel = o.vel
		return
	if o.stun > 0.0 or o.slide > 0.0 or o.pos.distance_to(ball.pos) > 1.7:
		ball.owner = -1
		return
	if o.skill != SimPlayer.Skill.NONE:
		_skill_ball(o)
		return
	var spd := o.vel.length()
	var ahead := 0.42 + spd * 0.07 + (0.45 if o.sprinting else 0.0)
	if o.shielding:
		ahead = 0.34 + spd * 0.03
	if o.windup > 0.0:
		ahead = 0.4
	var target := o.pos + o.facing * ahead
	var k := 6.0 + 6.0 * o.control
	var desired := o.vel + (target - ball.pos) * k
	var blend := clampf((0.22 + 0.3 * o.control) * 60.0 * DT, 0.0, 1.0)
	ball.vel = ball.vel.lerp(desired, blend)
	if ball.z < 0.35:
		ball.z = 0.0
		ball.vz = 0.0
	ball.spin = 0.0


func _possession() -> void:
	var best: SimPlayer = null
	var best_d := INF
	var owner_d := INF
	if ball.owner >= 0:
		owner_d = players[ball.owner].pos.distance_to(ball.pos)
	for p in players:
		if p.kick_cd > 0.0 or p.stun > 0.0 or p.slide > 0.0 or p.id == ball.owner:
			continue
		if protect_t > 0.0 and p.team != protect_team:
			continue
		var keeper_zone := p.is_keeper and in_own_box(p)
		var reach := 0.55 + 0.25 * p.control
		var max_z := 0.9
		if keeper_zone:
			reach = 1.0 + (1.3 if p.dive > 0.0 else 0.0)
			max_z = 2.6
		var d := p.pos.distance_to(ball.pos)
		if d > reach:
			continue
		if ball.z > max_z:
			if ball.z < (2.7 if p.has_trait(SimPlayer.Trait.CABECEADOR) else 2.4) and not keeper_zone:
				_header(p)
				return
			continue
		if ball.owner >= 0:
			var o: SimPlayer = players[ball.owner]
			if o.team == p.team or o.hold > 0.0:
				continue
			# só rouba se chegou na bola antes do dono (bola "solta" do pé dele)
			if d >= owner_d - 0.15:
				continue
		if d < best_d:
			best_d = d
			best = p
	if best != null:
		_take(best)


func _take(p: SimPlayer) -> void:
	if _check_offside(p):
		return
	var rel := (ball.vel - p.vel).length()
	var keeper_zone := p.is_keeper and in_own_box(p)
	var limit := 9.0 + 12.0 * p.control
	if keeper_zone:
		var catch_p := clampf(1.35 - rel / 30.0 + p.defense * 0.15 + (0.12 if p.has_trait(SimPlayer.Trait.GOLEIRO_SEGURO) else 0.0), 0.12, 0.97)
		if rel > 8.0 and not rng.chance(catch_p):
			# espalmada
			ball.vel = Vector2(-ball.vel.x * 0.35, ball.vel.y * 0.3 + rng.gauss() * 5.0)
			ball.vz = absf(ball.vz) * 0.3 + 1.5
			ball.last_touch_team = p.team
			ball.last_touch_id = p.id
			p.kick_cd = 0.35
			ball.owner = -1
			_emit("save", {"team": p.team, "caught": false})
			return
		ball.owner = p.id
		p.hold = 2.2
		p.vel *= 0.3
		ball.last_touch_team = p.team
		ball.last_touch_id = p.id
		pass_receiver = -1
		if rel > 8.0:
			_emit("save", {"team": p.team, "caught": true})
		return
	if rel > limit:
		# bola rápida demais: desvia (domínio falho)
		ball.vel = ball.vel * 0.4 + p.facing * 1.5
		ball.vz = absf(ball.vz) * 0.3
		ball.last_touch_team = p.team
		ball.last_touch_id = p.id
		p.kick_cd = 0.2
		if ball.owner >= 0:
			ball.owner = -1
		_emit("touch", {"team": p.team})
		return
	var prev_owner := ball.owner
	ball.owner = p.id
	var want := p.intent
	if p.shielding:
		# amortece e mata a bola
		ball.vel = p.vel * 0.5
	elif want.length() > 0.35 and rel > 2.0:
		# domínio orientado: o primeiro toque já leva a bola para onde o analógico aponta
		var miss := clampf((rel - 6.0) / 18.0, 0.0, 0.7) * (1.15 - p.control)
		var d := want.normalized().rotated(rng.gauss() * miss * 0.9)
		ball.vel = p.vel * 0.5 + d * (2.0 + (2.6 if p.want_sprint else 0.6))
		p.facing = _rotate_toward(p.facing, d, 1.2)
	else:
		ball.vel = p.vel * 0.85 + (ball.vel - p.vel) * 0.12
	if rel > 2.0:
		p.touch_t = 0.3
	ball.vz = 0.0
	ball.last_touch_team = p.team
	ball.last_touch_id = p.id
	pass_receiver = -1
	if ctrl[p.team].player != p.id:
		ctrl[p.team].player = p.id
		ctrl[p.team].charging = 0
	if prev_owner >= 0 and players[prev_owner].team != p.team:
		_emit("steal", {"team": p.team})


# ---------------------------------------------------------------- dribles (analógico direito)
## Direção do "flick" relativa ao corpo:
##   frente → toque longo e arrancada | trás → puxada (giro de 180°)
##   lado → corte/rolinho para o lado
##   LT/L2 + lado → pedalada (finta que desequilibra o marcador)
##   LT/L2 + trás → roleta (giro protegendo a bola)
func _start_skill(p: SimPlayer, rs: Vector2) -> void:
	if p.skill != SimPlayer.Skill.NONE or p.skill_cd > 0.0 or p.windup > 0.0 or p.hold > 0.0 or p.stun > 0.0:
		return
	var fwd := p.facing.normalized()
	var perp := Vector2(-fwd.y, fwd.x)
	var f := rs.dot(fwd)
	var side := rs.dot(perp)
	var kind: int
	if f > 0.6:
		kind = SimPlayer.Skill.KNOCK
	elif f < -0.6:
		kind = SimPlayer.Skill.ROULETTE if p.shielding else SimPlayer.Skill.DRAG
	else:
		kind = SimPlayer.Skill.STEPOVER if p.shielding else SimPlayer.Skill.ROLL
	p.skill = kind
	p.skill_t = 0.0
	p.skill_dir = fwd
	p.skill_side = signf(side) if absf(side) > 0.1 else 1.0
	_emit("skill", {"team": p.team, "id": p.id, "kind": kind})
	match kind:
		SimPlayer.Skill.KNOCK:
			ball.owner = -1
			ball.vel = fwd * (p.vel.length() + 4.2 + 1.6 * p.control)
			ball.last_touch_team = p.team
			ball.last_touch_id = p.id
			p.kick_cd = 0.16
			p.kick_anim = 0.25
			p.burst = 0.75
		SimPlayer.Skill.STEPOVER:
			# quem estiver na frente pode cair na finta
			for o in players:
				if o.team == p.team or o.is_keeper or o.stun > 0.0:
					continue
				var to := o.pos - p.pos
				var dist := to.length()
				if dist < 3.4 and to.dot(fwd) > 0.0:
					var chance := 0.3 + 0.45 * p.control - 0.3 * o.defense + (0.15 if p.has_trait(SimPlayer.Trait.DRIBLADOR) else 0.0)
					if rng.chance(chance):
						o.off_balance = 0.65
						o.vel += perp * p.skill_side * 2.2
						_emit("fooled", {"team": o.team, "id": o.id})


func _skill_move(p: SimPlayer) -> void:
	p.skill_t += DT
	var dur: float = SimPlayer.SKILL_TIME[p.skill]
	var u := clampf(p.skill_t / dur, 0.0, 1.0)
	var fwd := p.skill_dir
	var perp := Vector2(-fwd.y, fwd.x) * p.skill_side
	var v := Vector2.ZERO
	match p.skill:
		SimPlayer.Skill.DRAG:
			v = -fwd * 1.6 * sin(u * PI)
			var tgt := perp if u < 0.5 else -fwd
			p.facing = _rotate_toward(p.facing, tgt, 10.0 * DT).normalized()
		SimPlayer.Skill.ROLL:
			v = perp * lerpf(1.2, 4.6, sin(u * PI * 0.5)) + fwd * 1.2
			if u > 0.55:
				p.facing = _rotate_toward(p.facing, (fwd + perp).normalized(), 7.0 * DT).normalized()
		SimPlayer.Skill.STEPOVER:
			v = fwd * 1.3 + perp * sin(u * TAU) * 0.9
		SimPlayer.Skill.ROULETTE:
			v = perp * 2.6 * sin(u * PI) + fwd * 0.8
			p.facing = fwd.rotated(-p.skill_side * TAU * u)
	p.vel = p.vel.lerp(v, minf(1.0, 18.0 * DT))
	p.pos += p.vel * DT
	if u >= 1.0:
		var exit_dir := fwd
		match p.skill:
			SimPlayer.Skill.DRAG:
				exit_dir = -fwd
			SimPlayer.Skill.ROLL:
				exit_dir = (fwd + perp).normalized()
			SimPlayer.Skill.STEPOVER:
				# sai para o lado contrário da finta
				exit_dir = (fwd - perp * 0.9).normalized()
				p.burst = 0.5
		p.facing = exit_dir
		p.vel = exit_dir * maxf(p.vel.length(), 3.2)
		p.skill = SimPlayer.Skill.NONE
		p.skill_cd = 0.3


## Bola "presa" ao movimento do drible.
func _skill_ball(o: SimPlayer) -> void:
	var dur: float = SimPlayer.SKILL_TIME[o.skill]
	var u := clampf(o.skill_t / dur, 0.0, 1.0)
	var fwd := o.skill_dir
	var perp := Vector2(-fwd.y, fwd.x) * o.skill_side
	var target := o.pos + o.facing * 0.42
	match o.skill:
		SimPlayer.Skill.DRAG:
			target = o.pos + fwd * lerpf(0.45, -0.5, u)
		SimPlayer.Skill.ROLL:
			target = o.pos + fwd * 0.35 + perp * lerpf(-0.15, 0.6, u)
		SimPlayer.Skill.STEPOVER:
			target = o.pos + fwd * 0.45
		SimPlayer.Skill.ROULETTE:
			target = o.pos + fwd * 0.3 * cos(u * PI) + perp * 0.35 * sin(u * PI)
	ball.vel = o.vel + (target - ball.pos) * 14.0
	ball.z = 0.0
	ball.vz = 0.0
	ball.spin = 0.0


func _header(p: SimPlayer) -> void:
	if _check_offside(p):
		return
	var c: Ctrl = ctrl[p.team]
	var dir := team_dir(p.team)
	var out: Vector2
	var near_goal := absf(ball.pos.x - half_len * dir) < half_len * 0.35
	if c.shoot_held or (c.pending == InputFrame.SHOOT) or (near_goal and c.player != p.id):
		out = (Vector2(half_len * dir, rng.range_f(-goal_half, goal_half) * 0.8) - ball.pos).normalized()
		ball.vz = -1.0
		c.pending = 0
	else:
		out = p.facing
		ball.vz = 1.5
	ball.vel = out * maxf(ball.speed() * 0.55, 9.0)
	ball.owner = -1
	ball.last_touch_team = p.team
	ball.last_touch_id = p.id
	p.kick_cd = 0.4
	_emit("header", {"team": p.team})


func _ball_physics() -> void:
	if ball.owner >= 0 and players[ball.owner].hold > 0.0:
		return
	var prev := ball.pos
	if ball.step(DT, mode) and ball.speed() > 4.0:
		_emit("bounce", {})
	_posts()
	_goal_and_bounds(prev)


func _posts() -> void:
	if ball.z > goal_h:
		return
	for side: float in [-1.0, 1.0]:
		for py: float in [-goal_half, goal_half]:
			var post := Vector2(half_len * side, py)
			var d := ball.pos - post
			var min_d := ball.radius + 0.06
			if d.length() < min_d and d.length() > 0.0001:
				var nrm := d.normalized()
				ball.pos = post + nrm * min_d
				var vn := ball.vel.dot(nrm)
				if vn < 0.0:
					ball.vel -= nrm * vn * 1.75
					_emit("post", {})


func _goal_and_bounds(prev: Vector2) -> void:
	var r := ball.radius
	var walls := bool(mode["walls"])
	var wb: float = mode["wall_bounce"]
	for side: float in [-1.0, 1.0]:
		var line := half_len * side
		# cruzou a linha neste passo — ou já está fora dela, longe das traves
		# (ex.: bola solta por trás da linha), o que também é saída
		if ball.pos.x * side >= half_len and (prev.x * side < half_len or absf(ball.pos.y) >= goal_half):
			var inside_posts := absf(ball.pos.y) < goal_half - r
			if inside_posts and ball.z < goal_h - r:
				_goal_scored(1 if team_dir(0) * side < 0.0 else 0)
				return
			if inside_posts and ball.z < goal_h + r * 2.0:
				# travessão
				ball.pos.x = line - side * r
				ball.vel.x *= -0.45
				ball.vz = -absf(ball.vz) * 0.5
				_emit("post", {"bar": true})
				continue
			if walls:
				ball.pos.x = line - side * r
				ball.vel.x *= -wb
				_emit("wall", {})
				continue
			_out_goal_line(side)
			return
	if absf(ball.pos.y) > half_w + (0.0 if walls else r):
		if walls:
			var s := signf(ball.pos.y)
			ball.pos.y = s * (half_w - r)
			ball.vel.y *= -wb
			_emit("wall", {})
		else:
			# lateral para quem não tocou por último
			var t := 1 - ball.last_touch_team if ball.last_touch_team >= 0 else 0
			_emit("out", {})
			_setup_restart(t, Vector2(ball.pos.x, signf(ball.pos.y) * (half_w - 0.05)), "throwin")


func _out_goal_line(side: float) -> void:
	# time que defende esse lado
	var defending := 0 if team_dir(0) * side < 0.0 else 1
	_emit("out", {})
	if ball.last_touch_team == defending:
		var cy := signf(ball.pos.y) * (half_w - 0.3) if absf(ball.pos.y) > 0.01 else half_w - 0.3
		_setup_restart(1 - defending, Vector2(side * (half_len - 0.3), cy), "corner")
	else:
		var gk_x := side * (half_len - minf(5.5, float(mode["box_depth"]) * 0.5 + 1.0))
		_setup_restart(defending, Vector2(gk_x, 0.0), "goalkick")


func _goal_scored(t: int) -> void:
	score[t] += 1
	last_goal_team = t
	last_scorer = ball.last_touch_id
	var own_goal := ball.last_touch_team != t
	ball.owner = -1
	state = State.GOAL
	state_timer = 3.0
	kickoff_team = 1 - t
	pass_receiver = -1
	for c in ctrl:
		c.charging = 0
		c.pending = 0
	_emit("goal", {"team": t, "scorer": last_scorer, "own_goal": own_goal})


func _keep_ball_in_net() -> void:
	var depth: float = mode["goal_depth"]
	if absf(ball.pos.x) > half_len + depth - ball.radius:
		ball.pos.x = signf(ball.pos.x) * (half_len + depth - ball.radius)
		ball.vel *= 0.2
	if absf(ball.pos.x) > half_len:
		ball.pos.y = clampf(ball.pos.y, -goal_half + ball.radius, goal_half - ball.radius)
		ball.z = minf(ball.z, goal_h - ball.radius)
