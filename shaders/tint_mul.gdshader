// Escurece/colore a cena inteira (tempo fechado, entardecer). Multiplica o quadro.
shader_type canvas_item;
render_mode blend_mul;

uniform vec4 tint : source_color = vec4(1.0);

void fragment() {
	COLOR = tint;
}
