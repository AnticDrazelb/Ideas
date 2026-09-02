extends SceneTree
# THE LADDER, MEASURED AS DESIGN RATHER THAN AS RULES.
#
# tests/run.gd asks whether every cube decodes and solves at the par it claims.
# That is correctness. It says nothing about whether the cube is WORTH solving,
# and 140 of the 150 are minted rather than written, so that is the question
# somebody has to ask before charging for them.
#
# A move is interesting when several things look possible and one of them is
# right. So at every fold on the optimal line this counts two numbers:
#
#   legal    how many of the four folds the geometry even allows
#   keeps    how many of those still reach the core in the folds remaining
#
# legal 1 is not a decision, it is a corridor. keeps == legal is not a decision
# either, it is a formality — every road leads to Rome. What a designer is
# reaching for is legal 3 or 4 with keeps 1: the board offers you choices and
# exactly one of them is the line.
#
#   godot --path godot -s tools/audit.gd > audit.csv

# Re-applying a move to a state duplicates Solver's transition, which is the
# kind of copy that drifts. It is checked rather than trusted: every replay must
# land on the goal in exactly the par the solver reported, on all 150, or the
# run says so and the numbers are void.
# Projections are the expensive call and there are only 24 x 32 of them per
# cube. Same cache the solver keeps, for the same reason.
var _surf_cache := {}


func _surf(lv: Level, ori: int, world: int):
	var k := ori * 32 + world
	var got = _surf_cache.get(k)
	if got == null:
		got = Projection.project(lv.n, lv.eff(world), Turns.ori(ori))
		_surf_cache[k] = got
	return got


func _apply(lv: Level, s: SolveState, a: Act):
	var n: int = lv.n
	var m: Ori = Turns.ori(s.ori)
	if not a.is_turn:
		var v := Projection.view_of(n, m, s.pos)
		var surf = _surf(lv, s.ori, s.world)
		var u2: int = int(v.x) + Turns.DX[a.dir]
		var v2: int = int(v.y) + Turns.DY[a.dir]
		if u2 < 0 or v2 < 0 or u2 >= n or v2 >= n:
			return null
		var raw: Surf = surf[u2 * n + v2]
		if not raw.has or not Level.is_walk_type(raw.t):
			return null
		var nd := s.doors
		var di := lv.door_index_at(raw.w)
		if di >= 0 and (s.doors & (1 << di)) == 0:
			if Solver.popcount(s.kmask) - Solver.popcount(s.doors) < 1:
				return null
			nd = s.doors | (1 << di)
		var nk := s.kmask
		var ki := lv.key_index_at(raw.w)
		if ki >= 0:
			nk |= 1 << ki
		return SolveState.new(raw.w, s.ori, nk, nd, s.world ^ Level.glyph_bit(raw.t))

	var m2 := Turns.apply(a.dir, m)
	var surf2 = _surf(lv, Turns.ori_index(m2), s.world)
	var lv2 := Projection.view_of(n, m2, s.pos)
	var land = Projection.walkable(lv, surf2, int(lv2.x), int(lv2.y), s.doors)
	if land == null:
		return null
	var nk2 := s.kmask
	var ki2 := lv.key_index_at(land.w)
	if ki2 >= 0:
		nk2 |= 1 << ki2
	return SolveState.new(land.w, Turns.ori_index(m2), nk2, s.doors, s.world)


# How many places you could walk to from here without folding at all — the size
# of the board the player is reading before they consider turning it.
func _reach(lv: Level, s: SolveState) -> int:
	var n: int = lv.n
	var m: Ori = Turns.ori(s.ori)
	var surf = _surf(lv, s.ori, s.world)
	var seen := {}
	var q := [Projection.view_of(n, m, s.pos)]
	seen[int(q[0].x) * n + int(q[0].y)] = true
	var i := 0
	while i < q.size():
		var p: Vector3 = q[i]
		i += 1
		for t in range(4):
			var u2: int = int(p.x) + Turns.DX[t]
			var v2: int = int(p.y) + Turns.DY[t]
			if u2 < 0 or v2 < 0 or u2 >= n or v2 >= n:
				continue
			var k := u2 * n + v2
			if seen.has(k):
				continue
			var raw: Surf = surf[k]
			if not raw.has or not Level.is_walk_type(raw.t):
				continue
			seen[k] = true
			q.append(Vector3(u2, v2, 0))
	return seen.size()


