extends Node
class_name Hud

# THE READOUT IS A ROW OF CHIPS.
#
# Two lines of bare text was legible and anonymous: three labels in a row, all
# the same shape, and nothing to tell you at a glance which number belonged to
# which idea. A chip with a SYMBOL in it is read before it is parsed — you find
# the green diamond, not the word NODES — and it survives being glanced at with
# a thumb over half of it. The label stays, quietly, because a symbol nobody has
# met yet is a rebus.

var _canvas: CanvasLayer
var _dir = null

var _fold_n: Label
var _par_n: Label
var _key_n: Label
var _key_tot: Label
var _go_n: Label
var _hint_n: Label
var _lv_no: Label
var _lv_name: Label
var _vault: Label
var _toast: Label
var _plate_clock: Label
var _caption: Label

var _key_chip: Control
var _go_chip: Control
var _flash: ColorRect
var _vignette: ColorRect
var _plate_bar: ColorRect
var _aperture: UiRect
var _ticks := [null, null, null, null]
var _fold_btns := [null, null, null, null]
var _root: UiRect

var _depth_pool := []
var _depth_root: UiRect

# The two verbs, taught once, on the cube that needs them. See Coach.
var coach = null

var _toast_t := 0.0
var _flash_t := 0.0
var _flash_dur := 0.0
var _vig := 0.0
var _cap_t := 0.0

# Whole seconds last printed on the plate clock; -1 means the clock is off.
var _last_secs := -1
var _flash_col := Color(0, 0, 0, 0)
var _go_shown := ""

# the last three flashes, on the real clock — see flash()
var _flash_at := []
var _flash_idx := 0

# The turn indices are InputRouter's: 0 left, 1 right, 2 up, 3 down. One table,
# so the passive mark and the pressable one cannot drift apart.
const FOLD_ANCHOR := [Vector2(0, 0.5), Vector2(1, 0.5), Vector2(0.5, 1), Vector2(0.5, 0)]

# Where the arrow is drawn: against the inside of the stroke, at each edge.
const FOLD_MARK := [
	Vector2(HudMetrics.FOLD_EDGE, 0), Vector2(-HudMetrics.FOLD_EDGE, 0),
	Vector2(0, -HudMetrics.FOLD_EDGE), Vector2(0, HudMetrics.FOLD_EDGE),
]

# Where the assisted fold's HIT PLATE sits: in from the aperture's edge by half a
# tap target, so an eighty-eight unit target is entirely inside the housing
# rather than half of it on the bezel.
const FOLD_SLOT := [
	Vector2(Access.TAP_TARGET * 0.5, 0), Vector2(-Access.TAP_TARGET * 0.5, 0),
	Vector2(0, -Access.TAP_TARGET * 0.5), Vector2(0, Access.TAP_TARGET * 0.5),
]
const FOLD_SPIN := [90.0, -90.0, 0.0, 180.0]

# The numbers this file and Coach both have to agree about live in HudMetrics;
# these three are re-exported because the rest of the game names them here.
const FOLD_ICON := HudMetrics.FOLD_ICON
const FOLD_EDGE := HudMetrics.FOLD_EDGE
const MARK_BAND := HudMetrics.MARK_BAND

# SCREENS IS LOADED WHEN IT IS PRESSED, not when this file is parsed. Screens
# builds a pause card that carries the director that owns this readout, so naming
# the class here is a ring Godot refuses to load. See Store's note on the same
# tax: the one call is deferred, the many are not.
const _SCREENS := []


func _screens():
	if _SCREENS.empty():
		_SCREENS.append(load("res://ui/Screens.gd"))
	return _SCREENS[0]


# Make.of, because a class may not name itself in its own file — see Make.
static func build(dir) -> Hud:
	var c := UiKit.canvas("HUD", 10)
	var hud: Hud = Make.of("res://ui/Hud.gd")
	hud.name = "Hud"
	hud._canvas = c
	hud._dir = dir
	c.add_child(hud)
	hud._compose()
	return hud


