class_name Access
# AAA — AND IT IS THREE LETTERS BECAUSE IT IS THREE SENSES.
#
# The game speaks in light, in sound and in the motor. A player who cannot take
# delivery of one of those three is not playing a harder version of this game,
# they are playing a broken one — a plate clock that can only be heard is a
# five-second timer with no display to somebody deaf, and a board read off
# brightness alone is unplayable on a phone held in sunlight by anybody at all.
#
# So each sense gets a grade, each grade is a NUMBER rather than an intention,
# and every number here is asserted by a test:
#
#   APPARENT    every piece of text the interface prints reaches WCAG AAA, 7:1,
#               against the ground it is printed on; every graphical object
#               reaches 1.4.11's 3:1 against its neighbour; and no fact is
#               carried by hue alone.
#   AUDIBLE     everything the game says with a sound, it also says in words;
#               the room and the instrument have separate levels.
#   ATTAINABLE  every control is at least 44 CSS px (88 units here) on its
#               shortest side; every verb has a form that needs no swipe and no
#               hold; and motion has an OFF, not a quieter setting.
#
# THE DEFAULT IS THE SHIPPED BUILD, EXACTLY. This file adds settings; it does
# not move the game underneath somebody who did not ask.

# ---- what the player asked for --------------------------------------------

# TWO DIFFERENT PEOPLE ARE ASKING FOR TWO DIFFERENT THINGS, and one switch
# called EFFECTS was answering both of them badly.
#
# Camera shake, the zoom punch and the bent clock are VESTIBULAR: they move the
# frame, and for the player they trouble, forty per cent of a 0.95-trauma plate
# fire is still a plate fire. Sparks, bloom and the full-screen flash are
# PHOTOSENSITIVE: they change the brightness of the whole picture, which is a
# different criterion (2.3.1) with a different answer. Splitting them is not
# more knobs for its own sake — it is the difference between a setting that
# works and one that makes somebody choose which symptom to keep.
# The two grades themselves live in Grade — Store writes them and this reads
# them, and a class each of them names is the only way out of the ring that
# creates. These aliases are so that every call site in the game still says
# Access.MotionLevel, which is where a reader looks for it.
const MotionLevel := Grade.Motion
const LightLevel := Grade.Light


static func motion() -> int:
	return int(clamp(Store.data().motion, 0, 2))


# What every camera impulse and every bent clock is multiplied by. STILL IS
# ZERO, and that is the whole reason this exists: the old EFFECTS bit bottomed
# out at 0.4, so there was no way to ask this game to hold still.
static func motion_amount() -> float:
	var m := motion()
	return 1.0 if m == MotionLevel.FULL else (0.4 if m == MotionLevel.REDUCED else 0.0)


static func lighting() -> int:
	return int(clamp(Store.data().light, 0, 2))


# Whether anything at all is drawn as light on top of the board.
static func light() -> bool:
	return lighting() != LightLevel.NONE


# Whether the picture gets a second copy of itself laid over it.
static func bloom() -> bool:
	return lighting() == LightLevel.FULL


static func light_amount() -> float:
	var l := lighting()
	return 1.0 if l == LightLevel.FULL else (0.4 if l == LightLevel.REDUCED else 0.0)


# THE FLASH IS THE ONE EFFECT WITH A SEIZURE IN IT.
#
# 2.3.1 allows three flashes in any one second. Every flash in this game is
# player-triggered — a fold, a node, a plate — so a fast hand on a small cube
# can outrun that on its own, without the game ever having done anything wrong:
# nothing in the design says "no more than three folds a second", and nothing
# should. So the cap is enforced where the flash is RAISED rather than trusted
# to the pace of play, and NONE turns it off outright.
const FLASHES_PER_SECOND := 3


# Text and interface lifted to 7:1. Off by default; see Palette.
static func legible() -> bool:
	return Store.data().legible != 0


# Every sound also printed as words.
static func captions() -> bool:
	return Store.data().captions != 0


# A FOLD THAT IS NOT A SWIPE, AND A MATRIX THAT IS NOT A HOLD.
#
# Both of the gestures this game is built on are two-part: travel far enough, or
# stay still long enough. Neither is available to a player using a head pointer,
# a switch, or one unsteady finger — and there is no difficulty argument for the
# swipe, because the fold it performs is already on the keyboard for anybody on
# a desktop.
static func assist() -> bool:
	return Store.data().assist != 0


# Instrument level and room level, 0..150, as percentages.
static func volume() -> float:
	return clamp(Store.data().vol, 0, 150) / 100.0


static func room() -> float:
	return clamp(Store.data().room, 0, 150) / 100.0


# ---- the standard's own arithmetic -----------------------------------------
#
# WCAG 2.1 defines contrast on RELATIVE LUMINANCE, which is not the luminance
# Palette uses. Palette's is a straight weighted sum of the sRGB values, which
# is the right thing there — it answers "is this swatch brighter than that one"
# for two swatches of the same material, and it is what the depth ramps were
# authored against. This one linearises first, which is what makes a ratio
# computed from it mean "can a human read this", and the two are different
# enough that using either for the other's job gives a wrong answer with a
# plausible shape.

