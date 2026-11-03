## Hash do estado da simulação, para detectar dessincronia no online.
class_name StateHash
extends RefCounted


static func of(sim: MatchSim) -> int:
	var b := PackedByteArray()
	b.resize(16 + sim.players.size() * 16)
	b.encode_float(0, sim.ball.pos.x)
	b.encode_float(4, sim.ball.pos.y)
	b.encode_float(8, sim.ball.z)
	b.encode_s32(12, sim.ball.owner)
	var o := 16
	for p in sim.players:
		b.encode_float(o, p.pos.x)
		b.encode_float(o + 4, p.pos.y)
		b.encode_float(o + 8, p.vel.x)
		b.encode_float(o + 12, p.vel.y)
		o += 16
	return hash(b)
