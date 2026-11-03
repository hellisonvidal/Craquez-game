## Escudo de clube e ícone de liga, desenhados por código (originais, sem
## marcas reais): formato, padrão e sigla variam por clube, nas cores do time.
class_name Crest
extends Control

var c1: Color = Color.WHITE
var c2: Color = Color.BLACK
var short_name: String = ""
var seed_value: int = 0
var is_league: bool = false
var league_name: String = ""


func setup_team(t: Dictionary) -> void:
	c1 = t.get("c1", Color.WHITE)
	c2 = t.get("c2", Color.BLACK)
	short_name = String(t.get("short", ""))
	seed_value = hash(String(t.get("name", "")))
	is_league = false
	queue_redraw()


func setup_league(name_: String, short_: String, seed_: int) -> void:
	league_name = name_
	short_name = short_
	seed_value = seed_
	is_league = true
	queue_redraw()


func _draw() -> void:
	var s := minf(size.x, size.y)
	var o := (size - Vector2(s, s)) * 0.5
	if is_league:
		_draw_league(o, s)
	else:
		_draw_crest(o, s)


static func _shield(o: Vector2, s: float, kind: int) -> PackedVector2Array:
	var pts := PackedVector2Array()
	match kind:
		0:  # escudo clássico
			pts = PackedVector2Array([Vector2(0.12, 0.08), Vector2(0.88, 0.08), Vector2(0.88, 0.5), Vector2(0.78, 0.74),
				Vector2(0.5, 0.94), Vector2(0.22, 0.74), Vector2(0.12, 0.5)])
		1:  # círculo
			for i in 32:
				var a := TAU * i / 32.0
				pts.append(Vector2(0.5 + cos(a) * 0.43, 0.5 + sin(a) * 0.43))
		2:  # losango arredondado
			pts = PackedVector2Array([Vector2(0.5, 0.05), Vector2(0.9, 0.45), Vector2(0.5, 0.95), Vector2(0.1, 0.45)])
		_:  # escudo de topo reto com pontas
			pts = PackedVector2Array([Vector2(0.1, 0.06), Vector2(0.3, 0.12), Vector2(0.5, 0.06), Vector2(0.7, 0.12),
				Vector2(0.9, 0.06), Vector2(0.86, 0.62), Vector2(0.5, 0.95), Vector2(0.14, 0.62)])
	for i in pts.size():
		pts[i] = o + pts[i] * s
	return pts


func _draw_crest(o: Vector2, s: float) -> void:
	var r := RandomNumberGenerator.new()
	r.seed = seed_value
	var kind := r.randi() % 4
	var pattern := r.randi() % 5
	var shape := _shield(o, s, kind)
	# sombra + borda
	var shadow := PackedVector2Array()
	for p in shape:
		shadow.append(p + Vector2(0, s * 0.03))
	draw_colored_polygon(shadow, Color(0, 0, 0, 0.35))
	draw_colored_polygon(shape, c1)
	var accent := c2 if c2.get_luminance() != c1.get_luminance() else Color.WHITE
	# padrões recortados pela forma (aproximação: faixas desenhadas dentro do contorno)
	var box := Rect2(o + Vector2(0.1, 0.06) * s, Vector2(0.8, 0.88) * s)
	match pattern:
		0:  # listras verticais
			for i in 5:
				if i % 2 == 1:
					_clip_band(shape, Rect2(box.position + Vector2(box.size.x * i / 5.0, 0), Vector2(box.size.x / 5.0, box.size.y)), accent)
		1:  # faixa diagonal
			var band := PackedVector2Array([o + Vector2(0.1, 0.3) * s, o + Vector2(0.3, 0.08) * s, o + Vector2(0.9, 0.7) * s, o + Vector2(0.7, 0.92) * s])
			_clip_poly(shape, band, accent)
		2:  # metades
			_clip_band(shape, Rect2(box.position + Vector2(box.size.x * 0.5, 0), Vector2(box.size.x * 0.5, box.size.y)), accent)
		3:  # banda horizontal
			_clip_band(shape, Rect2(box.position + Vector2(0, box.size.y * 0.38), Vector2(box.size.x, box.size.y * 0.24)), accent)
		_:
			pass
	var outline := shape.duplicate()
	outline.append(shape[0])
	draw_polyline(outline, Color(0.05, 0.05, 0.08), maxf(2.0, s * 0.035), true)
	var inner := PackedVector2Array()
	var c := o + Vector2(0.5, 0.5) * s
	for p in shape:
		inner.append(c + (p - c) * 0.86)
	inner.append(inner[0])
	draw_polyline(inner, Color(1, 1, 1, 0.55), maxf(1.0, s * 0.015), true)
	# estrela no topo (alguns clubes)
	if r.randf() < 0.35:
		_star(o + Vector2(0.5, 0.0) * s + Vector2(0, s * 0.02), s * 0.07, Color("#facc15"))
	# sigla
	var f := ThemeDB.fallback_font
	var fs := int(s * 0.24)
	var txt_col := Color.WHITE if c1.get_luminance() < 0.6 else Color("#0f172a")
	var pos := o + Vector2(0, s * 0.58)
	draw_string_outline(f, pos, short_name, HORIZONTAL_ALIGNMENT_CENTER, s, fs, maxi(2, int(s * 0.03)), Color(0, 0, 0, 0.6))
	draw_string(f, pos, short_name, HORIZONTAL_ALIGNMENT_CENTER, s, fs, txt_col)


