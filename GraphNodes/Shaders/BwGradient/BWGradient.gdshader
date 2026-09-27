shader_type canvas_item;
uniform sampler2D textureOriginal;
uniform bool use_textureOriginal;
uniform sampler2D textureGradient : filter_nearest;
uniform bool use_textureGradient;

void vertex() {
	// Called for every vertex the material is visible on.
}

float rgb2greyscale(vec3 color)
{
	return ((color.r) + (color.g) + (color.b)) / 3.0;
}

void fragment() {
	vec4 color_source = texture(textureOriginal, UV);
	vec4 color_gradient = vec4(texture(textureGradient, vec2(rgb2greyscale(color_source.rgb),0.0)).rgb,color_source.a);
	// Called for every pixel the material is visible on.
	COLOR.rgba = use_textureOriginal ? (use_textureGradient ? color_gradient : color_source) : (use_textureGradient ? texture(textureGradient, UV) : vec4(1.0));
}
