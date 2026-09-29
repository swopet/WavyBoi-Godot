shader_type canvas_item;

// One pass of a video feedback loop: take last frame (via a Frame Buffer), zoom/rotate/shift
// and fade it, then blend fresh material on top. Feed the output back into a Frame Buffer.

uniform vec2 viewport_size = vec2(1920.0, 1080.0);
uniform sampler2D textureFeedback : filter_linear, repeat_disable;
uniform bool use_textureFeedback;
uniform sampler2D textureFresh : filter_linear;
uniform bool use_textureFresh;

uniform float zoom = 1.02;            // > 1 grows the image each pass, < 1 shrinks it
uniform float rotation_degrees = 1.0; // per pass, counter-clockwise
uniform float offset_x = 0.0;         // per pass, fraction of the frame width (+ = right)
uniform float offset_y = 0.0;         // per pass, fraction of the frame height (+ = down)
uniform float decay = 0.02;           // fraction faded toward black each pass
uniform float fresh_amount = 1.0;     // strength of the fresh texture
uniform int blend_mode = 2;           // 0 = mix, 1 = add, 2 = lighten

void fragment() {
	// Work in aspect-corrected coordinates around the centre so rotation doesn't shear
	float aspect = viewport_size.y > 0.0 ? viewport_size.x / viewport_size.y : 1.0;
	vec2 p = (UV - 0.5) * vec2(aspect, 1.0);
	p -= vec2(offset_x * aspect, offset_y);
	// Sampling at the point rotated the other way turns the image counter-clockwise on screen
	float a = radians(rotation_degrees);
	p = mat2(vec2(cos(a), sin(a)), vec2(-sin(a), cos(a))) * p;
	p /= max(zoom, 0.0001);
	vec2 feedback_uv = p / vec2(aspect, 1.0) + 0.5;

	// Anything brought in from outside the frame is black
	vec3 fed = vec3(0.0);
	if (use_textureFeedback && all(greaterThanEqual(feedback_uv, vec2(0.0))) && all(lessThanEqual(feedback_uv, vec2(1.0)))) {
		fed = texture(textureFeedback, feedback_uv).rgb;
	}
	fed *= clamp(1.0 - decay, 0.0, 1.0);

	vec4 fresh = use_textureFresh ? texture(textureFresh, UV) : vec4(0.0);
	float amount = clamp(fresh_amount, 0.0, 1.0) * fresh.a;
	vec3 rgb;
	if (blend_mode == 1) {
		rgb = fed + fresh.rgb * amount;
	} else if (blend_mode == 2) {
		rgb = max(fed, fresh.rgb * amount);
	} else {
		rgb = mix(fed, fresh.rgb, amount);
	}
	COLOR = vec4(clamp(rgb, 0.0, 1.0), 1.0);
}