const AAA_TEXT := 7.0
const AA_TEXT := 4.5
# 1.4.11: a graphical object against its neighbour.
const NON_TEXT := 3.0

# ---- and the standard's own sizes ------------------------------------------
#
# ONE CSS PIXEL IS TWO CANVAS UNITS. The shipped build's viewport is about 360
# CSS px wide and the canvas here references 720, so every number the WCAG text
# quotes in CSS pixels doubles on its way in and the conversion is the only
# arithmetic involved.

# The smallest type in the interface, 8.5 CSS px. Nothing may be smaller.
const SMALLEST_TYPE := 17

# 2.5.5 at AAA: 44 CSS px on the shortest side of anything you press.
#
# The buttons were never the problem — a button is 104 units and a primary is
# 120, both comfortably past it. The controls INSIDE a settings row were: a
# switch drawn 42 units tall and a slider handle drawn 28 are half the target
# and less, and they are the two things in this game most likely to be pressed
# by somebody who is already struggling with it, on the screen they opened to
# make it easier.
const TAP_TARGET := 88.0


static func _linear(c: float) -> float:
	return c / 12.92 if c <= 0.04045 else pow((c + 0.055) / 1.055, 2.4)


# WCAG relative luminance. Alpha is not part of it.
static func relative(c: Color) -> float:
	return 0.2126 * _linear(c.r) + 0.7152 * _linear(c.g) + 0.0722 * _linear(c.b)


# The contrast ratio between two opaque colours, 1..21.
static func contrast(a: Color, b: Color) -> float:
	var la := relative(a)
	var lb := relative(b)
	return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


# ---- and what a dichromat sees ---------------------------------------------
#
# The palette's central claim is that A TRACE IS BRIGHTER THAN THE LATTICE AT
# EVERY DEPTH, so the board survives a colour-blind eye. That claim was true
# when it was made and it is the kind of thing that stops being true silently,
# three commits after somebody nudges one hex value to look nicer — and the
# failure does not look like a bug, it looks like the game got hard.
#
# So it is simulated and asserted rather than believed. Viénot, Brettel and
# Mollon (1999): to LMS through Hunt-Pointer-Estevez, collapse the missing axis
# onto the plane the remaining two span, and back.

enum Sight { TRICHROMAT, PROTAN, DEUTAN, TRITAN }

const TO_LMS := [
	0.31399022, 0.63951294, 0.04649755,
	0.15537241, 0.75789446, 0.08670142,
	0.01775239, 0.10944209, 0.87256922,
]

const FROM_LMS := [
	5.47221206, -4.64196010, 0.16963708,
	-1.12524190, 2.29317094, -0.16789520,
	0.02980165, -0.19318073, 1.16364789,
]

const COLLAPSE_PROTAN := [0.0, 1.05118294, -0.05116099, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0]
const COLLAPSE_DEUTAN := [1.0, 0.0, 0.0, 0.95130920, 0.0, 0.04866992, 0.0, 0.0, 1.0]
const COLLAPSE_TRITAN := [1.0, 0.0, 0.0, 0.0, 1.0, 0.0, -0.86744736, 1.86727089, 0.0]


static func _mul(m: Array, v: Vector3) -> Vector3:
	return Vector3(
		m[0] * v.x + m[1] * v.y + m[2] * v.z,
		m[3] * v.x + m[4] * v.y + m[5] * v.z,
		m[6] * v.x + m[7] * v.y + m[8] * v.z)


static func _encode(c: float) -> float:
	c = clamp(c, 0.0, 1.0)
	return c * 12.92 if c <= 0.0031308 else 1.055 * pow(c, 1.0 / 2.4) - 0.055


# The same colour, as one of the three dichromacies receives it.
static func simulate(c: Color, s: int) -> Color:
	if s == Sight.TRICHROMAT:
		return c
	var collapse: Array = COLLAPSE_PROTAN
	if s == Sight.DEUTAN:
		collapse = COLLAPSE_DEUTAN
	elif s == Sight.TRITAN:
		collapse = COLLAPSE_TRITAN
	var v := Vector3(_linear(c.r), _linear(c.g), _linear(c.b))
	v = _mul(TO_LMS, v)
	v = _mul(collapse, v)
	v = _mul(FROM_LMS, v)
	return Color(_encode(v.x), _encode(v.y), _encode(v.z), c.a)


# Every eye this build claims to be readable by.
const ALL_SIGHT := [Sight.TRICHROMAT, Sight.PROTAN, Sight.DEUTAN, Sight.TRITAN]

# ---- the audit, as the interface actually prints it -------------------------
#
# A contrast rule nobody has written down is a rule that lasts until the next
# colour is picked. This is the list of pairs the interface really puts on
# screen, in one place, so the check is against the GAME rather than against a
# palette swatch — the two stop agreeing the moment somebody prints a hint in
# Dim on a Card instead of on the void, which is a change no palette-only test
# would ever notice.
#
# `why` is not documentation. It is what a failure prints, and the difference
# between a red line that names the screen and one that says "pair 14" is
# whether the next person can fix it.

