extends Reference
class_name InputRouter

# INPUT — one finger, three verbs, and the clash taken out.
#
# A gesture is a TAP until it has travelled far enough to be a drag, and once
# it is a drag it is locked to one axis for the rest of its life. No gesture
# ever changes its mind halfway, because a control that reinterprets itself
# under the thumb feels broken even when it is right.
#
# THE THIRD VERB COSTS THE OTHER TWO NOTHING. A gesture is a tap until it
# travels (drag) or until it stays (matrix). The timer is armed on every touch
# and disarmed by the first pixel of travel, so a drag can never become a peek
# and a peek can never become a walk — once the lattice is glass, letting go
# only closes it.
#
# ONE FLIP, AT THE DOOR. Every pointer position in this file is in the ORIGINAL
# convention: pixels from the BOTTOM-left, y growing upward. Godot hands them
# down-from-the-top and unproject_position gives them back the same way, so both
# are flipped once on the way in and once on the way out (see _point and
# cell_at_point) and the whole body below — the axis pick, the turn indices, the
# orbit signs — is the original arithmetic, unchanged. The alternative is a sign
# decision at every use, which is exactly the bug the original carries a long
# comment about: correct arithmetic on an input that changed sign underneath it.

const TAP_SLOP := 14.0
const HOLD_SECONDS := 0.210

var _s: Session
var _view: CubeView
var _cam: Camera

var _start := Vector2.ZERO
var _last := Vector2.ZERO
var _down_at := 0.0
var _down := false
var _peeking := false

var enabled := true

# THE AUDIO BUS. The original carries a long note here about `?.` being a guard
# that is not one — it tests the managed handle, which outlives the destroyed
# component behind it. GDScript has no such split: a freed Reference is null and
# `!= null` is the whole story. The note is kept because the shape of the bug is
# worth remembering, not because this line can still have it.
var sfx = null

# Godot has no edge-triggered polling, so the edges are kept here. Mouse buttons
# double as touch: the engine's touch emulation is on, and one finger is all this
# game has ever wanted.
var _held_last := false
var _keys_last := {}


func _init(s: Session, view: CubeView, cam: Camera) -> void:
	_s = s
	_view = view
	_cam = cam


static func _px_per_quarter() -> float:
	var sz := Layout.screen_size()
	return max(90.0, min(sz.x, sz.y) * 0.40)


# Godot's pointer is measured down from the top; everything below wants it up
# from the bottom. This is the only place that knows.
func _point() -> Vector2:
	var p: Vector2 = _view.cube.get_viewport().get_mouse_position()
	return Vector2(p.x, Layout.screen_size().y - p.y)


func _key_down(code: int) -> bool:
	var now := Input.is_key_pressed(code)
	var was: bool = _keys_last.get(code, false)
	_keys_last[code] = now
	return now and not was


func _has_axis_safe() -> bool:
	return _s.drag != null and _s.drag.has_axis


func tick(dt: float) -> void:
	if not enabled or _s.lv == null:
		release_peek()
		return

	var held := Input.is_mouse_button_pressed(BUTTON_LEFT)
	var p := _point()
	var down_now := held and not _held_last
	var up_now := (not held) and _held_last
	_held_last = held

	if down_now:
		_do_down(p)
	elif _down and held:
		_do_move(p)
	elif _down and up_now:
		_do_up(p)

	if _down and not _peeking and not _has_axis_safe() \
			and _unscaled_time() - _down_at > HOLD_SECONDS \
			and (p - _start).length_squared() < TAP_SLOP * TAP_SLOP:
		peek_on()

	# the matrix eases in and out rather than snapping, so a quick hold that
	# resolves as a tap does not strobe the board
	_view.peek_amt = move_toward(_view.peek_amt, 1.0 if _peeking else 0.0, dt * 6.0)

	_keyboard()


# Unity's unscaled clock, which is the wall clock. TimeBend bends the game's
# delta and must never bend the hold, or a slow-motion collapse would make the
# matrix take four times as long to open.
static func _unscaled_time() -> float:
	return OS.get_ticks_msec() / 1000.0


func _do_down(p: Vector2) -> void:
	if _s.won or _s.anim != null or _s.walking != null:
		return
	_down = true
	_start = p
	_last = p
	_down_at = _unscaled_time()
	_s.drag = Session.Drag.new()


func _do_move(p: Vector2) -> void:
	if _s.drag == null:
		return
	var d := p - _start

	if _peeking:
		# ORBIT. The hold has already landed, so this gesture is looking rather
		# than folding: it moves the camera one-to-one with the thumb, at the
		# same pixels-per-quarter-turn the drag uses.
		var q := _px_per_quarter() * 0.66
		_view.peek_yaw += (p.x - _last.x) / q * (PI / 2.0)

		# PLUS, NOT MINUS — and the minus is where it came from. The web original
		# runs in canvas coordinates, where y grows DOWNWARD, so it writes
		# `pitch -= dy` to mean "pull the front face down and the top tips toward
		# you". These coordinates grow UPWARD (see the note at the top of the
		# file), so the same expression produces the opposite gesture.
		_view.peek_pitch = clamp(_view.peek_pitch + (p.y - _last.y) / q * (PI / 2.0),
				-CubeView.PEEK_MAX_PITCH, CubeView.PEEK_MAX_PITCH)
		_last = p
		return

	if not _s.drag.has_axis:
		if abs(d.x) < TAP_SLOP and abs(d.y) < TAP_SLOP:
			return
		_s.drag.has_axis = true
		_s.drag.axis_x = abs(d.x) > abs(d.y)

	var amt: float = d.x if _s.drag.axis_x else d.y
	var qq: float = clamp(amt / _px_per_quarter(), -1.0, 1.0)
	var was: float = abs(_s.drag.ang)
	_s.drag.ang = qq * PI / 2.0

	# the vault complains as it moves, and complains louder near the detent — so
	# the commit threshold is something you can hear coming
	if abs(_s.drag.ang) - was > 0.12 and sfx != null:
		sfx.creak(abs(qq))
	if was < PI / 4.0 and abs(_s.drag.ang) >= PI / 4.0:
		Haptics.buzz(Haptics.FOLD)
	_last = p