## Faixa retangular limitada ao contorno do escudo.
func _clip_band(shape: PackedVector2Array, rect: Rect2, col: Color) -> void:
	var poly := PackedVector2Array([rect.position, rect.position + Vector2(rect.size.x, 0), rect.end, rect.position + Vector2(0, rect.size.y)])
	_clip_poly(shape, poly, col)


func _clip_poly(shape: PackedVector2Array, poly: PackedVector2Array, col: Color) -> void:
	var parts := Geometry2D.intersect_polygons(shape, poly)
	for p in parts:
		if p.size() >= 3:
			draw_colored_polygon(p, col)


func _star(c: Vector2, r: float, col: Color) -> void:
	var pts := PackedVector2Array()
	for i in 10:
		var a := -PI / 2 + TAU * i / 10.0
		pts.append(c + Vector2(cos(a), sin(a)) * (r if i % 2 == 0 else r * 0.45))
	draw_colored_polygon(pts, col)


func _draw_league(o: Vector2, s: float) -> void:
	var r := RandomNumberGenerator.new()
	r.seed = seed_value
	var hue := r.randf()
	var base := Color.from_hsv(hue, 0.65, 0.55)
	var light := Color.from_hsv(hue, 0.45, 0.9)
	var rect := Rect2(o + Vector2(0.06, 0.06) * s, Vector2(0.88, 0.88) * s)
	var sb := StyleBoxFlat.new()
	sb.bg_color = base
	sb.set_corner_radius_all(int(s * 0.18))
	sb.border_color = light
	sb.set_border_width_all(maxi(2, int(s * 0.04)))
	draw_style_box(sb, rect)
	# troféu estilizado (taça)
	var c := o + Vector2(0.5, 0.36) * s
	var cup := PackedVector2Array([c + Vector2(-0.16, -0.12) * s, c + Vector2(0.16, -0.12) * s, c + Vector2(0.11, 0.06) * s,
		c + Vector2(0.03, 0.1) * s, c + Vector2(0.03, 0.17) * s, c + Vector2(0.09, 0.2) * s, c + Vector2(-0.09, 0.2) * s,
		c + Vector2(-0.03, 0.17) * s, c + Vector2(-0.03, 0.1) * s, c + Vector2(-0.11, 0.06) * s])
	draw_colored_polygon(cup, Color("#facc15"))
	draw_arc(c + Vector2(-0.15, -0.04) * s, s * 0.05, PI * 0.5, PI * 1.5, 12, Color("#facc15"), maxf(1.5, s * 0.025))
	draw_arc(c + Vector2(0.15, -0.04) * s, s * 0.05, -PI * 0.5, PI * 0.5, 12, Color("#facc15"), maxf(1.5, s * 0.025))
	var f := ThemeDB.fallback_font
	draw_string(f, o + Vector2(0, 0.83) * s, short_name, HORIZONTAL_ALIGNMENT_CENTER, s, int(s * 0.2), Color.WHITE)
