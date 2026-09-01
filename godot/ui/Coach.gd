class_name Coach
extends Reference
# FIRST CONTACT — every verb, taught on the cube that needs it.
#
# THE HOLE THIS FILLS. The shipped web build showed the manual once, on the first
# press of PLAY. The Unity port dropped it and kept the field — `taught` was
# declared in the save and read by nothing — so a player opening this game for
# the first time was handed a black square, four dim arrows and no sentence in
# the entire product telling them that a swipe folds the world. Every OTHER
# mechanic in here is taught: the plate has a card, the matrix has its one nudge,
# the four eversion cubes teach eversion by being unsolvable without it. The two
# things the game is MADE of were the only two nobody taught.
#
# AND IT IS NOT THE MANUAL. Restoring the web build's answer would put a wall of
# type between a player and the thing they opened; nobody reads a manual in a
# puzzle game, which is what the manual's own header says. The doctrine this
# project already follows is written on the plate card: teach it when it shows
# up, once, on the cube that has one, with the thing itself waiting behind the
# card. This is that doctrine applied to the verbs.
#
# THE ORDER IS MEASURED, NOT ASSUMED. The obvious script opens with SWIPE TO
# FOLD, because folding is the idea. It would be wrong. Solve the authored cubes
# and read the first act of the optimal line:
#
#     cube 1  FOOTING     par 1   step-up   step-up  step-up  FOLD-left
#     cube 2  THE TURN    par 1   step-down FOLD-up  step-down
#     cube 3  BURIED      par 2   step-left step-up  FOLD-left ...
#     cube 4  TWO FACES   par 2   step-left step-down FOLD-right ...
#
# On all four the first thing a player has to do is WALK. A coach that opens by
# teaching the fold is teaching the second verb first and demonstrating a move
# the cube does not want yet. So the walk is taught at the start, and the fold is
# taught at the moment the fold becomes the answer — which is a question the game
# can already ask itself, because the solver is right there. Session.advice is
# the hint without the charge.
#
# WHAT RETIRES IT IS EVIDENCE, NOT EXPOSURE. The bits are written when the player
# DOES the verb, not when the prompt is shown — so a player who folds before
# being asked has already proved they can, and is never asked; and one who backs
# out of the game mid-lesson gets the lesson again rather than having spent it on
# a screen they left.
#
# IT NEVER TAKES THE INPUT. Every mark here ignores the mouse and there is no
# dismiss button, because there is nothing to dismiss: the way out of the lesson
# is to do the thing, and a player who already knows the game does it inside a
# second and never reads a word.
#
# ---- AND THEN IT STOPPED, WITH FOUR VERBS LEFT ----------------------------
#
# The two verbs above are what a player DOES. There are five more things the game
# is made of that a player has done TO them — the node and its lock, the two
# plates, the everter, the trigger — and exactly one of them was taught. PLATES
# gets a full card the first time a cube carries one, which is right: it is the
# largest single idea after the fold and it earns a screen. The other four
# arrived by chapter position and hope.
#
# That is a real hole and it is not a small one. Chapter IV opens on a lock you
# cannot pass, chapter VI on a plate that does the OPPOSITE of the one you were
# taught, chapter VII on a solid that turns itself inside out and chapter VIII on
# a board that is exchanged for another. Each of those is a rule, none of them is
# guessable from the art, and a puzzle whose rule you have not been told is not a
# puzzle — it is a search.
#
# FOUR MORE CARDS WOULD HAVE BEEN THE WRONG ANSWER. This file's own opening
# argument is that nobody reads a manual in a puzzle game, and four more walls of
# type between a player and a cube is the manual arriving in instalments. So the
# four are taught the way the first two are: the ring this coach already owns,
# put on the thing itself, with one line under the window saying what it does.
# The player's next tap is the lesson.
#
# THE ORDER IS THE LADDER'S, NOT A LIST'S. No lesson names a cube number. Each
# one fires on the first cube that CARRIES the thing, whenever that happens — so
# a player who reaches an everter through the seed box gets the everter's line,
# and re-cutting the catalogue cannot leave a lesson pointing at a chapter that
# no longer has one.

