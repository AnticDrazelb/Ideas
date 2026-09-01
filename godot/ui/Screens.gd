extends Node
class_name Screens

# Every screen that is not the board.
#
# One canvas, one card at a time, and a stack so BACK always goes up one rather
# than home. The board keeps running underneath — the attract cube on the title
# screen is the same renderer and the same rules, just with nobody playing it.
#
# A NODE WITH A STATIC FRONT DOOR. The C# is a static class; here the state lives
# on one instance, because a FuncRef needs an object to call and this file is
# nothing but callbacks. `_S` is the same "built once, visible everywhere,
# reachable from a static function" storage Store and Turns use — a const array
# whose binding is fixed and whose contents are not.
const _S := []


static func i() -> Screens:
	return _S[0] if not _S.empty() else null


var _canvas: CanvasLayer
var _dir = null
var _layers := {}
var _stack := []
var _view_band := 0

# FORGESCREENS IS LOADED, NOT NAMED. It builds its own shelf out of this file's
# Layer and Row, so naming the class here is a ring Godot refuses to load. See
# Store's note on the same tax.
const _FORGE := []


static func forge():
	if _FORGE.empty():
		_FORGE.append(load("res://ui/ForgeScreens.gd"))
	return _FORGE[0]


static func build(dir) -> void:
	var s: Screens = Make.of("res://ui/Screens.gd")
	s.name = "Screens"
	s._dir = dir
	_S.clear()
	_S.append(s)

	s._canvas = UiKit.canvas("Screens", 20)
	s._canvas.add_child(s)

	s._build_title()
	s._build_vaults()
	s._build_manual()
	s._build_plate_teach()
	s._build_chapter()
	s._build_pause()
	s._build_calibrate()
	s._build_access()
	s._build_win()
	forge().call("build", dir)
	s._hide_all()
	s.set_process(true)


# THE DEVICE TURNED, AND A LAYOUT IS NOT A STRETCH.
#
# Every screen in this file decides its shape once, while it is being built —
# whether the settings panel is one column or two, whether the vault rack has a
# seed box under it or beside it, whether the title's cube is over the controls
# or next to them. None of that is an anchor that can be re-solved; it is a
# different arrangement of different rects. So a rotation throws the interface
# away and builds the other one.
#
# WHICH IS NOT AS VIOLENT AS IT SOUNDS. Nothing in here is state — the state is
# in Store and in the Session, and every screen reads it on the way up. What is
# lost is the fade a screen arrives with and whatever is typed in the seed box,
# and the second of those is worth a note: a player halfway through typing a
# cube number who turns their phone loses four characters. The alternative is a
# screen that cannot be read at all in the hand it is now in.
#
# The stack survives, so BACK still goes where it went before the turn.
static func reface(dir) -> void:
	var me := i()
	var up: String = me.top_id()
	var stack: Array = me._stack.duplicate()
	var canvas := me._canvas

	# _FORGE caches the SCRIPT, not the instance, so it stays; ForgeScreens clears
	# its own singleton when it is rebuilt, and its node is a child of this one.
	_S.clear()
	UiKit.drop(canvas)

	build(dir)
	var now := i()
	now._stack = stack
	now._restore(up, dir)


# The screen that is up, or "" for the board.
func top_id() -> String:
	for k in _layers.keys():
		var n = _layers[k]
		if n != null and n.visible:
			return k
	return ""


# PUT BACK WHAT WAS THERE, and four of them are not a plain show().
#
# A screen that was opened through a door has to be opened through the same door
# again: the vault rack paints itself from the band it was looking at, the forge
# from what is on the shelf, the editor from the cube in hand, and the win card
# from the session that raised it. The two that cannot come back are the chapter
# word, which is a clock rather than a screen and simply finishes early, and the
# board, which is not a screen at all.
func _restore(up: String, dir) -> void:
	match up:
		"":
			show(null)
		"title":
			show_title()
		"vaults":
			_open_vaults()
		"forge":
			forge().call("open_shelf")
		"forgeEdit":
			forge().call("open_editor", Forge.resume())
		"win":
			show_win(dir.s, dir)
		"pause":
			show_pause(dir)
		"chapter":
			show(null)
		_:
			show(up)


# ---- plumbing --------------------------------------------------------------

# A LAYER IS NOW TWO THINGS, AND EVERY SCREEN GOT THE SECOND ONE FREE.
#
# The full-bleed rect stays, because something has to cover the whole display so
# nothing behind a screen can be pressed through it. What it no longer does is
# CARRY the background: that has moved to a rounded plate inset by the chassis,
# so the interface reads as a screen recessed into a panel rather than as paint
# on the glass.
#
# The plate is what gets returned, so every screen in this file builds itself
# inside the housing without one of them having to know that the housing exists.
# Their anchors are all relative and they were already inset by twenty-eight or
# more; nothing had to be re-laid out.
#
# FOUR INSETS RATHER THAN ONE, because the case is a real object and its bezel is
# deeper at the top and bottom than at the sides. A single number here put the
# plate's own corner radius over the metal on two edges and left a band of it
# showing on the other two.
static func layer(id: String) -> UiRect:
	var me := i()
	var root := UiKit.root_of(me._canvas)
	var rt := UiKit.rect(root, id, Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	# the catcher: a screen eats every press that is not on one of its controls
	rt.mouse_filter = Control.MOUSE_FILTER_STOP
	me._layers[id] = rt

	var plate := UiKit.rect(rt, "plate", Vector2.ZERO, Vector2.ONE,
			Vector2(Layout.chassis_left(), Layout.chassis_bottom()),
			Vector2(-Layout.chassis_right(), -Layout.chassis_top()))

	var bg := NinePatchRect.new()
	bg.name = "bg"
	bg.texture = Sprites.frame(false)
	bg.patch_margin_left = Sprites.FRAME_SLICE
	bg.patch_margin_right = Sprites.FRAME_SLICE
	bg.patch_margin_top = Sprites.FRAME_SLICE
	bg.patch_margin_bottom = Sprites.FRAME_SLICE
	bg.modulate = Palette.SCRIM
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bg.anchor_right = 1.0
	bg.anchor_bottom = 1.0
	plate.add_child(bg)

	# THE GLASS IS A BOUNDARY, NOT A SUGGESTION.
	#
	# "No type ever sits on the metal" is the first rule of the chassis and until
	# now it was a thing every screen had to remember. It is a clip now. A sentence
	# that outgrows its box, a name longer than the gap left for it, a control
	# somebody anchors past the edge later — none of them can reach the bezel,
	# whatever anyone does upstream.
	#
	# It is a floor and not a fix: a word cut off at the edge of the glass is still
	# a bug, and this only guarantees that it is a bug INSIDE the screen. Text that
	# has to fit wraps or shrinks — see UiKit.label and UiKit.fit.
	plate.rect_clip_content = true
	return plate


# A SCREEN MADE OF WORDS IS NOT A WINDOW.
#
# The pause card sits over a puzzle somebody is in the middle of and should let it
# through. The manual, the plate lesson, calibrate, the vault list and the Forge
# are text from top to bottom — and brickwork behind a paragraph is just contrast
# the reader has to fight. Those get a solid plate, which is also the difference
# between an interface and a stack of labels floating over a rotating object.
static func solid_layer(l: UiRect) -> void:
	_tint(l, Palette.VOID)


static func _tint(plate: UiRect, col: Color) -> void:
	(plate.get_node("bg") as NinePatchRect).modulate = col


func _hide_all() -> void:
	for k in _layers:
		_layers[k].visible = false


static func show(id) -> void:
	var me := i()
	me._hide_all()
	if id != null:
		var go: UiRect = me._layers[id]
		go.visible = true
		if me._stack.empty() or me._stack[me._stack.size() - 1] != id:
			me._stack.append(id)
		me._arrive(go)
	else:
		me._stack.clear()
	me._dir.set_screen_up(id != null)


# A SCREEN THAT SNAPS IN HAS NO WEIGHT.
#
# A hundred and forty milliseconds of fade and a few pixels of rise, and the
# difference is not that it looks nicer — it is that the eye is TOLD something
# arrived, so it goes looking for what changed instead of re-reading a screen it
# thinks it was already on. Short enough that nobody waiting to press a button
# ever waits.
#
# On the UNSCALED clock, because menus are opened during hitstop and a card that
# eases in at a third of speed reads as the game hanging. It is one step of this
# node's own _process rather than a coroutine, which is the same thing said in
# the engine that is here.
const ARRIVE_DUR := 0.14
const ARRIVE_RISE := 18.0

var _arriving: UiRect = null
var _arrive_t := 0.0


func _arrive(go: UiRect) -> void:
	_arriving = go
	_arrive_t = 0.0
	_step_arrive(0.0)


func _step_arrive(dt: float) -> void:
	if _arriving == null:
		return
	_arrive_t += dt
	var k: float = clamp(_arrive_t / ARRIVE_DUR, 0.0, 1.0)
	var e := 1.0 - (1.0 - k) * (1.0 - k)          # out-quad: fast, then settles
	_arriving.modulate.a = e
	# the source writes anchoredPosition on a stretched rect, which moves both
	# offsets together; here that is both margins, and down is positive
	var d := (1.0 - e) * ARRIVE_RISE
	_arriving.margin_top = d
	_arriving.margin_bottom = d
	if k >= 1.0:
		_arriving = null


func _process(dt: float) -> void:
	# THE REAL CLOCK. Godot's `delta` here is already unscaled — Engine.time_scale
	# is untouched by this game, which bends time itself in TimeBend rather than
	# through the engine — so this is the wall clock the fade wants.
	_step_arrive(dt)
	_step_foot()


static func back() -> void:
	var me := i()
	if not me._stack.empty():
		me._stack.remove(me._stack.size() - 1)
	show(me._stack[me._stack.size() - 1] if not me._stack.empty() else null)


func _on_back(_a = null) -> void:
	back()


# ---- the back button --------------------------------------------------------
#
# THERE IS A STACK AND NOTHING WAS ROUTED TO IT.
#
# show() pushes, back() pops, and the only caller either had was a button
# labelled BACK. Android's back gesture arrives as a key press, and the one line
# reading it did this:
#
#     if (Escape && !_screenUp && S.lv != null) ShowPause();
#
# — so back opened the pause card while playing and did NOTHING on every menu in
# the game. The manual, the vault rack, calibrate, access, the Forge and its
# editor: a player swiping back on any of them got no response at all, which on
# Android does not read as "that is not a control here", it reads as the app
# having hung.
#
# THE ROOT IS THE ONE THAT NEEDS A DECISION. Popping the title leaves an empty
# stack, which is a turning cube with no interface on it and no way out — so the
# title is where back means LEAVE. It asks first: a single press says so on the
# footer, a second inside two seconds takes it, and anything else cancels.
# Everything in this game is saved, so an accidental exit costs nothing but the
# animation — but a game that closes on one stray gesture feels careless, and the
# prompt is one label that already exists on that screen.
#
# AND A WON CUBE HAS NO BACK. Popping the win card would drop the player onto the
# board they have just finished, with the collapse over and nothing to do on it.
# Back there means the same thing MENU does.

var _foot: Label
const FOOT_LINE := "DIAGNOSTIC BUILD · NO DEAD PIXELS"
var _leave_asked_at := -99.0
var _foot_dirty := false

# What a back press means, given what is on top of the stack.
enum BackAct {
	NONE,     # Nothing is up; the caller decides (it opens the pause card).
	POP,      # Pop one screen.
	MENU,     # A finished cube has no back — go to the front instead.
	ASK,      # The root. Offer to leave.
	LEAVE,    # Asked already, and recently. Close the application.
}


# THE DECISION, WITH NOTHING IN IT THAT NEEDS A CANVAS — so the harness can assert
# what every screen does with a back press without building one. Same reason
# Coach.next is a pure function.
static func decide_back(top, asking: bool) -> int:
	if top == null or top == "":
		return BackAct.NONE
	if top == "title":
		return BackAct.LEAVE if asking else BackAct.ASK
	if top == "win":
		return BackAct.MENU
	return BackAct.POP


# How long the offer to leave stands.
const LEAVE_WINDOW := 2.0


# Handle a back press against whatever is on screen.
# Returns true only when the caller should close the application.
static func back_pressed() -> bool:
	var me := i()
	var top = me._stack[me._stack.size() - 1] if not me._stack.empty() else null
	var now := OS.get_ticks_msec() / 1000.0
	var asking: bool = now - me._leave_asked_at < LEAVE_WINDOW

	match decide_back(top, asking):
		BackAct.LEAVE:
			return true
		BackAct.ASK:
			me._leave_asked_at = now
			if me._foot != null:
				me._foot.text = "PRESS BACK AGAIN TO LEAVE"
				me._foot_dirty = true
			return false
		BackAct.MENU:
			show_title()
			return false
		BackAct.POP:
			back()
			return false
		_:
			return false


# The offer expires on the real clock, because it is a real two seconds however
# slowly the game happens to be running.
func _step_foot() -> void:
	if not _foot_dirty:
		return
	if OS.get_ticks_msec() / 1000.0 - _leave_asked_at >= LEAVE_WINDOW:
		_foot_dirty = false
		if _foot != null:
			_foot.text = FOOT_LINE


static func column(parent: Control, top: float, bottom: float) -> UiRect:
	return UiKit.rect(parent, "col", Vector2(0, 0), Vector2(1, 1),
			Vector2(40, bottom), Vector2(-40, -top))


# One of the four quiet destinations under the primary.
#
# `across` is how many of them share a row: two, which is the two-by-two the
# held screen is composed around, or four, which is what a turned display with
# no height left has room for. Four in a row is not the better shape — a
# two-by-two reads as a pair of pairs and a row of four reads as a toolbar — it
# is what a hundred and sixteen units of missing height buys back on a twenty by
# nine phone held sideways.
func _quad(col: Control, idx: int, label: String, method: String,
		across: int = 2) -> UiButton:
	var w := 1.0 / across
	var x: float = idx % across
	var y: float = idx / across
	var top: float = -(UiKit.PRIMARY_H + 16.0 + y * (UiKit.BTN_H + 12.0))
	var r := UiKit.rect(col, label,
			Vector2(x * w, 1), Vector2((x + 1) * w, 1),
			Vector2(0 if x == 0 else 6, top - UiKit.BTN_H),
			Vector2(-6 if x == across - 1 else 0, top))
	var b := UiKit.bracketed(r, label, label, funcref(self, method), 24 if across == 2 else 20)
	# FOUR ACROSS IS A HUNDRED AND TWENTY-EIGHT UNITS EACH, and "[ CALIBRATE ]" is
	# a hundred and fifty-six at twenty. The row of four exists because the height
	# ran out; shrinking the one word that does not fit is cheaper than widening
	# the column, which comes straight off the machine beside it.
	if across > 2:
		UiKit.fit(b.label_node(), 14)
	return b


static func row(col: Control, slot: int, of: int, label: String,
		on_click: FuncRef, primary: bool = false) -> void:
	var h := 1.0 / of
	var r := UiKit.rect(col, label, Vector2(0, 1 - (slot + 1) * h), Vector2(1, 1 - slot * h),
			Vector2(0, 8), Vector2(0, -8))
	UiKit.bracketed(r, label, label, on_click, 28, primary)


# ---- title -----------------------------------------------------------------

var _play_label: Label
var _resume_name: Label
var _resume_vault: Label

# THE FOURTH DOOR ANSWERS TO WHO IS STANDING AT IT.
#
# The front door offered DAILY, VAULTS, FORGE and CALIBRATE, and the rules were
# behind none of them — the manual was reachable only from inside the settings
# screen, which nobody guesses. Meanwhile a level editor with a verify pass and
# share codes is the least useful thing in this game to somebody who has not yet
# finished a cube: it is a tool for making the thing they have not played.
#
# So for exactly as long as the save has never cleared anything, this slot is
# MANUAL. The first clear turns it into FORGE and it never turns back — the same
# reading of `reached` the coach migrates on, so "has never finished a cube" has
# one definition in this game rather than two.
var _make_quad: UiButton


# AND `reached` ALONE CANNOT ANSWER IT ANY MORE, which is the whole reason
# SaveData carries `runs`: "`reached` is how far along this run you are, `runs` is
# whether the machine has ever been finished at all, and ONLY THE SECOND ONE
# SHOULD CHANGE WHAT THE TITLE OFFERS." The C# states that rule where the field
# is declared and then tests `reached <= 1` here — so finishing the machine, which
# hands the ladder back at cube one, takes the Forge away from the one player who
# has certainly earned it and offers them the beginner's manual instead.
static func before_the_first_clear() -> bool:
	return never_cleared(Store.data().reached, Store.data().runs)


# Pure, so the harness can ask it about a save it does not have to build.
static func never_cleared(reached: int, runs: int) -> bool:
	return reached <= 1 and not Vaults.cleared(runs)


func _open_forge_or_manual(_a = null) -> void:
	if before_the_first_clear():
		show_manual()
	else:
		forge().call("open_shelf")


func _build_title() -> void:
	var l := layer("title")
	# A WINDOW, NOT A PLATE. Every other screen keeps the layer's scrim because
	# every other screen is words; this one has the board turning behind it and
	# the two stage ramps are already the only thing a word here needs. At 0.72
	# the cube reads at just over a quarter and might as well not be running.
	_tint(l, Color(0, 0, 0, 0))

	# TWO SHAPES, ONE SCREEN. Held, the masthead is over the machine and the five
	# controls are under it. Turned there is no height for that — the type and the
	# controls alone come to eight hundred units and a turned phone has six
	# hundred and twenty-five — and there is width to spare instead, so the words
	# and the controls go in a column down the right and the cube takes the rest.
	# See Layout.title_rect, which is the other half of the same decision.
	var b: UiButton
	if Layout.is_landscape():
		b = _title_turned(l)
	else:
		b = _title_held(l)

	_play_label = b.label_node()


func _title_held(l: UiRect) -> UiButton:
	UiKit.stage(l)
	UiKit.label(l, "wordmark1", "SINGULARITY", 66, Palette.INK, UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1), Vector2(0, -170), Vector2(0, -90))
	UiKit.label(l, "wordmark2", "ENGINE", 66, Palette.rust(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1), Vector2(0, -252), Vector2(0, -172))

	# WHERE YOU LEFT OFF, ON THE SCREEN THAT OFFERS TO TAKE YOU BACK. The play
	# button used to be a promise with no subject: it did not say which cube, which
	# vault, or what it would cost. Naming the thing a button is about is the
	# cheapest confidence a menu can buy.
	_resume_vault = UiKit.label(l, "resumeVault", "", 19, Palette.rust(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1), Vector2(0, -290), Vector2(0, -262))
	_resume_name = UiKit.label(l, "resume", "", 24, Palette.INK, UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1), Vector2(0, -322), Vector2(0, -292))

	# One pixel of rust, 634 units wide, 11 above and 12 below. It is the only rule
	# on the screen and it closes the masthead: everything above it is what the game
	# is called and where you are in it, everything below is the machine itself.
	#
	# THE PITCH IS GONE AND THE SPACE IT HAD IS NOT RECLAIMED. Three lines of prose
	# used to sit under this rule — "You are a black hole inside a broken machine…"
	# — and the ninety units they occupied are deliberately left empty rather than
	# closed up. The cube is turning behind this screen and it is the best argument
	# the title has; air between the masthead and the object is the composition, not
	# a hole where a paragraph used to be.
	UiKit.panel(l, "mastRule", Palette.rust(),
			Vector2(0.5, 1), Vector2(0.5, 1), Vector2(-317, -346), Vector2(317, -344))

	# ONE PRIMARY, THEN A GRID OF FOUR.
	#
	# Five equal rows is a list, and a list has no answer to "what do I press". The
	# thing you came here to do is a full-width solid; the four places you might go
	# instead are a quiet two-by-two under it. That shape also buys back most of the
	# height five stacked rows were spending, which is what leaves room for the cube
	# to be the biggest object on its own title screen.
	var col := UiKit.rect(l, "buttons", Vector2(0, 0), Vector2(1, 0),
			Vector2(48, 96), Vector2(-48, 452))

	var go := UiKit.rect(col, "CONTINUE", Vector2(0, 1), Vector2(1, 1),
			Vector2(0, -UiKit.PRIMARY_H), Vector2(0, 0))
	var b: UiButton = UiKit.bracketed(go, "CONTINUE", "CONTINUE", funcref(self, "_on_continue"), 30, true)

	_quad(col, 0, "DAILY", "_on_daily")
	_quad(col, 1, "VAULTS", "_on_vaults")
	_make_quad = _quad(col, 2, "FORGE", "_open_forge_or_manual")
	_quad(col, 3, "CALIBRATE", "_on_calibrate")

	_foot = UiKit.label(l, "foot", FOOT_LINE, 19, Palette.dim(), UiKit.Anchor.LOWER_CENTER,
			Vector2(0, 0), Vector2(1, 0), Vector2(0, 40), Vector2(0, 76))
	return b


