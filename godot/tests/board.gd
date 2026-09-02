extends SceneTree
# THE BOARD, MEASURED ON THE GLASS.
#
# tests/run.gd holds the rules and tests/ui.gd holds the interface, and between
# them is the one question neither asks: does the thing the rules call FOOTING
# actually come out brighter than the thing they call solid, in the pixels a
# player looks at?
#
# It is the whole board. "I can stand there" is read off brightness before it is
# read off anything else, and every other check in this project can pass while
# that fails — the palette can hold its three-to-one, the mesh can put the cell
# in the right submesh, the material can carry the right ramp, and one arithmetic
# slip in a vertex shader still hands you a slab of identical plates.
#
# So this boots the game, plays cube one, and samples the framebuffer at the
# screen position of every surface cell, and compares the two populations.
#
#   xvfb-run -s "-screen 0 1400x1100x24" godot --path godot -s tests/board.gd

var _root: Node = null
var _step := 0
var _wait := 0
var _seen := Vector2.ZERO
var _still := 0
var _bad := 0
var _at := 0

const CUBES := [1, 3, 7, 11]


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


func _run() -> void:
	if _step == 0:
		if not _settled():
			return
		_root = load("res://game/Main.tscn").instance()
		get_root().add_child(_root)
		_step += 1
		_wait = 80
		return
	# ONE CUBE IS NOT THE BOARD. The surface a column collapses to depends on
	# which cells are solid and which way the cube is turned, so a single cube
	# can agree by luck. These four are a plain one, the buried one, and two with
	# more depth to get wrong.
	if _step == 1:
		# THE GLOW OFF, because this measures the BOARD. Bloom lifts a dark cell
		# that happens to sit beside a lit one, which is what it is for and what
		# a per-cell sample must not mistake for the cell's own colour. Access
		# already has the switch; a player who turns LIGHT off is looking at the
		# same board this test is.
		if OS.get_environment("BOARD_GLOW") == "":
			Store.data().light = 2
			Store.save()
		Screens.show(null)
		_root.director.play(CUBES[_at])
		_step += 1
		# A CUBE CHANGE IS NOT INSTANT. The view rebuilds its mesh and materials,
		# the arrival wave runs, and a cube that ends a chapter puts a word on the
		# screen first — measure too early and the materials still carry the last
		# cube's half_span and nothing has been walked yet.
		_wait = 200
		return

	if _step == 2:
		if Screens.chapter_up():
			Screens.i()._close_chapter()
			_wait = 60
			return
		Screens.show(null)
		print("--- cube %d" % CUBES[_at])
		_measure()
		_at += 1
		if _at < CUBES.size():
			_step = 1
			_wait = 2
			return
		_step += 1
		return

	print("%d faults over %d cubes" % [_bad, CUBES.size()])
	quit(1 if _bad > 0 else 0)


# THE PLATE, NOT WHAT IS SITTING ON IT. The singularity is a black disc and the
# exit is a lit ring, both centred — so a sample at the middle of those two cells
# reads the marker and not the surface under it, which is exactly the pair this
# test exists to check. Four samples out at a third of a cell, median taken:
# every one of them is on plate, none of them is on a marker or in the gutter.
func _sample(img: Image, p: Vector2, cell: float) -> float:
	var lums := []
	# WELL INSIDE THE PLATE. A cell is a rounded tile with a dark gutter around
	# it and a lifted rim just inside that — the shader multiplies the gutter by
	# 0.09 and the rim by 1.55 — so a sample a third of a cell out lands on one
	# or the other and reads a sixth or half again of the colour it was after.
	# That is not a subtle error: it made every trace look like lattice and three
	# lattice cells look like traces, and it is what I mistook for a broken board.
	var r: float = cell * 0.12
	for d in [Vector2.ZERO, Vector2(-r, -r), Vector2(r, -r), Vector2(-r, r), Vector2(r, r)]:
		var q: Vector2 = p + d
		if q.x < 1 or q.y < 1 or q.x >= img.get_width() - 1 or q.y >= img.get_height() - 1:
			continue
		var c: Color = img.get_pixel(int(q.x), int(q.y))
		lums.append(0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b)
	if lums.empty():
		return -1.0
	lums.sort()
	return float(lums[lums.size() / 2])


