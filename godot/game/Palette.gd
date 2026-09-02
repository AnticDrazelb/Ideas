class_name Palette
# COLOUR IS DATA, NOT MATERIAL.
#
# Nine values, each with exactly one meaning, and nothing here is allowed a
# tenth. There is no "shade of" anything: a colour on screen is a claim about
# what a thing IS, so two things that are not the same thing never share one,
# and one thing never changes colour to suit a background.
#
# You are always arc. The way out is always core. Nodes are always node. A
# player who learns those on cube three must still be able to read cube nine
# hundred at a glance.
#
# THE VALUES ARE THE UNITY BUILD'S, HEX FOR HEX. They were matched to the APK's
# own :root block value by value there and they are copied here the same way;
# Godot's Color("#rrggbb") and Unity's ColorUtility.TryParseHtmlString agree
# exactly, so nothing about the picture is re-derived.

const VOID := Color("#000000")     # unrendered space. Not dark blue. Black.
const VOID2 := Color("#050709")
const VOID3 := Color("#0b0f16")

const PANEL := Color("#0d1117")
const PANEL_HI := Color("#161c26")
const CARD := Color("#141924")
const SCRIM := Color(0, 0, 0, 0.72)

const GRID := Color("#1e293b")     # inactive lattice

# THE RING ROUND AN EMPTY CELL IN THE FORGE, AND YOU COULD NOT SEE IT.
#
# This was #334155 — 2.03:1 against the plate, and 1.41:1 against the Grid fill
# it outlines. In a LEVEL EDITOR, whose entire task is putting cells onto a
# grid, the grid was the least visible thing on the screen. It is very likely
# part of why a first-time Forge cube verified 101 times in 400 before the
# editor started saying which cells are faces.
#
# It is 3.37:1 on the plate now, which is 1.4.11's floor for a component you
# have to perceive to operate. The fill under it stays where it is: an inactive
# cell is meant to recede, and the ring is what says the cell is there at all.
const GRID_HI := Color("#4d6280")
const TRACE := Color("#38bdf8")    # active circuit, walkable
const TRACE_LO := Color("#0c4a6e")
const ARC := Color("#22d3ee")      # you, data flow, a perfect collapse
const NODE := Color("#a3e635")     # collectible data
const LOCK := Color("#facc15")     # closed gate

const INK := Color("#e2e8f0")

# ---- the five that move, and the mode that moves them ----------------------
#
# THE SHIPPED VALUES ARE THE DEFAULT AND THEY STAY EXACT. But nine of the pairs
# this interface actually prints are below WCAG AAA and two are below AA. DIM2
# on the void is 2.03:1 — that is the footer on the title screen and every field
# placeholder in the game. Black on the primary's rust is 5.90:1, and that is
# the one button every screen is built around. Those are measurements, not
# opinions, and the answer a shipping game gives is a MODE: the default stays
# the picture that was matched, and a player who needs the contrast gets it
# without being handed a different game.
#
# Each legible value is the SMALLEST step in lightness, at the same hue and
# saturation, that clears 7:1 against all four of the dark grounds this
# interface uses — void, panel, panelHi and card. They were solved for rather
# than picked, which is why they look like nothing in particular.
#
# AND THE DEFAULT MOVED TOO, ONCE, FOR A REASON THAT IS NOT TASTE. Dim was
# #64748b, which is 4.41:1 on black and 3.70:1 on a card — under AA, in the
# colour that carries every body paragraph in this game. Dim2 was #334155 at
# 2.03:1 and was setting five pieces of real type. AAA behind a mode is a
# legitimate shipping choice; AA in the default is not one, and matching a
# reference that fails it only reproduces the failure.
const RUST_SHIPPED := Color("#ea580c")
const RUST_LEGIBLE := Color("#f68950")
const CORE_SHIPPED := Color("#f97316")
const CORE_LEGIBLE := Color("#fa8838")
const FAULT_SHIPPED := Color("#ef4444")
const FAULT_LEGIBLE := Color("#f58686")
const DIM_SHIPPED := Color("#75859b")
const DIM_LEGIBLE := Color("#9ba7b7")
const DIM2_SHIPPED := Color("#516888")
const DIM2_LEGIBLE := Color("#96a8c0")


