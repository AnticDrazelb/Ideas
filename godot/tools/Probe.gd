extends Reference
# The heavy measurements behind tools/search.gd, in an ordinary object so they can
# be driven and timed one at a time. A SceneTree script cannot be instantiated a
# second time to poke at it.

const EXPAND_CAP := 30000
const ROOM_CAP := 40000
const DEAD_SAMPLE := 120

var R = load("res://tools/Replay.gd").new()


# HOW MANY FOLDS BEFORE THE CORE IS ON THE FACE AT ALL. Folds only — a step
# never changes which face you are looking at.
func _first_seen(lv: Level, budget: int) -> int:
	var s0: SolveState = R.start_state(lv)
	if R.gap(lv, s0)[1]:
		return 0
	var seen := {Solver.state_key(lv.n, s0): true}
	var frontier := [s0]
	for depth in range(1, budget + 1):
		var next := []
		for s in frontier:
			# free steps first — they cost nothing and move you within the face
			var walk := [s]
			var wi := 0
			var wseen := {Solver.state_key(lv.n, s): true}
			while wi < walk.size():
				var w: SolveState = walk[wi]
				wi += 1
				for t in range(4):
					var st = R.apply(lv, w, false, t)
					if st == null:
						continue
					var wk: int = Solver.state_key(lv.n, st)
					if wseen.has(wk):
						continue
					wseen[wk] = true
					walk.append(st)
			for w2 in walk:
				for t2 in range(4):
					var fd = R.apply(lv, w2, true, t2)
					if fd == null:
						continue
					var k: int = Solver.state_key(lv.n, fd)
					if seen.has(k):
						continue
					seen[k] = true
					if R.gap(lv, fd)[1]:
						return depth
					next.append(fd)
		frontier = next
		if frontier.empty():
			return -1
	return -1


# THE PLAYER. Two phases, because there are two questions and only one of them
# is "where do I walk".
#
#   core not on the face   turn it over, preferring faces you have not seen
#   core on the face       head for it, preferring to close the gap
#
# Greedy best first, so it thrashes exactly where a person would: it commits to
# whatever looks best and only backs out when that runs dry. What comes back is
# the number of states it opened and the number of FOLDS it tried — the failed
# turns, which is the thing par cannot tell you.
func _thrash(lv: Level, par: int) -> Array:
	var n: int = lv.n
	var s0: SolveState = R.start_state(lv)
	var buckets := []
	buckets.resize(4096)
	var faces := {}

	var expanded := 0
	var folds_tried := 0

	var pri0: int = _priority(lv, s0, faces)
	buckets[pri0] = [s0]
	var closed := {}
	var lo := 0

	while lo < 4096:
		var q = buckets[lo]
		if q == null or q.empty():
			lo += 1
			continue
		var s: SolveState = q.pop_back()
		var sk: int = Solver.state_key(n, s)
		if closed.has(sk):
			continue
		closed[sk] = true
		expanded += 1
		if s.pos == lv.goal:
			return [expanded, folds_tried, true]
		if expanded >= EXPAND_CAP:
			return [expanded, folds_tried, false]

		for t in range(4):
			var st = R.apply(lv, s, false, t)
			if st == null:
				continue
			if closed.has(Solver.state_key(n, st)):
				continue
			var p1: int = _priority(lv, st, faces)
			if buckets[p1] == null:
				buckets[p1] = []
			buckets[p1].append(st)
			if p1 < lo:
				lo = p1
		for t2 in range(4):
			var fd = R.apply(lv, s, true, t2)
			if fd == null:
				continue
			folds_tried += 1
			if closed.has(Solver.state_key(n, fd)):
				continue
			var p2: int = _priority(lv, fd, faces)
			if buckets[p2] == null:
				buckets[p2] = []
			buckets[p2].append(fd)
			if p2 < lo:
				lo = p2
	return [expanded, folds_tried, false]


func _priority(lv: Level, s: SolveState, faces: Dictionary) -> int:
	var g: Array = R.gap(lv, s)
	if g[1]:
		return int(min(1000, g[0]))          # the core is there: close the gap
	var fk: String = R.face_key(lv, s)
	if not faces.has(fk):
		faces[fk] = faces.size()
	# an unseen face is worth looking at; a face you have already turned to is
	# worth less the longer you have known about it
	return int(min(4095, 2000 + faces[fk]))


# HOW BIG IS THE ROOM. Every distinct state within par folds of the start.
func _room(lv: Level, par: int) -> Array:
	var n: int = lv.n
	var s0: SolveState = R.start_state(lv)
	var seen := {Solver.state_key(n, s0): 0}
	var frontier := [s0]
	var all := [s0]
	for depth in range(0, par + 1):
		var next := []
		for s in frontier:
			for t in range(4):
				var st = R.apply(lv, s, false, t)
				if st == null:
					continue
				var k: int = Solver.state_key(n, st)
				if seen.has(k):
					continue
				seen[k] = depth
				frontier.append(st)
				all.append(st)
				if seen.size() >= ROOM_CAP:
					return [seen.size(), all, true]
		if depth >= par:
			break
		for s2 in frontier:
			for t2 in range(4):
				var fd = R.apply(lv, s2, true, t2)
				if fd == null:
					continue
				var k2: int = Solver.state_key(n, fd)
				if seen.has(k2):
					continue
				seen[k2] = depth + 1
				next.append(fd)
				all.append(fd)
				if seen.size() >= ROOM_CAP:
					return [seen.size(), all, true]
		frontier = next
	return [seen.size(), all, false]


# CAN YOU LOSE IT? A sample of the room, each asked whether the core is still
# reachable. Undo makes a dead state survivable, but a game in which no wrong
# move can ever cost you anything is a game with no tension in it either.
func _dead(lv: Level, all: Array) -> Array:
	var step: int = int(max(1, all.size() / DEAD_SAMPLE))
	var tried := 0
	var dead := 0
	var i := 0
	while i < all.size():
		var s: SolveState = all[i]
		i += step
		tried += 1
		var r: SolveResult = Solver.solve(lv, 24, s)
		if not r.ok:
			dead += 1
	return [tried, dead]


