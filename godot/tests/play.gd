extends SceneTree
# PLAY THE GAME, WITHOUT HANDS.
#
# Boots Main.tscn for real — the whole director, the real main loop, the real
# renderer — and drives it with synthetic input, capturing the frame at each
# beat. Not a test: a way to LOOK at the thing and to press its controls, which
# is the one question the other three harnesses cannot answer.
#
#   xvfb-run -s "-screen 0 1280x1024x24" godot --path godot -s tests/play.gd
#
# Frames land in user://shots. It drives the pointer through Input.parse_input_event
# rather than through any hook of its own, so every gesture goes down the same
# path a thumb does: InputRouter polls the button and the position, and neither
# knows the difference.

const OUT := "user://shots/"

var _root: Node = null
var _n := 0
var _step := 0
var _wait := 0
var _shots := 0

# a drag in progress: [from, to, frames left, total]
var _drag = null


func _init() -> void:
	Directory.new().make_dir_recursive(OUT)
	# FROM A KNOWN SAVE, or the session is not repeatable: the title's fourth door
	# says MANUAL until the first cube is cleared and FORGE afterwards, and a run
	# that inherits yesterday's progress presses a different game.
	Directory.new().remove(Store.FILE)
	_root = load("res://game/Main.tscn").instance()
	get_root().add_child(_root)


func _idle(_dt: float) -> bool:
	_n += 1
	if _drag != null:
		_advance_drag()
		return false
	if _wait > 0:
		_wait -= 1
		return false
	_run()
	return false


# ---- the hands -------------------------------------------------------------

func _move(p: Vector2) -> void:
	var m := InputEventMouseMotion.new()
	m.position = p
	m.global_position = p
	Input.warp_mouse_position(p)
	Input.parse_input_event(m)


func _press(p: Vector2, down: bool) -> void:
	var e := InputEventMouseButton.new()
	e.button_index = BUTTON_LEFT
	e.pressed = down
	e.position = p
	e.global_position = p
	Input.parse_input_event(e)


# A TAP HAS TO LAST MORE THAN A FRAME. InputRouter polls the button once per
# frame, exactly as the C# does — a press and a release inside one frame is a
# button that was never down, and the router is right to say so. Three frames is
# what a fast thumb does.
func _tap(p: Vector2) -> void:
	_move(p)
	_press(p, true)
	_drag = [p, p, 3, 3]


# A DRAG IS NOT ONE EVENT. InputRouter polls once a frame and needs the button
# held across several of them, travelling, before it will call anything a fold —
# so this is spread over `frames` idles, which is what a thumb does anyway.
func _swipe(from: Vector2, to: Vector2, frames: int) -> void:
	_move(from)
	_press(from, true)
	_drag = [from, to, frames, frames]


func _advance_drag() -> void:
	var from: Vector2 = _drag[0]
	var to: Vector2 = _drag[1]
	var left: int = _drag[2]
	var total: int = _drag[3]
	left -= 1
	_drag[2] = left
	var k: float = 1.0 - float(left) / total
	_move(from.linear_interpolate(to, k))
	if left <= 0:
		_press(to, false)
		_drag = null


# Press and hold, without moving: the matrix.
func _hold_down(p: Vector2, frames: int) -> void:
	_move(p)
	_press(p, true)
	_drag = [p, p, frames, frames]


# ---- finding things --------------------------------------------------------

func _seek(from: Node, want: String) -> Node:
	if from == null:
		return null
	if from.name == want:
		return from
	for c in from.get_children():
		var hit = _seek(c, want)
		if hit != null:
			return hit
	return null


# THE SCREEN THAT IS UP, not every screen that exists. Every layer is named after
# its own id and so is the button that opens it — there is a rect called "manual"
# on the manual screen AND a link called "manual" on the pause card — so a search
# from the canvas root finds whichever was built first and it is usually hidden.
func _screen() -> Node:
	var sc = Screens.i()
	if sc._stack.empty():
		return null
	return sc._layers[sc._stack[sc._stack.size() - 1]]


func _on_canvas(canvas: String, nm: String) -> Control:
	var scope: Node = _screen() if canvas == "Screens" else null
	if scope != null:
		var hit = _seek(scope, nm)
		return hit if hit is Control else null
	for c in get_root().get_children():
		if c is CanvasLayer and c.name == canvas:
			var h2 = _seek(c, nm)
			if h2 is Control:
				return h2
	return null


func _centre(c: Control) -> Vector2:
	var r := c.get_global_rect()
	return r.position + r.size * 0.5