# What the coach has proof the player can do. Bits of the save's `taught`.
const LEARNED_STEP := 1
const LEARNED_FOLD := 2

# AND WHAT IT HAS PROOF THEY HAVE MET. These are not verbs the player performs —
# they are things the board does when it is stepped on — so the bit is written
# from the event the SESSION raises, not from a gesture: key_taken for the node,
# plate_fired for the other three, each carrying the bit that says which. A
# player who walks onto an everter before reading the line has been taught by the
# everter, which is the better teacher, and is never shown it again.
const LEARNED_NODE := 4
const LEARNED_SUBSTRATE := 8       # plate B, which opens the solid
const LEARNED_EVERTER := 16
const LEARNED_TRIGGER := 32

# The two verbs of first contact — what retires the opening lessons.
const LEARNED_ALL := LEARNED_STEP | LEARNED_FOLD

# Everything the coach has to say, for a save that has seen the lot.
const LEARNED_EVERYTHING := LEARNED_ALL | LEARNED_NODE | LEARNED_SUBSTRATE \
		| LEARNED_EVERTER | LEARNED_TRIGGER

# The last cube the coach will speak on. Vault I is authored and cube two is the
# second of the two that "show you a way out you cannot walk to"; past that the
# game is teaching itself and a voice over the top of it is noise. A player who
# has reached cube three without folding does not exist — the first two cannot be
# finished any other way.
const LAST_CUBE := 2

enum Lesson { NONE, STEP, FOLD, NODE, SUBSTRATE, EVERTER, TRIGGER, PEEK }

# THE ORDER THE LESSONS QUEUE IN when a cube carries more than one thing the
# player has not met. It is the order the ladder introduces them, so on the cubes
# that follow the curriculum nothing is ever out of turn — and on a cube reached
# out of order it is still one lesson at a time, which is the rule that matters.
const WORLD_VERBS := [
	[LEARNED_NODE, Lesson.NODE],
	[LEARNED_SUBSTRATE, Lesson.SUBSTRATE],
	[LEARNED_EVERTER, Lesson.EVERTER],
	[LEARNED_TRIGGER, Lesson.TRIGGER],
]

# How far the line's box stands above the stroke's inner edge, and how tall it
# is. The foot is the HUD's own first clear unit — the line and the four fold
# marks are the same kind of thing on the same frame — and the height is public
# because the toast stacks on top of it and the two must not be able to drift
# into each other.
const LINE_HEIGHT := HudMetrics.COACH_LINE_HEIGHT

# InputRouter's turn indices: 0 left, 1 right, 2 up, 3 down.
const FOLD_WAY := [Vector2(-1, 0), Vector2(1, 0), Vector2(0, 1), Vector2(0, -1)]

# The arrow glyph is drawn pointing up; the rest are that one turned.
const FOLD_SPIN := [90.0, -90.0, 0.0, 180.0]


# WHAT A CUBE HAS TO TEACH, as bits, so next() stays a pure function of numbers
# and can be played out by the harness with no scene in it.
#
# `saw_plate_card` is the one piece of state that is not the cube: PLATES has its
# own full screen, and a cube carrying B on the first plate a player ever meets
# would otherwise get the card AND a line about the second kind, in the same
# breath, about two different plates. The card teaches what a plate IS; this
# teaches that there is another one and it goes the other way — so it waits until
# the card has been.
static func carries(lv, saw_plate_card: bool) -> int:
	if lv == null:
		return 0
	var got := 0
	if lv.keys.size() > 0 and lv.doors.size() > 0:
		got |= LEARNED_NODE

	# EVERY CELL OF EVERY SOLID, because a trigger cube is two of them and the
	# glyph that swaps them can sit in either. Reading `vox` alone is the mistake
	# this project has now made three times — see Level.clone.
	var arrays := [lv.vox]
	for a in lv.alts:
		arrays.append(a)
	for arr in arrays:
		for i in range(arr.size()):
			var c: int = arr[i]
			if c == Level.PLATE_B and saw_plate_card:
				got |= LEARNED_SUBSTRATE
			elif c == Level.EVERTER:
				got |= LEARNED_EVERTER
			elif c == Level.TRIGGER or c == Level.TRIGGER2:
				got |= LEARNED_TRIGGER
	return got


