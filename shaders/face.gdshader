// Cabeça com rosto desenhado (olhos, sobrancelhas, nariz, boca, barba) + cel-shading.
// UV: u = 0.5 é a frente; v = 0 topo, 1 queixo.
shader_type spatial;
render_mode unshaded, cull_back;

uniform vec4 skin : source_color = vec4(0.8, 0.6, 0.45, 1.0);
uniform vec4 hair_col : source_color = vec4(0.1, 0.07, 0.05, 1.0);
uniform vec4 eye_col : source_color = vec4(0.25, 0.16, 0.08, 1.0);
uniform float beard = 0.0;
uniform vec3 light_dir = vec3(-0.35, 0.85, 0.45);

varying vec3 wn;

void vertex() {
	wn = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

float ell(vec2 p, vec2 c, vec2 r) {
	vec2 d = (p - c) / r;
	return dot(d, d);
}

void fragment() {
	float ph = (UV.x - 0.5) * 6.2831853;
	float th = UV.y * 3.14159265;
	vec3 c = skin.rgb;
	float aa = 0.08;
	// barba/rosto sombreado
	if (beard > 0.0 && th > 2.0 && abs(ph) < 1.35) {
		float b = smoothstep(2.0, 2.15, th) * (1.0 - smoothstep(1.1, 1.35, abs(ph)));
		c = mix(c, hair_col.rgb * 0.9 + skin.rgb * 0.1, b * beard * 0.85);
	}
	for (int k = 0; k < 2; k++) {
		float sx = k == 0 ? -1.0 : 1.0;
		vec2 e = vec2(sx * 0.34, 1.57);
		// sobrancelha (levemente inclinada)
		float by = 1.36 + (ph * sx - 0.34) * 0.12;
		if (abs(th - by) < 0.035 && abs(ph - sx * 0.35) < 0.17) c = hair_col.rgb * 0.85;
		// olho
		float d = ell(vec2(ph, th), e, vec2(0.13, 0.07));
		if (d < 1.0) {
			c = vec3(0.95, 0.94, 0.9);
			float di = ell(vec2(ph, th), e + vec2(0.0, 0.004), vec2(0.06, 0.062));
			if (di < 1.0) c = eye_col.rgb;
			if (di < 0.3) c = vec3(0.03);
			if (d > 0.72 && th < e.y) c = vec3(0.12, 0.08, 0.07);   // cílios/pálpebra
		}
	}
	// narinas
	for (int k = 0; k < 2; k++) {
		float sx = k == 0 ? -1.0 : 1.0;
		if (ell(vec2(ph, th), vec2(sx * 0.07, 1.86), vec2(0.035, 0.025)) < 1.0) c *= 0.6;
	}
	// boca (leve sorriso)
	float my = 2.18 - 0.05 * (ph / 0.22) * (ph / 0.22);
	if (abs(ph) < 0.24 && abs(th - my) < 0.028) c = mix(c, vec3(0.45, 0.18, 0.16), 0.85);
	if (abs(ph) < 0.18 && th > my + 0.028 && th < my + 0.07) c = mix(c, skin.rgb * vec3(0.92, 0.7, 0.68), 0.6);
	// cel-shading
	float ndl = dot(normalize(wn), normalize(light_dir));
	float lit = smoothstep(-0.03, 0.05, ndl);
	vec3 dark = c * 0.62 * mix(vec3(1.0), vec3(0.75, 0.78, 1.1), 0.5);
	ALBEDO = mix(dark, c, lit);
}
