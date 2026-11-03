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
uniform float wave = -1.0;      // posição da "ola" ao longo da arquibancada (0..1), <0 desligada
uniform float celebrate = 0.0;  // comemoração de gol
uniform float tifo = 0.0;       // mosaico listrado (torcida da casa)

float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

void fragment() {
	vec2 g = vec2(UV.x, 1.0 - UV.y) * seats;
	vec2 cell = floor(g);
	vec2 f = fract(g);
	float r = h21(cell);
	vec3 c = seat_color.rgb * (0.85 + 0.3 * h21(cell + 7.0));
	float ola = wave >= 0.0 ? exp(-pow((UV.x - wave) / 0.035, 2.0)) : 0.0;
	if (r < fill) {
		float hype = max(excitement, celebrate);
		float jump = max(0.0, sin(TIME * (5.0 + 4.0 * r + celebrate * 3.0) + r * 40.0)) * hype * (0.35 + 0.25 * celebrate);
		float by = f.y + jump - ola * 0.35;
		float pick = h21(cell + 5.0);
		vec3 shirt = pick < 0.88 ? (pick < 0.88 * bias ? team_a.rgb : team_b.rgb) : vec3(0.9);
		if (tifo > 0.0 && mod(cell.y, 4.0) < 2.0) shirt = mix(shirt, vec3(0.95), tifo);
		shirt *= 0.7 + 0.5 * h21(cell + 3.0);
		vec3 skin = mix(vec3(0.95, 0.75, 0.6), vec3(0.35, 0.22, 0.15), h21(cell + 11.0));
		// corpo
		if (abs(f.x - 0.5) < 0.32 && by > 0.05 && by < 0.55) c = shirt;
		// cabeça
		if (length(vec2(f.x - 0.5, by - 0.72) * vec2(1.0, 1.1)) < 0.2) c = skin;
		// braços pra cima quando empolgada
		bool arms = (excitement > 0.55 && r > 0.6) || ola > 0.3 || (celebrate > 0.3 && r > 0.25);
		if (arms && abs(abs(f.x - 0.5) - 0.36) < 0.06 && by > 0.55 && by < 0.95) c = skin;
		// cachecol erguido na comemoração
		if (celebrate > 0.3 && r > 0.5 && by > 0.92 && by < 1.05 && abs(f.x - 0.5) < 0.45) c = mod(cell.x, 2.0) < 1.0 ? team_a.rgb : vec3(0.95);
		// flashes de celular / câmera
		if (hype > 0.6 && step(0.996, h21(cell + floor(TIME * 9.0))) > 0.5) c = vec3(1.0);
	}
	// bandeiras tremulando acima de alguns torcedores
	float fl = h21(floor(cell / vec2(3.0, 2.0)) + 17.0);
	if (fl < 0.035 + 0.05 * celebrate) {
		vec2 fc = fract(g / vec2(3.0, 2.0));
		float wav = sin(TIME * 4.0 + cell.x * 0.7 + fc.x * 6.0) * 0.08;
		if (fc.y + wav > 0.35 && fc.y + wav < 0.95 && fc.x > 0.05 && fc.x < 0.95)
			c = fc.y + wav > 0.65 ? team_a.rgb : (fl < 0.02 ? vec3(0.95) : team_a.rgb * 0.7);
	}
	// sombreamento por fileira (profundidade)
	c *= 0.75 + 0.25 * UV.y;
	ALBEDO = c * light;
}
