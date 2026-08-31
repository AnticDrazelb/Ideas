class_name Layout
# A TELEVISION HAS NO TOUCHSCREEN, AND A TABLET IS NOT A TELEVISION.
#
# Three shapes, and the difference between them is not width, it is what the
# player is holding:
#
#   HELD      a phone in portrait. The board fits the width and the two HUD
#             bands take the rest. This is the shape everything is composed
#             against.
#   TURNED    the same device on its side. The board fits the HEIGHT now, and
#             the bands stay where they are — which is why they are anchored to
#             edges rather than sized as fractions.
#   DOCKED    wide, landscape and no touchscreen at all: a television, or a
#             desktop pretending to be one.
#
# THE OVERSCAN MARGIN IS NOT A DESIGN DECISION, IT IS A PLATFORM RULE. A TV
# throws away the outer few percent of the picture, so nothing that has to be
# read may live there. It applies only where the display is actually wide enough
# to be one, because insetting a phone by three and a half percent for no reason
# is just a smaller game.

const REF_WIDTH := 720.0
const REF_HEIGHT := 1280.0

# The scaler's match, and the exponent canvas_scale raises each ratio to. UiKit
# reads it rather than repeating it — one number, so the pixel arithmetic in this
# file cannot describe a canvas the game is not drawing on.
const CANVAS_MATCH := 0.5

const TOP_BAND := 162.0
const BOTTOM_BAND := 128.0

# THE TITLE'S OWN TWO BANDS, and they are not the HUD's.
#
# The front screen reserves a masthead — wordmark, the vault and cube it will
# resume, and the rule that closes them — and a block of controls stacked up from
# the floor. What is left between the two is the only place the machine itself
# can be, and it is the one thing on that screen worth looking at.
#
# They live here rather than as literals in Screens because three separate things
# have to agree about them: the screen that draws the masthead, the scrim that
# has to be clear where the cube is, and the camera that frames the cube into the
# gap.
const TITLE_MAST_BOTTOM := 346.0

# The controls, measured up from the plate's floor. See Screens.build_title.
const TITLE_FOOT_TOP := 452.0

# How far the title's scrim takes to get the black off the type, at each end. The
# machine may not stand inside it — a cube under a gradient is the smudge the
# whole restaging is about — so the band it is framed into starts past it.
const TITLE_SCRIM_RAMP := 30.0

# How far the machine is allowed to stand BEHIND the controls.
#
# The four buttons and the primary are opaque plates, so a solid passing behind
# them is occluded rather than muddled, and the scrim over that band is opaque
# too, so it cannot peek through the gutters between them either.
const TITLE_BEHIND := 90.0

# THE APERTURE'S OWN INSET — how far the stroke stands off whatever it is hung
# on, at the sides and at the ends.
#
# These two numbers were literals in the HUD, where they drew a stroke, and the
# camera had never heard of them: it fitted the cube to the whole board rect, so
# the moment a fold turned the solid past square it grew wider than its own frame
# and crossed the line that is supposed to be the edge of the window. One
# definition, three consumers now — the stroke, the rect the camera contains the
# solid inside, and the four fold marks that hang on the stroke's inner edge.
const APERTURE_X := 16.0
const APERTURE_Y := 10.0

# HOW MUCH ROOM THE CAMERA LEAVES ROUND THE CUBE, and it lives here rather than
# in the camera's signature because the HUD now has to know it. Fit sizes the
# solid so that n * this many world units span the shorter side of the window,
# which is the same as saying the drawn cube is 1/1.28 of that side and the rest
# is air.
const FIT_MARGIN := 1.28

const _TOUCH := []


static func screen_size() -> Vector2:
	var tree := Engine.get_main_loop()
	if tree != null and tree is SceneTree:
		var root: Viewport = (tree as SceneTree).root
		if root != null:
			return root.get_visible_rect().size
	return Vector2(REF_WIDTH, REF_HEIGHT)


# The same 1.18 the web build uses: a little past square, so a near-square tablet
# stays "held".
static func is_landscape() -> bool:
	var s := screen_size()
	return s.x > s.y * 1.18


# Whether this device has a touchscreen at all. Cached: it cannot change, and it
# is the question that separates a tablet on its side from a TV.
static func has_touch() -> bool:
	if _TOUCH.empty():
		_TOUCH.append(OS.has_touchscreen_ui_hint())
	return _TOUCH[0]


static func is_docked() -> bool:
	return not has_touch() and is_landscape() and screen_size().x >= 900


# Fraction of the shorter axis to give up at every edge. Zero unless docked.
static func overscan() -> float:
	return 0.035 if is_docked() else 0.0


static func overscan_pixels() -> int:
	var s := screen_size()
	return int(round(min(s.x, s.y) * overscan()))


# THE HOUSING IS NOT ONE NUMBER ANY MORE.
#
# It was, while it was drawn: a border you could make as thick as you liked and
# the same on all four sides. The case is a photograph of a real object now, and
# that object's bezel is half again as deep at the top and bottom as it is at the
# sides — so there are four numbers, and they live in Chassis because they are
# measurements OF THE ART. This is only the seam.
static func chassis_left() -> float:
	return Chassis.inset_left()


