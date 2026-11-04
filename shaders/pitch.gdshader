// Piso da quadra/campo com todas as linhas em SDF (nítidas em qualquer
// resolução, sem texturas). surface: 0 grama, 1 madeira (futsal), 2 asfalto.
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform int surface = 0;
uniform float hl = 52.5;
uniform float hw = 34.0;
uniform float box_depth = 16.5;
uniform float box_half = 20.16;
uniform float center_r = 9.15;
uniform float pen_dist = 11.0;
uniform int layout = 0;      // 0 campo, 1 futsal, 2 street
uniform float goal_half = 3.66;
uniform float line_w = 0.12;
uniform float stripes = 1.0;
uniform int mow_style = 0;    // 0 faixas, 1 xadrez, 2 círculos
uniform float night = 0.0;
// quadras de futsal: cores do piso, tipo (0 madeira, 1 sintético, 2 grama sintética)
uniform vec4 floor_a : source_color = vec4(0.74, 0.50, 0.29, 1.0);
uniform vec4 floor_b : source_color = vec4(0.83, 0.59, 0.36, 1.0);
uniform vec4 outside_col : source_color = vec4(0.16, 0.30, 0.55, 1.0);
uniform vec4 area_col : source_color = vec4(0.20, 0.40, 0.70, 1.0);
uniform int floor_kind = 0;
// rua: 0 asfalto, 1 placas de cimento, 2 areia, 3 terra, 4 emborrachado, 5 ladrilhos, 6 cimento liso
uniform int street_kind = 0;
uniform vec4 street_line : source_color = vec4(0.95, 0.85, 0.25, 1.0);
uniform float wet = 0.0;
uniform float snow = 0.0;

varying vec2 wp;

void vertex() {
	wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y);
}
float sd_box(vec2 p, vec2 b) { vec2 d = abs(p) - b; return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0); }
float ring(vec2 p, vec2 c, float r) { return abs(length(p - c) - r); }

float lines(vec2 p) {
	vec2 q = vec2(abs(p.x), p.y);
	vec2 a = vec2(abs(p.x), abs(p.y));
	float d = abs(sd_box(p, vec2(hl, hw)));
	d = min(d, max(abs(p.x), abs(p.y) - hw));
	d = min(d, ring(p, vec2(0.0), center_r));
	d = min(d, length(p) - 0.2);
	if (layout == 0) {
		d = min(d, abs(sd_box(q - vec2(hl - box_depth * 0.5, 0.0), vec2(box_depth * 0.5, box_half))));
		d = min(d, abs(sd_box(q - vec2(hl - 2.75, 0.0), vec2(2.75, 9.16))));
		d = min(d, length(q - vec2(hl - pen_dist, 0.0)) - 0.16);
		if (q.x < hl - box_depth) d = min(d, ring(q, vec2(hl - pen_dist, 0.0), 9.15));
		if (a.x < hl && a.y < hw) d = min(d, ring(a, vec2(hl, hw), 1.0));
	} else if (layout == 1) {
		float gy = goal_half * 0.55;
		if (a.x < hl) {
			if (a.y > gy) d = min(d, ring(a, vec2(hl, gy), box_depth));
			else d = min(d, abs(a.x - (hl - box_depth)));
		}
		d = min(d, length(q - vec2(hl - 6.0, 0.0)) - 0.12);
		d = min(d, length(q - vec2(hl - 10.0, 0.0)) - 0.12);
	} else {
		if (q.x < hl) d = min(d, ring(q, vec2(hl, 0.0), 3.0));
	}
	return d;
}

