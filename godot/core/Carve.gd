class_name Carve
# THE CARVE — giving a view square footing, and saying which world cell now
# provides it.
#
# It is its own class rather than three statics on Generator because the Walker
# below it has to call it, and GDScript 3.5 refuses a nested class that names
# its own outer class. Splitting it out is also the honest shape: this is the
# one operation the whole generator is built from.


# Give view square (u,v) footing in this world, and say which world cell now
# provides it. Returns the cell as a Vector3, or null when the carve is refused.
#
# Carving only ever turns lattice into trace. That is monotone: it can add
# footing, never remove it, so carving for the fifth fold can never break the
# path carved for the first. A locked cell is one an earlier leg relies on in a
# different world; it is never overwritten.
#
# `mirror` IS THE EVERTER'S WHOLE CONSTRAINT.
#
# The last thing this does, when a column has nothing in it at all, is PLACE a
# cell — which is how the carve guarantees itself footing in empty space. On an
# everter cube that breaks the one invariant the mechanic has: a cell is turning
# to show a different face, so both arrays have to agree about which cells
# exist.
#
# The first attempt at this simply DENIED the creation on the face array,
# confining the second route to cells the solid already had. The inert surface
# is forty squares a cube, so that sounded generous, and the yield was three
# cubes in four thousand six hundred attempts — the route does not want just any
# cells, it wants the ones in its way.
#
# So the creation is allowed and MIRRORED instead. A cell placed in one array
# appears in the other as bedrock, and occupancy stays locked by construction
# rather than by refusal.
static func at(n: int, vox: VoxRef, m: Ori, u: int, v: int, hint_depth,
		world: int, lock_map, mirror: VoxRef = null):
	var cells := vox.cells.size()
	var ev := PoolByteArray()
	ev.resize(cells)
	for i in range(cells):
		ev[i] = Level.eff_type(vox.cells[i], world)

	# WHICH CELL WINS THIS COLUMN, and it has to be Projection.project's answer
	# rather than a paraphrase of it — the carve is laying down footing that the
	# solver will later be asked to stand on, and if the two disagree about which
	# cell is the surface then the carve is guaranteeing a route through a cell
	# nobody can reach.
	#
	# So this is that comparison, term for term: a glyph beats a non-glyph, and
	# between two of the same kind the depth decides — NEAREST, ALWAYS. This
	# carried a second clause — farthest when the polarity bit was set — because
	# eversion used to be a reversal of this comparison. It is a second ARRAY of
	# types over the same cells now, so it arrives already resolved in `ev` and
	# there is one rule here again.
	#
	# IT WALKS THE VIEW MAP RATHER THAN PROJECTING EACH CELL. The C# calls
	# view_of once per cell per carve, which is three dot products and a rounding
	# apiece and was most of the generator's cost here; the map is the same
	# numbers, built once per (size, orientation) and shared with the projection
	# itself. Index order is unchanged — the C# loops y, z, x, which IS index
	# order — so the "first one to win" tie-break resolves identically.

	var map := Projection.view_map(n, m)
	var nn := n * n
	var best := 0
	var best_glyph := false
	var have_bw := false
	var bw := Vector3.ZERO

	for i in range(cells):
		if ev[i] == Level.VOID:
			continue
		var e: int = map[i]
		var vz := e % n
		var k := (e - vz) / n
		if k / n != u or k % n != v:
			continue

		var g := Level.is_glyph(ev[i])
		var win: bool = (not have_bw) or (g and not best_glyph) \
				or (g == best_glyph and vz > best)
		if not win:
			continue

		best = vz
		best_glyph = g
		var yy := i / nn
		var rr := i - yy * nn
		var zz := rr / n
		bw = Vector3(rr - zz * n, yy, zz)
		have_bw = true

	var want := Level.base_for_walk(world)

	if have_bw:
		var bi := Level.vidx_v(n, bw)
		if Level.is_glyph(vox.cells[bi]):
			return bw                                   # a plate is already footing
		if lock_map != null and lock_map.has(bi) and lock_map[bi] != want:
			return null
		vox.cells[bi] = want
		if lock_map != null:
			lock_map[bi] = want
		return bw

	# Nothing in that column at all: place a cell at the hinted depth. This is
	# the one move that can hide something from another face, which is what the
	# verify pass exists for — and the one move an everter's face array is not
	# allowed to make alone.
	var d: int = int(max(0, min(n - 1, hint_depth if hint_depth != null else (n >> 1))))
	var wc := Projection.world_of(n, m, Vector3(u, v, d))
	var wi := Level.vidx_v(n, wc)
	if lock_map != null and lock_map.has(wi) and lock_map[wi] != want:
		return null
	vox.cells[wi] = want
	# and the other face gains the same cell, as bedrock — see mirror
	if mirror != null and wi < mirror.cells.size() and mirror.cells[wi] == Level.VOID:
		mirror.cells[wi] = Level.LATTICE
	if lock_map != null:
		lock_map[wi] = want
	return wc


# THE COPY WAS TAKEN BEFORE THE CARVE, AND THE CARVE STILL ADDS CELLS.
#
# An everter's face array is filled from the primary's occupancy at the start,
# but the primary carve may then place a cell in an empty column to give itself
# footing — so by the end the two can disagree about which cells exist, which is
# the one thing an everter must never do.
#
# Every such cell is mirrored in as BEDROCK. It cannot break the face route,
# which is made of trace: the worst it does is put a dead square on the everted
# board, and a dead square is what most of that board is. The reverse cannot
# happen, because the face array is carved with creation denied.
static func mirror_occupancy(vox: VoxRef, alts: Array, locked: bool) -> void:
	if not locked:
		return
	for a in alts:
		var lim: int = min(vox.cells.size(), a.cells.size())
		for i in range(lim):
			if vox.cells[i] != Level.VOID and a.cells[i] == Level.VOID:
				a.cells[i] = Level.LATTICE
			elif vox.cells[i] == Level.VOID and a.cells[i] != Level.VOID:
				a.cells[i] = Level.VOID
