## Lockstep com atraso de entrada.
## A entrada local capturada agora vale para o tick (atual + delay). A simulação só
## avança um tick quando tem as entradas dos DOIS lados para ele — assim as duas
## máquinas simulam exatamente a mesma partida. 3 ticks de atraso ≈ 50 ms.
class_name Lockstep
extends RefCounted

signal desync(tick: int)

const HASH_EVERY := 60

var session: NetSession
var sim: MatchSim
var local_team: int
var delay: int
var stalled_frames := 0

var _local: Dictionary = {}     # tick -> InputFrame
var _next_input_tick := 0
var _my_hashes: Dictionary = {}


func _init(s: NetSession, m: MatchSim, team: int, input_delay: int = 3) -> void:
	session = s
	sim = m
	local_team = team
	delay = input_delay
	for i in delay:
		_schedule(InputFrame.new())


func _schedule(f: InputFrame) -> void:
	var q := f.quantized()
	_local[_next_input_tick] = q
	session.send_input(_next_input_tick, q)
	_next_input_tick += 1


## Chamar 1x por tick físico com a entrada atual do jogador local.
func push_local(f: InputFrame) -> void:
	if _next_input_tick - sim.tick < delay * 2:
		_schedule(f)
	else:
		# muito à frente do outro lado: espera e reenvia (pode ter perdido pacote)
		session.resend_recent()


## Avança o que der. `on_step` é chamado após cada tick simulado (para eventos/HUD).
func advance(on_step: Callable, max_steps: int = 3) -> int:
	var steps := 0
	while steps < max_steps:
		var t := sim.tick
		if not _local.has(t):
			break
		var remote := session.remote_input(t)
		if remote == null:
			break
		var frames: Array = [null, null]
		frames[local_team] = _local[t]
		frames[1 - local_team] = remote
		_local.erase(t)
		sim.step(frames)
		steps += 1
		if on_step.is_valid():
			on_step.call()
		if sim.tick % HASH_EVERY == 0:
			var h := StateHash.of(sim)
			_my_hashes[sim.tick] = h
			session.send_checksum(sim.tick, h)
	_check_hashes()
	stalled_frames = 0 if steps > 0 else stalled_frames + 1
	return steps


func _check_hashes() -> void:
	for t in _my_hashes.keys():
		var other: Variant = session.remote_checksum(t)
		if other != null:
			if int(other) != int(_my_hashes[t]):
				desync.emit(t)
			_my_hashes.erase(t)
