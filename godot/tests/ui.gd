extends SceneTree
# THE INTERFACE, MEASURED WHERE IT IS DRAWN.
#
# The Unity harness checks Layout's arithmetic across device sizes, which is the
# most it can do without an engine: it asks whether the numbers add up. This asks
# the built tree. Every screen in the game is constructed at runtime out of three
# thousand lines of anchors and offsets, and the question that actually decides
# whether a screen is any good — does this control land on that word — is a
# question about rects that only exist once something has laid them out.
#
# So it boots the game, visits every screen, and measures:
#
#   COLLISION  a word drawn over a control, or two controls over each other.
#              Overflow on its own is fine and deliberate — a number in a chip is
#              routinely wider than the chip — so what is checked is the drawn
#              extent of the type against the things it could be hiding.
#   TARGET     every pressable thing against the tap target it owes a finger.
#   BOUNDS     everything visible inside the plate, because rule one of the
#              chassis is that no word ever sits on the metal.
#   EMPTY      a rect with no width or no height, which is a control that was
#              built and cannot be seen or pressed.
#
#   xvfb-run -s "-screen 0 1280x1024x24" godot --path godot -s tests/ui.gd

var _root: Node = null
var _n := 0
var _step := 0
var _wait := 0
var _bad := 0
var _checked := 0
var _pending := ""

# Whether the audit has already turned the device over and re-measured. See the
# tail of _run.
var _turned := false

# Every screen the game builds, and the HUD over the board.
const SCREENS := ["title", "vaults", "manual", "plate", "chapter", "pause",
		"calibrate", "access", "win", "forge", "forgeEdit"]

# THE ONE SCREEN THAT CANNOT PAY THE TAP TARGET, and it is arithmetic rather
# than neglect. The editor is a column of ten bands over a deck that takes
# whatever is left; giving every band the eighty-eight a finger is owed costs a
# hundred and seventy-two units, and the deck's own floor is two hundred and
# forty — so the column would run off the bottom of the plate. A dense tool
# trades band height for the size of the thing being edited, deliberately, and
# the deck's cells are cells rather than buttons in any case: an n-by-n grid in
# a square cannot be eighty-eight a side past a six-cube.
const TOOL_SCREENS := ["forgeEdit"]


# THE SHAPE IS SET FROM IN HERE, NOT FROM THE COMMAND LINE.
#
# `--resolution` is a request made before the window exists, and an X server with
# no window manager is free to answer it with something else — 540x960 came back
# as 477x847 on one run and 540x960 on the next, in the same session. Worse, it
# sometimes settled on the second answer PART WAY THROUGH, so screens built at
# one size were measured against the glass of another and the audit reported
# eighty units of overshoot that no player would ever see.
#
# So the size is asked for after boot and then WAITED FOR. Nothing is measured
# until the root viewport has reported the same rectangle several frames running,
# which is the only statement about the window this harness can actually trust.
#
#   UI_SIZE=1280x720 godot --path godot -s tests/ui.gd
const DEFAULT_SIZE := "405x720"

var _want := Vector2.ZERO
var _seen := Vector2.ZERO
var _still := 0


func _init() -> void:
	Directory.new().remove(Store.FILE)
	# The board is drawn by whatever rasteriser this machine has, and on a build
	# machine that is a software one. Dynamic resolution would correctly react to
	# it and incorrectly move everything this harness measures.
	PerfWatch.pin()
	_want = _wanted_size()
	if _want.x > 0.0:
		OS.set_window_size(_want)


static func _wanted_size() -> Vector2:
	var spec := OS.get_environment("UI_SIZE")
	if spec == "":
		spec = DEFAULT_SIZE
	var bits := spec.to_lower().split("x")
	if bits.size() != 2 or not bits[0].is_valid_integer() or not bits[1].is_valid_integer():
		return Vector2.ZERO
	return Vector2(int(bits[0]), int(bits[1]))


# True once the window has stopped arguing.
func _settled() -> bool:
	var now := Layout.screen_size()
	if now == _seen:
		_still += 1
	else:
		_seen = now
		_still = 0
	return _still >= 8


func _idle(_dt: float) -> bool:
	_n += 1
	if _wait > 0:
		_wait -= 1
		return false
	_run()
	return false