# THE SAME FIVE THINGS AND THE SAME ORDER, STOOD IN A COLUMN.
#
# Masthead hung off the top of the column, controls off its bottom, and whatever
# height is left becomes air between them rather than being distributed — which
# is what the held screen does with the ninety units the pitch used to have, and
# for the same reason: the cube beside this is the best argument the title has
# and nothing should be crowding it.
#
# The wordmark comes down from sixty-six to forty-eight. Five hundred and sixty
# units is enough width for it at either size; it is the HEIGHT that is not, and
# a title that has to shed a control to keep its type big has its priorities the
# wrong way round.
const TITLE_PAD := 16.0
const TITLE_RULE_GAP := 12.0

# WHERE THE FOUR GO IN ONE ROW INSTEAD OF TWO.
#
# The masthead and the five controls come to five hundred and sixty-six units in
# the two-by-two shape. Sixteen by nine turned gives six hundred and one, which
# holds it; twenty by nine gives five hundred and thirty-three, which does not,
# and the hundred and sixteen units a second quad row costs is exactly the
# difference.
const TITLE_TWO_ROW_PLATE := 580.0


func _title_turned(l: UiRect) -> UiButton:
	var plate := Layout.plate_size().y
	var roomy: bool = plate >= TITLE_TWO_ROW_PLATE
	var across: int = 2 if roomy else 4
	var word: float = 56.0 if roomy else 46.0
	var word_pt: int = 46 if roomy else 38

	# TWO PLATES, NOT A STAGE.
	#
	# The held screen protects its type with two ramps — three hundred and
	# seventy-six units down from the top and four hundred and eighty-two up from
	# the floor, with the machine turning in the two hundred and seventy between
	# them. Turned, the plate is six hundred tall, so those two ramps come to more
	# than the whole screen: the cube was behind a sheet of black exactly as it
	# was in the C# before the ground came out, and for the same arithmetic
	# reason. It is not a bug in the ramps; a band measured off each end is a
	# portrait idea.
	#
	# So the turned screen says what it is instead: the machine in a window with
	# the aperture's own stroke around it, and the words on a framed plate beside
	# it. That is the language every other screen in this game is already written
	# in — framed plates, hairlines, one lit control — and it needs no gradient at
	# all, because the type is not over the cube any more. It is next to it.
	var win := UiKit.rect(l, "window", Vector2(0, 0), Vector2(1, 1),
			Vector2(0, Layout.RAIL_CLEAR),
			Vector2(-Layout.TITLE_COLUMN, -Layout.RAIL_CLEAR))
	UiKit.stroke(win, UiKit.edge())

	var col := UiKit.rect(l, "titleCol", Vector2(1, 0), Vector2(1, 1),
			Vector2(-Layout.TITLE_COLUMN, 0), Vector2(0, 0))
	var ru := Palette.rust()
	UiKit.framed(col, Palette.PANEL, Color(ru.r, ru.g, ru.b, 0.55))

	var y := -TITLE_PAD
	UiKit.label(col, "wordmark1", "SINGULARITY", word_pt, Palette.INK, UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1), Vector2(24, y - word), Vector2(-24, y))
	y -= word
	UiKit.label(col, "wordmark2", "ENGINE", word_pt, Palette.rust(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1), Vector2(24, y - word), Vector2(-24, y))
	y -= word + 6.0

	_resume_vault = UiKit.label(col, "resumeVault", "", 19, Palette.rust(),
			UiKit.Anchor.UPPER_CENTER, Vector2(0, 1), Vector2(1, 1),
			Vector2(24, y - 26.0), Vector2(-24, y))
	y -= 26.0
	_resume_name = UiKit.label(col, "resume", "", 24, Palette.INK,
			UiKit.Anchor.UPPER_CENTER, Vector2(0, 1), Vector2(1, 1),
			Vector2(24, y - 32.0), Vector2(-24, y))
	y -= 32.0 + TITLE_RULE_GAP

	UiKit.panel(col, "mastRule", Palette.rust(), Vector2(0, 1), Vector2(1, 1),
			Vector2(32, y - 2.0), Vector2(-32, y))

	# THE CONTROLS HANG OFF THE BOTTOM, and the stack is exactly as tall as what
	# _quad puts in it: the primary, sixteen of air, and one or two rows of the
	# four. Written as the same sum _quad measures from, so a change to either is
	# a change to one number.
	var rows: int = 4 / across
	var stack_h: float = UiKit.PRIMARY_H + 16.0 \
			+ rows * UiKit.BTN_H + (rows - 1) * 12.0
	var stack := UiKit.rect(col, "buttons", Vector2(0, 0), Vector2(1, 0),
			Vector2(24, TITLE_PAD), Vector2(-24, TITLE_PAD + stack_h))

	var go := UiKit.rect(stack, "CONTINUE", Vector2(0, 1), Vector2(1, 1),
			Vector2(0, -UiKit.PRIMARY_H), Vector2(0, 0))
	var b: UiButton = UiKit.bracketed(go, "CONTINUE", "CONTINUE",
			funcref(self, "_on_continue"), 30, true)

	_quad(stack, 0, "DAILY", "_on_daily", across)
	_quad(stack, 1, "VAULTS", "_on_vaults", across)
	_make_quad = _quad(stack, 2, "FORGE", "_open_forge_or_manual", across)
	_quad(stack, 3, "CALIBRATE", "_on_calibrate", across)

	# THE BUILD LINE GOES UNDER THE MACHINE, NOT UNDER THE CONTROLS. It is the
	# quietest thing on the screen and the column is the loudest column; the strip
	# of glass below the cube is where a plate number is stencilled on the real
	# object anyway.
	_foot = UiKit.label(l, "foot", FOOT_LINE, 19, Palette.dim(), UiKit.Anchor.LOWER_CENTER,
			Vector2(0, 0), Vector2(1, 0),
			Vector2(0, 18), Vector2(-Layout.TITLE_COLUMN, 50))
	return b


func _on_continue(_a = null) -> void:
	show(null)
	_dir.play(Vaults.resume(Store.data().reached))


func _on_daily(_a = null) -> void:
	show(null)
	_dir.play(Daily.SPEC_LEVEL, Session.LoadKind.DAILY)


func _on_vaults(_a = null) -> void:
	_open_vaults()


func _on_calibrate(_a = null) -> void:
	show_calibrate()


func _on_manual(_a = null) -> void:
	show_manual()


func _on_menu(_a = null) -> void:
	show_title()


static func show_title() -> void:
	var me := i()
	var r := Vaults.resume(Store.data().reached)
	var done := Vaults.cleared(Store.data().runs)
	# A machine that has never been started is INITIALISED, not continued.
	#
	# AND ONE THAT HAS BEEN FINISHED IS NEITHER. It used to read THE CORE, because
	# a finished save sat past the last cube and the button was offering it back.
	# Finishing relocks the ladder now and hands it over at cube one, so the button
	# is genuinely starting again — and AGAIN is the honest word for that, as well
	# as being the one the second chapter says. INITIALISE would be a lie to
	# somebody who has already been to the core.
	if me._play_label != null:
		me._play_label.text = "[ AGAIN ]" if (done and r <= 1) \
				else ("[ INITIALISE ]" if r <= 1 else "[ CONTINUE ]")
	if me._make_quad != null:
		UiKit.set_label(me._make_quad, "MANUAL" if before_the_first_clear() else "FORGE")

	# AND A RELOCKED LADDER SAYS SO, WHERE THE QUESTION IS ASKED.
	#
	# Finishing the machine sets `reached` back to one. Every best, every par and
	# every time survives — the rack still shows the pips you earned — but the
	# cubes are shut, and a player looking at a hundred and fifty locked cards with
	# their own scores printed on them has exactly one thought, and it is that the
	# game has eaten their save.
	#
	# The C# answers it nowhere. The button reads AGAIN, which is the right word
	# and is not an explanation, and the way back in is the eighth row of a
	# settings screen that only grows that row once you have finished. So the two
	# lines that name the cube you are returning to say what happened instead —
	# on the screen the player is standing on when they wonder, in the place their
	# eye is already going.
	if done and r <= 1:
		if me._resume_vault != null:
			me._resume_vault.text = "THE MACHINE RESET"
		if me._resume_name != null:
			me._resume_name.text = "EVERY BEST KEPT · UNSEAL IN CALIBRATE"
		me._stack.clear()
		me._leave_asked_at = -99.0
		me._foot_dirty = false
		if me._foot != null:
			me._foot.text = FOOT_LINE
		show("title")
		me._dir.show_attract()
		return

	var band := Vaults.vault_of(r)
	if me._resume_vault != null:
		me._resume_vault.text = "VAULT " + Vaults.roman_of(band) + ": " + Vaults.vault_name(band)
	if me._resume_name != null:
		# the cube's own name and what it costs, so the button has a subject
		var bl = Baked.try_at(r)
		var par: int = bl.par if bl != null else -1
		me._resume_name.text = Vaults.level_name(r) + ("  /  PAR " + str(par) if par >= 0 else "")
	me._stack.clear()
	me._leave_asked_at = -99.0
	me._foot_dirty = false
	if me._foot != null:
		me._foot.text = FOOT_LINE
	show("title")
	me._dir.show_attract()


# ---- vault select ----------------------------------------------------------

var _vault_name: Label
var _grid: UiRect
var _prev_btn: UiButton
var _next_btn: UiButton
var _jump_field: UiField
var _jump_msg: Label
var _jump_row: UiRect


