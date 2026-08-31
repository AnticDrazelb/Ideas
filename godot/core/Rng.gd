class_name Rng
# THE GENERATOR'S RANDOM SOURCE, ported so that it produces the exact same
# stream of doubles as the C# port and the JavaScript original before it.
#
# This matters more here than anywhere else in the port. Every cube in the game
# is a pure function of its level number, so a single divergent draw means cube
# 4,127 in Godot is a different puzzle from cube 4,127 in Unity, and the "same
# cube for everyone, no server" promise the whole design rests on quietly stops
# being true.
#
# Two rules carried over verbatim from the source:
#
#  * NEVER SHUFFLE WITH A COMPARATOR. The JS comments record a real bug here:
#    `[0,1,2,3].sort(() => rng() - 0.5)` is an inconsistent comparator, so how
#    many numbers it pulls off the stream is a property of the engine's sort,
#    not of the seed. Fisher-Yates consumes exactly one number per element, on
#    every platform, forever.
#
#  * ALL ARITHMETIC IS 32-BIT AND WRAPPING. See Bits, which is where the three
#    operations that make that true live.

# Forwarded so call sites read like the C# — Daily asks for Rng.imul and
# Rng.ushr by name, exactly as it does there.
static func imul(a: int, b: int) -> int:
	return Bits.imul(a, b)


static func ushr(x: int, n: int) -> int:
	return Bits.ushr(x, n)


static func s32(x: int) -> int:
	return Bits.s32(x)


# mulberry32. An object rather than a function so each generator carries its
# own state and call sites read like the original `rng()`.
class State:
	var _a: int

	# `seed` is a GDScript built-in and cannot be a parameter name here.
	func _init(seed_in: int) -> void:
		_a = Bits.s32(seed_in)

	# One draw in [0, 1), matching mulberry32 bit for bit.
	func next() -> float:
		_a = Bits.s32(_a + 0x6D2B79F5)
		var t := Bits.imul(_a ^ Bits.ushr(_a, 15), 1 | _a)
		t = (t + Bits.imul(t ^ Bits.ushr(t, 7), 61 | t)) ^ t
		return float((t ^ Bits.ushr(t, 14)) & 0xFFFFFFFF) / 4294967296.0

	# Fisher-Yates over a small int array. One draw per element.
	func shuffle(a: Array) -> void:
		for i in range(a.size() - 1, 0, -1):
			var j := int(floor(next() * (i + 1)))
			var t = a[i]
			a[i] = a[j]
			a[j] = t


static func mulberry32(seed_in: int) -> State:
	return State.new(seed_in)


# Level number to generator seed.
#
# The JS is `(level * 2654435761) ^ 0x9e3779b9`, and the multiply there happens
# in double precision before `^` truncates it to int32 — so the product is
# taken at full width and then wrapped, not computed in 32-bit arithmetic that
# could differ. For every level this game can reach the product is exact.
#
# Returns the value as an UNSIGNED 32-bit number: every caller feeds it straight
# back into State, which wraps it, and Vaults.level_name indexes with it and
# must not index negatively. That was a real bug in the JS — a signed shift
# there named half the cubes past level 40 "undefined".
static func hash_seed(level: int) -> int:
	var h := Bits.s32((level * 2654435761) & 0xFFFFFFFF) ^ Bits.s32(0x9e3779b9)
	h = Bits.imul(h ^ Bits.ushr(h, 15), Bits.s32(2246822507))
	h = Bits.imul(h ^ Bits.ushr(h, 13), Bits.s32(3266489909))
	return (h ^ Bits.ushr(h, 16)) & 0xFFFFFFFF
