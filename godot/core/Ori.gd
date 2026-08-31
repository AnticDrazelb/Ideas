class_name Ori
# AN ORIENTATION IS A WORLD-TO-VIEW BASIS: r is the world direction that points
# screen-right, u points screen-up, and f points TOWARD THE CAMERA.
#
# All three are signed unit axes, so every orientation is an integer signed
# permutation and nothing ever drifts — which is what lets Projection.view_of
# and Projection.world_of be exact round trips, and what lets a level's
# identity be "the smallest of the twenty-four ways it can be written down".
#
# A CELL COORDINATE IS A Vector3 HOLDING INTEGERS, deliberately, and never a
# float in disguise. Godot's Vector3 is three 32-bit floats; every value this
# game ever puts in one is a whole number between -1 and 8, all of which are
# exact in a float, so equality and dictionary hashing behave like the C#
# Int3 struct they replace. Anything that indexes with a component says int()
# at the point of use rather than trusting the conversion.

var r: Vector3
var u: Vector3
var f: Vector3


func _init(rr: Vector3 = Vector3(1, 0, 0), uu: Vector3 = Vector3(0, 1, 0), ff: Vector3 = Vector3(0, 0, 1)) -> void:
	r = rr
	u = uu
	f = ff

# THE IDENTITY IS `Ori.new()`, and there is no id() beside it: GDScript 3.5
# refuses a class that names itself ("using own name in class file is not
# allowed"), so the defaults above ARE the identity and every call site says
# Ori.new() where the C# said Ori.Id. A fresh object each time is also the
# right thing — an orientation is passed around freely and a shared mutable one
# would be a bug waiting for its first writer.


func key() -> String:
	return "%d,%d,%d;%d,%d,%d;%d,%d,%d" % [
		int(r.x), int(r.y), int(r.z),
		int(u.x), int(u.y), int(u.z),
		int(f.x), int(f.y), int(f.z)]
