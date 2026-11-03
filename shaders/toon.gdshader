// Sombreamento "desenhado" (cel-shading): 2 tons + brilho de borda.
// Sem cálculo de luz da engine → leve e idêntico em PC e celular.
shader_type spatial;
render_mode unshaded, cull_back;

uniform vec4 albedo : source_color = vec4(1.0);
uniform vec3 light_dir = vec3(-0.35, 0.85, 0.45);
uniform float shade : hint_range(0.0, 1.0) = 0.6;
uniform vec4 shade_tint : source_color = vec4(0.62, 0.66, 0.95, 1.0);
uniform float rim = 0.22;
uniform float spec = 0.0;

varying vec3 wnormal;

void vertex() {
	wnormal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

void fragment() {
	vec3 n = normalize(wnormal);
	vec3 l = normalize(light_dir);
	float ndl = dot(n, l);
	float lit = smoothstep(-0.03, 0.05, ndl);
	vec3 dark = albedo.rgb * shade * mix(vec3(1.0), shade_tint.rgb * 1.2, 0.5);
	vec3 c = mix(dark, albedo.rgb, lit);
	vec3 v = normalize((INV_VIEW_MATRIX * vec4(VIEW, 0.0)).xyz);
	float fres = pow(1.0 - clamp(dot(n, v), 0.0, 1.0), 3.0);
	c += vec3(fres * rim * (0.4 + 0.6 * lit));
	if (spec > 0.0) {
		vec3 h = normalize(l + v);
		c += vec3(step(0.97, dot(n, h)) * spec);
	}
	ALBEDO = c;
}
