## Adversário controlado pelo computador.
## Não mexe na simulação diretamente: produz um InputFrame, exatamente como um
## controle físico ou um jogador remoto. Determinístico (usa só o estado da sim).
class_name CpuBrain
extends RefCounted

var team: int = 1
var skill: float = 0.6        # 0..1 (dificuldade)
var _hold_btn: int = 0
var _hold_ticks: int = 0
var _cooldown: int = 0


func _init(t: int = 1, difficulty: float = 0.6) -> void:
	team = t
	skill = difficulty


## Hash determinístico simples para "decisões aleatórias" sem mexer no RNG da sim.
func _noise(sim: MatchSim, salt: int) -> float:
	var h := (sim.tick * 73856093) ^ (salt * 19349663) ^ (team * 83492791)
	h = (h ^ (h >> 13)) * 1274126177
	return float(h & 0xFFFF) / 65535.0


func make_input(sim: MatchSim) -> InputFrame:
	var f := InputFrame.new()
	if sim.state != MatchSim.State.PLAYING:
		return f
	var p: SimPlayer = sim.controlled_player(team)
	var dir := sim.team_dir(team)
	_cooldown = maxi(0, _cooldown - 1)

	# terminando de segurar um botão (chute/passe carregado)
	if _hold_btn != 0:
		f.buttons |= _hold_btn
		_hold_ticks -= 1
		if _hold_ticks <= 0:
			f.buttons &= ~_hold_btn
			_hold_btn = 0
			_cooldown = 20

	if sim.ball.owner == p.id:
		var goal := Vector2(sim.half_len * dir, 0.0)
		var to_goal := goal - p.pos
		var mv := to_goal.normalized()
		var opp := sim.nearest_opponent(p)
		var opp_d := opp.pos.distance_to(p.pos) if opp != null else 99.0
		if opp != null and opp_d < 4.5:
			var away := (p.pos - opp.pos).normalized()
			mv = (mv + away * 0.9).normalized()
		# evita linhas laterais
		if absf(p.pos.y) > sim.half_w * 0.85:
			mv.y -= signf(p.pos.y) * 0.8
		f.move = mv.normalized()
		if opp_d > 3.5:
			f.buttons |= InputFrame.SPRINT
		if _hold_btn == 0 and _cooldown == 0:
			var shoot_range := sim.half_len * lerpf(0.32, 0.45, skill)
			if to_goal.length() < shoot_range and absf(p.pos.y) < sim.half_w * 0.6:
				# aponta para um canto
				f.move = Vector2(dir, (0.6 if _noise(sim, 1) > 0.5 else -0.6))
				_press(f, InputFrame.SHOOT, int(lerpf(18.0, 40.0, to_goal.length() / shoot_range)))
			elif opp_d < 2.2 and _noise(sim, 2) < 0.06 + skill * 0.08:
				f.move = Vector2(dir, (_noise(sim, 3) - 0.5) * 1.6).normalized()
				_press(f, InputFrame.PASS if _noise(sim, 4) > 0.25 else InputFrame.LOB, 8 + int(_noise(sim, 5) * 14.0))
	else:
		var target := sim.ball.pos + sim.ball.vel * 0.3
		if sim.ball.owner >= 0 and sim.players[sim.ball.owner].team != team:
			# fica entre a bola e o gol
			var own_goal := Vector2(-sim.half_len * dir, 0.0)
			target = sim.ball.pos + (own_goal - sim.ball.pos).normalized() * 0.7
		var to := target - p.pos
		var d := to.length()
		f.move = (to / d) * clampf(d / 1.5, 0.3, 1.0) if d > 0.05 else Vector2.ZERO
		if d > 3.0:
			f.buttons |= InputFrame.SPRINT
		var owner := sim.owner_player()
		if owner != null and owner.team != team and _cooldown == 0:
			var bd := p.pos.distance_to(sim.ball.pos)
			if bd < 1.3 and _noise(sim, 6) < 0.05 + skill * 0.06:
				_press(f, InputFrame.PASS, 1)
			elif bd < 2.6 and bd > 1.4 and _noise(sim, 7) < 0.004 + skill * 0.006:
				_press(f, InputFrame.SHOOT, 1)
		elif owner == null and _cooldown == 0:
			var me_d := p.pos.distance_to(sim.ball.pos)
			var n := sim.nearest_to_ball(team)
			if n != null and n.id != p.id and n.pos.distance_to(sim.ball.pos) + 4.0 < me_d:
				f.buttons |= InputFrame.SWITCH
				_cooldown = 15
	return f


func _press(f: InputFrame, btn: int, ticks: int) -> void:
	f.buttons |= btn
	_hold_btn = btn
	_hold_ticks = maxi(1, ticks)
