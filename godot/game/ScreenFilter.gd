class_name ScreenFilter
extends CanvasLayer
# A DARK GAME ON A BRIGHT DAY IS AN UNREADABLE GAME.
#
# The phone's own brightness slider moves the whole system, not this — and a
# puzzle whose entire readout is "is this cell brighter than that one" is exactly
# the kind of thing that stops working in sunlight while every other app on the
# device is fine.
#
# Both controls default to 100, and AT 100 NOTHING IS DRAWN AT ALL: the layer is
# hidden, so the cost of these two settings is zero for anyone who leaves them
# alone.
#
# IT IS ABOVE EVERY CANVAS, NOT ON THE CAMERA, and that is the same decision the
# C# makes for the same reason: the readout, the housing and every card have to
# be filtered too, or the board dims and the words over it do not.
#
# WHAT IS NOT PORTED IS THE STACK OF BLENDED QUADS, and the reason is worth
# recording because it is the one place this port is simpler on purpose. Unity's
# interface is a screen-space overlay drawn after the camera's post pass, so
# nothing that reads the frame can reach it; the C# therefore performs
# `out = in*b*c + 0.5*(1 - c)` in BLEND MODES across up to three overlay quads,
# solving the decomposition at runtime and swapping to reverse-subtract when the
# contrast goes past 100 and the offset turns negative.
#
# That machinery shipped two real bugs — a gain of c with an offset of (b - 1),
# which turns the brightness control into an addition and collapses every dark
# value into the same white; and `Blend DstColor Zero` written as the literal 4,
# which is OneMinusDstColor, so every quad inverted the screen and the rust
# interface came out blue the moment either slider left 100.
#
# A canvas layer that reads the screen has neither problem. The formula is
# written once, in the order it is written down, in filter.shader.

# ABOVE EVERYTHING. The glass is at 25 and the housing at 5; this is the last
# thing between the picture and the eye.
const ORDER := 200

var _rect: ColorRect
var _mat: ShaderMaterial
var _bright_was := -1
var _contrast_was := -1


static func attach(under: Node) -> ScreenFilter:
	var f = Make.of("res://game/ScreenFilter.gd")
	f.name = "Screen filter"
	f.layer = ORDER
	under.add_child(f)
	f._compose()
	f.refresh()
	return f


func _compose() -> void:
	_rect = ColorRect.new()
	_rect.name = "filter"
	# A FULL-SCREEN GRAPHIC THAT EATS EVERY TAP IS THE CLASSIC WAY TO SHIP A GAME
	# NOBODY CAN PLAY, and this one is over the entire interface.
	_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_rect.anchor_right = 1.0
	_rect.anchor_bottom = 1.0
	# A MISSING SHADER MUST COST THE SETTING, NOT THE GAME.
	var sh = Shaders.get_shader("res://shaders/filter.shader")
	if sh != null:
		_mat = ShaderMaterial.new()
		_mat.shader = sh
		_rect.material = _mat
	add_child(_rect)
	visible = false


# Read the settings back out of the store; call after Calibrate changes one.
func refresh() -> void:
	var bi: int = Store.data().bright
	var ci: int = Store.data().contrast
	if bi == _bright_was and ci == _contrast_was:
		return
	_bright_was = bi
	_contrast_was = ci
	if _mat == null:
		return

	var on: bool = bi != 100 or ci != 100
	if visible != on:
		visible = on
	if not on:
		return

	_mat.set_shader_param("brightness", bi / 100.0)
	_mat.set_shader_param("contrast", ci / 100.0)


# What the filter does to one channel, as arithmetic with nothing in it that
# needs an engine — so the harness can hold the shader to the formula rather than
# to a screenshot.
static func apply_to(x: float, bi: int, ci: int) -> float:
	var v := x * (bi / 100.0)
	v = (v - 0.5) * (ci / 100.0) + 0.5
	return clamp(v, 0.0, 1.0)
