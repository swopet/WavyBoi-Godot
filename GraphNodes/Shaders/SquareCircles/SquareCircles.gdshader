shader_type canvas_item;
// Ported from square_circles.frag: rings of 10 bordered squares orbiting around circles (ring
// direction alternates), rows of rings scrolling sideways over 4 depth layers, squares breathing
// and colour-cycling. The result is blended with Feedback (was last_frame; wire a Frame Buffer
// of this node's output) by Mask brightness, or by an oscillating amount without a Mask.
// Bass enlarges the rings.

uniform vec2 viewport_size = vec2(1920.0, 1080.0);
uniform sampler2D texture0 : filter_linear, repeat_disable; // mask for the trail amount
uniform bool use_texture0;
uniform sampler2D textureFeedback : filter_linear, repeat_disable; // previous frame
uniform bool use_textureFeedback;
uniform float time = 0.0;
uniform float bass = 0.0;


vec2 cs(float angle){
	return vec2(cos(angle), sin(angle));
}

float inSquare(vec2 d, vec2 size){
	if (d.x <= size.x / 2.0 && d.y <= size.y / 2.0 && d.x >= -size.x / 2.0 && d.y >= -size.y / 2.0) return 1.0;
	else return 0.0;
}

void fragment()
{
	vec2 fragCoord = vec2(UV.x, 1.0 - UV.y);
	vec2 uv = fragCoord - vec2(0.5, 0.5);
	uv = uv * viewport_size / viewport_size.yy * 3.0;
	vec3 col = vec3(0.0);
	const int SQUARES = 10;
	float squares = float(SQUARES);
	float radius = 1.1 + 2.0 * bass;
	float width = 0.2;
	float border = 0.01;
	float fade = 0.005;
	float angle_add = time / 5.0 / squares * TAU;

	float ratio = 1.0;
	const int ZOOM_LEVELS = 4;
	float zoom_levels = float(ZOOM_LEVELS);
	for (int zoom_index = 1; zoom_index <= ZOOM_LEVELS; zoom_index++){
		float zoom = float(zoom_index);
		vec2 uv_ = uv * zoom_levels / zoom;
		float angle_ = atan(uv_.y, uv_.x);
		vec2 uv_rot = length(uv_) * cs(angle_);
		uv_rot = uv_rot + ((mod(zoom, 2.0) == 1.0) ? 1.0 : -1.0) * vec2(time * radius * ratio * 2.0 / 5.0, 0.0);
		float covered_ = 0.0;
		float pos = floor(uv_rot.x / (radius * ratio) + 100.0) - 100.0;
		for (int k = -1; k <= 1; k++){
			float ind = pos + float(k);
			for (int si = 0; si < SQUARES; si++){
				float i = float(si);
				float pos_nes = (mod(ind + zoom + 100.0, 2.0) == 1.0) ? -1.0 : 1.0;
				float angle = i / squares * TAU + angle_add * pos_nes;
				float angle2 = (i + 1.0) / squares * TAU + angle_add * pos_nes;
				vec2 p = radius * cs(angle);
				vec2 p_ = radius * cs(angle2);
				vec2 d = uv_rot - vec2(radius * ratio * ind, 0.0) - p;
				vec2 d_ = uv_rot - vec2(radius * ratio * ind, 0.0) - p_;
				vec2 size = width * vec2(0.8 + 0.2 * sin(angle + pos_nes * time * TAU / 5.0), 0.8 + 0.2 * cos(angle + pos_nes * time * TAU / 5.0));
				vec2 size_ = width * vec2(0.8 + 0.2 * sin(angle2 + pos_nes * time * TAU / 5.0), 0.8 + 0.2 * cos(angle2 + pos_nes * time * TAU / 5.0));
				if (inSquare(d, size) == 1.0 && inSquare(d_, size_) == 0.0){
					if (uv_.y > 0.0 || covered_ == 0.0){
						float bd = min(min(abs(d.x - size.x / 2.0), abs(d.x + size.x / 2.0)), min(abs(d.y - size.y / 2.0), abs(d.y + size.y / 2.0)));
						col = smoothstep(0.0, fade, bd - border) * zoom / zoom_levels * vec3(0.5 + 0.5 * cos((time + zoom) * TAU / 5.0), 0.5 + 0.5 * sin((time + zoom / 3.0) * TAU / 5.0), 0.5 + 0.25 * cos(time * TAU * 2.0 / 5.0) - 0.25 * sin((time + uv_rot.x) * TAU * 3.0 / 5.0));
						covered_ = 1.0;
					}
				}
			}
		}
	}
	float mix_val = 0.5 + 0.5 * cos(time);
	// The old app's textures were stored upside down; Godot's aren't, so sample at UV directly
	if (use_texture0){
		vec3 mask = texture(texture0, UV).rgb;
		mix_val = mask.r * 0.22 + mask.g * 0.71 + mask.b * 0.07;
	}
	if (use_textureFeedback){
		col = mix(col, texture(textureFeedback, UV).rgb, mix_val);
	}
	COLOR = vec4(col, 1.0);
}
