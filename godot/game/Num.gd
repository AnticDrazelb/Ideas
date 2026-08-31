class_name Num
# SMALL NUMBERS THAT DO NOT ALLOCATE.
#
# str(int) makes a new string every call, and the HUD calls it from a per-frame
# tick. That is not a micro-optimisation: with depth numbers on, a nine-cube
# shows up to eighty-one of them and every one is rebuilt every frame, so the
# game produces something like five thousand short-lived strings a second while
# you are just looking at the board.
#
# The values are bounded and small. A cube is at most nine cells across, so a
# depth is 0..8; folds, nodes and hints are counters nobody drives past a few
# dozen. A table covers all of it, is built once, and hands back the SAME string
# each time — which matters twice over, because a Label's text setter compares
# the incoming string to the one it has and skips the mesh rebuild when they
# match.
#
# Past the table it falls back to str() and allocates, which is correct: the
# alternative is a cache that grows without bound because somebody's fold count
# reached four figures.

const MAX := 256

const _PLAIN := []
const _SLASHED := []


static func _ensure() -> void:
	if not _PLAIN.empty():
		return
	for i in range(MAX):
		_PLAIN.append(str(i))
		_SLASHED.append("/" + str(i))


# A number, as text, without garbage for the values the game actually reaches.
static func of(v: int) -> String:
	if v < 0 or v >= MAX:
		return str(v)
	_ensure()
	return _PLAIN[v]


# The same, with the leading slash the HUD's chips print — "/24" against a par,
# "/3" against a node count. Concatenating it at the call site would allocate on
# every frame the table exists to avoid.
static func over(v: int) -> String:
	if v < 0 or v >= MAX:
		return "/" + str(v)
	_ensure()
	return _SLASHED[v]
