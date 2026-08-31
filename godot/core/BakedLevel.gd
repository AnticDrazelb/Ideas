class_name BakedLevel
extends Reference
# One of the authored cubes, unpacked out of the const array Baked stores it in
# so that call sites can say `.name` and `.par` rather than doing slot
# arithmetic.
#
# IT HOLDS THE FIELDS RATHER THAN THE ROW, and that is not a style choice: a
# wrapper that read `a[Baked.NAME]` would name Baked, Baked names this to build
# one, and Godot refuses the cycle outright — "Resource is already being loaded.
# Cyclic reference?" — with both classes then failing to load. The slot
# constants are Baked's business and stop at its door.

var n := 0
var name := ""
var par := 0
var vox := ""
var vox_b := ""
var start := Vector3.ZERO
var goal := Vector3.ZERO
var keys := []
var doors := []


func _init(row: Array) -> void:
	n = row[0]
	name = row[1]
	par = row[2]
	vox = row[3]
	start = row[4]
	goal = row[5]
	keys = row[6]
	doors = row[7]
	vox_b = row[8]


func to_level(level: int) -> Level:
	var lv := Level.new()
	lv.n = n
	lv.vox = vox.to_ascii()
	lv.start = start
	lv.goal = goal
	lv.keys = keys.duplicate()
	lv.doors = doors.duplicate()
	lv.par = par
	lv.level = level
	if vox_b != "":
		lv.set_vox_b(vox_b.to_ascii())
	lv.band = Vaults.vault_of(level)
	lv.name = name
	lv.authored = true
	return lv
