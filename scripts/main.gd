## Ponto de entrada: alterna entre menu e partida.
extends Node

var _current: Node


func _ready() -> void:
	# Teste de fumaça (CI):  godot --headless -- --autoplay=futsal
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--autoplay"):
			var mode := a.get_slice("=", 1) if "=" in a else GameModes.FIELD
			_show_menu()
			_start_match({"mode": mode, "team_a": 0, "team_b": 1, "devices": ["cpu", "cpu"],
				"seed": 3, "half_minutes": 0.25})
			return
	_show_menu()


func _swap(n: Node) -> void:
	if _current != null:
		_current.queue_free()
	_current = n
	add_child(n)


func _show_menu() -> void:
	var m := MainMenu.new()
	m.start_match.connect(_start_match)
	_swap(m)


func _start_match(cfg: Dictionary) -> void:
	var game := MatchGame.new()
	game.exit_to_menu.connect(func(): _show_menu.call_deferred())
	_swap(game)
	game.start(cfg)