func _measure() -> void:
	var s = _root.director.s
	var img: Image = get_root().get_texture().get_data()
	img.flip_y()
	img.lock()

	var cv = _root.director.view
	for nm in ["_mat_trace", "_mat_lattice", "_mat_plate"]:
		var mt = cv.get(nm)
		if mt == null:
			print("    ", nm, " is null")
			continue
		print("    %-12s col_far %s  col_near %s  half_span %s  unlit %s" % [
				nm, str(mt.get_shader_param("col_far")), str(mt.get_shader_param("col_near")),
				str(mt.get_shader_param("half_span")), str(mt.get_shader_param("unlit"))])

	var eff: PoolByteArray = s.lv.eff(s.world)
	var square := Layout.board_square()
	var mid: Vector2 = square.position + square.size * 0.5
	# The window is measured with the origin at the BOTTOM left, the way every
	# rectangle in Layout is; the image has row zero at the top.
	mid.y = max(1.0, Layout.screen_size().y) - mid.y
	var cell: float = max(4.0, square.size.x / (float(max(1, s.n)) * Layout.FIT_MARGIN))
	var half: float = (s.n - 1) * 0.5
	# THE VIEW'S u AXIS RUNS RIGHT TO LEFT ON THE GLASS. That is the camera's
	# half turn about Y and it is not a fault — see CameraRig.init.
	var flip := -1.0
	var vflip := 1.0

	var walk := []
	var far := []
	var solid := []
	var report := []
	var marks := []
	for u in range(s.n):
		for v in range(s.n):
			var sf: Surf = s.surf[u * s.n + v]
			if not sf.has:
				continue
			# THE GRID IS TAKEN FROM THE LAYOUT, NOT FROM THE CAMERA.
			#
			# unproject_position answers in the board viewport's own space, and
			# that viewport is read with render_target_v_flip — so turning its
			# answer into a pixel on the glass needs a mirror whose pivot is not
			# the screen's centre, and getting it wrong samples cell (u,v) at
			# the position of (u, n-1-v). Rows reversed reads exactly like a
			# board that has stopped separating.
			#
			# At rest the board is a flat square grid, fitted to the shorter side
			# of the window with FIT_MARGIN to spare and centred on it. That is
			# two lines of arithmetic with nothing in it that can be off by a
			# mirror, and it is the same arithmetic the camera was given.
			var p := Vector2(
					mid.x + (u - half) * cell * flip,
					mid.y - (v - half) * cell * vflip)
			marks.append(p)
			var lum: float = _sample(img, p, cell)
			if lum < 0.0:
				continue
			# THREE STATES, NOT TWO. A cell is solid, or it is footing you can
			# get to, or it is footing you cannot get to YET — and the third is
			# the one this test was written blind to. The board drains and dims
			# what you cannot reach, on purpose; the question is whether that dim
			# takes a trace down into the lattice's range, because if it does
			# then "light means you can stand there" has stopped being true.
			var can: bool = Level.is_walk_type(sf.t)
			# BY SURFACE SQUARE, NOT BY VOXEL. Session.reach is n*n — one entry
			# per column of the cube, because reachability is a walk across the
			# SURFACE — and indexing it with a voxel index reads a different
			# cell's answer, or none. CubeView has always used u*n+v; this did
			# not, and reported the player's own cell as unreachable.
			var k: int = u * s.n + v
			var near: bool = can and k < s.reach.size() and s.reach[k]
			var kind: String = "solid  "
			if can:
				kind = "FOOTING" if near else "footing-unreached"
			# AND WHICH OF THE THREE MATERIALS THE MESH PUT IT IN, which is the
			# only thing that decides its ramp. CubeGeometry keys that off the
			# EFFECTIVE cell: TRACE goes to the trace submesh, a glyph to the
			# plate, and everything else to the lattice.
			var t: int = eff[Level.vidx_n(s.n, int(sf.w.x), int(sf.w.y), int(sf.w.z))]
			var sub: String = "plate"
			if not Level.is_glyph(t):
				sub = "trace" if t == Level.TRACE else "LATTICE"
			var tag: String = "%d,%d %s drawn-as %s lum %.3f" % [u, v, kind, sub, lum]
			if sf.w == s.pos:
				tag += "  <- you are here"
			if sf.w == s.lv.goal:
				tag += "  <- the exit"
			report.append(tag)
			# THE TWO CELLS WITH SOMETHING ON THEM ARE NOT MEASURABLE HERE. The
			# singularity is a black disc and the exit a lit ring, both centred
			# and both larger than any patch of plate left around them, so what
			# a sample there reads is the marker. They are reported and left out
			# of the comparison; what is under them is the same material as
			# everything else of their kind, which is what the ordering is about.
			if sf.w == s.pos or sf.w == s.lv.goal:
				pass
			elif near:
				walk.append(lum)
			elif can:
				far.append(lum)
			else:
				solid.append(lum)
	# AND A PICTURE OF WHERE IT LOOKED, because a sampler that is wrong reads
	# like a board that is broken and the two are not distinguishable from the
	# numbers alone.
	for pt in marks:
		for dx in range(-4, 5):
			for dy in range(-4, 5):
				if abs(dx) > 1 and abs(dy) > 1:
					continue
				var qx: int = int(pt.x) + dx
				var qy: int = int(pt.y) + dy
				if qx >= 0 and qy >= 0 and qx < img.get_width() and qy < img.get_height():
					img.set_pixel(qx, qy, Color(1, 0, 1))
	img.unlock()
	var d := Directory.new()
	d.make_dir_recursive("user://shots")
	img.save_png("user://shots/probe.png")

	for line in report:
		print("    ", line)

	# THE CLAIM IS ABOUT FOOTING AGAINST SOLID, and reachable-or-not is a second
	# reading laid over the first. On a cube where the only reachable footing is
	# the cell you are standing on — which this excludes, because the singularity
	# is drawn on top of it — there is still a board to check.
	var foot := []
	foot += walk
	foot += far
	foot.sort()
	if foot.empty() or solid.empty():
		print("  ! nothing to compare — the board did not draw")
		_bad += 1
		return

	walk.sort()
	far.sort()
	solid.sort()
	var dimmest_footing: float = foot[0]
	var brightest_solid: float = solid[solid.size() - 1]
	print("  reachable footing %s   unreached footing %s   solid %.3f..%.3f" % [
			("%.3f..%.3f" % [walk[0], walk[walk.size() - 1]]) if not walk.empty() else "none",
			("%.3f..%.3f" % [far[0], far[far.size() - 1]]) if not far.empty() else "none",
			solid[0], solid[solid.size() - 1]])

	# THE ONE PROPERTY. Not a ratio and not a threshold — an ORDER. Every cell a
	# player may stand on is brighter than every cell they may not, or the board
	# is not telling them the rule.
	if dimmest_footing <= brightest_solid:
		print("  ! [board] a cell you cannot stand on is drawn as bright as one you can")
		_bad += 1
