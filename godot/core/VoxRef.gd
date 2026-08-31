class_name VoxRef
extends Reference
# A MUTABLE HANDLE ON A VOXEL ARRAY, and it exists because of one difference
# between C# and GDScript that would otherwise have been silent.
#
# In C# a `char[]` is a reference: Carve takes one, writes into it, and the
# caller sees the change. Godot's PoolByteArray is a VALUE with copy-on-write —
# passing one to a function and mutating it there changes a copy and nothing
# else, with no error anywhere. The carve is built entirely out of "write
# footing into the solid and carry on", so every leg would have been thrown
# away and the generator would have produced a cube it had never actually
# carved.
#
# So the array travels inside an object. One field, no behaviour: the point is
# only that an object is a reference and a pool array is not.
var cells: PoolByteArray


func _init(c: PoolByteArray) -> void:
	cells = c


# A ZERO-FILLED PoolByteArray IS NOT ZERO-FILLED. Godot's resize() leaves
# whatever was in the buffer, so an array that is supposed to be all void has to
# be written all void — the C# `new char[]` gave that for free and this does
# not. Every array in the carve starts here for exactly that reason.
#
# It is a plain function rather than a static factory because GDScript 3.5 will
# not let VoxRef name VoxRef; callers say `VoxRef.new(VoxRef.void_cells(n))`.
static func void_cells(size: int, with: int) -> PoolByteArray:
	var c := PoolByteArray()
	c.resize(size)
	for i in range(size):
		c[i] = with
	return c