func _build_vaults() -> void:
	var l := layer("vaults")
	solid_layer(l)

	# It says "VAULT I" here and "VAULT X · SINGULARITY" by the end, in a gap fixed
	# by the two arrows either side of it — so it shrinks to fit rather than sliding
	# underneath them. See UiKit.fit.
	# ONE ROW, SO THEY CANNOT DRIFT.
	#
	# The C# hangs the heading off both plate edges with a fixed inset and hangs
	# the two arrows off the same edges independently, then relies on the two sets
	# of numbers agreeing. They agree at the authored aspect and nowhere else: on a
	# taller phone the heading's box comes out sixty units into the right arrow,
	# and fit() cannot save it because fit() shrinks type to a BOX and the box is
	# the thing that is wrong. It had already shrunk to its eighteen-point floor.
	#
	# All three live in one row now and the middle is inset past both ends, so
	# "the name goes between the arrows" is what the anchors SAY rather than what
	# two independent sums happen to work out to. There is no aspect at which this
	# can overlap.
	# TURNED, THE RACK IS THE LEFT PANEL AND EVERYTHING ELSE IS A COLUMN.
	#
	# Held, this screen is a stack: which vault, the cards, the seed box, the way
	# out. Turned there are six hundred units of height for it and eleven hundred
	# of width, and the cards are what the width is for — a rack that has to be
	# three across on a phone can be six across turned, which is the difference
	# between scrolling a vault and seeing it.
	#
	# So the four things above and below the rack become four things beside it,
	# in the same order read down: the vault's name at the top, the seed box under
	# it, what the seed box said, and the way out at the foot.
	var turned := Layout.is_landscape()
	var side: UiRect = l
	if turned:
		side = UiKit.rect(l, "side", Vector2(1, 0), Vector2(1, 1),
				Vector2(-VAULT_COLUMN, 0), Vector2(0, 0))

	var head := UiKit.rect(side, "head", Vector2(0, 1), Vector2(1, 1),
			Vector2(40, -154), Vector2(-40, -66))

	var prev := UiKit.rect(head, "prev", Vector2(0, 0), Vector2(0, 1), Vector2(0, 0), Vector2(100, 0))
	_prev_btn = UiKit.bracketed(prev, "prev", "<", funcref(self, "_on_prev_vault"), 26)
	var nxt := UiKit.rect(head, "next", Vector2(1, 0), Vector2(1, 1), Vector2(-100, 0), Vector2(0, 0))
	_next_btn = UiKit.bracketed(nxt, "next", ">", funcref(self, "_on_next_vault"), 26)

	# TWO LINES, BECAUSE ONE OF THEM WILL NOT GO.
	#
	# "VAULT III · DISTANT NEIGHBOURS" is thirty characters. The gap between the
	# arrows is 262 units on a tall phone, which at the eighteen-point floor holds
	# twenty-four — so the C#'s single centred line cannot be made to fit by any
	# amount of shrinking, and fit() correctly bottoms out and lets it overflow
	# into both arrows. The box is not the problem either: widening it takes the
	# room the arrows need.
	#
	# They are two different facts anyway. The numeral is where you are in the
	# ladder and the name is what the chapter is called, and the interface already
	# sets them as two lines everywhere else it prints them — the title's resume
	# block, the pause card, the win card. This is the odd one out, and the middle
	# dot was there to join two things that never wanted joining.
	UiKit.label(head, "vaultNo", "VAULT I", 19, Palette.rust(), UiKit.Anchor.LOWER_CENTER,
			Vector2(0, 0), Vector2(1, 1), Vector2(108, 44), Vector2(-108, -4))
	_vault_name = UiKit.fit(
			UiKit.label(head, "vaultName", "THE COLLAPSE", 26, Palette.INK, UiKit.Anchor.UPPER_CENTER,
					Vector2(0, 0), Vector2(1, 1), Vector2(108, 2), Vector2(-108, -42)), 15)

	_grid = UiKit.rect(l, "grid", Vector2(0, 0), Vector2(1, 1),
			Vector2(RACK_INSET, RACK_FOOT_TURNED if turned else 300.0),
			Vector2(-(RACK_INSET + VAULT_COLUMN) if turned else -RACK_INSET,
					-(RACK_TOP_TURNED if turned else RACK_TOP)))

	# THE SEED BOX. Every generated cube in this game is a pure function of its
	# number, so the NUMBER IS THE SEED — a few characters that cut the same puzzle
	# on every device, forever. There has to be a way to type one.
	#
	# And typing one is a seed viewer, not a shortcut: a jump past `reached` records
	# nothing, unlocks nothing and posts nothing, or the whole progression would be
	# one text field wide.
	#
	# A FIXED FOOT, AND THE RACK TAKES WHAT IS LEFT. These used to follow the rack
	# — placed directly under however many rows _paint_vaults drew — which fixed one
	# hole by opening another: a ten-cube vault put the seed box at 724 and BACK at
	# 950, so a quarter of the screen was a single continuous black band with a link
	# floating at the bottom of it. Now the foot is where a foot goes and the CARDS
	# grow to meet it, which is the one thing on this screen that is worth more
	# space.
	#
	# AND THE TWO OFFSETS ARE THE RIGHT WAY ROUND HERE, which is the one place
	# this port does not copy the original literally. The C# writes offsetMin at
	# -JumpTop and offsetMax at -(JumpTop + 88) — offsetMax the MORE negative of
	# the two — which is a row eighty-eight units tall in the wrong direction, and
	# a rect with negative height lays its children out inside nothing. The seed
	# box and its message are not on the shipped vault screen at all. It is the
	# same slip the pause card carries a long note about, and UiRect exists to
	# make it impossible: it would catch and repair these two silently. They are
	# written correctly instead, so the guard stays a guard.
	var jt := jump_top()
	_jump_row = UiKit.rect(side, "jumpRow", Vector2(0, 1), Vector2(1, 1),
			Vector2(40, -(jt + Access.TAP_TARGET)), Vector2(-40, -jt))
	_jump_field = UiKit.field(_jump_row, "jump", "CUBE NUMBER",
			Vector2(0, 0), Vector2(0.72, 1), Vector2.ZERO, Vector2(-8, 0))
	var go_slot := UiKit.rect(_jump_row, "goSlot", Vector2(0.72, 0), Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	UiKit.bracketed(go_slot, "go", "GO", funcref(self, "_jump"), 24)

	_jump_msg = UiKit.label(side, "jumpMsg", "", 18, Palette.dim(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1),
			Vector2(40, -(jt + Access.TAP_TARGET + 42.0)),
			Vector2(-40, -(jt + Access.TAP_TARGET + 8.0)))

	# A LINK, NOT A PLATE. This screen's subject is the rack; BACK was a full-width
	# filled control at the bottom of it, which made the heaviest object on the
	# screen the one way OFF the screen. The house rule is already written down in
	# UiKit: one plate per screen, and everything else is a bracketed label with
	# nothing behind it.
	var back_rt := UiKit.rect(side, "back", Vector2(0, 0), Vector2(1, 0),
			Vector2(60, FOOT_FLOOR), Vector2(-60, FOOT_FLOOR + Access.TAP_TARGET))
	UiKit.link(back_rt, "back", "BACK", funcref(self, "_on_back"), 24, Palette.INK)


func _seek_label(nm: String) -> Label:
	var rt = _layers.get("vaults")
	if rt == null:
		return null
	var host = rt.find_node(nm, true, false)
	return host.get_node("text") as Label if host != null and host.has_node("text") else null


func _on_prev_vault(_a = null) -> void:
	_view_band = int(max(0, _view_band - 1))
	_paint_vaults()


func _on_next_vault(_a = null) -> void:
	_view_band = int(min(Vaults.LAST_BAND, _view_band + 1))
	_paint_vaults()


# HOW MANY COLUMNS, AND HOW BIG A CARD — one answer, computed from the vault's
# size and the space there is for it.
#
# THE SHAPE OF A CARD IS THE CONSTRAINT, NOT THE NUMBER OF THEM. A card carries a
# number over a name over three pips, and that stack stops working at both ends:
# past CARD_TALLEST it is a tower, under CARD_FLATTEST it is a bar in a chart with
# writing on it. Every column count that produces a card between those two is
# allowed, and the one with the MOST CARD wins — area, because all three things on
# it get easier as it grows.
#
# It falls out at three columns for the fifteen every vault holds, and the rack
# then fills the height it is given exactly. At twenty-five it falls out at five,
# which is what the constant this replaced was right about, for that size. At ten
# it is three again, four rows.
#
# Static and given its numbers rather than reading the layout, so the harness can
# ask it about sizes this game does not currently ship.
const CARD_TALLEST := 1.30
const CARD_FLATTEST := 0.62

# The column counts worth considering. Two is a list; seven is a keyboard.
const MIN_COLS := 3
const MAX_COLS := 6


# Returns [cols, w, h].
static func rack(size: int, grid_w: float, avail: float) -> Array:
	var best_cols := MAX_COLS
	var best_w := grid_w / MAX_COLS
	var best_h := 1.0
	var best_area := -1.0

	for cols in range(MIN_COLS, MAX_COLS + 1):
		var rows := int(ceil(size / float(cols)))
		if rows < 1:
			continue

		var w := grid_w / cols
		var h: float = min(w * CARD_TALLEST, avail / rows)
		if h < w * CARD_FLATTEST:
			continue                       # a bar, not a card

		var area := w * h
		if area <= best_area:
			continue
		best_cols = cols
		best_w = w
		best_h = h
		best_area = area

	# NOTHING QUALIFYING IS STILL AN ANSWER. A vault big enough that every column
	# count draws bars gets the most columns and whatever height is left, which is
	# cramped and legible — the alternative is a rack drawn off the bottom of the
	# screen.
	if best_area < 0.0:
		var rows: int = int(max(1, ceil(size / float(MAX_COLS))))
		best_h = min(best_w * CARD_TALLEST, avail / rows)
	return [best_cols, best_w, max(1.0, best_h)]


# Where the rack starts, and where the seed box that follows it does.
const RACK_TOP := 170.0

# The rack's margin either side of the plate.
const RACK_INSET := 40.0


# The rack's room, in canvas units: how wide the grid is and how much height there
# is between the heading and the seed box. Twenty-six of that height is air under
# the last row.
#
# It answers in REFERENCE units rather than from the live rect, because the
# harness asks it before any canvas has been laid out — and because the plate is
# the same 625 by 1127 on every display, which is the whole point of authoring
# against one. Returns [grid_w, avail].
# THE COLUMN BESIDE THE RACK, turned. Wide enough for "VAULT III" over "DISTANT
# NEIGHBOURS" between two arrows, which is the widest thing that goes in it.
const VAULT_COLUMN := 500.0

# Where the rack starts and stops turned: the same air the held screen leaves
# over its cards, and a foot rather than the three hundred units the seed box
# and the way out used to need under them.
const RACK_TOP_TURNED := 40.0
const RACK_FOOT_TURNED := 40.0


static func rack_space() -> Array:
	if Layout.is_landscape():
		var p := Layout.plate_size()
		return [p.x - VAULT_COLUMN - RACK_INSET * 2.0,
				p.y - RACK_TOP_TURNED - RACK_FOOT_TURNED]
	return [Layout.plate_width() - RACK_INSET * 2.0, jump_top() - RACK_TOP - 26.0]


# The seed box, measured up from the foot: BACK, twenty-four of air, the message
# line, eight, and then the box itself.
# HOW FAR DOWN THE SEED BOX SITS. Held that is measured from the plate's floor,
# past the way out; turned it is measured from the top of the column it is in,
# under the vault's name.
static func jump_top() -> float:
	if Layout.is_landscape():
		return 200.0
	return Layout.plate_height() - FOOT_FLOOR - Access.TAP_TARGET * 2.0 - 66.0


func _jump(_a = null) -> void:
	var raw := _jump_field.text().strip_edges()
	# Godot's LineEdit has no integer content type, so the field takes anything and
	# this is the gate — which is what the original's int.TryParse was anyway.
	if not raw.is_valid_integer() or int(raw) < 1:
		_jump_msg.text = "TYPE A CUBE NUMBER"
		_jump_msg.add_color_override("font_color", Palette.fault())
		return
	var level := int(raw)

	# The generator has no last cube, but a text field needs a ceiling: a
	# fat-fingered 99999999 is otherwise a minute of minting nobody asked for.
	if level > Vaults.MAX_CUBE:
		_jump_msg.text = "THE CATALOGUE STOPS AT " + str(Vaults.MAX_CUBE)
		_jump_msg.add_color_override("font_color", Palette.fault())
		return

	# ---- AND A CUBE YOU HAVE NOT REACHED IS SHUT ---------------------------
	#
	# This used to hand the cube over as PRACTICE — playable, with nothing recorded
	# — which meant the ladder had no lock on it at all: type any number and the
	# cube arrives. A gate that lets everybody through is a gate nobody notices, and
	# it made the relock after finishing meaningless, because everything was open
	# the whole time anyway.
	#
	# TWO REFUSALS, BECAUSE THEY ARE DIFFERENT FACTS. A cube nobody has reached is
	# ahead of the player. A cube they solved and cannot open is sealed — the
	# machine has taken it back — and the word says that without saying where the
	# key is, because finding the switch is the joke.
	if not Store.open(level):
		_jump_msg.text = ("CUBE " + str(level) + " IS SEALED") if Store.ever_cleared(level) \
				else ("CUBE " + str(level) + " IS NOT OPEN YET")
		_jump_msg.add_color_override("font_color", Palette.fault())
		return

	_jump_msg.text = ""
	show(null)
	_dir.play(level, Session.LoadKind.VAULT)


func _open_vaults() -> void:
	# Resume rather than reached, because a finished machine stores 501 and
	# vault_of would open the rack on a vault that does not exist.
	_view_band = Vaults.vault_of(Vaults.resume(Store.data().reached))
	_paint_vaults()
	show("vaults")


func _paint_vaults() -> void:
	# BOTH ENDS, ONCE, HERE — rather than trusting the two arrows to have clamped
	# themselves. This is the one function that reads _view_band, so it is the one
	# place that can promise the band is a real vault.
	_view_band = int(clamp(_view_band, 0, Vaults.LAST_BAND))
	if _prev_btn != null:
		_prev_btn.enabled = _view_band > 0
	if _next_btn != null:
		_next_btn.enabled = _view_band < Vaults.LAST_BAND

	# Removed before freed, so the count below is the new rack's and not the old
	# one's — queue_free alone leaves the child in the tree until the frame ends.
	for c in _grid.get_children():
		_grid.remove_child(c)
		c.queue_free()

	var head_no := _seek_label("vaultNo")
	if head_no != null:
		head_no.text = "VAULT " + Vaults.roman_of(_view_band)
	UiKit.set_fitted(_vault_name, Vaults.vault_name(_view_band))

	var start := Vaults.vault_start(_view_band)
	var size := Vaults.vault_size(_view_band)

	# THREE COLUMNS OF CARDS, NOT FIVE OF NUMBERS.
	#
	# A rack of bare numbers is unreadable twice over: the number printed on a
	# cleared cube was its BEST SCORE while the number on an unplayed one was its
	# LEVEL, so the same glyph meant two different things and neither was labelled.
	# And a cube in this game has a NAME — the vault list is the only place a player
	# ever sees it, and a name is what makes "the one with the plates" a thing you
	# can go back to.
	#
	# So each cube gets a card: its number, its name, and how well it went.
	#
	# AND THE RACK CHOOSES ITS OWN COLUMN COUNT, because a constant cannot. Five was
	# right for a vault of twenty-five and it is wrong for the fifteen every vault
	# holds: five across is three rows, the card is capped by its own shape at 142
	# units tall, and the rack ends two hundred units above the seed box. A quarter
	# of this screen was a black band with nothing in it. rack() computes it
	# instead, from the size of the vault and the space there is.
	#
	# The live width, with the authored one as the answer before the first layout
	# pass — they are the same number on every display, which is the point of
	# authoring against one plate.
	var grid_w: float = _grid.rect_size.x
	if grid_w <= 1.0:
		grid_w = rack_space()[0]
	# THE SPACE THE RACK HAS IS rack_space's ANSWER, not a second copy of it. The
	# height was written out here as the same three terms — the seed box's top less
	# the rack's own top less the air — which is true held and nonsense turned,
	# where the seed box is beside the rack rather than under it and its "top" is
	# two hundred units down a column. It came out at four units of card.
	var r: Array = rack(size, grid_w, rack_space()[1])
	var cols: int = r[0]
	var cell_h: float = r[2]

	for idx in range(size):
		var level := start + idx
		var cx := idx % cols
		var cy := idx / cols

		# THE LAST ROW IS CENTRED, NOT LEFT-ALIGNED.
		#
		# A vault of ten in three columns is three, three, three and then ONE CARD
		# alone against the left edge — which does not read as a tenth cube, it reads
		# as a layout that broke. Shifting the short row by half a cell per missing
		# card costs nothing and is the difference between a rack and an accident.
		var in_row: int = int(min(cols, size - cy * cols))
		var nudge := (cols - in_row) * 0.5 / cols

		var slot := UiKit.rect(_grid, "c" + str(level),
				Vector2(cx / float(cols) + nudge, 1.0), Vector2((cx + 1.0) / cols + nudge, 1.0),
				Vector2(6, -(cy + 1) * cell_h + 6), Vector2(-6, -cy * cell_h - 6))

		var reached := Store.open(level)
		var best = Store.try_best(level)
		var par = Store.try_par(level)
		var cleared: bool = best != null
		var ranked: bool = par != null and cleared

		# THREE PIPS, AND THEY ARE THE ONLY SCORE THIS SCREEN SHOWS. A raw fold count
		# means nothing without the par beside it; "how close to perfect" is the thing
		# a player actually wants off a grid, and it survives being three pixels tall.
		# A cube cleared before this build recorded pars has a best and no par to judge
		# it by. It gets one pip — cleared — rather than a guess, because inventing a
		# rating is worse than showing less.
		var pips := 0
		if cleared:
			if not ranked:
				pips = 1
			elif best <= par:
				pips = 3
			elif best <= par + 2:
				pips = 2
			else:
				pips = 1

		var arc := Palette.ARC
		var edge: Color = Palette.dim2() if not reached else (arc if pips == 3 else Palette.TRACE)

		var act := UiAct.on(slot, self, "_open_card", level)
		var btn := UiKit.bracketed(slot, "b", "", funcref(act, "press"), 1)

		# bracketed gives every control the same plate and edge; a card that is ABOUT
		# its state overrides both, and drops the empty label.
		var lbl := btn.label_node()
		if lbl != null:
			lbl.visible = false
		btn.plate_node().modulate = Color(arc.r, arc.g, arc.b, 0.16) if pips == 3 else Palette.PANEL
		btn.edge_node().modulate = edge

		UiKit.label(btn, "n", str(level), 30,
				Palette.INK if reached else Palette.dim(), UiKit.Anchor.LOWER_CENTER,
				Vector2(0, 0.56), Vector2(1, 0.94), Vector2.ZERO, Vector2.ZERO)

		# THE NAME WRAPS TO TWO LINES, AT FIFTEEN, AND THAT IS MEASURED.
		#
		# A card is ninety-seven units wide inside its inset. The longest name this
		# game prints is NOTHING UNDERNEATH — eighteen characters, a hundred and
		# eighty-four units at seventeen point, which on one line runs out through both
		# sides of the card. So it wraps; but at seventeen the wrap does not save it
		# either, because TURNED INSIDE OUT breaks into THREE lines at that width and
		# sixty-seven units of text will not go into a forty-unit band.
		#
		# Fifteen is the largest size at which every name this game can produce comes
		# out at two lines or fewer. fit() stays as the backstop, because a name is
		# DATA: it is the thing that catches a name added later that this arithmetic
		# never saw. Truncating is not an option when the game has cubes whose names
		# differ only in the last word.
		if reached:
			UiKit.fit(UiKit.label(btn, "name", Vaults.level_name(level), 15, Palette.dim(),
					UiKit.Anchor.UPPER_CENTER,
					Vector2(0, 0.16), Vector2(1, 0.52), Vector2.ZERO, Vector2.ZERO, true), 12)

		for p in range(3):
			UiKit.icon(btn, "sqfill" if p < pips else "sq",
					arc if p < pips else Palette.dim2(), 9.0,
					Vector2(0.5, 0.09), Vector2((p - 1) * 13.0, 0.0))


func _open_card(level: int) -> void:
	# A LOCKED CARD IS INERT, not a door to a practice run. It is already drawn in
	# dim2 and it already reads as unavailable; opening the cube anyway was the
	# screen contradicting itself.
	if not Store.open(level):
		return
	show(null)
	_dir.play(level, Session.LoadKind.VAULT)


# ---- the manual ------------------------------------------------------------

# The running y of the manual's flow layout, which the C# keeps in a local that
# its nested functions close over.
var _man_y := 0.0
var _man_m: UiRect

# WHICH HALF OF THE PAGE THE CURSOR IS IN, and how tall the other half got.
#
# Held, the manual is one column that scrolls. Turned, a page of nine rules in
# one column shows one and a half of them at a time, and the reader who came here
# to check one thing has to hunt for it — so it breaks into two, at the seam the
# writing already has: the rule and the verbs on the left, the objects and what
# is worth knowing on the right. It still scrolls if it has to; on every landscape
# shape measured it does not have to.
var _man_x0 := 0.0
var _man_x1 := 1.0
var _man_tall := 0.0


func _build_manual() -> void:
	var l := layer("manual")
	solid_layer(l)
	# A SCREEN IS NAMED AFTER THE CONTROL THAT OPENS IT. This was CALIBRATION and
	# the settings screen is CALIBRATE — two headings one word apart, and the button
	# that opens this one says MANUAL, so the player had three names for two screens.
	# THE HEADING AND THE WAY OUT BOTH COME IN WHEN THE DEVICE TURNS, because the
	# page between them is the screen and there are five hundred and seventy-five
	# units of plate rather than eleven hundred and twenty-eight. A hundred and
	# thirty of air above a title is composition on a phone and a third of the
	# manual on a tablet.
	var turned := Layout.is_landscape()
	UiKit.label(l, "h", "MANUAL", 32 if turned else 40, Palette.INK, UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1),
			Vector2(0, -70 if turned else -130), Vector2(0, -28 if turned else -80))

	# The title stays put and the rules scroll under it, so the heading is always
	# there to say what you are reading.
	# The page stops above the button, and the button is a finger tall — see the
	# note on "got" below.
	var sc := UiKit.scroll(l, "scroll", Vector2(0, 0), Vector2(1, 1),
			Vector2(0, 128 if turned else 196), Vector2(0, -80 if turned else -150))
	_man_m = sc.content

	# NINE LINES, NOT NINE PARAGRAPHS — AND A PICTURE FOR EACH.
	#
	# Nobody reads a manual in a puzzle game. They glance at it, twice, and go back
	# to the cube. So every rule is a label you can take in with one saccade, a line
	# under it, and a DIAGRAM beside it — because the four things this page has to
	# explain are all spatial, and a sentence about a fold is a worse description of
	# a fold than two squares and an arrow.
	_man_y = 24.0
	_man_x0 = 0.0
	_man_x1 = 0.5 if turned else 1.0
	_man_tall = 0.0

	_kicker("THE RULE")
	# NO AUTHORED BREAKS IN A BODY. They were put in against a wider plate and
	# against a label that could not wrap, and now that it can they only force a
	# short line in the middle of a good one. The heights below are what a body of
	# three or four wrapped lines needs; the page scrolls, so the cost of the taller
	# ones is nothing.
	var a1 := _entry("ONE FACE AT A TIME",
			"Two traces that line up when you fold are connected, however far apart they look.", 104.0)
	# two decks, and the fold that brings them together
	_mark(a1, "sq", Palette.TRACE, 34, -22, 28)
	_mark(a1, "sq", Palette.TRACE, 34, -22, -28)
	_mark(a1, "arrow", Palette.rust(), 26, 26, 0)

	_kicker("THE VERBS")
	var a2 := _entry("SWIPE TO FOLD",
			"Past halfway it commits. The ticks say which folds have footing.")
	_mark(a2, "sq", Palette.TRACE, 30, 0, 0)
	_mark(a2, "arrow", Palette.rust(), 20, 0, 34)
	_spin_mark(_mark(a2, "arrow", Palette.rust(), 20, 0, -34), 180.0)
	_spin_mark(_mark(a2, "arrow", Palette.rust(), 20, -34, 0), 90.0)
	_spin_mark(_mark(a2, "arrow", Palette.rust(), 20, 34, 0), -90.0)

	var a3 := _entry("TAP TO MOVE",
			"The singularity walks to any trace connected to the one under it.")
	# a column of three, and you on the surface of it
	_mark(a3, "sq", Palette.TRACE, 30, 0, 32)
	_mark(a3, "sq", Palette.TRACE, 30, 0, 0)
	_mark(a3, "sq", Palette.TRACE, 30, 0, -32)
	_mark(a3, "hole", Palette.VOID, 15, 0, 32)
	_mark(a3, "ring", Palette.ARC, 17, 0, 32)

	var a4 := _entry("COLLAPSE INTO THE CORE", "Reach it and the vault is solved.", 62.0)
	_mark(a4, "core", Palette.core(), 52, 0, 0)

	# THE SEAM. Everything above is the rule and the two verbs; everything below is
	# what is in the lattice and what is worth knowing about it. Turned, that is
	# where the page folds.
	_man_break()

	_kicker("THE OBJECTS")
	var cards := UiKit.rect(_man_m, "cards", _man_a0(), _man_a1(),
			Vector2(44, -(_man_y + 92)), Vector2(-44, -_man_y))
	_card(cards, 0, "sqfill", Palette.TRACE, "TRACE", "CIRCUIT")
	_card(cards, 1, "node", Palette.NODE, "NODE", "COLLECT")
	_card(cards, 2, "lock", Palette.LOCK, "LOCK", "BARRIER")
	_card(cards, 3, "plate", Palette.rust(), "PLATE", "INVERTS")
	_card(cards, 4, "evert", Palette.EVERT, "EVERTER", "FAR SIDE")
	_man_y += 106.0

	_kicker("WORTH KNOWING")
	var a5 := _entry("HOLD FOR MATRIX",
			"The lattice goes to glass. Keep holding and drag to turn it.")
	# the solid, seen through — three faces of one box
	var arc := Palette.ARC
	_mark(a5, "sq", arc, 44, -8, -6)
	_mark(a5, "sq", Color(arc.r, arc.g, arc.b, 0.45), 44, 12, 12)
	_mark(a5, "sqfill", Palette.TRACE, 14, -14, -12)

	# THREE LINES, NOT FOUR — the gap under this entry was the complaint. At four
	# lines the box has to be 124 tall while its diagram gutter is only 96, so
	# twenty-eight units of empty column sat between this and TO GO and read as a
	# hole in the page.
	var a6 := _entry("PLATES INVERT",
			"Every trace goes dead and every dead cell lights up for five seconds. A plate is always footing.", 102.0)
	_mark(a6, "sqfill", Palette.TRACE, 26, -16, 14)
	_mark(a6, "sq", Palette.dim2(), 26, 14, 14)
	_mark(a6, "sq", Palette.dim2(), 26, -16, -16)
	_mark(a6, "sqfill", Palette.TRACE, 26, 14, -16)

	var a7 := _entry("TO GO", "The fewest folds still possible from where you stand.", 82.0)
	_mark(a7, "i.togo", arc, 44, 0, 0)

	# THE LAST RULE, AND IT ARRIVES LAST ON PURPOSE. Vault IX is where the ladder
	# stops getting harder, so it is where a new idea has to land — and the four
	# cubes there teach it without a word. This entry is for the player who came
	# back to the manual afterwards to check they had understood, which is the only
	# thing a manual is good for.
	var a8 := _entry("STAND ON AN EVERTER",
			"The engine turns inside out. Every column shows its far side instead of its near one — the same solid, the other way round.", 124.0)
	_mark(a8, "evert", Palette.EVERT, 40, -14, 10)
	_mark(a8, "sq", Palette.TRACE, 24, 16, -12)

	# THE COLOPHON, AND WHY A PUZZLE GAME HAS ONE.
	#
	# The typeface is licensed under the SIL OFL, which asks that the copyright
	# notice and the licence travel with the font wherever it goes. The licence text
	# itself does travel — it is beside the .ttf in assets/, so it is inside the
	# shipped player and not only in the repository. This is the part a person can
	# find.
	#
	# Gated on the font having actually loaded: if the import failed and the game is
	# drawing in the builtin, crediting JetBrains Mono for what you are looking at
	# would simply be false. It also means this line is a live check on the import —
	# no credit, no font.
	if UiKit.has_mono():
		_kicker("THE FACE")
		UiKit.label(_man_m, "colophon",
				"JetBrains Mono, by the JetBrains Mono Project Authors, under the SIL Open Font License 1.1. The licence ships with the game.",
				17, Palette.dim(), UiKit.Anchor.UPPER_LEFT,
				_man_a0(), _man_a1(),
				Vector2(44, -(_man_y + 76)), Vector2(-44, -_man_y), true)
		_man_y += 76.0

	# The page is as tall as its TALLER column, not as tall as the cursor — which
	# is the second one, and shorter than the first on some shapes.
	sc.end_scroll(max(_man_tall, _man_y) + 24.0)

	# EIGHTY-EIGHT, NOT SEVENTY. This is the single primary on the screen and the
	# only way off it, and the C# builds it seventy units tall — eighteen under the
	# target every other control in the game is measured against, on the button
	# that gets pressed most often on the page it is on.
	var got := UiKit.rect(l, "got",
			Vector2(0.25 if turned else 0.0, 0), Vector2(0.75 if turned else 1.0, 0),
			Vector2(60, 22 if turned else 90), Vector2(-60, (22 if turned else 90) + 88))
	UiKit.bracketed(got, "got", "GOT IT", funcref(self, "_on_back"), 28, true)


