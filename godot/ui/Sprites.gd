class_name Sprites
# THE FRAME, THE PILL AND THE GLOW, GENERATED.
#
# EVERY PRESSABLE THING IN THIS GAME IS A LIT FRAME, and it took a side-by-side
# with the shipped build to notice that the Unity port had been drawing flat
# rectangles instead. The brackets around a label say "this is pressable"; the
# frame is what makes the whole interface look like an INSTRUMENT rather than a
# list of words on black. It is not decoration — it is the difference the eye
# reads first.
#
# Generated rather than imported, and nine-sliced, so one 64px texture is every
# button, chip, field and card at every size, and the corner radius stays the
# same number of real pixels whatever the control's shape.
#
# ---- THE SHIPPED METRICS, EXTRACTED RATHER THAN EYEBALLED -------------------
#
# Every number below is read off the APK's own :root custom properties. The
# canvas here is 720 units wide against a viewport that is about 360 CSS pixels,
# so ONE CSS PIXEL IS TWO CANVAS UNITS and the conversion is the only arithmetic
# in it.
#
#   --r-btn      7px   -> 14    --h-btn   46px -> 92
#   --r-card    10px   -> 20    --h-ctl   44px -> 88
#   --rule       2px   ->  4    --h-row   50px -> 100
#   .btn         min-height  h-btn +  6px -> 104
#   .btn.primary min-height  h-btn + 14px -> 120
#   --edge      rgba(234,88,12,.34)   the border every control wears
#   primary     inset rgba(251,146,60,.55) + a 22px rust glow
#
# The border was twice as strong as this and the corners nearly twice as round,
# which together are most of why the controls read as an app's rather than an
# instrument's.

const FRAME_TEX := 64
const FRAME_RAD := 14
const FRAME_SLICE := 20
const FRAME_STROKE := 4

# How far the primary's glow reaches past its own edge. 22px blur, -4 spread.
const GLOW_REACH := 36.0

const _CACHE := {}


# The signed distance to a rounded rectangle, negative inside. The same
# expression the frame, the pill, the disc and the glow are all cut from, so
# four shapes cannot disagree about what a corner is.
static func _sdf(x: float, y: float, half: float, rad: float) -> float:
	var dx := abs(x + 0.5 - half) - (half - rad)
	var dy := abs(y + 0.5 - half) - (half - rad)
	var o := sqrt(max(dx, 0.0) * max(dx, 0.0) + max(dy, 0.0) * max(dy, 0.0))
	return o + min(max(dx, dy), 0.0) - rad


static func _tex_from(img: Image) -> ImageTexture:
	var t := ImageTexture.new()
	t.create_from_image(img, Texture.FLAG_FILTER)
	return t


# The plate: a filled rounded rectangle. The edge: the same rectangle as a
# stroke, and the thing that lights up.
static func frame(stroke: bool) -> ImageTexture:
	var key := "frame_line" if stroke else "frame_fill"
	if _CACHE.has(key):
		return _CACHE[key]
	var img := Image.new()
	img.create(FRAME_TEX, FRAME_TEX, false, Image.FORMAT_RGBA8)
	img.lock()
	var h := FRAME_TEX * 0.5
	for y in range(FRAME_TEX):
		for x in range(FRAME_TEX):
			var d := _sdf(x, y, h, FRAME_RAD)
			var a: float
			if stroke:
				# a band hugging the edge from the inside, feathered both ways
				a = clamp(0.5 - d, 0.0, 1.0) * clamp(d + FRAME_STROKE + 0.5, 0.0, 1.0)
			else:
				a = clamp(0.5 - d, 0.0, 1.0)
			img.set_pixel(x, y, Color(1, 1, 1, clamp(a, 0.0, 1.0)))
	img.unlock()
	_CACHE[key] = _tex_from(img)
	return _CACHE[key]