func _run() -> void:
	# THE GAME IS NOT BUILT UNTIL THE WINDOW HAS STOPPED MOVING.
	#
	# Asking for a size and instancing in the same breath builds every screen
	# against whatever the window happened to be at startup, and then measures it
	# against what the window became — which reports the difference between the
	# two as a layout fault on every screen at once. A player's device is one
	# size when the game opens; so is this one, now.
	if _step == 0:
		if not _settled():
			return
		if _want.x > 0.0 and _seen != _want:
			print("  ! [window] asked for %.0fx%.0f, the display gave %.0fx%.0f"
					% [_want.x, _want.y, _seen.x, _seen.y])
		_root = load("res://game/Main.tscn").instance()
		get_root().add_child(_root)
		_step += 1
		_wait = 70
		return
	if _step == 1:
		# a save far enough in that every screen has something to draw
		Store.data().reached = 40
		Store.data().runs = 1
		Store.set_best(1, 2)
		Store.set_par(1, 1)
		Store.save()
		_step += 1
		_wait = 5
		return

	if _step == 2:
		if not _envelope():
			print("0 controls measured, 1 faults")
			quit(1)
			return
		print("case  L%.1f R%.1f T%.1f B%.1f  k %.3f  %.2f in  dpi %d" % [
				Layout.chassis_left(), Layout.chassis_right(),
				Layout.chassis_top(), Layout.chassis_bottom(),
				Chassis.scale_now(), Chassis.diagonal_inches(), OS.get_screen_dpi()])
		print("plate %.0f x %.0f  screen %s  scale %.4f  panel floor %.0f" % [
				Layout.plate_width(), Layout.plate_height(),
				str(Layout.screen_size()), Layout.canvas_scale(), Screens.panel_floor()])

	if _step == -1:
		if not _settled():
			return
		print("turned to %s — %s" % [str(_seen),
				"landscape" if Layout.is_landscape() else "portrait"])
		_step = 2
		_wait = 30
		return

	var idx := _step - 2
	if idx < SCREENS.size():
		var id: String = SCREENS[idx]
		if _pending != "":
			_audit(_pending, _layer_of(_pending))
			_pending = ""
		_show(id)
		_pending = id
		_step += 1
		_wait = 12
		return
	if idx == SCREENS.size():
		if _pending != "":
			_audit(_pending, _layer_of(_pending))
			_pending = ""
		# and the readout, which is the one surface drawn over the board
		Screens.show(null)
		_step += 1
		_wait = 12
		return
	if idx == SCREENS.size() + 1:
		_audit("hud", _hud_root())
		_step += 1
		_wait = 2
		return

	if idx == SCREENS.size() + 2:
		Screens.show_title()
		# THE FRAMING EASES, so the first poses after the title comes up are the
		# camera on its way rather than the pose it settles at. Sampling those
		# reports the cube leaving a band it was never in.
		_step += 1
		_wait = 60
		return
	if idx < SCREENS.size() + 3 + TITLE_SAMPLES:
		_sample_attract()
		_step += 1
		_wait = 1
		return
	if idx == SCREENS.size() + 3 + TITLE_SAMPLES:
		_report_attract()
		_step += 1
		return

	# ---- AND THEN TURN IT OVER --------------------------------------------
	#
	# Every arrangement in this game is decided while a screen is being built, so
	# a rotation is a rebuild rather than a re-fit — and a rebuild is exactly the
	# kind of thing that works when it is the only thing that has ever happened
	# and falls over the second time. So the whole audit runs again on the
	# transposed window, on the interface the running game built for it, without
	# restarting the process.
	#
	# It is the same measurement, so it is the same code: the step counter goes
	# back to the top of the screen loop and everything downstream of it happens
	# a second time.
	# A TURN THAT LANDS OUTSIDE THE ENVELOPE IS NOT A FAILURE, IT IS THE ENVELOPE.
	#
	# A four-by-three tablet turned is a three-by-four tablet, which is the one
	# shape between the two the game is authored for — and _envelope already says
	# so, once, rather than fifty times. Turning into it and then reporting that
	# as a fault would be the audit arguing with itself.
	if not _turned and not _would_hold():
		print("not turned: %s would be outside the envelope" % str(
				Vector2(Layout.screen_size().y, Layout.screen_size().x)))
		_turned = true
	if not _turned:
		_turned = true
		_pending = ""
		_sampled = 0
		_worst = Rect2()
		var now := Layout.screen_size()
		OS.set_window_size(Vector2(now.y, now.x))
		_still = 0
		_step = -1
		_wait = 4
		return
	print("%d controls measured, %d faults" % [_checked, _bad])
	quit(1 if _bad > 0 else 0)


