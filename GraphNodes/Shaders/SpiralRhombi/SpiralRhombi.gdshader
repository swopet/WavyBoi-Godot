shader_type canvas_item;
/* Ported from spiral_rhombi.frag: the rhombus tiling wrapped into an Escher-like spiral whose
arm structure is set by the integers A and B. Tiles morph and scroll along the spiral, and the
contour bands blend texture0 (default black) and texture1 (default white); Bass adds bands.
Using https://www.iquilezles.org/www/articles/distfunctions2d/distfunctions2d.htm for the rhombus sdf,
https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm for the repetition of the tiling
using rhombus tesselation functions from https://www.shadertoy.com/view/tlGGDR
spiral stuff based on http://isohedral.ca/escher-like-spiral-tilings/
*/

uniform vec2 viewport_size = vec2(1920.0, 1080.0);
uniform sampler2D texture0 : filter_linear, repeat_disable;
uniform bool use_texture0;
uniform sampler2D texture1 : filter_linear, repeat_disable;
uniform bool use_texture1;
uniform float time = 0.0;
uniform float bass = 0.0;
// Change these to any integer for more fun
uniform int a = 3;
uniform int b = 5;

const float SQRT_HALF = 0.70710678118;

float ndot(vec2 v, vec2 w) { return v.x * w.x - v.y * w.y; }

float sdRhombus(in vec2 p, in vec2 r)
{
	vec2 q = abs(p);
	float h = clamp((-2.0 * ndot(q, r) + ndot(r, r)) / dot(r, r), -1.0, 1.0);
	float d = length(q - 0.5 * r * vec2(1.0 - h, 1.0 + h));
	return d * sign(q.x * r.y + q.y * r.x - r.x * r.y);
}

vec2 rotatePoint(vec2 p, float theta){
	return vec2(p.x * cos(theta) - p.y * sin(theta), p.x * sin(theta) + p.y * cos(theta));
}

vec2 translatePoint(vec2 p, vec2 t){
	return p - t;
}

float tesselatedRhombiPos(float theta1, vec2 p){
	vec2 p_ = p;
	p_ = translatePoint(p_, vec2(0.5, 0.5));
	p_ = rotatePoint(p_, -PI / 4.0);
	float d = sdRhombus(p_, vec2(cos(PI / 4.0), sin(PI / 4.0)));
	p_ = p;
	p_ = translatePoint(p_, vec2(0.5 + 0.5 * cos(theta1), 1.0 + 0.5 * sin(theta1)));
	p_ = rotatePoint(p_, -theta1 / 2.0);
	d = min(d, sdRhombus(p_, vec2(cos(theta1 / 2.0), sin(theta1 / 2.0))));
	p_ = p;
	p_ = translatePoint(p_, vec2(1.0 + 0.5 * sin(theta1), 0.5 - 0.5 * cos(theta1)));
	p_ = rotatePoint(p_, (PI - theta1) / 2.0);
	d = min(d, sdRhombus(p_, vec2(cos(theta1 / 2.0), sin(theta1 / 2.0))));
	p_ = p;
	p_ = translatePoint(p_, vec2(1.0 + SQRT_HALF * cos(theta1 - PI / 4.0), 1.0 + SQRT_HALF * sin(theta1 - PI / 4.0)));
	p_ = rotatePoint(p_, -theta1 - PI / 4.0);
	d = min(d, sdRhombus(p_, vec2(cos(PI / 4.0), sin(PI / 4.0))));
	return d;
}

vec2 opRep(in vec2 p, in vec2 c)
{
	return mod(p + 0.5 * c, c) - 0.5 * c;
}

