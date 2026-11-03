## Sessão online 1x1 sobre ENet (UDP). Host = time 0 (casa), convidado = time 1.
##
## Protocolo (pacotes binários pequenos):
##   HELLO  (convidado→host)  versão
##   CONFIG (host→convidado)  JSON da partida (modo, times, semente, tempo)
##   READY  (convidado→host)
##   INPUTS (ambos)           tick inicial + até 8 InputFrames (redundância contra perda)
##   HASH   (ambos)           tick + hash do estado (detecção de dessincronia)
class_name EnetSession
extends NetSession

signal config_received(cfg: Dictionary)
signal ready_received

const PORT := 27015
const PROTOCOL := "craquez-1"
const T_HELLO := 1
const T_CONFIG := 2
const T_READY := 3
const T_INPUTS := 4
const T_HASH := 5
const REDUNDANCY := 8

var peer: ENetMultiplayerPeer
var is_host := false
var connected := false
var status := ""

var _local: Dictionary = {}     # tick -> PackedByteArray(3)
var _remote: Dictionary = {}    # tick -> InputFrame
var _hashes: Dictionary = {}    # tick -> int
var _last_sent_tick := -1


func is_online() -> bool:
	return true


func local_team() -> int:
	return 0 if is_host else 1


func host(port: int = PORT) -> Error:
	peer = ENetMultiplayerPeer.new()
	var err := peer.create_server(port, 1)
	if err != OK:
		status = "Não foi possível abrir a porta %d" % port
		return err
	is_host = true
	peer.peer_connected.connect(_on_peer_connected)
	peer.peer_disconnected.connect(_on_peer_disconnected)
	status = "Aguardando adversário…"
	return OK


func join(address: String, port: int = PORT) -> Error:
	peer = ENetMultiplayerPeer.new()
	var err := peer.create_client(address, port)
	if err != OK:
		status = "Endereço inválido"
		return err
	is_host = false
	peer.peer_connected.connect(_on_peer_connected)
	peer.peer_disconnected.connect(_on_peer_disconnected)
	status = "Conectando a %s…" % address
	return OK


func _on_peer_connected(_id: int) -> void:
	connected = true
	status = "Conectado!"
	if not is_host:
		var b := PackedByteArray([T_HELLO])
		b.append_array(PROTOCOL.to_utf8_buffer())
		_send(b, true)
	peer_connected.emit()


func _on_peer_disconnected(_id: int) -> void:
	connected = false
	status = "Adversário desconectou"
	peer_disconnected.emit()


func send_config(cfg: Dictionary) -> void:
	var b := PackedByteArray([T_CONFIG])
	b.append_array(JSON.stringify(cfg).to_utf8_buffer())
	_send(b, true)


func send_ready() -> void:
	_send(PackedByteArray([T_READY]), true)


func _send(b: PackedByteArray, reliable: bool) -> void:
	if peer == null or not connected:
		return
	peer.transfer_mode = MultiplayerPeer.TRANSFER_MODE_RELIABLE if reliable else MultiplayerPeer.TRANSFER_MODE_UNRELIABLE
	peer.set_target_peer(MultiplayerPeer.TARGET_PEER_BROADCAST if is_host else MultiplayerPeer.TARGET_PEER_SERVER)
	peer.put_packet(b)


func send_input(tick: int, frame: InputFrame) -> void:
	_local[tick] = frame.to_bytes()
	_last_sent_tick = maxi(_last_sent_tick, tick)
	_local.erase(tick - 240)
	_send_window(_last_sent_tick)


func resend_recent() -> void:
	if _last_sent_tick >= 0:
		_send_window(_last_sent_tick)


func _send_window(last: int) -> void:
	var first := maxi(0, last - REDUNDANCY + 1)
	while first <= last and not _local.has(first):
		first += 1
	if first > last:
		return
	var b := PackedByteArray([T_INPUTS, 0, 0, 0, 0, 0])
	b.encode_u32(1, first)
	var n := 0
	for t in range(first, last + 1):
		if not _local.has(t):
			break
		b.append_array(_local[t])
		n += 1
	b.encode_u8(5, n)
	_send(b, false)


func remote_input(tick: int) -> InputFrame:
	var f: InputFrame = _remote.get(tick)
	if f != null:
		_remote.erase(tick - 240)
	return f


func send_checksum(tick: int, h: int) -> void:
	var b := PackedByteArray([T_HASH])
	b.resize(13)
	b.encode_u32(1, tick)
	b.encode_s64(5, h)
	_send(b, true)


func remote_checksum(tick: int) -> Variant:
	return _hashes.get(tick)


func poll() -> void:
	if peer == null:
		return
	peer.poll()
	while peer.get_available_packet_count() > 0:
		var pkt := peer.get_packet()
		if pkt.is_empty():
			continue
		match pkt[0]:
			T_HELLO:
				var ver := pkt.slice(1).get_string_from_utf8()
				if ver != PROTOCOL:
					status = "Versão diferente do jogo (%s)" % ver
			T_CONFIG:
				var parsed: Variant = JSON.parse_string(pkt.slice(1).get_string_from_utf8())
				if parsed is Dictionary:
					config_received.emit(parsed)
			T_READY:
				ready_received.emit()
			T_INPUTS:
				if pkt.size() < 6:
					continue
				var first := pkt.decode_u32(1)
				var n := pkt.decode_u8(5)
				for i in n:
					var o := 6 + i * 3
					if o + 3 > pkt.size():
						break
					var t := first + i
					if not _remote.has(t):
						_remote[t] = InputFrame.from_bytes(pkt, o)
			T_HASH:
				if pkt.size() >= 13:
					_hashes[pkt.decode_u32(1)] = pkt.decode_s64(5)


func close() -> void:
	if peer != null:
		peer.close()
	connected = false


## IPs locais (IPv4 de rede privada) para mostrar ao anfitrião.
static func local_ips() -> Array[String]:
	var out: Array[String] = []
	for a in IP.get_local_addresses():
		if a.count(".") == 3 and (a.begins_with("192.168.") or a.begins_with("10.") or a.begins_with("172.")):
			out.append(a)
	return out
