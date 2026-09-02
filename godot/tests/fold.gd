extends SceneTree
# THE SOLID GOES WHERE THE THUMB GOES.
#
# Every other harness in this project checks a statement the game makes about
# itself: the rules against the original's fixtures, the interface against its
# own arithmetic, the board's colours against the rule the player is given. None
# of them can see the one thing a fold IS — a drag to the left turning the cube
# to the left — because the rules and the renderer agree with each other whether
# or not either agrees with the hand.
#
# It shipped wrong. The camera stands on the minus side of the cube and is turned
# about Y to face it, which puts its own right at world -X; the mesh was built
# with the rules' x on world +X to meet it, so the solid rolled AWAY from the
# thumb on every horizontal fold. Measured here at -137 pixels for a leftward
# drag before the fix and +137 after, on a 405-wide screen, and no test in the
# project so much as flinched.
#
# So: take a point on the face the player is looking at, commit each of the four
# turns, and watch where that point goes on the glass.
#
#   xvfb-run -s "-screen 0 1400x1100x24" godot --path godot -s tests/fold.gd

const CUBES := [1, 3, 7]

# 0 left, 1 right, 2 up, 3 down — Turns.ID, and the order is load-bearing there.
#
# Screen space, as unproject answers it: x grows right and y grows DOWN. That
# second one is established rather than assumed — see _prove_up.
const WANT := [Vector2(-1, 0), Vector2(1, 0), Vector2(0, -1), Vector2(0, 1)]

# A quarter turn of a cube this size moves the near face most of a board width.
# Anything under this is the cube not having turned at all.
const LEAST := 40.0

var _root: Node = null
var _step := 0
var _wait := 0
var _seen := Vector2.ZERO
var _still := 0
var _bad := 0
var _at := 0
var _turn := 0
var _anchor := Vector3.ZERO
var _from := Vector2.ZERO
var _tried := 0
var _took := 0


func _init() -> void:
	PerfWatch.pin()
	Directory.new().remove(Store.FILE)
	OS.set_window_size(Vector2(405, 720))


func _settled() -> bool:
	var now := Layout.screen_size()
	if now == _seen:
		_still += 1
	else:
		_seen = now
		_still = 0
	return _still >= 8


func _idle(_dt: float) -> bool:
	if _wait > 0:
		_wait -= 1
		return false
	_run()
	return false


func _on_glass(at: Vector3) -> Vector2:
	var cv = _root.director.view
	var cam = _root.director.rig.cam
	return cam.unproject_position(cv.cube.global_transform.xform(at))


# WHICH WAY IS UP IN THE ANSWER unproject GIVES.
#
# The board renders into a viewport read with render_target_v_flip, so its own y
# is not obviously the glass's y, and a vertical fold graded against the wrong
# sign would pass while the cube rolled the wrong way. Two points a cube apart on
# the rules' up axis settle it, once, out loud.
func _prove_up() -> void:
	var lo := _on_glass(CubeGeometry.to_object(0, -2, -2))
	var hi := _on_glass(CubeGeometry.to_object(0, 2, -2))
	print("  the rules' UP runs %.0f -> %.0f in y, so y grows %s" % [
			lo.y, hi.y, "DOWNWARD" if hi.y < lo.y else "upward"])
	if hi.y >= lo.y:
		print("  ! [fold] the board is upside down on the glass")
		_bad += 1


func _run() -> void:
	if _step == 0:
		if not _settled():
			return
		_root = load("res://game/Main.tscn").instance()
		get_root().add_child(_root)
		_step += 1
		_wait = 80
		return

	# A CUBE PER TURN, FRESH. A fold changes which turns are legal, so trying
	# four in a row on one board measures whatever the first one left behind.
	if _step == 1:
		Screens.show(null)
		_root.director.play(CUBES[_at])
		_step += 1
		_wait = 200
		return

	if _step == 2:
		if Screens.chapter_up():
			Screens.i()._close_chapter()
			_wait = 60
			return
		Screens.show(null)
		if _turn == 0:
			print("--- cube %d" % CUBES[_at])
			if _at == 0:
				_prove_up()
		# THE CENTRE OF THE FACE THE PLAYER IS LOOKING AT. The rules call the
		# direction toward the camera f = +z; the conversion negates it.
		var s = _root.director.s
		_anchor = CubeGeometry.to_object(0, 0, float(s.n) * 0.5)
		_from = _on_glass(_anchor)
		_step += 1
		if not s.try_turn(_turn):
			# A refused turn is the rules doing their job — there is no footing
			# on the face it would bring round — and there is nothing to grade.
			print("    %-5s refused" % Turns.ID[_turn])
			_step = 4
			_wait = 2
			return
		_tried += 1
		_wait = 90
		return

	if _step == 3:
		var went: Vector2 = _on_glass(_anchor) - _from
		var want: Vector2 = WANT[_turn]
		var along: float = went.dot(want)
		var across: float = abs(went.dot(Vector2(want.y, -want.x)))
		print("    %-5s the near face went %+6.1f, %+6.1f — %.0f along the drag, %.0f across" % [
				Turns.ID[_turn], went.x, went.y, along, across])
		if along < LEAST:
			print("  ! [fold] a %s fold moves the solid %s" % [
					Turns.ID[_turn],
					"the wrong way" if along < -LEAST else "hardly at all"])
			_bad += 1
		else:
			_took += 1
		if across > LEAST:
			print("  ! [fold] a %s fold moves the solid sideways as well" % Turns.ID[_turn])
			_bad += 1
		_step += 1
		_wait = 2
		return

	if _step == 4:
		_turn += 1
		if _turn < 4:
			_step = 1
			_wait = 2
			return
		_turn = 0
		_at += 1
		if _at < CUBES.size():
			_step = 1
			_wait = 2
			return
		_step += 1
		return

	print("%d folds committed of %d tried, %d faults over %d cubes" % [
			_took, _tried, _bad, CUBES.size()])
	quit(1 if _bad > 0 else 0)
