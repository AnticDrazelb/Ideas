class_name CubeView
extends Spatial
# THE CUBE ON SCREEN: one mesh, three materials, and a rotation.
#
# Everything the web build had to draw by hand — which cell wins a column, how
# bright it is for its depth, what a fold looks like halfway through — falls out
# of putting the real solid in front of an orthographic camera that is looking
# straight down an axis. The depth buffer performs the collapse, the shader
# performs the ramp, and the transform performs the fold.

const CELL_PATH := "res://shaders/cell.shader"
const WIRE_PATH := "res://shaders/wire.shader"
const FX_PATH := "res://shaders/fx.shader"

var cube: Spatial
var mesh_inst: MeshInstance

var _mat_trace: ShaderMaterial
var _mat_lattice: ShaderMaterial
var _mat_plate: ShaderMaterial

# the schematic: its own geometry, its own material, off unless held
var _wire: MeshInstance
var _mat_wire: ShaderMaterial
var _wire_cols := PoolColorArray()

var _s: Session = null
var _built_world := -1
var _built_cells := -1

var _vertex_cols := PoolColorArray()
var _mesh: ArrayMesh
var _wire_mesh: ArrayMesh
var _cell_of_vertex := []
var _wire_cell_of_vertex := []
var _wire_class := []

# THE REVEAL FRONT, in BFS steps. It runs out from the player after every settled
# change, so the lit reachable set is DRAWN rather than switched on — and the
# sweep is the connectivity graph being traced one cell at a time.
var _reveal := 99.0
const REVEAL_PER_SECOND := 26.0

# The transition front, in cells, and which way it runs.
var _wave := 99.0
var _wave_dir := 1.0
var _was_turning := false
var _wave_focus := Vector3.ZERO
const WAVE_PER_SECOND := 22.0

# the cage, and the two reads that change nothing
var _cage: MeshInstance
var _cage_mat: ShaderMaterial
var _near_pips := []
var _through: GlyphQuad
var _near := []

# markers
var _marker_root: Spatial
var _markers := []

# ---- live view state (driven by the session and the input router) ----------
var peek_yaw := 0.0
var peek_pitch := 0.0
var peek_amt := 0.0

# ---- the attract pose ------------------------------------------------------
#
# A POSE, NOT A TUMBLE, AND THE SIZE IS THE ARGUMENT.
#
# This used to be a continuous yaw — a full revolution every twenty-one seconds —
# with the pitch rocking through twenty-two degrees under it. A solid turning
# through every angle presents its DIAGONAL at some point in the cycle, so it has
# to be framed for root three of its own width, and the front screen has one band
# to give it: whatever the masthead and the controls leave, which is about a
# third of the plate. Framed for the worst angle, the cube was drawn at little
# more than half the size the band could hold, on the one screen where it is the
# subject.
#
# Bounded, the worst silhouette is root two and it comes at forty-five degrees,
# which is inside the swing. That is the whole difference: same band, same
# containment argument, a cube half again as big — and a composed three-quarter
# view rather than whatever angle the clock happened to stop at, which is the
# difference between a product shot and a screensaver. It still moves; it
# breathes rather than spins.

# The three-quarter view the machine is presented in.
const ATTRACT_YAW := 34.0
const ATTRACT_PITCH := 23.0

# How far either angle drifts either side of it, and how slowly.
const ATTRACT_YAW_SWING := 15.0
const ATTRACT_PITCH_SWING := 6.0
const ATTRACT_YAW_HZ := 0.055
const ATTRACT_PITCH_HZ := 0.083

var attract := false
var _attract_t := 0.0

const PEEK_YAW := 0.62
const PEEK_PITCH := 0.42
const PEEK_MAX_PITCH := 1.15

# ---- eversion --------------------------------------------------------------
#
# THE RENDERER AND THE RULES HAVE TO STAY THE SAME STATEMENT. That is the claim
# at the top of cell.shader and it is the reason the board is drawn as a solid at
# all: an orthographic camera down one axis makes the DEPTH BUFFER perform the
# projection, so the nearest cell in a column is the one that gets drawn, which
# is precisely the rule the solver plays by.
#
# Everting reverses that rule — each column shows its FAR side — and the renderer
# reverses with it, per cell, in the vertex stage. NO GLOBAL FLIP: the mesh never
# changes for an eversion, because a polarity changes no cell's TYPE, only which
# one wins its column, so the whole effect is three uniforms and nothing on the
# CPU moves at all.
var evert_amt := 0.0
var _evert_target := 0.0

# How long the solid takes to turn through itself, in seconds.
const EVERT_SECONDS := 0.55

