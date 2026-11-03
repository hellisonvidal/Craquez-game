## Um "quadro" de entrada de um jogador em um tick da simulação.
## Contém só ESTADO (botões segurados + analógicos), nunca eventos: as bordas
## (apertou/soltou, "flick" do analógico direito) são calculadas dentro da
## simulação. Serializa em 6 bytes (leve para a rede).
class_name InputFrame
extends RefCounted

const PASS := 1        # A / Cruz      - passe rasteiro | defesa: bote
const SHOOT := 2       # B / Círculo   - chute (segurar = força) | defesa: carrinho
const LOB := 4         # X / Quadrado  - passe alto / cruzamento
const THROUGH := 8     # Y / Triângulo - enfiada | defesa: chamar 2º marcador
const SPRINT := 16     # RT / R2       - correr
const SWITCH := 32     # LB / L1       - trocar jogador
const PAUSE := 64      # Start / Options
const SHIELD := 128    # LT / L2       - condução curta / proteger | defesa: contenção (jockey)
const FINESSE := 256   # RB / R1       - chute colocado (com curva) / passe de efeito

const SIZE := 6

var move: Vector2 = Vector2.ZERO
var rstick: Vector2 = Vector2.ZERO   # analógico direito: dribles
var buttons: int = 0


func has(b: int) -> bool:
	return (buttons & b) != 0


func copy() -> InputFrame:
	var f := InputFrame.new()
	f.move = move
	f.rstick = rstick
	f.buttons = buttons
	return f


## 6 bytes: eixo X/Y (int8), botões (uint16), analógico direito X/Y (int8).
func to_bytes() -> PackedByteArray:
	var b := PackedByteArray()
	b.resize(SIZE)
	b.encode_s8(0, clampi(roundi(move.x * 127.0), -127, 127))
	b.encode_s8(1, clampi(roundi(move.y * 127.0), -127, 127))
	b.encode_u16(2, buttons & 0xFFFF)
	b.encode_s8(4, clampi(roundi(rstick.x * 127.0), -127, 127))
	b.encode_s8(5, clampi(roundi(rstick.y * 127.0), -127, 127))
	return b


static func from_bytes(b: PackedByteArray, offset: int = 0) -> InputFrame:
	var f := InputFrame.new()
	f.move = Vector2(b.decode_s8(offset) / 127.0, b.decode_s8(offset + 1) / 127.0)
	f.buttons = b.decode_u16(offset + 2)
	f.rstick = Vector2(b.decode_s8(offset + 4) / 127.0, b.decode_s8(offset + 5) / 127.0)
	return f


## Aplica quantização local também, para que o jogador local e o remoto
## simulem exatamente o mesmo valor.
func quantized() -> InputFrame:
	return InputFrame.from_bytes(to_bytes())