# Move the cursor to the second column, if there is one. Called at the seam.
func _man_break() -> void:
	if not Layout.is_landscape():
		return
	_man_tall = max(_man_tall, _man_y)
	_man_x0 = 0.5
	_man_x1 = 1.0
	_man_y = 24.0


func _man_a0() -> Vector2:
	return Vector2(_man_x0, 1)


func _man_a1() -> Vector2:
	return Vector2(_man_x1, 1)


func _kicker(s: String) -> void:
	UiKit.label(_man_m, s, s, 19, Palette.rust(), UiKit.Anchor.LOWER_LEFT,
			_man_a0(), _man_a1(), Vector2(44, -(_man_y + 24)), Vector2(-44, -_man_y))
	_man_y += 28


# THE GUTTER THE DIAGRAMS SHARE, and how much taller a wrapped body gets when
# the page is two columns wide instead of one.
#
# A half-width column is five hundred and forty units, less the gutter, less the
# margin — three hundred and fifty for the text, against five hundred and ten
# held. So every body wraps to about one more line than it was authored for, and
# the box heights, which were measured against the held wrap, come out short by
# exactly that line. The gutter gives back what it can and the box takes the
# rest.
const MAN_GUT := 190.0
const MAN_GUT_TURNED := 150.0
const MAN_TALLER := 1.34


