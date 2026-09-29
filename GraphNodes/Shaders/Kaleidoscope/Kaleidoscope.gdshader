shader_type canvas_item;
// Write color and alpha as-is. The quad covers the whole render target, so there's nothing
// to blend with, and blending into the transparent target would darken semi-transparent
// pixels from the source image.
render_mode blend_disabled;

// Adapted for the graph: the source comes in on a texture input instead of TEXTURE, the
// output resolution is set by the node (viewport_size), the input resolution is read from
// the texture, and the vec2/bool uniforms are split into floats/ints so wires can drive them.
uniform vec2 viewport_size = vec2(1920.0, 1080.0);
uniform sampler2D textureInput : filter_linear, repeat_disable;
uniform bool use_textureInput;
uniform float sample_center_x = 0.5;
uniform float sample_center_y = 0.5;
uniform float offset_center_x = 0.5;
uniform float offset_center_y = 0.5;
uniform float zoom = 1.0;
uniform float reflections = 4.0;
uniform float rotation = 0.0;
// Keep only the largest centered circle; everything outside becomes transparent
uniform int circle_mode = 0;

float flip_clamp(float val, float divisor){
	float modf_val = mod(val, divisor * 2.0);
	return min(divisor * 2.0 - modf_val, modf_val);
}

vec2 cis(float angle){
	return vec2(cos(angle),sin(angle));
}

float angle_offset(float angle,float radius, float safe_reflections){
	//return 0.0;
	return 3.14159 / safe_reflections / 2.0 * sin(angle + radius * 3.14159 /  100.0);
}

void fragment(){
	vec2 output_resolution = viewport_size;
	vec2 input_resolution = use_textureInput ? vec2(textureSize(textureInput, 0)) : output_resolution;
	vec2 sample_center = vec2(sample_center_x, sample_center_y);
	vec2 offset_center = vec2(offset_center_x, offset_center_y);
	// Keep zoom and reflections off zero so nothing divides by zero
	float safe_zoom = max(zoom, 0.0001);
	float safe_reflections = max(reflections, 0.0001);

	vec2 uv = UV - offset_center;
	uv = uv * output_resolution / safe_zoom;
	float angle = atan(uv.y, uv.x);
	float radius = length(uv);
	float angle_off = angle_offset(angle,radius,safe_reflections);
	angle_off = 0.0;
	angle = angle - rotation;
	angle += angle_off;
	angle = flip_clamp(angle, 3.14159265359 / safe_reflections);
	angle -= angle_off;
	angle = flip_clamp(angle, 3.14159265359 / safe_reflections);

	angle = angle + rotation;


	uv = radius * cis(angle);
	uv = uv / input_resolution;
	uv = uv + sample_center;
	uv = vec2(flip_clamp(uv.x,1.0),flip_clamp(uv.y,1.0));
	COLOR = use_textureInput ? vec4(texture(textureInput,uv)) : vec4(0.0, 0.0, 0.0, 1.0);
	if (circle_mode != 0){
		vec2 from_center = UV * output_resolution - output_resolution * 0.5;
		float circle_radius = min(output_resolution.x, output_resolution.y) * 0.5;
		// Hard edge: a pixel is kept only if its center is inside the circle
		if (length(from_center) > circle_radius){
			COLOR.a = 0.0;
		}
	}
}