void fragment() {
	vec2 p = wp;
	bool inside = abs(p.x) < hl + 0.05 && abs(p.y) < hw + 0.05;
	vec3 c;
	float n = noise(p * 0.35) * 0.6 + noise(p * 2.1) * 0.4;
	if (surface == 0) {
		vec3 g1 = vec3(0.20, 0.50, 0.21);
		vec3 g2 = vec3(0.25, 0.58, 0.25);
		float band = mod(floor((p.x + hl) / (hl / 10.0)), 2.0);
		if (mow_style == 1) band = mod(floor((p.x + hl) / (hl / 8.0)) + floor((p.y + hw) / (hw / 5.0)), 2.0);
		else if (mow_style == 2) band = mod(floor(length(p) / 5.0), 2.0);
		c = mix(g1, g2, band * stripes);
		// textura de folhas (fina, some ao longe)
		float blades = noise(p * 38.0) * 0.5 + noise(p * 71.0 + 3.0) * 0.5;
		c *= 0.95 + 0.1 * blades * (1.0 - clamp(fwidth(p.x) * 6.0, 0.0, 1.0));
		// faixas cruzadas leves (corte em xadrez)
		float band2 = mod(floor((p.y + hw) / (hw / 6.0)), 2.0);
		c *= 0.97 + 0.04 * band2 * stripes;
		c *= 0.9 + 0.2 * n;
		if (abs(p.x) > hl + 3.0 || abs(p.y) > hw + 3.0) c *= 0.82;
	} else if (surface == 1) {
		if (floor_kind == 0) {
			float row = floor(p.y / 0.45);
			float plank = floor((p.x + h21(vec2(row, 1.0)) * 6.0) / 3.2);
			float t = h21(vec2(row, plank));
			c = mix(floor_a.rgb, floor_b.rgb, t);
			c *= 0.94 + 0.08 * noise(vec2(p.x * 0.6, p.y * 9.0));
			float seam = smoothstep(0.0, 0.03, abs(fract(p.y / 0.45) - 0.5) - 0.47);
			c *= 1.0 - 0.15 * (1.0 - seam);
		} else if (floor_kind == 1) {
			c = mix(floor_a.rgb, floor_b.rgb, 0.5 + 0.5 * noise(p * 0.2)) * (0.96 + 0.06 * noise(p * 8.0));
		} else {
			float band = mod(floor((p.x + hl) / 2.5), 2.0);
			c = mix(floor_a.rgb, floor_b.rgb, band) * (0.9 + 0.2 * noise(p * 3.0));
		}
		if (!inside) c = outside_col.rgb * (0.95 + 0.1 * n);
		else if (abs(p.x) < hl && abs(p.y) < hw && length(vec2(abs(p.x) - hl, p.y)) < box_depth + 0.5) c = mix(c, area_col.rgb, 0.35);
	} else {
		if (street_kind == 0) {
			c = vec3(0.36, 0.36, 0.39) * (0.8 + 0.35 * n);
			c *= 0.92 + 0.12 * step(0.85, noise(p * 6.0));
		} else if (street_kind == 1) {
			vec2 g = abs(fract(p / 3.0) - 0.5);
			c = vec3(0.62, 0.61, 0.58) * (0.86 + 0.2 * n) * (1.0 - 0.25 * step(0.47, max(g.x, g.y)));
		} else if (street_kind == 2) {
			c = vec3(0.87, 0.77, 0.56) * (0.9 + 0.15 * noise(p * 1.5)) * (0.95 + 0.08 * noise(p * 14.0));
		} else if (street_kind == 3) {
			c = vec3(0.55, 0.38, 0.24) * (0.82 + 0.3 * noise(p * 0.8)) * (0.92 + 0.12 * noise(p * 10.0));
		} else if (street_kind == 4) {
			c = vec3(0.13, 0.27, 0.52) * (0.92 + 0.12 * noise(p * 5.0));
		} else if (street_kind == 5) {
			vec2 q2 = p / vec2(0.9, 0.45);
			q2.x += mod(floor(q2.y), 2.0) * 0.5;
			vec2 g = abs(fract(q2) - 0.5);
			c = mix(vec3(0.55, 0.32, 0.25), vec3(0.62, 0.4, 0.3), h21(floor(q2))) * (1.0 - 0.3 * step(0.44, max(g.x, g.y)));
		} else {
			c = vec3(0.58, 0.58, 0.6) * (0.9 + 0.15 * n);
		}
		if (!inside) c *= 0.75;
	}
	// chuva: grama mais escura e com reflexos; neve: manto branco irregular
	if (wet > 0.0) {
		c *= 1.0 - 0.22 * wet;
		float sheen = smoothstep(0.62, 0.95, noise(p * 0.6 + vec2(TIME * 0.05, 0.0))) * wet;
		c += vec3(0.08, 0.1, 0.12) * sheen;
	}
	if (snow > 0.0) {
		float cover = smoothstep(0.25, 0.75, noise(p * 0.9) * 0.7 + snow * 0.6);
		c = mix(c, vec3(0.93, 0.95, 0.98), cover * snow);
	}
	float d = lines(p);
	float aa = fwidth(d) * 0.8 + 0.002;
	float lm = 1.0 - smoothstep(line_w * 0.5 - aa, line_w * 0.5 + aa, d);
	vec3 lc = surface == 1 ? vec3(0.97, 0.97, 0.95) : vec3(0.96);
	if (surface == 2) lc = street_line.rgb;
	c = mix(c, lc, lm * 0.95);
	if (night > 0.0) {
		float e = length(p / vec2(hl + 6.0, hw + 6.0));
		c *= 1.0 - night * 0.28 * smoothstep(0.55, 1.35, e);
		c *= 1.0 + night * 0.05;
	}
	ALBEDO = c;
}