func _compose() -> void:
	# a zeroed timestamp is "now" during the first second of the process, which
	# would spend the whole flash budget before anything happened
	for _i in range(Access.FLASHES_PER_SECOND):
		_flash_at.append(-9.0)

	# THE READOUT IS ON THE GLASS.
	#
	# The root used to be the whole display, which was survivable while the
	# housing was a twenty-eight unit border and wrong the whole time: the chips
	# are anchored to the top of this rect, so their upper edge was under the
	# bezel and the fold arrows were hard against it. With a real case in front of
	# them they were half swallowed, and the vault name ran off the side of the
	# machine altogether.
	#
	# Insetting HERE rather than in each control is also what keeps rule one of
	# the chassis true by construction — there is no longer anywhere in this file
	# that a word CAN be put on the metal, so it cannot be done by accident later.
	#
	# TWO RECTS AND NOT ONE, and the reason is in UiCanvasRoot: the scaled root is
	# re-cut every frame from the safe area, so anything anchored directly to it
	# has its offsets overwritten. The inset went on the direct child the first
	# time and lasted until frame one.
	var scaled := UiKit.root_of(_canvas)
	var safe := UiKit.rect(scaled, "safe", Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	_root = UiKit.rect(safe, "root", Vector2.ZERO, Vector2.ONE,
			Vector2(Layout.chassis_left(), Layout.chassis_bottom()),
			Vector2(-Layout.chassis_right(), -Layout.chassis_top()))

	# and clipped to it, so the readout cannot reach the metal however long a
	# vault is named or however narrow the display gets — the same boundary every
	# screen gets in Screens.layer, for the same reason
	_root.rect_clip_content = true

	# ---- the flash and the vignette sit under everything ---------------------
	_flash = UiKit.panel(_root, "flash", Color(0, 0, 0, 0),
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	_vignette = UiKit.panel(_root, "vignette", Color(0, 0, 0, 0),
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)

	# ---- the aperture --------------------------------------------------------
	#
	# THE BOARD IS INSIDE THE MACHINE, AND UNTIL NOW IT WAS NOT.
	#
	# Every other surface in this interface is a framed plate — that is what makes
	# the chrome read as an instrument rather than as words on black — and the one
	# thing the whole game is about had no housing at all. It floated, and because
	# a square cube fitted to a portrait width cannot fill a portrait height, what
	# it floated in was a large and obviously unaccounted-for hole.
	#
	# This is the aperture that hole was always meant to be: the same rounded
	# stroke and the same edge rust as every control, drawn around exactly the
	# rectangle Layout reserves for the board. It costs one nine-sliced quad and it
	# changes what the empty space MEANS — from "the layout did not reach" to
	# "this is the window".
	#
	# A STROKE, NOT A PLATE. This is a canvas layer over the viewport's 3D, so
	# anything filled here would paint over the cube. And the void inside it stays
	# void: a cell that is not there is still unrendered space, which is the one
	# thing this game will not decorate.
	#
	# AND THE FOUR NUMBERS COME FROM LAYOUT, not from the two bands and the two
	# insets added up here. They used to be the same sum on both sides, which is
	# not the same thing as one definition: the camera contains the solid inside
	# ITS window, so a stroke drawn from a private copy of the arithmetic is a line
	# the cube can cross without anything noticing. See Layout.aperture_insets.
	var ap: Array = Layout.aperture_insets()
	_aperture = UiKit.rect(_root, "aperture", Vector2(0, 0), Vector2(1, 1),
			Vector2(ap[0], ap[1]), Vector2(-ap[2], -ap[3]))
	UiKit.stroke(_aperture, UiKit.edge())

	# ON THE APERTURE, because everything it points at is in the window: the ring
	# lands on a square of the board and the swipe travels across it. Built here
	# rather than last so it draws UNDER the readout — a prompt is the quietest
	# thing on the screen, not the loudest.
	coach = load("res://ui/Coach.gd").new()
	coach.build(_aperture)

	# ---- top band ------------------------------------------------------------
	var top := UiKit.rect(_root, "barTop", Vector2(0, 1), Vector2(1, 1),
			Vector2(0, -160), Vector2(0, 0))

	# FOUR READINGS, FOUR INSTRUMENTS.
	#
	# These were four runs of bare text sharing one strip, which is a sentence
	# rather than a panel: nothing said where one number stopped and the next
	# began, and the colours had to do all the separating on their own. Each is now
	# a lit chip in the colour of the thing it counts — the same colour that thing
	# is on the board — so the top of the screen reads as instrumentation, and a
	# glance can find the one number it came for without parsing the other three.
	var fold_chip := _chip(top, "foldChip", 0.00, 0.25, Palette.rust())
	UiKit.label(fold_chip, "foldIcon", "▤", 20, Palette.rust(), UiKit.Anchor.MIDDLE_LEFT,
			Vector2.ZERO, Vector2.ONE, Vector2(14, 0), Vector2.ZERO)
	_fold_n = UiKit.label(fold_chip, "foldN", "0", 34, Palette.INK, UiKit.Anchor.MIDDLE_CENTER,
			Vector2.ZERO, Vector2.ONE, Vector2(10, 0), Vector2(-26, 0))
	_par_n = UiKit.label(fold_chip, "parN", "/0", 20, Palette.dim(), UiKit.Anchor.MIDDLE_RIGHT,
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2(-12, 0))

	_key_chip = _chip(top, "keyChip", 0.25, 0.50, Palette.NODE)
	UiKit.label(_key_chip, "keyIcon", "◆", 20, Palette.NODE, UiKit.Anchor.MIDDLE_LEFT,
			Vector2.ZERO, Vector2.ONE, Vector2(14, 0), Vector2.ZERO)
	_key_n = UiKit.label(_key_chip, "keyN", "0", 34, Palette.NODE, UiKit.Anchor.MIDDLE_CENTER,
			Vector2.ZERO, Vector2.ONE, Vector2(10, 0), Vector2(-26, 0))
	_key_tot = UiKit.label(_key_chip, "keyTot", "/0", 20, Palette.dim(), UiKit.Anchor.MIDDLE_RIGHT,
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2(-12, 0))

	_go_chip = _chip(top, "goChip", 0.50, 0.78, Palette.ARC)
	UiKit.label(_go_chip, "goLbl", "TO GO", 21, Palette.ARC, UiKit.Anchor.MIDDLE_LEFT,
			Vector2.ZERO, Vector2.ONE, Vector2(14, 0), Vector2.ZERO)
	_go_n = UiKit.label(_go_chip, "goN", "·", 34, Palette.ARC, UiKit.Anchor.MIDDLE_RIGHT,
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2(-14, 0))

	var hint_chip := _chip(top, "hintChip", 0.78, 1.00, Palette.LOCK)
	UiKit.label(hint_chip, "hintIcon", "⬡", 20, Palette.LOCK, UiKit.Anchor.MIDDLE_LEFT,
			Vector2.ZERO, Vector2.ONE, Vector2(14, 0), Vector2.ZERO)
	_hint_n = UiKit.label(hint_chip, "hintN", "3", 30, Palette.LOCK, UiKit.Anchor.MIDDLE_RIGHT,
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2(-14, 0))

	_lv_no = UiKit.label(top, "lvNo", "—", 22, Palette.rust(), UiKit.Anchor.MIDDLE_LEFT,
			Vector2(0, 0), Vector2(0.6, 0.45), Vector2(28, 0), Vector2.ZERO)
	_lv_name = UiKit.label(top, "lvName", "—", 22, Palette.dim(), UiKit.Anchor.MIDDLE_LEFT,
			Vector2(0, 0), Vector2(0.9, 0.45), Vector2(96, 0), Vector2.ZERO)
	_vault = UiKit.label(top, "vault", "", 20, Palette.dim(), UiKit.Anchor.MIDDLE_RIGHT,
			Vector2(0.5, 0), Vector2(1, 0.45), Vector2.ZERO, Vector2(-28, 0))

	# LAYOUT'S NUMBER, NOT A COPY OF IT. This rule closes the top band and the
	# camera frames the board against the same band, so a literal here is the
	# second definition that the aperture used to have — change one and the readout
	# draws a line where the board now starts.
	var rule := UiKit.rule(_root, 1.0)
	(rule.get_parent() as UiRect).set_anchored_position(Vector2(0, -Layout.TOP_BAND))

	# ---- the plate clock -----------------------------------------------------
	#
	# The count can be HEARD in the last three seconds, but it also has to be
	# SEEN, and the number cannot go where the eyes are — which is the board. So
	# it is a bar, at the top edge, draining.
	_plate_bar = UiKit.panel(_root, "plateBar", Palette.rust(),
			Vector2(0, 1), Vector2(1, 1), Vector2(0, -6), Vector2(0, 0))
	# AND IT IS INSIDE THE WINDOW NOW, because the band it used to sit in is gone.
	# It hung between the readout's rule and the top of the old square aperture;
	# the stroke reaches the rule now, so the place left for it is under the
	# stroke, past the fold mark. On the aperture, so it goes where the window
	# goes. See HudMetrics.MARK_BAND.
	_plate_clock = UiKit.label(_aperture, "plateClock", "", 22, Palette.RUST_HI,
			UiKit.Anchor.UPPER_CENTER, Vector2(0, 1), Vector2(1, 1),
			Vector2(0, -(HudMetrics.MARK_BAND + HudMetrics.CLOCK_HEIGHT)),
			Vector2(0, -HudMetrics.MARK_BAND))
	_plate_bar.get_parent().visible = false

	# ---- the four folds, at the four edges -----------------------------------
	#
	# FOUR ARROWS, NOT FOUR DASHES.
	#
	# This is the one piece of the HUD that is about the RULE rather than the run
	# — where you stand decides which folds you have — and it was four six-unit
	# bars. On a phone that is exactly what they looked like: orange ticks at the
	# edges of the screen with nothing to say which way any of them meant. The rule
	# was reading as dust on the lens.
	#
	# The arrow already exists; the manual draws the four folds with it. So this is
	# the mark the player was taught, pointing the way the fold goes, lit when that
	# fold has footing and nearly out when it does not.
	#
	# ONE MARK PER EDGE, IN BOTH MODES. The assisted fold buttons want to be in the
	# same four places, because that is where the answer to "can I fold that way"
	# already is — so they are, and the passive arrow steps aside when the pressable
	# one is on. Two arrows at one edge would be the interface disagreeing with
	# itself about which one to look at.
	#
	# THEY HANG ON THE APERTURE, NOT ON THE SCREEN. They were anchored to the
	# screen edges with hand-written offsets that happened to land near the board,
	# which is a coincidence that survives exactly until the bands change height.
	# Parented to the frame, "on the left edge of the play area" is what the anchor
	# SAYS, in both orientations and on any phone.
	for t in range(4):
		_ticks[t] = _tick("tick" + str(t), FOLD_ANCHOR[t], FOLD_MARK[t], FOLD_SPIN[t])
		_fold_btns[t] = _fold_button("fold" + str(t), t, FOLD_ANCHOR[t], FOLD_SLOT[t],
				FOLD_MARK[t], FOLD_SPIN[t])
	refresh_assist()

	# ---- bottom band ---------------------------------------------------------
	var bot := UiKit.rect(_root, "barBot", Vector2(0, 0), Vector2(1, 0),
			Vector2(0, 0), Vector2(0, Layout.BOTTOM_BAND))
	_third(bot, 0, "MENU", "_on_menu")
	_third(bot, 1, "UNDO", "_on_undo")
	_third(bot, 2, "HINT", "do_hint")

	# DEPTH, PRINTED ON EVERY CELL. Off by default, because brightness IS distance
	# and a number on top of that is a second answer to a question already
	# answered. On, it is the fastest way to learn the ramp — and for anyone who
	# cannot read the ramp at all, it is the only way.
	_depth_root = UiKit.rect(_root, "depth", Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)

	# THE SAME MOVE AT THE OTHER END. The toast sat 140 units up from the glass,
	# which was the dead plate over the control band; that plate is window now and
	# 140 is under the down fold's arrow. It goes above the coach's line, in the
	# band between the marks and the board — the cube is fitted inside the square
	# with Layout.FIT_MARGIN to spare.
	_toast = UiKit.label(_aperture, "toast", "", 26, Palette.INK, UiKit.Anchor.LOWER_CENTER,
			Vector2(0, 0), Vector2(1, 0),
			Vector2(0, HudMetrics.TOAST_FOOT),
			Vector2(0, HudMetrics.TOAST_FOOT + HudMetrics.TOAST_BOX))
	_toast.add_color_override("font_color", Color(1, 1, 1, 0))

	# THE CAPTION SITS UNDER THE PLATE CLOCK, NOT UNDER THE TOAST.
	#
	# A toast is the game talking; a caption is the game's SOUND written down, and
	# they are different enough that stacking them in one line would make each look
	# like an interruption of the other. It goes just below the top rule for the
	# same reason the plate clock does: the eyes are on the board, and that is the
	# nearest edge to them.
	_caption = UiKit.label(_aperture, "caption", "", 19, Palette.dim(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1),
			Vector2(0, -(HudMetrics.CAPTION_TOP + HudMetrics.CAPTION_HEIGHT)),
			Vector2(0, -HudMetrics.CAPTION_TOP))
	_caption.add_color_override("font_color", Color(1, 1, 1, 0))

	set_process(false)


func _chip(bar: Control, node_name: String, x0: float, x1: float, hue: Color) -> UiRect:
	var rt := UiKit.rect(bar, node_name, Vector2(x0, 0.42), Vector2(x1, 1.0),
			Vector2(14, 8), Vector2(-6, -10))
	UiKit.framed(rt, Color(hue.r, hue.g, hue.b, 0.10), Color(hue.r, hue.g, hue.b, 0.80))
	return rt


func _fold_button(node_name: String, turn: int, anchor: Vector2, seat: Vector2,
		mark: Vector2, spin: float) -> UiButton:
	var h := Access.TAP_TARGET * 0.5
	var slot := UiKit.rect(_aperture, node_name, anchor, anchor,
			Vector2(seat.x - h, seat.y - h), Vector2(seat.x + h, seat.y + h))

	# The plate is a hit target rather than a picture: the ARROW is the picture,
	# and it is the same arrow the passive tick draws. A bracketed "<" would be a
	# third thing meaning the same as the other two.
	var b := UiKit.bracketed(slot, node_name, "", funcref(self, "_fold_" + str(turn)), 1)

	# The C# destroys the label outright; hiding it is the same picture and leaves
	# the button's own reference to it valid, which set_primary and the enabled
	# tint both assume.
	var lbl := b.label_node()
	if lbl != null:
		lbl.visible = false

	# AND THE ARROW SITS WHERE THE PASSIVE ONE SITS, which is not the middle of
	# its own plate. The plate is eighty-eight units of thumb and has to stay
	# inside the housing; the arrow is a mark on the stroke and belongs against it.
	# Offsetting the glyph by the difference puts the two modes' arrows at the same
	# point on the screen, so turning assist on moves a hit target and nothing the
	# player is looking at.
	var glyph := UiKit.icon(b, "arrow", Palette.rust(),
			HudMetrics.FOLD_ICON, Vector2(0.5, 0.5), mark - seat)
	_spin(glyph, spin)
	return b


# THE CLOSURE, WRITTEN OUT. The C# captures `turn` in a lambda per button; a
# FuncRef takes no arguments and GDScript 3.5 has no lambda, so the capture is
# four one-line methods. They are the whole difference.
func _fold_0() -> void:
	_fold(0)


func _fold_1() -> void:
	_fold(1)


func _fold_2() -> void:
	_fold(2)


func _fold_3() -> void:
	_fold(3)


func _fold(turn: int) -> void:
	var s: Session = _dir.s
	if s == null or s.lv == null or s.won:
		return
	# the same two lines the swipe and the arrow keys both end in, so an assisted
	# fold is the same event and gets the same refusal
	if not s.try_turn(turn):
		s.refuse_turn(turn)


# The glyph is drawn pointing up; every other direction is that one turned. The
# rect is anchored by its centre, so the pivot has to be moved there before the
# rotation means what it says.
static func _spin(img: TextureRect, deg: float) -> void:
	var rt := img.get_parent() as UiRect
	rt.set_pivot(Vector2(0.5, 0.5))
	rt.rect_rotation = -deg


func _tick(node_name: String, anchor: Vector2, offset: Vector2, spin: float) -> TextureRect:
	var r := Palette.rust()
	var i := UiKit.icon(_aperture, "arrow", Color(r.r, r.g, r.b, 0.30),
			HudMetrics.FOLD_ICON, anchor, offset)
	i.get_parent().name = node_name
	_spin(i, spin)
	return i


func _third(parent: Control, i: int, label: String, method: String) -> void:
	# The band is 128 tall and these were inset 22 top and bottom, which leaves 84
	# — four units under the target, on the three controls that are pressed more
	# than everything else in the game put together.
	var inset := (128.0 - Access.TAP_TARGET) * 0.5
	var slot := UiKit.rect(parent, label, Vector2(i / 3.0, 0), Vector2((i + 1) / 3.0, 1),
			Vector2(10, inset), Vector2(-10, -inset))
	UiKit.bracketed(slot, label, label, funcref(self, method))


func _on_menu() -> void:
	_screens().show_pause(_dir)


func _on_undo() -> void:
	if _dir.s.undo():
		_dir.sfx.undo()
	else:
		toast("NOTHING TO UNDO")


func do_hint() -> void:
	var a = _dir.s.hint()
	if a != null:
		toast(("FOLD " if a.is_turn else "STEP ") + Turns.ID[a.dir].to_upper())
		refresh(_dir.s)
	else:
		toast("NO HINTS LEFT" if _dir.s.hints_left <= 0 else "NO ROUTE FROM HERE")


# ---- state -----------------------------------------------------------------

func set_visible(on: bool) -> void:
	_root.visible = on


# A FOLD THAT IS NOT A SWIPE.
#
# Both of this game's gestures are two-part — travel far enough, or stay still
# long enough — and neither is available to somebody using a head pointer, a
# switch, or one unsteady finger. There is no difficulty argument for the swipe
# either: the same four folds have been on the arrow keys since the first build,
# for anybody who happens to open this on a desktop. This is that keyboard, for a
# thumb.
#
# They sit ON the four ticks, which is the one place in the interface that
# already means "this is the fold in this direction" — so the buttons appear
# where the answer to "can I fold that way" already was, rather than as a fifth
# thing to learn.
func refresh_assist() -> void:
	var on := Access.assist()
	for i in range(_fold_btns.size()):
		var b = _fold_btns[i]
		if b != null and b.visible != on:
			b.visible = on
		# the pressable arrow replaces the passive one rather than landing on top
		# of it — see the note where the four are built
		var t = _ticks[i]
		if t != null and t.get_parent().visible == on:
			t.get_parent().visible = not on


# THE WINDOW MOVES WHEN THE DISPLAY CHANGES SHAPE, and until it was a square it
# did not have to: four constant insets are the same four constants in any
# aspect. A square cut out of the glass is not — its side is the shorter of two
# things that both changed — so the one rect every fold mark hangs on has to be
# re-cut when the device turns.
func relayout() -> void:
	if _aperture == null:
		return
	var ap: Array = Layout.aperture_insets()
	_aperture.anchor_to(Vector2(0, 0), Vector2(1, 1),
			Vector2(ap[0], ap[1]), Vector2(-ap[2], -ap[3]))


func toast(msg: String) -> void:
	_toast.text = msg.to_upper()
	_toast_t = 1.6


# THE FLASH IS THE ONE EFFECT WITH A SEIZURE IN IT, and the game cannot promise
# 2.3.1 by being tasteful — it can only promise it by counting.
#
# Every flash here is raised by something the PLAYER did: a fold, a node, a lock,
# a plate. Nothing in the design limits how fast those can happen and nothing
# should, so on a small cube a quick hand can put four or five full-screen
# luminance changes inside one second without the game having done anything
# wrong. So the last few are remembered and a fourth inside a second is simply
# not raised: the event still gets its sound, its haptic, its shake and its
# debris, and loses only the one channel that was over budget.
#
# The window is the real clock, not the bent one — a hitstop must not buy back a
# flash.
func flash(c: Color, amount: float, dur: float = 0.5) -> void:
	amount *= Access.light_amount()
	if amount <= 0.001:
		return

	var now := OS.get_ticks_msec() / 1000.0
	var recent := 0
	for i in range(_flash_at.size()):
		if now - _flash_at[i] < 1.0:
			recent += 1
	if recent >= Access.FLASHES_PER_SECOND:
		return

	_flash_at[_flash_idx] = now
	_flash_idx = (_flash_idx + 1) % _flash_at.size()

	_flash_col = c
	_flash_t = amount
	_flash_dur = dur


# THE OTHER FULL-SCREEN EFFECT, AND THE ONE THAT WAS NOT GATED.
#
# flash() is careful: it scales by the light setting, it refuses a fourth inside
# a second, and NONE turns it off outright. This was one line with no gate on it
# at all — so a player who had set the light channel to NONE, which is the
# setting a photosensitive player is told to use, still got the whole screen
# pulsing to forty-two per cent black on every refused fold and every dead end.
# The setting said none and something was still flashing.
#
# It is scaled rather than switched, like flash(), so REDUCED gets a subtler one
# instead of a cliff.
#
# NOT IN THE THREE-PER-SECOND BUDGET, and that is a judgement rather than an
# oversight. 2.3.1's threshold is a PAIR of opposing luminance changes of at
# least a tenth of maximum; this is a darkening, on a screen that is already
# nearly black, so the excursion is small and downward. Sharing the budget would
# mean a burst of refusals could spend the flash allowance on vignettes and leave
# a genuine flash unable to fire, which trades a real signal for a theoretical
# one.
func vignette(amount: float) -> void:
	amount *= Access.light_amount()
	if amount <= 0.001:
		return
	_vig = max(_vig, amount)


# The words for a sound that has just played. Short-lived, and it REPLACES rather
# than queues: two cues inside a second are two things that just happened, and a
# caption still showing the first of them is telling the player about the past.
func caption(words: String) -> void:
	_caption.text = words
	_cap_t = 1.1


# WHAT THE CHIPS ARE SHOWING, so that a frame in which nothing changed costs
# nothing. refresh() is called from tick(), which is every frame, and it used to
# rebuild eight strings each time — a caption that is three concatenations, four
# counters, a name. A Label compares the string before it rebuilds the mesh, so
# the canvas was safe; the GARBAGE was not, and a collection landing in the
# middle of a fold is the one hitch this game cannot afford.
#
# Sentinels start at a value nothing can be, so the first refresh always writes.
# -1 is impossible for every counter here and _last_band is only consulted after
# the daily/forge case has been ruled out.
var _last_turns := -1
var _last_par := -1
var _last_held := -1
var _last_keys := -1
var _last_hints := -1
var _last_lv_no := -1
var _last_band := -1
var _last_named := false
var _last_name = null


func refresh(s: Session) -> void:
	if s == null or s.lv == null:
		return

	if s.turns != _last_turns:
		_last_turns = s.turns
		_fold_n.text = Num.of(s.turns)
	if s.lv.par != _last_par:
		_last_par = s.lv.par
		_par_n.text = Num.over(s.lv.par)

	var has_keys: bool = s.lv.keys.size() > 0
	_key_chip.visible = has_keys
	if has_keys:
		var held := 0
		for i in range(s.lv.keys.size()):
			if (s.kmask & (1 << i)) != 0:
				held += 1
		if held != _last_held:
			_last_held = held
			_key_n.text = Num.of(held)
		if s.lv.keys.size() != _last_keys:
			_last_keys = s.lv.keys.size()
			_key_tot.text = Num.over(s.lv.keys.size())

	if s.hints_left != _last_hints:
		_last_hints = s.hints_left
		_hint_n.text = Num.of(s.hints_left)

	# THE CAPTION IS THE EXPENSIVE ONE — three concatenations and a roman numeral,
	# every frame, to produce a string that changes once a vault. It is keyed on
	# the two things that can change it and nothing else.
	var named: bool = not (s.is_daily() or s.is_made())
	var band: int = Vaults.vault_of(s.level_no) if named else -1
	if s.level_no != _last_lv_no or named != _last_named:
		_last_lv_no = s.level_no
		_last_named = named
		_lv_no.text = "DAILY" if s.is_daily() else ("FORGE" if s.is_made() else Num.of(s.level_no))
	if band != _last_band:
		_last_band = band
		_vault.text = ("VAULT " + Vaults.roman_of(band) + " · " + Vaults.vault_name(band)) \
				if named else ""

	var nm: String = s.lv.name
	if nm != _last_name:
		_last_name = nm
		_lv_name.text = nm

	_refresh_ticks(s)
	_paint_go(s)


func _refresh_ticks(s: Session) -> void:
	var r := Palette.rust()
	for t in range(4):
		var ok := s.can_turn(t)
		_ticks[t].modulate = Color(r.r, r.g, r.b, 0.85 if ok else 0.14)

		# A CONTROL THAT CANNOT BE USED SAYS SO BEFORE IT IS PRESSED.
		#
		# The passive arrow already answered "does this fold have footing" by going
		# nearly out; the pressable one has to answer it too, or the assisted player
		# is the only one who has to find out by being refused. Set only on the
		# change, because this runs every frame.
		var b = _fold_btns[t]
		if b != null and b.enabled != ok:
			b.enabled = ok


func _paint_go(s: Session) -> void:
	var show: bool = Store.data().togo != 0 and not s.won
	_go_chip.visible = show
	if not show:
		return

	var pill: Array = s.remain.pill(s)
	var text: String = pill[0]
	var slip: bool = pill[1]
	var dead: bool = pill[2]
	var thinking: bool = pill[3]
	_go_n.text = text

	# A stale number must not be judged: folds have already gone up and the count
	# has not come down yet, so the verdict freezes at its last true value until
	# the answer catches up.
	if thinking and s.remain.value != -2:
		var c := _go_n.get_color("font_color")
		_go_n.add_color_override("font_color", Color(c.r, c.g, c.b, 0.45))
		return

	_go_n.add_color_override("font_color",
			Palette.fault() if dead else (Palette.LOCK if slip else Palette.ARC))
	_go_shown = text


func tick(dt: float, s: Session) -> void:
	if _toast_t > 0.0:
		_toast_t -= dt
		var a: float = clamp(_toast_t / 0.4, 0.0, 1.0)
		var ink := Palette.INK
		_toast.add_color_override("font_color", Color(ink.r, ink.g, ink.b, a))

	if _cap_t > 0.0:
		_cap_t -= dt
		var a: float = clamp(_cap_t / 0.3, 0.0, 1.0)
		var d := Palette.dim()
		_caption.add_color_override("font_color", Color(d.r, d.g, d.b, a))

	if _flash_t > 0.0:
		_flash_t = max(0.0, _flash_t - dt / max(0.05, _flash_dur))
		_flash.color = Color(_flash_col.r, _flash_col.g, _flash_col.b, _flash_t * 0.6)

	_vig = max(0.0, _vig - dt * 1.8)
	_vignette.color = Color(0, 0, 0, _vig * 0.42)

	# the plate clock, draining
	var plate: bool = s.world != 0 and s.plate_t > 0.0
	var bar_rect := _plate_bar.get_parent() as UiRect
	if bar_rect.visible != plate:
		bar_rect.visible = plate
	if plate:
		var k: float = s.plate_t / Session.PLATE_MS
		bar_rect.anchor_right = k
		# ONE STRING A SECOND, NOT ONE A FRAME. The clock counts whole seconds and
		# there are five of them, so the concatenation ran sixty times for every
		# value it produced.
		var secs := int(ceil(s.plate_t / 1000.0))
		if secs != _last_secs:
			_last_secs = secs
			_plate_clock.text = Num.of(secs) + "S"
	elif _last_secs != -1:
		_last_secs = -1
		_plate_clock.text = ""

	refresh(s)


# The coach rides with the depth pass because it needs the same two things — the
# camera and the view — to put a mark on a square of the board, and because both
# are the parts of the readout that are about the CELLS rather than about the run.
func tick_coach(s: Session, cam: Camera, view: CubeView, dt: float) -> void:
	if coach != null:
		coach.tick(s, cam, view, dt)


# One number per surface cell, placed where the cell actually is on screen. The
# labels are pooled and only ever repositioned, because this runs every frame
# while it is on.
func tick_depth(s: Session, cam: Camera, view: CubeView) -> void:
	var on: bool = Store.data().depth != 0 and s != null and s.lv != null and not s.won
	if not on:
		if _depth_root.visible:
			_depth_root.visible = false
		return
	if not _depth_root.visible:
		_depth_root.visible = true

	var n := s.n
	var want := n * n
	var used := 0
	while _depth_pool.size() < want:
		_depth_pool.append(UiKit.label(_depth_root, "d" + str(_depth_pool.size()), "", 21,
				Palette.INK, UiKit.Anchor.MIDDLE_CENTER, Vector2.ZERO, Vector2.ZERO,
				Vector2(-24, -12), Vector2(24, 12)))

	var xform: Transform = view.cube.global_transform
	# THE PIVOT GOES TO A SCREEN PIXEL, and everything between this pool and the
	# screen — the safe area, the chassis inset, the canvas scale — is in one
	# transform already. Inverting it is the whole conversion, and it cannot go
	# stale the way a hand-written sum of the same insets could. The flip back to
	# y-up is the last step, because anchored_position is measured the source's
	# way. See UiRect.
	var inv := _depth_root.get_global_transform().affine_inverse()
	var pool_h := _depth_root.rect_size.y
	for u in range(n):
		for v in range(n):
			var sc: Surf = s.surf[u * n + v]
			if not sc.has:
				continue
			var t: Label = _depth_pool[used]
			used += 1
			# EIGHTY-ONE OF THESE, EVERY FRAME, on a nine-cube. A depth is 0..8 by
			# construction, so the table always hits and the reference it hands back
			# is the one the Label already holds — which is what makes the assignment
			# free rather than merely cheap.
			t.text = Num.of(sc.d)
			# the number takes the cell's own colour, so it never claims to be a
			# different material from the block it is sitting on
			var c := Palette.tile(sc.t, sc.d, n)
			t.add_color_override("font_color", Color(c.r + 0.35, c.g + 0.35, c.b + 0.35, 0.85))
			var world: Vector3 = xform.xform(CubeGeometry.cell_to_object(n, sc.w))
			var local: Vector2 = inv.xform(cam.unproject_position(world))
			var rt := t.get_parent() as UiRect
			rt.set_anchored_position(Vector2(local.x, pool_h - local.y))
			if not rt.visible:
				rt.visible = true

	for i in range(used, _depth_pool.size()):
		var rt := _depth_pool[i].get_parent() as UiRect
		if rt.visible:
			rt.visible = false
