// Arquibancada com torcida procedural (cada célula = um torcedor que pula).
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec2 seats = vec2(200.0, 18.0);
uniform float excitement = 0.2;
uniform vec4 seat_color : source_color = vec4(0.10, 0.12, 0.17, 1.0);
uniform vec4 team_a : source_color = vec4(0.9, 0.2, 0.2, 1.0);
uniform vec4 team_b : source_color = vec4(0.2, 0.4, 0.9, 1.0);
uniform float fill = 0.88;
uniform float bias = 0.5;      // 1 = só torcida do time A, 0 = só do B
uniform float light = 1.0;

float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

void fragment() {
	vec2 g = vec2(UV.x, 1.0 - UV.y) * seats;
	vec2 cell = floor(g);
	vec2 f = fract(g);
	float r = h21(cell);
	vec3 c = seat_color.rgb * (0.85 + 0.3 * h21(cell + 7.0));
	if (r < fill) {
		float jump = max(0.0, sin(TIME * (5.0 + 4.0 * r) + r * 40.0)) * excitement * 0.35;
		float by = f.y + jump;
		float pick = h21(cell + 5.0);
		vec3 shirt = pick < 0.88 ? (pick < 0.88 * bias ? team_a.rgb : team_b.rgb) : vec3(0.9);
		shirt *= 0.7 + 0.5 * h21(cell + 3.0);
		vec3 skin = mix(vec3(0.95, 0.75, 0.6), vec3(0.35, 0.22, 0.15), h21(cell + 11.0));
		// corpo
		if (abs(f.x - 0.5) < 0.32 && by > 0.05 && by < 0.55) c = shirt;
		// cabeça
		if (length(vec2(f.x - 0.5, by - 0.72) * vec2(1.0, 1.1)) < 0.2) c = skin;
		// braços pra cima quando empolgada
		if (excitement > 0.55 && r > 0.6 && abs(abs(f.x - 0.5) - 0.36) < 0.06 && by > 0.55 && by < 0.95) c = skin;
	}
	// sombreamento por fileira (profundidade)
	c *= 0.75 + 0.25 * UV.y;
	ALBEDO = c * light;
}
