shader_type canvas_item;
// Ported from square_grayscale.frag: a halftone grid of squares sized by texture0's brightness.
// Inside the squares texture0 drifts vertically, outside horizontally; the result blends toward
// texture1 by brightness. The grid scale breathes over time; Bass Twist swirls each cell.
// (GL coordinates are bottom-left; UV is converted so the pattern keeps its orientation.)

uniform vec2 viewport_size = vec2(1920.0, 1080.0);
uniform sampler2D texture0 : filter_linear, repeat_disable;
uniform bool use_texture0;
uniform sampler2D texture1 : filter_linear, repeat_disable;
uniform bool use_texture1;
uniform float time = 0.0;
uniform float bass = 0.0;
uniform int bass_twist = 0; // was bass_set: the swirl only happens when enabled


vec2 cs(float angle){
	return vec2(cos(angle), sin(angle));
}

float sdBox(in vec2 p, in vec2 b)
{
	vec2 d = abs(p) - b;
	return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

vec2 posmod(vec2 d, vec2 r){
	return mod(mod(d, r) + r, r);
}

// Mirror-repeat in GL space, then flip to texture space
vec2 mirror_uv(vec2 gl_uv){
	vec2 p = posmod(gl_uv, vec2(2.0));
	p = vec2((p.x <= 1.0) ? p.x : 2.0 - p.x, (p.y <= 1.0) ? p.y : 2.0 - p.y);
	return vec2(p.x, 1.0 - p.y);
}

vec3 evaluate_texture0(vec2 gl_uv){
	return use_texture0 ? texture(texture0, mirror_uv(gl_uv)).rgb : vec3(0.0);
}

vec3 evaluate_texture1(vec2 gl_uv){
	return use_texture1 ? texture(texture1, mirror_uv(gl_uv)).rgb : vec3(0.0);
}

void fragment()
{
	vec2 fragCoord = vec2(UV.x, 1.0 - UV.y);
	vec2 uv = (fragCoord - vec2(0.5, 0.5)) * 2.0;
	uv = uv * viewport_size / viewport_size.yy;
	float scale = 21.0 + 20.0 * cos(time / 2.0);

	float grayscale = 0.5;
	if (use_texture0){
		grayscale = dot(evaluate_texture0(fragCoord), vec3(0.22, 0.71, 0.07));
	}
	vec2 uvmod = posmod(uv * scale, vec2(2.0, 2.0)) - vec2(1.0);
	if (bass_twist != 0){
		uvmod = length(uvmod) * cs(atan(uvmod.y, uvmod.x) + bass * PI + length(uv) / scale * 4.0 * PI);
	}
	float d = sdBox(uvmod, vec2(grayscale));
	vec3 col = (d < 0.0) ? evaluate_texture0(fragCoord + vec2(0.0, cos(time / 10.0))) : evaluate_texture0(fragCoord + vec2(sin(time / 9.0), 0.0));
	vec3 col1 = (d < 0.0) ? evaluate_texture1(fragCoord + vec2(0.0, cos(time / 11.0))) : evaluate_texture1(fragCoord + vec2(sin(time / 13.0), 0.0));
	col = mix(col, col1, grayscale);
	COLOR = vec4(col, 1.0);
}
