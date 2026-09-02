shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never, depth_test_disable;

// THE MACHINE AS A DRAWING.
//
// A held MATRIX used to THIN the board: the fill went translucent and the far
// side of each cell came up as edges. What it could not do was show you what was
// BEHIND — the depth buffer performs the projection, so every cell in a column
// but the nearest was gone before any fragment shader had an opinion — and it
// could not draw a cell that had no faces. CubeMesh.build meshes away every face
// with something next to it, which is right, and it means a cell buried in the
// lattice has no vertices at all and a VOID has none by definition.
//
// So the schematic is its own geometry: one wire box per cell for every cell in
// the volume, empty ones included, drawn additively with no depth test at all.
// See CubeMesh.build_wire for why the space matters as much as the material.
//
// IT SHARES THE SURFACE'S VERTEX STAGE, and it has to: the arrival wave, the
// eversion and the exit burst all move cells individually, and a wire that did
// not travel with the cell it belongs to would come off it the moment anything
// moved. ShaderLab puts that shared stage in an include; Godot 3.5 has no
// includes, so the two files carry the same vertex() and CellShaders proves at
// load time that they still do — a copy nobody checks is a copy that drifts.
//
// THE PROJECTION IS NOT BEING BROKEN. This draws nothing but lines, and a line
// is not a surface. cell.shader still decides what the board IS and still obeys
// the depth buffer to the letter; this only says where the machine has material.
// That is the question a held MATRIX is asking, and the only one it can answer
// without lying about the rule the whole game is built on.

// The three classes, which are the three answers: nothing here, something here,
// something here you can stand on.
uniform vec4 col_grid : hint_color = vec4(0.50, 0.60, 0.85, 1.0);
uniform vec4 col_lattice : hint_color = vec4(0.50, 0.60, 0.85, 1.0);
uniform vec4 col_trace : hint_color = vec4(0.62, 0.91, 1.00, 1.0);

uniform float gain_grid : hint_range(0, 1) = 0.045;
uniform float gain_lattice : hint_range(0, 1) = 0.14;
uniform float gain_trace : hint_range(0, 1) = 0.40;

uniform float wire_far : hint_range(0, 1) = 0.26;

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
	// A DIRECTION, NOT TWO POSITIONS — and that is a bug fix, not a tidy-up.
	//
	// This asked view space where the cell centre is, asked it where the object's
	// origin is, and subtracted. Both answers are about FORTY, because that is
	// where the camera stands, and the signal buried in them is the two and a
	// half units of half-span. Sixteen bits of mantissa carries that; ten does
	// not — and a GLES2 driver is free to give the vertex stage ten. On one it
	// resolves the cube into sixteen shades of depth and on another it resolves
	// it into one, and every cell comes out at the NEAR end of the ramp: a solid
	// board of mid-grey plates where there should be a lit route through a dark
	// lattice. Depth is brightness in this game, so losing depth loses the only
	// channel that says which cells are floor.
	//
	// With w = 0 the translation column is skipped, so this is the cell's offset
	// along the view axis and nothing else. R*c + t minus t IS R*c; the two lines
	// were computing a difference the matrix could have been asked for directly,
	// and the forty never enters the arithmetic at all.
	//
	// View space looks down -Z, so a LARGER z is nearer.
	float centre_z = (MODELVIEW_MATRIX * vec4(e_centre, 0.0)).z;
	v_depth01 = clamp(centre_z / (2.0 * half_span) + 0.5, 0.0, 1.0);

	v_nrm_view = normalize((MODELVIEW_MATRIX * vec4(nrm, 0.0)).xyz);
	v_uv = UV;
	v_reach = COLOR;
}


void fragment() {
	if (peek < 0.02) {
		discard;
	}

	// The class rode in on ALPHA, because the other three channels are rewritten
	// by the reach pass on every settle and this one is a property of the cube
	// rather than of the moment.
	float cls = v_reach.a;

	vec3 col = cls < 0.25 ? col_grid.rgb : (cls < 0.75 ? col_lattice.rgb : col_trace.rgb);
	float gain = cls < 0.25 ? gain_grid : (cls < 0.75 ? gain_lattice : gain_trace);

	// FAR IS FAINT. Without this every wire in the solid arrives at the same
	// strength and the picture has no inside — the eye is handed a flat tangle
	// instead of a depth. It is the same ramp the surface uses, doing the same
	// job with light instead of value.
	float fade = mix(wire_far, 1.0, v_depth01);

	// If the board is thrown while somebody is holding, the wire leaves with the
	// cell it is drawn on.
	float leaving = 1.0 - smoothstep(0.55, 1.0, v_gone);

	// NOT `dim`. That is the surface being taken away as the hold comes on, and
	// applying it here would dim the wire by exactly the amount the wire exists
	// to replace. `all_amt` is a different claim — the whole board leaving — and
	// the schematic is part of the board.
	ALBEDO = col * (gain * fade * peek * leaving * all_amt);
}