# How far out of step the cells are, as a fraction of the whole turn.
#
# Zero and every cell flips together, which is a slab turning over. One and the
# last cell only starts as the first one lands, which is a slow ripple and loses
# the sense of a single event. Three quarters is a wave with a clear front that
# still reads as one motion — and it is keyed on DEPTH, so the turn sweeps from
# the face you are looking at to the face you are about to be looking at.
const EVERT_STAGGER := 0.75

# ---- the exit --------------------------------------------------------------
#
# THE MACHINE STOPS BEING ONE OBJECT.
#
# Winning used to shrink every cell to a point, running out from the core. It was
# correct and it was quiet: the board did not come apart, it was switched off one
# cell at a time, and the biggest moment in the game read as a screen being
# cleared to make room for a card.
#
# So it is thrown instead. First the solid TURNS — a lean into three quarters
# with the camera easing back — because the explosion only means anything if the
# eye has just been told the thing is three-dimensional and made of parts. Then
# every part goes.
#
# The turn is rotation only. It deliberately does not raise the glass: MATRIX
# takes the board to wire so you can see through it, and debris you can see
# through is not debris.
var win_lean := 0.0
var burst_amt := 0.0

const LEAN_YAW := 0.50      # radians, ~29 degrees
const LEAN_PITCH := 0.33    # ~19 degrees
const LEAN_SECONDS := 0.40
const BURST_SECONDS := 1.30

# How far out of step the cells are, keyed on DISTANCE FROM THE CORE, so the
# break travels outward as a front. Wider than the eversion's, because this one
# is allowed to lose its shape — it is ending.
const BURST_STAGGER := 0.55

var _dim_all := 1.0
var _was_dimmed := false

# Which vault's weathering the lattice is wearing. See Palette.
var _band := 0

# HOW FAR OUT OF THE WAY THE MATERIAL IS — which is what `peek` has always meant,
# and it is not only the hold.
#
# An eversion pushes it too: halfway through the flip the solid is edge-on and
# the cube goes to glass on the way in and hardens on the way out, deliberately,
# because that is a look MATRIX has already taught the player. So the x-ray
# flares for half a second in the middle of a polarity change, which is the right
# picture — the machine turning through itself, seen as wire.
var glass_amount := 0.0


# The pose at a time, and it is a pure function of one — so a check can walk the
# entire cycle rather than trusting a comment about it.
static func attract_pose(t: float) -> Basis:
	var yaw := ATTRACT_YAW + sin(t * ATTRACT_YAW_HZ * 2.0 * PI) * ATTRACT_YAW_SWING
	var pitch := ATTRACT_PITCH + sin(t * ATTRACT_PITCH_HZ * 2.0 * PI) * ATTRACT_PITCH_SWING
	return Basis(Vector3.UP, deg2rad(yaw)) * Basis(Vector3.RIGHT, deg2rad(pitch))


# TAKE THE WHOLE BOARD DOWN TOGETHER — deck, schematic, cage and every glyph
# standing on it — as one number, so nothing is left lit behind a dark cube.
#
# It is NOT the hold's dim, which shades the far cells to say which of them is
# nearer. This one multiplies that: the hold keeps saying what it says, quieter,
# all the way to nothing. Only the finale moves it, and whole() puts it back, so
# the ONE path every cube is entered through is also the path that guarantees the
# next cube arrives at full strength.
func dim(a: float) -> void:
	_dim_all = clamp(a, 0.0, 1.0)


# Put the solid back together. Every entry into a cube goes through here.
func whole() -> void:
	win_lean = 0.0
	burst_amt = 0.0
	_dim_all = 1.0


# The hold, eased — how far into the schematic the board is. Everything that
# comes on with MATRIX reads it from here rather than smoothing the raw amount
# again in its own way and drifting out of step with the rest.
func peek_ease() -> float:
	return peek_amt * peek_amt * (3.0 - 2.0 * peek_amt)


