shader_type canvas_item;
render_mode blend_disabled, unshaded;

// ADD IT BACK — pass three.
//
// Additive rather than screened, because the thing being composited is LIGHT:
// two traces crossing should be brighter than one, and a screen blend flattens
// exactly that.
//
// THE BOARD ARRIVES HERE AS A TEXTURE, which is the whole reason the world is
// rendered into a viewport: the interface draws over the top of this, and the
// glow belongs to the board rather than to the instrument it is mounted in. The
// C# gets that for free because a Unity screen-space canvas draws after the
// camera's post pass; here it has to be arranged, and this is the arrangement.

uniform sampler2D board : hint_albedo;
uniform sampler2D bloom : hint_albedo;
uniform float intensity = 1.15;
uniform float amount : hint_range(0, 1) = 1.0;


void fragment() {
	vec4 c = texture(board, UV);
	vec4 b = texture(bloom, UV);
	COLOR = vec4(c.rgb + b.rgb * intensity * amount, 1.0);
}
