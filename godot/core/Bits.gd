class_name Bits
# THIRTY-TWO-BIT ARITHMETIC ON A SIXTY-FOUR-BIT INT.
#
# C# could say `unchecked` and let the hardware wrap. GDScript's int is 64 bits
# wide and never wraps, so every operation that has to agree with the original
# JavaScript — which is all of them, because every cube in the game is a pure
# function of its level number — has to be told where the word ends.
#
# It is its own file rather than three statics inside Rng because Rng's inner
# State class needs them, and GDScript 3.5 refuses a nested class that names
# its own outer class: "using own name in class file is not allowed".

# Wrap a value into the signed 32-bit range, which is how C# stores an int.
static func s32(x: int) -> int:
	x = x & 0xFFFFFFFF
	return x - 0x100000000 if x >= 0x80000000 else x


# JavaScript's `x >>> n`: a logical shift on the 32-bit pattern.
static func ushr(x: int, n: int) -> int:
	return (x & 0xFFFFFFFF) >> n


# JavaScript's `Math.imul`: a wrapping 32-bit signed multiply.
#
# Split rather than straight. Two unsigned 32-bit values multiply to as much as
# 1.8e19, which overflows a signed 64-bit int — so the left operand is cut into
# halves and the high half is masked to the sixteen bits that survive the wrap
# before it is shifted, which keeps every intermediate under 2^49.
static func imul(a: int, b: int) -> int:
	a = a & 0xFFFFFFFF
	b = b & 0xFFFFFFFF
	var al := a & 0xFFFF
	var ah := (a >> 16) & 0xFFFF
	return s32((al * b + (((ah * b) & 0xFFFF) << 16)) & 0xFFFFFFFF)
