## Definições dos modos de jogo. Tudo em metros / segundos.
## Formação: x em [-1, 0] (−1 = linha do próprio gol, 0 = meio-campo), y em [-1, 1].
class_name GameModes
extends RefCounted

const FIELD := "field"
const FUTSAL := "futsal"
const STREET := "street"
const ALL: Array[String] = [FIELD, FUTSAL, STREET]


## Formações disponíveis por modo (posições normalizadas: x de -1 = próprio gol a 0 = meio; y lateral).
## O primeiro item é o goleiro (campo/futsal). Street não tem goleiro.
static func formations(id: String) -> Dictionary:
	match id:
		FUTSAL:
			return {
				"2-1-1": [Vector2(-0.95, 0), Vector2(-0.62, -0.5), Vector2(-0.62, 0.5), Vector2(-0.35, 0.0), Vector2(-0.12, 0.0)],
				"1-2-1": [Vector2(-0.95, 0), Vector2(-0.65, 0.0), Vector2(-0.4, -0.55), Vector2(-0.4, 0.55), Vector2(-0.12, 0.0)],
				"2-2": [Vector2(-0.95, 0), Vector2(-0.62, -0.5), Vector2(-0.62, 0.5), Vector2(-0.2, -0.4), Vector2(-0.2, 0.4)],
				"3-1": [Vector2(-0.95, 0), Vector2(-0.6, -0.6), Vector2(-0.65, 0.0), Vector2(-0.6, 0.6), Vector2(-0.15, 0.0)],
			}
		STREET:
			return {
				"1-2": [Vector2(-0.7, 0.0), Vector2(-0.3, -0.5), Vector2(-0.3, 0.5)],
				"2-1": [Vector2(-0.6, -0.45), Vector2(-0.6, 0.45), Vector2(-0.25, 0.0)],
				"1-1-1": [Vector2(-0.7, 0.0), Vector2(-0.45, 0.0), Vector2(-0.2, 0.0)],
			}
	var gk := Vector2(-0.96, 0.0)
	var back4 := [Vector2(-0.72, -0.62), Vector2(-0.76, -0.22), Vector2(-0.76, 0.22), Vector2(-0.72, 0.62)]
	return {
		"4-4-2": [gk] + back4 + [Vector2(-0.42, -0.66), Vector2(-0.46, -0.2), Vector2(-0.46, 0.2), Vector2(-0.42, 0.66), Vector2(-0.12, -0.2), Vector2(-0.14, 0.22)],
		"4-3-3": [gk] + back4 + [Vector2(-0.48, -0.35), Vector2(-0.52, 0.0), Vector2(-0.48, 0.35), Vector2(-0.16, -0.6), Vector2(-0.1, 0.0), Vector2(-0.16, 0.6)],
		"4-2-3-1": [gk] + back4 + [Vector2(-0.55, -0.2), Vector2(-0.55, 0.2), Vector2(-0.32, -0.6), Vector2(-0.3, 0.0), Vector2(-0.32, 0.6), Vector2(-0.1, 0.0)],
		"3-5-2": [gk, Vector2(-0.74, -0.4), Vector2(-0.78, 0.0), Vector2(-0.74, 0.4), Vector2(-0.42, -0.78), Vector2(-0.5, -0.3), Vector2(-0.54, 0.0), Vector2(-0.5, 0.3), Vector2(-0.42, 0.78), Vector2(-0.12, -0.2), Vector2(-0.14, 0.22)],
		"5-3-2": [gk, Vector2(-0.66, -0.8), Vector2(-0.76, -0.38), Vector2(-0.8, 0.0), Vector2(-0.76, 0.38), Vector2(-0.66, 0.8), Vector2(-0.46, -0.4), Vector2(-0.5, 0.0), Vector2(-0.46, 0.4), Vector2(-0.14, -0.2), Vector2(-0.14, 0.22)],
	}


## Função do atleta pela posição na formação (GK/DF/MF/FW).
static func role_for(base: Vector2, slot: int, has_keeper: bool) -> String:
	if has_keeper and slot == 0:
		return "GK"
	if base.x < -0.6:
		return "DF"
	if base.x < -0.3:
		return "MF"
	return "FW"


static func display_name(id: String) -> String:
	match id:
		FUTSAL:
			return "Futsal"
		STREET:
			return "Street"
	return "Campo"


static func get_mode(id: String) -> Dictionary:
	match id:
		FUTSAL:
			return {
				"id": FUTSAL, "length": 40.0, "width": 20.0,
				"goal_width": 3.0, "goal_height": 2.0, "goal_depth": 1.0,
				"keeper": true, "walls": false, "wall_bounce": 0.0,
				"box_depth": 6.0, "box_half": 8.0, "penalty_dist": 6.0, "offside": false, "center_r": 3.0, "restart_dist": 5.0,
				"roll_decel": 0.75, "ground_drag": 0.004, "air_drag": 0.016, "bounce": 0.45,
				"surface": "wood", "view_width": 30.0, "run_margin": 1.0,
				"formation": [Vector2(-0.95, 0), Vector2(-0.62, -0.5), Vector2(-0.62, 0.5),
					Vector2(-0.35, 0.0), Vector2(-0.12, 0.0)],
			}
		STREET:
			return {
				"id": STREET, "length": 30.0, "width": 18.0,
				"goal_width": 2.6, "goal_height": 1.4, "goal_depth": 0.8,
				"keeper": false, "walls": true, "wall_bounce": 0.72,
				"box_depth": 0.0, "box_half": 0.0, "penalty_dist": 0.0, "offside": false, "center_r": 2.5, "restart_dist": 3.0,
				"roll_decel": 0.95, "ground_drag": 0.006, "air_drag": 0.014, "bounce": 0.68,
				"surface": "asphalt", "view_width": 26.0, "run_margin": 0.0,
				"formation": [Vector2(-0.7, 0.0), Vector2(-0.3, -0.5), Vector2(-0.3, 0.5)],
			}
	return {
		"id": FIELD, "length": 105.0, "width": 68.0,
		"goal_width": 7.32, "goal_height": 2.44, "goal_depth": 2.0,
		"keeper": true, "walls": false, "wall_bounce": 0.0,
		"box_depth": 16.5, "box_half": 20.15, "penalty_dist": 11.0, "offside": true, "center_r": 9.15, "restart_dist": 9.15,
		"roll_decel": 1.25, "ground_drag": 0.006, "air_drag": 0.013, "bounce": 0.58,
		"surface": "grass", "view_width": 46.0, "run_margin": 3.0,
		"formation": [Vector2(-0.96, 0.0),
			Vector2(-0.72, -0.62), Vector2(-0.76, -0.22), Vector2(-0.76, 0.22), Vector2(-0.72, 0.62),
			Vector2(-0.42, -0.66), Vector2(-0.46, -0.2), Vector2(-0.46, 0.2), Vector2(-0.42, 0.66),
			Vector2(-0.12, -0.2), Vector2(-0.14, 0.22)],
	}