func _man_gut() -> float:
	return MAN_GUT_TURNED if Layout.is_landscape() else MAN_GUT


func _entry(head: String, body: String, h0: float = 88.0) -> UiRect:
	var h: float = round(h0 * MAN_TALLER) if Layout.is_landscape() else h0
	var gut := _man_gut()
	UiKit.label(_man_m, head, head, 23, Palette.INK, UiKit.Anchor.UPPER_LEFT,
			_man_a0(), _man_a1(), Vector2(44, -(_man_y + 30)), Vector2(-gut, -_man_y))
	# WRAPPED, because it is a sentence. The line breaks in these strings were
	# authored against a plate that was thirty-nine units wider than the one the
	# case leaves, and every one of them that no longer fits used to run out over
	# the bezel and lose its last word.
	UiKit.label(_man_m, head + "_d", body, 17, Palette.dim(), UiKit.Anchor.UPPER_LEFT,
			_man_a0(), _man_a1(), Vector2(44, -(_man_y + h)), Vector2(-gut, -(_man_y + 32)), true)

	# the diagram lives in a fixed gutter on the right, so every picture on the page
	# shares one optical column
	var art := UiKit.rect(_man_m, head + "_art", _man_a1(), _man_a1(),
			Vector2(-(gut - 14.0), -(_man_y + 96)), Vector2(-44, -(_man_y + 4)))
	_man_y += h + 16.0
	return art


static func _mark(art: Control, role: String, c: Color, size: float, x: float, v: float) -> TextureRect:
	return UiKit.icon(art, role, c, size, Vector2(0.5, 0.5), Vector2(x, v))


static func _spin_mark(img: TextureRect, deg: float) -> void:
	var rt := img.get_parent() as UiRect
	rt.set_pivot(Vector2(0.5, 0.5))
	rt.rect_rotation = -deg


# One of the five object cards: the mark, its name, and its job.
static func _card(row_rt: Control, idx: int, glyph: String, col: Color, nm: String, job: String) -> void:
	var c := UiKit.rect(row_rt, nm, Vector2(idx / 5.0, 0), Vector2((idx + 1) / 5.0, 1),
			Vector2(5, 0), Vector2(-5, 0))
	var r := Palette.rust()
	UiKit.framed(c, Palette.PANEL_HI, Color(r.r, r.g, r.b, 0.5))
	UiKit.icon(c, glyph, col, 34, Vector2(0.5, 1.0), Vector2(0, -32))
	UiKit.label(c, "n", nm, 19, Palette.INK, UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 0), Vector2(1, 0), Vector2(0, 26), Vector2(0, 48))
	UiKit.label(c, "j", job, 17, Palette.dim(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 0), Vector2(1, 0), Vector2(0, 8), Vector2(0, 26))


static func show_manual() -> void:
	show("manual")


# ---- pause -----------------------------------------------------------------

var _pause_name: Label
var _pause_where: Label

# The running y of the pause card's upward stack, which the C# keeps in a local.
var _up_y := 0.0


# THE PAUSE AND WIN CARDS ARE THE SAME OBJECT SEEN TWICE, and they turn the same
# way: what cube this is on the left, what you can do about it on the right.
#
# Held they are one column — the name at the top, the actions stacked up from the
# foot, and the board showing through the middle. That stack is three hundred and
# sixty-four units and the head is two hundred and sixty; a turned plate has five
# hundred and seventy-five, so one of the two has to go somewhere else. The
# actions go beside, which is also where the thumb already is.
const CARD_COLUMN := 520.0

# Where the actions go on the card being built: the plate held, a column turned.
var _card_side: Control = null


# The two halves of a card. Returns [body, side]; they are the same rect held.
func _card_halves(l: UiRect) -> Array:
	if not Layout.is_landscape():
		return [l, l]
	var body := UiKit.rect(l, "body", Vector2(0, 0), Vector2(1, 1),
			Vector2(0, 0), Vector2(-CARD_COLUMN, 0))
	var side := UiKit.rect(l, "side", Vector2(1, 0), Vector2(1, 1),
			Vector2(-CARD_COLUMN, 0), Vector2(0, 0))
	return [body, side]


func _build_pause() -> void:
	var l := layer("pause")
	# Dark enough that the board is a memory rather than a distraction. The pause
	# card is still the one screen that lets the puzzle through — you are in the
	# middle of it — but at 0.72 the cube was legible straight through the words,
	# which is not "letting it through", it is clutter.
	_tint(l, Color(0, 0, 0, 0.90))

	# THE CARD IS ABOUT THE CUBE YOU ARE IN, NOT ABOUT BEING PAUSED.
	#
	# "PAUSED" over six identical bars tells you one thing you already know and then
	# asks you to read six labels to find the one that says go back. Naming the cube
	# and its vault costs nothing and turns the card into a place; RESUME takes the
	# only plate, the three ways to a different board share a row, and the two
	# settings screens are text.
	#
	# AND IT IS A SCREEN, NOT A LUMP IN THE MIDDLE OF ONE. Every one of these was
	# anchored to the plate's CENTRE, so the whole card occupied 420 units of an
	# eleven-hundred-unit plate with three hundred and fifty of nothing above it and
	# the same below. Two holes.
	#
	# It takes the win card's shape now, because they are the same object seen
	# twice: what cube this is at the top, what you can do about it stacked UP from
	# the foot, and the space between them in one piece rather than two. That space
	# is not empty either — this is the one card that lets the board through, and
	# what shows in the middle of it is the puzzle you are about to go back to.
	# THE FLOOR IS THE ONE THE DISPLAY GAVE, not the authored one.
	#
	# _up measures DOWN from the top of the plate, so the number it starts from is
	# the plate's height — and Layout.plate_height is the reference eleven hundred
	# and twenty-eight, which is right on the phone this was composed against and
	# wrong on every other. On a 20:9 phone the card floated a hundred and thirty
	# units above the floor; turned, the whole card — RESUME, the three ways to
	# another board, both links — was drawn five hundred units below the plate and
	# clipped out of existence. The screen could only be left by solving the cube,
	# which is the same failure UiRect's own note describes, arrived at from the
	# other direction.
	var turned := Layout.is_landscape()
	var halves := _card_halves(l)
	var body: Control = halves[0]
	_card_side = halves[1]
	var head: float = 138.0 if turned else 206.0
	_pause_name = UiKit.label(body, "h", "", 34 if turned else 40, Palette.INK,
			UiKit.Anchor.LOWER_CENTER, Vector2(0, 1), Vector2(1, 1),
			Vector2(40, -head), Vector2(-40, -(head - 56.0)))
	_pause_where = UiKit.label(body, "where", "", 17, Palette.rust(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1),
			Vector2(40, -(head + 38.0)), Vector2(-40, -(head + 8.0)))

	_up_y = Layout.plate_size().y - (24.0 if turned else FOOT_FLOOR)

	# TWO LINKS, AND THE RULES ARE THE ONE THAT MATTERS.
	#
	# This was CALIBRATE alone, and the manual was reachable only from inside it —
	# the front door offers DAILY, VAULTS, FORGE and CALIBRATE, so a player wanting
	# to know what a plate does had to guess that the rules live behind the settings.
	# Nobody guesses that.
	#
	# The pause card is where somebody goes when they are stuck, which is exactly
	# when a reference is worth having, so this is the right two taps: MENU, then
	# MANUAL.
	var links := _up(_card_side, "links", Access.TAP_TARGET, 18.0)
	var man_slot := UiKit.rect(links, "m", Vector2(0, 0), Vector2(0.5, 1), Vector2.ZERO, Vector2.ZERO)
	UiKit.link(man_slot, "manual", "MANUAL", funcref(self, "_on_manual"), 20)
	var cal_slot := UiKit.rect(links, "c", Vector2(0.5, 0), Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	UiKit.link(cal_slot, "calibrate", "CALIBRATE", funcref(self, "_on_calibrate"), 20)

	var three := _up(_card_side, "three", Access.TAP_TARGET, 26.0)
	_third(three, 0, "RESET", "_on_reset")
	_third(three, 1, "VAULTS", "_on_vaults")
	_third(three, 2, "MENU", "_on_menu")

	var go := _up(_card_side, "resume", UiKit.PRIMARY_H, 0.0)
	UiKit.bracketed(go, "resume", "RESUME", funcref(self, "_on_resume"), 28, true)


# MEASURED DOWN FROM THE TOP, WHICH MEANS THE BOTTOM EDGE IS THE MORE NEGATIVE OF
# THE TWO. offsetMin is the bottom-left corner and offsetMax the top-right;
# writing them the other way round gives every row a height of MINUS eighty-eight,
# and a rect with negative height lays its children out inside nothing. That is
# what happened here — the card kept its name and its vault line, which are
# anchored normally, and lost RESUME, RESET, VAULTS, MENU and CALIBRATE entirely,
# leaving a screen that could only be left by solving the cube. See UiRect, which
# now shouts about it rather than drawing nothing.
func _up(l: Control, nm: String, h: float, gap: float) -> UiRect:
	var pad: float = 24.0 if Layout.is_landscape() else 72.0
	var r := UiKit.rect(l, nm, Vector2(0, 1), Vector2(1, 1),
			Vector2(pad, -_up_y), Vector2(-pad, -(_up_y - h)))
	_up_y -= h + gap
	return r


func _on_resume(_a = null) -> void:
	show(null)


func _on_reset(_a = null) -> void:
	show(null)
	_dir.play(_dir.s.level_no, _dir.s.kind, _dir.s.made_key)


static func show_pause(dir) -> void:
	var me := i()
	var sn: Session = dir.s
	var band := Vaults.vault_of(sn.level_no)
	me._pause_name.text = (sn.lv.name if sn.lv.name != "" else "MADE CUBE") if sn.is_made() \
			else ("DAILY " + Daily.day_label() if sn.is_daily() else Vaults.level_name(sn.level_no))
	me._pause_where.text = ("PAR " + str(sn.lv.par)) if sn.is_daily() \
			else ("VAULT " + Vaults.roman_of(band) + " / " + Vaults.vault_name(band)
					+ " / PAR " + str(sn.lv.par))
	show("pause")


# ---- the plate, taught once ------------------------------------------------
#
# Shown the first time a cube carries one, and never again. A mechanic this large
# should not arrive as a toast, and it should not arrive as a manual page either —
# it arrives when the thing is on screen, with the thing itself waiting behind the
# card.

# One row of the lesson is a name and a body under it: thirty units of heading,
# two of air, and seventy-eight of wrapped body.
const PLATE_BODY := 110.0

const PLATE_ROWS := [
	"THE LATTICE INVERTS|Every trace goes dead. Every dead cell lights up. Nothing moves — only what carries current.",
	"IT LASTS FIVE SECONDS|Then the lattice springs back and puts you on the nearest plate. Running out costs you the walk, never the vault.",
	"TAP IT AGAIN TO PRESS IT AGAIN|A plate is a door between two versions of the same engine, and the core is usually only reachable in one of them.",
	"IT ALWAYS GIVES FOOTING|Cut clean through the lattice, visible from every face. While you stand on a plate every fold is legal.",
]


func _build_plate_teach() -> void:
	var l := layer("plate")
	solid_layer(l)
	var head_up: float = 60.0 if Layout.is_landscape() else 0.0
	UiKit.label(l, "eyebrow", "A NEW COMPONENT", 20, Palette.dim(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1),
			Vector2(0, -130 + head_up), Vector2(0, -100 + head_up))
	UiKit.label(l, "h", "PLATES", 44, Palette.rust(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1),
			Vector2(0, -190 + head_up), Vector2(0, -140 + head_up))
	# TWO LINES, BECAUSE THE FACE IS A MONOSPACE. Fifty-six characters at twenty
	# points across the plate is one line in a proportional face and two in this one
	# — every glyph is 0.6em, and lowercase is where that costs. The box is as tall
	# as the sentence actually needs.
	#
	# AND THE SAME FORTY-EIGHT OF MARGIN THE ROWS UNDER IT HAVE. It had none because
	# it had never wrapped: a single centred line sat well inside the plate on its
	# own. Two lines fill the measure, and at zero margin the second one would have
	# run to the literal edge of the glass with the bezel starting at the next pixel.
	UiKit.label(l, "sub", "Everything you know how to do is about to be worth less.",
			20, Palette.dim(), UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1),
			Vector2(48, -272 + head_up), Vector2(-48, -200 + head_up), true)

	# NO AUTHORED BREAKS IN A BODY. Exactly the change the manual got, for exactly
	# the reason, and this screen is where it actually bit: the breaks were placed
	# where a proportional face ran out of plate, so under a monospace each one
	# forced a short line AND the wrap that was going to happen anyway.
	#
	# Three wrapped lines at nineteen points is a hair over seventy-five units of
	# type, so the body box is seventy-eight and the row pitch is a hundred and
	# thirty. Four rows from -290 put the last body's floor at -790, and the STEP ON
	# IT button's ceiling is at -967 — the column still ends well clear of it.
	# FOUR ROWS DOWN, OR TWO BY TWO.
	#
	# Four rows at a hundred and thirty from -290 put the last one's floor at -790,
	# which is what a portrait plate has. Turned there are six hundred units and
	# the header takes a hundred and ninety of them, so the four go two and two —
	# the same rows in the same order, read down the first column and then down
	# the second, and each half as wide, which is why the bodies are given the
	# wrap they already had.
	var turned := Layout.is_landscape()
	var cols: int = 2 if turned else 1
	var per: int = int(ceil(PLATE_ROWS.size() / float(cols)))
	var top: float = -196.0 if turned else -290.0
	var foot: float = 20.0 if turned else 90.0

	# THE PITCH IS WHAT IS LEFT DIVIDED BY WHAT IS IN IT, not a number.
	#
	# A hundred and thirty is right for a portrait plate and for a turned one at
	# sixteen by nine. At twenty by nine there are sixty-eight fewer units of
	# height and the second row's body printed through STEP ON IT. The rows are
	# a hundred and ten of type each; the pitch is the air between them, and air
	# is the thing there is a variable amount of.
	var free: float = Layout.plate_size().y + top \
			- (foot + Access.TAP_TARGET + 16.0) - PLATE_BODY
	var pitch: float = min(130.0, free / max(1, per - 1)) if per > 1 else 130.0
	var w := 1.0 / cols
	var pad := 24.0 if turned else 48.0

	for i in range(PLATE_ROWS.size()):
		var p: PoolStringArray = PLATE_ROWS[i].split("|")
		var col: int = i / per
		var y: float = top - (i % per) * pitch
		var x0 := col * w
		var x1 := (col + 1) * w
		UiKit.label(l, p[0], p[0], 23, Palette.INK, UiKit.Anchor.UPPER_LEFT,
				Vector2(x0, 1), Vector2(x1, 1),
				Vector2(pad + (24.0 if col > 0 else 0.0), y - 30),
				Vector2(-pad, y))
		UiKit.label(l, p[0] + "_d", p[1], 19, Palette.dim(), UiKit.Anchor.UPPER_LEFT,
				Vector2(x0, 1), Vector2(x1, 1),
				Vector2(pad + (24.0 if col > 0 else 0.0), y - 110),
				Vector2(-pad, y - 32), true)

	# a finger tall, for the same reason GOT IT is — held, the last body's floor
	# is at -790 and this reaches -178; turned, the last is at -566 and this
	# reaches -108 out of six hundred
	var go := UiKit.rect(l, "go", Vector2(0.25 if turned else 0.0, 0), Vector2(0.75 if turned else 1.0, 0),
			Vector2(60, foot), Vector2(-60, foot + Access.TAP_TARGET))
	UiKit.bracketed(go, "go", "STEP ON IT", funcref(self, "_on_resume"), 28, true)


