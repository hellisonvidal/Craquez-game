## Estado de um atleta na simulação (sem nós da engine: puro dado + lógica).
class_name SimPlayer
extends RefCounted

const RADIUS := 0.38

# dribles (analógico direito)
enum Skill { NONE, KNOCK, DRAG, ROLL, STEPOVER, ROULETTE }
const SKILL_TIME := [0.0, 0.25, 0.5, 0.42, 0.55, 0.62]

var id: int = 0
var team: int = 0
var index: int = 0               # posição na escalação
var is_keeper: bool = false
var number: int = 1
var name: String = ""
var face_id: int = 0
var face: Array = []              # [pele, cor do cabelo, estilo, barba, olhos, porte] (data/leagues.json)
var base: Vector2 = Vector2.ZERO # posição na formação (normalizada)

# atributos 0..1
var pace: float = 0.7
var accel: float = 0.7
var shot: float = 0.7
var passing: float = 0.7
var control: float = 0.7
var defense: float = 0.7

# estado físico
var pos: Vector2 = Vector2.ZERO
var vel: Vector2 = Vector2.ZERO
var facing: Vector2 = Vector2.RIGHT
var stamina: float = 1.0
var intent: Vector2 = Vector2.ZERO   # direção desejada (0..1)
var want_sprint: bool = false
var sprinting: bool = false

# timers de ação
var kick_cd: float = 0.0
var stun: float = 0.0
var slide: float = 0.0
var slide_hit_ball: bool = false
var tackle_anim: float = 0.0
var kick_anim: float = 0.0
var hold: float = 0.0       # goleiro segurando a bola
var dive: float = 0.0       # goleiro mergulhando
var run_phase: float = 0.0  # só visual (animação de corrida)

# estilo EA FC: preparação do chute, dribles, proteção, desequilíbrio
var windup: float = 0.0           # perna armando: a bola sai quando zera
var windup_total: float = 0.0
var windup_kind: int = 0
var windup_power: float = 0.0
var windup_aim: Vector2 = Vector2.ZERO
var windup_finesse: bool = false
var kick_kind: int = 0            # último toque: InputFrame.PASS/THROUGH/LOB/SHOOT (para a animação)
var skill: int = Skill.NONE
var skill_t: float = 0.0
var skill_dir: Vector2 = Vector2.RIGHT
var skill_side: float = 1.0
var skill_cd: float = 0.0
var burst: float = 0.0            # arrancada depois de "tocar e correr"
var off_balance: float = 0.0      # defensor enganado pela finta
var face_ball: bool = false      # IA sem a bola: desloca olhando para o lance (recua/anda de lado)
var shielding: bool = false     # LT/L2: protege a bola (com) ou contém (sem)
var touch_t: float = 0.0          # domínio (só visual)


func top_speed() -> float:
	return lerpf(6.4, 8.8, pace)


func jog_speed() -> float:
	return top_speed() * 0.64
