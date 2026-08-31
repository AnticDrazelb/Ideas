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


# ---- what the rest of the interface is allowed to know ---------------------

# Display edge to glass, in canvas units. Everything readable lives inside these.
static func inset_left() -> float:
	return spec().left * spec().scale


static func inset_right() -> float:
	return spec().right * spec().scale


static func inset_top() -> float:
	return spec().top * spec().scale


static func inset_bottom() -> float:
	return spec().bottom * spec().scale


static func corner() -> float:
	return spec().corner * spec().scale


# The housing, on its own layer behind everything.
static func build() -> CanvasLayer:
	var c: CanvasLayer = kit().canvas("Chassis", 5)
	var root: UiRect = kit().rect(kit().root_of(c), "panel",
			Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	root.set_script(load("res://game/ChassisFit.gd"))
	root.call("setup")
	return c
