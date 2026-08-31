class_name Projection
# THE COLLAPSE. This is the whole game.
#
# Every cell sharing a screen column projects to the same square, and the same
# square is ONE PLACE. So a column is represented by exactly one cell: the
# nearest solid one to the camera. Everything behind it is discarded — not
# hidden, discarded. Two traces a hundred cells apart in the world are
# neighbours on screen if their columns are neighbours, and you may walk
# between them as if they touched, because on screen they do.
#
# EVERYTHING HERE IS STATIC, and the view-map cache is a `const` Dictionary —
# see the note in Turns for why that is where a table lives in GDScript 3.5.

# World <-> view. Both are integer round trips: c is the cube's centre and
# cancels out, so view_of(world_of(v)) == v exactly.
#
# Every value reaching _js_round here is already an exact integer — an
# orientation is a signed permutation, so each dot product selects a single
# component, and for even n the two half-integers cancel. The rounding is belt
# and braces against float drift, and it uses JS's definition
# (floor(x + 0.5), ties toward +infinity) rather than C#'s banker's or
# away-from-zero, so the implementations cannot part company even if a value
# ever did land on a half.
static func _js_round(x: float) -> int:
	return int(floor(x + 0.5))


static func view_of(n: int, m: Ori, p: Vector3) -> Vector3:
	var c := (n - 1) / 2.0
	var qx := p.x - c
	var qy := p.y - c
	var qz := p.z - c
	return Vector3(
		_js_round(m.r.x * qx + m.r.y * qy + m.r.z * qz + c),
		_js_round(m.u.x * qx + m.u.y * qy + m.u.z * qz + c),
		_js_round(m.f.x * qx + m.f.y * qy + m.f.z * qz + c))


static func world_of(n: int, m: Ori, v: Vector3) -> Vector3:
	var c := (n - 1) / 2.0
	var a := v.x - c
	var b := v.y - c
	var d := v.z - c
	return Vector3(
		_js_round(a * m.r.x + b * m.u.x + d * m.f.x + c),
		_js_round(a * m.r.y + b * m.u.y + d * m.f.y + c),
		_js_round(a * m.r.z + b * m.u.z + d * m.f.z + c))


# An orientation is a signed permutation, so where a cell lands on screen is
# fixed the moment the orientation is — it does not depend on the cube's
# contents at all. Recomputing it per cell per projection was most of the
# solver's cost: a search over 24 orientations x 32 worlds fills a lot of
# projections. The mapping is built once per (size, orientation) and reused for
# the life of the process.
const _VIEW_MAPS := {}


static func view_map(n: int, m: Ori) -> PoolIntArray:
	var key := n * 32 + Turns.ori_index(m)
	if _VIEW_MAPS.has(key):
		return _VIEW_MAPS[key]
	var map := PoolIntArray()
	map.resize(n * n * n)
	for y in range(n):
		for z in range(n):
			for x in range(n):
				var v := view_of(n, m, Vector3(x, y, z))
				map[Level.vidx_n(n, x, y, z)] = (int(v.x) * n + int(v.y)) * n + int(v.z)
	_VIEW_MAPS[key] = map
	return map


# THE COLLAPSE, AND IT HAS ONE RULE.
#
# This took an `evert` flag that reversed the depth test, so the same function
# answered two different questions about the same solid depending on a bit its
# callers had to remember to pass. Every call site that forgot got the near
# board for an everted world and no error anywhere.
#
# Eversion is a different ARRAY now rather than a different comparison, so it
# arrives here already resolved: eff(world) hands over the types the faces are
# showing and this does what it always did with them.
#
# A PLATE STILL WINS ITS COLUMN FROM EITHER SIDE. It is carved clean through
# the rock, so it is the surface whichever way you look.
static func project(n: int, vox: PoolByteArray, m: Ori) -> Array:
	var surf := []
	surf.resize(n * n)
	for i in range(n * n):
		surf[i] = Surf.new()
	var nn := n * n
	var map := view_map(n, m)
	for i in range(vox.size()):
		var t := vox[i]
		if t == Level.VOID:
			continue
		var e := map[i]
		var d := e % n
		var k := (e - d) / n
		var g := Level.is_glyph(t)
		var cur: Surf = surf[k]
		# A plate is carved clean through the rock, so it wins its column
		# outright; among equals the nearer one wins as always.
		if (not cur.has) or (g and not Level.is_glyph(cur.t)) \
				or (g == Level.is_glyph(cur.t) and d > cur.d):
			var yy := i / nn
			var rr := i - yy * nn
			var zz := rr / n
			cur.has = true
			cur.d = d
			cur.t = t
			cur.w = Vector3(rr - zz * n, yy, zz)
	return surf


# An object — key, lock, core, and the player's own footing — is attached to a
# cell, and exists in a projection only while that cell IS the surface of its
# column. Bury it behind lattice and it is gone until you fold.
static func surface_at(n: int, surf: Array, m: Ori, w: Vector3) -> bool:
	var v := view_of(n, m, w)
	var s: Surf = surf[int(v.x) * n + int(v.y)]
	return s.has and s.w == w


# May the player stand on view square (u,v)? Returns the surface cell, or null.
# A shut lock is a wall while you are looking at its face.
static func walkable(lv: Level, surf: Array, u: int, v: int, doors_open: int):
	if u < 0 or v < 0 or u >= lv.n or v >= lv.n:
		return null
	var s: Surf = surf[u * lv.n + v]
	if not s.has or not Level.is_walk_type(s.t):
		return null
	for i in range(lv.doors.size()):
		if (doors_open & (1 << i)) != 0:
			continue
		if s.w == lv.doors[i]:
			return null
	return s


# Folding is legal only when the column you are standing in still has footing
# after the fold. That single rule is what makes WHERE you stand gate WHICH
# folds you have — position gating rotation, rotation gating position. It is
# the lock and the key, falling out of the geometry rather than being placed on
# top of it.
static func landing(lv: Level, m2: Ori, pos: Vector3, doors_open: int, world: int):
	var surf2 := project(lv.n, lv.eff(world), m2)
	var v := view_of(lv.n, m2, pos)
	return walkable(lv, surf2, int(v.x), int(v.y), doors_open)