# The instrument itself — as a mark, as type, and as the one filled surface in
# the interface.
#
# ONE VALUE SERVES BOTH BECAUSE OF AN IDENTITY, not because of luck. A colour
# that clears 7:1 against black as type is exactly a colour that black clears
# 7:1 against as a surface: the ratio is symmetric. So the same lift that makes
# the vault name readable makes the primary's literal black label readable, and
# the primary does not need — and must not have — a second rust of its own.
#
# Neither black nor white clears 7:1 on the SHIPPED rust: white is worse, at
# 3.56:1 against black's 5.90. No choice of ink fixes that button. The surface
# is what has to move.
static func rust() -> Color:
	return RUST_LEGIBLE if Access.legible() else RUST_SHIPPED


# The rust one step up. 7.55:1 on the worst ground already, so it does not move.
const RUST_HI := Color("#fb923c")


static func core() -> Color:
	return CORE_LEGIBLE if Access.legible() else CORE_SHIPPED


static func fault() -> Color:
	return FAULT_LEGIBLE if Access.legible() else FAULT_SHIPPED


static func dim() -> Color:
	return DIM_LEGIBLE if Access.legible() else DIM_SHIPPED


# THE QUIETEST MARK IN THE INTERFACE — AND IT IS NOT TYPE ANY MORE.
#
# It was: the title footer, a field placeholder, a locked vault's name, the
# HUD's vault caption. All five were 2.03:1 on black, and the reason they cannot
# simply be lifted to AA is arithmetic rather than taste. At 4.5:1 on near-black
# this lands at 5.56:1 against Dim's 5.58 — two type tiers that measure the same
# are one type tier with two names. So the words went to Dim, where they are
# read, and this kept the job it was always better at: an unlit pip, an
# unreachable cell, the edge of a vault you have not opened.
static func dim2() -> Color:
	return DIM2_LEGIBLE if Access.legible() else DIM2_SHIPPED


# THE SAME SLATE, AS A SURFACE RATHER THAN AS TYPE — and it is a separate name
# because it is a separate job. The shipped build spent one token on both the
# quiet mark and the inert half of a control: the track behind a slider, the
# body of a switch that is off. A mark has to get LIGHTER to be told from a dark
# panel; a track has to stay DARK or the rust fill on top of it stops being
# visible. #313f52 is eight thousandths of lightness down from the old shared
# value, which is invisible and is exactly the step that takes the rust fill
# against it from 2.91:1 to 3.00 — over 1.4.11's floor.
const INERT := Color("#313f52")

# ---- the two depth ramps --------------------------------------------------
#
#   lattice   #1a2332 (lum 34) .. #2b3545 (lum 52)
#   trace     #0b8ed0 (lum 119) .. #38bdf8 (lum 165)
#
# A gap of 67 between the materials, 46 of spread within the trace, and the near
# trace is exactly the value the brief names. The whole board is read off one
# property: A TRACE IS BRIGHTER THAN THE LATTICE, ALWAYS, at every depth — so
# "may I stand there" survives a dim screen and a colour-blind eye.
#
# That last claim is simulated and asserted rather than believed; see
# Access.simulate and the band test in tests/run.gd. It holds under all three
# dichromacies at every vault in both palettes, with the narrowest margin under
# deuteranopia at the bottom of the ladder.
const TRACE_FAR := Color8(11, 142, 208, 255)
const TRACE_NEAR := Color8(56, 189, 248, 255)
const LATTICE_FAR := Color8(26, 35, 50, 255)

const LATTICE_NEAR_SHIPPED := Color8(43, 53, 69, 255)
const LATTICE_NEAR_LEGIBLE := Color8(35, 43, 56, 255)


