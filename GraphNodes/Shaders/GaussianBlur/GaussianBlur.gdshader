shader_type canvas_item;

// Radius that the shader was designed for.
const float DEFAULT_RADIUS = 10.000000;

// Unit vector: (1, 0) or (0, 1)
uniform float x_step = 0.0;
uniform float y_step = 0.0;
// Desired blur radius.
uniform float radius = 10.000000;
uniform sampler2D textureOriginal;
uniform bool use_textureOriginal;
uniform bool useAlpha = false;

void fragment() {
	vec2 step = vec2(x_step,y_step);
	vec2 s = radius / DEFAULT_RADIUS * step / vec2(textureSize(textureOriginal, 0));
	
	float offsets[] = {
		-10.000000000, -8.415809477, -6.435363708, -4.455121108, -2.475020813,
		-0.495000167, 1.485004498, 3.465057055, 5.445220765, 7.425557483, 9.406126897
	};
	
	float weights[] = {
		0.011194727, 0.039368696, 0.071308558, 0.110237219, 0.145451038,
		0.163798995, 0.157439298, 0.129158204, 0.090434685, 0.054043977, 0.027564604
	};
	
	COLOR.rgba = vec4(0.0);
	
	for (int i = 0; i < offsets.length(); i++) {
		vec4 sample = texture(textureOriginal, UV + offsets[i] * s);
		COLOR.rgba += weights[i] * vec4(sample.rgb * (useAlpha ? 1.0 : sample.a), (useAlpha ? 1.0 : sample.a));
	}
}