float getTesselatedRhombusVal(vec2 p, float theta1){
	vec2 a_vec = vec2(cos(theta1), 1.0 + sin(theta1));
	float angle = atan(a_vec.y, a_vec.x);
	float len = length(a_vec);
	vec2 offset = vec2(len, len);
	vec2 p_ = rotatePoint(p, -angle);
	p_ = opRep(p_, offset);
	float val = tesselatedRhombiPos(theta1, rotatePoint(p_, angle));
	val = min(val, tesselatedRhombiPos(theta1, rotatePoint(p_ + vec2(offset.x, 0.0), angle)));
	val = min(val, tesselatedRhombiPos(theta1, rotatePoint(p_ - vec2(offset.x, 0.0), angle)));
	val = min(val, tesselatedRhombiPos(theta1, rotatePoint(p_ + vec2(0.0, offset.y), angle)));
	val = min(val, tesselatedRhombiPos(theta1, rotatePoint(p_ - vec2(0.0, offset.y), angle)));
	val = min(val, tesselatedRhombiPos(theta1, rotatePoint(p_ + vec2(offset.x, offset.y), angle)));
	val = min(val, tesselatedRhombiPos(theta1, rotatePoint(p_ - vec2(offset.x, offset.y), angle)));
	val = min(val, tesselatedRhombiPos(theta1, rotatePoint(p_ + vec2(-offset.y, offset.y), angle)));
	val = min(val, tesselatedRhombiPos(theta1, rotatePoint(p_ - vec2(-offset.y, offset.y), angle)));
	return val;
}

float getFilteredRhombusVal(vec2 p, float theta1){
	float val = 0.6 * getTesselatedRhombusVal(p, theta1);
	//basic filter to prevent aliasing crap
	val += 0.1 * getTesselatedRhombusVal(p + vec2(0.01, 0.0), theta1);
	val += 0.1 * getTesselatedRhombusVal(p - vec2(0.01, 0.0), theta1);
	val += 0.1 * getTesselatedRhombusVal(p + vec2(0.0, 0.01), theta1);
	val += 0.1 * getTesselatedRhombusVal(p - vec2(0.0, 0.01), theta1);
	return val;
}

vec2 posmod(vec2 d, vec2 r){
	return mod(mod(d, r) + r, r);
}

vec2 tex_coord(vec2 gl_uv)
{
	vec2 p = posmod(gl_uv + vec2(2.0), vec2(2.0));
	return vec2((p.x <= 1.0) ? p.x : 2.0 - p.x, (p.y <= 1.0) ? p.y : 2.0 - p.y);
}

vec3 evaluate_texture0(vec2 p){
	return use_texture0 ? texture(texture0, vec2(p.x, 1.0 - p.y)).rgb : vec3(0.0);
}

vec3 evaluate_texture1(vec2 p){
	return use_texture1 ? texture(texture1, vec2(p.x, 1.0 - p.y)).rgb : vec3(1.0);
}

void fragment()
{
	vec2 uv = vec2(UV.x, 1.0 - UV.y);
	vec2 fragCoord = tex_coord(uv);
	float t = time / 2.0;
	vec2 ab = vec2(float(a), float(b));
	uv -= vec2(0.5, 0.5);
	vec2 cart = uv * viewport_size / viewport_size.x * length(ab) * 2.0;
	float angle = atan(cart.y, cart.x);
	float r = length(cart) / 2.0;
	cart = vec2(angle, r);
	float rhombus_theta = mod(t, 2.0) * PI / 2.0;
	vec2 a_vec = vec2(cos(rhombus_theta), 1.0 + sin(rhombus_theta));
	vec2 b_vec = vec2(1.0 + sin(rhombus_theta), -cos(rhombus_theta));
	vec2 ab_vec = ab.x * a_vec + ab.y * b_vec;
	float theta = atan(ab_vec.y, ab_vec.x);
	float l = length(ab_vec) / TAU;

	vec2 p = vec2(cart.x * cos(theta) - cart.y * sin(theta),
	              cart.x * sin(theta) + cart.y * cos(theta)) * l;
	vec2 offset_vec = a_vec * mod(t, 2.0) / 2.0;
	p = p - offset_vec;
	float val = getFilteredRhombusVal(p, rhombus_theta);
	vec3 col = mix(evaluate_texture0(fragCoord), evaluate_texture1(fragCoord), 0.5 + 0.5 * cos(val * PI * (2.0 + 6.0 * bass) + t * 5.0));
	COLOR = vec4(col, 1.0);
}
