class_name Generator
# Carving the SOLUTION first, then filling in around it, is the only way this is
# tractable: a random cube is a random cube, but a cube built by walking and
# folding is guaranteed to contain at least the route that built it. The solver
# then reports the true optimum, which is usually shorter than the carve — and
# that difference is where the good cubes live.

const CAP_LOCKS := 3
const GLYPH_FROM := 20


# ---- the difficulty contract ---------------------------------------------

static func glyph_plan(level: int) -> Array:
	if level < GLYPH_FROM:
		return [0, 0]
	var band := (level - 1) / 10
	# ONE plate, always. A second lowers par (more worlds to shortcut through)
	# and costs a chunk of search time for the privilege. Variety past vault V
	# comes from WHICH plate it is, not how many.
	return [1, 2 if band >= 4 else 1]


static func spec_for(level: int) -> Spec:
	var band := (level - 1) / 10
	var w := (level - 1) % 10
	var b: int = min(band, 11)   # locks and decoys saturate; SIZE no longer does

	# THE SIZE LADDER IS WHERE THE DIFFICULTY ACTUALLY LIVES. Par counts FOLDS,
	# and folds live in a 24-orientation group whose graph has a diameter of
	# four, so pure rotation distance can never ask for more than four.
	# Everything above that comes from FOOTING: you cannot fold where you have
	# nowhere to stand, so a route gets forced through orientations it did not
	# want. More surface is more places to force it, which is why size is the
	# lever.
	var n := 5 if band < 1 else (6 if band < 3 else (7 if band < 8 else (8 if band < 15 else 9)))

	var gp := glyph_plan(level)

	var par_lo := 1 if n == 5 else 2
	# The band has to widen with the cube or the ceiling is still six:
	# solve(lv, par_hi) is a BOUNDED search, so a candidate harder than par_hi is
	# not scored lower, it is DISCARDED.
	var par_span := 4 if n <= 6 else (5 if n == 7 else (6 if n == 8 else 8))
	var carve_turns: int = min(9, 3 + b + (1 if gp[0] != 0 else 0))

	var s := Spec.new()
	s.n = n
	s.glyphs = gp[0]
	s.glyph_kinds = gp[1]
	s.turns = carve_turns
	# locks multiply the search by 2^locks and worlds already multiply it by
	# four; a cube carrying a plate keeps its lock count down so a mint stays
	# inside a couple of hundred milliseconds on a phone
	s.locks = int(min(2 if gp[0] != 0 else CAP_LOCKS, b / 2 + (1 if w >= 6 else 0)))
	s.density = min(0.62, 0.42 + 0.021 * b + 0.004 * w)
	# Leg length is deliberately NOT scaled with the vault: a longer carve lays
	# down more trace, more trace means more shortcuts, and par falls while route
	# length barely moves.
	s.leg_min = 2 + n / 3
	s.leg_max = 4 + n / 2
	s.par_lo = par_lo
	s.par_hi = par_lo + par_span
	s.min_steps = int(min(5 + band * 2 + w, n * 3))
	# HOW MANY CANDIDATES TO LOOK AT, AS A COUNT AND NEVER AS A CLOCK. A
	# millisecond budget meant a faster machine got through more candidates and
	# therefore chose a DIFFERENT winner, so one cube number was two different
	# puzzles on two devices. Work is counted, not timed: every device does
	# exactly this many passes.
	s.tries = 24 if gp[0] != 0 else 40
	s.decoys = 3 if gp[0] != 0 else 4
	s.band = band
	s.level = level
	return s


# ---- the carve ------------------------------------------------------------
#
# THE CARVE ITSELF LIVES IN Carve, because the Walker below has to call it and
# GDScript will not let a nested class name its own outer class. These two
# forwarders keep the C# call sites — Generator.carve, Generator.mirror_occupancy
# — reading the way they read there.

static func carve(n: int, vox: VoxRef, m: Ori, u: int, v: int, hint_depth,
		world: int, lock_map, mirror: VoxRef = null):
	return Carve.at(n, vox, m, u, v, hint_depth, world, lock_map, mirror)