# ---- the attract cube, across its whole cycle ------------------------------
#
# The title is the one screen with a moving object on it, and the object is a
# CUBE — its silhouette is widest on its diagonal and it is turning, so a pose
# that fits is not an argument that the pose four seconds later fits. The band it
# may occupy is Layout.title_rect: under the masthead rule, over the primary.
# This walks the cycle and keeps the worst corner.
const TITLE_SAMPLES := 240

var _worst := Rect2()
var _sampled := 0


func _sample_attract() -> void:
	var d = _root.director
	if d.view == null or d.view.cube == null or d.rig == null:
		return
	var n: int = d.s.n
	if n < 1:
		return
	var cam: Camera = d.rig.cam
	var x: Transform = d.view.cube.global_transform
	var e: float = n * 0.5
	var lo := Vector2(1e9, 1e9)
	var hi := Vector2(-1e9, -1e9)
	for i in range(8):
		var c := Vector3(e if (i & 1) else -e, e if (i & 2) else -e, e if (i & 4) else -e)
		var pt: Vector2 = cam.unproject_position(x.xform(c))
		lo.x = min(lo.x, pt.x); lo.y = min(lo.y, pt.y)
		hi.x = max(hi.x, pt.x); hi.y = max(hi.y, pt.y)
	var box := Rect2(lo, hi - lo)
	_worst = box if _sampled == 0 else _worst.merge(box)
	_sampled += 1


func _report_attract() -> void:
	if _sampled == 0:
		_fault("title", "the attract cube never turned")
		return
	var band := Layout.title_rect()
	var over := _outside_by(_worst, band)
	if over == "":
		print("  attract stays inside its band across %d poses" % _sampled)
	else:
		_fault("title", "the attract cube leaves its band by " + over)


# How far outside, on each side that is outside. Returns "" when it is inside.
func _outside_by(box: Rect2, band: Rect2) -> String:
	var out := PoolStringArray()
	if box.position.x < band.position.x - 0.5:
		out.append("%.0f px left" % (band.position.x - box.position.x))
	if box.position.y < band.position.y - 0.5:
		out.append("%.0f px top" % (band.position.y - box.position.y))
	if box.end.x > band.end.x + 0.5:
		out.append("%.0f px right" % (box.end.x - band.end.x))
	if box.end.y > band.end.y + 0.5:
		out.append("%.0f px bottom" % (box.end.y - band.end.y))
	return out.join(", ")


# THE AUTHORED ENVELOPE, NAMED ONCE INSTEAD OF FIFTY-FOUR TIMES.
#
# Layout's plate is 625 by 1128 canvas units and the canvas scaler holds the
# canvas AREA constant, so a display that is wider relative to its height has
# fewer units of HEIGHT to give. Past a point there are not enough of them, every
# screen anchored to the plate's bottom hangs off the display, and this audit
# reports one fault per label. All of them are the same fault.
#
# THE BOUNDARY IS MEASURED RATHER THAN DERIVED. The obvious sum — the plate plus
# its two chassis insets — comes to exactly the reference canvas, and the layout
# turns out to tolerate being well short of it: 0.60 lays out with 323 controls
# and no faults at 41 units under. 0.75 does not. The line is drawn between what
# was seen to work and what was seen to fail, and it moves when that changes.
#
# This game is authored for a portrait phone and the original targets one. A
# four-by-three tablet is outside that, and saying so once is worth more than
# fifty symptoms of it.
const WIDEST_ASPECT := 0.68


# AND THE OTHER SIDE OF THE SAME LINE.
#
# The game answers two shapes now: a phone held, and a device turned. Between
# them — a four-by-three tablet in PORTRAIT, an aspect from 0.68 up to the 1.18
# where Chassis calls it landscape — is a shape neither arrangement is authored
# for: too wide for a column of full-width rows, not wide enough for two of them.
# Saying so once is still worth more than fifty symptoms of it.
# Whether the transposed window would be a shape the game answers.
func _would_hold() -> bool:
	var s2 := Layout.screen_size()
	var a: float = s2.y / max(1.0, s2.x)
	return a <= WIDEST_ASPECT or a > 1.18


