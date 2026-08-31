shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;

// THE BOARD, AS A SOLID.
//
// The web original draws two paths that agree exactly at rest: a flat grid of
// rects when the cube is square to the camera, and an honest 3D solid while it
// is folding. Here there is only ever the solid — because with an orthographic
// camera looking straight down one axis, the depth buffer performs the collapse
// for us. The nearest solid cell in a column is the one that gets drawn, which
// is precisely the rule the solver plays by. The renderer and the rules cannot
// drift apart, because they are the same statement.
//
// TWO RAMPS THAT MUST NEVER MEET. Depth is drawn as brightness, and material is
// drawn as brightness too, so the two jobs are given non-overlapping ranges: a
// near lump of lattice must never be brighter than a far trace, or "may I stand
// there" stops being readable. The ranges live in Palette and arrive here as
// col_far / col_near.
//
// CULL IS OFF, deliberately. The cube is a closed opaque solid, so the depth
// buffer already picks the nearest face and backface culling would buy nothing
// but a chance to get the winding wrong. Plates are cut through the lattice and
// genuinely do want both of their faces.
//
// ONE FILE, NOT TWO PASSES. ShaderLab needs the vertex stage in an include
// because a Pass is its own compilation unit; Godot 3.5 has one vertex stage per
// shader and the schematic is a separate material on separate geometry, so the
// same arithmetic is written once here and once in wire.shader. They must stay
// the same statement — a wire that did not travel with the cell it belongs to
// would come off it the moment anything moved.

uniform vec4 col_far : hint_color = vec4(0.04, 0.05, 0.07, 1.0);
uniform vec4 col_near : hint_color = vec4(0.22, 0.28, 0.36, 1.0);
uniform float half_span = 1.0;
uniform float facing_dark : hint_range(0, 1) = 0.42;
uniform float edge_inset : hint_range(0, 0.5) = 0.06;
uniform float edge_lift : hint_range(0, 2) = 0.55;
uniform float peek : hint_range(0, 1) = 0.0;
uniform vec4 wire_col : hint_color = vec4(0.62, 0.91, 1.0, 1.0);
uniform float gutter : hint_range(0, 0.3) = 0.055;
uniform float round_rad : hint_range(0, 0.5) = 0.09;
uniform float dim = 1.0;

// THE WHOLE BOARD, TOGETHER. Not `dim` — that is the hold shading the far cells
// to say which of them is nearer, and it means something. This one means the
// board is going away, so it is the last multiply in the fragment stage and
// nothing is allowed to be exempt from it.
uniform float all_amt : hint_range(0, 1) = 1.0;

uniform float reveal = 99.0;
uniform float wave = 99.0;
uniform float wave_dir = 1.0;
uniform float wave_soft = 1.6;

uniform float evert : hint_range(0, 1) = 0.0;
uniform vec3 evert_axis = vec3(0.0, 0.0, 1.0);
uniform vec3 evert_spin = vec3(1.0, 0.0, 0.0);
uniform float evert_spread : hint_range(0, 1) = 0.75;

uniform float burst : hint_range(0, 1) = 0.0;
uniform float burst_throw = 3.4;
uniform float burst_spread : hint_range(0, 1) = 0.55;
uniform float burst_spin = 1.35;

uniform float unlit : hint_range(0, 1) = 0.46;
uniform float unlit_sat : hint_range(0, 1) = 0.30;

// THE CELL CENTRE'S THIRD COMPONENT, which has nowhere else to ride: Godot's
// mesh arrays carry a two-component UV2 and the centre is a point. The mesh
// writes x and y into UV2 and this rebuilds z from the cell's own place in the
// volume, which the vertex already knows — see CubeMesh.build.
varying float v_depth01;
varying float v_gone;
varying vec3 v_nrm_view;
varying vec4 v_reach;
varying vec2 v_uv;


// Rodrigues. Both of the things that turn a cell about its own centre want it,
// and both of them have to turn the NORMAL by the same amount — see the note
// where the eversion uses it.
vec3 turn(vec3 v, vec3 axis, float ang) {
	float cs = cos(ang);
	float sn = sin(ang);
	return v * cs + cross(axis, v) * sn + axis * dot(axis, v) * (1.0 - cs);
}


