// Contorno de "desenho" (casco invertido): espessura constante na tela.
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_opaque;

uniform vec4 color : source_color = vec4(0.04, 0.05, 0.08, 1.0);
uniform float width = 0.0016;

void vertex() {
	vec4 vp = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	float d = clamp(-vp.z, 1.0, 200.0);
	VERTEX += NORMAL * width * d;
}

void fragment() {
	ALBEDO = color.rgb;
}
