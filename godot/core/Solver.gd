class_name Solver
# 0-1 BFS. A step costs nothing, a fold costs one — so the first time the core
# is reached is the FEWEST FOLDS it can possibly be done in, which is the number
# a cube is graded and filtered on, and the number on the HUD.
#
# Buckets rather than a deque because the cost never exceeds a couple of dozen.

const DEFAULT_BUDGET := 40


static func popcount(x: int) -> int:
	var c := 0
	while x != 0:
		x &= x - 1
		c += 1
	return c


# THE VISITED SET'S KEY, AND IT HAS TO BE THE WHOLE STATE.
#
# Five fields pack into one integer: where you stand, how the engine is folded,
# which nodes are taken, which locks are open, and which world you are in. Two
# states that differ in any of them are different states, and a key that cannot
# tell them apart is a search that visits one and marks the other done.
#
# WORLD GETS FIVE BITS BECAUSE THERE ARE FIVE OF THEM. It had two, from when a
# world was the two plate bits and nothing else — and then eversion added a
# third (4) and the trigger added two more (8, 16), and this line was not
# widened either time. The bits did not vanish; they landed on `doors`. An
# everted cube with no locks open hashed identically to an uneverted one with
# lock 1 open, so the search reached the second, wrote the first off as seen,
# and reported a par that was too high by however many folds the discarded route
# saved.
#
# It was wrong on five of the five hundred — every one of them a cube with both
# a trigger and a lock, which is the only place the collision is reachable — and
# cube 500, the last one in the game, was one of them. The projection cache
# below carries the same warning in its own comment and was widened twice; this
# was missed both times, because a narrow cache hands back a wrong picture that
# somebody sees and a narrow visited key hands back a wrong NUMBER that looks
# fine.
#
# Public because RemainSolver keys its cache on the same state and used to do it
# with its own copy of this expression. That is how the two drifted; there is one
# of them now.
static func state_key(n: int, s: SolveState) -> int:
	var v := Level.vidx_v(n, s.pos)
	return ((((v * 24 + s.ori) << 8) | s.kmask) << 13) | (s.doors << 5) | (s.world & 31)


