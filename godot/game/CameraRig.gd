class_name CameraRig
extends Node
# THE CAMERA, AND THE TRAUMA.
#
# ORTHOGRAPHIC AND AXIS-ALIGNED, ALWAYS. That is not a look, it is the rule: the
# collapse says every cell sharing a screen column is one place, and only a
# parallel projection down an axis makes that true of the picture as well as of
# the maths. A perspective camera would show you cells that the solver says are
# the same square landing on different pixels.
#
# Everything else here is feel. A landed fold gets an aimed kick along its own
# axis, trauma on top so the whole frame is briefly unsafe, and a punch of zoom,
# which is what makes it read as a hit rather than a slide.

var cam: Camera

var _trauma := 0.0
var _punch := 0.0
var _kick_x := 0.0
var _kick_y := 0.0
var _kick_decay := 0.0
var _sq_x := 1.0
var _sq_y := 1.0
var _sq_t := 1.0
var _base_size := 4.0
var _base_offset := Vector2.ZERO

# how far out the camera has had to pull to keep the solid in its window, and how
# big the solid is when it is square to us
var _room := 1.0
var _board_n := 0

# STAND BACK AND WATCH IT GO. An extra, held pull on the orthographic size that
# whoever set it is responsible for clearing — unlike the room, which is the
# framing and eases itself home, and unlike the punch, which is an impulse and
# decays. The exit is the only thing that uses it, and it wants the camera to
# stay out for the whole of the throw.
var pull := 0.0

# AIM AT ONE CELL AND CLOSE ON IT. Zero is the framing the game is played in; one
# is that cell filling the aperture with everything else outside it. Only the
# finale uses it — the board is a whole object every other second of the game and
# the camera has no business preferring part of it.
var close := 0.0
var at := Vector3.ZERO

var _noise := OpenSimplexNoise.new()


# EVERY IMPULSE IN THIS FILE GOES THROUGH ONE NUMBER, and at STILL that number is
# zero — not forty per cent, which is what it used to bottom out at. Forty per
# cent of a plate fire is still a plate fire to the player who cannot take one.
static func amount() -> float:
	return Access.motion_amount()


func init(into: Node) -> void:
	cam = Camera.new()
	cam.name = "Camera"
	cam.projection = Camera.PROJECTION_ORTHOGONAL
	cam.near = 0.01
	cam.far = 200.0
	cam.current = true
	cam.translation = Vector3(0, 0, 40.0)
	# AND IT IS NOT TURNED ROUND. A Godot camera looks down its OWN -Z, so one
	# standing at +40 with no rotation is already looking at the origin — which is
	# the same view the C# camera has looking down +Z from -40. The half turn that
	# used to be here read the sentence one word wrong and pointed the camera at
	# the empty half of the world: every frame was correct, drawn from behind.
	# The handedness the two engines disagree about is paid for once, in
	# CubeGeometry.to_object and rotation_for, and nothing else needs to know.
	into.add_child(cam)

	# THE PERLIN THE SHAKE IS MADE OF. Unity has one built in; Godot's nearest
	# is simplex, which is a different function with the same shape and the same
	# range — and the shake is two independent walks through a smooth field, so
	# what matters is that it is smooth and unrepeating, not which basis it uses.
	_noise.seed = 0x5eed
	_noise.octaves = 1
	_noise.period = 1.0
	set_process(false)


# Fit the cube into the space the HUD is not using, and CENTRE IT THERE rather
# than on the screen.
#
# That distinction is the whole of this method. Centring on the screen and hoping
# the bands do not reach is what puts the primary action of the game on top of
# the hero object the first time somebody adds a button or runs it on a shorter
# phone. The bands have real heights; the board takes exactly what is left
# between them and sits in the middle of that.
#
# It works the same in both orientations because it asks for a RECTANGLE and fits
# to whichever of its sides is smaller — landscape is not a special case, it is
# the same question with a different answer.
func fit(n: int, margin: float = Layout.FIT_MARGIN) -> void:
	fit_to(Layout.board_rect(), n, margin)