static func chassis_right() -> float:
	return Chassis.inset_right()


static func chassis_top() -> float:
	return Chassis.inset_top()


static func chassis_bottom() -> float:
	return Chassis.inset_bottom()


# THE GLASS, IN THE UNITS EVERY SCREEN IS AUTHORED IN.
#
# A screen that has to do arithmetic about its own height — the Forge is the one,
# because its deck takes whatever the fixed bands below it do not — used to do it
# against the literal 1280. That was the canvas, and the canvas is not what a
# screen gets: it is inset into the opening, and the housing takes a hundred and
# fifty units of height. So the Forge sized its deck as though it had the whole
# display, and the bottom row of the column went off the bottom of the plate.
#
# The reference numbers rather than the live rect, deliberately: these are read
# while the screen is being BUILT, before any layout has happened and while every
# rect in the game still measures zero.
static func plate_width() -> float:
	return REF_WIDTH - chassis_left() - chassis_right()


static func plate_height() -> float:
	return REF_HEIGHT - chassis_top() - chassis_bottom()


# THE SCALE THE INTERFACE IS ACTUALLY DRAWN AT, AND IT WAS NOT A MINIMUM.
#
# Everything in this file converts canvas units to pixels, and the C# used
# min(w/720, h/1280) to do it. The canvas does not: the scaler's match is 0.5,
# which is the GEOMETRIC MEAN of the two ratios, computed as
# pow(w/rw, 1-m) * pow(h/rh, m).
#
# On a 16:9 phone the two answers are within a per cent of each other, which is
# why nothing ever looked wrong. On 1440x2960 they are 2.000 and 2.151 — seven
# and a half per cent apart — so the camera believed the readout ended thirty-six
# pixels above where the readout drew itself, and every band in here was measured
# against a ruler the interface was not using. Two definitions of one number is
# the bug; this is the number, and the min is gone.
static func canvas_scale() -> float:
	var s := screen_size()
	if s.x <= 0.0 or s.y <= 0.0:
		return 1.0
	return pow(s.x / REF_WIDTH, 1.0 - CANVAS_MATCH) * pow(s.y / REF_HEIGHT, CANVAS_MATCH)


# The safe area in pixels, with the origin at the BOTTOM left, because every
# rectangle in this file is measured the way the C# measures it. Godot reports it
# top-down; the flip is here and nowhere else.
static func safe_area() -> Rect2:
	var s := screen_size()
	var r := OS.get_window_safe_area()
	if r.size.x <= 0 or r.size.y <= 0:
		return Rect2(0, 0, max(1, s.x), max(1, s.y))
	return Rect2(r.position.x, s.y - r.position.y - r.size.y, r.size.x, r.size.y)


# The glass: the safe area, less the overscan, less the housing. Every other
# rectangle in this file is cut out of this one, and it is exactly the rect the
# HUD's own root occupies — which is what lets the stroke and the camera be told
# about the window in the same sentence.
static func glass_rect() -> Rect2:
	var safe := safe_area()
	var ov := overscan_pixels()
	var s := canvas_scale()
	var left := chassis_left() * s + ov
	var right := chassis_right() * s + ov
	var top := chassis_top() * s + ov
	var bottom := chassis_bottom() * s + ov

	return Rect2(safe.position.x + left, safe.position.y + bottom,
			max(1.0, safe.size.x - left - right),
			max(1.0, safe.size.y - top - bottom))


static func aperture_rect() -> Rect2:
	return _inset(board_rect())


# WHERE THE MACHINE GOES ON THE FRONT SCREEN — the gap between the masthead and
# the controls, which is a different rectangle from the play window and was being
# framed with the play window's centre.
#
# The attract cube was fitted to board_rect(), whose centre is pulled down by the
# HUD's two bands being different heights — bands that are not even drawn on this
# screen. So the one object the title is about sat seventy units low, crowding
# the primary and leaving a hole under the rule.
static func title_rect() -> Rect2:
	var g := glass_rect()
	var s := canvas_scale()

	# The top is the masthead plus the scrim's ramp: the machine may not stand
	# where the type is, and it may not stand in the gradient that gets the black
	# off the type either — a cube under a ramp is the smudge this whole change
	# is about.
	var top := (TITLE_MAST_BOTTOM + TITLE_SCRIM_RAMP) * s
	var bottom := (TITLE_FOOT_TOP - TITLE_BEHIND) * s

	# A SHORT DEVICE STILL GETS A BAND. The masthead and the controls are fixed
	# heights and the plate is not, so on the shortest phone this has to answer
	# is a sliver — and a sliver is still where the cube goes. It is never
	# allowed to invert, which would put the cube behind the wordmark.
	var h: float = max(120.0 * s, g.size.y - top - bottom)
	var y: float = g.position.y + max(0.0, g.size.y - top - h)
	return Rect2(g.position.x, y, g.size.x, h)


