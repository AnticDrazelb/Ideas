extends SceneTree
# SOLVE A CUBE, USING THE GAME'S OWN ANSWER.
#
# tests/play.gd presses controls; this one PLAYS. It asks the session for its own
# advice — the same solver the HINT button uses — and performs it as a gesture,
# until the cube is solved. That exercises the one path nothing else reaches: the
# collapse, the exit, the win card and the cube after it.

const OUT := "user://shots/"

var _root: Node = null
var _n := 0
var _step := 0
var _wait := 0
var _drag = null
var _moves := 0
var _shots := 0


func _init() -> void:
	# THE POINTER IS DRIVEN IN SCREEN PIXELS, so the ruler must not move under it.
	# Under a software rasteriser the frame budget correctly drops the render size
	# a few seconds in — and the root viewport's size override is what every tap
	# this file sends is measured against, so a gesture aimed at a cell lands on
	# the one beside it and the run walks in place. See PerfWatch.pin.
	PerfWatch.pin()
	Directory.new().make_dir_recursive(OUT)
	# from cube one every time, so "SOLVED in n folds (par p)" means something
	Directory.new().remove(Store.FILE)
	_root = load("res://game/Main.tscn").instance()
	get_root().add_child(_root)


func _idle(_dt: float) -> bool:
	_n += 1
	if _drag != null:
		var from: Vector2 = _drag[0]
		var to: Vector2 = _drag[1]
		_drag[2] -= 1
		var k: float = 1.0 - float(_drag[2]) / _drag[3]
		_move(from.linear_interpolate(to, k))
		if _drag[2] <= 0:
			_press(to, false)
			_drag = null
		return false
	if _wait > 0:
		_wait -= 1
		return false
	_run()
	return false


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


func _tap(p: Vector2) -> void:
	_move(p)
	_press(p, true)
	_drag = [p, p, 3, 3]


func _swipe(from: Vector2, to: Vector2, frames: int) -> void:
	_move(from)
	_press(from, true)
	_drag = [from, to, frames, frames]


func _swipe_fold(t: int) -> void:
	var mid := Vector2(202, 350)
	var reach := 150.0
	var to := mid
	match t:
		0: to = mid + Vector2(-reach, 0)
		1: to = mid + Vector2(reach, 0)
		2: to = mid + Vector2(0, -reach)
		3: to = mid + Vector2(0, reach)
	_swipe(mid - (to - mid) * 0.15, to, 16)


func _shot(nm: String) -> void:
	var img: Image = get_root().get_texture().get_data()
	img.flip_y()
	img.save_png(OUT + nm + ".png")
	_shots += 1
	print("shot: " + nm)


func _dir():
	return _root.director


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


func _click_screen(nm: String) -> bool:
	var sc = Screens.i()
	if sc._stack.empty():
		return false
	var b = _seek(sc._layers[sc._stack[sc._stack.size() - 1]], nm)
	if not (b is Control) or not b.visible:
		print("  ! no ", nm)
		return false
	var r = b.get_global_rect()
	_tap(r.position + r.size * 0.5)
	return true


func _run() -> void:
	_step += 1
	match _step:
		1: _wait = 70
		2:
			_click_screen("CONTINUE")
			_wait = 110
		3:
			_play_one()
		100:
			_post_win()
			_step = 99
		_:
			pass


# One move a visit, from the game's own advice.
var _phase := 0


func _play_one() -> void:
	var d = _dir()
	var s = d.s

	if s.won:
		print("SOLVED in %d folds (par %d)" % [s.turns, s.lv.par])
		_step = 99
		_wait = 20
		return

	if _moves > 24:
		print("  ! gave up after %d moves, turns=%d" % [_moves, s.turns])
		_shot("x-stuck")
		quit()
		return

	if s.walking != null or s.anim != null:
		_step -= 1
		_wait = 5
		return

	var a = s.advice()
	if a == null:
		print("  ! no advice; turns=", s.turns)
		_shot("x-noadvice")
		quit()
		return

	_moves += 1
	if a.is_turn:
		print("  move %d: FOLD %s" % [_moves, Turns.ID[a.dir]])
		_swipe_fold(a.dir)
		_step -= 1
		_wait = 70
	else:
		# A STEP IS ONE CELL IN ONE DIRECTION, which is what the advice means —
		# not "walk to the exit". The turn indices are the same four the folds use,
		# and Turns.DX/DY put them in view space, which is the space the board is
		# drawn in and the space a tap lands in.
		var here: Vector3 = Projection.view_of(s.n, s.m, s.pos)
		var u: int = int(here.x) + Turns.DX[a.dir]
		var v: int = int(here.y) + Turns.DY[a.dir]
		if u < 0 or v < 0 or u >= s.n or v >= s.n:
			print("  ! advice steps off the board")
			quit()
			return
		var sc = s.surf[u * s.n + v]
		print("  move %d: STEP %s to (%d,%d)" % [_moves, Turns.ID[a.dir], u, v])
		_tap(d.rig.cam.unproject_position(
				d.view.cube.global_transform.xform(CubeGeometry.cell_to_object(s.n, sc.w))))
		_step -= 1
		_wait = 70


var _post := 0


func _post_win() -> void:
	_post += 1
	match _post:
		1:
			_shot("w1-collapse")
			_wait = 60
		2:
			_shot("w2-exit")
			_wait = 90
		3:
			_shot("w3-card")
			_wait = 5
		4:
			_click_screen("next")
			_wait = 140
		5:
			_shot("w4-next-cube")
			var s = _dir().s
			print("next cube: %d %s par %d" % [s.level_no, s.lv.name, s.lv.par])
			_wait = 5
		6:
			print("captured %d frames over %d ticks" % [_shots, _n])
			quit()