static func show_plate_teach() -> void:
	show("plate")


# ---- the word after a chapter ----------------------------------------------
#
# ONE WORD ON A BLACK SCREEN, TYPED. Nothing else is on it — no heading, no
# chapter number, no button. A caption would make it a card and a button would
# make it a step; it is neither, it is the machine saying something and then not
# being there any more.
#
# It is also the only screen in the game with no way out drawn on it, which is
# deliberate and is why it must never need one: it leaves on its own after a beat,
# and a touch anywhere takes it early. See Chapters.

var _chapter_word: Label
var _chapter_full := ""
var _chapter_t := 0.0
var _chapter_then: FuncRef = null
var _touched := false


func _build_chapter() -> void:
	var l := layer("chapter")
	solid_layer(l)

	# CENTRED ON THE PLATE, not on the aperture. There is no board behind this and
	# nothing to sit beside, so the word goes in the middle of the screen the way a
	# word alone in the dark should.
	_chapter_word = UiKit.label(l, "word", "", 52, Palette.INK, UiKit.Anchor.MIDDLE_CENTER,
			Vector2(0, 0.5), Vector2(1, 0.5), Vector2(60, -70), Vector2(-60, 70))


# A TOUCH ANYWHERE. The chapter screen draws no control, so there is nothing for a
# button to catch — this is the whole hit test, and it is only armed while the
# word is up.
func _input(event: InputEvent) -> void:
	if _chapter_full.length() > 0 and UiKit.any_press(event):
		_touched = true


# Put the word up and call `then` when it has been read — by the clock or by a
# touch. The caller does not need to know which.
static func show_chapter_word(word: String, then: FuncRef) -> void:
	var me := i()
	me._chapter_full = word
	me._chapter_then = then
	me._chapter_t = 0.0
	me._touched = false
	me._chapter_word.text = ""
	show("chapter")


static func chapter_up() -> bool:
	return i()._chapter_full.length() > 0


# Pumped from GameDirector's own step on the UNSCALED clock, because the bent
# clock belongs to the board and there is no board here.
static func tick_chapter(dt: float) -> void:
	var me := i()
	if not chapter_up():
		return
	me._chapter_t += dt

	var chars: int = int(clamp(
			floor((me._chapter_t - Chapters.LEAD) / Chapters.PER_CHAR),
			0, me._chapter_full.length()))
	me._chapter_word.text = me._chapter_full.substr(0, chars)

	var touched: bool = me._chapter_t > Chapters.DEAF_FOR and me._touched
	if touched or me._chapter_t >= Chapters.seconds(me._chapter_full):
		me._close_chapter()


func _close_chapter() -> void:
	var then := _chapter_then
	_chapter_full = ""
	_chapter_then = null
	_touched = false
	show(null)
	if then != null and then.is_valid():
		then.call_func()


# ---- calibrate --------------------------------------------------------------
#
# YOU CALIBRATE AN INSTRUMENT; YOU READ A MANUAL. These were the wrong way round
# in an early build: the settings lived unlabelled inside the pause card while the
# button called CALIBRATE opened the how-to, so a player looking for brightness
# pressed CALIBRATE and got a rulebook.

var _cal_row := 0
var _acc_row := 0
var _unlock_row: UiRect


# THE HEADING OF A SETTINGS SCREEN, AND — TURNED — THE THREE WAYS OFF IT.
#
# Held, this is two centred lines with a hundred and forty units of air over
# them, and the exits are a band at the other end of the plate. Turned, both are
# one bar: the two lines set flush left, the three exits flush right, and the
# panel gets everything under it. See PANEL_BAR.
func _settings_head(l: UiRect, head: String, sub: String) -> void:
	if not Layout.is_landscape():
		UiKit.label(l, "h", head, 40, Palette.INK, UiKit.Anchor.UPPER_CENTER,
				Vector2(0, 1), Vector2(1, 1), Vector2(0, -140), Vector2(0, -90))
		UiKit.label(l, "sub", sub, 20, Palette.dim(), UiKit.Anchor.UPPER_CENTER,
				Vector2(0, 1), Vector2(1, 1), Vector2(0, -176), Vector2(0, -146))
		var feet := UiKit.rect(l, "feet", Vector2(0, 0), Vector2(1, 0),
				Vector2(40, FOOT_FLOOR), Vector2(-40, FOOT_FLOOR + Access.TAP_TARGET))
		_third(feet, 0, "MANUAL", "_on_manual")
		_third(feet, 1, "BACK", "_on_back")
		_third(feet, 2, "MENU", "_on_menu")
		return

	var bar := UiKit.rect(l, "bar", Vector2(0, 1), Vector2(1, 1),
			Vector2(28, -PANEL_BAR), Vector2(-28, 0))
	UiKit.label(bar, "h", head, 32, Palette.INK, UiKit.Anchor.LOWER_LEFT,
			Vector2(0, 0.44), Vector2(0.5, 1), Vector2(6, 0), Vector2.ZERO)
	UiKit.label(bar, "sub", sub, 18, Palette.dim(), UiKit.Anchor.UPPER_LEFT,
			Vector2(0, 0), Vector2(0.5, 0.44), Vector2(8, 0), Vector2.ZERO)

	var exits := UiKit.rect(bar, "feet", Vector2(1, 0), Vector2(1, 1),
			Vector2(-PANEL_EXIT_W, 0), Vector2(0, 0))
	_third(exits, 0, "MANUAL", "_on_manual")
	_third(exits, 1, "BACK", "_on_back")
	_third(exits, 2, "MENU", "_on_menu")


func _build_calibrate() -> void:
	var l := layer("calibrate")
	solid_layer(l)
	_settings_head(l, "CALIBRATE", "TUNE THE INSTRUMENT")

	# ONE PANEL, EIGHT ROWS, EACH WITH ITS OWN MARK.
	#
	# These were eight labels floating on black with [ON] buttons and plus and minus
	# steppers beside them. Two things were wrong with that. A stepper is a fine way
	# to change a number by one and a poor way to answer "how bright, out of how
	# bright it goes" — brightness is only ever judged against the ends of its range,
	# so it wants a POSITION, not a reading. And a row of eight items that differ
	# only in their wording has to be read; a row where each carries a mark is
	# recognised, which is what you want on the screen somebody opens to change one
	# thing they already had in mind.
	var panel := UiKit.rect(l, "panel", Vector2(0, 0), Vector2(1, 1),
			Vector2(28, panel_floor()), Vector2(-28, -panel_top()))
	var r := Palette.rust()
	UiKit.framed(panel, Palette.PANEL, Color(r.r, r.g, r.b, 0.55))

	_cal_row = 0
	_toggle(panel, "i.sound", "SOUND", "", "sound", "_after_sound")
	# THE ON SWITCH IS THE BOTTOM OF THE SLIDER, and there is no reason for both.
	# This panel carried a HAPTICS toggle and a HAPTIC FEEDBACK strength slider two
	# rows apart, which is two controls for one setting — and Haptics has always
	# read them as one anyway: it returns early on `haptic == 0 || buzz <= 0`, so
	# zero on the slider was already off.
	#
	# THE EFFECTS SWITCH WAS TWO SETTINGS WEARING ONE COAT, and both of them belong
	# on the screen that is about being able to play at all rather than on the one
	# about taste. What is left here is a door to it, which is also how somebody
	# finds a screen they did not know the game had.
	_cal_link(panel, "i.fx", "ACCESS", "MOTION · LIGHT · CONTRAST · WORDS", "_on_access")
	_toggle(panel, "i.togo", "FOLDS TO GO", "CYAN: PERFECT · RUST: YOU SLIPPED", "togo", "")
	_toggle(panel, "i.depth", "DEPTH READOUT", "DISTANCE PRINTED ON EVERY CELL", "depth", "")

	# Reading through the old flag rather than migrating the save: anybody who had
	# haptics switched off has haptic 0 and a strength they set before that, and the
	# slider must show them OFF rather than the number it would buzz at if they
	# turned it back on. See _read_setting.
	_bar(panel, "i.haptic", "HAPTICS", "0 IS OFF", 0, 150, "haptic", "")
	# Both default to 100, and at 100 no filter is applied at all — the cost of these
	# two is zero for anyone who leaves them alone.
	_bar(panel, "i.bright", "BRIGHTNESS", "", 60, 160, "bright", "")
	_bar(panel, "i.contrast", "CONTRAST", "", 70, 150, "contrast", "")

	# ---- and the way out of the soft lock, LAST -----------------------------
	#
	# IT IS NOT DRAWN UNTIL THERE IS SOMETHING TO UNLOCK. On a first run the whole
	# ladder is ahead of the player and a switch offering to open it is offering to
	# spoil the thing they just started.
	#
	# AND IT IS THE LAST ROW BECAUSE IT IS THE HIDDEN ONE. CAL_ROWS is the divisor
	# for every row's height, so the list is eight tall whether or not this one is
	# drawn; a hidden row in the MIDDLE would be a hole between DEPTH READOUT and
	# HAPTICS, and a hidden row at the bottom is a little more air above the feet,
	# which nobody reads as missing.
	_unlock_row = _toggle(panel, "i.togo", "UNSEAL CUBES", "EVERYTHING YOU HAVE SOLVED",
			"unlocked", "")



# EIGHT, AND THE COMMENT THAT USED TO BE HERE WAS RIGHT.
#
# It said: "it is the divisor for the row height, so it is not a comment about the
# list — it IS the list, and a row added below without changing it draws on top of
# the last one." UNSEAL CUBES was added and this was left at seven, so CONTRAST
# drew through the feet — on a screen where the bottom row is a slider somebody is
# meant to drag.
#
# So it is not written down twice any more. _cal_row counts what it lays out and
# the harness holds the result against the panel it has to fit in and against the
# tap target every row owes a finger.
const CAL_ROWS := 8

# HOW MUCH OF THE RIGHT-HAND END A ROW'S CONTROL OWNS, plus eight of air. The
# widest is the OPEN link at 124 from the edge; a switch is 108 and the three
# stops of a segmented control are a third of the row. A hint may run up to here
# and no further, and because it is a distance from the same edge the control is
# measured from, it is the same answer at every aspect.
const HINT_CLEAR := 132.0
const HINT_NARROW := 300.0

# HOW SMALL A HINT MAY GET BEFORE IT IS ALLOWED TO OVERFLOW INSTEAD.
#
# Thirteen held. Turned it is eleven, and that is not a relaxation of the rule —
# it is the same rule read in the right units. A canvas unit is a constant
# FRACTION of the display, so eleven units on the ten-inch panel a two-column
# settings list implies is physically bigger type than thirteen on a phone. What
# changed is the box: two columns of a four-by-three plate are four hundred and
# twenty units wide, and "MOTION · LIGHT · CONTRAST · WORDS" is two hundred and
# sixty-four at thirteen.
static func hint_floor() -> int:
	return 11 if Layout.is_landscape() else 13


# WHERE THE THREE WAYS OFF A SETTINGS SCREEN SIT, and how much air is above them.
#
# CALIBRATE and ACCESS both put their panel's floor at 190 and their feet at 90,
# which leaves TWELVE UNITS between a bordered panel and a row of framed buttons —
# near enough to touching that the feet read as a fourth row of the panel that had
# fallen off the bottom of it, while ninety units of nothing sat underneath. The
# gap and the margin have swapped most of the way round.
const FOOT_FLOOR := 62.0

# EIGHT ROWS OF NINETY-TWO, WHICH IS WHAT EIGHT CONTROLS OF EIGHTY-EIGHT NEED.
#
# The C# divides whatever is left between the sub-heading and the feet into eight
# and then puts an eighty-eight unit control in the middle of each — and what is
# left is 720, so each row is ninety, each row's own two units of inset take it
# to eighty-six, and every control on both settings panels overflows its row by a
# unit at each end. It is invisible at the authored size and it is the reason the
# rows collide outright on a plate any shorter.
#
# Sixteen units bought back — six from the air above the feet and ten from the
# gap under the sub-heading, neither of which is doing anything with them — makes
# the panel 736, the row 92, and the control fit inside it with air to spare.
const FOOT_GAP := 52.0
const PANEL_TOP := 190.0


# THE SAME PANEL, TURNED, IS TWO LISTS SIDE BY SIDE.
#
# Eight rows of ninety-two want seven hundred and thirty-six units of height and
# a turned phone's plate has six hundred and twenty-five, of which the heading
# and the feet were taking three hundred and ninety-two. Four rows want three
# hundred and sixty-eight, which fits with room to spare — and the width the
# second column needs is width that was going to be air.
#
# The heading and the feet come in too, because a landscape screen has no height
# to spend on a hundred and forty units of air over a title.
# TURNED, THE HEADING AND THE THREE WAYS OUT SHARE ONE BAR ACROSS THE TOP.
#
# Held, they are at opposite ends of the plate — a hundred and ninety units of
# title over the panel and two hundred and two of feet under it — and there are
# eleven hundred and twenty-eight units of height to spend on that. Turned there
# are six hundred, or five hundred and thirty on the twenty by nine phones that
# are exactly the ones somebody turns sideways, and three hundred and ninety-two
# of it cannot go on chrome: eight settings in two columns need four rows of a
# tap target and there is no arrangement of the rest that leaves room for them.
#
# So the title goes to the left of one bar and the three exits to the right of
# it, which costs a hundred instead of three hundred and ninety-two and reads as
# the header of an instrument rather than as a page. The panel keeps the FULL
# width, which is what keeps every hint inside its own row: two columns of five
# hundred is a shrink the fit can absorb, and three columns of three hundred and
# seventy is not.
const PANEL_BAR := 88.0
const PANEL_BAR_GAP := 12.0
const PANEL_FLOOR_TURNED := 12.0

# How much of the bar the three exits take. Three bracketed labels at a hundred
# and fifty each, plus the air between them.
const PANEL_EXIT_W := 480.0

# The least a settings row may be and still hold a control a finger can hit: the
# tap target and its two units of inset at each end.
const PANEL_ROW_MIN := Access.TAP_TARGET + 4.0

# The gutter between the two columns. Eighteen is what a row already leaves to
# the right of its own control, so this is the pair of them.
const PANEL_GUTTER := 18.0


static func panel_top() -> float:
	return PANEL_BAR + PANEL_BAR_GAP if Layout.is_landscape() else PANEL_TOP


static func panel_floor() -> float:
	if Layout.is_landscape():
		return PANEL_FLOOR_TURNED
	return FOOT_FLOOR + Access.TAP_TARGET + FOOT_GAP


