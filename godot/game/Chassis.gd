class_name Chassis
# THE MACHINE YOU ARE HOLDING.
#
# Everything else in this interface is a reading. This is the thing the readings
# are mounted in: a salvaged steel case, rusted through at the corners, bolted at
# four points, with the glass sunk into a machined recess.
#
# It is not decoration, and the argument for it is the same one the aperture
# made. The fiction is that you are inside a broken machine looking at its
# diagnostics. Every screen in this game already reads as an instrument panel —
# framed plates, hairlines, one lit control, a monospace face — and then that
# panel was floating on nothing. Chrome with no chassis is a costume; give it a
# housing and the same pixels become a device.
#
# THE ONE IMPORTED ASSET IN THE PROJECT, AND IT IS WORTH SAYING WHY.
#
# Everything else here is generated: the glyphs, the frames, the glow, the icon,
# the sound. That is a real principle and it has paid for itself — a game with no
# assets has nothing to load, nothing to unhook, and nothing in a diff that a
# person cannot read. This is the exception, and the exception is honest: the
# housing went through four generated passes — flat grey, corrugated grey,
# pillowy grey, and a fairly good milled bezel — and not one of them was the
# object in the reference. Rust does not come out of value noise. Neither does
# forty years of somebody else's handling.
#
# So the case is a photograph of the thing it is meant to be, cut to its own
# silhouette, with the glass taken out of the middle. The mock-up it came from
# and the Python the cut was done in are both in the repository, under
# unity/tools/chassis, because an asset nobody can regenerate is exactly the
# unreviewable blob this project spent its whole life avoiding.
#
# TWO RULES SURVIVE FROM THE GENERATED VERSION, AND THEY STILL BIND.
#
# 1. NO TYPE EVER SITS ON THE METAL. The whole access audit is a set of contrast
#    measurements against four dark grounds, and a rusted steel ground would
#    invalidate every one of them. The metal is a border; the words live on the
#    glass, which is as dark as it ever was. This is enforced by construction
#    rather than by care: the HUD and every screen are inset to the opening, so
#    there is nowhere on the metal to put a word.
#
# 2. IT IS NEVER BEHIND THE BOARD. The interface draws over the viewport, so
#    anything painted across the middle would paint over the cube. The case is
#    twelve pieces around the edge — eight corner arms and four sides — and the
#    reason it is twelve rather than eight is exactly this rule: a square piece
#    at each corner would be simpler and would hang a transparent quad over four
#    corners of the board. There is no quad over the middle at all, not even a
#    clear one.

const TEX_PATH := "res://assets/chassis.png"

const _SPEC := []
const _TEX := []

# LAYOUT ASKS THIS FILE HOW DEEP THE BEZEL IS AND THIS FILE ASKS THE KIT FOR A
# CANVAS, and the kit measures itself against Layout — a ring of three that Godot
# refuses to load, taking the whole interface down with it. The one edge that
# only exists to build twelve rectangles is the one that gets deferred.
const _KIT := []


static func kit():
	if _KIT.empty():
		_KIT.append(load("res://ui/UiKit.gd"))
	return _KIT[0]


# Loaded once and kept. A function rather than a constant because the whole point
# of the spec is that a different case can be dropped in.
static func spec() -> ChassisSpec:
	if _SPEC.empty():
		var s := load_spec()
		if not s.consistent():
			# A crop that disagrees with the art it describes places all twelve
			# pieces wrong, and does it subtly — a few pixels of seam rather than
			# a visible fault. Say so.
			push_warning("Chassis: chassis.json says the art is %dx%d but its crop is %dx%d."
					% [s.art_w, s.art_h, int(s.crop.size.x), int(s.crop.size.y)])
		_SPEC.append(s)
	return _SPEC[0]


# READ THE NUMBERS BESIDE THE PICTURE. It is here rather than on ChassisSpec
# because GDScript will not let a class name itself, and a factory returning a
# ChassisSpec has to say the word.
static func load_spec() -> ChassisSpec:
	var s := ChassisSpec.new()
	var f := File.new()
	if f.open(ChassisSpec.PATH, File.READ) != OK:
		return s                                   # the shipped case's own numbers
	var text := f.get_as_text()
	f.close()
	var j := JSON.parse(text)
	if j.error != OK or not (j.result is Dictionary):
		push_warning("Chassis: " + ChassisSpec.PATH + " did not parse; using the shipped case's numbers.")
		return s
	s.read(j.result)
	return s


# Forget the loaded spec.
static func reload() -> void:
	_SPEC.clear()
	_TEX.clear()


static func texture():
	if _TEX.empty():
		var t = load(TEX_PATH)
		if t == null:
			push_warning("Chassis: " + TEX_PATH + " is missing; the case will not be drawn.")
		_TEX.append(t)
	return _TEX[0]


