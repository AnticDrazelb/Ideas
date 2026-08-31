shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disable, blend_add, vertex_lighting;

// SPARKS, CHIPS, EMBERS AND FRONTS.
//
// One additive pass with vertex colour and no texture at all. The shape comes
// out of the quad's own UV: a chip is square because the mass that came off the
// board was square, and a spark is round because it is light. Two shapes, one
// draw call, nothing to load.
//
// Additive rather than alpha-blended, because everything this draws is EMISSION
// — grit catching the light off a circuit, a front going out across the lattice.
// Nothing here is a surface, so nothing here should occlude.
//
// THE EFFECTS LIVE IN FRONT OF THE BOARD and are never occluded by it — they are
// what the board just did. That is the depth test being off, and it is a rule
// rather than a convenience.
//
// BLEND IS SrcAlpha One, WHICH GODOT SPELLS blend_add PLUS THE MULTIPLY. The
// engine's additive mode is One One, so the source alpha is applied in the
// fragment instead — same picture, and the one place it is written down.

uniform float softness : hint_range(0.001, 0.5) = 0.18;
uniform vec4 tint : hint_color = vec4(1.0, 1.0, 1.0, 1.0);

varying vec2 v_uv;
varying vec2 v_kind;
varying vec4 v_col;


void vertex() {
	v_uv = UV;
	// x: 0 round, 1 square. y is unused, and the mesh writes it as zero.
	v_kind = UV2;
	v_col = COLOR * tint;
}


void fragment() {
	vec2 p = v_uv * 2.0 - 1.0;

	// round: distance from the centre. square: the Chebyshev distance, which is
	// the same maths with a max instead of a length.
	float d_round = length(p);
	float d_square = max(abs(p.x), abs(p.y));
	float d = mix(d_round, d_square, v_kind.x);

	float a = 1.0 - smoothstep(1.0 - softness, 1.0, d);
	ALBEDO = v_col.rgb * (v_col.a * a);
}
