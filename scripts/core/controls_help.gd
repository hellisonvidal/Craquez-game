## Texto de ajuda dos controles conforme a plataforma e o dispositivo em uso
## (toque no celular, Xbox, PlayStation ou teclado).
class_name ControlsHelp
extends RefCounted


static func pad_kind(dev: String) -> String:
	if not dev.begins_with("pad:"):
		return ""
	var n := Input.get_joy_name(dev.substr(4).to_int()).to_lower()
	if "ps4" in n or "ps5" in n or "dualsense" in n or "dualshock" in n or "playstation" in n or "sony" in n:
		return "ps"
	return "xbox"


static func text(dev: String) -> String:
	if dev == "touch":
		return "Toque: analógico virtual à esquerda · PASSE · CHUTE (segure p/ força) · ALTO · ENFIADA · CORRER · TROCAR · DRIBLE (+ direção) · PROTEGER · COLOCADO"
	match pad_kind(dev):
		"ps":
			return "✕ passe · ◯ chute · ▢ alto · △ enfiada · R2 correr · L1 trocar · L2 proteger/conter · R1 colocado · analógico direito: dribles · Options: pausa"
		"xbox":
			return "A passe · B chute · X alto · Y enfiada · RT correr · LB trocar · LT proteger/conter · RB colocado · analógico direito: dribles · Start: pausa"
	if dev == "kb2":
		return "Setas mover · Num1 passe · Num2 chute · Num3 alto · Num5 enfiada · Num0 correr · Num4 trocar · Num7 proteger · Num9 colocado · Num6+direção drible · Backspace pausa"
	return "WASD mover · J passe · K chute · L alto · I enfiada · Shift correr · Q trocar · C proteger · U colocado · E+direção drible · P/Esc pausa"


## Dispositivo padrão do jogador 1 para a plataforma atual.
static func default_device(devices: Array) -> int:
	for i in devices.size():
		if String(devices[i]).begins_with("pad:"):
			return i
	if OS.has_feature("mobile") or DisplayServer.is_touchscreen_available() and not OS.has_feature("pc"):
		var t := devices.find("touch")
		if t >= 0:
			return t
	return maxi(0, devices.find("kb1"))
