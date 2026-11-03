## Lê dispositivos físicos e gera InputFrames.
## Dispositivos: "kb1" (WASD), "kb2" (setas/numérico), "pad:N" (Xbox/PS/genérico via SDL),
## "touch" (controle virtual no Android). Cada jogador local fica com um dispositivo.
class_name InputRouter
extends RefCounted

const DEADZONE := 0.18

static var touch_frame: InputFrame = InputFrame.new()


static func available_devices() -> Array[String]:
	var list: Array[String] = []
	for id in Input.get_connected_joypads():
		list.append("pad:%d" % id)
	list.append("kb1")
	list.append("kb2")
	if DisplayServer.is_touchscreen_available():
		list.append("touch")
	return list


static func device_label(dev: String) -> String:
	if dev.begins_with("pad:"):
		var id := dev.substr(4).to_int()
		var n := Input.get_joy_name(id)
		var lower := n.to_lower()
		var kind := "Controle"
		if "xbox" in lower or "xinput" in lower:
			kind = "Xbox"
		elif "ps4" in lower or "ps5" in lower or "dualsense" in lower or "dualshock" in lower or "playstation" in lower or "sony" in lower:
			kind = "PlayStation"
		return "%s %d" % [kind, id + 1]
	match dev:
		"kb1":
			return "Teclado (WASD)"
		"kb2":
			return "Teclado (Setas)"
		"touch":
			return "Toque"
		"cpu":
			return "CPU"
	return dev


static func read(dev: String) -> InputFrame:
	if dev.begins_with("pad:"):
		return _read_pad(dev.substr(4).to_int())
	match dev:
		"kb1":
			return _read_keys(KEY_W, KEY_S, KEY_A, KEY_D, KEY_J, KEY_K, KEY_L, KEY_I, KEY_SHIFT, KEY_Q, KEY_ESCAPE)
		"kb2":
			return _read_keys(KEY_UP, KEY_DOWN, KEY_LEFT, KEY_RIGHT, KEY_KP_1, KEY_KP_2, KEY_KP_3, KEY_KP_5, KEY_KP_0, KEY_KP_4, KEY_BACKSPACE)
		"touch":
			return touch_frame.copy()
	return InputFrame.new()


static func _read_keys(up: Key, down: Key, left: Key, right: Key, pass_k: Key, shoot_k: Key, lob_k: Key,
		through_k: Key, sprint_k: Key, switch_k: Key, pause_k: Key) -> InputFrame:
	var f := InputFrame.new()
	var v := Vector2(
		float(Input.is_physical_key_pressed(right)) - float(Input.is_physical_key_pressed(left)),
		float(Input.is_physical_key_pressed(down)) - float(Input.is_physical_key_pressed(up)))
	f.move = v.normalized() if v.length() > 0.0 else Vector2.ZERO
	if Input.is_physical_key_pressed(pass_k): f.buttons |= InputFrame.PASS
	if Input.is_physical_key_pressed(shoot_k): f.buttons |= InputFrame.SHOOT
	if Input.is_physical_key_pressed(lob_k): f.buttons |= InputFrame.LOB
	if Input.is_physical_key_pressed(through_k): f.buttons |= InputFrame.THROUGH
	if Input.is_physical_key_pressed(sprint_k): f.buttons |= InputFrame.SPRINT
	if Input.is_physical_key_pressed(switch_k): f.buttons |= InputFrame.SWITCH
	if Input.is_physical_key_pressed(pause_k): f.buttons |= InputFrame.PAUSE
	return f


## Layout SDL: A/Cruz=0, B/Círculo=1, X/Quadrado=2, Y/Triângulo=3 — igual em Xbox e PS.
static func _read_pad(id: int) -> InputFrame:
	var f := InputFrame.new()
	var v := Vector2(Input.get_joy_axis(id, JOY_AXIS_LEFT_X), Input.get_joy_axis(id, JOY_AXIS_LEFT_Y))
	# deadzone radial com reescala (sem "degrau" ao sair da zona morta)
	var l := v.length()
	if l < DEADZONE:
		v = Vector2.ZERO
	else:
		v = v / l * minf(1.0, (l - DEADZONE) / (1.0 - DEADZONE))
	# direcional digital também move
	var dpad := Vector2(
		float(Input.is_joy_button_pressed(id, JOY_BUTTON_DPAD_RIGHT)) - float(Input.is_joy_button_pressed(id, JOY_BUTTON_DPAD_LEFT)),
		float(Input.is_joy_button_pressed(id, JOY_BUTTON_DPAD_DOWN)) - float(Input.is_joy_button_pressed(id, JOY_BUTTON_DPAD_UP)))
	if dpad != Vector2.ZERO:
		v = dpad.normalized()
	f.move = v
	if Input.is_joy_button_pressed(id, JOY_BUTTON_A): f.buttons |= InputFrame.PASS
	if Input.is_joy_button_pressed(id, JOY_BUTTON_B): f.buttons |= InputFrame.SHOOT
	if Input.is_joy_button_pressed(id, JOY_BUTTON_X): f.buttons |= InputFrame.LOB
	if Input.is_joy_button_pressed(id, JOY_BUTTON_Y): f.buttons |= InputFrame.THROUGH
	if Input.get_joy_axis(id, JOY_AXIS_TRIGGER_RIGHT) > 0.4 or Input.is_joy_button_pressed(id, JOY_BUTTON_RIGHT_SHOULDER):
		f.buttons |= InputFrame.SPRINT
	if Input.is_joy_button_pressed(id, JOY_BUTTON_LEFT_SHOULDER): f.buttons |= InputFrame.SWITCH
	if Input.is_joy_button_pressed(id, JOY_BUTTON_START): f.buttons |= InputFrame.PAUSE
	return f


static func rumble(dev: String, weak: float, strong: float, duration: float) -> void:
	if dev.begins_with("pad:") and Settings.vibration:
		Input.start_joy_vibration(dev.substr(4).to_int(), weak, strong, duration)
