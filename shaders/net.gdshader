// Rede do gol: malha procedural transparente.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

uniform vec2 size = vec2(7.32, 2.44);
uniform float cell = 0.14;
uniform float alpha = 0.75;
uniform vec4 tint : source_color = vec4(0.96, 0.96, 0.96, 1.0);

void fragment() {
	vec2 p = UV * size / cell;
	vec2 g = abs(fract(p - 0.5) - 0.5) / fwidth(p);
	float line = 1.0 - clamp(min(g.x, g.y) - 0.6, 0.0, 1.0);
	ALBEDO = tint.rgb;
	ALPHA = line * alpha;
}
