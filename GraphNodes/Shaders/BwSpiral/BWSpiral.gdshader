shader_type canvas_item;
uniform float gap_size;
uniform float rotation_degrees;
uniform float spiral_count = 1.0f;
uniform vec2 viewport_size;
uniform sampler2D texture0;
uniform bool use_texture0;
uniform sampler2D texture1;
uniform bool use_texture1;
uniform float fade_pixels = 2.0;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	vec2 centered_uv = UV - 0.5;
	vec2 normalized_uv = centered_uv * viewport_size;
	float angle = atan(normalized_uv.y, normalized_uv.x) + rotation_degrees * PI / 180.0;
	float cell = fract((length(normalized_uv) + angle * spiral_count * gap_size / PI) / (gap_size * 2.0)) * 2.0;
	
	float edge_dist = min(abs(1.0-cell),min(cell, 2.0-cell));
	
	vec4 color0 = (use_texture0) ? texture(texture0, UV) : vec4(0.0,0.0,0.0,1.0);
	vec4 color1 = (use_texture1) ? texture(texture1, UV) : vec4(1.0);
	
	vec4 bw = (cell > 1.0) ? color0 : color1;
	
	// Fade width for smooth borders (adjust this value to control fade thickness)
	float fade_width = fade_pixels / gap_size;;
	
	// Lerp between current cell color and opposite color based on edge proximity
	float fade = smoothstep(0.0, fade_width, edge_dist);
	vec4 half_bw = mix(color0, color1, 0.5);
	vec4 color = mix(half_bw, bw, fade);
	
	// Called for every pixel the material is visible on.
	COLOR.rgba = color;
}
