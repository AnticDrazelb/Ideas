class_name UiBar
extends UiRect
# A CONTINUOUS CONTROL FOR A CONTINUOUS QUANTITY. These were plus and minus
# buttons, which is a fine way to change a number by one and a poor way to answer
# "how bright, out of how bright it goes" — a stepper shows you a reading, a
# slider shows you a POSITION IN A RANGE, and brightness is only ever adjusted by
# comparison with the ends.
#
# THE WHOLE TRACK IS THE TARGET, NOT THE HANDLE.
#
# Godot's own slider takes its input on the handle and the groove it draws; this
# one takes it on the rect, which is however tall the row gave it. That is a
# 2.5.5 fix and a plain bug fix at the same time: in the C# the handle was
# twenty-eight units across and there was nothing on the track for the raycaster
# to find, so a tap anywhere on the bar did nothing at all.

const TRACK_H := 14.0
const KNOB := 28.0

var lo := 0
var hi := 100
var value := 0

var _fill: UiRect
var _handle: UiRect
var _set: FuncRef


func build(lo_in: int, hi_in: int, now: int, setter: FuncRef) -> void:
	lo = lo_in
	hi = hi_in
	value = now
	_set = setter
	mouse_filter = Control.MOUSE_FILTER_STOP

	var bg: UiRect = UiButton.kit().rect(self, "track", Vector2(0, 0.5), Vector2(1, 0.5),
			Vector2(KNOB * 0.5, -TRACK_H * 0.5), Vector2(-KNOB * 0.5, TRACK_H * 0.5))
	var bgi := NinePatchRect.new()
	bgi.name = "groove"
	bgi.texture = Sprites.bar()
	bgi.patch_margin_left = 7
	bgi.patch_margin_right = 7
	bgi.patch_margin_top = 7
	bgi.patch_margin_bottom = 7
	bgi.modulate = Palette.INERT      # a surface, not type — see Palette.INERT
	bgi.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bgi.anchor_right = 1.0
	bgi.anchor_bottom = 1.0
	bg.add_child(bgi)

	_fill = UiButton.kit().rect(bg, "fill", Vector2(0, 0), Vector2(1, 1), Vector2.ZERO, Vector2.ZERO)
	var fi := NinePatchRect.new()
	fi.name = "lit"
	fi.texture = Sprites.bar()
	fi.patch_margin_left = 7
	fi.patch_margin_right = 7
	fi.patch_margin_top = 7
	fi.patch_margin_bottom = 7
	fi.modulate = Palette.rust()
	fi.mouse_filter = Control.MOUSE_FILTER_IGNORE
	fi.anchor_right = 1.0
	fi.anchor_bottom = 1.0
	_fill.add_child(fi)

	# THE HANDLE'S AREA IS A BAND, NOT THE WHOLE ROW. Given the whole row it
	# would come out a hundred and thirty tall and thirty wide, which is the
	# squashed circle the C# note is about.
	var area: UiRect = UiButton.kit().rect(self, "handleArea", Vector2(0, 0.5), Vector2(1, 0.5),
			Vector2(KNOB * 0.5, -KNOB * 0.5), Vector2(-KNOB * 0.5, KNOB * 0.5))
	_handle = UiButton.kit().rect(area, "handle", Vector2(0, 0.5), Vector2(0, 0.5), Vector2.ZERO, Vector2.ZERO)
	_handle.set_size_delta(Vector2(KNOB, KNOB))
	var hi2 := TextureRect.new()
	hi2.name = "disc"
	hi2.texture = Sprites.disc()
	hi2.expand = true
	hi2.stretch_mode = TextureRect.STRETCH_SCALE
	hi2.modulate = Palette.INK
	hi2.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hi2.anchor_right = 1.0
	hi2.anchor_bottom = 1.0
	_handle.add_child(hi2)

	_paint()


func _fraction() -> float:
	return 0.0 if hi <= lo else clamp((value - lo) / float(hi - lo), 0.0, 1.0)


func _paint() -> void:
	var f := _fraction()
	_fill.anchor_right = f
	_handle.anchor_left = f
	_handle.anchor_right = f
	_handle.set_anchored_position(Vector2.ZERO)


func set_value(v: int, tell: bool = true) -> void:
	var was := value
	value = int(clamp(v, lo, hi))
	_paint()
	if tell and value != was and _set != null and _set.is_valid():
		_set.call_func(value)


func _gui_input(event: InputEvent) -> void:
	var at := -1.0
	if (event is InputEventMouseButton or event is InputEventScreenTouch) and event.pressed:
		at = event.position.x
	elif event is InputEventMouseMotion and (event.button_mask & BUTTON_MASK_LEFT) != 0:
		at = event.position.x
	elif event is InputEventScreenDrag:
		at = event.position.x
	if at < 0.0:
		return
	accept_event()
	var usable: float = max(1.0, rect_size.x - KNOB)
	var f: float = clamp((at - KNOB * 0.5) / usable, 0.0, 1.0)
	set_value(int(round(lo + f * (hi - lo))))
