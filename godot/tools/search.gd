extends SceneTree
# WHAT THE PLAYER ACTUALLY DOES, WHICH IS NOT THE SOLUTION.
#
# tools/audit.gd and tools/fun.gd both walk the OPTIMAL LINE and grade the moves
# on it. That is a description of the answer, and nobody experiences the answer —
# they experience the search for it. Par is a floor nobody stands on. A player
# folds, looks, folds back, folds somewhere else, and most of what they do never
# appears in the line at all.
#
# Worse, both tools assumed the player can see where they are going. They cannot.
# CubeView._shown refuses the exit marker unless the cell is on the surface, so on
# a cube where the core starts buried the first job is not "route to the core", it
# is "find the core" — a sub-goal neither tool counted, and the reason 60 cubes
# were called single-objective when they are nothing of the sort.
#
# So this measures the search:
#
#   seen        is the core on the face at the start, and if not, how many folds
#               before it can be seen at all
#   thrash      a two-phase player — turn it over until the core shows, then head
#               for it — run as a greedy best-first search, counting every state
#               expanded and every fold tried. The closed list of a heuristic
#               search is the thing the Sokoban literature found tracks both
#               perceived difficulty and the number of moves a human makes.
#   room        how many distinct states sit within par folds of the start: the
#               size of the space being searched, as against the length of the
#               line through it
#   dead        states within reach from which the core can no longer be got to
#               at all — whether the game can be lost, as against merely delayed
#
#   godot --path godot -s tools/search.gd > search.csv

var P = load("res://tools/Probe.gd").new()


func _init() -> void:
	Catalogue._ensure()
	var t0 := OS.get_ticks_msec()
	print("level,par,steps,keys,visible_at_start,first_seen,expanded,folds_tried,solved,room,room_capped,dead_sampled,dead")
	for level in range(1, Catalogue.count() + 1):
		if not Catalogue.has(level):
			continue
		var lv: Level = Catalogue.get_level(level)
		if lv == null:
			continue
		P.R.fresh()
		var res: SolveResult = Solver.solve(lv)
		if not res.ok:
			continue
		var vis: bool = P.R.gap(lv, P.R.start_state(lv))[1]
		var fs: int = 0 if vis else P._first_seen(lv, res.turns + 2)
		var th: Array = P._thrash(lv, res.turns)
		var rm: Array = P._room(lv, res.turns)
		var dd: Array = P._dead(lv, rm[1])
		print("%d,%d,%d,%d,%s,%d,%d,%d,%s,%d,%s,%d,%d" % [
				level, res.turns, res.steps, lv.keys.size(),
				"yes" if vis else "no", fs, th[0], th[1], "yes" if th[2] else "no",
				rm[0], "yes" if rm[2] else "no", dd[0], dd[1]])
		if level % 5 == 0:
			printerr("  ... %d, %.0fs" % [level, (OS.get_ticks_msec() - t0) / 1000.0])
	quit()