const _PAL := []


# PALETTE, REACHED AT RUNTIME. Palette asks this file whether legibility is on
# and this file asks Palette for a colour by name; naming each other as global
# classes is a ring Godot refuses at load time. Deferring one side breaks it.
static func _palette():
	if _PAL.empty():
		_PAL.append(load("res://game/Palette.gd"))
	return _PAL[0]


static func named(role: String) -> Color:
	var p = _palette()
	match role:
		"Void": return p.VOID
		"Panel": return p.PANEL
		"PanelHi": return p.PANEL_HI
		"Card": return p.CARD
		"Ink": return p.INK
		"Dim": return p.dim()
		"Dim2": return p.dim2()
		"Inert": return p.INERT
		"Grid": return p.GRID
		"GridHi": return p.GRID_HI
		"Evert": return p.EVERT
		"Rust": return p.rust()
		"RustHi": return p.RUST_HI
		"Core": return p.core()
		"Fault": return p.fault()
		"Node": return p.NODE
		"Lock": return p.LOCK
		"Arc": return p.ARC
		"Trace": return p.TRACE
		"TraceFar": return p.TRACE_FAR
		"TraceNear": return p.TRACE_NEAR
		"LatticeFar": return p.LATTICE_FAR
		"LatticeNear": return p.lattice_near()
		# loud on purpose: a typo must not read as a pass
		_: return Color(1, 0, 1)


# Every ink-on-ground the interface prints, and the floor it has to clear.
# [ink, ground, floor, why]
const PRINTED := [
	["Ink", "Void", AAA_TEXT, "the wordmark, and most body text"],
	["Ink", "Panel", AAA_TEXT, "a label on a control plate"],
	["Ink", "PanelHi", AAA_TEXT, "a label on a raised plate"],
	["Ink", "Card", AAA_TEXT, "a label on a card"],
	["Ink", "Inert", AAA_TEXT, "the OFF half of a switch"],
	["Dim", "Void", AAA_TEXT, "the pitch under the masthead"],
	["Dim", "Panel", AAA_TEXT, "a hint under a calibrate row"],
	["Dim", "Card", AAA_TEXT, "a hint on a card"],
	["Dim", "PanelHi", AAA_TEXT, "a hint on a raised plate — the worst of the four grounds"],
	["Rust", "Void", AAA_TEXT, "ENGINE, and every vault caption"],
	["Rust", "Panel", AAA_TEXT, "the reading beside a slider"],
	["Void", "Rust", AAA_TEXT, "the primary's label — literally black"],
	["Core", "Void", AAA_TEXT, "the core readout"],
	["Fault", "Void", AAA_TEXT, "every refusal the interface has to explain"],
	["Node", "Void", AAA_TEXT, "the node count"],
	["Lock", "Void", AAA_TEXT, "the hint count"],
	["Arc", "Void", AAA_TEXT, "TO GO, on a perfect line"],
	["Trace", "Void", AAA_TEXT, "the trace readout"],

	# graphical objects rather than type: 1.4.11's 3:1, against the thing they
	# have to be told apart FROM rather than against the page
	["TraceFar", "LatticeNear", NON_TEXT, "the board's worst pair: farthest trace, nearest lattice"],
	["Rust", "Inert", NON_TEXT, "a slider's reading against its own track"],

	# DIM2 IS A MARK NOW, NOT TYPE, so it is measured against 1.4.11's 3:1 rather
	# than against a text floor — and the five places it was carrying words have
	# moved to Dim. It could not have cleared a text floor and stayed a tier: at
	# 4.5:1 on near-black it lands on top of Dim, and two type tiers that measure
	# the same are one type tier with two names.
	["Dim2", "Void", NON_TEXT, "an unlit pip, an unreachable cell, a locked vault's edge"],
	["Dim2", "PanelHi", NON_TEXT, "the same marks on a raised plate"],

	# AN EVERTER IS A MARK, AND A MARK IS A SHAPE HERE, NOT A COLOUR.
	#
	# The obvious pair to assert is the everter against the trace it sits on, and
	# it is the wrong one. Measured against Trace: node 1.42, lock 1.40, core
	# 1.31, the player 1.19. Every mark this game has ever drawn is invisible by
	# luminance alone on a lit cell, because the trace is bright by design and a
	# mark that cleared 3:1 under it would have to be nearly black — the window
	# between "3:1 below the trace" and "3:1 above the void" is 0.10 to 0.11 of
	# relative luminance and no usable hue lands in it.
	#
	# 1.4.1 is satisfied by FORM: these are glyphs with distinct silhouettes,
	# which is why Glyphs exists. So what is asserted is the same thing asserted
	# of every other mark — that it clears the ground it is drawn against.
	["Evert", "Void", NON_TEXT, "an everter on the board"],
	["GridHi", "Void", NON_TEXT, "the ring round an empty cell in the Forge"],
	["GridHi", "Panel", NON_TEXT, "the same ring on a control plate"],
]