func _do_up(p: Vector2) -> void:
	_down = false
	var drag = _s.drag
	if drag == null:
		release_peek()
		return

	if not drag.has_axis:
		var was_peek := _peeking
		_s.drag = null
		release_peek()
		if was_peek:
			return                       # a hold is looking, never walking
		var hit := cell_at_point(_start)
		if hit[0]:
			_s.tap_cell(hit[1], hit[2])
		return

	var ang: float = drag.ang
	var axis_x: bool = drag.axis_x
	if abs(ang) > PI / 4.0:
		var t: int
		if axis_x:
			t = 1 if ang > 0.0 else 0
		else:
			t = 2 if ang > 0.0 else 3
		if not _s.try_turn(t):
			_s.refuse_turn(t)
	else:
		_s.spring_back()


func peek_on() -> void:
	if _peeking:
		return
	_peeking = true
	if sfx != null:
		sfx.peek()
	_view.peek_yaw = CubeView.PEEK_YAW
	_view.peek_pitch = CubeView.PEEK_PITCH
	if Store.data().saw_peek == 0:
		Store.data().saw_peek = 1
		Store.save()


func release_peek() -> void:
	if not _peeking:
		return
	_peeking = false
	if sfx != null:
		sfx.peek_off()
	_view.peek_yaw = 0.0
	_view.peek_pitch = 0.0


func peeking() -> bool:
	return _peeking


# WHERE DID THEY ACTUALLY TAP?
#
# The web build needed two answers to this: arithmetic while the board was flat,
# and a search against the drawn picture while it was leaned over, because a view
# mode that draws one thing and hits another is not a view mode, it is a bug with
# a switch on it.
#
# Here there is one answer, and it is the honest one: ask the renderer. Project
# every surface cell exactly as it is drawn and take the nearest centre, with a
# radius so a tap into empty space stays a tap into empty space rather than
# snapping to whatever happened to be closest. It cannot drift from what is on
# screen, because it reads what is on screen.
#
# Returns [found, u, v] — GDScript 3.5 has no `out`.
func cell_at_point(screen: Vector2) -> Array:
	var bu := -1
	var bv := -1
	if _s.surf.empty():
		return [false, bu, bv]

	var screen_h: float = max(1.0, Layout.screen_size().y)
	# `Camera.size` is the world height of the WHOLE screen (the original's
	# orthographicSize is half of it — see CameraRig), so this is world units
	# per pixel either way round.
	var cell: float = _cam.size / screen_h
	var radius: float = 0.62 / (cell * cell)          # in squared pixels
	var bd := radius
	var found := false

	var xform: Transform = _view.cube.global_transform
	for u in range(_s.n):
		for v in range(_s.n):
			var s: Surf = _s.surf[u * _s.n + v]
			if not s.has:
				continue
			var world: Vector3 = xform.xform(CubeGeometry.cell_to_object(_s.n, s.w))
			var raw: Vector2 = _cam.unproject_position(world)
			var sp := Vector2(raw.x, screen_h - raw.y)
			var dx := sp.x - screen.x
			var dy := sp.y - screen.y
			var d2 := dx * dx + dy * dy
			if d2 < bd:
				bd = d2
				bu = u
				bv = v
				found = true

	return [found, bu, bv]


# Desktop, for anyone who opens this on one. The same split the thumb gets.
func _keyboard() -> void:
	if _s.won:
		return

	if Input.is_key_pressed(KEY_SPACE) or Input.is_key_pressed(KEY_SHIFT):
		peek_on()
	elif _peeking and not _down:
		release_peek()

	# All four edges are read every frame, not short-circuited: an `elif` chain
	# would leave the later keys' edges unrecorded, and an unrecorded key reads as
	# freshly pressed on the next frame. The engine the original ran on tracked
	# these itself and had no such trap.
	var left := _key_down(KEY_LEFT)
	var right := _key_down(KEY_RIGHT)
	var up := _key_down(KEY_UP)
	var down := _key_down(KEY_DOWN)
	var zed := _key_down(KEY_Z)

	var t := -1
	if left:
		t = 0
	elif right:
		t = 1
	elif up:
		t = 2
	elif down:
		t = 3

	if t >= 0:
		if _peeking:
			# held with the matrix open, the arrows orbit instead
			_view.peek_yaw += (-0.18 if t == 0 else (0.18 if t == 1 else 0.0))
			_view.peek_pitch = clamp(
					_view.peek_pitch + (0.18 if t == 2 else (-0.18 if t == 3 else 0.0)),
					-CubeView.PEEK_MAX_PITCH, CubeView.PEEK_MAX_PITCH)
		elif not _s.try_turn(t):
			_s.refuse_turn(t)

	if zed:
		_s.undo()
