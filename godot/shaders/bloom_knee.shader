shader_type canvas_item;
render_mode blend_disabled, unshaded;

// THE GLOW, AND WHY THE BOARD NEEDS ONE — pass one of three.
//
// The original paints its traces into a second canvas and composites that over
// the frame at the end. That is not an effect bolted on afterwards — it is how
// the board says which cells are LIVE. A trace is not merely a brighter blue
// than the lattice; it is a brighter blue that spills onto what is next to it,
// and the spill is most of what makes a schematic read as something powered
// rather than something printed.
//
// Three passes, at quarter resolution, which is where a blur of this radius
// wants to live anyway: everything below the knee is thrown away, the survivors
// are smeared, and the result is ADDED back. Quarter res costs a sixteenth of
// the samples and is invisible at this radius — a bloom is a low-frequency
// signal by construction, so resolving it finely is spending on detail the
// effect exists to destroy.
//
// GODOT SPELLS A BLIT AS A VIEWPORT, so the three passes are three small
// viewports rather than three RenderTexture.Blit calls. Same chain, same
// arithmetic; see Bloom, which builds it.
//
// THE KNEE. Keep what is brighter than the threshold, and keep it SOFTLY: a hard
// cut makes the bloom pop on and off as a cell's depth ramp crosses the line,
// which reads as flicker rather than as light.

uniform sampler2D src : hint_albedo;
uniform float threshold = 0.62;


void fragment() {
	vec4 c = texture(src, UV);
	float lum = max(c.r, max(c.g, c.b));
	float k = clamp((lum - threshold) / max(1e-4, 1.0 - threshold), 0.0, 1.0);
	COLOR = vec4(c.rgb * k * k, 1.0);
}
