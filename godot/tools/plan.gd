extends SceneTree
# DOES THINKING AHEAD PAY?
#
#   godot --path godot -s tools/plan.gd > plan.csv

var P = load("res://tools/Probe.gd").new()

func _init() -> void:
	Catalogue._ensure()
	var t0 := OS.get_ticks_msec()
	print("level,par,d1,d2,d3,x1,x2,x3")
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
		print("%d,%d,%d,%d,%d,%d,%d,%d" % [level, res.turns,
				P.plan(lv, 1, res.turns), P.plan(lv, 2, res.turns), P.plan(lv, 3, res.turns),
				P.plan(lv, 1, res.turns, 1), P.plan(lv, 2, res.turns, 1), P.plan(lv, 3, res.turns, 1)])
		if level % 15 == 0:
			printerr("  ... %d, %.0fs" % [level, (OS.get_ticks_msec() - t0) / 1000.0])
	quit()