# THE COLOUR THAT DECIDES THE WHOLE BOARD, AND IT WAS MEASURED IN THE WRONG
# PLACE.
#
# The worst pair on the board is the farthest trace against the nearest lattice,
# where the two ramps come closest, and the audit measured it exactly once —
# against the UN-AGED lattice, which is the best case it ever has. The lattice
# corrodes down the ladder, so it walks toward the trace: the pair that read
# 2.56:1 in vault one was 2.30:1 in vault thirty, and the legibility mode did not
# help, because the corroded end of the ramp is shared. Thirty vaults, both
# palettes, every one of them under 1.4.11's 3:1, and nothing said so — the band
# check runs at every vault but asks whether the trace is BRIGHTER, not whether
# you can tell the two apart.
#
# So both ends of the ramp sank: this one, and LATTICE_NEAR_DEEP with it. The
# tightest ratio anywhere on the ladder is 3.07:1 now, and the game's own
# luminance band went from 0.1362 to 0.1692 — a quarter wider — as a side
# effect. It costs the trace nothing, because the trace does not move.
#
# AND THAT 3.07 WAS MEASURED WITH ONE PAIR OF EYES.
#
# It is the trichromat figure. Under deuteranopia the same pair reads 2.85 in
# vault one and 2.76 in vault ten — under 1.4.11's 3:1 on every rung of the
# ladder, in the shipped palette, for the one distinction the whole board is
# read off. The note above this one says in as many words that the legibility
# mode does not help because the corroded end of the ramp is shared, and then
# both palettes went on sharing it: by vault ten the legible ramp has lerped
# onto LATTICE_NEAR_DEEP and measures exactly what the shipped one does.
#
# Nothing caught it because nothing asked. Palette.band_gap is written, is
# documented as "the band guarantee, asserted rather than assumed", and had no
# callers at all — two hundred and thirty tests and not one of them read it.
# tests/run.gd asks now, at every vault, in both palettes, through all four
# eyes, and it asks for the ratio rather than only for the sign.
#
# All three near ends are down twenty per cent of LINEAR light, which is a hue-
# preserving move and keeps the fresh and corroded ends luminance-matched to
# each other exactly as before. Worst pair on the ladder: 3.06:1, under
# deuteranopia at vault ten. The trace still does not move.
static func lattice_near() -> Color:
	return LATTICE_NEAR_LEGIBLE if Access.legible() else LATTICE_NEAR_SHIPPED


static func luminance(c: Color) -> float:
	return 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b


# The band guarantee, asserted rather than assumed. The palette is four numbers
# somebody could edit, and a silent regression here does not look like a bug —
# it looks like the game got hard.
static func band_gap() -> float:
	return luminance(TRACE_FAR) - luminance(lattice_near())


# ---- swapping an interface that is already on screen ----------------------
#
# Every screen here is built once, in code, with its colours baked into the
# nodes as they are made. That is the right shape for an interface with no
# scenes and the wrong shape for a setting that has to take effect while the
# screen it changes is on top of the stack.
#
# Rebuilding the canvas would work, and would throw away scroll positions, input
# focus and whatever the player was in the middle of. So the two palettes are
# treated as what they are — a bijection — and the interface is walked, swapping
# one set for the other. Nothing in the legible set collides with anything in
# the shipped set, so a colour already on screen says without ambiguity which
# palette it came from, and the walk is idempotent and needs no bookkeeping.
#
# THE TWO LISTS ARE STILL THE POINT. Type lives on a Label and surfaces live on
# a ColorRect or a StyleBox, which is the distinction these lists make.

# The roles that appear as TYPE, and only ever as a label's colour.
const TYPE_SWAPS := [
	[RUST_SHIPPED, RUST_LEGIBLE],
	[CORE_SHIPPED, CORE_LEGIBLE],
	[FAULT_SHIPPED, FAULT_LEGIBLE],
	[DIM_SHIPPED, DIM_LEGIBLE],
	[DIM2_SHIPPED, DIM2_LEGIBLE],
]

# The roles that appear as a SURFACE. Only rust: it is the one filled shape in
# the interface, and INERT deliberately does not move.
const FILL_SWAPS := [
	[RUST_SHIPPED, RUST_LEGIBLE],
]

