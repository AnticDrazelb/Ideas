shader_type canvas_item;
render_mode blend_disabled, unshaded;

// ONE AXIS OF A SEPARABLE BLUR — pass two, run twice.
//
// Nine taps on the five-tap linear sampling trick: the hardware's bilinear
// filter fetches two texels per sample, so this is a nine-wide kernel for five
// reads.

uniform sampler2D src : hint_albedo;
uniform vec2 dir = vec2(1.0, 0.0);
uniform vec2 texel = vec2(1.0 / 256.0, 1.0 / 256.0);


void fragment() {
	vec2 t = texel * dir;
	vec4 c = texture(src, UV) * 0.227027;
	c += texture(src, UV + t * 1.3846153) * 0.3162162;
	c += texture(src, UV - t * 1.3846153) * 0.3162162;
	c += texture(src, UV + t * 3.2307692) * 0.0702702;
	c += texture(src, UV - t * 3.2307692) * 0.0702702;
	COLOR = c;
}
