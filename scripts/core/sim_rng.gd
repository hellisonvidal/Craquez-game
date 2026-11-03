## RNG determinístico (xorshift32). Toda aleatoriedade da simulação passa por aqui,
## para que duas máquinas com a mesma semente + mesmas entradas produzam a mesma partida
## (pré-requisito para replays e PvP online com rollback/lockstep).
class_name SimRng
extends RefCounted

const MASK := 0xFFFFFFFF

var state: int = 1


func _init(seed_value: int = 12345) -> void:
	state = seed_value & MASK
	if state == 0:
		state = 0x9E3779B9


func next_u32() -> int:
	var x: int = state
	x ^= (x << 13) & MASK
	x ^= x >> 17
	x ^= (x << 5) & MASK
	state = x & MASK
	return state


func randf01() -> float:
	return float(next_u32()) / 4294967296.0


func range_f(a: float, b: float) -> float:
	return a + (b - a) * randf01()


func chance(p: float) -> bool:
	return randf01() < p


## Aproximação de distribuição normal (média 0, desvio ~1).
func gauss() -> float:
	return (randf01() + randf01() + randf01() + randf01() - 2.0) * 1.73