func init(s: Session) -> void:
	_s = s

	cube = Spatial.new()
	cube.name = "Cube"
	add_child(cube)

	mesh_inst = MeshInstance.new()
	mesh_inst.name = "Solid"
	mesh_inst.cast_shadow = GeometryInstance.SHADOW_CASTING_SETTING_OFF
	cube.add_child(mesh_inst)

	_mat_trace = Shaders.material(CELL_PATH)
	_mat_lattice = Shaders.material(CELL_PATH)
	_mat_plate = ShaderMaterial.new()
	# It is cut clean through the lattice and legible from every face, so it
	# draws over whatever is in front of it. That is the rule, not a hack: it is
	# what makes every fold legal while you stand on one, and what makes planning
	# with them possible at all.
	_mat_plate.shader = Shaders.cell_always()
	_mat_plate.render_priority = 1

	push_palette()
	_mat_plate.set_shader_param("edge_lift", 1.4)

	# THE SCHEMATIC IS ITS OWN OBJECT, on the same transform as the solid so it
	# folds with it. It has to be separate geometry — see CubeGeometry.build_wire —
	# and it is switched off entirely at rest, so a player who never holds MATRIX
	# never pays a draw call for it.
	_wire = MeshInstance.new()
	_wire.name = "Wire"
	_wire.cast_shadow = GeometryInstance.SHADOW_CASTING_SETTING_OFF
	_wire.visible = false
	cube.add_child(_wire)
	_mat_wire = Shaders.material(WIRE_PATH)
	_mat_wire.render_priority = 2
	_wire.material_override = _mat_wire

	_marker_root = Spatial.new()
	_marker_root.name = "Markers"
	add_child(_marker_root)

	_build_cage()
	_build_reads()


# ---- the cage --------------------------------------------------------------
#
# Under a held MATRIX the lattice goes to glass, and glass with no edges is just
# a dimmer board. The cage is the twelve edges of the solid you are holding — the
# one piece of the picture that says the thing has a SHAPE rather than being a
# grid that happens to be lit from behind. It rotates with the cube because it
# belongs to the cube, and it fades out entirely at rest because at rest the cube
# is square to the camera and its own silhouette says the same thing for free.

const CAGE_EDGE := [
	[0, 1], [1, 3], [3, 2], [2, 0],
	[4, 5], [5, 7], [7, 6], [6, 4],
	[0, 4], [1, 5], [2, 6], [3, 7],
]


func _build_cage() -> void:
	_cage = MeshInstance.new()
	_cage.name = "Cage"
	_cage.cast_shadow = GeometryInstance.SHADOW_CASTING_SETTING_OFF
	_cage.visible = false
	cube.add_child(_cage)
	_cage_mat = Shaders.material(FX_PATH)
	_cage_mat.render_priority = 3
	_cage.material_override = _cage_mat


func _rebuild_cage(half: float) -> void:
	var corner := []
	for i in range(8):
		corner.append(Vector3(
				-half if (i & 1) == 0 else half,
				-half if (i & 2) == 0 else half,
				-half if (i & 4) == 0 else half))

	var v := PoolVector3Array()
	var uv := PoolVector2Array()
	var kind := PoolVector2Array()
	var col := PoolColorArray()
	var tri := PoolIntArray()
	var w := 0.035

	for e in range(12):
		var a: Vector3 = corner[CAGE_EDGE[e][0]]
		var b: Vector3 = corner[CAGE_EDGE[e][1]]
		var dir := (b - a).normalized()
		# two crossed slabs per edge, so it never vanishes edge-on
		var p1 := dir.cross(Vector3.UP)
		if p1.length_squared() < 0.01:
			p1 = dir.cross(Vector3.RIGHT)
		p1 = p1.normalized()
		var p2 := dir.cross(p1).normalized()
		_slab(v, uv, kind, col, tri, a, b, p1 * w)
		_slab(v, uv, kind, col, tri, a, b, p2 * w)

	# AN EMPTY SURFACE IS NOT AN EMPTY MESH. VisualServer refuses a surface with no
	# vertices and says so once per commit, which on a board that is legitimately
	# bare — before the first cube is loaded, or a schematic with nothing to draw —
	# is an error a frame about nothing being wrong.
	if v.size() == 0:
		_cage.mesh = null
		return
	var m := ArrayMesh.new()
	var arr := []
	arr.resize(ArrayMesh.ARRAY_MAX)
	arr[ArrayMesh.ARRAY_VERTEX] = v
	arr[ArrayMesh.ARRAY_TEX_UV] = uv
	arr[ArrayMesh.ARRAY_TEX_UV2] = kind
	arr[ArrayMesh.ARRAY_COLOR] = col
	arr[ArrayMesh.ARRAY_INDEX] = tri
	m.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	_cage.mesh = m


static func _slab(v: PoolVector3Array, uv: PoolVector2Array, kind: PoolVector2Array,
		col: PoolColorArray, tri: PoolIntArray, a: Vector3, b: Vector3, across: Vector3) -> void:
	var i := v.size()
	v.append(a - across)
	v.append(b - across)
	v.append(b + across)
	v.append(a + across)
	uv.append(Vector2(0, 0))
	uv.append(Vector2(1, 0))
	uv.append(Vector2(1, 1))
	uv.append(Vector2(0, 1))
	for _j in range(4):
		kind.append(Vector2(1, 0))
		col.append(Color(1, 1, 1, 1))
	tri.append(i)
	tri.append(i + 1)
	tri.append(i + 2)
	tri.append(i)
	tri.append(i + 2)
	tri.append(i + 3)


