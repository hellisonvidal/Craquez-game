## Um "quadro" de entrada de um jogador em um tick da simulação.
## Contém só ESTADO (botões segurados + direção), nunca eventos: as bordas
## (apertou/soltou) são calculadas dentro da simulação. Isso torna o quadro
## trivial de serializar e enviar pela rede (3 bytes).
class_name InputFrame
extends RefCounted

const PASS := 1       # A / Cruz      - passe rasteiro | defesa: dividida em pé
const SHOOT := 2      # B / Círculo   - chute (segurar = força) | defesa: carrinho
const LOB := 4        # X / Quadrado  - passe alto / cruzamento
const THROUGH := 8    # Y / Triângulo - enfiada | defesa: chamar 2º marcador
const SPRINT := 16    # RT / R2       - correr
const SWITCH := 32    # LB / L1       - trocar jogador
const PAUSE := 64     # Start / Options

var move: Vector2 = Vector2.ZERO
var buttons: int = 0


func has(b: int) -> bool:
	return (buttons & b) != 0


func copy() -> InputFrame:
	var f := InputFrame.new()
	f.move = move
	f.buttons = buttons
	return f


## Quantiza para 3 bytes: eixo X (int8), eixo Y (int8), botões (uint8).
func to_bytes() -> PackedByteArray:
	var b := PackedByteArray()
	b.resize(3)
	b.encode_s8(0, clampi(roundi(move.x * 127.0), -127, 127))
	b.encode_s8(1, clampi(roundi(move.y * 127.0), -127, 127))
	b.encode_u8(2, buttons & 0xFF)
	return b


static func from_bytes(b: PackedByteArray, offset: int = 0) -> InputFrame:
	var f := InputFrame.new()
	f.move = Vector2(b.decode_s8(offset) / 127.0, b.decode_s8(offset + 1) / 127.0)
	f.buttons = b.decode_u8(offset + 2)
	return f


## Aplica quantização local também, para que o jogador local e o remoto
## simulem exatamente o mesmo valor.
func quantized() -> InputFrame:
	return InputFrame.from_bytes(to_bytes())
