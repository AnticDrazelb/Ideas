class_name ChassisSpec
extends Reference
# THE MEASUREMENTS OF WHATEVER CASE IS CURRENTLY IN THE MACHINE.
#
# The C# holds these in a ScriptableObject so the editor can draw an inspector
# for them and you can drag a slider and watch the bezel move. There is no editor
# tool in this port and inventing one would be inventing a feature, so they are
# JSON beside the picture they measure — which keeps the property that actually
# matters: the numbers live with the asset, and swapping the case is dropping in
# two files rather than editing code with a calculator open.
#
# EVERY LENGTH IS IN THE ART'S OWN PIXELS except `scale`, which is the exchange
# rate to canvas units and the only number here that is a decision rather than an
# observation.

const PATH := "res://assets/chassis.json"

# the picture
var art_w := 461
var art_h := 1018

# display edge to glass, per side. The case is not symmetrical and pretending
# otherwise puts a screen's own background over the metal on one edge. Four
# numbers, always.
var left := 38.0
var right := 38.0
var top := 60.0
var bottom := 62.0

# THE NINE-SLICE. How much of the art a corner piece takes. Both cuts have to
# land in a stretch of edge whose profile is CONSTANT — past the notch on the
# top, above the waist on the side — because what stretches has to be featureless
# along the direction it stretches in.
var corner := 160.0

# How deep each piece has to be to hold all the metal in its stretch. The arms
# are wider than the bezel by the opening's corner radius, because the metal
# reaches further in where the glass turns the corner.
var arm_w := 58.0
var arm_h := 80.0
var side_w := 44.0
var band_h := 66.0

# ART PIXELS TO CANVAS UNITS, FIXED rather than whatever makes the case fit.
# Scaling the art to the display would make the bezel thicker on a tall phone
# than on a short one and move the layout underneath it — a bad trade for a
# housing whose whole job is to be the one thing that never moves.
var scale := 1.25

# how it was cut, so it can be cut again
var crop := Rect2(50, 6, 461, 1018)
var glass := Rect2(88, 66, 384, 895)
var glass_radius := 15.0
var dark_below := 9.0
var source_rows := 1024


# THE LOADER LIVES IN Chassis, not here, and that is GDScript's rule rather than
# a choice: a class may not name itself, so a static factory returning a
# ChassisSpec cannot be written inside ChassisSpec.gd. See Chassis.load_spec.


# Take the numbers out of a parsed chassis.json. Anything absent keeps the
# shipped case's value, so a file that only overrides the bezel is a valid file.
func read(d: Dictionary) -> void:
	art_w = int(d.get("artW", art_w))
	art_h = int(d.get("artH", art_h))
	left = float(d.get("left", left))
	right = float(d.get("right", right))
	top = float(d.get("top", top))
	bottom = float(d.get("bottom", bottom))
	corner = float(d.get("corner", corner))
	arm_w = float(d.get("armW", arm_w))
	arm_h = float(d.get("armH", arm_h))
	side_w = float(d.get("sideW", side_w))
	band_h = float(d.get("bandH", band_h))
	scale = float(d.get("scale", scale))
	glass_radius = float(d.get("glassRadius", glass_radius))
	dark_below = float(d.get("darkBelow", dark_below))
	source_rows = int(d.get("sourceRows", source_rows))
	if d.has("crop") and d["crop"] is Array and d["crop"].size() == 4:
		var c: Array = d["crop"]
		crop = Rect2(c[0], c[1], c[2] - c[0], c[3] - c[1])
	if d.has("glass") and d["glass"] is Array and d["glass"].size() == 4:
		var g: Array = d["glass"]
		glass = Rect2(g[0], g[1], g[2] - g[0], g[3] - g[1])


# The one that is checked rather than trusted: a spec whose crop does not match
# the art it describes will place all twelve pieces wrong, and it will do it
# subtly — a few pixels of seam rather than a visible fault.
func consistent() -> bool:
	return art_w > 0 and art_h > 0 \
			and abs(art_w - crop.size.x) < 0.001 \
			and abs(art_h - crop.size.y) < 0.001