# WHERE ONE ROW OF A SETTINGS PANEL GOES, and whether it owes a hairline under
# itself. Both panels ask; neither knows which orientation it is in.
#
# HALF A TURNED PANEL IS AS WIDE AS A WHOLE HELD ONE, which is why nothing inside
# a row had to change. Eleven hundred and twenty-nine units across a turned plate,
# halved and less the gutter, is five hundred and fifty-five; the held panel is
# five hundred and sixty-nine. Every offset in a row — the hint's clearance, the
# switch's hundred and eight, the slider's track — is measured against the width
# it was authored against either way.
#
# Filled down the first column and then down the second, which is the order the
# rows are written in and the order a settings list is read in.
static func panel_slot(panel: Control, nm: String, slot: int, of: int) -> Array:
	var cols := panel_columns(of)
	var rows: int = int(ceil(float(of) / cols))
	var col: int = int(slot / rows)
	var idx: int = slot % rows
	var w := 1.0 / cols
	var hh := 1.0 / rows
	var half := PANEL_GUTTER * 0.5
	var r := UiKit.rect(panel, nm,
			Vector2(col * w, 1 - (idx + 1) * hh), Vector2((col + 1) * w, 1 - idx * hh),
			Vector2(0.0 if col == 0 else half, 2),
			Vector2(0.0 if col == cols - 1 else -half, -2))
	return [r, idx < rows - 1 and slot + 1 < of]


# HOW MANY COLUMNS THE PANEL BREAKS INTO, WHICH IS A QUESTION ABOUT THE DISPLAY
# AND NOT ABOUT TASTE.
#
# Held, one: the plate is eleven hundred and twenty-eight units tall and eight
# rows of ninety-two fit down it with the heading and the feet.
#
# Turned, the height is whatever the aspect left. Sixteen by nine gives six
# hundred and one units of plate and four rows fit; twenty by nine — which is
# most phones, and exactly the ones somebody turns sideways — gives five hundred
# and thirty-three and they do not. So the count is derived from the height, the
# same way the vault rack derives its own: take the fewest columns whose rows
# still clear the tap target, and stop at three because a fourth column of a
# settings list is a spreadsheet.
static func panel_columns(of: int) -> int:
	if not Layout.is_landscape():
		return 1
	var avail: float = Layout.plate_size().y - panel_top() - panel_floor()
	for c in range(2, 4):
		if ceil(float(of) / c) * PANEL_ROW_MIN <= avail:
			return c
	return 3


# ONE OF THREE ACROSS, AND THE WORD SHRINKS RATHER THAN LEANING ON ITS NEIGHBOUR.
#
# A third of a plate is two hundred units on the phone this was composed against
# and a hundred and five on a 20:9 phone in portrait, where the canvas comes out
# 652 wide rather than 720 — and "[ VAULTS ]" is a hundred and forty-four at
# twenty-four points. The three of these are always the same three words at the
# same size on the same row, so the one that does not fit is the one that gives.
func _third(parent: Control, idx: int, label: String, method: String) -> void:
	var slot := UiKit.rect(parent, label, Vector2(idx / 3.0, 0), Vector2((idx + 1) / 3.0, 1),
			Vector2(6, 0), Vector2(-6, 0))
	var b := UiKit.bracketed(slot, label, label, funcref(self, method), 24)
	UiKit.fit(b.label_node(), 15)


func _on_access(_a = null) -> void:
	show_access()


# A row of the calibrate panel: mark, name, what it does, and the control.
func _cal_rt(panel: Control, icon: String, label: String, hint: String, wide_hint: bool) -> UiRect:
	var slot := _cal_row
	_cal_row += 1
	var seat: Array = panel_slot(panel, label, slot, CAL_ROWS)
	var r: UiRect = seat[0]

	UiKit.icon(r, icon, Palette.rust(), 26, Vector2(0, 0.5), Vector2(34, 0))
	UiKit.label(r, "l", label, 21, Palette.INK, UiKit.Anchor.MIDDLE_LEFT,
			Vector2(0, 0.0 if hint == "" else 0.40), Vector2(0.62, 1), Vector2(58, 0), Vector2.ZERO)
	if hint != "":
		# MEASURED FROM THE CONTROL, NOT AS A FRACTION OF THE ROW. A switch is 108
		# units of the right-hand end and the OPEN link is 124; a hint that runs to
		# 0.72 of the row clears both at the authored aspect and walks into them on a
		# taller phone, where 0.72 of a wider row is further right and the control is
		# still the same distance from the edge. HINT_CLEAR is that distance plus air.
		# and it SHRINKS rather than running on. Every hint on this panel is a fixed
		# string in a box that is narrower on a taller phone; without a floor to fall
		# to, the longest of them prints its last word over the control beside it.
		UiKit.fit(UiKit.label(r, "h", hint, 17, Palette.dim(), UiKit.Anchor.MIDDLE_LEFT,
				Vector2(0, 0), Vector2(1, 0.40),
				Vector2(58, 0), Vector2(-(HINT_CLEAR if wide_hint else HINT_NARROW), 0)), hint_floor())

	# a hairline under every row but the last, so eight rows read as one instrument
	# rather than eight unrelated controls
	if seat[1]:
		var ru := Palette.rust()
		UiKit.panel(r, "rule", Color(ru.r, ru.g, ru.b, 0.20),
				Vector2(0, 0), Vector2(1, 0), Vector2(20, 0), Vector2(-20, 1))
	return r


func _toggle(panel: Control, icon: String, label: String, hint: String,
		field: String, after: String) -> UiRect:
	var r := _cal_rt(panel, icon, label, hint, true)
	var act := UiAct.on(r, self, "_flip", [field, after])
	UiKit.toggle(r, "sw", _read_setting(field) != 0, funcref(act, "pass_through"),
			Vector2(1, 0.5), Vector2(1, 0.5),
			Vector2(-108, -Access.TAP_TARGET * 0.5), Vector2(-18, Access.TAP_TARGET * 0.5))
	return r


func _bar(panel: Control, icon: String, label: String, hint: String,
		lo: int, hi: int, field: String, after: String) -> void:
	# The reading sits AFTER the hint rather than on top of it. Both were pinned to
	# the same left edge of the same band, so "120%" was printed straight through the
	# word INTENSITY.
	#
	# ONE COLUMN FOR THE READING, ON EVERY ROW. It was anchored into the hint's band,
	# so a row WITH a hint printed the number under the label and a row without one
	# printed it beside — three sliders, two different layouts, and the odd one out
	# was the only row that also had a second word under its name. It now sits in its
	# own column, right-aligned hard against the track, so the three readings line up
	# with each other whatever the row above them says.
	var r := _cal_rt(panel, icon, label, hint, false)
	var now := _read_setting(field)
	var val := UiKit.label(r, "v", str(now) + "%", 21, Palette.rust(), UiKit.Anchor.MIDDLE_RIGHT,
			Vector2(0.30, 0), Vector2(0.50, 1.0), Vector2.ZERO, Vector2(-10, 0))
	var act := UiAct.on(r, self, "_slide", [field, after, val])
	UiKit.bar(r, "bar", lo, hi, now, funcref(act, "pass_through"),
			Vector2(0.52, 0.5), Vector2(1, 0.5),
			Vector2(0, -Access.TAP_TARGET * 0.5), Vector2(-18, Access.TAP_TARGET * 0.5))


# A calibrate row whose control is a way somewhere else.
#
# A BRACKETED LABEL, NOT A PLATE. This was a full framed button a hundred and
# forty units wide and a tap target tall, which made the one row on the panel that
# goes nowhere by itself the heaviest thing on the screen — and it sat on top of
# its own hint, so the row that says what is behind the door was the row you could
# not read. The kit's own note on link() says why the brackets are enough.
func _cal_link(panel: Control, icon: String, label: String, hint: String, method: String) -> void:
	var r := _cal_rt(panel, icon, label, hint, true)
	var slot := UiKit.rect(r, "go", Vector2(1, 0.5), Vector2(1, 0.5),
			Vector2(-124, -Access.TAP_TARGET * 0.5), Vector2(-18, Access.TAP_TARGET * 0.5))
	UiKit.link(slot, "open", "OPEN", funcref(self, method), 20, Palette.INK)


static func show_calibrate() -> void:
	var me := i()
	if me._unlock_row != null:
		me._unlock_row.visible = Vaults.cleared(Store.data().runs)
	show("calibrate")


# ---- the settings, as two tables -------------------------------------------
#
# THE LAMBDAS, WRITTEN DOWN. Every get/set pair on these two panels reads and
# writes one field of the save, so the pair is a field NAME — and the four rows
# that also do something to the running game name a method beside it. That is the
# whole of what the closures carried.
#
# HAPTICS IS THE ONE BRANCH, and it is a branch in the C# too: the row is a single
# slider standing in for a switch and a strength that the save records separately,
# so zero on the track has to mean the flag as well as the number.

func _read_setting(field: String) -> int:
	var d := Store.data()
	if field == "haptic":
		return 0 if d.haptic == 0 else d.buzz
	return d.get(field)


func _write_setting(field: String, v: int) -> void:
	var d := Store.data()
	if field == "haptic":
		d.buzz = v
		d.haptic = 1 if v > 0 else 0
	else:
		d.set(field, v)


# A switch, pressed. Returns the state it settled in, which is what UiSwitch paints.
func _flip(bind: Array, _wanted = null) -> bool:
	var field: String = bind[0]
	var after: String = bind[1]
	var v: int = 0 if _read_setting(field) != 0 else 1
	_write_setting(field, v)
	Store.save()
	if after != "":
		call(after)
	return v != 0


# A slider, dragged.
func _slide(bind: Array, v: int) -> void:
	var field: String = bind[0]
	var after: String = bind[1]
	_write_setting(field, v)
	Store.save()
	if bind.size() > 2 and bind[2] != null:
		(bind[2] as Label).text = str(v) + "%"
	if after != "":
		call(after)


func _after_sound() -> void:
	if Store.data().sound == 0:
		_dir.sfx.stop_ambience()
	else:
		_dir.sfx.ambience(Vaults.vault_of(_dir.s.level_no))


func _after_light() -> void:
	_dir.glow.refresh()


# THE ONE SETTING THAT REPAINTS THE GAME UNDERNEATH ITSELF, which is exactly why
# it is worth doing in place: the row you just pressed is still under your thumb,
# and the contrast you asked for is a thing you can now see on the screen you
# asked for it on.
func _after_legible() -> void:
	UiKit.repaint()
	_dir.view.push_palette()
	_dir.filter.refresh()
	Scanlines.refresh()   # the rows go
	Glass.refresh()       # and the grease on the screen with them


# apply_faders is what makes these two faders audible WHILE THEY MOVE. A bus's
# volume is what everything is already passing through, so the bed follows the
# ROOM slider under your thumb rather than waiting for the next sound to be
# played.
func _after_faders() -> void:
	_dir.sfx.apply_faders()


func _after_assist() -> void:
	_dir.hud.refresh_assist()


# ---- access ----------------------------------------------------------------
#
# THE SCREEN THIS GAME NEEDED AND DID NOT HAVE.
#
# Everything here answers a question of the form "can this player take delivery of
# what the game is saying", which is a different question from the one CALIBRATE
# answers — that one is about taste and about the room you are sitting in. Keeping
# them apart is not tidiness: a player looking for the setting that stops the
# screen shaking should not have to go through eight rows about brightness to find
# out whether it exists.
#
# Three sections, one per sense, in the order the grades are stated: what you can
# see, what you can hear, what your hand has to do.

const ACC_ROWS := 8

# The three stops start at 0.58 of the row; a hint must stop short of that, and a
# fraction of the row is what fails on a taller phone. 260 is the widest the stops
# and their air have ever been.
const STEPS_CLEAR := 260.0


func _build_access() -> void:
	var l := layer("access")
	solid_layer(l)
	_settings_head(l, "ACCESS", "SIGHT · SOUND · HAND")

	var panel := UiKit.rect(l, "panel", Vector2(0, 0), Vector2(1, 1),
			Vector2(28, panel_floor()), Vector2(-28, -panel_top()))
	var r := Palette.rust()
	UiKit.framed(panel, Palette.PANEL, Color(r.r, r.g, r.b, 0.55))

	_acc_row = 0

	# ---- sight ---------------------------------------------------------------
	_steps(panel, "i.fx", "MOTION", "SHAKE · ZOOM · CLOCK",
			["FULL", "LESS", "STILL"], "motion", "")
	_steps(panel, "i.fx", "LIGHT", "SPARKS · BLOOM · FLASH",
			["FULL", "LESS", "NONE"], "light", "_after_light")
	_acc_toggle(panel, "i.bright", "LEGIBILITY", "EVERY LABEL AT 7:1", "legible", "_after_legible")

	# ---- sound ---------------------------------------------------------------
	_acc_toggle(panel, "i.sound", "CAPTIONS", "EVERY CUE, IN WORDS", "captions", "")
	_acc_bar(panel, "i.sound", "INSTRUMENT", "", 0, 150, "vol", "_after_faders")
	_acc_bar(panel, "i.sound", "ROOM", "", 0, 150, "room", "_after_faders")

	# ---- hand ----------------------------------------------------------------
	_acc_toggle(panel, "i.haptic", "FOLD BUTTONS", "FOUR ARROWS · NO SWIPE NEEDED",
			"assist", "_after_assist")
	_acc_toggle(panel, "i.depth", "DEPTH READOUT", "DISTANCE PRINTED ON EVERY CELL", "depth", "")



static func show_access() -> void:
	show("access")


# A row of the access panel.
#
# `hint_to` IS NOT A TASTE, IT IS THE CONTROL'S SHADOW. A switch is ninety units
# of the right-hand end and a hint can run most of the way to it; three named
# stops are a third of the row, and a hint written for the switch rows walked
# straight under the first of them — MOTION and LIGHT both had their last term
# printed behind [FULL]. The row that owns the widest control is the row that has
# to say so.
func _acc_rt(panel: Control, icon: String, label: String, hint: String,
		hint_to: float = HINT_CLEAR) -> UiRect:
	var slot := _acc_row
	_acc_row += 1
	var seat: Array = panel_slot(panel, label, slot, ACC_ROWS)
	var r: UiRect = seat[0]

	UiKit.icon(r, icon, Palette.rust(), 26, Vector2(0, 0.5), Vector2(34, 0))
	UiKit.label(r, "l", label, 21, Palette.INK, UiKit.Anchor.MIDDLE_LEFT,
			Vector2(0, 0.0 if hint == "" else 0.40), Vector2(0.55, 1), Vector2(58, 0), Vector2.ZERO)
	if hint != "":
		UiKit.fit(UiKit.label(r, "h", hint, 17, Palette.dim(), UiKit.Anchor.MIDDLE_LEFT,
				Vector2(0, 0), Vector2(1, 0.40), Vector2(58, 0), Vector2(-hint_to, 0)), hint_floor())

	if seat[1]:
		var ru := Palette.rust()
		UiKit.panel(r, "rule", Color(ru.r, ru.g, ru.b, 0.20),
				Vector2(0, 0), Vector2(1, 0), Vector2(20, 0), Vector2(-20, 1))
	return r


func _acc_toggle(panel: Control, icon: String, label: String, hint: String,
		field: String, after: String) -> void:
	var r := _acc_rt(panel, icon, label, hint)
	var act := UiAct.on(r, self, "_flip", [field, after])
	UiKit.toggle(r, "sw", _read_setting(field) != 0, funcref(act, "pass_through"),
			Vector2(1, 0.5), Vector2(1, 0.5),
			Vector2(-108, -Access.TAP_TARGET * 0.5), Vector2(-18, Access.TAP_TARGET * 0.5))


