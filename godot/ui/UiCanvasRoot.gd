extends UiRect
# THE CANVAS SCALER, AND THE SAFE AREA, IN ONE NODE.
#
# Unity gets both from components; here they are the same two lines of a process
# step, because both answers change for the same reasons — a rotation, a window
# dragged wider, a notch that was not measured on the first frame.
#
# THE SCALE IS REPRODUCED RATHER THAN APPROXIMATED. Layout.canvas_scale computes
# the factor Unity's CanvasScaler would apply at match 0.5, and this applies it,
# so a child anchored at 0.5 with an offset of 128 units lands exactly where the
# C# puts it — and Layout's own pixel arithmetic is measuring the same ruler the
# interface is drawn with.
#
# A NOTCH IS NOT A SUGGESTION. The web original has a long note about this:
# env(safe-area-inset-*) reads zero inside an Android WebView however correctly
# the host sets viewport-fit, because a WebView is a View inside somebody else's
# Activity. Godot does not have that problem — OS.get_window_safe_area is the
# real measurement — but it does have the same requirement, and a HUD band under
# a camera cutout is just as unreadable either way.
#
# A TELEVISION THROWS AWAY THE OUTER FEW PERCENT OF THE PICTURE, so nothing that
# has to be read may live there. That is a platform rule rather than a taste, and
# it applies only where the display is actually wide enough to be one.

var _last := Rect2()
var _last_scale := 0.0


func setup() -> void:
	set_process(true)
	_apply()


func _process(_dt: float) -> void:
	_apply()


func _apply() -> void:
	var s := Layout.canvas_scale()
	var screen := Layout.screen_size()
	if screen.x <= 0 or screen.y <= 0:
		return

	var ov := Layout.overscan_pixels()
	# Godot reports the safe area top-down, which is the frame this node lives
	# in, so it is used as given here — Layout flips it because every rectangle
	# in that file is measured the way the C# measures it.
	var safe := OS.get_window_safe_area()
	if safe.size.x <= 0 or safe.size.y <= 0:
		safe = Rect2(0, 0, screen.x, screen.y)
	if ov > 0:
		safe = Rect2(safe.position.x + ov, safe.position.y + ov,
				max(1.0, safe.size.x - ov * 2.0), max(1.0, safe.size.y - ov * 2.0))

	if safe == _last and abs(s - _last_scale) < 0.0001:
		return
	_last = safe
	_last_scale = s

	# Anchored to nothing: the root is positioned and sized in pixels and then
	# scaled, so its children can be authored in reference units.
	anchor_left = 0.0
	anchor_top = 0.0
	anchor_right = 0.0
	anchor_bottom = 0.0
	rect_scale = Vector2(s, s)
	rect_position = safe.position
	rect_size = safe.size / max(0.0001, s)
