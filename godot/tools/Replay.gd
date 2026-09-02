extends Reference
# THE GAME'S TRANSITION, OUTSIDE THE GAME.
#
# Three tools now need to move a SolveState by one move, and the solver's own
# copy of that arithmetic is buried inside its search loop where nothing else can
# reach it. This is that arithmetic, once, so the tools cannot drift from each
# other — and tools/audit.gd deliberately keeps its own second copy and checks it
# by replaying all 150 optimal lines onto the core, which is what says this one
# is right.

var _surf_cache := {}


func fresh() -> void:
	_surf_cache.clear()


func surf(lv: Level, ori: int, world: int):
	var k := ori * 32 + world
	var got = _surf_cache.get(k)
	if got == null:
		got = Projection.project(lv.n, lv.eff(world), Turns.ori(ori))
		_surf_cache[k] = got
	return got


func start_state(lv: Level) -> SolveState:
	var s := SolveState.new(lv.start, 0, 0, 0, 0)
	var s0: int = lv.key_index_at(lv.start)
	if s0 >= 0:
		s.kmask = 1 << s0
	s.world = lv.glyph_at(lv.start)
	return s


func apply(lv: Level, s: SolveState, is_turn: bool, dir: int):
	var n: int = lv.n
	var m: Ori = Turns.ori(s.ori)
	if not is_turn:
		var v: Vector3 = Projection.view_of(n, m, s.pos)
		var sf = surf(lv, s.ori, s.world)
		var u2: int = int(v.x) + Turns.DX[dir]
		var v2: int = int(v.y) + Turns.DY[dir]
		if u2 < 0 or v2 < 0 or u2 >= n or v2 >= n:
			return null
		var raw: Surf = sf[u2 * n + v2]
		if not raw.has or not Level.is_walk_type(raw.t):
			return null
		var nd: int = s.doors
		var di: int = lv.door_index_at(raw.w)
		if di >= 0 and (s.doors & (1 << di)) == 0:
			if Solver.popcount(s.kmask) - Solver.popcount(s.doors) < 1:
				return null
			nd = s.doors | (1 << di)
		var nk: int = s.kmask
		var ki: int = lv.key_index_at(raw.w)
		if ki >= 0:
			nk |= 1 << ki
		return SolveState.new(raw.w, s.ori, nk, nd, s.world ^ Level.glyph_bit(raw.t))

	var m2 := Turns.apply(dir, m)
	var oi2: int = Turns.ori_index(m2)
	var sf2 = surf(lv, oi2, s.world)
	var lv2: Vector3 = Projection.view_of(n, m2, s.pos)
	var land = Projection.walkable(lv, sf2, int(lv2.x), int(lv2.y), s.doors)
	if land == null:
		return null
	var nk2: int = s.kmask
	var ki2: int = lv.key_index_at(land.w)
	if ki2 >= 0:
		nk2 |= 1 << ki2
	return SolveState.new(land.w, oi2, nk2, s.doors, s.world)


# CAN THE PLAYER SEE THE CORE FROM HERE, and how far across the face is it?
#
# Seeing it is not a detail — CubeView._shown refuses the exit marker unless the
# cell is on the surface, so a core behind a wall of lattice is not on the board
# as far as the person holding the phone is concerned. Everything that reasons
# about what a player would DO has to ask this first.
func gap(lv: Level, s: SolveState) -> Array:
	var n: int = lv.n
	var m: Ori = Turns.ori(s.ori)
	var a: Vector3 = Projection.view_of(n, m, s.pos)
	var b: Vector3 = Projection.view_of(n, m, lv.goal)
	var sf = surf(lv, s.ori, s.world)
	var k: int = int(b.x) * n + int(b.y)
	var shown := false
	if k >= 0 and k < sf.size():
		var raw: Surf = sf[k]
		shown = raw.has and raw.w == lv.goal
	return [int(abs(a.x - b.x) + abs(a.y - b.y)), shown]


# How much of the solid is on the face right now — the surface a player is
# reading. Used as the reward for a player who cannot see the core yet and is
# turning the thing over to find it.
func face_key(lv: Level, s: SolveState) -> String:
	var n: int = lv.n
	var sf = surf(lv, s.ori, s.world)
	var out := PoolByteArray()
	for i in range(sf.size()):
		var raw: Surf = sf[i]
		out.append(0 if not raw.has else int(raw.t))
	return Marshalls.raw_to_base64(out)
