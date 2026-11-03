## Grava as entradas de cada tick. Como a simulação é determinística,
## (semente + entradas) reproduzem a partida inteira: base para replays,
## testes de dessincronia e rollback do online.
class_name ReplayBuffer
extends RefCounted

var seed_value: int = 0
var mode_id: String = ""
var frames: PackedByteArray = PackedByteArray()   # 2 quadros por tick (InputFrame.SIZE bytes cada)


func record(f0: InputFrame, f1: InputFrame) -> void:
	frames.append_array(f0.to_bytes())
	frames.append_array(f1.to_bytes())


func tick_count() -> int:
	return frames.size() / (InputFrame.SIZE * 2)


func frames_at(tick: int) -> Array:
	var o := tick * InputFrame.SIZE * 2
	return [InputFrame.from_bytes(frames, o), InputFrame.from_bytes(frames, o + InputFrame.SIZE)]