# THE WHOLE DECISION, WITH NOTHING IN IT THAT NEEDS A SCENE — so the harness can
# play the state machine out against a real Session without a canvas, a camera or
# a frame.
static func next(taught: int, kind: int, level_no: int, won: bool,
		advice_is_fold: bool, carried: int, peek_wanted: bool = false) -> int:
	# THE LADDER ONLY. A daily is somebody's second week, a made cube is the
	# Forge's output and practice is a cube visited by number; none of the three
	# is anybody's first minute, and the coach speaking there would be the game
	# explaining a swipe to somebody who typed a level code in.
	if kind != Session.LoadKind.VAULT or won:
		return Lesson.NONE

	# ---- first contact: the two verbs, on the two cubes that need them
	if level_no <= LAST_CUBE and (taught & LEARNED_ALL) != LEARNED_ALL:
		if (taught & LEARNED_STEP) == 0:
			return Lesson.STEP
		if (taught & LEARNED_FOLD) == 0 and advice_is_fold:
			return Lesson.FOLD
		return Lesson.NONE

	# ---- and the world's verbs, wherever the ladder puts them
	#
	# ONE AT A TIME, and never over the top of first contact: a player on cube one
	# is learning to walk and is not also being told what a lock is. Past that,
	# the first thing this cube carries that they have not met is the whole
	# lesson, and the rest wait for a cube of their own.
	if level_no <= LAST_CUBE:
		return Lesson.NONE

	for wv in WORLD_VERBS:
		if (carried & wv[0]) != 0 and (taught & wv[0]) == 0:
			return wv[1]

	# ---- AND THE ONE VERB THAT IS ABOUT LOOKING RATHER THAN DOING
	#
	# Hold anywhere and the lattice goes to glass. It is the gesture that says the
	# board is a SOLID rather than a grid of squares, and it is the only thing on
	# the screen that can answer "where is the exit" when the exit is behind
	# something — which is the state this fires on, and nothing else.
	#
	# LAST, because the world's verbs are about the rules and this is about the
	# view: a cube that introduces a plate teaches the plate, and mentions the
	# glass on some later cube that does not.
	#
	# It cannot reach cubes one and two — first contact returns above — and that is
	# the right floor rather than an accident of ordering. Measured through
	# LevelSupply, cubes one and two draw their core and cube three does not, so
	# the first board that can ask this question is the first board that has it.
	if peek_wanted:
		return Lesson.PEEK

	return Lesson.NONE


# The line under the window. Nothing here is longer than the fold's.
static func line_for(l: int) -> String:
	match l:
		Lesson.STEP:
			return "TAP TO MOVE"
		Lesson.FOLD:
			return "SWIPE TO FOLD"
		# WHAT IT DOES, NOT WHAT TO PRESS. The ring already says where to go; a
		# line that also said "step on it" would be spending the only sentence
		# there is on the half the player can see.
		Lesson.NODE:
			return "THE NODE OPENS THE LOCK"
		Lesson.SUBSTRATE:
			return "THIS PLATE OPENS THE SOLID"
		Lesson.EVERTER:
			return "EVERY CELL TURNS"
		Lesson.TRIGGER:
			return "THE BOARD IS EXCHANGED"
		# WHAT IT DOES, and it has to explain rather than name: MATRIX is what the
		# manual calls it and this is where the player meets it, so a line that
		# said "hold for matrix" would be introducing a word instead of a verb.
		Lesson.PEEK:
			return "HOLD TO SEE INSIDE IT"
	return ""


