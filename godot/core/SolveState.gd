class_name SolveState
# The state a search visits: where you stand, how the engine is folded, and
# what you carry.
var pos := Vector3.ZERO
var ori := 0
var kmask := 0
var doors := 0
var world := 0


func _init(p := Vector3.ZERO, o := 0, k := 0, d := 0, w := 0) -> void:
	pos = p
	ori = o
	kmask = k
	doors = d
	world = w
