## Tira screenshots automáticas do jogo (usado pelo CI):
##   godot --path . --resolution 1920x1080 -- --shots=<pasta>
extends Node

var out_dir := "user://shots"
var main: Node


func run(dir: String, main_node: Node) -> void:
	out_dir = dir
	main = main_node
	DirAccess.make_dir_recursive_absolute(out_dir)
	Settings.quality = Settings.Quality.ULTRA
	Settings.apply()
	main._show_menu()
	await _wait(1.5)
	await _shot("01_menu")

	await _match(GameModes.FIELD, 0, 1, ["cpu", "cpu"], 11, [[6.0, "02_campo_saida"], [18.0, "03_campo_jogo"], [30.0, "04_campo_ataque"]])
	# de perto: quem está com a bola (ou o mais próximo dela)
	for i in 3:
		var game: Node = main._current
		var near: SimPlayer = game.sim.nearest_to_ball(i % 2)
		game.view.closeup_id = near.id
		await _wait(0.9)
		await _shot("04_%d_perto" % i)
	main._current.view.closeup_id = -1
	await _match(GameModes.FUTSAL, 2, 3, ["cpu", "cpu"], 5, [[8.0, "05_futsal"], [20.0, "06_futsal_jogo"]])
	await _match(GameModes.STREET, 4, 5, ["cpu", "cpu"], 7, [[8.0, "07_street"], [20.0, "08_street_jogo"]])

	# celular (Android): proporção 20:9 com controles de toque
	get_window().size = Vector2i(2400, 1080)
	await _wait(0.5)
	await _match(GameModes.FIELD, 6, 7, ["touch", "cpu"], 21, [[10.0, "09_android_toque"]])
	var tc: Control = main._current.touch
	if tc != null:
		print("touch: visible=", tc.is_visible_in_tree(), " size=", tc.size, " pos=", tc.global_position)
	else:
		print("touch: null")

	# preset Baixo (aparelhos fracos)
	get_window().size = Vector2i(1920, 1080)
	Settings.quality = Settings.Quality.LOW
	Settings.apply()
	await _match(GameModes.FIELD, 0, 2, ["cpu", "cpu"], 11, [[18.0, "10_qualidade_baixa"]])

	# caça um gol para mostrar o banner com a face do artilheiro
	Settings.quality = Settings.Quality.ULTRA
	Settings.apply()
	await _goal_hunt()
	get_tree().quit()


func _match(mode: String, a: int, b: int, devices: Array, seed_value: int, shots: Array) -> void:
	main._start_match({"mode": mode, "team_a": a, "team_b": b, "devices": devices,
		"seed": seed_value, "half_minutes": 3.0})
	var t := 0.0
	for s in shots:
		await _wait(float(s[0]) - t)
		t = float(s[0])
		await _shot(String(s[1]))


func _goal_hunt() -> void:
	for seed_value in [3, 8, 14, 27]:
		main._start_match({"mode": GameModes.FUTSAL, "team_a": 1, "team_b": 4,
			"devices": ["cpu", "cpu"], "seed": seed_value, "half_minutes": 3.0})
		await _wait(0.3)
		var game: Node = main._current
		var waited := 0.0
		while waited < 90.0:
			await get_tree().create_timer(0.1).timeout
			waited += 0.1
			if game.sim != null and game.sim.state == MatchSim.State.GOAL:
				await _wait(0.6)
				await _shot("11_gol")
				return


func _wait(s: float) -> void:
	if s > 0.0:
		await get_tree().create_timer(s).timeout


func _shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	var path := out_dir.path_join(name + ".png")
	img.save_png(path)
	print("screenshot: ", path, " ", img.get_size())
