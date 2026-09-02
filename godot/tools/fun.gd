extends SceneTree
# THE THREE THINGS THAT ACTUALLY PREDICT HOW HARD A PUZZLE FEELS.
#
# tools/audit.gd counts decisions, which is a structural claim: a fold where
# several things are legal and one is right. It cannot tell you whether the right
# one is HARD TO SEE, and that is the whole of the difference between a puzzle
# and a form to fill in.
#
# The nearest thing to real evidence on this is Jarusek and Pelanek's work on
# Sokoban, which timed people solving two thousand problems and correlated the
# times against computable properties of the boards. Three came out, in order:
#
#   problem decomposition     Spearman 0.82   can the sub-goals be done one at
#                                             a time, or must they be planned
#                                             together?
#   counterintuitive moves    Spearman 0.69   moves that take you AWAY from the
#                                             goal before they take you to it
#   solution length           Spearman 0.47   the weakest of the three, and the
#                                             only one this catalogue is minted
#                                             against
#
# Sokoban is not this game, so each has to be translated rather than copied:
#
#   counterintuitive   a fold after which the core is FURTHER AWAY across the
#                      face than it was, or has gone off the face entirely, and
#                      which is still on the optimal line. That is the fold you
#                      have to see past.
#
#   decomposition      solve it greedily, one nearest sub-goal at a time — take
#                      the closest key, then the next, then the core — and
#                      compare with par. Zero difference means the puzzle comes
#                      apart into independent errands.
#
#   transparent        can a player who simply always reduces the distance to
#                      the core solve it at par? If so there is nothing in it.
#
#   godot --path godot -s tools/fun.gd > fun.csv

var _surf_cache := {}


func _surf(lv: Level, ori: int, world: int):
	var k := ori * 32 + world
	var got = _surf_cache.get(k)
	if got == null:
		got = Projection.project(lv.n, lv.eff(world), Turns.ori(ori))
		_surf_cache[k] = got
	return got


func _apply(lv: Level, s: SolveState, is_turn: bool, dir: int):
	var n: int = lv.n
	var m: Ori = Turns.ori(s.ori)
	if not is_turn:
		var v: Vector3 = Projection.view_of(n, m, s.pos)
		var surf = _surf(lv, s.ori, s.world)
		var u2: int = int(v.x) + Turns.DX[dir]
		var v2: int = int(v.y) + Turns.DY[dir]
		if u2 < 0 or v2 < 0 or u2 >= n or v2 >= n:
			return null
		var raw: Surf = surf[u2 * n + v2]
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
	var surf2 = _surf(lv, oi2, s.world)
	var lv2: Vector3 = Projection.view_of(n, m2, s.pos)
	var land = Projection.walkable(lv, surf2, int(lv2.x), int(lv2.y), s.doors)
	if land == null:
		return null
	var nk2: int = s.kmask
	var ki2: int = lv.key_index_at(land.w)
	if ki2 >= 0:
		nk2 |= 1 << ki2
	return SolveState.new(land.w, oi2, nk2, s.doors, s.world)


# HOW FAR THE CORE IS ACROSS THE FACE, and whether it is on the face at all.
# The collapse means a column shows only its nearest cell, so a core buried
# behind lattice is not merely distant — it is not there to walk to.
func _gap(lv: Level, s: SolveState) -> Array:
	var n: int = lv.n
	var m: Ori = Turns.ori(s.ori)
	var a: Vector3 = Projection.view_of(n, m, s.pos)
	var b: Vector3 = Projection.view_of(n, m, lv.goal)
	var surf = _surf(lv, s.ori, s.world)
	var k: int = int(b.x) * n + int(b.y)
	var shown := false
	if k >= 0 and k < surf.size():
		var raw: Surf = surf[k]
		shown = raw.has and raw.w == lv.goal
	return [abs(a.x - b.x) + abs(a.y - b.y), shown]


func _start_state(lv: Level) -> SolveState:
	var s := SolveState.new(lv.start, 0, 0, 0, 0)
	var s0: int = lv.key_index_at(lv.start)
	if s0 >= 0:
		s.kmask = 1 << s0
	s.world = lv.glyph_at(lv.start)
	return s


