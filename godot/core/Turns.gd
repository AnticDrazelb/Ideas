class_name Turns
# THE FOUR QUARTER-TURNS, and the derivation of each one's effect on the basis.
#
# Dragging RIGHT pushes the near face right, so the world's LEFT side swings
# toward the camera. Dragging UP tips the top away and brings the bottom
# forward. Both are derived once here rather than at each call site, exactly as
# in the original.
#
# Index order — 0 left, 1 right, 2 up, 3 down — is load-bearing: it is the
# order the solver enumerates moves in, so changing it changes which of two
# equal-cost solutions the search finds first, and therefore the hint.
#
# THE TWENTY-FOUR ORIENTATIONS LIVE IN A `const` DICTIONARY, which is the one
# piece of GDScript 3.5 exotica this port needs and is worth explaining once
# because several files use it.
#
# C# builds this table in a static constructor. GDScript 3.5 has static
# FUNCTIONS and no static VARIABLES, so there is nowhere for a table to live —
# except that a `const` Array or Dictionary is a single object owned by the
# script, shared by every caller, and its CONTENTS are mutable even though the
# binding is not. That gives exactly the "built once, visible everywhere,
# reachable from a static function" storage the C# static constructor provided,
# and it works in a headless `-s` run where an autoload's global identifier does
# not exist at parse time.

const ID := ["left", "right", "up", "down"]
const DX := [-1, 1, 0, 0]
const DY := [0, 0, 1, -1]

const _ORIS := []
const _INDEX := {}


# Build the table by closing the identity under the four quarter-turns. Called
# by everything that reads it; the first call does the work and the rest see a
# non-empty array and return.
static func _ensure() -> void:
	if not _ORIS.empty():
		return
	var start := Ori.new()
	_ORIS.append(start)
	_INDEX[start.key()] = 0
	var i := 0
	while i < _ORIS.size():
		for t in range(4):
			var m := apply(t, _ORIS[i])
			var k := m.key()
			if not _INDEX.has(k):
				_INDEX[k] = _ORIS.size()
				_ORIS.append(m)
		i += 1


# The 24 orientations, indexed, so a solver state can be an integer.
static func oris() -> Array:
	_ensure()
	return _ORIS


static func ori(i: int) -> Ori:
	_ensure()
	return _ORIS[i]


static func count() -> int:
	_ensure()
	return _ORIS.size()


static func ori_index(m: Ori) -> int:
	_ensure()
	return _INDEX[m.key()]


# One quarter-turn applied to a basis. The four cases are the four rows of the
# C# table, written out because GDScript 3.5 has no lambdas to put in an array.
static func apply(t: int, m: Ori) -> Ori:
	match t:
		0:
			return Ori.new(-m.f, m.u, m.r)          # left
		1:
			return Ori.new(m.f, m.u, -m.r)          # right
		2:
			return Ori.new(m.r, m.f, -m.u)          # up
		_:
			return Ori.new(m.r, -m.f, m.u)          # down
