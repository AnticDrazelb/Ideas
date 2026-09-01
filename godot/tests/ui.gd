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


func _init() -> void:
	Directory.new().remove(Store.FILE)
	_root = load("res://game/Main.tscn").instance()
	get_root().add_child(_root)


func _idle(_dt: float) -> bool:
	_n += 1
	if _wait > 0:
		_wait -= 1
		return false
	_run()
	return false


func _run() -> void:
	if _step == 0:
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
		print("plate %.0f x %.0f  screen %s  scale %.4f  panel floor %.0f" % [
				Layout.plate_width(), Layout.plate_height(),
				str(Layout.screen_size()), Layout.canvas_scale(), Screens.panel_floor()])

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
		_step += 1
		_wait = 20
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


func _show(id: String) -> void:
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

	if where == "vaults":
		var sc2: float = max(0.0001, Layout.canvas_scale())
		for c in all:
			if c.name in ["head", "prev", "next", "vaultName", "plate"]:
				var rr := _rect(c)
				print("    %-10s %-14s x %.0f..%.0f  w %.0f  parent %s" % [
						c.name, c.get_class(), rr.position.x / sc2, rr.end.x / sc2,
						rr.size.x / sc2, c.get_parent().name])

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
				_fault(where, "'%s' into %s  ink %.0f..%.0f  box %.0f..%.0f  ctrl %.0f..%.0f  %dpt"
						% [l.text.substr(0, 20), p.name,
						li.position.x / scale, li.end.x / scale,
						lb.position.x / scale, lb.end.x / scale,
						pr.position.x / scale, pr.end.x / scale,
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
		if li2.position.x < glass.position.x - 1.0 or li2.end.x > glass.end.x + 1.0:
			_fault(where, "'%s' reaches the bezel" % l.text.substr(0, 28))
