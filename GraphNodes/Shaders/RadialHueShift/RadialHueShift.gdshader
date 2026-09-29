shader_type canvas_item;
// Shifts the HSV hue of every pixel of the input by its angle around a centre point plus an
// offset: a full turn around the centre is a full turn around the colour wheel. The angle is
// measured counter-clockwise from straight right, in pixels, so it isn't squashed by the aspect ratio.

uniform vec2 viewport_size = vec2(1920.0, 1080.0);
uniform sampler2D texture0 : filter_linear, repeat_disable;
uniform bool use_texture0;
uniform float center_x = 0.5;   // 0 left .. 1 right
uniform float center_y = 0.5;   // 0 top .. 1 bottom
uniform float offset = 0.0;     // degrees

// From https://web.archive.org/web/20200207113336/http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
vec3 rgb2hsv(vec3 c)
{
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c)
{
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment()
{
	vec4 col = use_texture0 ? texture(texture0, UV) : vec4(0.0, 0.0, 0.0, 1.0);
	vec2 d = (UV - vec2(center_x, center_y)) * viewport_size;
	float angle = atan(-d.y, d.x); // screen y points down; flip so counter-clockwise is positive
	float shift = angle / TAU + offset / 360.0;
	vec3 hsv = rgb2hsv(col.rgb);
	hsv.x = fract(hsv.x + shift);
	COLOR = vec4(hsv2rgb(hsv), col.a);
}