# FIT INTO A NAMED RECTANGLE, because the game has two of them.
#
# The play window is the square the HUD's stroke is drawn around. The title's is
# the gap between the masthead and the controls, which is a different shape in a
# different place — and the attract cube was being framed with the play window's
# centre, so it sat low on the one screen where it is the subject rather than the
# board.
func fit_to(board: Rect2, n: int, margin: float) -> void:
	var screen := Layout.screen_size()
	var screen_h: float = max(1.0, screen.y)

	# the cube needs this many world units across, allowing for the fact that a
	# cube mid-fold is wider than its face
	_board_n = n

	# THE MARGIN'S JOB CHANGED. It was here to leave room for a cube mid-fold,
	# and it never had enough — a fold is root two wide and this is 1.28, so the
	# solid left the aperture on every horizontal swipe. _room contains it
	# exactly now, which means this number no longer decides whether the cube
	# CLIPS. What it decides is how often the camera has to move: at 1.28 a
	# forty-five degree fold costs a twenty per cent pull-out and everything else
	# costs nothing. Tighter is a bigger board and a busier camera; that is the
	# whole trade.
	var need := n * margin
	var shorter: float = max(1.0, min(board.size.x, board.size.y))

	# `size` is half the world height of the WHOLE screen
	_base_size = need * screen_h / (2.0 * shorter)

	# and the board's centre is not the screen's centre
	#
	# THE TWO AXES DO NOT TAKE THE SAME SIGN, AND THAT IS THE FLIP TALKING.
	#
	# Move a camera right and what it is looking at goes left, so the offset that
	# puts the world origin at a given point on the screen is MINUS the drift.
	# That is the x line. The y line is the opposite because the board's viewport
	# is read with render_target_v_flip: the picture is mirrored top to bottom on
	# its way to the screen, so a camera moved up shows the origin higher rather
	# than lower, and the minus cancels.
	#
	# It has been one line with one sign since the port, and nothing caught it,
	# because until the display could turn NO RECTANGLE IN THIS GAME WAS EVER OFF
	# CENTRE HORIZONTALLY. The two bands are top and bottom; the title's band is
	# top and bottom; every drift the arithmetic had ever been asked for was
	# vertical, and vertical was the axis that happened to be right. Turned, the
	# window is the left three quarters of the glass and the board was drawn a
	# window's width to the right of it, half under the rail.
	var centre := board.position + board.size * 0.5
	var drift := centre - Vector2(screen.x * 0.5, screen_h * 0.5)
	var world_per_pixel := 2.0 * _base_size / screen_h
	_base_offset = -drift * world_per_pixel

	PerfWatch.settle()


func kick(mag: float, dx: float, dy: float) -> void:
	var a := amount()
	_kick_x += dx * mag * 0.012 * a
	_kick_y += dy * mag * 0.012 * a
	_kick_decay = 1.0


func shake(amt: float) -> void:
	_trauma = min(1.0, _trauma + amt * amount())


# HOLD THE FRAME AT A LEVEL, rather than shoving it once.
#
# shake ADDS, because every caller of it is an event: a fold lands, a plate
# fires, the machine breaks. Trauma decays at 2.4 a second, so an event's shove
# is gone in well under half a second and the next one stacks on whatever is left
# — which is exactly right for events and exactly wrong for a rumble that is
# supposed to RISE over two seconds. Called every frame, shake wins the race
# against the decay by about ten to one and pins trauma at maximum from a fifth
# of the way in; what you get is not a star waking up, it is the picture coming
# off its mount.
#
# This sets the floor instead. An event on top of it still reads, because the max
# is taken rather than the assignment — a rumble cannot quieten a hit that
# happens during it.
func rumble(amt: float) -> void:
	_trauma = max(_trauma, clamp(amt, 0.0, 1.0) * amount())


func punch(amt: float) -> void:
	_punch = clamp(_punch + amt * amount(), -0.12, 0.12)


func squash(axis_x: bool, amt: float) -> void:
	amt *= amount()
	_sq_x = 1.0 + amt if axis_x else 1.0 - amt * 0.6
	_sq_y = 1.0 - amt * 0.6 if axis_x else 1.0 + amt
	_sq_t = 0.0


