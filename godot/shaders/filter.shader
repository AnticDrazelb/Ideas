shader_type canvas_item;
render_mode blend_disabled, unshaded;

// A DARK GAME ON A BRIGHT DAY IS AN UNREADABLE GAME.
//
// The phone's own brightness slider moves the whole system, not this — and a
// puzzle whose entire readout is "is this cell brighter than that one" is
// exactly the kind of thing that stops working in sunlight while every other app
// on the device is fine.
//
// Both controls default to 100, and AT 100 NO FILTER IS APPLIED AT ALL: the
// layer is hidden entirely, so the cost of these two settings is zero for anyone
// who leaves them alone.
//
// IT IS ONE SHADER HERE AND A STACK OF BLENDED QUADS IN THE C#, and the
// difference is worth stating because the C# version cost two real bugs.
//
// Unity's interface is a screen-space overlay drawn after the camera's post
// pass, so a camera blit cannot reach it — and the filter has to reach it, or
// the board dims and the readout over it does not. The answer there was to
// perform `out = in*b*c + 0.5*(1 - c)` in BLEND MODES, across a stack of overlay
// quads. It shipped once with the decomposition wrong — a gain of c and an
// offset of (b - 1), which turns the brightness control from a multiply into an
// addition and collapses every dark value in the palette into the same white —
// and once with `Blend DstColor Zero` written as a literal 4, which is
// OneMinusDstColor, so every quad inverted the screen and the rust interface
// came out blue the moment either slider left 100.
//
// A canvas layer above everything, reading the screen, has neither problem: the
// formula is written once, in the order it is written down, and the numbers are
// the numbers.

uniform float brightness = 1.0;
uniform float contrast = 1.0;


void fragment() {
	vec4 c = texture(SCREEN_TEXTURE, SCREEN_UV);

	c.rgb *= brightness;

	// Contrast about mid grey. Doing it about black would just be brightness
	// again, and doing it in gamma space is the right call here: the whole
	// palette is authored as hex values somebody picked by eye, so the
	// adjustment should move them the way the eye expects rather than the way
	// the physics does.
	c.rgb = (c.rgb - vec3(0.5)) * contrast + vec3(0.5);

	COLOR = vec4(clamp(c.rgb, vec3(0.0), vec3(1.0)), 1.0);
}