# ---- the two reads that change nothing --------------------------------------

func _build_reads() -> void:
	# Four pips, one per neighbour, telling the eye where the next useful step is
	# before the hand has to work it out.
	for i in range(4):
		var q := GlyphQuad.new()
		q.name = "pip" + str(i)
		_marker_root.add_child(q)
		q.build("node", Color(1, 1, 1, 1), 0.22, 95)
		q.visible = false
		_near_pips.append(q)

	# And the antipode, drawn faintly THROUGH the player: a look at the far side
	# of the world down the column you are standing in.
	#
	# ABOVE THE HORIZON, NOT BEHIND IT. This is the far side of the world seen
	# THROUGH the hole, so it has to draw after the hole has removed the deck —
	# at 94 it was being covered by the very thing it is meant to be visible
	# through. See PlayerOrb for the rest of the stack.
	_through = GlyphQuad.new()
	_through.name = "through"
	_marker_root.add_child(_through)
	_through.build("core", Color(1, 1, 1, 1), 0.34, 98)
	_through.visible = false


func _place_reads(cam: Camera) -> void:
	var c := (_s.n - 1) * 0.5
	var quiet: bool = _s.walking == null and _s.anim == null and not _s.won

	_s.near_specials(_near)
	for i in range(_near_pips.size()):
		var on: bool = quiet and i < _near.size()
		var pip: GlyphQuad = _near_pips[i]
		if pip.visible != on:
			pip.visible = on
		if not on:
			continue
		var e: Array = _near[i]
		# THROUGH THE CONVERSION, NOT AROUND IT. near_specials answers in the
		# rules' view coordinates and this is the only place that turned them
		# into object space by hand — which was invisible for as long as the
		# conversion left x alone, and stopped being the moment it did not.
		pip.translation = CubeGeometry.to_object(e[0] - c, e[1] - c, _s.n * 0.5 + 0.4)
		pip.face(cam)
		pip.set_role(_role_of(e[2]))
		pip.set_colour(_colour(e[2]) * 0.75)

	var through: int = _s.through_look() if quiet else Session.Special.NONE
	var show_through: bool = through != Session.Special.NONE
	if _through.visible != show_through:
		_through.visible = show_through
	if show_through:
		_through.set_role(_role_of(through))
		# faint, and deliberately so: it is a look through a hole, and the only
		# reward is noticing
		var col := _colour(through)
		_through.set_colour(Color(col.r, col.g, col.b, 0.30))
		var toward := -cam.global_transform.basis.z
		_through.translation = cube.global_transform.xform(CubeGeometry.cell_to_object(_s.n, _s.pos)) \
				- toward * 0.40
		_through.face(cam)
		# rocking very slightly, because the point is that it is GLIMPSED
		_through.rotate_object_local(Vector3.FORWARD,
				deg2rad(sin(float(OS.get_ticks_msec()) / 1000.0 * 0.42) * 4.0))


static func _role_of(s: int) -> String:
	if s == Session.Special.CORE:
		return "core"
	return "node" if s == Session.Special.NODE else "lock"


static func _colour(s: int) -> Color:
	if s == Session.Special.CORE:
		return Palette.core()
	return Palette.NODE if s == Session.Special.NODE else Palette.LOCK


