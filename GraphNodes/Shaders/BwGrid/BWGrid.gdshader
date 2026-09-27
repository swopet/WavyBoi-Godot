shader_type canvas_item;
uniform float cell_size;
uniform float cell_rotation_degrees;
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
	float angle = atan(normalized_uv.y, normalized_uv.x) + cell_rotation_degrees * PI / 180.0;
	vec2 rotated_uv = length(normalized_uv) * vec2(cos(angle),sin(angle));
	vec2 cell_coord = rotated_uv / cell_size;
	vec2 cell = floor(cell_coord);
	vec2 cell_frac = fract(cell_coord);
	
	// Distance from cell boundary (0 at edges, 0.5 at center)
	float edge_dist_x = min(abs(cell_frac.x), abs(1.0 - cell_frac.x));
	float edge_dist_y = min(abs(cell_frac.y), abs(1.0 - cell_frac.y));
	float edge_dist = min(edge_dist_x, edge_dist_y);
	
	vec4 color0 = (use_texture0) ? texture(texture0, UV) : vec4(0.0,0.0,0.0,1.0);
	vec4 color1 = (use_texture1) ? texture(texture1, UV) : vec4(1.0);
	
	// Checkerboard pattern
	vec4 bw = (int(cell.x + cell.y) % 2 == 0) ? color0 : color1;
	
	// Fade width for smooth borders (adjust this value to control fade thickness)
	float fade_width = fade_pixels / cell_size;;
	
	// Lerp between current cell color and opposite color based on edge proximity
	float fade = smoothstep(0.0, fade_width, edge_dist);
	vec4 half_bw = mix(color0, color1, 0.5);
	vec4 color = mix(half_bw, bw, fade);
	
	// Called for every pixel the material is visible on.
	COLOR.rgba = color;
}
