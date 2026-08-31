class_name UiSwitch
extends UiRect
# A SWITCH THAT IS A SHAPE RATHER THAN A WORD. Orange and knob-right is on;
# slate and knob-left is off, and you can tell which from further away than you
# can read the label.
#
# THE TARGET IS NOT THE INK.
#
# A pill drawn forty-two units tall is a good-looking switch and half a tap
# target: 2.5.5 at AAA asks for 44 CSS px on the shortest side, which is
# eighty-eight units here. Growing the pill to meet that would make it a
# rectangle with rounded ends rather than a pill, and the SHAPE is the whole
# reason a switch beats a three-letter word — you can read knob-left from across
# a room.
#
# So the pressable rect and the drawn pill are separated. The outer rect takes
# whatever height it is given and carries the input; the pill is drawn inside it
# at the size it was designed. Nothing about the picture changes; the thing you
# can hit stops being the picture.

const PILL_H := 42.0

var _track: NinePatchRect
var _knob: UiRect
var _label: Label
var _set: FuncRef
var _on := false


func build(on: bool, setter: FuncRef) -> void:
	_set = setter
	mouse_filter = Control.MOUSE_FILTER_STOP

	var rt: UiRect = UiButton.kit().rect(self, "pill", Vector2(0, 0.5), Vector2(1, 0.5),
			Vector2(0, -PILL_H * 0.5), Vector2(0, PILL_H * 0.5))

	_track = NinePatchRect.new()
	_track.name = "track"
	_track.texture = Sprites.pill()
	_track.patch_margin_left = 15
	_track.patch_margin_right = 15
	_track.patch_margin_top = 15
	_track.patch_margin_bottom = 15
	_track.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_track.anchor_right = 1.0
	_track.anchor_bottom = 1.0
	rt.add_child(_track)

	_knob = UiButton.kit().rect(rt, "knob", Vector2(0, 0.5), Vector2(0, 0.5), Vector2.ZERO, Vector2.ZERO)
	_knob.set_size_delta(Vector2(34, 34))
	var kimg := TextureRect.new()
	kimg.name = "disc"
	kimg.texture = Sprites.disc()
	kimg.expand = true
	kimg.stretch_mode = TextureRect.STRETCH_SCALE
	kimg.modulate = Palette.INK
	kimg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	kimg.anchor_right = 1.0
	kimg.anchor_bottom = 1.0
	_knob.add_child(kimg)

	_label = UiButton.kit().label(rt, "state", "", 19, Palette.VOID, UiButton.kit().Anchor.MIDDLE_CENTER,
			Vector2.ZERO, Vector2.ONE, Vector2(12, 0), Vector2(-12, 0))

	paint(on)


func paint(v: bool) -> void:
	_on = v
	# Inert rather than Dim2: this is a SURFACE, and under legibility the quiet
	# type it used to share a value with goes the other way.
	_track.modulate = Palette.rust() if v else Palette.INERT
	_knob.anchor_left = 1.0 if v else 0.0
	_knob.anchor_right = 1.0 if v else 0.0
	_knob.set_anchored_position(Vector2(-22.0 if v else 22.0, 0.0))
	_label.text = "ON" if v else "OFF"
	_label.add_color_override("font_color", Palette.VOID if v else Palette.INK)
	_label.align = Label.ALIGN_LEFT if v else Label.ALIGN_RIGHT


func _gui_input(event: InputEvent) -> void:
	if (event is InputEventMouseButton or event is InputEventScreenTouch) and event.pressed:
		accept_event()
		if _set != null and _set.is_valid():
			paint(_set.call_func(true))