# THE WINDOW, SAID IN THE UNITS THE HUD BUILDS IN — insets into the glass, so the
# stroke can be hung on the same rectangle the camera is framing against instead
# of on a second guess at it. Returns [left, bottom, right, top].
static func aperture_insets() -> Array:
	var g := glass_rect()
	var a := aperture_rect()
	var s: float = max(0.0001, canvas_scale())
	return [
		(a.position.x - g.position.x) / s,
		(a.position.y - g.position.y) / s,
		(g.end.x - a.end.x) / s,
		(g.end.y - a.end.y) / s,
	]


# The rectangle left for the board, in pixels, after the safe area, the overscan,
# the housing and the two HUD bands.
#
# THE BANDS ARE MEASURED FROM THE GLASS, not from the display. They are the space
# the HUD occupies, the HUD is inset into the opening, and a band measured from
# the display edge would have counted the bezel twice — which is how the cube
# ends up sitting a bezel's width higher than the aperture drawn around it.
static func board_rect() -> Rect2:
	var g := glass_rect()
	var s := canvas_scale()
	var top := TOP_BAND * s
	var bottom := BOTTOM_BAND * s
	var free := Rect2(g.position.x, g.position.y + bottom,
			max(1.0, g.size.x), max(1.0, g.size.y - top - bottom))
	return _window(free)


# THE WINDOW IS AS WIDE AS THE BOARD AND AS TALL AS THE BAND.
#
# Everything above measures the space the board is ALLOWED, and on a 20:9 phone
# that space is 1236 by 2008. The board is a square fitted to the shorter of
# those, so it is 1236 either way.
#
# The window used to be cut to that square, in both axes. That fixed a real thing
# — a frame twice as tall as its contents reads as contents that failed to load,
# not as air — and it paid for it with two bands of dead plate: about four hundred
# pixels under the rule that closes the readout, and four hundred more over the
# three controls, each a stretch of case with nothing in it and no edge to say
# why. The housing is a photograph of an instrument, and an instrument does not
# have unaccounted-for gaps between its window and its chrome.
#
# So the cut is in ONE axis now, not two: to the board across, and to the whole
# band down. The stroke's top lands APERTURE_Y under the readout's rule and its
# bottom the same distance over the control band.
#
# THE BOARD DOES NOT MOVE AND DOES NOT CHANGE SIZE. Fit sizes the cube from the
# SHORTER side of this rect and offsets it by the rect's CENTRE. The shorter side
# is still the width, and taking the height out to the full band about its own
# centre does not move the centre.
#
# LANDSCAPE IS NOT A SPECIAL CASE, it is the same rule with the other answer.
# Turned, the bands bind the HEIGHT, so the shorter side is the height, the width
# is cut to it, and the window comes out the square it already was.
static func _window(r: Rect2) -> Rect2:
	var w: float = max(1.0, min(r.size.x, r.size.y))
	var h: float = max(1.0, r.size.y)
	var c := r.position + r.size * 0.5
	return Rect2(c.x - w * 0.5, c.y - h * 0.5, w, h)


# THE SQUARE THE BOARD ACTUALLY OCCUPIES, concentric with the window.
#
# The window is the stroke; this is the shape of the thing inside it. On a phone
# the two differ by the eight hundred pixels of height the window took back, and
# something still has to know the smaller number — the camera sizes the cube from
# the shorter side of the window, and this is the square that side describes.
static func board_square() -> Rect2:
	var w := board_rect()
	var side: float = max(1.0, min(w.size.x, w.size.y))
	var c := w.position + w.size * 0.5
	return Rect2(c.x - side * 0.5, c.y - side * 0.5, side, side)


# WHERE THE SOLID HAS TO STAY, WHICH IS NO LONGER THE WHOLE WINDOW.
#
# The camera pulls back until the leaned or mid-fold solid fits inside a
# rectangle. That rectangle was the aperture, and while the aperture was square
# the two questions had one answer. They do not any more: containing against the
# tall window would make a horizontal fold cost fifteen per cent and a vertical
# one cost nothing, because the height would never be the binding constraint
# again. A solid is as tall as it is wide and it turns both ways; the framing it
# provokes should not care which way.
static func contain_rect() -> Rect2:
	return _inset(board_square())


# THE BAND BETWEEN THE STROKE AND THE BOARD, in canvas units, at each of the
# window's two ends.
#
# This is the space the window took back, and it is where the plate clock, the
# caption, the toast and the coach's line go — so it is worth a number rather
# than an eyeball. It is symmetric because the fit centres the cube on the rect's
# centre and this is cut about the same one.
static func clear_band() -> float:
	var w := board_rect()
	var shorter: float = max(1.0, min(w.size.x, w.size.y))
	var drawn := shorter / FIT_MARGIN
	return max(0.0, (w.size.y - drawn) * 0.5) / max(0.0001, canvas_scale())


# The stroke's own inset, applied to a rect. One definition, two callers — the
# window the player sees and the square the camera works against — so the two
# cannot disagree about how thick the frame is.
static func _inset(b: Rect2) -> Rect2:
	var s := canvas_scale()
	var ix := APERTURE_X * s
	var iy := APERTURE_Y * s
	return Rect2(b.position.x + ix, b.position.y + iy,
			max(1.0, b.size.x - ix * 2.0),
			max(1.0, b.size.y - iy * 2.0))