# THE PRIMARY CASTS. In the shipped build this is one line of CSS —
# box-shadow: 0 0 22px -4px rgba(234,88,12,.7) — and it is most of what makes
# that button look switched ON rather than filled in.
#
# It is worth knowing that this is NOT the camera bloom the board gets. The
# interface draws after the camera's post pass, so no amount of bloom would ever
# have reached it; the glow was always going to have to be drawn. A soft-edged
# plate behind the button, additive, is the same picture for one more quad.
static func glow() -> ImageTexture:
	if _CACHE.has("glow"):
		return _CACHE["glow"]
	var n := 128
	var rad := 14.0
	var soft := 36.0
	var img := Image.new()
	img.create(n, n, false, Image.FORMAT_RGBA8)
	img.lock()
	var h := n * 0.5
	for y in range(n):
		for x in range(n):
			var dx := abs(x + 0.5 - h) - (h - soft - rad)
			var dy := abs(y + 0.5 - h) - (h - soft - rad)
			var o := sqrt(max(dx, 0.0) * max(dx, 0.0) + max(dy, 0.0) * max(dy, 0.0))
			var d := o + min(max(dx, dy), 0.0) - rad
			# a blur falls off faster than linear; squaring it is close enough to
			# a gaussian at this radius and costs nothing
			var a := clamp(1.0 - d / soft, 0.0, 1.0)
			img.set_pixel(x, y, Color(1, 1, 1, a * a))
	img.unlock()
	_CACHE["glow"] = _tex_from(img)
	return _CACHE["glow"]


static func glow_border() -> int:
	return int(36 + 14)


# ---- the pill --------------------------------------------------------------
#
# The same nine-slice trick with the radius run all the way to the half height,
# so the ends are semicircles at any width. A toggle wants to be a PILL rather
# than a small rectangle for one reason: a rectangle that says ON and a
# rectangle that says OFF are the same object with different text, and a pill
# with the knob at the other end is a different SHAPE. Shape reads across a
# room; a three-letter word does not.
#
# A NINE-SLICE BORDER HAS TO FIT INSIDE THE CONTROL IT IS STRETCHED OVER.
#
# These were one 64px stadium with a 30px border on every side. Sixty pixels of
# border in a forty-two pixel switch leaves nothing for the middle, so the border
# gets scaled down to fit — by a different amount on each axis, and by a
# different amount again on a six-pixel slider track. That is the whole of "the
# pills are not uniform and the sliders look like squashed circles": one sprite
# asked to be three sizes it could not be.
#
# So there are two, each sized for what it is stretched over, and the thin one is
# used only on the track.
static func round_tex(size: int, rad: int) -> ImageTexture:
	var key := "round%d_%d" % [size, rad]
	if _CACHE.has(key):
		return _CACHE[key]
	var img := Image.new()
	img.create(size, size, false, Image.FORMAT_RGBA8)
	img.lock()
	var h := size * 0.5
	for y in range(size):
		for x in range(size):
			var d := _sdf(x, y, h, rad)
			img.set_pixel(x, y, Color(1, 1, 1, clamp(0.5 - d, 0.0, 1.0)))
	img.unlock()
	_CACHE[key] = _tex_from(img)
	return _CACHE[key]


static func pill() -> ImageTexture:
	return round_tex(32, 16)


static func bar() -> ImageTexture:
	return round_tex(16, 8)


# The knob, and every other circle the interface needs.
static func disc() -> ImageTexture:
	return round_tex(64, 32)


# A vertical ramp, one pixel wide, whose alpha at each row is whatever the caller
# says. The title's two scrim pieces are the only users; see UiKit.stage.
#
# ROW ZERO IS THE TOP HERE AND THE BOTTOM IN THE C#, because Godot's images are
# written downward and Unity's textures upward. The callers pass "distance from
# my own anchored edge", so the flip lives here and neither of them has to know.
static func ramp(key: String, from_top: bool, alpha_at: FuncRef) -> ImageTexture:
	if _CACHE.has(key):
		return _CACHE[key]
	var n := 128
	var img := Image.new()
	img.create(1, n, false, Image.FORMAT_RGBA8)
	img.lock()
	for i in range(n):
		var t := i / float(n - 1)
		var from_edge := t if from_top else 1.0 - t
		img.set_pixel(0, i, Color(0, 0, 0, clamp(alpha_at.call_func(from_edge), 0.0, 1.0)))
	img.unlock()
	_CACHE[key] = _tex_from(img)
	return _CACHE[key]
