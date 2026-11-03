// Bola com gomos (icosaedro) e sombreamento desenhado.
shader_type spatial;
render_mode unshaded;

uniform vec3 light_dir = vec3(-0.35, 0.85, 0.45);
uniform vec4 base_color : source_color = vec4(0.98, 0.98, 0.98, 1.0);

varying vec3 wn;
varying vec3 on;

void vertex() {
	wn = normalize(MODEL_NORMAL_MATRIX * NORMAL);
	on = normalize(NORMAL);
}

void fragment() {
	const float t = 1.618034;
	vec3 v[6] = vec3[6](vec3(-1.0, t, 0.0), vec3(1.0, t, 0.0), vec3(0.0, -1.0, t), vec3(0.0, 1.0, t), vec3(t, 0.0, -1.0), vec3(t, 0.0, 1.0));
	float m = 0.0;
	for (int i = 0; i < 6; i++) {
		m = max(m, abs(dot(normalize(on), normalize(v[i]))));
	}
	vec3 base = m > 0.935 ? vec3(0.07, 0.08, 0.12) : base_color.rgb;
	float ndl = dot(normalize(wn), normalize(light_dir));
	float lit = smoothstep(-0.05, 0.08, ndl);
	ALBEDO = mix(base * vec3(0.55, 0.58, 0.75), base, lit);
}