func _click(canvas: String, nm: String) -> bool:
	var b := _on_canvas(canvas, nm)
	if b == null or not b.visible:
		print("  ! no visible control named " + nm + " on " + canvas)
		return false
	_tap(_centre(b))
	return true


func _shot(nm: String) -> void:
	var img: Image = get_root().get_texture().get_data()
	img.flip_y()
	img.save_png(OUT + nm + ".png")
	_shots += 1
	print("shot: " + nm)


func _dir():
	return _root.director


# Where a surface cell is on screen, projected exactly as it is drawn.
func _cell_at(u: int, v: int) -> Vector2:
	var d = _dir()
	var s = d.s
	var sc = s.surf[u * s.n + v]
	var world: Vector3 = d.view.cube.global_transform.xform(
			CubeGeometry.cell_to_object(s.n, sc.w))
	return d.rig.cam.unproject_position(world)


# The first reachable cell that is not the one the player is standing on.
func _a_step_away():
	var d = _dir()
	var s = d.s
	var here := Projection.view_of(s.n, s.m, s.pos)
	for u in range(s.n):
		for v in range(s.n):
			if not s.reach[u * s.n + v]:
				continue
			if u == int(here.x) and v == int(here.y):
				continue
			return Vector2(u, v)
	return null


# ---- the session -----------------------------------------------------------

# Which fold to try next. FOOTING is par one, so exactly one of the four is the
# answer and the rest are refusals — which is worth seeing too.
var _fold := 0


func _swipe_fold(t: int) -> void:
	# InputRouter's turn indices: 0 left, 1 right, 2 up, 3 down. Screen y grows
	# downward here and the router flips it, so a "up" fold drags UP the screen.
	var mid := Vector2(202, 350)
	var reach := 150.0
	var to := mid
	match t:
		0: to = mid + Vector2(-reach, 0)
		1: to = mid + Vector2(reach, 0)
		2: to = mid + Vector2(0, -reach)
		3: to = mid + Vector2(0, reach)
	_swipe(mid - (to - mid) * 0.15, to, 16)


func _run() -> void:
	_step += 1
	var mid := Vector2(202, 350)
	match _step:
		1: _wait = 70
		2:
			_shot("01-title")
			_wait = 2
		3:
			_click("Screens", "CONTINUE")
			_wait = 100
		4:
			_shot("02-board")
			_say("board")
			_wait = 2
		5:
			var c = _a_step_away()
			if c == null:
				print("  ! nothing reachable")
			else:
				_tap(_cell_at(int(c.x), int(c.y)))
			_wait = 60
		6:
			_shot("03-walked")
			_say("after a tap")
			_wait = 2
		7:
			_hold_down(mid, 40)
		8:
			_shot("04-matrix")
			_wait = 25
		9:
			print("  trying fold ", _fold)
			_swipe_fold(_fold)
		10:
			_wait = 70
		11:
			_say("after fold " + str(_fold))
			if _dir().s.turns == 0 and _fold < 3:
				_fold += 1
				_step = 8      # go round again on the next direction
				_wait = 5
			else:
				_shot("05-folded")
				_wait = 40
		12:
			_shot("06-after")
			_say("settled")
			_wait = 2
		13:
			_click("HUD", "MENU")
			_wait = 45
		14:
			_shot("07-pause")
			_wait = 2
		15:
			_click("Screens", "manual")
			_wait = 50
		16:
			_shot("08-manual")
			_wait = 2
		17:
			_click("Screens", "got")
			_wait = 40
		18:
			_click("Screens", "calibrate")
			_wait = 45
		19:
			_shot("09-calibrate")
			_wait = 2
		20:
			_click("Screens", "open")
			_wait = 45
		21:
			_shot("10-access")
			_wait = 2
		22:
			_click("Screens", "MENU")
			_wait = 70
		23:
			_click("Screens", "VAULTS")
			_wait = 60
		24:
			_shot("11-vaults")
			_wait = 2
		25:
			_click("Screens", "back")
			_wait = 45
		26:
			# THE FOURTH DOOR IS MANUAL UNTIL THE FIRST CLEAR, and the node keeps
			# the name it was built with whatever the label later says. See Screens.
			_click("Screens", "FORGE")
			_wait = 60
		27:
			_shot("12-fourth-door")
			_wait = 2
		28:
			print("captured %d frames over %d ticks" % [_shots, _n])
			quit()


func _say(where: String) -> void:
	var s = _dir().s
	print("  [%s] turns=%d par=%d won=%s pos=%s world=%d walking=%s anim=%s" % [
			where, s.turns, s.lv.par, str(s.won), str(s.pos), s.world,
			str(s.walking != null), str(s.anim != null)])
