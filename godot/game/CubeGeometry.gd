class_name CubeGeometry
# THE CUBE, AS GEOMETRY.
#
# NOT CubeMesh, WHICH IS THE C#'s NAME FOR IT: Godot already has a CubeMesh — the
# primitive box every 3D tutorial starts with — and a `class_name` that shadows a
# native class is refused outright. The name here says what it is rather than
# what it makes, which is the better name anyway.
#
# Only faces with nothing next to them are ever built — the inside of the cube is
# not visible from anywhere, and meshing it away is the difference between
# thirteen hundred quads and three hundred on the cubes that need it.
#
# Three surfaces, because three materials: trace, lattice, and plate. The plate
# gets its own because it is CUT CLEAN THROUGH the lattice and has to draw over
# whatever is in front of it, in every orientation, in every world. That is not a
# rendering trick — it is the rule that makes plates legible enough to plan with,
# and it is why every fold is legal while you stand on one.

const SUB_TRACE := 0
const SUB_LATTICE := 1
const SUB_PLATE := 2

const FACE_DIR := [
	Vector3(1, 0, 0), Vector3(-1, 0, 0),
	Vector3(0, 1, 0), Vector3(0, -1, 0),
	Vector3(0, 0, 1), Vector3(0, 0, -1),
]

const QUAD_UV := [Vector2(1, 1), Vector2(0, 1), Vector2(0, 0), Vector2(1, 0)]

# Which cell each vertex belongs to, in the order build() emitted them.
#
# Kept so the reveal sweep can rewrite the colour stream on every settle without
# re-triangulating: the geometry only changes when the WORLD changes, but what is
# reachable changes on every step and every fold.
const _CELL_OF_VERTEX := []
const _WIRE_CELL_OF_VERTEX := []

# What each wire cell IS: 0 empty, 128 lattice, 255 something you can stand on.
# Kept because the reach pass rewrites the other three colour channels every
# settle and must not lose this one.
const _WIRE_CLASS := []
const _QUADS := []


static func cell_of_vertex() -> Array:
	return _CELL_OF_VERTEX


static func wire_cell_of_vertex() -> Array:
	return _WIRE_CELL_OF_VERTEX


static func wire_class() -> Array:
	return _WIRE_CLASS


# CORE CELL SPACE TO OBJECT SPACE, AND BOTH OF THE MINUSES ARE EARNED.
#
# The z flip converts handedness: the rules use a right-handed basis and the
# engine's cameras look down their own -Z. See the note on rotation_for for why
# it has to exist here rather than being folded into the rotation.
#
# THE X FLIP IS THE GESTURE. The camera stands on the minus side of the cube and
# is turned to face it, which is the only arrangement that draws the face the
# rules are played on — see CameraRig.init. Turning it about Y puts its own right
# at world -X, so a cube built with the rules' x running along world +X comes out
# with the rules' RIGHT on the left of the screen. Nothing about the settled
# board shows that: the rules' u axis has no meaning of its own, and a tap is
# unprojected rather than mapped, so taps land under the thumb either way.
#
# The FOLD shows it, every time. A drag to the left is turn 0, which pushes the
# near face toward the rules' -x, and that face was travelling +137 pixels to the
# RIGHT on a 405-wide screen. The whole solid rolled away from the thumb. So the
# rules' x runs along world -X, the two flips together make the conversion a
# proper rotation, and the picture agrees with the hand.
static func to_object(x: float, y: float, z: float) -> Vector3:
	return Vector3(-x, y, -z)


static func dir_object(d: Vector3) -> Vector3:
	return Vector3(-d.x, d.y, -d.z)


# The four corners of each face, in object units of one cell. Built from the
# OBJECT-space direction, so the winding stays consistent in the space the mesh
# actually lives in.
static func _quads() -> Array:
	if not _QUADS.empty():
		return _QUADS
	var sg := [Vector2(1, 1), Vector2(-1, 1), Vector2(-1, -1), Vector2(1, -1)]
	for f in range(6):
		var d := dir_object(FACE_DIR[f])
		var a := Vector3.UP if abs(d.x) == 1 else Vector3.RIGHT
		var b := d.cross(a).normalized()
		a = b.cross(d).normalized()
		var q := []
		for i in range(4):
			q.append(d * 0.5 + (a * sg[i].x + b * sg[i].y) * 0.5)
		_QUADS.append(q)
	return _QUADS


# Where a world cell sits in the cube's object space, in cell units.
static func cell_to_object(n: int, w: Vector3) -> Vector3:
	var c := (n - 1) * 0.5
	return to_object(w.x - c, w.y - c, w.z - c)


