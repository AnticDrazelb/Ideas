extends UiRect
# ONE TEXEL WIDE BY ONE DEVICE PIXEL PER ROW, rebuilt when that number changes.
#
# The same shape of watcher the chassis uses, and for the same reason: this
# cannot be drawn until something has a size, and the size is not known when the
# node is made.
#
# ---- THE PANEL IS ALIVE ----------------------------------------------------
#
# A case, a pane of dirty glass and a line pitch, and the whole thing perfectly
# still. Everything in this machine was drawn by somebody and nothing in it is
# POWERED, which is a difference the eye reads immediately even when it cannot
# say what it is looking at.
#
# Three behaviours, and they are deliberately almost nothing:
#
#   THE BREATHE. The rows brighten and fade by a tenth of their own strength, on
#   two slow waves that do not share a period so it never settles into a pulse.
#   It is under the threshold at which you would call it an animation; it is over
#   the one at which a display looks switched on.
#
#   THE ROLL. Once every half a minute or so, a soft band travels down the glass
#   — the frame sync of something that has not been serviced in thirty years.
#   Rare enough that it reads as the machine's fault rather than as a screensaver.
#
#   THE WARM-UP. The same band, once, faster and brighter, when the thing is
#   switched on.
#
# ADDITIVE, LIKE THE GRIME. It can only put light on the glass, never take it
# away, so nothing that was legible stops being — and the whole lot stops dead
# when motion is set to STILL, which is what that setting is for.

var _img: TextureRect
var _tex: ImageTexture
var _rows := 0

var _band: UiRect          # the roll, parked off-screen until it runs
var _band_img: TextureRect
var _t := -1.0
var _dur := 0.0
var _peak := 0.0
var _next := 24.0


func setup() -> void:
	_img = TextureRect.new()
	_img.name = "lines"
	_img.expand = true
	_img.stretch_mode = TextureRect.STRETCH_SCALE
	_img.modulate = Color(1, 1, 1, 1)       # the texture carries the whole effect
	_img.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_img.anchor_right = 1.0
	_img.anchor_bottom = 1.0
	add_child(_img)
	set_process(true)


# Send the band down the glass once.
func sweep(dur: float, peak: float) -> void:
	if Access.motion_amount() <= 0.0:
		return
	_t = 0.0
	_dur = max(0.2, dur)
	_peak = peak


func _process(dt: float) -> void:
	_refit()

	var motion := Access.motion_amount()
	var now := float(OS.get_ticks_msec()) / 1000.0

	# THE BREATHE. Two waves that do not share a period, so it never lands on a
	# beat you could count.
	var lift := 0.0
	if motion > 0.0:
		lift = (sin(now * 0.61) * 0.6 + sin(now * 0.23) * 0.4) * 0.10 * motion
	_img.modulate = Color(1, 1, 1, clamp(Scanlines.STRENGTH * (1.0 + lift), 0.0, 1.0))

	if motion <= 0.0:
		if _band != null and _band.visible:
			_band.visible = false
		return

	if _t < 0.0:
		_next -= dt
		if _next <= 0.0:
			sweep(1.7, 0.30)
			_next = rand_range(26.0, 44.0)
		return

	_t += dt
	var k := _t / _dur
	if k >= 1.0:
		_t = -1.0
		if _band != null:
			_band.visible = false
		return

	if _band == null:
		_build_band()
	if not _band.visible:
		_band.visible = true

	# down the glass, and fading out as it goes
	_band.anchor_top = k
	_band.anchor_bottom = k
	_band.margin_top = -60.0
	_band.margin_bottom = 60.0
	var fade := sin(k * PI)                 # in and out, no hard ends
	_band_img.modulate = Color(1, 1, 1, _peak * fade * motion)


func _build_band() -> void:
	_band = UiKit.rect(self, "roll", Vector2(0, 1), Vector2(1, 1), Vector2.ZERO, Vector2.ZERO)

	# a soft bar: no edges to catch on, because a hard-edged band travelling down
	# a screen is a wipe and this is a fault
	var h := 64
	var img := Image.new()
	img.create(1, h, false, Image.FORMAT_RGBA8)
	img.lock()
	for y in range(h):
		var u := y / float(h - 1)
		var a := sin(u * PI)
		img.set_pixel(0, y, Color(1, 1, 1, a * a))
	img.unlock()
	var t := ImageTexture.new()
	t.create_from_image(img, Texture.FLAG_FILTER)

	_band_img = TextureRect.new()
	_band_img.name = "bar"
	_band_img.texture = t
	_band_img.expand = true
	_band_img.stretch_mode = TextureRect.STRETCH_SCALE
	_band_img.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_band_img.anchor_right = 1.0
	_band_img.anchor_bottom = 1.0
	_band_img.material = Glass.additive()
	_band.add_child(_band_img)


func _refit() -> void:
	var scale := Layout.canvas_scale()
	if scale <= 0.0:
		return
	var rows := int(round(rect_size.y * scale))
	if rows < 8 or rows == _rows:
		return
	_rows = rows
	_paint(rows, scale)


func _paint(rows: int, scale: float) -> void:
	# whole pixels, and at least one lit row between every dark one, or a pitch
	# of two on a low-density display would be a grey wash
	var pitch: int = int(max(2, round(Scanlines.PITCH * scale)))
	var weight: int = int(clamp(round(Scanlines.WEIGHT * scale), 1, pitch - 1))

	var img := Image.new()
	img.create(1, rows, false, Image.FORMAT_RGBA8)
	img.lock()
	var a: float = clamp(Scanlines.STRENGTH, 0.0, 1.0)
	for y in range(rows):
		# counted from the TOP, so the pattern is anchored to the top of the
		# opening and does not shuffle when the height changes. Godot's images
		# are written downward already, so this is the row index itself where the
		# C# has to subtract it from the height.
		img.set_pixel(0, y, Color(0, 0, 0, a if (y % pitch) < weight else 0.0))
	img.unlock()

	# POINT, NOT BILINEAR. At one to one there is nothing to interpolate, and a
	# half-pixel of drift should move which row is dark rather than smear every
	# line across two.
	_tex = ImageTexture.new()
	_tex.create_from_image(img, 0)
	_img.texture = _tex