func _envelope() -> bool:
	var screen := Layout.screen_size()
	var aspect: float = screen.x / max(1.0, screen.y)
	if aspect <= WIDEST_ASPECT or Layout.is_landscape():
		return true
	var canvas: Vector2 = screen / max(0.0001, Layout.canvas_scale())
	print("  ! [envelope] aspect %.2f is between the two shapes the game is authored for" % aspect)
	print("  ! [envelope] (%.0f units of canvas height; the reference portrait has 1280)" % canvas.y)
	return false


func _show(id: String) -> void:
	# THE CHAPTER WORD LEAVES ON ITS OWN, AND IT WAS STILL LEAVING THREE SCREENS
	# LATER.
	#
	# It is the one screen in the game with no way out drawn on it: it types
	# itself, waits, and then calls show(null). The audit puts it up like any
	# other screen and moves on — so a few seconds afterwards, in the middle of
	# auditing the forge or the editor, the word's own clock ran out and took the
	# screen down underneath the measurement. The audit then found nothing, and
	# reported no faults, because everything it checks iterates over what it
	# gathered. Two runs in three, and which screen it landed on depended on how
	# fast the machine was.
	if Screens.chapter_up():
		Screens.i()._close_chapter()

	var sc = Screens.i()
	match id:
		"win":
			Screens.show_win(_root.director.s, _root.director)
		"pause":
			Screens.show_pause(_root.director)
		"vaults":
			sc._open_vaults()
		"forge":
			ForgeScreens.open_shelf()
		"forgeEdit":
			ForgeScreens.open_editor(Forge.resume())
		"chapter":
			Screens.show_chapter_word("test.", null)
		_:
			Screens.show(id)

func _hud_root() -> Node:
	for c in get_root().get_children():
		if c is CanvasLayer and c.name == "HUD":
			return c
	return null


func _layer_of(id: String) -> Node:
	return Screens.i()._layers.get(id)


# ---- the measurements ------------------------------------------------------

func _live(c: Control) -> bool:
	var n: Node = c
	while n != null and n is CanvasItem:
		if not (n as CanvasItem).visible:
			return false
		n = n.get_parent()
	return true


func _gather(from: Node, into: Array) -> void:
	if from is Control and _live(from):
		into.append(from)
	for c in from.get_children():
		_gather(c, into)


# THE RECT AS IT IS ON THE GLASS.
#
# Control.get_global_rect() is a trap under a scaled canvas: its POSITION comes
# from the global transform and its SIZE does not, so every box it returns is the
# right place at the wrong size — 1.78x too big here, which is enough to invent
# a collision on every control in the game and to report the whole readout as
# hanging off the bezel. The transform carries both; this asks it for both.
# A LABEL'S TEXT, FIT FOR ONE LINE OF A REPORT. A body with a newline in it cut
# every fault after it out of the listing — the message ended mid-sentence and
# the next line of the report was the rest of somebody's paragraph.
static func _say(t: String, n: int) -> String:
	return t.replace("\n", " / ").substr(0, n)


# Whether anything above this control scrolls. A control below the fold of a
# scrolling page is where it should be; a control below the fold of a plate is
# not.
static func _in_scroll(c: Node) -> bool:
	var n := c.get_parent()
	while n != null:
		if n is ScrollContainer:
			return true
		n = n.get_parent()
	return false


func _rect(c: Control) -> Rect2:
	var t := c.get_global_transform_with_canvas()
	return Rect2(t.origin, c.rect_size * t.get_scale())


func _is_press(c: Control) -> bool:
	return c is UiButton or c is UiSwitch or c is UiBar or c is UiField


# What a label actually covers, which is its box widened to the type in it and
# pinned by the alignment. A chip's number overflowing its own padding is by
# design; the same overflow landing on a button is not.
func _ink(l: Label) -> Rect2:
	var r := _rect(l)
	if l.text == "" or l.autowrap:
		return r
	var f = l.get_font("font")
	if f == null:
		return r
	var w: float = f.get_string_size(l.text).x * l.get_global_transform_with_canvas().get_scale().x
	match l.align:
		Label.ALIGN_LEFT:
			return Rect2(r.position, Vector2(w, r.size.y))
		Label.ALIGN_RIGHT:
			return Rect2(Vector2(r.end.x - w, r.position.y), Vector2(w, r.size.y))
		_:
			var cx := r.position.x + r.size.x * 0.5
			return Rect2(Vector2(cx - w * 0.5, r.position.y), Vector2(w, r.size.y))