func _acc_bar(panel: Control, icon: String, label: String, hint: String,
		lo: int, hi: int, field: String, after: String) -> void:
	var r := _acc_rt(panel, icon, label, hint)
	var now := _read_setting(field)
	var val := UiKit.label(r, "v", str(now) + "%", 21, Palette.rust(), UiKit.Anchor.MIDDLE_RIGHT,
			Vector2(0.30, 0), Vector2(0.50, 1.0), Vector2.ZERO, Vector2(-10, 0))
	var act := UiAct.on(r, self, "_slide", [field, after, val])
	UiKit.bar(r, "bar", lo, hi, now, funcref(act, "pass_through"),
			Vector2(0.52, 0.5), Vector2(1, 0.5),
			Vector2(0, -Access.TAP_TARGET * 0.5), Vector2(-18, Access.TAP_TARGET * 0.5))


# THREE STATES IS NOT A SWITCH AND IT IS NOT A SLIDER.
#
# A switch can only say two things, and this setting has three because the middle
# one is the honest answer for most people — a slider would invite a player to hunt
# for a number when there are only three answers and each is named. So: three
# labelled stops, the current one lit the way the primary is lit, and the whole row
# is a target.
#
# TWO UNITS SHORT, WHICH WAS HALF THE BUG. The hint ran to 0.50 of the row and the
# three stops began at 0.52, which fitted the proportional face this was measured
# against with room to spare. In the mono, SPARKS · BLOOM · FLASH is 224.4 units
# against 226.5 of space — inside by two — and on a device it printed SPARKS ·
# BLOOM · FLAS. A hint that loses its last word is worse than no hint, and a
# two-unit margin is not a margin.
#
# AND THE OTHER HALF WAS MEASURING THE WRONG STRING. That fix claimed the three
# slots were "more than [ NONE ] needs" — on the strength of having measured NONE.
# bracketed() renders [ LABEL ], four characters more, and at seventeen point every
# one of these six stops overflowed its 67.7-unit slot. So they are segments, which
# is the same plate without the brackets. STILL is 51 units now, in a slot of 67.7.
func _steps(panel: Control, icon: String, label: String, hint: String,
		names: Array, field: String, after: String) -> void:
	# THE THREE STOPS ARE THE WIDEST CONTROL ON THE PANEL, so the hint beside them
	# has to stop soonest — see HINT_CLEAR. The row is 0.58 to the right edge less
	# eighteen, and eight of air ahead of that.
	var r := _acc_rt(panel, icon, label, hint, STEPS_CLEAR)
	var row_rt := UiKit.rect(r, "steps", Vector2(0.58, 0.5), Vector2(1, 0.5),
			Vector2(0, -Access.TAP_TARGET * 0.5), Vector2(-18, Access.TAP_TARGET * 0.5))

	var group := StepGroup.new()
	group.name = "steps"
	group.screens = self
	group.field = field
	group.after = after
	row_rt.add_child(group)

	for idx in range(names.size()):
		var slot := UiKit.rect(row_rt, names[idx],
				Vector2(idx / float(names.size()), 0), Vector2((idx + 1.0) / names.size(), 1),
				Vector2(0 if idx == 0 else 3, 0), Vector2(0 if idx == names.size() - 1 else -3, 0))
		var act := UiAct.on(slot, group, "pick", idx)
		var b := UiKit.segment(slot, names[idx], names[idx], funcref(act, "press"), 17)
		group.plates.append(b.plate_node())
		group.labels.append(b.label_node())
	group.paint()


# The stops of one segmented control, and the repaint they share. This is the
# `Paint()` local function and its two arrays, which have to live somewhere a
# FuncRef can reach.
class StepGroup extends Node:
	var screens = null
	var field := ""
	var after := ""
	var plates := []
	var labels := []

	func pick(idx: int) -> void:
		screens._write_setting(field, idx)
		Store.save()
		if after != "":
			screens.call(after)
		paint()

	func paint() -> void:
		var now: int = int(clamp(screens._read_setting(field), 0, plates.size() - 1))
		for k in range(plates.size()):
			if plates[k] == null:
				continue
			plates[k].modulate = Palette.rust() if k == now else Palette.PANEL
			labels[k].add_color_override("font_color",
					Palette.VOID if k == now else Palette.dim())


# ---- the win card -----------------------------------------------------------

var _win_what: Label
var _win_verdict: Label
var _win_folds: Label
var _win_par: Label
var _win_best: Label
var _win_vault: Label
var _win_next_row: UiRect
var _win_retry_row: UiRect
var _win_outs_row: UiRect
var _win_stats: UiRect
var _win_next: UiButton
var _win_retry: UiButton

const WIN_PAD := 84.0

# The instrument: three rows deep enough to read as one panel.
const STATS_H := 204.0

# Air under the last way out, before the bezel.
const WIN_FOOT := 62.0

# Where the heading block ends: the vault line, plus its air.
const WIN_HEAD := 286.0


func _build_win() -> void:
	var l := layer("win")
	_tint(l, Color(0, 0, 0, 0.92))

	# A CARD, NOT A SCREENFUL.
	#
	# This was full-bleed: the stat names sat against the left edge and their numbers
	# against the right, six hundred pixels away, so nothing read as a pair. Under it
	# were three identical framed buttons, which is three equal offers on a card that
	# has exactly one thing you came here to do. Everything now lives in a centred
	# column the width of the thing it is reporting on, the numbers sit inside a
	# bordered plate with a hairline between each row, and only NEXT keeps a plate.
	#
	# AND IT IS THREE BLOCKS, NOT ONE. Fixing the column left a worse problem behind:
	# everything was still measured downward from the top, so the whole card finished
	# two thirds of the way up a plate that is eleven hundred units tall, and the
	# bottom third was empty. It read as a dialogue that had been dropped on the
	# screen rather than a screen.
	#
	# So the three blocks are anchored to three different things, because they answer
	# to three different things. WHAT HAPPENED goes to the top. WHAT IT COST is the
	# instrument and sits on the middle of the plate. WHAT NOW is anchored to the
	# BOTTOM and stacks upward from there, which is both where a thumb is and the
	# only arrangement in which a hidden RETRY cannot open a hole — see _flow_win.
	var halves := _card_halves(l)
	var body: Control = halves[0]
	_card_side = halves[1]

	_win_what = UiKit.label(body, "what", "VAULT SOLVED", 17, Palette.rust(), UiKit.Anchor.LOWER_CENTER,
			Vector2(0, 1), Vector2(1, 1), Vector2(WIN_PAD, -150), Vector2(-WIN_PAD, -122))
	_win_verdict = UiKit.label(body, "verdict", "AT PAR", 46, Palette.ARC, UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1), Vector2(WIN_PAD, -224), Vector2(-WIN_PAD, -158))
	_win_vault = UiKit.label(body, "vault", "", 18, Palette.ARC, UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 1), Vector2(1, 1), Vector2(WIN_PAD, -262), Vector2(-WIN_PAD, -232))

	# BETWEEN THE TWO OF THEM, and _flow_win decides where — it is the only block
	# with slack either side of it, so it is the one that absorbs a missing RETRY row
	# instead of letting the hole land somewhere it can be read as a mistake.
	_win_stats = UiKit.rect(body, "stats", Vector2(0, 1), Vector2(1, 1),
			Vector2(WIN_PAD, -474), Vector2(-WIN_PAD, -306))
	var ru := Palette.rust()
	UiKit.framed(_win_stats, Palette.PANEL_HI, Color(ru.r, ru.g, ru.b, 0.55))
	_win_folds = _stat(_win_stats, "FOLDS USED", 0)
	_win_par = _stat(_win_stats, "PAR", 1)
	_win_best = _stat(_win_stats, "YOUR BEST", 2)

	_win_next_row = UiKit.rect(_card_side, "next", Vector2(0, 1), Vector2(1, 1),
			Vector2(WIN_PAD, -498 - UiKit.PRIMARY_H), Vector2(-WIN_PAD, -498))
	_win_next = UiKit.bracketed(_win_next_row, "next", "NEXT", funcref(self, "_on_next"), 28, true)

	# The rest are text. They are ways OUT of this card rather than things it is for,
	# and a bracketed label with nothing behind it says that without needing to be
	# smaller or greyer than it can be read at.
	_win_retry_row = UiKit.rect(_card_side, "retry", Vector2(0, 1), Vector2(1, 1),
			Vector2(WIN_PAD, -640), Vector2(-WIN_PAD, -592))
	_win_retry = UiKit.link(_win_retry_row, "retry", "RETRY FOR PAR",
			funcref(self, "_on_reset"), 21, Palette.INK)

	_win_outs_row = UiKit.rect(_card_side, "outs", Vector2(0, 1), Vector2(1, 1),
			Vector2(WIN_PAD, -700), Vector2(-WIN_PAD, -652))
	var v_slot := UiKit.rect(_win_outs_row, "v", Vector2(0, 0), Vector2(0.5, 1), Vector2.ZERO, Vector2.ZERO)
	UiKit.link(v_slot, "vaults", "VAULTS", funcref(self, "_on_vaults"), 20)
	var m_slot := UiKit.rect(_win_outs_row, "m", Vector2(0.5, 0), Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	UiKit.link(m_slot, "menu", "MENU", funcref(self, "_on_menu"), 20)


func _on_next(_a = null) -> void:
	# AND IF THAT CUBE ENDED A CHAPTER, THE MACHINE HAS SOMETHING TO SAY FIRST. It
	# goes between the card and the next board rather than over the win: the card is
	# about the cube just solved and the word is about the fifteen of them.
	var at: int = _dir.s.level_no
	if Chapters.due(at, _dir.s.is_daily(), _dir.s.is_made(), Store.data().said_chapter):
		Store.data().said_chapter = Chapters.mark(Store.data().said_chapter, at)
		Store.save()
		_next_after = at + 1
		show_chapter_word(Chapters.word_after(at), funcref(self, "_play_next"))
		return
	show(null)
	_dir.play(at + 1)


# The one thing the chapter word's callback has to carry.
var _next_after := 1


func _play_next() -> void:
	_dir.play(_next_after)


# AND THE ACTIONS FLOW, because two of the three come and go: NEXT is meaningless
# after the daily and RETRY FOR PAR only exists if you went over. Fixed positions
# would leave the hole this card had before — and a PERFECT COLLAPSE, the best
# outcome in the game, is exactly the case that hides one.
#
# IT STACKS UPWARD, from the bottom of the plate. Flowing downward from a fixed top
# put the hole back in a different place: with RETRY hidden the stack simply ended
# a hundred units higher and left the bottom of the screen empty. Anchored to the
# foot, a missing row costs air ABOVE the primary — which is the one direction
# where extra space reads as composition rather than as something failing to load.
#
# It also puts the three of them in the reverse of their importance from the bottom
# up: the ways out are furthest from the thumb, NEXT is nearest the eye, and the
# last row is always at the same height however many rows there are.
func _flow_win() -> void:
	# measured downward from the top of the plate, because that is the frame the
	# offsets are in — the foot is just where we start
	# The measured floor, for the reason the pause card's note gives — these two
	# are the same object seen twice and they flow the same way.
	_flow_y = Layout.plate_size().y - (24.0 if Layout.is_landscape() else WIN_FOOT)

	_place(_win_outs_row, Access.TAP_TARGET, 18.0)
	_place(_win_retry_row, Access.TAP_TARGET, 26.0)
	_place(_win_next_row, UiKit.PRIMARY_H, 0.0)

	# AND THE INSTRUMENT TAKES THE MIDDLE OF WHATEVER IS LEFT. It is the only block
	# with slack on both sides, so it is where a missing RETRY row is allowed to show
	# up — as equal air above and below a panel, which is a composition, rather than
	# as a gap at one end, which is a screen that failed to finish loading.
	# _flow_y is now the top of the stack — the last _place took no gap after it.
	# TURNED, THE INSTRUMENT IS NOT BETWEEN THE HEAD AND THE STACK — they are in
	# different columns. It takes the middle of what is left under the head on its
	# own side, which is the same sentence with the stack's height taken out of it.
	var floor_y: float = _flow_y if not Layout.is_landscape() \
			else Layout.plate_size().y - 24.0
	var mid := (WIN_HEAD + floor_y) * 0.5
	var pad: float = 40.0 if Layout.is_landscape() else WIN_PAD
	_win_stats.anchor_to(Vector2(0, 1), Vector2(1, 1),
			Vector2(pad, -(mid + STATS_H * 0.5)), Vector2(-pad, -(mid - STATS_H * 0.5)))


var _flow_y := 0.0


func _place(r: UiRect, h: float, gap: float) -> void:
	if not r.visible:
		return
	var pad: float = 24.0 if Layout.is_landscape() else WIN_PAD
	r.anchor_to(Vector2(0, 1), Vector2(1, 1),
			Vector2(pad, -_flow_y), Vector2(-pad, -(_flow_y - h)))
	_flow_y -= h + gap


# One reading of the card: its name on the left, its number on the right, and a
# hairline under it so three of them read as one instrument. Close enough together
# that the eye pairs them without being told to.
static func _stat(card: Control, key: String, slot: int) -> Label:
	var rows := 3
	var h := 1.0 / rows
	var r := UiKit.rect(card, key, Vector2(0, 1 - (slot + 1) * h), Vector2(1, 1 - slot * h),
			Vector2.ZERO, Vector2.ZERO)

	UiKit.label(r, "k", key, 17, Palette.dim(), UiKit.Anchor.MIDDLE_LEFT,
			Vector2.ZERO, Vector2.ONE, Vector2(28, 0), Vector2(-120, 0))
	if slot < rows - 1:
		var ru := Palette.rust()
		UiKit.panel(r, "rule", Color(ru.r, ru.g, ru.b, 0.22),
				Vector2(0, 0), Vector2(1, 0), Vector2(22, 0), Vector2(-22, 1))

	return UiKit.label(r, "v", "0", 26, Palette.INK, UiKit.Anchor.MIDDLE_RIGHT,
			Vector2.ZERO, Vector2.ONE, Vector2(0, 0), Vector2(-28, 0))


static func show_win(s: Session, dir) -> void:
	var me := i()
	me._win_what.text = "CUBE SOLVED" if s.is_made() \
			else ("DAILY SOLVED" if s.is_daily() else "VAULT SOLVED")

	me._win_folds.text = str(s.turns)
	me._win_par.text = str(s.lv.par)
	var b = Store.try_best(s.level_no)
	me._win_best.text = str(Store.data().daily_best) if s.is_daily() \
			else (str(b) if b != null else str(s.turns))

	me._win_verdict.text = "BELOW PAR" if s.turns < s.lv.par \
			else ("PERFECT COLLAPSE" if s.turns == s.lv.par else str(s.turns - s.lv.par) + " OVER PAR")
	me._win_verdict.add_color_override("font_color",
			Palette.ARC if s.turns <= s.lv.par else Palette.rust())

	# A vault boundary is worth marking — it is the only structure an endless game
	# has, and it is where the demand steps up.
	var crossing: bool = not s.is_daily() and not s.is_made() \
			and Vaults.vault_of(s.level_no + 1) != Vaults.vault_of(s.level_no)
	if s.is_daily():
		me._win_vault.text = "DAILY " + Daily.day_label()
	elif crossing:
		var nb := Vaults.vault_of(s.level_no + 1)
		me._win_vault.text = "VAULT " + Vaults.roman_of(nb) + " / " + Vaults.vault_name(nb)
	else:
		me._win_vault.text = ""

	# there is no next cube after the daily — there is tomorrow
	me._win_next_row.visible = not s.is_daily() and not s.is_made()
	me._win_retry_row.visible = s.turns > s.lv.par
	me._flow_win()

	show("win")