# CAN THE PLAYER SEE WHERE THEY ARE GOING? The exit exists in a projection only
# while its cell IS the surface of its column — bury it behind lattice and it is
# gone until you fold, which is the whole reason the glass is worth reaching for.
static func exit_hidden(s) -> bool:
	if s == null or s.lv == null or s.won or s.surf.empty():
		return false
	return not Projection.surface_at(s.n, s.surf, s.m, s.lv.goal)


static func is_world_verb(l: int) -> bool:
	return l == Lesson.NODE or l == Lesson.SUBSTRATE or l == Lesson.EVERTER or l == Lesson.TRIGGER


# A SAVE THAT HAS BEEN PAST THE FIRST CUBE IS TAUGHT.
#
# Everyone playing before this file existed has a save with no bits set in it,
# and handing them a tutorial on their four-hundredth cube is a worse bug than
# the one this fixes. `reached` is the honest witness: it only ever moves when a
# cube is cleared.
static func migrate(taught: int, reached: int) -> int:
	return taught | LEARNED_ALL if reached > 1 else taught


# AND A CUBE THE PLAYER HAS ALREADY CLEARED TEACHES NOTHING.
#
# migrate above answers for the two gestures, where `reached > 1` is proof on its
# own. The world's verbs need a different witness, because a player who was two
# hundred cubes deep before these lessons existed has fired every plate and
# turned every solid in the game, and would otherwise be told what an everter is
# on their next chapter-VII cube.
#
# THE FIRST ANSWER WAS A SCAN AND IT WAS THE WRONG SHAPE. It found the lowest
# cube in the catalogue carrying each thing and compared `reached` against that,
# which meant decoding the whole ladder at launch to migrate a handful of saves,
# and meant the coach holding four cube numbers that a re-cut could quietly
# invalidate.
#
# This is the same rule with the numbers taken out. `reached` is the cube the
# player is up to, so `level_no < reached` says THEY HAVE ALREADY CLEARED THIS
# ONE — and every chapter that introduces a verb is cut against a claim that the
# glyph is required, so clearing a cube that carries one is proof they used it,
# not that they saw it. Costs one comparison, knows no cube numbers, and is exact
# on a re-cut ladder because it never asks where anything is.
static func retire_seen(taught: int, level_no: int, reached: int, carried: int) -> int:
	return taught | carried if level_no < reached else taught


# Called from the events the player's own hands raise. The write is debounced by
# Store, so four of these in a second cost one save.
static func learned(bit: int) -> void:
	if (Store.data().taught & bit) == bit:
		return
	Store.data().taught |= bit
	Store.save()


# The far end of the walk, and it must be PLAIN TRACE. Returns [u, v] or null.
#
# One tap walks the singularity to any trace connected to the one under it, so
# the honest demonstration of the verb is its full reach rather than the next
# square along — the lesson is "you go where you point", not "you shuffle". But
# stepping onto a glyph FIRES it: a plate turns the whole board inside out for
# five seconds, an everter turns the solid over. Teaching the walk by detonating
# the biggest event in the game is not teaching the walk, so the ring only ever
# lands on ordinary trace, and the same rule keeps it off a node and a lock —
# both of which are state, and neither of which is what the sentence under it
# says.
static func far_end(s):
	if s == null or s.lv == null or s.surf.empty() or s.reach.empty():
		return null

	var n: int = s.n
	var best := -1
	var bu := -1
	var bv := -1
	var here := Projection.view_of(n, s.m, s.pos)

	for u in range(n):
		for v in range(n):
			var k := u * n + v
			if not s.reach[k]:
				continue
			if u == int(here.x) and v == int(here.y):
				continue
			var c: Surf = s.surf[k]
			if not c.has or Level.is_glyph(c.t):
				continue
			if s.lv.key_index_at(c.w) >= 0 or s.lv.door_index_at(c.w) >= 0:
				continue

			var d: int = s.reach_dist[k]
			if d <= best:
				continue                           # ties keep the first, so it is stable
			best = d
			bu = u
			bv = v

	return [bu, bv] if best > 0 else null