void vertex() {
	// The cell centre: x and y off UV2, z recovered from the vertex itself.
	// A face's four corners are all within half a cell of their centre and the
	// centre is on the half-integer grid, so rounding the vertex to that grid is
	// exact for every face of every cell.
	vec3 centre = vec3(UV2.x, UV2.y, floor(VERTEX.z + 0.5));

	// THE CUBE ARRIVES AND LEAVES ONE CELL AT A TIME.
	//
	// A board that appears all at once is a slide; a board that builds outward
	// from where the player will be standing is the machine assembling itself,
	// and it costs one lerp. Each cell shrinks to its own centre until the front
	// reaches it, which is why the vertex is interpolated toward the centre
	// rather than scaled about the origin — scaling about the origin would slide
	// every cell across the board instead of letting each one grow where it
	// belongs.
	float wave_d = COLOR.b * 255.0;
	float w = wave_dir > 0.0
			? clamp((wave - wave_d) / wave_soft, 0.0, 1.0)
			: clamp((wave_d - wave) / wave_soft, 0.0, 1.0);
	vec3 local = mix(centre, VERTEX, w * w * (3.0 - 2.0 * w));

	// ---- EVERSION, ONE CELL AT A TIME ----------------------------------
	//
	// The rule is that each column shows its FAR cell instead of its near one,
	// so the honest end state is every cell mirrored along the camera axis.
	// Doing that to the whole solid at once is one line and reads as the object
	// being squashed through a plane.
	//
	// Doing it PER CELL reads as what it is. Each cell travels to its own mirror
	// position and turns a half-circle about its own centre on the way, and the
	// cells do not leave together: the stagger is keyed on depth, so the turn
	// sweeps through the machine from the face you are looking at to the one you
	// are about to be looking at. Every square on screen is a card flipping
	// over, and what is on the back of it is the cell that was hiding behind it
	// — which is not a metaphor for the rule, it is the rule.
	//
	// A half-circle about a face axis maps a cube onto itself, so the settled
	// board is exactly the mirrored board and nothing needs to be undone at the
	// end.
	vec3 e_centre = centre;
	vec3 nrm = NORMAL;
	if (evert > 1e-5) {
		vec3 ax = normalize(evert_axis);
		float along = dot(centre, ax);

		// where this cell sits along the view axis, 0 far .. 1 near — and
		// therefore when its turn starts. 2 * half_span is the full span of cell
		// centres, the same divisor the depth ramp below uses; four would
		// squeeze every cell into the middle quarter of the sweep and the
		// stagger would be invisible.
		float t = clamp(along / (2.0 * half_span) + 0.5, 0.0, 1.0);

		// the whole sweep still finishes at evert = 1 however wide the stagger
		// is: the front is scaled up by the spread and each cell subtracts its
		// own share
		float k = clamp(evert * (1.0 + evert_spread) - t * evert_spread, 0.0, 1.0);
		float e = k * k * (3.0 - 2.0 * k);

		e_centre = centre - 2.0 * along * ax * e;

		// Rodrigues, about the cell's own centre. The spin axis is the camera's
		// right in object space, so every cell turns the same way on screen no
		// matter which way the engine is folded.
		//
		// THE NORMAL TURNS WITH IT. A half-circle maps the face axes onto their
		// opposites, so a cell that has finished its flip is showing the face
		// whose AUTHORED normal points away from the camera — and the fragment
		// stage reads that normal to decide whether a face is a back face, which
		// it then discards. The settled everted board survived that only because
		// culling is off and the far face of the same cell was standing in for
		// it. That is a coincidence, not a rendering.
		vec3 sp = normalize(evert_spin);
		vec3 rel = local - centre;
		float ang = 3.14159265 * e;
		rel = turn(rel, sp, ang);
		nrm = turn(nrm, sp, ang);

		local = e_centre + rel;
	}

	// ---- THE BOARD LEAVES IN PIECES ------------------------------------
	//
	// The machine is a solid made of cells and the win is the moment it stops
	// being one. Every cell is thrown straight out from the core — the thing
	// that just took you — tumbling about an axis of its own, and the throw is
	// staggered by DISTANCE FROM THE CORE so the break travels outward as a
	// front rather than happening to the whole object at once.
	//
	// It is the eversion's machinery pointed somewhere else, which is the
	// argument for having built it per-cell in the first place: the board was
	// already made of individually addressable cubes, so the explosion is four
	// uniforms and nothing on the CPU.
	//
	// THE SCHEDULE, at the shipped stagger of 0.55. The cell on the core leaves
	// immediately and the eight corners hold until the throw is 35% done;
	// halfway through, the corners are 53% of the way out and the middle is 96%,
	// which is the front. Every cell lands exactly at burst = 1 whatever the
	// spread, because the front is scaled up by it and each cell subtracts its
	// own share — the same arithmetic the eversion above uses.
	//
	// The direction comes off the AUTHORED centre even when the board is everted
	// and the cell is standing at its mirror. A mirror along the view axis
	// changes only the sign of the component the orthographic camera cannot see,
	// so on screen the two answers are the same throw — and the cell is still
	// the cell it was authored as, which is the answer that means something.
	float gone = 0.0;
	if (burst > 1e-5) {
		float far_d = max(1e-4, 1.7320508 * half_span);   // centre to corner
		float r = clamp(length(centre) / far_d, 0.0, 1.0);
		float k = clamp(burst * (1.0 + burst_spread) - r * burst_spread, 0.0, 1.0);

		// FAST OUT, THEN COASTING — the opposite ease to everything else in this
		// game, because this is the only thing that is thrown rather than moved.
		//
		// SQUARED, NOT CUBED. At the third power a cell was two thirds of the
		// way out a third of the way through its own throw, which reads as a
		// thing that has already happened rather than a thing happening.
		float u = 1.0 - k;
		float e = 1.0 - u * u;

		// A cell sitting on the core has no direction of its own, so it takes
		// the one that is always true: it comes at you.
		float len = length(centre);
		vec3 dir = mix(vec3(0.0, 0.0, -1.0), centre / max(1e-4, len),
				clamp(len / max(1e-4, 0.5 * half_span), 0.0, 1.0));

		// an axis that differs per cell, so the debris is a cloud of separate
		// objects rather than a set of parallel cards
		vec3 tum = normalize(centre * vec3(0.71, -1.03, 0.44) + vec3(0.31, 0.17, 0.93));
		vec3 rel = local - e_centre;
		float ang = 6.2831853 * burst_spin * e;
		rel = turn(rel, tum, ang);
		nrm = turn(nrm, tum, ang);

		e_centre += dir * (burst_throw * half_span * e);
		local = e_centre + rel;
		gone = k;
	}
	v_gone = gone;

	VERTEX = local;
	NORMAL = nrm;

	// Depth is measured from the CELL CENTRE, not the vertex, so that at rest a
	// front face reads exactly d/(N-1) — the same number the flat 2D original
	// prints — rather than half a step brighter. Measured against the cube's own
	// centre so it is independent of how far away the camera happens to sit.
	//
	// The ANIMATED centre, not the authored one: depth is drawn as brightness,
	// so a cell reading its old depth while it travels would keep the near ramp
	// all the way to the far side.
	float centre_z = (MODELVIEW_MATRIX * vec4(e_centre, 1.0)).z;
	float origin_z = (MODELVIEW_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).z;
	// View space looks down -Z, so a LARGER z is nearer.
	v_depth01 = clamp((centre_z - origin_z) / (2.0 * half_span) + 0.5, 0.0, 1.0);

	v_nrm_view = normalize((MODELVIEW_MATRIX * vec4(nrm, 0.0)).xyz);
	v_uv = UV;
	v_reach = COLOR;
}