# ---- how thick the case is, and it is not one number ----------------------
#
# A TEN-INCH TABLET DOES NOT HAVE A TWO-INCH BEZEL.
#
# The canvas scaler keeps the canvas AREA constant at every resolution, which is
# what makes one set of authored offsets land correctly on every phone — and it
# also means a canvas unit is the same FRACTION of the display everywhere. The
# case is 23.5% of the screen on a five-inch handheld and 23.5% of the screen on
# a thirteen-inch panel, and only the first of those is a machine you are holding.
# On the second it is a picture of a machine with the game inside it.
#
# So the bezel is the authored thickness on anything handheld and thins from
# there. It is a function of the display's DIAGONAL rather than its pixels,
# because pixels are what the scaler already normalised away.

# Where the case stops being a thing in your hands, and where it is a panel on a
# desk. Between them it thins in a straight line.
const HANDHELD_INCHES := 6.5
const PANEL_INCHES := 10.0
const PANEL_BEZEL := 0.5


# Pure, so the harness can hold the policy without a display to measure.
static func bezel_factor(inches: float) -> float:
	# A PLATFORM THAT WILL NOT SAY GETS THE CASE THE GAME WAS BUILT WITH. Zero is
	# what diagonal_inches answers when the DPI it was given is not a figure any
	# handheld reports — an X server's nominal 96, say — and guessing from it
	# would thin the bezel on every desktop the game was ever tested on.
	if inches <= 0.0:
		return 1.0
	if inches <= HANDHELD_INCHES:
		return 1.0
	var k: float = clamp((inches - HANDHELD_INCHES) / (PANEL_INCHES - HANDHELD_INCHES), 0.0, 1.0)
	return lerp(1.0, PANEL_BEZEL, k)


# The display's diagonal, or zero when the number offered is not believable.
#
# THE WINDOW IS READ HERE RATHER THAN THROUGH LAYOUT, and it has to be: Layout
# asks Chassis for its insets on every rect it computes, so a call back the other
# way is a ring the engine refuses to load. It is the same two lines either way.
static func window_size() -> Vector2:
	var tree := Engine.get_main_loop()
	if tree != null and tree is SceneTree:
		var root: Viewport = (tree as SceneTree).root
		if root != null:
			return root.get_visible_rect().size
	return Vector2(720, 1280)


static func diagonal_inches() -> float:
	var dpi := OS.get_screen_dpi()
	if dpi < 120 or dpi > 800:
		return 0.0
	var s := window_size()
	return sqrt(s.x * s.x + s.y * s.y) / float(dpi)


# The exchange rate from the art's own pixels to canvas units, as it stands on
# THIS display. Everything in the case is measured through it.
static func scale_now() -> float:
	return spec().scale * bezel_factor(diagonal_inches())


# ---- what the rest of the interface is allowed to know ---------------------

# Display edge to glass, in canvas units. Everything readable lives inside these.
static func inset_left() -> float:
	return spec().left * scale_now()


static func inset_right() -> float:
	return spec().right * scale_now()


static func inset_top() -> float:
	return spec().top * scale_now()


static func inset_bottom() -> float:
	return spec().bottom * scale_now()


static func corner() -> float:
	return spec().corner * scale_now()


# ---- and the case is not inert ---------------------------------------------
#
# A QUARTER OF THE DISPLAY WAS DOING NO WORK. Twelve pieces of photographed metal
# that never changed, on a screen where the board is barely half. The case is the
# machine the whole game is set inside; it can afford to behave like one.
#
# Two things reach it. The LADDER corrodes it — the same vault age the lattice
# already wears, so a player ten chapters in is holding a visibly worse machine
# than the one they started on, and never had to read a number to know it. And
# the two events that are about the MACHINE rather than about the board — a plate
# turning the world inside out, and the core taking you — light its inner edge.
const _FIT := []


static func attach(fit) -> void:
	_FIT.clear()
	_FIT.append(fit)


static func _fit():
	return _FIT[0] if not _FIT.empty() and is_instance_valid(_FIT[0]) else null


# How far along the ladder the case has corroded, 0 at the first vault and 1 at
# the last. Called where the board's own palette is pushed, from one number.
static func set_band(band: int) -> void:
	var f = _fit()
	if f != null:
		f.set_age(Palette.vault_age(band))


# One event on the metal. Scaled by the light setting like every other flash in
# the game — a player who asked for none gets none.
static func pulse(col: Color, amount: float) -> void:
	var f = _fit()
	if f != null:
		f.flash(col, amount * Access.light_amount())


# The plate's five seconds, as a light rather than as a number. 0 is off.
static func set_clock(k: float) -> void:
	var f = _fit()
	if f != null:
		f.set_clock(k * Access.light_amount())


static func tick(dt: float) -> void:
	var f = _fit()
	if f != null:
		f.step(dt)


# The housing, on its own layer behind everything.
static func build() -> CanvasLayer:
	var c: CanvasLayer = kit().canvas("Chassis", 5)
	var root: UiRect = kit().rect(kit().root_of(c), "panel",
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	root.set_script(load("res://game/ChassisFit.gd"))
	root.call("setup")
	return c