# THE WALK, as an object, because the C# built it out of closures over local
# state and GDScript 3.5 has none. Every field below is one of those locals and
# every method one of those closures; nothing about the algorithm changed.
class Walker:
	var n: int
	var vox: VoxRef
	var alts: Array
	var lock_occupancy: bool
	var rng
	var m: Ori
	var pos: Vector3
	var world := 0
	var lock_map := {}
	var path := []
	var order := [0, 1, 2, 3]

	# WHICH ARRAY IS BEING CARVED: the layout the walk is standing in.
	func solid(w: int) -> VoxRef:
		var l := Level.layout_of(w)
		return alts[l - 1] if l > 0 and l <= alts.size() else vox

	# WHICH ARRAY HAS TO BE KEPT IN STEP with the one being carved. Null on
	# every cube that is not an everter, so nothing else changes.
	func mirror(w: int):
		if not lock_occupancy or alts.empty():
			return null
		return vox if Level.layout_of(w) > 0 else alts[0]

	func walk_once() -> bool:
		order[0] = 0
		order[1] = 1
		order[2] = 2
		order[3] = 3
		rng.shuffle(order)
		var vv := Projection.view_of(n, m, pos)
		for t in range(4):
			var u2: int = int(vv.x) + Turns.DX[order[t]]
			var v2: int = int(vv.y) + Turns.DY[order[t]]
			if u2 < 0 or v2 < 0 or u2 >= n or v2 >= n:
				continue
			var nx = Carve.at(n, solid(world), m, u2, v2, int(vv.z), world, lock_map, mirror(world))
			if nx == null:
				continue
			pos = nx
			path.append(pos)
			return true
		return false