void fragment() {
	vec3 c = mix(col_far.rgb, col_near.rgb, v_depth01);

	// A face turned away from the camera is darker, so that mid-fold the thing
	// reads as a solid being turned rather than as a set of squares sliding over
	// each other. At rest every visible face is dead-on and this term is exactly
	// 1, which is what keeps the settled board identical to the flat original.
	float facing = clamp(dot(v_nrm_view, vec3(0.0, 0.0, 1.0)), 0.0, 1.0);
	c *= mix(1.0 - facing_dark, 1.0, facing);

	// --r-cell IS FOUR CSS PIXELS, which on this canvas is eight units and on a
	// mid-sized cube about nine hundredths of a cell. This was 0.16 — nearly
	// twice as round — which is what turned a rack of circuit tiles into a tray
	// of lozenges.
	//
	// A CELL IS A TILE, NOT A SHARE OF THE SURFACE. Every cell is drawn as a
	// rounded plate with a dark gutter around it, so a run of adjacent traces
	// reads as separate things you could stand on one at a time rather than as
	// one lit region. That is the whole difference between a schematic and a
	// heat map, and it is the shape the eye uses to count a route before the
	// hand moves.
	//
	// The gutter is DARKENED rather than discarded. Cutting the corners away
	// would let whatever is behind show through a hole inside a cell, and this
	// game's one rule is that a column shows exactly its nearest solid cell — a
	// gap the rules do not know about would be the renderer disagreeing with the
	// solver about what is visible.
	//
	// A TILE ROUNDS OFF; A WIRE DOES NOT. Under a held MATRIX neither the corner
	// radius nor the rim inset is true any more — the board has stopped being a
	// floor and become a drawing of the machine — so the corners go square and
	// the rim narrows to a hairline as the hold comes on.
	float rnd = mix(round_rad, 0.004, peek);
	float edg = mix(edge_inset, 0.020, peek);

	vec2 p = abs(v_uv - 0.5);
	vec2 q = p - (0.5 - gutter - rnd);
	float sd = length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - rnd;

	float plate = 1.0 - smoothstep(-0.010, 0.010, sd);
	float rim = (1.0 - smoothstep(0.0, edg, -sd)) * plate;
	c *= 1.0 + rim * edge_lift * facing;
	c *= mix(0.09, 1.0, plate);

	// THE LATTICE GOES TO GLASS.
	//
	// Under a held MATRIX the solid does not vanish, it THINS — and the edges
	// come up as it goes, so what is left at the end of the lean is a lit
	// wireframe with a film of material still stretched across it. Those are two
	// separate movements and doing only the first gives you a dim cube rather
	// than a glass one.
	//
	// IT GOES TO SIX PER CENT, not twenty-two. Twenty-two per cent of a fill is
	// still a surface, and a surface is the one thing a schematic must not have:
	// it is what stops the far side reading as far and turns a lattice of wire
	// boxes back into a stack of dim tiles. What is left is the wire.
	c *= 1.0 - 0.94 * peek;

	// AND THE FAR SIDE IS WIRE ONLY, WHEN IT IS THERE AT ALL.
	//
	// A translucent fill behind a translucent fill behind a translucent fill is
	// mud, and mud is the opposite of what a look-inside is for. So the back of
	// the solid is not drawn until the lean has actually started, and when it
	// arrives it arrives as edges.
	//
	// Keyed off the FACING term rather than off cull state on purpose. The
	// cube's own Z is flipped to convert the rules' right-handed basis into the
	// engine's, which inverts winding — so "back face" and "clockwise" have come
	// apart in this mesh, and only one of the two still means what it says.
	//
	// ... and never while the board is coming apart. A tumbling cell presents
	// its back to the camera for half of every turn, and discarding that would
	// put a hole straight through the middle of a piece of debris.
	float back = step(facing, 0.01);
	if (back > 0.5 && peek < 0.12 && burst < 1e-5) {
		discard;
	}

	c *= 1.0 - back * peek;

	// AND THE WIRE IS ITS OWN COLOUR. This added col_near, which is the near end
	// of the DEPTH ramp — so the brightest thing in the schematic was whatever
	// the board happened to be tinted by the vault it was cut from. The wire is
	// a different statement from the surface and it says so: see
	// Palette.WIRE_TRACE.
	c += wire_col.rgb * rim * peek * mix(1.30, 0.44, back);

	// THE REVEAL. After every fold the lit reachable set sweeps outward from the
	// player in BFS order rather than snapping on. It is the prettiest thing on
	// screen and it is also the most useful: the sweep IS the connectivity graph
	// being traced, so the player watches the answer to "what did that fold buy
	// me" get drawn one cell at a time. Juice and teaching, the same effect.
	float dist = v_reach.g * 255.0;
	float arrived = step(dist, reveal);
	float lit = v_reach.r * arrived;

	// TWO READS WERE SHARING ONE CHANNEL.
	//
	// Depth is brightness. Material is brightness. And "can I stand there" — the
	// question the player asks after every single fold, and the only one that
	// changes what they do next — was ALSO brightness: a flat dim to 46%. Three
	// answers in one number, on a board whose whole argument is that the number
	// means distance.
	//
	// So unreachable now DRAINS as well as darkens. Value keeps saying how far
	// away a cell is; saturation says whether you can get to it. They are
	// independent channels and the eye reads them independently, which is the
	// entire point. It costs nothing that matters: the dim is unchanged, so
	// every contrast pair the audit measures is exactly where it was — the grey
	// a drained cell moves toward is its OWN luminance.
	float lum = dot(c, vec3(0.2126, 0.7152, 0.0722));
	c = mix(mix(vec3(lum), c, unlit_sat), c, lit);
	c *= mix(unlit, 1.0, lit);

	// AND THE DEBRIS GOES OUT, LATE. It keeps its full brightness for most of
	// the throw — a piece that starts fading the instant it leaves never reads
	// as a piece — and is gone by the time the card arrives. On a void
	// background, fading to black and fading to transparent are the same
	// picture, which is what lets the whole exit stay an opaque pass with the
	// depth buffer still doing its one job.
	c *= 1.0 - smoothstep(0.55, 1.0, v_gone);

	ALBEDO = c * dim * all_amt;
}
