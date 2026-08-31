class_name Validation
# THE RULES A CUBE HAS TO PASS.
#
# Par is the solver's proven minimum, and every scored thing in this game reads
# it, so a cube without one cannot be played — which makes the solver the
# Forge's real validator rather than a nicety bolted on at the end. A cube that
# cannot be finished is not a hard cube, it is a broken one.
#
# The geometry checks are the same ones mint applies to its own candidates, in
# the same order, so a hand-built cube is held to exactly the standard a
# generated one is.


# Does any of the twenty-four orientations put the core on a visible face? Zero
# means it is walled in on all six sides and no amount of folding will ever
# expose it.
static func goal_ever_shows(lv: Level) -> bool:
	var n: int = lv.n
	for i in range(Turns.count()):
		var m: Ori = Turns.ori(i)
		var s := Projection.project(n, lv.eff(0), m)
		if Projection.surface_at(n, s, m, lv.goal):
			return true
	return false


# Returns { ok, why, par, steps }.
static func validate(lv: Level) -> Dictionary:
	var n: int = lv.n

	if lv.start == lv.goal:
		return _fail("START AND EXIT ARE THE SAME CELL")
	if lv.keys.size() != lv.doors.size():
		return _fail("EVERY NODE NEEDS A LOCK")
	if not Level.is_walk_type(lv.vox[Level.vidx_v(n, lv.start)]):
		return _fail("START IS NOT ON A TRACE")
	if not Level.is_walk_type(lv.vox[Level.vidx_v(n, lv.goal)]):
		return _fail("EXIT IS NOT ON A TRACE")

	lv.clear_eff()
	var s0 := Projection.project(n, lv.eff(0), Ori.new())
	if not Projection.surface_at(n, s0, Ori.new(), lv.start):
		return _fail("START IS BURIED")

	# AND THE EXIT HAS TO BE ON A FACE OF SOMETHING, EVER. The commonest
	# authoring mistake there is — filling a deck solid and putting the exit in
	# the middle of it — otherwise reaches the solver and comes back as "NO
	# SOLUTION EXISTS". True, and useless.
	if not goal_ever_shows(lv):
		return _fail("EXIT IS SEALED INSIDE THE SOLID")

	if lv.door_index_at(lv.start) >= 0 or lv.door_index_at(lv.goal) >= 0:
		return _fail("A LOCK SITS ON THE START OR THE EXIT")

	var r := Solver.solve(lv, 60)
	if not r.ok:
		return _fail("NO SOLUTION EXISTS")

	# A CUBE YOU CAN FINISH WITHOUT FOLDING IS A CORRIDOR. The generator can
	# never produce one, but the Forge has no floor of its own — and par is the
	# whole scoring system, so a par of zero makes every clear worse than perfect
	# and the win card reads as nonsense.
	if r.turns < 1:
		return _fail("NO FOLD NEEDED — THAT IS A CORRIDOR, NOT A CUBE")

	return {"ok": true, "why": "", "par": r.turns, "steps": r.steps}


static func _fail(why: String) -> Dictionary:
	return {"ok": false, "why": why, "par": 0, "steps": 0}