static func generate(seed_in: int, opt: Spec):
	var rng := Rng.mulberry32(seed_in)
	var n: int = opt.n
	var cells := n * n * n

	# THE SECOND SOLID IS CARVED THE SAME WAY AS THE FIRST, by the same walk, in
	# the legs that happen after the trigger fires. carve already takes the array
	# it writes to as an argument, so the whole of "carve into the other layout"
	# is choosing which one to hand it — the route crosses between them exactly
	# as it crosses between plate worlds, and the guarantee that comes with it is
	# the same: footing is CONSTRUCTED on both sides rather than hoped for.
	var n_layouts: int = int(max(1, min(4, opt.layouts)))
	var alts := []
	for _i in range(n_layouts - 1):
		alts.append(VoxRef.new(VoxRef.void_cells(cells, Level.VOID)))

	var lock_occupancy: bool = opt.glyph_set != "" and opt.glyph_set.find("E") >= 0

	var vox := VoxRef.new(VoxRef.void_cells(cells, Level.VOID))
	for i in range(cells):
		vox.cells[i] = Level.LATTICE if rng.next() < opt.density else Level.VOID

	# THE SECOND SOLID IS FILLED HERE OR IT IS NOT FILLED AT ALL. A fresh array
	# is zeroes, not '.', so a later pass written as `if (voxB[i] == '.')` never
	# fires and the array ships full of nulls — which encode as void, look like
	# void to the projection, and are not void to a byte comparison. The
	# share-code check caught it: two hundred and two cells of a two hundred and
	# sixteen cell cube differing between the level and its own round trip.
	#
	# Guarded, so a single-layout cube draws exactly the numbers it drew before
	# this existed. Every cube already minted is byte-identical.
	#
	# AND AN EVERTER'S SECOND ARRAY IS NOT DRAWN, IT IS COPIED. A trigger
	# exchanges the ground: its second solid is its own roll of the dice, so half
	# the cells that were there are not, and the board's silhouette moves with
	# them. An everter does not exchange anything — every cell stays exactly
	# where it is and turns to show a different face — so the second array
	# carries the SAME OCCUPANCY and differs only in what each cell is. Bedrock
	# everywhere to begin with; the carve then lays the face route through it.
	for a in alts:
		for i in range(cells):
			if lock_occupancy:
				a.cells[i] = Level.VOID if vox.cells[i] == Level.VOID else Level.LATTICE
			else:
				a.cells[i] = Level.LATTICE if rng.next() < opt.density else Level.VOID

	var wk := Walker.new()
	wk.n = n
	wk.vox = vox
	wk.alts = alts
	wk.lock_occupancy = lock_occupancy
	wk.rng = rng
	wk.m = Ori.new()

	var n_glyph: int = opt.glyphs
	var kinds: int = 1 if opt.glyph_kinds <= 0 else opt.glyph_kinds
	var placed := []

	# the legs a plate is dropped on, spread through the route so the world
	# changes in the MIDDLE of solving rather than at one end
	var leg_count: int = opt.turns + 1
	var glyph_legs := {}
	for i in range(n_glyph):
		glyph_legs[int(max(1, floor(float(leg_count) * (i + 1) / (n_glyph + 1) + 0.5)))] = true

	var u0 := 1 + int(floor(rng.next() * (n - 2)))
	var v0 := 1 + int(floor(rng.next() * (n - 2)))
	var p0 = Carve.at(n, wk.solid(0), wk.m, u0, v0, null, 0, wk.lock_map, wk.mirror(0))
	if p0 == null:
		return null
	wk.pos = p0
	wk.path.append(p0)

	for leg in range(opt.turns + 1):
		var steps: int = opt.leg_min + int(floor(rng.next() * (opt.leg_max - opt.leg_min + 1)))
		for _st in range(steps):
			if not wk.walk_once():
				break

		# Drop a plate under the foot standing here, and carry on carving in the
		# world it opens. Without that continuation the plate lands on the final
		# leg and the core ends up ON it, which is rejected — so every candidate
		# died and the vault fell through to fallback.
		if glyph_legs.has(leg) and placed.size() < n_glyph and wk.path.size() > 2:
			var gi := Level.vidx_v(n, wk.pos)
			var kind: int
			if opt.glyph_set != "":
				kind = ord(opt.glyph_set[int(rng.next() * opt.glyph_set.length()) % opt.glyph_set.length()])
			else:
				kind = Level.PLATE_B if (kinds > 1 and rng.next() < 0.45) else Level.PLATE_A

			# A TRIGGER HAS TO EXIST IN BOTH SOLIDS OR IT IS A ONE-WAY DOOR. The
			# cell is written into whichever layout the walk is standing in AND
			# into the other one at the same index, so the swap can be undone by
			# stepping back onto it — the same promise a plate makes, and the
			# reason a plate is a decision rather than a commitment you cannot
			# inspect.
			wk.solid(wk.world).cells[gi] = kind
			#
			# AND SO DOES AN EVERTER, for a reason that is even sharper. The cell
			# you are standing on is the one turning; if it is not walkable on
			# the other face you are standing on nothing the instant the turn
			# lands, and the route dies there. This condition read 'T' and 'U'
			# only, so an everter was written into the primary and left as
			# bedrock in the face array — and every single everter cube failed to
			# mint. Four thousand six hundred and eighty of four thousand six
			# hundred and eighty, which is the kind of number that looks like a
			# yield problem right up until you print it.
			if (kind == Level.TRIGGER or kind == Level.TRIGGER2 or kind == Level.EVERTER) and not alts.empty():
				vox.cells[gi] = kind
				for a in alts:
					a.cells[gi] = kind
			wk.lock_map[gi] = kind
			placed.append([wk.pos, kind])
			wk.world ^= Level.glyph_bit(kind)

			# and the landing cell has to exist in the world we just opened, or
			# the walk continues from somewhere it is not
			var vv := Projection.view_of(n, wk.m, wk.pos)
			var kept = Carve.at(n, wk.solid(wk.world), wk.m, int(vv.x), int(vv.y), null,
					wk.world, wk.lock_map, wk.mirror(wk.world))
			if kept == null:
				return null
			wk.pos = kept

			var after := 2 + int(floor(rng.next() * 3))
			for _ea in range(after):
				if not wk.walk_once():
					break

		if leg == opt.turns:
			break

		# fold, then guarantee footing on the far side of it
		var pick := int(floor(rng.next() * 4))
		var m2 := Turns.apply(pick, wk.m)
		var lv2 := Projection.view_of(n, m2, wk.pos)
		var lnd = Carve.at(n, wk.solid(wk.world), m2, int(lv2.x), int(lv2.y), null,
				wk.world, wk.lock_map, wk.mirror(wk.world))
		if lnd == null:
			continue
		wk.m = m2
		wk.pos = lnd
		wk.path.append(lnd)

	var goal: Vector3 = wk.path[wk.path.size() - 1]
	var start: Vector3 = wk.path[0]

	if start == goal:
		return null
	# Neither end may be a plate: one would fire before the first frame, the
	# other would change the world at the moment the cube ends.
	if Level.is_glyph(vox.cells[Level.vidx_v(n, start)]):
		return null
	if Level.is_glyph(vox.cells[Level.vidx_v(n, goal)]):
		return null
	# and the goal has to be somewhere in the layout it is reached in
	for a in alts:
		if a.cells[Level.vidx_v(n, goal)] == Level.VOID:
			a.cells[Level.vidx_v(n, goal)] = Level.TRACE
	if placed.size() != n_glyph:
		return null

	Carve.mirror_occupancy(vox, alts, lock_occupancy)

	var lv := Level.new()
	lv.n = n
	lv.vox = vox.cells
	for a in alts:
		lv.alts.append(a.cells)
	lv.start = start
	lv.goal = goal
	lv.glyphs = placed
	lv.lock_map = wk.lock_map

	# Keys and locks go on the route, but NEVER on a plate. A shut lock sitting
	# on a plate blocks the plate's own column, which destroys the one property
	# plates are built around — that you can always fold from one.
	for i in range(opt.locks):
		var kw = _slot_near(lv, wk.path, int(floor(wk.path.size() * (0.18 + 0.22 * i))))
		var dw = _slot_near(lv, wk.path, int(floor(wk.path.size() * (0.55 + 0.2 * i))))
		if kw == null or dw == null or kw == dw:
			continue
		lv.keys.append(kw)
		lv.doors.append(dw)

	return lv