# The two depth ramps, onto the materials that draw them.
#
# Separated out because legibility moves the near lattice, and a setting that
# changes the board has to be able to say so to the board after it has already
# been built. These are the only colours the cube has: everything else about a
# cell is arithmetic in the shader.
func push_palette(band: int = -1) -> void:
	if band >= 0:
		_band = band
	if _mat_trace == null:
		return
	_mat_trace.set_shader_param("col_far", Palette.TRACE_FAR)
	_mat_trace.set_shader_param("col_near", Palette.TRACE_NEAR)
	_mat_lattice.set_shader_param("col_far", Palette.lattice_far_of(_band))
	_mat_lattice.set_shader_param("col_near", Palette.lattice_near_of(_band))

	# A plate is the one thing that is the same in every world, so it does not
	# take the depth ramp: it is rust at full strength wherever it is.
	_mat_plate.set_shader_param("col_far", Palette.rust() * 0.62)
	_mat_plate.set_shader_param("col_near", Palette.RUST_HI)

	# AND THE SCHEMATIC, which is what the same three materials are drawn in
	# while somebody is holding MATRIX. Constants, so they are pushed with the
	# palette rather than every frame. The plate keeps its rust for the reason it
	# keeps everything else: it is the one thing that is the same in every world,
	# and a schematic that recoloured it would be saying it was not.
	_mat_trace.set_shader_param("wire_col", Palette.WIRE_TRACE)
	_mat_lattice.set_shader_param("wire_col", Palette.WIRE_LATTICE)
	_mat_plate.set_shader_param("wire_col", Palette.RUST_HI)

	if _mat_wire == null:
		return
	_mat_wire.set_shader_param("col_trace", Palette.WIRE_TRACE)
	_mat_wire.set_shader_param("col_lattice", Palette.WIRE_LATTICE)
	_mat_wire.set_shader_param("col_grid", Palette.WIRE_LATTICE)

	# THE THREE STRENGTHS ARE THE WHOLE BALANCE OF THE PICTURE, and they are an
	# order of magnitude apart on purpose.
	#
	# Every cell in the volume draws a box, so the empty ones are the most
	# numerous thing on screen by a long way — that is the point, they are the
	# space the corridors are cut through — and they have to sum to a faint field
	# rather than to a wall. At 0.045 it takes twenty-two of them stacked to
	# reach full white, which is more than any ray through the cube crosses.
	#
	# The lattice is the material you cannot stand on and the trace is the
	# material you can, and they keep the ratio the board keeps, so the one
	# property the whole game is read off survives into the x-ray.
	_mat_wire.set_shader_param("gain_grid", 0.045)
	_mat_wire.set_shader_param("gain_lattice", 0.14)
	_mat_wire.set_shader_param("gain_trace", 0.40)
	_mat_wire.set_shader_param("wire_far", 0.26)


func rebuild(force: bool = false) -> void:
	if _s == null or _s.lv == null:
		return
	if not force and _built_world == _s.world and _built_cells == _s.lv.vox.size():
		return
	_built_world = _s.world
	_built_cells = _s.lv.vox.size()

	_mesh = CubeGeometry.build(_s.lv, _s.world)
	mesh_inst.mesh = _mesh
	mesh_inst.set_surface_material(CubeGeometry.SUB_TRACE, _mat_trace)
	mesh_inst.set_surface_material(CubeGeometry.SUB_LATTICE, _mat_lattice)
	mesh_inst.set_surface_material(CubeGeometry.SUB_PLATE, _mat_plate)
	_cell_of_vertex = CubeGeometry.cell_of_vertex().duplicate()
	_vertex_cols.resize(_cell_of_vertex.size())

	# the same cube, drawn instead of built — see CubeGeometry.build_wire
	_wire_mesh = CubeGeometry.build_wire(_s.lv, _s.world)
	_wire.mesh = _wire_mesh
	_wire_cell_of_vertex = CubeGeometry.wire_cell_of_vertex().duplicate()
	_wire_class = CubeGeometry.wire_class().duplicate()
	_wire_cols.resize(_wire_cell_of_vertex.size())

	_rebuild_cage(_s.n * 0.5)

	var half_span := (_s.n - 1) * 0.5
	_mat_trace.set_shader_param("half_span", half_span)
	_mat_lattice.set_shader_param("half_span", half_span)
	_mat_plate.set_shader_param("half_span", half_span)
	if _mat_wire != null:
		_mat_wire.set_shader_param("half_span", half_span)

	_build_markers()
	update_reach()