# WHAT IS ACTUALLY ON THE GLASS, which is the ink cut down by everything that
# clips it. Every screen's plate clips — that is rule one of the chassis, made
# structural — and a scroll clips its page to its window. Treating either as
# "skip this label" throws away every label in the game; treating neither reports
# every line below a fold as landing on the button under the window. The honest
# answer is the intersection.
func _clip_to_ancestors(c: Control, ink: Rect2) -> Rect2:
	var n: Node = c.get_parent()
	while n != null:
		if n is ScrollContainer or (n is Control and (n as Control).rect_clip_content):
			ink = ink.clip(_rect(n))
			if ink.size.x <= 0.0 or ink.size.y <= 0.0:
				return Rect2()
		n = n.get_parent()
	return ink


# The parents, so a number that is wrong says which rect made it wrong.
func _chain(c: Control, scale: float) -> String:
	var out := ""
	var n: Node = c.get_parent()
	var hops := 0
	while n != null and n is Control and hops < 3:
		out += "  <- %s %.0f" % [n.name, _rect(n).size.y / scale]
		n = n.get_parent()
		hops += 1
	return out


func _fault(where: String, what: String) -> void:
	print("  ! [%s] %s" % [where, what])
	_bad += 1


func _descends(a: Node, b: Node) -> bool:
	var n := a
	while n != null:
		if n == b:
			return true
		n = n.get_parent()
	return false


