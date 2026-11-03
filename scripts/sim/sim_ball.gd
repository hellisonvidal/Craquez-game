## Bola em 2.5D: posição no gramado (x, y) + altura z.
## Física: gravidade, arrasto quadrático no ar, efeito Magnus (curva),
## quique com restituição da superfície, atrito de rolagem e de deslizamento.
class_name SimBall
extends RefCounted

const G := 9.81
const MAGNUS := 0.035      # força lateral por (rad/s * m/s)
const SPIN_DECAY_AIR := 0.35
const SPIN_DECAY_GROUND := 3.0

var pos: Vector2 = Vector2.ZERO
var z: float = 0.0
var vel: Vector2 = Vector2.ZERO
var vz: float = 0.0
var spin: float = 0.0          # efeito lateral (positivo curva para a esquerda do movimento)
var radius: float = 0.11
var owner: int = -1            # id do jogador com a posse (-1 = livre)
var last_touch_team: int = -1
var last_touch_id: int = -1
var roll_angle: float = 0.0    # só visual


func speed() -> float:
	return vel.length()


func is_airborne() -> bool:
	return z > 0.02 or vz > 0.05


## Avança a bola. Retorna true se quicou no chão neste passo.
func step(dt: float, mode: Dictionary) -> bool:
	var bounced := false
	var air_drag: float = mode["air_drag"]
	var s := vel.length()
	var airborne := is_airborne()

	if airborne:
		# arrasto quadrático (≈ ½ρCdA/m) aplicado no vetor 3D
		var v3 := Vector3(vel.x, vel.y, vz)
		var s3 := v3.length()
		v3 -= v3 * (air_drag * s3 * dt)
		vel = Vector2(v3.x, v3.y)
		vz = v3.z
		# Magnus: aceleração perpendicular ao movimento horizontal
		if absf(spin) > 0.01 and s > 0.5:
			var perp := Vector2(-vel.y, vel.x)
			vel += perp * (spin * MAGNUS * dt)
		spin *= exp(-SPIN_DECAY_AIR * dt)
		vz -= G * dt
		z += vz * dt
		if z <= 0.0:
			z = 0.0
			var restitution: float = mode["bounce"]
			if vz < -1.2:
				vz = -vz * restitution
				# perde velocidade horizontal ao tocar o chão
				vel *= 0.86
				spin *= 0.5
				bounced = true
			else:
				vz = 0.0
	else:
		z = 0.0
		vz = 0.0
		if s > 0.0:
			var roll: float = mode["roll_decel"]
			var gdrag: float = mode["ground_drag"]
			var dec := roll + gdrag * s * s
			var ns := maxf(0.0, s - dec * dt)
			vel = vel * (ns / s)
			if absf(spin) > 0.01 and ns > 0.5:
				vel += Vector2(-vel.y, vel.x) * (spin * MAGNUS * 0.25 * dt)
		spin *= exp(-SPIN_DECAY_GROUND * dt)

	pos += vel * dt
	roll_angle += vel.length() * dt / radius
	return bounced
