shader_type canvas_item;
render_mode blend_disabled, unshaded;

// DEEP SKY, BEHIND THE MACHINE.
//
// The camera used to clear to black and the comment on it said why: a void
// column is unrendered space, and anything painted there is the display claiming
// to have data it does not have. This is the deliberate reversal of that, and it
// keeps the half of the argument that was load-bearing.
//
// WHAT IS PAINTED HERE MUST NEVER READ AS BOARD DATA, and two properties are
// what make that true rather than hoped for:
//
//   A STAR IS A POINT. Cells are the largest objects on the screen — a hundred
//   and ninety pixels across on a phone — and a star here is under four, stated
//   in pixels so it is the same star on every display. Nothing at that size is
//   ever mistaken for a cell, however bright it is, which is why the stars are
//   allowed to be bright: a dim star is not a subtle star, it is a smudge.
//
//   NOTHING ELSE IS. The field is otherwise black. The nebula is the one area
//   fill and its peak is held below the dimmest cell this game draws, so the
//   depth ramp — brightness IS distance — keeps its whole range against a floor
//   that has not moved.
//
// IT IS A CANVAS ITEM ON THE LAYER BELOW THE BOARD, where the C# draws a clip-
// space quad in the Background queue. Same picture and the same argument: the
// camera's orthographic size moves constantly — the room pulls out on a fold,
// the close drops to a sixth on a win, punch and shake ride on top — and a quad
// sized in world units would have to chase all of it. A 2D layer under the
// viewport ignores the transform entirely, so there is nothing to chase.
//
// THE ONLY MOTION IS THE PARALLAX, and it comes in as `drift` from Tilt, which
// is scaled by the motion setting before it ever gets here. Nothing twinkles. A
// background that animates on its own is a background competing with the board.

uniform vec2 drift = vec2(0.0, 0.0);
uniform float peak : hint_range(0, 1) = 0.85;
uniform float nebula_peak : hint_range(0, 0.2) = 0.055;
uniform float density : hint_range(0, 1) = 0.10;
uniform float star_px : hint_range(0.4, 4) = 1.7;
uniform float fade : hint_range(0, 1) = 1.0;
uniform vec4 tint : hint_color = vec4(0.13, 0.42, 0.62, 1.0);
uniform vec2 screen_size = vec2(1080.0, 1920.0);


float hash21(vec2 p) {
	p = fract(p * vec2(127.31, 311.7));
	p += vec2(dot(p, p + 47.53));
	return fract(p.x * p.y);
}


// ONE SHELL OF SKY. The plane is cut into cells, at most one star to a cell, its
// position and brightness both drawn from the cell's own hash — so the field is
// fixed in space and reproducible, and moving through it is a change of offset
// rather than a new random field.
float shell(vec2 uv, float scale, float dens, float px) {
	vec2 g = uv * scale;
	vec2 id = floor(g);
	float h = hash21(id);
	if (h > dens) {
		return 0.0;
	}

	// re-hash the survivor so position, size and brightness are not three
	// readings of one number
	vec2 jit = vec2(fract(h * 731.0), fract(h * 1097.0)) - 0.5;
	float d = length(fract(g) - 0.5 - jit * 0.72);

	// A STAR IS STATED IN PIXELS AND STAYS THERE. `px` arrives as this shell's
	// own cell size for one screen pixel, so the radius means the same thing on
	// a 1080 phone and a 1440 one — and it is a number Sky owns and holds
	// against the size of a cell, which is what keeps a star a point rather than
	// an object.
	float r = px * (0.6 + 0.4 * fract(h * 197.0));
	float core = 1.0 - smoothstep(0.0, r, d);
	return core * (0.28 + 0.72 * fract(h * 419.0));
}


// A very low frequency wash, and the only area fill in the file.
float nebula(vec2 uv) {
	vec2 g = uv * 1.7;
	vec2 i = floor(g);
	vec2 f = fract(g);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash21(i);
	float b = hash21(i + vec2(1.0, 0.0));
	float c = hash21(i + vec2(0.0, 1.0));
	float d = hash21(i + vec2(1.0, 1.0));
	float n = mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
	// squared, so most of the sky is empty and the wash is in patches
	return n * n;
}


void fragment() {
	// UV runs 0..1 across the layer; the C# samples a -1..1 quad, and the star
	// field is scale-invariant, so the only thing that has to match is the
	// aspect correction and the pixel size below.
	vec2 ndc = UV * 2.0 - 1.0;

	// square the field up, so a star is round on a 20:9 phone
	float aspect = screen_size.x / max(1.0, screen_size.y);
	vec2 uv = vec2(ndc.x * aspect, ndc.y);

	// one pixel, in the units uv is measured in
	float px = 2.0 / max(1.0, screen_size.y);

	// THREE SHELLS, AND THE NEAR ONE MOVES MOST. That is the whole of the depth:
	// the field has no perspective and needs none, because parallax IS the cue.
	float s = shell(uv + drift * 1.00, 26.0, density, star_px * px * 26.0)
			+ shell(uv + drift * 0.55, 44.0, density * 0.85, star_px * px * 44.0 * 0.78)
			+ shell(uv + drift * 0.28, 72.0, density * 0.70, star_px * px * 72.0 * 0.62);

	float neb = nebula(uv + drift * 0.18) * nebula_peak;

	vec3 col = tint.rgb * neb + clamp(s, 0.0, 1.0) * peak;
	COLOR = vec4(col * fade, 1.0);
}
