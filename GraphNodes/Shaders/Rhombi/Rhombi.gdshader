shader_type canvas_item;
// Ported from rhombi.frag: a rhombus tiling whose angle morphs every 2 seconds, with a timed
// rotate/shift sequence. Contour bands around the tiles blend texture0 (default black) and
// texture1 (default white); Bass adds more bands.
// Rhombus SDF from https://www.iquilezles.org/www/articles/distfunctions2d/distfunctions2d.htm,
// tiling from https://www.shadertoy.com/view/tlGGDR.

uniform vec2 viewport_size = vec2(1920.0, 1080.0);
uniform sampler2D texture0 : filter_linear, repeat_disable;
uniform bool use_texture0;
uniform sampler2D texture1 : filter_linear, repeat_disable;
uniform bool use_texture1;
uniform float time = 0.0;
uniform float bass = 0.0;
uniform float scale = 12.0; // tiles across the width (was a constant)

const float SQRT_HALF = 0.70710678118;

vec2 CS(float a){ return vec2(cos(a), sin(a)); }

float ndot(vec2 a, vec2 b) { return a.x * b.x - a.y * b.y; }

float sdRhombus(in vec2 p, in vec2 b)
{
	vec2 q = abs(p);
	float h = clamp((-2.0 * ndot(q, b) + ndot(b, b)) / dot(b, b), -1.0, 1.0);
	float d = length(q - 0.5 * b * vec2(1.0 - h, 1.0 + h));
	return d * sign(q.x * b.y + q.y * b.x - b.x * b.y);
}

mat2 rot(float a){
	return mat2(vec2(cos(a), -sin(a)), vec2(sin(a), cos(a)));
}

float tesselatedRhombiPos(float theta1, vec2 p){
	vec2 p_ = p;
	p_ -= vec2(0.5, 0.5);
	p_ *= rot(-PI / 4.0);
	float d = sdRhombus(p_, CS(PI / 4.0));
	p_ = p;
	p_ -= vec2(0.5 + 0.5 * cos(theta1), 1.0 + 0.5 * sin(theta1));
	p_ *= rot(-theta1 / 2.0);
	d = min(d, sdRhombus(p_, CS(theta1 / 2.0)));
	p_ = p;
	p_ -= vec2(1.0 + 0.5 * sin(theta1), 0.5 - 0.5 * cos(theta1));
	p_ *= rot((PI - theta1) / 2.0);
	d = min(d, sdRhombus(p_, CS(theta1 / 2.0)));
	p_ = p;
	p_ -= vec2(1.0 + SQRT_HALF * cos(theta1 - PI / 4.0), 1.0 + SQRT_HALF * sin(theta1 - PI / 4.0));
	p_ *= rot(-theta1 - PI / 4.0);
	d = min(d, sdRhombus(p_, CS(PI / 4.0)));
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
	vec2 p_ = p * rot(-angle);
	p_ = opRep(p_, offset);
	float val = tesselatedRhombiPos(theta1, (p_ * rot(angle)));
	val = min(val, tesselatedRhombiPos(theta1, (p_ + vec2(offset.x, 0.0)) * rot(angle)));
	val = min(val, tesselatedRhombiPos(theta1, (p_ - vec2(offset.x, 0.0)) * rot(angle)));
	val = min(val, tesselatedRhombiPos(theta1, (p_ + vec2(0.0, offset.y)) * rot(angle)));
	val = min(val, tesselatedRhombiPos(theta1, (p_ - vec2(0.0, offset.y)) * rot(angle)));
	val = min(val, tesselatedRhombiPos(theta1, (p_ + vec2(offset.x, offset.y)) * rot(angle)));
	val = min(val, tesselatedRhombiPos(theta1, (p_ - vec2(offset.x, offset.y)) * rot(angle)));
	val = min(val, tesselatedRhombiPos(theta1, (p_ + vec2(-offset.y, offset.y)) * rot(angle)));
	val = min(val, tesselatedRhombiPos(theta1, (p_ - vec2(-offset.y, offset.y)) * rot(angle)));
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

// tex_coord is in GL space; flip to texture space
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
	float t = time;
	vec2 p = uv - vec2(0.5, 0.5);
	p *= viewport_size / viewport_size.x * scale;
	if (floor(mod(t / 2.0, 3.0)) == 1.0){
		p *= rot(mod(t, 2.0) * PI / 4.0);
	}
	if (floor(mod(t / 2.0, 5.0)) == 1.0){
		p = p + vec2(0.0, 1.0);
	}
	float theta1 = mod(t, 2.0) * PI / 4.0;
	float val = getTesselatedRhombusVal(p, theta1);
	vec3 col = mix(evaluate_texture0(fragCoord), evaluate_texture1(fragCoord), 0.5 - 0.5 * cos(val * PI * (2.0 + 8.0 * bass)));
	COLOR = vec4(col, 1.0);
}
