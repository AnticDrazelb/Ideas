class_name Act
# One move in a solution: a free step, or a fold that costs one.
var is_turn := false
var dir := -1


func _init(turn := false, d := -1) -> void:
	is_turn = turn
	dir = d


# THE ABSENCE OF A MOVE IS A FRESH OBJECT, not a shared constant: GDScript 3.5
# refuses a class that names itself, so there is no `Act.None` to hand out, and
# a shared mutable singleton would be worse than an allocation anyway.
static func none() -> Act:
	return null
