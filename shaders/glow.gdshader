// Brilho aditivo sempre virado para a câmera (refletores, luzes).
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;

uniform vec4 color : source_color = vec4(1.0, 0.95, 0.8, 1.0);
uniform float power = 2.2;

void vertex() {
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
	float r = length(UV * 2.0 - 1.0);
	float a = pow(max(0.0, 1.0 - r), power);
	ALBEDO = color.rgb * a;
}
