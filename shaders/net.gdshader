// Rede do gol: malha procedural transparente.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

uniform vec2 size = vec2(7.32, 2.44);
uniform float cell = 0.14;
uniform float alpha = 0.75;
uniform vec4 tint : source_color = vec4(0.96, 0.96, 0.96, 1.0);
// rede estufando: ponto atingido (mundo), direção e intensidade
uniform vec3 hit_pos = vec3(0.0, -100.0, 0.0);
uniform vec3 push_dir = vec3(1.0, 0.0, 0.0);
uniform float hit_amp = 0.0;

void vertex() {
	vec3 wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float d = distance(wp, hit_pos);
	VERTEX += push_dir * hit_amp * exp(-d * d / 1.3);
}

void fragment() {
	vec2 p = UV * size / cell;
	vec2 g = abs(fract(p - 0.5) - 0.5) / fwidth(p);
	float line = 1.0 - clamp(min(g.x, g.y) - 0.6, 0.0, 1.0);
	ALBEDO = tint.rgb;
	ALPHA = line * alpha;
}