static func _slot_near(lv: Level, path: Array, idx: int):
	for off in range(path.size()):
		for sgn in [-1, 1]:
			var j: int = idx + off * sgn
			if j < 1 or j > path.size() - 2:
				continue
			var c: Vector3 = path[j]
			if Level.is_glyph(lv.vox[Level.vidx_v(lv.n, c)]):
				continue
			if c == lv.start or c == lv.goal:
				continue
			if lv.key_index_at(c) >= 0 or lv.door_index_at(c) >= 0:
				continue
			return c
	return null


# WIDENING. The carve leaves a single thread: from any face you see the one
# route and walk it. Dead ends are what make a face worth reading, so a few
# extra surface cells are promoted to trace at random across random faces — then
# the cube is re-solved and kept only if the answer did not get any cheaper.
# Difficulty from plausible wrong turns, never hidden ones.
static func widen(rng, lv: Level, count: int, lock_map) -> void:
	var n: int = lv.n
	var pool := []
	for _c in range(count):
		var m: Ori = Turns.ori(int(floor(rng.next() * 24)))
		var surf := Projection.project(n, lv.eff(0), m)
		pool.clear()
		for i in range(surf.size()):
			var s: Surf = surf[i]
			if s.has and s.t == Level.LATTICE:
				pool.append(s.w)
		if pool.empty():
			continue
		var w: Vector3 = pool[int(floor(rng.next() * pool.size()))]
		var wi := Level.vidx_v(n, w)
		if Level.is_glyph(lv.vox[wi]):
			continue
		if lock_map != null and lock_map.has(wi):
			continue                                  # a route in some world needs this
		lv.vox[wi] = Level.TRACE
		lv.clear_eff()


# ---- scoring and the floor ------------------------------------------------

# How well a candidate answers the demand. Being too EASY is a much smaller sin
# than being too hard or malformed, because a cube below par still plays — it
# just under-delivers.
#
# TURNS DOMINATE, and by a wide margin. Par is the number on the HUD and the
# number the player is playing against; route length only breaks ties between
# cubes of equal par.
static func score(turns: int, steps: int, fill: float, spec: Spec) -> int:
	if fill < 0.24 or fill > 0.80:
		return 50
	if turns > spec.par_hi:
		return 200                                    # overshoot: usable, not wanted
	if turns < spec.par_lo:
		return 100 + turns * 10 + int(min(60, steps))
	return 1000 + turns * 300 + int(min(90, steps)) + (40 if steps >= spec.min_steps else 0)


