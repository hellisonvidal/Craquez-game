shader_type canvas_item;

uniform float strength = 0.38;
uniform vec4 tint : source_color = vec4(0.0, 0.02, 0.06, 1.0);

void fragment() {
	vec2 uv = SCREEN_UV - 0.5;
	float v = smoothstep(0.32, 0.86, length(uv * vec2(1.0, 1.25)));
	COLOR = vec4(tint.rgb, v * strength);
}
