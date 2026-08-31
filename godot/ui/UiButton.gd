class_name UiButton
extends UiRect
# A CONTROL THAT KNOWS IT HAS BEEN PRESSED, and draws the three states the
# shipped build draws.
#
# Godot has a Button, and it is the wrong shape for this interface: every control
# here is a generated nine-slice plate with a separate edge that lights, a
# bracketed label in a monospace, and — on the one primary a screen is allowed —
# a soft glow that is a SIBLING rather than a child, because a child draws after
# its parent and the glow has to be behind the plate. Expressing that through
# StyleBoxes would be four resources per control and a theme nobody can read in a
# diff; expressing it as the four nodes it actually is costs this file.
#
# THE PRESS STATES ARE THE C#'s OWN NUMBERS: white at rest, 1.15 highlighted,
# 0.8 pressed, over six hundredths of a second.

var enabled := true setget _set_enabled

# UiKit BUILDS THESE AND THESE CALL UiKit, and naming each other as global
# classes is a ring Godot refuses at load time — both fail, with an error that
# names neither. One side is deferred to a load(), kept because a file lookup
# per control would be absurd.
const _KIT := []


static func kit():
	if _KIT.empty():
		_KIT.append(load("res://ui/UiKit.gd"))
	return _KIT[0]


var _plate: NinePatchRect
var _edge: NinePatchRect
var _label: Label
var _on_click: FuncRef
var _primary := false
var _held := false
var _tint := 1.0
var _want := 1.0


func build_plated(shown: String, size: int, primary: bool, on_click) -> void:
	_primary = primary
	_on_click = on_click
	mouse_filter = Control.MOUSE_FILTER_STOP

	# OPAQUE, ALWAYS. The pause card is deliberately translucent — it sits over a
	# puzzle somebody is in the middle of and should let it through — but that is
	# the CARD's job, not its buttons'. A translucent plate put the board inside
	# the controls, so the cube was visibly running through the middle of the
	# words on them. A control is a solid thing you press; whatever is behind the
	# screen stops at its edge.
	_plate = kit().framed(self, Palette.rust() if primary else Palette.PANEL,
			kit().edge_on() if primary else kit().edge())
	_edge = get_node("edge")

	# --t-sm on a control, --t-md on a primary; black on the rust, because the
	# shipped rule is literally color:#000
	_label = kit().label(self, "label", shown, size,
			Palette.VOID if primary else Palette.INK, kit().Anchor.MIDDLE_CENTER,
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	set_process(true)


func build_link(shown: String, size: int, tint: Color, on_click) -> void:
	_on_click = on_click
	mouse_filter = Control.MOUSE_FILTER_STOP
	_label = kit().label(self, "label", shown, size, tint, kit().Anchor.MIDDLE_CENTER,
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	set_process(true)


func set_text(text: String) -> void:
	if _label != null:
		_label.text = text


func label_node() -> Label:
	return _label


func set_primary(on: bool) -> void:
	_primary = on
	if _plate != null:
		_plate.modulate = Palette.rust() if on else Palette.PANEL
	if _edge != null:
		_edge.modulate = kit().edge_on() if on else kit().edge()
	if _label != null:
		_label.add_color_override("font_color", Palette.VOID if on else Palette.INK)

	# the glow is a SIBLING, because a child can never draw behind its parent's
	# own graphic — see UiKit._plated
	var parent := get_parent()
	if parent != null and parent.has_node(name + "_glow"):
		parent.get_node(name + "_glow").visible = on


func _set_enabled(v: bool) -> void:
	enabled = v
	mouse_filter = Control.MOUSE_FILTER_STOP if v else Control.MOUSE_FILTER_IGNORE
	if not v:
		_held = false
		_want = 1.0


func _gui_input(event: InputEvent) -> void:
	if not enabled:
		return
	if event is InputEventMouseButton or event is InputEventScreenTouch:
		var pressed: bool = event.pressed
		if pressed:
			_held = true
			_want = 0.8
			accept_event()
		elif _held:
			_held = false
			_want = 1.0
			accept_event()
			if _on_click != null and _on_click.is_valid():
				_on_click.call_func()


func _notification(what: int) -> void:
	if what == NOTIFICATION_MOUSE_EXIT:
		if _held:
			_held = false
			_want = 1.0


func _process(dt: float) -> void:
	if abs(_tint - _want) < 0.001:
		return
	# fadeDuration 0.06
	_tint = lerp(_tint, _want, clamp(dt / 0.06, 0.0, 1.0))
	var k := _tint
	if _plate != null:
		var base: Color = Palette.rust() if _primary else Palette.PANEL
		_plate.modulate = Color(base.r * k, base.g * k, base.b * k, base.a)
	elif _label != null:
		# a link has no plate, so the press is carried by the type
		_label.modulate = Color(1, 1, 1, lerp(0.5, 1.0, k))