# Rewrite the colour stream with what is reachable from where the player stands.
# No re-triangulation: the geometry only changes when the WORLD does, and this
# changes on every step.
#
# Reachability is a property of a view SQUARE, not of a cell — so a cell is lit
# when it is the surface of a reachable column, which is exactly the set the
# player may walk to.
#
# GODOT REBUILDS THE SURFACE RATHER THAN SETTING A COLOUR STREAM, because an
# ArrayMesh surface is immutable once added. It is the same work: the vertex
# arrays are kept and only the colour one is rewritten, so nothing is
# re-triangulated and the cost is a memcpy of the positions.
func update_reach() -> void:
	if _s == null or _s.lv == null or _cell_of_vertex.empty() or _s.reach.empty():
		return

	var n: int = _s.n
	var cells := n * n * n
	var lit := PoolByteArray()
	var dist := PoolByteArray()
	var wave := PoolByteArray()
	lit.resize(cells)
	dist.resize(cells)
	wave.resize(cells)
	for i in range(cells):
		lit[i] = 0
		dist[i] = 0

	# The transition front is measured in VIEW cells from its focus, not in world
	# cells: the wave is something the screen does, and two cells that land on
	# the same square have to arrive together or the front tears.
	var focus := Projection.view_of(n, _s.m, _wave_focus)
	var nn := n * n
	for i in range(cells):
		var y := i / nn
		var rr := i - y * nn
		var z := rr / n
		var v3 := Projection.view_of(n, _s.m, Vector3(rr - z * n, y, z))
		var d := sqrt((v3.x - focus.x) * (v3.x - focus.x) + (v3.y - focus.y) * (v3.y - focus.y))
		wave[i] = int(min(255, round(d)))

	for u in range(n):
		for v in range(n):
			var k := u * n + v
			if not _s.reach[k]:
				continue
			var sc: Surf = _s.surf[k]
			if not sc.has:
				continue
			var idx := Level.vidx_v(n, sc.w)
			lit[idx] = 255
			dist[idx] = _s.reach_dist[k]

	for i in range(_cell_of_vertex.size()):
		var cell: int = _cell_of_vertex[i]
		_vertex_cols[i] = Color(lit[cell] / 255.0, dist[cell] / 255.0, wave[cell] / 255.0, 1.0)

	_recolour(_mesh, mesh_inst, _vertex_cols, [_mat_trace, _mat_lattice, _mat_plate])

	# AND THE SCHEMATIC TAKES THE SAME THREE CHANNELS, so it arrives on the same
	# wave as the board it is drawing. Alpha is the one it does not take: that is
	# which class the cell is, it was written when the wire was built, and it is
	# a property of the cube rather than of the moment.
	if _wire_cell_of_vertex.size() != _wire_cols.size():
		return
	for i in range(_wire_cell_of_vertex.size()):
		var cell2: int = _wire_cell_of_vertex[i]
		_wire_cols[i] = Color(lit[cell2] / 255.0, dist[cell2] / 255.0, wave[cell2] / 255.0,
				_wire_class[i] / 255.0)
	_recolour(_wire_mesh, _wire, _wire_cols, [_mat_wire])


# Swap one array on an existing mesh. Every other stream is read back and handed
# straight in again, which is what makes this a colour write rather than a
# rebuild.
static func _recolour(mesh: ArrayMesh, inst: MeshInstance, cols: PoolColorArray, mats: Array) -> void:
	if mesh == null:
		return
	var out := ArrayMesh.new()
	for s in range(mesh.get_surface_count()):
		var arr: Array = mesh.surface_get_arrays(s)
		if arr[ArrayMesh.ARRAY_VERTEX].size() == cols.size():
			arr[ArrayMesh.ARRAY_COLOR] = cols
		out.add_surface_from_arrays(mesh.surface_get_primitive_type(s), arr)
	inst.mesh = out
	for i in range(min(mats.size(), out.get_surface_count())):
		inst.set_surface_material(i, mats[i])
	if mats.size() == 1:
		inst.material_override = mats[0]


# Send the front back to the player and let it run out again.
func restart_reveal(head_start: float = 0.0) -> void:
	_reveal = -head_start


# Run the board in from a cell, or out of it. Only the inward direction has a
# caller now: the exit used to be an outward wave — every cell shrinking to a
# point, running from the core — and it is a burst instead. The direction stays
# because it costs one sign and the wave is the only thing in here that can end a
# board quietly, which some future screen will want.
func start_wave(focus: Vector3, inward: bool) -> void:
	_wave_focus = focus
	_wave_dir = 1.0 if inward else -1.0
	_wave = -2.0 if inward else 0.0
	update_reach()


func skip_wave() -> void:
	_wave = 999.0
	_wave_dir = 1.0


# ---- markers ---------------------------------------------------------------
#
# An object — node, lock, core, and the player's own footing — is attached to a
# cell, and exists in a projection only while that cell IS the surface of its
# column. Bury it behind lattice and it is gone until you fold. So the markers
# are not children of the cube: they are placed each frame at the cube's current
# world position for that cell, pushed a little toward the camera, and hidden
# outright when the projection says the cell they belong to is not what you are
# looking at.

class Marker:
	var quad: GlyphQuad
	var cell := Vector3.ZERO
	var role := ""
	var index := 0
	var col := Color(1, 1, 1, 1)   # as authored, so a fade has something to come back to


func _build_markers() -> void:
	for m in _markers:
		if is_instance_valid(m.quad):
			m.quad.queue_free()
	_markers.clear()

	_add_marker(_s.lv.goal, "core", 0, Palette.core(), 0.86)
	for i in range(_s.lv.keys.size()):
		_add_marker(_s.lv.keys[i], "node", i, Palette.NODE, 0.62)
	for i in range(_s.lv.doors.size()):
		_add_marker(_s.lv.doors[i], "lock", i, Palette.LOCK, 0.74)