# Build the visible surface of `lv` as seen in `world`. Coordinates are centred
# on the cube's middle and scaled so one cell is one unit, so the object's
# transform is pure rotation and the camera never has to know the cube's size.
#
# THE ATTRIBUTES ARE THE SHADER'S, and there are five of them. Position, normal
# and UV are ordinary; UV2 carries the CELL CENTRE, which is what lets the
# arrival wave and the exit burst move a whole cell rather than each vertex on
# its own, and the vertex COLOUR is the per-cell state the reach pass rewrites
# without touching the geometry.
static func build(lv: Level, world: int) -> ArrayMesh:
	var n: int = lv.n
	var eff := lv.eff(world)
	var quads := _quads()

	var verts := PoolVector3Array()
	var norms := PoolVector3Array()
	var uv0 := PoolVector2Array()
	var uv1 := PoolVector2Array()
	var cols := PoolColorArray()
	_CELL_OF_VERTEX.clear()
	# PLAIN ARRAYS, AND THE REASON IS THE ONE THAT BITES TWICE IN THIS PORT.
	#
	# A Pool*Array is a VALUE in GDScript, so `tri[sub]` hands back a COPY and
	# `tri[sub].append(...)` fills a copy that is thrown away on the next line.
	# Every index this loop emitted went into the bin: eight hundred vertices
	# arrived with no triangles referencing them, the mesh committed three
	# degenerate placeholders instead of three surfaces, and the board rendered as
	# nothing at all — with no error anywhere, because nothing failed. An Array is
	# a reference and appends in place; the pool is built once at the end, which is
	# the only place it was ever wanted.
	var tri := [[], [], []]

	var c := (n - 1) * 0.5

	for y in range(n):
		for z in range(n):
			for x in range(n):
				var t: int = eff[Level.vidx_n(n, x, y, z)]
				if t == Level.VOID:
					continue

				var sub := SUB_PLATE if Level.is_glyph(t) else (SUB_TRACE if t == Level.TRACE else SUB_LATTICE)
				var centre := to_object(x - c, y - c, z - c)

				for f in range(6):
					var d: Vector3 = FACE_DIR[f]
					var nx := x + int(d.x)
					var ny := y + int(d.y)
					var nz := z + int(d.z)
					var outside: bool = nx < 0 or ny < 0 or nz < 0 or nx >= n or ny >= n or nz >= n

					# A plate is cut through, so it always shows its own faces;
					# otherwise a face is built only where the neighbour is
					# nothing.
					if not outside and eff[Level.vidx_n(n, nx, ny, nz)] != Level.VOID and sub != SUB_PLATE:
						continue

					var b := verts.size()
					var nrm := dir_object(d)
					for i in range(4):
						verts.append(centre + quads[f][i])
						norms.append(nrm)
						uv0.append(QUAD_UV[i])
						# THE CELL CENTRE RIDES IN UV2 as two of its three
						# numbers and the third in the colour's red — Godot's
						# mesh arrays have no three-component second UV, and the
						# shader wants the whole point. See Shaders.
						uv1.append(Vector2(centre.x, centre.y))
						cols.append(Color(0, 0, 0, 1))
						_CELL_OF_VERTEX.append(Level.vidx_n(n, x, y, z))
					tri[sub].append(b)
					tri[sub].append(b + 1)
					tri[sub].append(b + 2)
					tri[sub].append(b)
					tri[sub].append(b + 2)
					tri[sub].append(b + 3)

	var mesh := ArrayMesh.new()
	for s in range(3):
		if tri[s].size() == 0:
			# AN EMPTY SURFACE IS STILL A SURFACE, because the material slots are
			# assigned by index and a cube with no plate in it would otherwise
			# hand the plate's material to nothing and the lattice's to the plate.
			# One degenerate triangle costs nothing and keeps the three slots the
			# three things they are.
			var arr0 := []
			arr0.resize(ArrayMesh.ARRAY_MAX)
			arr0[ArrayMesh.ARRAY_VERTEX] = PoolVector3Array([Vector3.ZERO, Vector3.ZERO, Vector3.ZERO])
			arr0[ArrayMesh.ARRAY_INDEX] = PoolIntArray([0, 1, 2])
			mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr0)
			continue
		var arr := []
		arr.resize(ArrayMesh.ARRAY_MAX)
		arr[ArrayMesh.ARRAY_VERTEX] = verts
		arr[ArrayMesh.ARRAY_NORMAL] = norms
		arr[ArrayMesh.ARRAY_TEX_UV] = uv0
		arr[ArrayMesh.ARRAY_TEX_UV2] = uv1
		arr[ArrayMesh.ARRAY_COLOR] = cols
		arr[ArrayMesh.ARRAY_INDEX] = PoolIntArray(tri[s])
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	return mesh


# ---- the schematic ---------------------------------------------------------

# Half a cell, less the gutter — the SAME inset the surface shader uses for its
# plate, so a wire box and the rim of the face in front of it are the same line
# rather than two lines a hair apart.
const WIRE_HALF := 0.5 - 0.055


