class_name UiRect
extends Control
# A RECTANGLE THAT MEASURES ITSELF THE WAY THE ORIGINAL DOES.
#
# This is the one piece of scaffolding in the port that has no counterpart in
# the C#, and it exists because of a single difference that would otherwise
# have touched every line of layout in the game: UNITY'S UI COUNTS Y UPWARD AND
# GODOT'S COUNTS IT DOWN.
#
# Every screen in this project is composed out of anchors and offsets — a rule
# at 0.58 of the plate, a control band 128 units up from the floor, a masthead
# 346 units down from the top — and there are some three thousand lines of it.
# Flipping each of those by hand is not a translation, it is a rewrite, and a
# rewrite of numbers is how a layout that was measured against a real phone
# becomes a layout that was measured against nothing.
#
# So the numbers stay exactly as they are written in the Unity project and this
# converts them, once, in four lines:
#
#   godot.anchor_left   = unity.anchorMin.x       godot.margin_left   =  offsetMin.x
#   godot.anchor_right  = unity.anchorMax.x       godot.margin_right  =  offsetMax.x
#   godot.anchor_top    = 1 - unity.anchorMax.y   godot.margin_top    = -offsetMax.y
#   godot.anchor_bottom = 1 - unity.anchorMin.y   godot.margin_bottom = -offsetMin.y
#
# It also carries Unity's PIVOT, which Godot has no equivalent of at all: a
# point-anchored control there is positioned by its pivot and sized by
# sizeDelta, and the chassis, the fold marks and every icon in the game are
# placed that way.

var pivot := Vector2(0.5, 0.5)
var size_delta := Vector2.ZERO
var anchored_position := Vector2.ZERO

# Whether this rect is a STRETCH on each axis — the anchors differ — or a point.
# Set by anchor_to(); the two are laid out by different arithmetic.
var _stretch_x := true
var _stretch_y := true
var _off_min := Vector2.ZERO
var _off_max := Vector2.ZERO


# Unity's four numbers, verbatim.
func anchor_to(a_min: Vector2, a_max: Vector2, off_min: Vector2, off_max: Vector2) -> void:
	# A RECT WITH NEGATIVE HEIGHT DRAWS NOTHING AND SAYS NOTHING.
	#
	# offsetMin is the bottom-left corner and offsetMax the top-right, and on an
	# axis whose two anchors are the SAME the difference between them is the
	# whole size — so writing the pair the wrong way round gives a row a height
	# of minus eighty-eight, lays every child out inside it, and produces a
	# screen that is simply absent. The pause card lost its five controls to
	# exactly this and there was nothing on screen to say so; you could only
	# leave it by solving the cube.
	#
	# Where the anchors DIFFER the axis is a stretch and the offsets are insets
	# against two different edges, so offsetMax below offsetMin is ordinary and
	# correct. That is why this asks about the anchors first.
	# STRICTLY LESS THAN, because ZERO IS A LEGITIMATE ANSWER. A point-anchored
	# rect with both offsets at zero is how every icon in the game is built —
	# UiKit.icon makes one and then sizes it through set_size_delta, which is
	# Unity's own sizeDelta and has no offset form at all. Only an INVERTED pair
	# is the mistake this guard exists for.
	if a_min.x == a_max.x and off_max.x < off_min.x:
		push_error("UiRect: '%s' is %f units wide — offsetMin and offsetMax are the wrong way round"
				% [name, off_max.x - off_min.x])
		var t := off_min.x
		off_min.x = off_max.x
		off_max.x = t
	if a_min.y == a_max.y and off_max.y < off_min.y:
		push_error("UiRect: '%s' is %f units tall — offsetMin and offsetMax are the wrong way round"
				% [name, off_max.y - off_min.y])
		var t2 := off_min.y
		off_min.y = off_max.y
		off_max.y = t2

	_stretch_x = a_min.x != a_max.x
	_stretch_y = a_min.y != a_max.y
	_off_min = off_min
	_off_max = off_max

	anchor_left = a_min.x
	anchor_right = a_max.x
	anchor_top = 1.0 - a_max.y
	anchor_bottom = 1.0 - a_min.y

	margin_left = off_min.x
	margin_right = off_max.x
	margin_top = -off_max.y
	margin_bottom = -off_min.y

	# A point-anchored rect is sized by sizeDelta about its pivot, so seed both
	# from what the offsets already say.
	if not _stretch_x:
		size_delta.x = off_max.x - off_min.x
	if not _stretch_y:
		size_delta.y = off_max.y - off_min.y


# Unity's sizeDelta on a point-anchored axis, re-applied through the pivot.
func set_size_delta(s: Vector2) -> void:
	size_delta = s
	_reapply()


func set_anchored_position(p: Vector2) -> void:
	anchored_position = p
	_reapply()


func set_pivot(p: Vector2) -> void:
	pivot = p
	_reapply()


func _reapply() -> void:
	if not _stretch_x:
		margin_left = anchored_position.x - pivot.x * size_delta.x
		margin_right = anchored_position.x + (1.0 - pivot.x) * size_delta.x
	if not _stretch_y:
		# Y is measured up in the source and down here, so the top margin is the
		# NEGATIVE of the far edge and the bottom margin the negative of the near
		# one. Getting this backwards is the whole class of bug this file exists
		# to make impossible.
		margin_top = -(anchored_position.y + (1.0 - pivot.y) * size_delta.y)
		margin_bottom = -(anchored_position.y - pivot.y * size_delta.y)


# The rect's own size in reference units, which is what every layout question in
# the game is asked in.
func unit_size() -> Vector2:
	return rect_size