func _init() -> void:
	Catalogue._ensure()
	var total: int = Catalogue.count()
	print("level,n,par,steps,cells,trace,plate,everter,trigger,keys,doors,layouts,",
			"decisions,legal_mean,keeps_mean,forced,free,real,reach_mean,replay_ok,id")
	var t0 := OS.get_ticks_msec()
	for level in range(1, total + 1):
		if level % 10 == 0:
			printerr("  ... %d of %d, %.0fs" % [level, total, (OS.get_ticks_msec() - t0) / 1000.0])
		if not Catalogue.has(level):
			continue
		var lv: Level = Catalogue.get_level(level)
		if lv == null:
			continue
		_surf_cache.clear()
		var res: SolveResult = Solver.solve(lv)
		if not res.ok:
			print("%d,UNSOLVED" % level)
			continue

		# the shape of the material
		var cells := 0
		var trace := 0
		var plate := 0
		var everter := 0
		var trigger := 0
		for i in range(lv.vox.size()):
			var t: int = lv.vox[i]
			if t == Level.VOID:
				continue
			cells += 1
			if t == Level.TRACE:
				trace += 1
			elif t == Level.PLATE_A or t == Level.PLATE_B:
				plate += 1
			elif t == Level.EVERTER:
				everter += 1
			elif t == Level.TRIGGER or t == Level.TRIGGER2:
				trigger += 1

		# walk the line, grading every fold on it
		var s: SolveState = SolveState.new(lv.start, 0, 0, 0, 0)
		var s0: int = lv.key_index_at(lv.start)
		if s0 >= 0:
			s.kmask = 1 << s0
		s.world = lv.glyph_at(lv.start)

		var spent := 0
		var decisions := 0
		var legal_sum := 0
		var keeps_sum := 0
		var forced := 0        # only one fold was even legal
		var free_ := 0         # every legal fold kept the solution
		var real := 0          # more than one on offer and at least one of them wrong
		var reach_sum := 0
		for a in res.line:
			if a.is_turn:
				var remain: int = res.turns - spent
				var legal := 0
				var keeps := 0
				for t in range(4):
					var alt = _apply(lv, s, Act.new(true, t))
					if alt == null:
						continue
					legal += 1
					var r2: SolveResult = Solver.solve(lv, remain - 1, alt)
					if r2.ok and r2.turns <= remain - 1:
						keeps += 1
				decisions += 1
				legal_sum += legal
				keeps_sum += keeps
				# THE THREE KINDS OF FOLD, and they have to be counted as three
				# rather than subtracted from each other: one legal fold that
				# works is BOTH the only thing you could do and a fold every
				# option survives, so a real count taken as total minus the two
				# came out negative on cube 116.
				if legal <= 1:
					forced += 1
				elif keeps >= legal:
					free_ += 1
				else:
					real += 1
				reach_sum += _reach(lv, s)
				spent += 1
			var nxt = _apply(lv, s, a)
			if nxt == null:
				s = null
				break
			s = nxt

		var replay_ok: bool = s != null and s.pos == lv.goal and spent == res.turns
		var d: float = max(1.0, float(decisions))
		print("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%.2f,%.2f,%d,%d,%d,%.1f,%s,%s" % [
				level, lv.n, res.turns, res.steps, cells, trace, plate, everter, trigger,
				lv.keys.size(), lv.doors.size(), lv.layouts(),
				decisions, legal_sum / d, keeps_sum / d, forced, free_, real, reach_sum / d,
				"yes" if replay_ok else "NO", str(level)])
	quit()