# THE MACHINE AS A DRAWING: one wire box per cell, for EVERY cell in the volume,
# including the empty ones.
#
# This is the geometry the surface mesh cannot supply and never could. build()
# meshes away every face with something next to it, which is right — the inside
# of a solid is not visible from anywhere — and it means a cell buried in the
# lattice has no vertices at all, and a VOID has none by definition. So a
# schematic drawn from face borders can only ever outline the walls that already
# happened to exist. What it cannot draw is a complete cell, and what it cannot
# draw at all is the space.
#
# THE SPACE IS THE POINT. A void is not absence in this game, it is the route:
# the corridors are what you fold to line up. Drawing the whole n-cubed volume as
# a faint lattice and the material as brighter boxes on top says where the matter
# ISN'T, which is the question somebody holding MATRIX is actually asking and the
# one thing the board has never been able to answer — a void and the outside of
# the cube looked identical.
#
# Eight vertices and twelve edges a cell, as lines. A full eight-cube is four
# thousand vertices, which is nothing, and it carries the same attributes as the
# surface so it runs through the same vertex stage — the arrival wave, the
# eversion, the exit burst. A wire that did not travel with the cell it belongs
# to would come off it the moment anything moved.
static func build_wire(lv: Level, world: int) -> ArrayMesh:
	var n: int = lv.n
	var eff := lv.eff(world)
	var c := (n - 1) * 0.5

	var verts := PoolVector3Array()
	var norms := PoolVector3Array()
	var uv1 := PoolVector2Array()
	var cols := PoolColorArray()
	var idx := PoolIntArray()
	_WIRE_CELL_OF_VERTEX.clear()
	_WIRE_CLASS.clear()

	for y in range(n):
		for z in range(n):
			for x in range(n):
				var cell := Level.vidx_n(n, x, y, z)
				var t: int = eff[cell]
				var k := 0
				if t != Level.VOID:
					k = 255 if Level.is_walk_type(t) else 128

				var centre := to_object(x - c, y - c, z - c)
				var b := verts.size()
				for i in range(8):
					verts.append(centre + Vector3(
							-WIRE_HALF if (i & 1) == 0 else WIRE_HALF,
							-WIRE_HALF if (i & 2) == 0 else WIRE_HALF,
							-WIRE_HALF if (i & 4) == 0 else WIRE_HALF))
					# The wire fragment never reads it, but the shared vertex
					# stage normalises the normal and a zero one is a NaN in an
					# interpolator. One constant is cheaper than finding out which
					# driver minds.
					norms.append(Vector3.FORWARD)
					uv1.append(Vector2(centre.x, centre.y))
					cols.append(Color(0, 0, 0, k / 255.0))
					_WIRE_CELL_OF_VERTEX.append(cell)
					_WIRE_CLASS.append(k)

				# the twelve edges: every pair of corners differing in exactly one
				# bit
				for i in range(8):
					var bit := 1
					while bit <= 4:
						if (i & bit) == 0:
							idx.append(b + i)
							idx.append(b + i + bit)
						bit <<= 1

	var mesh := ArrayMesh.new()
	# AN EMPTY SURFACE IS NOT AN EMPTY MESH. VisualServer refuses a surface with no
	# vertices and says so once per commit, which on a board that is legitimately
	# bare — before the first cube is loaded, or a schematic with nothing to draw —
	# is an error a frame about nothing being wrong.
	if verts.size() == 0:
		return mesh
	var arr := []
	arr.resize(ArrayMesh.ARRAY_MAX)
	arr[ArrayMesh.ARRAY_VERTEX] = verts
	arr[ArrayMesh.ARRAY_NORMAL] = norms
	arr[ArrayMesh.ARRAY_TEX_UV2] = uv1
	arr[ArrayMesh.ARRAY_COLOR] = cols
	arr[ArrayMesh.ARRAY_INDEX] = idx
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_LINES, arr)
	return mesh


# THE ROTATION THAT PUTS THE CUBE IN THE ORIENTATION `m` DESCRIBES.
#
# This is the one place the two coordinate conventions have to be reconciled, and
# it is worth being explicit about why. The rules use a RIGHT-handed view basis —
# R right, U up, F toward the camera — which is the convention the projection
# maths is written in. The engine's camera looks down its own -Z, so the world it
# draws has +Z coming toward the viewer for a right-handed basis and the mesh is
# built with cell z negated. Negating one row of the basis to convert would give
# a matrix with determinant -1, which is a reflection and not a rotation: the
# cube would come out mirrored and no quaternion could express it.
#
# The fix is to conjugate rather than to convert: M = J A J, where A has rows R,
# U, F and J is the same signed diagonal to_object applies. A conjugation cannot
# change a determinant — det(J A J) = det(J)^2 det(A) = det(A) — so M is a proper
# rotation whatever J is, and there is a quaternion for it.
#
# J = diag(-1, 1, -1), matching to_object, and it is a rotation in its own right:
# a half turn about Y. Entry by entry, M[i][j] = s(i) * A[i][j] * s(j) with
# s = (-1, 1, -1), which negates the middle row and the middle column and leaves
# the corners of the outer ones alone.
#
# The mesh is built in the object handedness already, so the J on the right is
# absorbed by reading the cube's local axes back out of the columns of M below.
static func rotation_for(m: Ori) -> Basis:
	# columns of M = images of the object-space basis vectors
	var ix := Vector3(m.r.x, -m.u.x, m.f.x)
	var iy := Vector3(-m.r.y, m.u.y, -m.f.y)
	var iz := Vector3(m.r.z, -m.u.z, m.f.z)
	# A signed permutation is exactly orthonormal, so this cannot be handed
	# anything degenerate.
	return Basis(ix, iy, iz)
