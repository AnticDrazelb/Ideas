shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disable, blend_mix;

// THE FOUR OBJECTS, drawn over the solid rather than fighting it in depth.
//
// A node, a lock, the core and the singularity itself are attached to CELLS, and
// the projection has already decided whether the cell they belong to is the
// surface of its column. If it is, the object is on screen; if it is not, the
// object is gone until you fold. That decision is made in the rules, so by the
// time a glyph is being drawn at all, the answer is yes — and it must not then
// be second-guessed by a depth test against a face it is sitting a hair inside.
//
// So the depth test is off, and visibility comes from the one place entitled to
// decide it.
//
// THE MARKER NEEDS BOTH BLENDS. The hole is OPAQUE — it has to remove the board
// under it, not tint it — while the photon ring and the lensing halo around it
// are light and must ADD. ShaderLab expresses that as two blend factors on one
// shader; Godot's blend mode is a render_mode, so it is two shaders instead, and
// CellShaders makes the second from the first with one line changed. Same
// picture, one definition.

uniform sampler2D tex : hint_albedo;
uniform vec4 tint : hint_color = vec4(1.0, 1.0, 1.0, 1.0);

varying vec2 v_uv;
varying vec4 v_col;


void vertex() {
	v_uv = UV;
	v_col = COLOR * tint;
}


void fragment() {
	vec4 c = texture(tex, v_uv) * v_col;
	ALBEDO = c.rgb;
	ALPHA = c.a;
}
