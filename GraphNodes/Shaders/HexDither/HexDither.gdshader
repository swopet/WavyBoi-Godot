shader_type canvas_item;
// Ported from the web app's hex_frag_shader: a nested hexagon line pattern whose subdivision
// level and line opening follow the brightness of the Input image, so the image comes out as a
// dithered hex drawing. Lines are drawn in Color 0 over a background of Color 1, each one of
// 0 black, 1 white, 2 the Input colour, 3 the Color Texture (the Input when that isn't wired).
// Hex grid maths after https://www.redblobgames.com/grids/hexagons/.

uniform vec2 viewport_size = vec2(1920.0, 1080.0);
uniform sampler2D texture0 : filter_linear, repeat_disable; // drives the pattern (the web app blurred it first)
uniform bool use_texture0;
uniform sampler2D texture1 : filter_linear, repeat_disable; // colour source for option 3 (the web app's unblurred image)
uniform bool use_texture1;
uniform float min_size = 6.0;   // side length of the smallest hexes, in pixels
uniform int steps = 6;          // subdivision levels, up to MAX_STEPS
uniform float line_width = 2.0;
uniform float line_fade = 12.0;
uniform float gamma = 1.0;
uniform int color0 = 3;
uniform int color1 = 0;
uniform int control = 1;        // 0 luminance, 1 brightness
uniform int invert = 1;

const float SQRT_3 = 1.73205080757;
const float HALF_SQRT_3 = 0.86602540378;
const float THIRD_SQRT_3 = 0.57735026919;
const float SIXTH_PI = 0.52359877559;
const float THIRD_PI = 1.0471975512;
const float HALF_PI = 1.57079632679;
const int MAX_STEPS = 12;

struct Orientation
{
	float f0; float f1; float f2; float f3;
	float b0; float b1; float b2; float b3;
	float start_angle;
};

struct Layout
{
	Orientation orientation;
	vec2 layout_size;
	vec2 origin;
};

struct Hex
{
	int q;
	int r;
	int s;
};

struct FractionalHex
{
	float q;
	float r;
	float s;
};

Orientation layout_pointy()
{
	return Orientation(SQRT_3, HALF_SQRT_3, 0.0, 3.0 / 2.0,
			THIRD_SQRT_3, -1.0 / 3.0, 0.0, 2.0 / 3.0,
			0.5);
}

Orientation layout_flat()
{
	return Orientation(3.0 / 2.0, 0.0, HALF_SQRT_3, SQRT_3,
			2.0 / 3.0, 0.0, -1.0 / 3.0, THIRD_SQRT_3,
			0.0);
}

// The original's round(): halves always go up, unlike the built-in
float round_half_up(float val)
{
	return floor(val + 0.5);
}

vec2 hex_to_pixel(Layout lay, Hex h)
{
	Orientation M = lay.orientation;
	float x = (M.f0 * float(h.q) + M.f1 * float(h.r)) * lay.layout_size.x;
	float y = (M.f2 * float(h.q) + M.f3 * float(h.r)) * lay.layout_size.y;
	return vec2(x + lay.origin.x, y + lay.origin.y);
}

FractionalHex pixel_to_hex(Layout lay, vec2 p)
{
	Orientation M = lay.orientation;
	vec2 pt = vec2((p.x - lay.origin.x) / lay.layout_size.x,
			(p.y - lay.origin.y) / lay.layout_size.y);
	float q = M.b0 * pt.x + M.b1 * pt.y;
	float r = M.b2 * pt.x + M.b3 * pt.y;
	return FractionalHex(q, r, -q - r);
}

float sdf_line(vec2 p, vec2 a, vec2 b)
{
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h);
}

vec2 cis(float theta)
{
	return vec2(cos(theta), sin(theta));
}

Hex hex_round(FractionalHex h)
{
	int q = int(round_half_up(h.q));
	int r = int(round_half_up(h.r));
	int s = int(round_half_up(h.s));
	float q_diff = abs(float(q) - h.q);
	float r_diff = abs(float(r) - h.r);
	float s_diff = abs(float(s) - h.s);
	if (q_diff > r_diff && q_diff > s_diff) {
		q = -r - s;
	} else if (r_diff > s_diff) {
		r = -q - s;
	} else {
		s = -q - r;
	}
	return Hex(q, r, s);
}

float hex_grid(vec2 p, float d_in, float div_size, float num_steps)
{
	vec2 hex_center = viewport_size / 2.0;
	float div_d = d_in * num_steps;
	float div_select = floor(div_d);
	float div_size_ = div_size * pow(SQRT_3, num_steps - div_select);
	div_d = div_d - div_select;
	for (int i = 0; i < MAX_STEPS; i++) {
		float fi = float(i);
		if (fi < div_select) {
			float scalar = div_size * pow(SQRT_3, num_steps - fi - 1.0) * 2.0;
			vec2 dir = scalar * cis(i % 2 == 0 ? THIRD_PI : -HALF_PI);
			hex_center = hex_center - dir;
		}
	}
	bool even = mod(div_select, 2.0) == 0.0;
	Layout lay = Layout(even ? layout_pointy() : layout_flat(), vec2(div_size_), hex_center);
	hex_center = hex_to_pixel(lay, hex_round(pixel_to_hex(lay, p)));
	p = p - hex_center;
	float r = div_d * div_size_ * THIRD_SQRT_3;
	float theta = div_d * PI / 6.0;
	float d = 1e10;
	for (int i = 0; i < 6; i++) {
		float a_angle = (even ? 0.0 : SIXTH_PI) + THIRD_PI * float(i);
		float b_angle = (even ? THIRD_PI : HALF_PI) + THIRD_PI * float(i);
		vec2 a = r * cis(a_angle);
		vec2 b = r * cis(b_angle);
		d = min(d, sdf_line(p, a, b));
		if (i % 2 == 0) {
			vec2 a_ = a + div_size_ * cis((a_angle + b_angle) / 2.0 - theta);
			vec2 b_ = b + div_size_ * cis((a_angle + b_angle) / 2.0 + theta);
			d = min(d, sdf_line(p, a, a_));
			d = min(d, sdf_line(p, b, b_));
		}
	}
	return d;
}

vec4 pick_color(int choice, vec4 input_color, vec4 second_color)
{
	if (choice == 0) return vec4(0.0, 0.0, 0.0, 1.0);
	if (choice == 1) return vec4(1.0);
	if (choice == 2) return input_color;
	return second_color;
}

void fragment()
{
	vec4 input_color = use_texture0 ? texture(texture0, UV) : vec4(0.0, 0.0, 0.0, 1.0);
	vec4 second_color = use_texture1 ? texture(texture1, UV) : input_color;
	vec3 lin = pow(max(input_color.rgb, vec3(0.0)), vec3(gamma));
	float luminance = dot(lin, vec3(0.22, 0.71, 0.07));
	float brightness = dot(lin, vec3(1.0 / 3.0));
	float greyscale = control == 0 ? luminance : brightness;
	float prog = invert == 1 ? greyscale : 1.0 - greyscale;
	// The original works in GL pixel space (origin bottom-left); keep that so the pattern matches
	vec2 p = vec2(UV.x, 1.0 - UV.y) * viewport_size;
	float num_steps = float(clamp(steps, 1, MAX_STEPS));
	float val = hex_grid(p, clamp(prog, 0.0, 1.0), max(min_size, 0.5), num_steps);
	vec4 col0 = pick_color(color0, input_color, second_color);
	vec4 col1 = pick_color(color1, input_color, second_color);
	COLOR = mix(col0, col1, smoothstep(line_width / 2.0, line_width / 2.0 + line_fade, val));
}