func _audit(where: String, root: Node) -> void:
	if root == null:
		_fault(where, "the screen was never built")
		return

	var all := []
	_gather(root, all)

	var presses := []
	var labels := []
	for c in all:
		if _is_press(c):
			presses.append(c)
		elif c is Label:
			labels.append(c)

	_checked += presses.size() + labels.size()
	if OS.get_environment("UI_VERBOSE") != "":
		print("    %-10s %d pressable, %d words" % [where, presses.size(), labels.size()])

	# A SCREEN THAT MEASURES NOTHING IS NOT A SCREEN THAT PASSES.
	#
	# Every check below iterates over what was gathered, so a screen the audit
	# never saw reports zero faults and is indistinguishable in the total from one
	# that was measured and was clean. That is the one failure mode an audit must
	# not have, and it happened: the forge editor came back empty on two runs in
	# three and the sweep said every shape was clean.
	if presses.empty() and labels.empty():
		var vis: bool = root is CanvasItem and (root as CanvasItem).visible
		var up := []
		for k in Screens.i()._layers.keys():
			var n = Screens.i()._layers[k]
			if n != null and n.visible:
				up.append(k)
		_fault(where, "measured nothing — the layer is %s and has %d children; showing %s"
				% ["visible" if vis else "HIDDEN", root.get_child_count(), str(up)])
		return

	# ---- EMPTY: built and unpressable
	for p in presses:
		var r: Rect2 = _rect(p)
		if r.size.x < 1.0 or r.size.y < 1.0:
			_fault(where, "%s is %.0f x %.0f — built and cannot be pressed"
					% [p.name, r.size.x, r.size.y])

	# ---- TARGET: what a finger is owed
	#
	# HEIGHT, WHICH IS THE DESIGN'S OWN RULE — the C# suite calls this
	# EveryControlHeightClearsTheTapTarget and means it. Width is not the same
	# question: the three stops of a segmented control are 71 wide and 88 tall,
	# and a thumb landing anywhere in that band hits the right one, because the
	# three are side by side and there is nothing else in the row to hit.
	var scale: float = max(0.0001, Layout.canvas_scale())
	if not (where in TOOL_SCREENS):
		for p in presses:
			var r: Rect2 = _rect(p)
			if r.size.x < 1.0 or r.size.y < 1.0:
				continue
			var tall: float = r.size.y / scale
			if tall < Access.TAP_TARGET - 0.5:
				_fault(where, "%s is %.0f units tall, under the %d a finger is owed%s"
						% [p.name, tall, int(Access.TAP_TARGET), _chain(p, scale)])

	# ---- COLLISION: a word over a control
	for l in labels:
		if l.text == "":
			continue
		var li: Rect2 = _clip_to_ancestors(l, _ink(l))
		if li.size.x < 1.0:
			continue
		for p in presses:
			if _descends(l, p) or _descends(p, l):
				continue
			var pr: Rect2 = _rect(p)
			if not li.intersects(pr):
				continue
			# only report a real bite, not a hairline of anti-aliasing
			var over := li.clip(pr)
			if over.size.x > 2.0 and over.size.y > 2.0:
				var f2 = l.get_font("font")
				var lb := _rect(l)
				_fault(where, "'%s' into %s  ink x %.0f..%.0f y %.0f..%.0f  ctrl x %.0f..%.0f y %.0f..%.0f  box %.0f..%.0f  %dpt"
						% [_say(l.text, 20), p.name,
						li.position.x / scale, li.end.x / scale,
						li.position.y / scale, li.end.y / scale,
						pr.position.x / scale, pr.end.x / scale,
						pr.position.y / scale, pr.end.y / scale,
						lb.position.x / scale, lb.end.x / scale,
						int(f2.size) if f2 is DynamicFont else 0])

	# ---- BOUNDS: nothing on the metal
	var glass := Rect2(
			Vector2(Layout.chassis_left(), Layout.chassis_top()) * scale,
			(Layout.screen_size() / scale - Vector2(
					Layout.chassis_left() + Layout.chassis_right(),
					Layout.chassis_top() + Layout.chassis_bottom())) * scale)
	for l in labels:
		if l.text == "":
			continue
		var li2: Rect2 = _clip_to_ancestors(l, _ink(l))
		if li2.size.x < 1.0:
			continue
		# THE TOLERANCE IS IN CANVAS UNITS, NOT IN PIXELS, and it has to be.
		# A label's ink is measured by asking the font for the string's advance
		# at the size it is rasterised at, and a hinted face rounds every glyph's
		# advance to a whole pixel — so the same twenty-eight characters come out
		# a unit wider at one scale than at another. A word set flush to the
		# plate's own edge then reports as being ON the metal at 0.75 and off it
		# at 0.5625, which is a fact about the rasteriser rather than about the
		# layout. One canvas unit of slack is under a third of a stroke and well
		# inside the rounding; anything a player could see is many.
		var slack: float = 1.0 * scale
		var over: float = max(glass.position.x - li2.position.x, li2.end.x - glass.end.x)
		if over > slack:
			_fault(where, "'%s' reaches the bezel by %.1f units"
					% [_say(l.text, 28), over / scale])

	# ---- AND THE OTHER TWO EDGES, which nothing was checking
	#
	# The rule is "no word ever sits on the metal" and it has four sides. Only two
	# of them were measured, because on a portrait phone a column that overruns
	# runs out of the BOTTOM and that had never happened — every screen was
	# authored against the one plate it was ever shown on.
	#
	# Turned, it happens immediately: the forge editor sizes its deck against
	# Layout.plate_height, which is the authored eleven hundred and twenty-eight
	# and not the five hundred and seventy-five a landscape display leaves, so
	# the deck and every band under it were drawn off the end of the machine. The
	# audit reported no faults, because nothing it measured was horizontal.
	# NOT CLIPPED FIRST, WHICH IS THE WHOLE POINT. Screens.layer clips every plate
	# to the glass, so a control drawn off the bottom is invisible rather than on
	# the metal — and a check that measures what is left after the clip can never
	# see it. This measures where the layout PUT it.
	#
	# A scroll is the one legitimate reason to be outside: the page is longer than
	# the window and the reader brings the rest up. Everything else that is mostly
	# outside is a control the player cannot reach.
	for c in presses + labels:
		if _in_scroll(c):
			continue
		var cr: Rect2 = _rect(c)
		if cr.size.x < 1.0 or cr.size.y < 1.0:
			continue
		var seen := cr.clip(glass)
		var area: float = cr.size.x * cr.size.y
		if area <= 0.0 or seen.size.x * seen.size.y >= area * 0.5:
			continue
		var down: float = max(glass.position.y - cr.position.y, cr.end.y - glass.end.y)
		_fault(where, "%s is %.0f units off the plate, %s" % [c.name, max(0.0, down) / scale,
				"above the top" if cr.position.y < glass.position.y else "below the floor"])
