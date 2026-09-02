extends SceneTree
#   godot --path godot -s tools/prune.gd > prune.csv

var P = load("res://tools/Probe.gd").new()

func _init() -> void:
	Catalogue._ensure()
	print("level,par,decisions,rank1,rank2,ranklast,offered_mean,ranks")
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
		var got: Array = P.rank_of_truth(lv, res.line)
		var ranks: Array = got[0]
		var offered: Array = got[1]
		var r1 := 0
		var r2 := 0
		var rl := 0
		var osum := 0
		for i in range(ranks.size()):
			if ranks[i] == 1:
				r1 += 1
			if ranks[i] <= 2:
				r2 += 1
			if ranks[i] == offered[i] and offered[i] > 1:
				rl += 1
			osum += offered[i]
		var sig := ""
		for r in ranks:
			sig += str(r)
		print("%d,%d,%d,%d,%d,%d,%.2f,%s" % [level, res.turns, ranks.size(), r1, r2, rl,
				float(osum) / max(1, ranks.size()), sig])
	quit()