func _add_marker(cell: Vector3, role: String, index: int, col: Color, size: float) -> void:
	var q := GlyphQuad.new()
	q.name = role + str(index)
	_marker_root.add_child(q)
	q.build(role, col, size, 100)
	var m := Marker.new()
	m.quad = q
	m.cell = cell
	m.role = role
	m.index = index
	m.col = col
	_markers.append(m)


# ---- per frame -------------------------------------------------------------

func apply(cam: Camera, dt: float) -> void:
	if _s == null or _s.lv == null:
		return

	# base orientation, then the live fold on top of it in world space
	var base_rot: Basis = CubeGeometry.rotation_for(_s.m)
	var ang := 0.0
	var axis_x := true
	if _s.anim != null:
		ang = _s.anim.ang
		axis_x = _s.anim.axis_x
	elif _s.drag != null and _s.drag.has_axis:
		ang = _s.drag.ang
		axis_x = _s.drag.axis_x

	var live := Basis()
	if abs(ang) > 1e-5:
		# A horizontal drag yaws the cube; dragging RIGHT pushes the near face
		# right, which in this frame is a NEGATIVE rotation about up. A vertical
		# drag pitches it, and dragging up lifts the near face, which is a
		# positive rotation about right.
		live = Basis(Vector3.UP, -ang) if axis_x else Basis(Vector3.RIGHT, ang)

	# THE TURN, AND THE GLASS THAT COVERS IT.
	#
	# Halfway through the flip the solid is edge-on and a scale of zero is a
	# degenerate object — a line. That instant is not hidden, it is USED: the cube
	# goes to glass on the way in and hardens on the way out, which is the same
	# look MATRIX already taught the player, so "there is something behind this"
	# is a sentence they have heard. Eversion is that sentence finished: the
	# behind is now the front.
	_evert_target = 1.0 if _s.everted() else 0.0
	evert_amt = move_toward(evert_amt, _evert_target, dt / max(0.01, EVERT_SECONDS))
	var ev := evert_amt

	# one at the middle of the turn, zero at either end — how much of the
	# transition we are IN, as opposed to which side of it we are on
	var turning := 1.0 - abs(ev * 2.0 - 1.0)

	var e: float = max(peek_ease(), turning * 0.85)
	glass_amount = e
	if e > 1e-4:
		live = Basis(Vector3.UP, -peek_yaw * e) * Basis(Vector3.RIGHT, peek_pitch * e) * live

	# THE LEAN OUT. Gated on motion because it is a camera move on top of the
	# picture rather than the framing of it — a player who asked the board to
	# hold still gets the burst without the turn, and the burst is still the board
	# coming apart.
	if win_lean > 1e-4:
		var wl := win_lean * Access.motion_amount()
		live = Basis(Vector3.UP, -LEAN_YAW * wl) * Basis(Vector3.RIGHT, LEAN_PITCH * wl) * live

	if attract:
		_attract_t += dt
		live = attract_pose(_attract_t) * live

	# The squash rides on the cube's scale, so the basis is written without it
	# and the scale is put back — see CameraRig.tick, which owns the squash.
	var keep := cube.scale
	cube.transform.basis = live * base_rot
	cube.scale = keep

	var dim_near: float = lerp(1.0, 0.55, e)
	_mat_lattice.set_shader_param("dim", dim_near)
	_mat_trace.set_shader_param("dim", lerp(1.0, 0.85, e))

	_wave += dt * WAVE_PER_SECOND

	# THE SCHEMATIC RUNS THE SAME VERTEX STAGE, so it takes the same uniforms —
	# every one of them, or a wire would sit still through a fold the cell it
	# belongs to was travelling through. It is off entirely below the threshold
	# its own fragment discards at, so a player who never holds MATRIX never pays
	# a draw call.
	var wire_on: bool = _mat_wire != null and e > 0.02
	if _wire.visible != wire_on:
		_wire.visible = wire_on

	# THE TWO AXES ARE TAKEN FROM THE CUBE'S OWN ROTATION, every frame, because
	# the solid is folded and the shader works in object space. Forward is what
	# gets mirrored; right is what the cells spin about, so they all turn the same
	# way on screen whichever face happens to be pointing at the camera.
	var inv := cube.global_transform.basis.orthonormalized().inverse()
	var evert_axis := inv.xform(Vector3(0, 0, -1))
	var evert_spin := inv.xform(Vector3(1, 0, 0))

	for m in [_mat_trace, _mat_lattice, _mat_plate, _mat_wire]:
		if m == null:
			continue
		m.set_shader_param("peek", e)
		m.set_shader_param("wave", _wave)
		m.set_shader_param("wave_dir", _wave_dir)
		m.set_shader_param("evert", ev)
		m.set_shader_param("evert_axis", evert_axis)
		m.set_shader_param("evert_spin", evert_spin)
		m.set_shader_param("evert_spread", EVERT_STAGGER)
		m.set_shader_param("burst", burst_amt)
		m.set_shader_param("burst_spread", BURST_STAGGER)
		m.set_shader_param("all_amt", _dim_all)

	_reveal += dt * REVEAL_PER_SECOND

	# THE COLOUR OF THE TURN. The everter's violet is pushed into the near-colour
	# of every material while the solid is passing through itself, so the flip is
	# not a silent geometric event — the whole board says which control did this,
	# in the control's own hue, and the light channel scales it because it is
	# light.
	if turning > 1e-4:
		var k := turning * Access.light_amount()
		_mat_trace.set_shader_param("col_near",
				Palette.TRACE_NEAR.linear_interpolate(Palette.EVERT, k * 0.8))
		_mat_lattice.set_shader_param("col_near",
				Palette.lattice_near().linear_interpolate(Palette.EVERT, k * 0.55))
	elif _was_turning:
		_mat_trace.set_shader_param("col_near", Palette.TRACE_NEAR)
		_mat_lattice.set_shader_param("col_near", Palette.lattice_near())
	_was_turning = turning > 1e-4

	_mat_trace.set_shader_param("reveal", _reveal)
	_mat_lattice.set_shader_param("reveal", _reveal)
	_mat_plate.set_shader_param("reveal", _reveal)

	# the cage only earns its place while the solid is being looked at
	var cage_on: bool = e > 0.01
	if _cage.visible != cage_on:
		_cage.visible = cage_on
	if cage_on:
		# 0.46 at rest and 0.40 more under a full lean, which is the original's
		# cage alpha. The cage is the twelve edges of the thing you are HOLDING,
		# so it has to come up as the material goes away — glass with no edges is
		# just a dimmer board.
		#
		# AND IT IS THE BRIGHTEST LINE IN THE SCHEMATIC. Under a full hold the
		# whole solid has become wire, and a rust cage at 0.86 sitting inside a
		# lattice of lit wire is the outline of the object reading as less present
		# than its contents. It goes to the trace's wire colour at full strength,
		# which is the one thing on screen that should be unambiguous: this is
		# where the machine ends.
		var pe := peek_ease()
		var cage := Palette.rust().linear_interpolate(Palette.WIRE_TRACE, pe * 0.85)
		_cage_mat.set_shader_param("tint",
				Color(cage.r, cage.g, cage.b, e * (0.46 + pe * 0.54) * _dim_all))

	# A MARKER BELONGS TO A CELL, AND THE CELLS HAVE LEFT. Every glyph on the
	# board is placed each frame at the cube's world position for its cell, and
	# the burst happens in the vertex shader — so the CPU still thinks every cell
	# is exactly where it was authored. Leaving them on would hang the core, the
	# nodes and the player in mid-air over an empty screen. They go with the
	# board.
	var intact: bool = burst_amt <= 1e-4
	if _marker_root.visible != intact:
		_marker_root.visible = intact
	if not intact:
		return

	_place_markers(cam)
	_place_reads(cam)


