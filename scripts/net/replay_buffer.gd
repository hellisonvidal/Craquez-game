## Grava as entradas de cada tick. Como a simulação é determinística,
## (semente + entradas) reproduzem a partida inteira: base para replays,
## testes de dessincronia e rollback do online.
class_name ReplayBuffer
extends RefCounted

var seed_value: int = 0
var mode_id: String = ""
var frames: PackedByteArray = PackedByteArray()   # 6 bytes por tick (3 por time)


func record(f0: InputFrame, f1: InputFrame) -> void:
	frames.append_array(f0.to_bytes())
	frames.append_array(f1.to_bytes())


func tick_count() -> int:
	return frames.size() / 6


func frames_at(tick: int) -> Array:
	var o := tick * 6
	return [InputFrame.from_bytes(frames, o), InputFrame.from_bytes(frames, o + 3)]
