// Camisa com padrão (liso, listras, aros, faixa diagonal, metades, riscas,
// faixa no peito), gola e punhos na cor secundária + cel-shading.
// UV = posição de repouso do corpo (x lateral, y altura em metros).
shader_type spatial;
render_mode unshaded, cull_back;

uniform vec4 c1 : source_color = vec4(1.0, 0.0, 0.0, 1.0);
uniform vec4 c2 : source_color = vec4(1.0);
uniform int pattern = 0;
uniform vec3 light_dir = vec3(-0.35, 0.85, 0.45);

varying vec3 wn;

void vertex() {
	wn = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

void fragment() {
	vec2 p = UV;
	float x = p.x;
	float y = p.y;
	bool sec = false;
	if (pattern == 1) sec = mod(floor((x + 0.5) / 0.065), 2.0) > 0.5;
	else if (pattern == 2) sec = mod(floor(y / 0.085), 2.0) > 0.5;
	else if (pattern == 3) sec = abs((x + 0.5) - (y - 0.85) * 0.9 - 0.45) < 0.06;
	else if (pattern == 4) sec = x > 0.0 && abs(x) < 0.2;
	else if (pattern == 5) sec = abs(fract((x + 0.5) / 0.045) - 0.5) < 0.09;
	else if (pattern == 6) sec = y > 1.22 && y < 1.33;
	// gola e punhos
	if (y > 1.52) sec = true;
	if (abs(x) > 0.19 && y < 1.335 && y > 1.29) sec = true;
	vec3 c = sec ? c2.rgb : c1.rgb;
	float ndl = dot(normalize(wn), normalize(light_dir));
	float lit = smoothstep(-0.03, 0.05, ndl);
	vec3 dark = c * 0.6 * mix(vec3(1.0), vec3(0.74, 0.79, 1.14), 0.5);
	ALBEDO = mix(dark, c, lit);
}