# WHERE THE THING IS, ON THE BOARD AS IT IS RIGHT NOW. Returns [u, v] or null.
#
# The ring goes on the glyph itself, and it may only go somewhere the player can
# actually see: a cube carries its everter in the solid, but the cell that wins
# its column is a question about the current fold, and a ring drawn over a square
# showing something else is worse than no ring at all. So a lesson whose subject
# is not on screen does not show — it waits, and the next fold that brings the
# thing to the surface gives it.
#
# REACHABLE FIRST, VISIBLE OTHERWISE. If the player can walk to it the ring means
# "go here"; if they cannot it means "that is the thing", and both are worth
# saying. Ties keep the first square scanned so the mark does not wander between
# frames on a cube with two of them.
static func mark_at(s, want: int):
	if s == null or s.lv == null or s.surf.empty():
		return null

	var n: int = s.n
	var best_u := -1
	var best_v := -1
	for u in range(n):
		for v in range(n):
			var k := u * n + v
			var c: Surf = s.surf[k]
			if not c.has or not _subject(s, want, c):
				continue
			if not s.reach.empty() and s.reach[k]:
				return [u, v]
			if best_u < 0:
				best_u = u
				best_v = v

	return [best_u, best_v] if best_u >= 0 else null


# Is this the cell the lesson is about?
static func _subject(s, want: int, c: Surf) -> bool:
	match want:
		Lesson.NODE:
			var ki: int = s.lv.key_index_at(c.w)
			return ki >= 0 and (s.kmask & (1 << ki)) == 0
		Lesson.SUBSTRATE:
			return c.t == Level.PLATE_B
		Lesson.EVERTER:
			return c.t == Level.EVERTER
		Lesson.TRIGGER:
			return c.t == Level.TRIGGER or c.t == Level.TRIGGER2
	return false


# Cheap enough to ask every time the board settles, and never asked more often
# than that. A five-cube with a par of one is a search of a few hundred states;
# the budget is small for the same reason. Returns the fold direction, or -1.
static func advice_is_fold(s) -> int:
	var a = s.advice(8)
	if a == null or not a.is_turn:
		return -1
	return a.dir


static func stamp(s) -> int:
	return (Turns.ori_index(s.m) << 40) ^ (int(s.pos.x) << 34) ^ (int(s.pos.y) << 28) \
			^ (int(s.pos.z) << 22) ^ (s.kmask << 12) ^ (s.doors << 4) ^ s.world \
			^ (s.level_no << 46) ^ ((1 << 60) if s.won else 0)


# ---- the marks -------------------------------------------------------------

var _root: UiRect
var _ring: TextureRect
var _arrow: TextureRect
var _line: Label

var _showing = Lesson.NONE
var _t := 0.0

# Where the ring goes: the far end of the walk, in board squares.
var _cell_u := -1
var _cell_v := -1

# Which way the coached fold goes, in InputRouter's turn indices.
var _fold_dir := -1

# Restated only when the state changes, because advice runs a solver.
var _stamp := -0x7FFFFFFFFFFFFFF


