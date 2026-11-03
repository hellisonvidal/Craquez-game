// Placa de LED animada (pixels + faixas correndo).
shader_type spatial;
render_mode unshaded;

uniform vec4 c1 : source_color = vec4(0.1, 0.2, 0.6, 1.0);
uniform vec4 c2 : source_color = vec4(0.05, 0.05, 0.1, 1.0);
uniform vec2 dots = vec2(600.0, 12.0);

void fragment() {
	vec2 f = fract(UV * dots);
	float px = smoothstep(0.08, 0.2, f.x) * smoothstep(0.08, 0.2, f.y);
	float seg = step(0.5, fract(UV.x * 5.0 - TIME * 0.18));
	float wave = 0.5 + 0.5 * sin(UV.x * 60.0 - TIME * 3.0);
	vec3 col = mix(c1.rgb, c2.rgb, seg) * (0.55 + 0.35 * wave);
	ALBEDO = col * (0.55 + 0.45 * px);
}
