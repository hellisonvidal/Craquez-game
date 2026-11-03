shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

uniform float strength = 0.42;

void fragment() {
	float r = length(UV * 2.0 - 1.0);
	ALBEDO = vec3(0.02, 0.05, 0.03);
	ALPHA = (1.0 - smoothstep(0.35, 1.0, r)) * strength;
}