# The floor under everything: a cube that cannot fail to be solvable, because it
# is a straight line of traces in open space and nothing else. It is
# deliberately boring. It exists so that "every cube the player is handed has
# been proved winnable" has no exceptions — not almost none, none. The tests
# assert it never actually fires.
static func fallback_level(level: int) -> Level:
	var n := 5
	var vox := PoolByteArray()
	vox.resize(n * n * n)
	for i in range(vox.size()):
		vox[i] = Level.VOID
	for i in range(n):
		vox[Level.vidx_n(n, i, 2, 2)] = Level.TRACE
	var lv := Level.new()
	lv.n = n
	lv.vox = vox
	lv.start = Vector3(0, 2, 2)
	lv.goal = Vector3(n - 1, 2, 2)
	lv.par = 0
	lv.level = level
	lv.band = (level - 1) / 10
	lv.fallback = true
	return lv


# MINT — the only way a cube ever reaches a player.
#
# Solvability is not filtered for, it is CONSTRUCTED. The solver's job is the
# other half: proving the route survived the decoy pass, that the opening cell
# is not buried, that the keys really do open the locks in some order, and
# reporting the true minimum, which becomes par.
#
# Then the gate: whatever wins, the cube is solved one final time before it is
# handed over, and anything that fails falls back. There is no path through this
# function that returns an unwinnable cube — not on a bad seed, not when the
# budget expires, not at level nine million.
static func mint(level: int, seed_override = null) -> Level:
	var spec := spec_for(level)
	var seed_in: int = seed_override if seed_override != null else Rng.hash_seed(level)

	var best = null
	var best_score := -1
	var tries := 0

	# BOTH SEEDS COME OFF THE SAME `i`, which is why the loop is written out
	# rather than as a `for`: a `continue` must still advance it, exactly as the
	# C# `for` header does, or the candidate stream repeats itself.
	var i := -1
	while true:
		i += 1
		if i >= spec.tries * 14 or tries >= spec.tries:
			break
		var lv = generate((seed_in + i * 7919) & 0xFFFFFFFF, spec)
		if lv == null:
			continue
		if lv.keys.size() != spec.locks:
			continue

		var wrng := Rng.mulberry32((seed_in + i * 104729) & 0xFFFFFFFF)
		widen(wrng, lv, spec.decoys, lv.lock_map)

		lv.clear_eff()
		var s0 := Projection.project(lv.n, lv.eff(0), Ori.new())
		if not Projection.surface_at(lv.n, s0, Ori.new(), lv.start):
			continue                                  # opened buried
		if lv.door_index_at(lv.start) >= 0 or lv.door_index_at(lv.goal) >= 0:
			continue

		# One bounded solve does all the work: past par_hi it stops looking, so a
		# cube that is too hard costs no more than one that is right.
		tries += 1
		var r := Solver.solve(lv, spec.par_hi)
		if not r.ok:
			continue

		var solid := 0
		for k in range(lv.vox.size()):
			if lv.vox[k] != Level.VOID:
				solid += 1
		var sc := score(r.turns, r.steps, float(solid) / lv.vox.size(), spec)
		if sc > best_score:
			best_score = sc
			best = lv
			best.par = r.turns
			best.steps = r.steps

		# Stop only on a cube that maxes the band on BOTH axes. Anything less and
		# the remaining budget is better spent looking: the first in-band
		# candidate is usually the weakest one that qualifies.
		if r.turns >= spec.par_hi and r.steps >= spec.min_steps:
			break

	if best == null:
		best = fallback_level(level)

	var gate := Solver.solve(best, 60)                # THE GATE
	if not gate.ok:
		best = fallback_level(level)
		gate = Solver.solve(best, 60)
	best.par = gate.turns
	best.steps = gate.steps
	best.level = level
	best.band = spec.band
	return best