# KEEP THE SOLID INSIDE ITS OWN WINDOW.
#
# A cube square to the camera is n across. The same cube turned forty-five
# degrees is n times root two, and corner-on it is n times root three — so a
# board fitted to the aperture at rest is half again too big for it the moment a
# fold starts, and the thing runs out through the frame that is supposed to be
# the edge of the window. It did.
#
# The fix is not a bigger margin. Reserving the worst case permanently costs
# thirty per cent of the board at rest, every second of play, to pay for a shape
# that exists for half of one animation — and it would still be wrong for the
# matrix, which the player can hold at any angle they like.
#
# So the camera measures. The cube's whole transform rotates, so its on-screen
# extent is the rotated axis-aligned box of a known solid: three dot products,
# exact, no bounds query and no guessing. The size needed to contain that inside
# the aperture is arithmetic, and the camera takes whichever is larger, that or
# the resting fit.
#
# OUT AT ONCE AND BACK SLOWLY. Easing outward would let the corner clip for the
# two frames the ease takes, which is the whole bug; the extent it is tracking is
# already a smooth function of the fold, so snapping to it IS smooth. Coming back
# is eased, because a camera that returns as fast as it left reads as a bounce.
#
# It is not gated on the motion setting, and that is deliberate. This is not an
# impulse on top of the picture, it is the framing of the picture: STILL turns
# off shake, kick, punch and the bent clock, and a player who asked for those to
# stop did not ask for the cube to leave the screen.
func _room_for(dt: float, cube: Spatial, contain: bool) -> float:
	var want := 1.0

	if contain and cube != null and _board_n > 1 and _base_size > 0.0001:
		var b := cube.global_transform.basis
		var rx := b.x
		var ry := b.y
		var rz := b.z

		# half the solid, plus a hair so it never sits ON the stroke
		var e := _board_n * 0.5 * 1.04
		var ex := e * (abs(rx.x) + abs(ry.x) + abs(rz.x))
		var ey := e * (abs(rx.y) + abs(ry.y) + abs(rz.y))

		# THE SQUARE, NOT THE STROKE. The window is taller than the board now —
		# it runs from the readout's rule to the control band — and containing
		# against that would give a vertical fold all the room it wants while a
		# horizontal one still pays. See Layout.contain_rect: this is the rect
		# the aperture used to be.
		var ap := Layout.contain_rect()
		var screen_h: float = max(1.0, Layout.screen_size().y)

		# `size` is half the world height of the WHOLE screen, so the window only
		# sees the fraction of it that it covers
		var need: float = max(ex * screen_h / ap.size.x, ey * screen_h / ap.size.y)
		want = max(1.0, need / _base_size)

	if want > _room:
		_room = want
	else:
		_room = move_toward(_room, want, dt * 1.1)
	return _room


func tick(dt: float, cube: Spatial, contain: bool = true) -> void:
	_trauma = max(0.0, _trauma - dt * 2.4)
	_punch = move_toward(_punch, 0.0, dt * 0.42)
	_kick_decay = max(0.0, _kick_decay - dt * 5.2)
	_kick_x = move_toward(_kick_x, 0.0, dt * 2.6)
	_kick_y = move_toward(_kick_y, 0.0, dt * 2.6)
	_sq_t = min(1.0, _sq_t + dt * 5.0)

	var t2 := _trauma * _trauma
	var now := float(OS.get_ticks_msec()) / 1000.0
	var sx := _noise.get_noise_2d(now * 34.0, 0.0) * t2 * 0.55
	var sy := _noise.get_noise_2d(0.0, now * 31.0) * t2 * 0.55

	var cl: float = clamp(close, 0.0, 1.0)
	var aim := _base_offset.linear_interpolate(Vector2(at.x, at.y), cl * cl * (3.0 - 2.0 * cl))

	# AND THE CLOSE IS NOT GATED ON MOTION. It is not an impulse on top of the
	# picture, it is the framing — the same argument the room makes. A player who
	# asked the board to hold still did not ask to be shown the wrong part of it.
	var zoom: float = lerp(1.0, 0.16, cl)

	# SHAKE IS A SCREEN GESTURE MEASURED IN WORLD UNITS, and those two agree only
	# at one zoom. Trauma of one is half a world unit against a four-unit frame —
	# a fifth of the height, which reads as a hit; the same half unit against the
	# closed frame's 0.64 is most of the screen, and the shockwave that is
	# supposed to rattle the star would instead fling it out of shot and shake an
	# empty picture. So every offset travels with the aperture and the AMPLITUDE
	# ON SCREEN is what stays constant, which is the only part of it anybody can
	# see.
	cam.translation = Vector3(
			aim.x + (sx + _kick_x * _kick_decay) * zoom,
			aim.y + (sy + _kick_y * _kick_decay) * zoom,
			40.0)
	cam.size = _base_size * _room_for(dt, cube, contain) \
			* (1.0 + pull * amount()) * (1.0 - _punch) \
			* zoom * 2.0

	if cube != null:
		var k := 1.0 - _sq_t * _sq_t
		cube.scale = Vector3(lerp(1.0, _sq_x, k), lerp(1.0, _sq_y, k), 1.0)
