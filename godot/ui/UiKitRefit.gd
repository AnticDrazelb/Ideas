class_name UiKitRefit
extends Reference
# TWO THINGS GDSCRIPT CANNOT DO WITHOUT AN OBJECT, and they are both one line.
#
# `funcref` needs an instance, and the two places UiKit hands out a function are
# the title scrim's two ramps and the deferred re-fit of a label whose box was
# not measured yet. Neither is worth a class of its own, and neither can live on
# UiKit, which is static all the way down.

# UiKit HANDS THESE OUT AND THEY CALL UiKit BACK, which is a ring Godot refuses
# at load time. Deferred, like every other one in this port, with the reason
# written where the load is.
const _KIT := []
const _I := []


static func kit():
	if _KIT.empty():
		_KIT.append(load("res://ui/UiKit.gd"))
	return _KIT[0]

var stage_height := 0.0


# The one instance, and it cannot say its own name to make itself — so it goes
# through the script handle, exactly as the kit above does.
static func instance():
	if _I.empty():
		_I.append(load("res://ui/UiKitRefit.gd").new())
	return _I[0]


# The alpha of the title's top scrim piece, as a fraction of its own height.
func stage_top(from_edge: float) -> float:
	return kit().stage_top_alpha(from_edge * stage_height)


func stage_bottom(from_edge: float) -> float:
	return kit().stage_bottom_alpha(from_edge * stage_height)


# A label whose box measured zero when it was built — which is every label, since
# screens are composed before the first layout — asking again now that it has a
# width.
func on_resized(t: Label) -> void:
	if is_instance_valid(t):
		kit()._refit(t)
