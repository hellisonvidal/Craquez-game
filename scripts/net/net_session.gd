## Contrato de sessão de rede. A partida só conversa com esta interface,
## então trocar "local" por "online" não exige mudar a simulação.
##
## Modelo planejado (docs/ONLINE.md): lockstep com atraso de entrada + rollback.
## Cada tick, cada lado envia seu InputFrame (3 bytes) com o número do tick.
class_name NetSession
extends RefCounted

signal peer_connected
signal peer_disconnected
signal desync_detected(tick: int)

## Atraso de entrada em ticks (esconde a latência; 2 ticks ≈ 33 ms).
var input_delay: int = 2


func is_online() -> bool:
	return false


## Time controlado por esta máquina (0 ou 1). Local: ambos.
func local_team() -> int:
	return -1


## Envia a entrada local para o tick `tick`.
func send_input(_tick: int, _frame: InputFrame) -> void:
	pass


## Retorna a entrada do adversário para `tick`, ou null se ainda não chegou
## (no rollback usamos uma previsão e corrigimos depois).
func remote_input(_tick: int) -> InputFrame:
	return null


## Verificação de sincronia: hash do estado a cada N ticks.
func send_checksum(_tick: int, _hash: int) -> void:
	pass


func poll() -> void:
	pass


func close() -> void:
	pass