# A PLAYER WHO ONLY EVER CLOSES THE GAP. Steps first, because they are free;
# otherwise the fold that leaves the core nearest. If this reaches the core in
# par, there was nothing to work out.
func _transparent(lv: Level, par: int) -> int:
	var s: SolveState = _start_state(lv)
	var folds := 0
	var guard := 0
	var seen := {}
	while guard < 400:
		guard += 1
		if s.pos == lv.goal:
			return folds
		var key: int = Solver.state_key(lv.n, s)
		if seen.has(key):
			return -1
		seen[key] = true
		var here: Array = _gap(lv, s)
		var best = null
		var best_d: float = here[0]
		for t in range(4):
			var alt = _apply(lv, s, false, t)
			if alt == null:
				continue
			var g: Array = _gap(lv, alt)
			if g[0] < best_d:
				best_d = g[0]
				best = alt
		if best != null:
			s = best
			continue
		var bf = null
		var bf_d := 99999.0
		for t2 in range(4):
			var alt2 = _apply(lv, s, true, t2)
			if alt2 == null:
				continue
			var g2: Array = _gap(lv, alt2)
			if g2[0] < bf_d:
				bf_d = g2[0]
				bf = alt2
		if bf == null:
			return -1
		s = bf
		folds += 1
		if folds > par * 3:
			return -1
	return -1


# THE COST OF ONE ERRAND, from here to there, with the same 0-1 rule the game
# is graded on: a step is free, a fold costs one. Written against this file's
# own transition rather than the solver's, which the replay check in
# tools/audit.gd has already matched to the solver on all 150 lines.
func _cost_to(lv: Level, from: SolveState, target: Vector3, budget: int):
	var n: int = lv.n
	var buckets := []
	var seen := {}
	var k0: int = Solver.state_key(n, from)
	seen[k0] = 0
	buckets.resize(budget + 2)
	buckets[0] = [from]
	for cost in range(0, budget + 1):
		var q = buckets[cost]
		if q == null:
			continue
		var qi := 0
		while qi < q.size():
			var s: SolveState = q[qi]
			qi += 1
			var sk: int = Solver.state_key(n, s)
			if seen[sk] < cost:
				continue
			if s.pos == target:
				return [cost, s]
			for t in range(4):
				var st = _apply(lv, s, false, t)
				if st != null:
					var k1: int = Solver.state_key(n, st)
					if not seen.has(k1) or seen[k1] > cost:
						seen[k1] = cost
						q.append(st)
			if cost + 1 > budget:
				continue
			for t2 in range(4):
				var fd = _apply(lv, s, true, t2)
				if fd != null:
					var k2: int = Solver.state_key(n, fd)
					if not seen.has(k2) or seen[k2] > cost + 1:
						seen[k2] = cost + 1
						if buckets[cost + 1] == null:
							buckets[cost + 1] = []
						buckets[cost + 1].append(fd)
	return null


# ONE ERRAND AT A TIME. Take the cheapest sub-goal from here, then the next,
# until the core. If that costs no more than par, the cube is a list rather than
# a puzzle — this is the metric that predicted human solving time best.
func _greedy_parts(lv: Level, par: int) -> int:
	var s: SolveState = _start_state(lv)
	var total := 0
	var guard := 0
	while guard < 24:
		guard += 1
		if s.pos == lv.goal:
			return total
		var best_cost := 9999
		var best_state = null
		var targets := []
		for i in range(lv.keys.size()):
			if (s.kmask & (1 << i)) == 0:
				targets.append(lv.keys[i])
		targets.append(lv.goal)
		for w in targets:
			var r = _cost_to(lv, s, w, 40)
			if r == null:
				continue
			if r[0] < best_cost:
				best_cost = r[0]
				best_state = r[1]
		if best_state == null:
			return -1
		total += best_cost
		s = best_state
	return -1


func _init() -> void:
	Catalogue._ensure()
	print("level,par,steps,keys,folds,counter,counter_frac,buried,transparent,greedy,decomp,sig")
	for level in range(1, Catalogue.count() + 1):
		if not Catalogue.has(level):
			continue
		var lv: Level = Catalogue.get_level(level)
		if lv == null:
			continue
		_surf_cache.clear()
		var res: SolveResult = Solver.solve(lv)
		if not res.ok:
			continue

		var s: SolveState = _start_state(lv)
		var counter := 0
		var buried := 0
		var folds := 0
		var sig := ""
		for a in res.line:
			if a.is_turn:
				var before: Array = _gap(lv, s)
				var after_s = _apply(lv, s, true, a.dir)
				if after_s != null:
					var after: Array = _gap(lv, after_s)
					if after[0] > before[0]:
						counter += 1
					if before[1] and not after[1]:
						buried += 1
				folds += 1
				sig += ["L", "R", "U", "D"][a.dir]
			var nxt = _apply(lv, s, a.is_turn, a.dir)
			if nxt == null:
				break
			s = nxt

		var tr: int = _transparent(lv, res.turns)
		var gp: int = _greedy_parts(lv, res.turns)
		var f: float = max(1.0, float(folds))
		print("%d,%d,%d,%d,%d,%d,%.2f,%d,%s,%d,%s,%s" % [
				level, res.turns, res.steps, lv.keys.size(), folds, counter, counter / f, buried,
				"yes" if (tr >= 0 and tr <= res.turns) else "no",
				tr, str(gp - res.turns) if gp >= 0 else "?", sig])
	quit()
