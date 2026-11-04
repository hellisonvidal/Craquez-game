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
	# abertura e telas do menu
	main._show_intro()
	await _wait(1.6)
	await _shot("00a_claude")
	main._current.show_page(1)
	await _wait(2.0)
	await _shot("00b_ia_gratuito")
	main._show_menu(MainMenu.Page.TITLE)
	await _wait(1.0)
	await _shot("00c_titulo")
	main._current.show_page(MainMenu.Page.MODES)
	await _wait(0.8)
	await _shot("00d_modos")
	main._current._pick_mode(GameModes.FIELD, false)
	await _wait(0.8)
	await _shot("00e_config")
	main._current.show_page(MainMenu.Page.TEAMS)
	await _wait(1.0)
	await _shot("01_menu")
	main._current._show_roster(0)
	await _wait(0.8)
	await _shot("01c_elenco")
	main._current._close_roster()
	main._current._pick_mode(GameModes.FUTSAL, false)
	main._current.show_page(MainMenu.Page.TEAMS)
	await _wait(1.0)
	await _shot("01b_times_futsal")

	await _match(GameModes.FIELD, _t("bra", 8), _t("bra", 13), ["cpu", "cpu"], 11, [[6.0, "02_campo_saida"], [18.0, "03_campo_jogo"], [30.0, "04_campo_ataque"]])
	# de perto: quem está com a bola (ou o mais próximo dela)
	for i in 3:
		var game: Node = main._current
		var near: SimPlayer = game.sim.nearest_to_ball(i % 2)
		game.view.closeup_id = near.id
		await _wait(0.9)
		await _shot("04_%d_perto" % i)
	main._current.view.closeup_id = -1
	await _match(GameModes.FUTSAL, _t("fbr", 1), _t("fbr", 0), ["cpu", "cpu"], 5, [[8.0, "05_futsal"], [20.0, "06_futsal_jogo"]])
	await _match(GameModes.STREET, _t("arg", 6), _t("arg", 21), ["cpu", "cpu"], 7, [[8.0, "07_street"], [20.0, "08_street_jogo"]])

	# celular (Android): proporção 20:9 com controles de toque
	get_window().size = Vector2i(2400, 1080)
	await _wait(0.5)
	await _match(GameModes.FIELD, _t("ita", 7), _t("ita", 11), ["touch", "cpu"], 21, [[10.0, "09_android_toque"]])
	var tc: Control = main._current.touch
	if tc != null:
		print("touch: visible=", tc.is_visible_in_tree(), " size=", tc.size, " pos=", tc.global_position)
	else:
		print("touch: null")

	# preset Baixo (aparelhos fracos)
	get_window().size = Vector2i(1920, 1080)
	Settings.quality = Settings.Quality.LOW
	Settings.apply()
	await _match(GameModes.FIELD, _t("esp", 15), _t("esp", 3), ["cpu", "cpu"], 11, [[18.0, "10_qualidade_baixa"]])

	# clima, horário e porte de estádio
	Settings.quality = Settings.Quality.ULTRA
	Settings.apply()
	await _match(GameModes.FIELD, _t("ned", 1), _t("ned", 2), ["cpu", "cpu"], 31, [[14.0, "12_chuva_dia"]], {"weather": "chuva", "tod": "dia"})
	await _match(GameModes.FIELD, _t("rus", 0), _t("rus", 3), ["cpu", "cpu"], 32, [[14.0, "13_neve_entardecer"]], {"weather": "neve", "tod": "tarde"})
	await _match(GameModes.FIELD, _t("uru", 15), _t("uru", 14), ["cpu", "cpu"], 33, [[12.0, "14_tempestade_estadio_modesto"]], {"weather": "tempestade", "tod": "noite"})
	await _match(GameModes.FIELD, _t("sau", 0), _t("sau", 1), ["cpu", "cpu"], 34, [[12.0, "15_saudita_dia"]], {"weather": "sol", "tod": "dia"})
	# cerimônia de entrada
	await _match(GameModes.FIELD, _t("bra", 8), _t("bra", 13), ["kb1", "cpu"], 61, [[2.0, "18a_abertura_estadio"], [6.5, "18b_entrada_times"], [9.6, "18c_escalacao_casa"], [12.4, "18d_escalacao_visitante"]], {"skip_intro": false})
	# variações de quadra de futsal e de rua
	for v in [1, 4, 5]:
		await _match(GameModes.FUTSAL, _t("fes", 0), _t("fes", 1), ["cpu", "cpu"], 40 + v, [[7.0, "16_futsal_local_%d" % v]], {"venue": v})
	for v in [1, 2, 4, 6]:
		await _match(GameModes.STREET, _t("bra", 2), _t("bra", 5), ["cpu", "cpu"], 50 + v, [[7.0, "17_rua_local_%d" % v]], {"venue": v, "tod": "dia"})
	# caça um gol para mostrar o banner com a face do artilheiro
	Settings.quality = Settings.Quality.ULTRA
	Settings.apply()
	await _goal_hunt()
	get_tree().quit()


## Índice global do k-ésimo time da liga `id` (ou k, se o banco de ligas não carregou).
func _t(id: String, k: int) -> int:
	for lg: Dictionary in Teams.leagues():
		if String(lg["id"]) == id:
			return int(lg["first"]) + k
	return k


func _match(mode: String, a: int, b: int, devices: Array, seed_value: int, shots: Array, extra: Dictionary = {}) -> void:
	var cfg := {"mode": mode, "team_a": a, "team_b": b, "devices": devices,
		"seed": seed_value, "half_minutes": 3.0, "weather": "sol", "tod": "noite", "skip_intro": true}
	cfg.merge(extra, true)
	main._start_match(cfg)
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
