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

var R = load("res://tools/Replay.gd").new()


# A PLAYER WHO ONLY EVER CLOSES THE GAP. Steps first, because they are free;
# otherwise the fold that leaves the core nearest. If this reaches the core in
# par, there was nothing to work out.
func _transparent(lv: Level, par: int) -> int:
	var s: SolveState = R.start_state(lv)
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
		var here: Array = R.gap(lv, s)
		var best = null
		var best_d: float = here[0]
		for t in range(4):
			var alt = R.apply(lv, s, false, t)
			if alt == null:
				continue
			var g: Array = R.gap(lv, alt)
			if g[0] < best_d:
				best_d = g[0]
				best = alt
		if best != null:
			s = best
			continue
		var bf = null
		var bf_d := 99999.0
		for t2 in range(4):
			var alt2 = R.apply(lv, s, true, t2)
			if alt2 == null:
				continue
			var g2: Array = R.gap(lv, alt2)
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
				var st = R.apply(lv, s, false, t)
				if st != null:
					var k1: int = Solver.state_key(n, st)
					if not seen.has(k1) or seen[k1] > cost:
						seen[k1] = cost
						q.append(st)
			if cost + 1 > budget:
				continue
			for t2 in range(4):
				var fd = R.apply(lv, s, true, t2)
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
	var s: SolveState = R.start_state(lv)
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
		R.fresh()
		var res: SolveResult = Solver.solve(lv)
		if not res.ok:
			continue

		var s: SolveState = R.start_state(lv)
		var counter := 0
		var buried := 0
		var folds := 0
		var sig := ""
		for a in res.line:
			if a.is_turn:
				var before: Array = R.gap(lv, s)
				var after_s = R.apply(lv, s, true, a.dir)
				if after_s != null:
					var after: Array = R.gap(lv, after_s)
					if after[0] > before[0]:
						counter += 1
					if before[1] and not after[1]:
						buried += 1
				folds += 1
				sig += ["L", "R", "U", "D"][a.dir]
			var nxt = R.apply(lv, s, a.is_turn, a.dir)
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