static func solve(lv: Level, budget: int = DEFAULT_BUDGET, from = null) -> SolveResult:
	var n: int = lv.n

	# Keys are tracked as a bitmask of WHICH ones are taken, never a count — a
	# count lets the search step off a key cell and back on to mint another one.
	var start: SolveState
	if from != null:
		start = from
	else:
		start = SolveState.new(lv.start, 0, 0, 0, 0)
		var s0 := lv.key_index_at(lv.start)
		if s0 >= 0:
			start.kmask = 1 << s0
		# Opening on a plate would fire it before the first frame. Generation
		# never does that; a hand-written cube might.
		start.world = lv.glyph_at(lv.start)

	var buckets := []
	var seen := {}
	var proj_cache := {}
	var parent := {}
	var truncated := [false]

	_relax(n, 0, start, 0, null, false, budget, seen, parent, buckets, truncated)

	for cost in range(0, budget + 1):
		if cost >= buckets.size():
			continue
		var q = buckets[cost]
		if q == null:
			continue

		# q grows while we walk it — intended; free steps land here.
		var qi := 0
		while qi < q.size():
			var s: SolveState = q[qi]
			qi += 1
			var sk := state_key(n, s)
			if seen[sk] < cost:
				continue
			if s.pos == lv.goal:
				return _report(cost, sk, parent)

			var m: Ori = Turns.ori(s.ori)

			# THIRTY-TWO WORLDS PER ORIENTATION: three material bits and two
			# layout bits. This was widened from four to eight when eversion
			# arrived and it is the same trap — a cache keyed narrower than the
			# world it is caching silently hands back another world's projection.
			var ck := s.ori * 32 + s.world
			var surf = proj_cache.get(ck)
			if surf == null:
				surf = Projection.project(n, lv.eff(s.world), m)
				proj_cache[ck] = surf

			var v := Projection.view_of(n, m, s.pos)

			# steps — free
			for t in range(4):
				var u2: int = int(v.x) + Turns.DX[t]
				var v2: int = int(v.y) + Turns.DY[t]
				if u2 < 0 or v2 < 0 or u2 >= n or v2 >= n:
					continue
				var raw: Surf = surf[u2 * n + v2]
				if not raw.has or not Level.is_walk_type(raw.t):
					continue

				var nd := s.doors
				var di := lv.door_index_at(raw.w)
				if di >= 0 and (s.doors & (1 << di)) == 0:
					# a lock you can open is a step, not a wall
					if popcount(s.kmask) - popcount(s.doors) < 1:
						continue
					nd = s.doors | (1 << di)

				var nk := s.kmask
				var ki := lv.key_index_at(raw.w)
				if ki >= 0:
					nk |= 1 << ki

				# a plate fires under the foot that lands on it
				var nw: int = s.world ^ Level.glyph_bit(raw.t)

				_relax(n, cost, SolveState.new(raw.w, s.ori, nk, nd, nw),
						sk, Act.new(false, t), true, budget, seen, parent, buckets, truncated)

			# folds — one each. A plate you FOLD onto does not fire: you press it
			# with a foot, and rotating the engine is not pressing it. That also
			# keeps the landing just checked from being invalidated by the world
			# changing underneath it.
			for t2 in range(4):
				var m2 := Turns.apply(t2, m)
				# THE SAME QUESTION Projection.landing ASKS, THROUGH THE CACHE.
				#
				# landing() projects the whole cube every time it is called, and
				# it is called four times per state — so a search that already
				# holds every projection it needs was rebuilding one for each
				# fold it considered. This is landing() written out against the
				# cache above: project, look up the square the player's cell
				# lands on, ask whether it is walkable. Identical answer, and it
				# is most of the difference between a mint that finishes and one
				# that does not.
				var oi2 := Turns.ori_index(m2)
				var ck2 := oi2 * 32 + s.world
				var surf2 = proj_cache.get(ck2)
				if surf2 == null:
					surf2 = Projection.project(n, lv.eff(s.world), m2)
					proj_cache[ck2] = surf2
				var lv2 := Projection.view_of(n, m2, s.pos)
				var land = Projection.walkable(lv, surf2, int(lv2.x), int(lv2.y), s.doors)
				if land == null:
					continue
				var nk2 := s.kmask
				var ki2 := lv.key_index_at(land.w)
				if ki2 >= 0:
					nk2 |= 1 << ki2
				_relax(n, cost + 1,
						SolveState.new(land.w, oi2, nk2, s.doors, s.world),
						sk, Act.new(true, t2), true, budget, seen, parent, buckets, truncated)

	var miss := SolveResult.new()
	miss.ok = false
	miss.over = truncated[0]
	return miss


# `truncated` is a one-element array because GDScript has no `ref` parameter and
# the flag has to survive back to the caller — a search cut short by the budget
# is an honest unknown and must not be reported as "no route".
static func _relax(n: int, cost: int, s: SolveState, from_key: int, act,
		has_from: bool, budget: int, seen: Dictionary, parent: Dictionary,
		buckets: Array, truncated: Array) -> void:
	if cost > budget:
		truncated[0] = true
		return
	var k := state_key(n, s)
	if not seen.has(k) or seen[k] > cost:
		seen[k] = cost
		parent[k] = [from_key, act, has_from]
		while buckets.size() <= cost:
			buckets.append(null)
		if buckets[cost] == null:
			buckets[cost] = []
		buckets[cost].append(s)


# Walk the parent chain back from the core. A hint needs only the very first
# action — the rest is the player's to find — but the LENGTH of the chain is the
# second difficulty axis the vault curve scales on, so it is measured here where
# the answer is in hand.
static func _report(cost: int, end_key: int, parent: Dictionary) -> SolveResult:
	var chain := []
	var k := end_key
	var guard := 0
	while parent.has(k) and parent[k][2] and guard < 8000:
		guard += 1
		chain.append(parent[k][1])
		k = parent[k][0]
	chain.invert()
	var steps := 0
	for a in chain:
		if not a.is_turn:
			steps += 1
	var r := SolveResult.new()
	r.ok = true
	r.turns = cost
	r.steps = steps
	r.first = chain[0] if chain.size() > 0 else null
	r.has_first = chain.size() > 0
	r.line = chain
	return r