func build(aperture: Control) -> void:
	_root = UiKit.rect(aperture, "coach", Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	_root.visible = false

	# THE RING IS THE ONE THE PLAYER ALREADY OWNS. It is the mark drawn around
	# the singularity itself in the manual's TAP TO MOVE diagram, so the coach is
	# pointing with the game's own vocabulary rather than introducing a finger, a
	# hand or a dotted line that appears nowhere else in the product.
	_ring = UiKit.icon(_root, "ring", Palette.ARC, 64.0, Vector2(0.5, 0.5), Vector2.ZERO)
	_arrow = UiKit.icon(_root, "arrow", Palette.RUST_HI, 44.0, Vector2(0.5, 0.5), Vector2.ZERO)

	# ONE LINE, UNDER THE BOARD. Not a card, not a panel and not over the cube:
	# the thing being talked about is the board, and a coach that covers its own
	# subject is a worse teacher than no coach.
	#
	# IT USED TO BE UNDER THE WINDOW, in the gap the square aperture left between
	# itself and the control band. That gap is window now — the stroke runs from
	# the readout's rule to the controls — so twenty-six units below the frame is
	# on top of MENU, UNDO and HINT. It moves to the same side of the line:
	# inside the stroke, past the down fold's arrow, and still well below the
	# cube, which is fitted inside the square with 1.28 of margin and never
	# reaches down this far.
	_line = UiKit.label(_root, "line", "", 22, Palette.INK, UiKit.Anchor.UPPER_CENTER,
			Vector2(0, 0), Vector2(1, 0),
			Vector2(0, HudMetrics.MARK_BAND), Vector2(0, HudMetrics.MARK_BAND + LINE_HEIGHT))


# Pumped from the HUD, which is where the readout's own clock already is. The
# state is only recomputed when the board has actually changed — position,
# orientation, keys, doors and world — because the recompute runs a solver and a
# frame is not a state change.
func tick(s, cam: Camera, view, dt: float) -> void:
	if s == null or s.lv == null:
		_hide()
		return

	var st := stamp(s)
	if st != _stamp:
		_stamp = st
		_restate(s)

	# THE ONE LESSON WHOSE ACT DOES NOT MOVE THE BOARD.
	#
	# Every other prompt retires for free, because the thing that teaches it — a
	# walk, a fold, a node taken — changes the state the stamp is made of, so the
	# next tick restates and the lesson is gone. Holding the glass changes nothing
	# on the board: same position, same orientation, same keys. The stamp is
	# identical and the line would sit there congratulating the player until their
	# next move. It goes the moment they hold instead.
	if _showing == Lesson.PEEK and Store.data().saw_peek != 0:
		_hide()

	if _showing == Lesson.NONE:
		return

	_t += dt
	_paint(s, cam, view)


func _restate(s) -> void:
	var taught: int = Store.data().taught

	# The fold's direction is the only thing here that costs a search, so it is
	# asked for ONLY when there is a fold lesson left to give.
	var dir := -1
	var fold: bool = (taught & LEARNED_FOLD) == 0 \
			and (taught & LEARNED_STEP) != 0 \
			and s.kind == Session.LoadKind.VAULT and s.level_no <= LAST_CUBE and not s.won
	if fold:
		dir = advice_is_fold(s)
		fold = dir >= 0

	var carried := carries(s.lv, Store.data().saw_plate != 0)

	# A CUBE ALREADY CLEARED RETIRES ITS LESSONS INSTEAD OF GIVING THEM, and it
	# writes the bits, so the retirement is permanent rather than recomputed on
	# every board.
	var seen := retire_seen(taught, s.level_no, Store.data().reached, carried) & ~taught
	if seen != 0:
		for wv in WORLD_VERBS:
			if (seen & wv[0]) != 0:
				learned(wv[0])
		taught = Store.data().taught

	# `saw_peek` is already the honest record of whether they have ever held, so
	# the lesson needs no bit of its own and no migration: a save that found the
	# glass before this file existed is a save that is never told about it.
	var peek_wanted: bool = Store.data().saw_peek == 0 and exit_hidden(s)

	var want := next(taught, s.kind, s.level_no, s.won, fold, carried, peek_wanted)

	# A LESSON WITH NOWHERE TO POINT IS NOT SHOWN. Both marks are marks on the
	# board, so both can fail to have a square — the walk when every reachable
	# cell is a glyph, the world's verbs when the thing is not the cell winning
	# its column in this fold. Neither is an error and neither is worth a
	# sentence on its own: the lesson simply waits.
	if want == Lesson.STEP:
		var at = far_end(s)
		if at == null:
			want = Lesson.NONE
		else:
			_cell_u = at[0]
			_cell_v = at[1]
	elif is_world_verb(want):
		var at2 = mark_at(s, want)
		if at2 == null:
			want = Lesson.NONE
		else:
			_cell_u = at2[0]
			_cell_v = at2[1]
	_fold_dir = dir

	if want != _showing:
		_showing = want
		_t = 0.0
		_root.visible = want != Lesson.NONE
		_line.text = line_for(want)

	_ring.get_parent().visible = _showing == Lesson.STEP or is_world_verb(_showing)
	_arrow.get_parent().visible = _showing == Lesson.FOLD


func _hide() -> void:
	if _showing == Lesson.NONE:
		return
	_showing = Lesson.NONE
	_stamp = -0x7FFFFFFFFFFFFFF
	_root.visible = false


# THE PULSE HAS A FLOOR, AND THE FLOOR IS THE ACCESSIBILITY ANSWER.
#
# Motion has an OFF in this game and it means off — camera shake, the bent clock,
# all of it. A prompt is not decoration though: it is the only thing on screen
# saying what to do, so it may not switch off with the sparks. It stops
# TRAVELLING instead. At motion_amount zero the arrow sits still at the edge it
# is pointing to and the ring holds its size, and both are still perfectly
# legible; at full they move.
func _paint(s, cam: Camera, view) -> void:
	var m := Access.motion_amount()
	var beat: float = fmod(_t, 1.4) / 1.4

	if _showing == Lesson.STEP or is_world_verb(_showing):
		if cam == null or view == null or view.cube == null:
			return
		if _cell_u < 0 or s.surf.empty():
			return
		var c: Surf = s.surf[_cell_u * s.n + _cell_v]
		if not c.has:
			return

		var world_at: Vector3 = view.cube.global_transform.xform(CubeGeometry.cell_to_object(s.n, c.w))
		var screen := cam.unproject_position(world_at)
		var host: UiRect = _ring.get_parent()
		host.set_anchored_position(_to_canvas(screen))

		# a ring closing on the square, then starting again — the shape a tap
		# makes, drawn at the place a tap is wanted
		var g := 1.0 + (1.0 - beat) * 0.55 * m
		host.set_size_delta(Vector2(64.0 * g, 64.0 * g))
		_ring.modulate = Color(Palette.ARC.r, Palette.ARC.g, Palette.ARC.b,
				0.35 + 0.55 * sin(beat * PI))
		return

	if _showing == Lesson.FOLD and _fold_dir >= 0:
		# the swipe, drawn as the arrow the four fold marks already use,
		# travelling the way the finger goes
		var way: Vector2 = FOLD_WAY[_fold_dir]
		var reach: float = min(_root.rect_size.x, _root.rect_size.y) * 0.30
		var along: float = (-0.5 + beat * m) * 2.0 * reach * (1.0 if m > 0.001 else 0.0)

		var ahost: UiRect = _arrow.get_parent()
		ahost.set_anchored_position(way * (along + reach * 0.55))
		_arrow.rect_pivot_offset = _arrow.rect_size * 0.5
		_arrow.rect_rotation = FOLD_SPIN[_fold_dir]
		_arrow.modulate = Color(Palette.RUST_HI.r, Palette.RUST_HI.g, Palette.RUST_HI.b,
				0.30 + 0.60 * sin(beat * PI))


# A point in viewport pixels, as an anchored position inside the coach's root —
# which is anchored to the whole aperture, so the offset is measured from its
# centre in canvas units.
func _to_canvas(screen: Vector2) -> Vector2:
	var s: float = max(0.0001, Layout.canvas_scale())
	var origin := _root.get_global_transform_with_canvas().origin
	var local := (screen - origin) / s
	var half := _root.rect_size * 0.5
	# The root's Y counts down and an anchored position counts up.
	return Vector2(local.x - half.x, half.y - local.y)
