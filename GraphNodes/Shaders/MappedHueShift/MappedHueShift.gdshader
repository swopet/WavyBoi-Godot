shader_type canvas_item;
// Shifts the HSV hue of each Source pixel by the hue angle of the matching Hue Map pixel
// (red 0, green 120, blue 240 degrees), scaled by Amount, plus an Offset in degrees.
// Greys in the map have no hue, so they leave the source as it is (before the Offset).

uniform sampler2D texture0 : filter_linear, repeat_disable; // source
uniform bool use_texture0;
uniform sampler2D texture1 : filter_linear, repeat_disable; // hue map
uniform bool use_texture1;
uniform float amount = 1.0;
uniform float offset = 0.0; // degrees

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
	float map_hue = use_texture1 ? rgb2hsv(texture(texture1, UV).rgb).x : 0.0;
	vec3 hsv = rgb2hsv(col.rgb);
	hsv.x = fract(hsv.x + map_hue * amount + offset / 360.0);
	COLOR = vec4(hsv2rgb(hsv), col.a);
}
