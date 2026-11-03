## Configurações globais + presets de qualidade gráfica.
## Autoload "Settings". Salvo em user://settings.cfg.
extends Node

enum Quality { AUTO, LOW, MEDIUM, HIGH, ULTRA }

const QUALITY_NAMES := ["Automática", "Baixa", "Média", "Alta", "Ultra"]
const PATH := "user://settings.cfg"

var quality: int = Quality.AUTO
var effective_quality: int = Quality.HIGH
var half_minutes: float = 3.0
var vibration: bool = true
var show_touch: bool = false
var cpu_skill: float = 0.6
var sfx_volume: float = 0.8

## Flags de detalhe derivadas do preset (consultadas pela renderização).
var detail := {
	"shadows": true, "crowd": true, "stripes": true, "net": true, "player_detail": 2,
	"face_px": 512, "trail": true,
}


func _ready() -> void:
	load_settings()
	show_touch = show_touch or OS.has_feature("mobile")
	apply()


func detect_quality() -> int:
	var cores := OS.get_processor_count()
	if OS.has_feature("mobile") or OS.has_feature("web"):
		if cores <= 4:
			return Quality.LOW
		if cores <= 6:
			return Quality.MEDIUM
		return Quality.HIGH
	if cores <= 2:
		return Quality.LOW
	if cores <= 4:
		return Quality.MEDIUM
	return Quality.ULTRA if cores >= 8 else Quality.HIGH


func apply() -> void:
	effective_quality = detect_quality() if quality == Quality.AUTO else quality
	var win := get_window()
	match effective_quality:
		Quality.LOW:
			# renderiza em 854x480 e escala: muito leve em GPUs/celulares fracos
			win.content_scale_mode = Window.CONTENT_SCALE_MODE_VIEWPORT
			win.content_scale_size = Vector2i(854, 480)
			get_viewport().msaa_2d = Viewport.MSAA_DISABLED
			Engine.max_fps = 30
			detail = {"shadows": false, "crowd": false, "stripes": false, "net": false,
				"player_detail": 0, "face_px": 128, "trail": false}
		Quality.MEDIUM:
			win.content_scale_mode = Window.CONTENT_SCALE_MODE_VIEWPORT
			win.content_scale_size = Vector2i(1280, 720)
			get_viewport().msaa_2d = Viewport.MSAA_DISABLED
			Engine.max_fps = 60
			detail = {"shadows": true, "crowd": false, "stripes": true, "net": true,
				"player_detail": 1, "face_px": 256, "trail": false}
		Quality.HIGH:
			# resolução nativa (nítido em 1080p/1440p)
			win.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
			win.content_scale_size = Vector2i(1280, 720)
			get_viewport().msaa_2d = Viewport.MSAA_2X
			Engine.max_fps = 60
			detail = {"shadows": true, "crowd": true, "stripes": true, "net": true,
				"player_detail": 2, "face_px": 512, "trail": true}
		_:
			# nativo até 4K, MSAA 4x, até 144 fps
			win.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
			win.content_scale_size = Vector2i(1280, 720)
			get_viewport().msaa_2d = Viewport.MSAA_4X
			Engine.max_fps = 144
			detail = {"shadows": true, "crowd": true, "stripes": true, "net": true,
				"player_detail": 2, "face_px": 1024, "trail": true}


func load_settings() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(PATH) != OK:
		return
	quality = cfg.get_value("video", "quality", quality)
	half_minutes = cfg.get_value("match", "half_minutes", half_minutes)
	vibration = cfg.get_value("input", "vibration", vibration)
	show_touch = cfg.get_value("input", "show_touch", show_touch)
	cpu_skill = cfg.get_value("match", "cpu_skill", cpu_skill)
	sfx_volume = cfg.get_value("audio", "sfx_volume", sfx_volume)


func save_settings() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("video", "quality", quality)
	cfg.set_value("match", "half_minutes", half_minutes)
	cfg.set_value("input", "vibration", vibration)
	cfg.set_value("input", "show_touch", show_touch)
	cfg.set_value("match", "cpu_skill", cpu_skill)
	cfg.set_value("audio", "sfx_volume", sfx_volume)
	cfg.save(PATH)