func _place_markers(cam: Camera) -> void:
	var toward := cam.global_transform.basis.z * 0.52

	# A GLYPH IS A QUAD, NOT A CELL, so the board's fade does not reach it
	# through a material — it has to be written here. The write is skipped
	# entirely at full strength, and done ONCE MORE on the frame the fade ends,
	# so ordinary play costs nothing and nothing is left dark.
	var dimming: bool = _dim_all < 0.999
	var paint: bool = dimming or _was_dimmed
	_was_dimmed = dimming

	for m in _markers:
		var shown := _shown(m)
		if m.quad.visible != shown:
			m.quad.visible = shown
		if not shown:
			continue
		m.quad.translation = cube.global_transform.xform(CubeGeometry.cell_to_object(_s.n, m.cell)) + toward
		m.quad.face(cam)
		if paint:
			m.quad.set_colour(m.col * _dim_all)


func _shown(m) -> bool:
	if _s.surf.empty():
		return false
	if m.role == "node" and (_s.kmask & (1 << m.index)) != 0:
		return false                               # collected
	if m.role == "lock" and (_s.doors & (1 << m.index)) != 0:
		return false                               # opened
	# in this world at all?
	if not Level.is_walk_type(Level.eff_type(_s.lv.vox[_s.lv.vidx(m.cell)], _s.world)):
		return false
	return Projection.surface_at(_s.n, _s.surf, _s.m, m.cell)