# ---- the machine corrodes as you go deeper --------------------------------
#
# Ten vaults, each with a name, a difficulty and its own room, and the board was
# the same four numbers from cube one to cube a hundred and twenty. Progression
# was a number in the corner and nothing else.
#
# THE TRACE DOES NOT MOVE, AND THAT IS THE RULE THIS OBEYS. It is the signal —
# the one colour that means "you can stand here" — and every contrast assertion
# in the audit is anchored to it. What ages is the LATTICE: the inert body of
# the machine, which has no job except to be darker than the trace. So the
# circuit stays exactly as legible as it was at cube one and the metal it is
# printed on goes over to rust, which is also the same thing that happened to
# the case.
#
# The two ends are luminance-matched to within a hundredth, so the band
# guarantee is not being spent on a mood.
const LATTICE_FAR_DEEP := Color8(43, 37, 35, 255)
const LATTICE_NEAR_DEEP := Color8(62, 51, 44, 255)


# How far through the ladder a vault sits, 0 at the first and 1 at the last.
# The drift is meant to be something you notice having happened, not something
# you watch.
static func vault_age(band: int) -> float:
	return clamp(band / float(max(1, Vaults.RANKED_VAULTS - 1)), 0.0, 1.0)


static func lattice_far_of(band: int) -> Color:
	return LATTICE_FAR.linear_interpolate(LATTICE_FAR_DEEP, vault_age(band))


static func lattice_near_of(band: int) -> Color:
	return lattice_near().linear_interpolate(LATTICE_NEAR_DEEP, vault_age(band))


# The everter, and it is deliberately NOT on either depth ramp.
#
# Trace runs blue and lattice runs slate, and the whole board is read off the one
# property that a trace is brighter than the lattice at every depth. An everter
# is neither material: it is the control that swaps which face of the solid you
# are standing on, and putting it on a ramp would make it read as "a trace that
# is slightly wrong". It is its own hue for the same reason a node and a lock
# are.
#
# Violet because it is the only unused corner of the wheel this palette occupies
# — rust, amber, lime, cyan and blue are all spoken for — and because it stays
# distinguishable from the trace under all three dichromacies.
const EVERT := Color("#a78bfa")

# THE SCHEMATIC. What the lattice is drawn in while it is being LOOKED THROUGH
# rather than played on.
#
# A held MATRIX is not a view of the board, it is a view of the MACHINE: the
# fill goes and every cell becomes a wire box, at every depth, with nothing
# occluding anything. That is a different job from the settled board — nobody is
# asking "can I stand there" with their thumb held down — and it gets its own
# two colours, cool where the housing is rust, because a schematic drawn in the
# same ink as the object is a schematic nobody can tell apart from it.
#
# These are ADDITIVE and land on the void, so they are the light itself rather
# than a surface colour — which is why they are allowed to be this bright when
# nothing else in the palette is.
#
# WIRE_LATTICE is a periwinkle rather than the everter's violet on purpose: an
# everter is a walk type, so its own wire is drawn in WIRE_TRACE, but its GLYPH
# is violet and it is sitting over this. Thirty-one degrees of hue between them
# is what stops "the violet marker" and "the violet lattice" being the same read
# at a glance — the shape settles it either way, but the shape should not have
# to.
const WIRE_TRACE := Color("#9fe8ff")
const WIRE_LATTICE := Color("#8098d8")

# THE TRIGGER, and it is not on either depth ramp for the same reason the
# everter is not: it does not change what a cell IS, it changes which board you
# are standing on. A green because it is the last unspoken corner of this wheel,
# and because it has to be told apart from the everter's violet at a glance, the
# two being the only marks that rearrange the board rather than recolour it.
const TRIGGER := Color("#34d399")


# The flat-board colour of a cell — the same arithmetic the shader does per
# pixel.
static func tile(t: int, d: int, n: int, band: int = 0) -> Color:
	if t == Level.EVERTER:
		return EVERT
	if t == Level.TRIGGER or t == Level.TRIGGER2:
		return TRIGGER
	var near := clamp(d / float(max(1, n - 1)), 0.0, 1.0)
	var walk := Level.is_walk_type(t)
	var a := TRACE_FAR if walk else lattice_far_of(band)
	var b := TRACE_NEAR if walk else lattice_near_of(band)
	return a.linear_interpolate(b, near)
