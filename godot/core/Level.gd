class_name Level
extends Reference
# A level is n^3 cells of five kinds:
#
#   '.'  void     — nothing there, and impassable
#   '#'  lattice  — solid, but you cannot stand on its face
#   '+'  trace    — solid, and walkable when it is the nearest thing
#   'A'  invert plate
#   'B'  drain plate
#
# Plates are always walkable, are always the surface of their column, and
# change the world when stepped on.
#
# Index order is [y][z][x], so a level literal reads as a stack of decks from
# the bottom up — which is the only way authoring one by hand is survivable,
# and is the order the Forge editor steps through.
#
# CELLS ARE BYTES AND NOT ONE-CHARACTER STRINGS. C# had a char[]; GDScript's
# nearest equivalent that is not a per-cell heap allocation is a PoolByteArray
# of the same ASCII codes, which indexes and compares as an integer. Every
# comparison in the game is against one of the constants below, so nothing
# downstream ever needs to know it is looking at a number.

const VOID := 46      # .
const LATTICE := 35   # #
const TRACE := 43     # +
const PLATE_A := 65   # A — invert
const PLATE_B := 66   # B — drain
const EVERTER := 69   # E
const TRIGGER := 84   # T
const TRIGGER2 := 85  # U

var n := 0
var vox: PoolByteArray

# THE SECOND SOLID, and empty on every cube that does not have one.
#
# A, B and E are all functions of ONE voxel array: swap two materials, swap two
# others, reverse which cell wins its column. Eight readings of the same thing.
# A TRIGGER is not that — it exchanges the array itself, so the ground under
# the rest of the route is rearranged rather than re-coloured, and that is a
# different kind of claim about the machine.
#
# It rides in the same world mask as everything else, one bit higher, so
# nothing downstream needs to know it exists: eff() picks which array to start
# from and then does exactly what it always did.
var alts := []        # Array of PoolByteArray

var start := Vector3.ZERO
var goal := Vector3.ZERO
var keys := []        # Array of Vector3
var doors := []       # Array of Vector3

var par := 0
var steps := 0
var level := 0
var band := 0
var name := ""
var authored := false
var fallback := false

# Plates placed by the carve, kept so the generator can check its own work.
# Each entry is [Vector3 w, int kind].
var glyphs := []

# Cells an earlier carve leg depends on in some world; never overwritten.
var lock_map := {}

var _eff := []


# The first alternate layout, for the code and identity paths that predate the
# chain.
func get_vox_b():
	return alts[0] if alts.size() > 0 else null


func set_vox_b(v) -> void:
	alts = [] if v == null else [v]


# How many solids this cube is, counting the first. One is every cube in the
# game until the trigger arrives.
func layouts() -> int:
	return alts.size() + 1


# A COPY OF THE CUBE, AND THAT MEANS ALL OF ITS SOLIDS.
#
# `alts` was missing from this list in the C# port, and Session.load is
# `lv = source.clone()` — so the game loaded every trigger cube with its second
# solid dropped on the floor. Nothing threw. layouts() fell to 1, eff() fell
# back to `vox` for the swapped world exactly as it is written to for a cube
# that has one solid, and the trigger became a glyph that toggles a bit and
# changes nothing. On the last cube of the ladder it moved 357 cells before the
# clone and 0 after.
#
# It was 125 of the 300 cubes then in the catalogue — every one carrying a
# trigger, the last cube in the game among them — unsolvable in the hand while
# every check in the harness called them solvable, because every check solved
# the Level the catalogue decoded and the game plays a copy.
#
# The lesson is the one this project keeps relearning: a field added to a class
# is not added to the code that copies it, and nothing about that is visible at
# the call site. Anything added above must be added here too.
# NOTE the missing `-> Level` and the `get_script().new()`: GDScript 3.5 will
# not let a class name itself, so a constructor and a return type that both say
# "Level" inside Level.gd are compile errors. The script is its own factory.
func clone():
	var c = get_script().new()
	c.n = n
	c.vox = PoolByteArray(vox)
	c.start = start
	c.goal = goal
	c.keys = keys.duplicate()
	c.doors = doors.duplicate()
	c.par = par
	c.steps = steps
	c.level = level
	c.band = band
	c.name = name
	c.authored = authored
	c.fallback = fallback
	c.alts = []
	for a in alts:
		c.alts.append(PoolByteArray(a))
	return c


static func vidx_n(nn: int, x: int, y: int, z: int) -> int:
	return (y * nn + z) * nn + x


static func vidx_v(nn: int, p: Vector3) -> int:
	return (int(p.y) * nn + int(p.z)) * nn + int(p.x)


func vidx(p: Vector3) -> int:
	return (int(p.y) * n + int(p.z)) * n + int(p.x)


func at(p: Vector3) -> int:
	return vox[vidx(p)]


# ---- worlds ---------------------------------------------------------------
#
# A PLATE is the second verb. Step on one and the cube's MATERIAL inverts —
# every trace goes dead and every dead cell lights up, or all the stone becomes
# air and all the air becomes stone. The geometry is untouched. What changes is
# which of it you may stand on, which means the projection you spent four folds
# learning is now its own negative.
#
#   world 0   trace +   lattice #   void .     as carved
#   world 1   trace #   lattice +   void .     INVERT
#   world 2   trace +   lattice .   void #     DRAIN
#   world 3   trace .   lattice +   void #     both — a third world
#
# Glyphs are exempt from all of it: a plate is a plate in every world, which is
# what makes it safe to stand on while everything around it changes, and what
# makes every fold legal while you are on one.

# THE THIRD BIT IS NOT A KIND OF CELL, IT IS A DIRECTION OF LOOKING.
#
# Bits 1 and 2 are the plates: they change what a cell IS. Bit 4 changes
# nothing about any cell. It selects the other array of faces, and that is why
# eff_type ignores it entirely while layout_of is the only place that reads it.
const EVERTED := 4

# THE PLATE BITS — the only ones the five-second clock has ever belonged to,
# and the only ones a revert takes back.
#
# This mask exists because the alternative kept being written as
# `world & ~EVERTED`, which is the same thing ONLY while bits 1, 2 and 4 are
# the whole of the world. The moment the trigger added bits 8 and 16 that
# expression started answering "a plate is up" with "yes" for a cube that has
# no plate in it at all — which armed a countdown nobody asked for, sprang the
# board back when it lapsed, and then, with the clock at zero and the world
# still not empty, did it again every frame.
#
# So the question is asked by NAME. A bit added later is not a plate until
# somebody says it is here.
const PLATES := 1 | 2

# THE LAYOUT BITS: which of the cube's solids is on.
#
# TWO BITS AND NOT A COUNTER, which is the whole design decision. Every
# world-changer in this game is an INVOLUTION — step on it twice and you are
# back where you started — and that is what makes one a decision you can
# inspect rather than a door that slams. A counter advancing 1-2-3-4 would
# break it: the board you were solving would be gone and undo would be the only
# way back.
const SWAPPED := 8
const SWAPPED2 := 16

# THE STANDING BITS: set by the player, unset only by the player.
#
# An everter changes which face of the solid is the surface and a trigger
# changes which solid it is; neither is temporary and neither is on a clock.
# THE SOLVER CANNOT SEE A CLOCK — it carries `world` through its search in a
# world with no real time in it — so anything the runtime expires behind its
# back makes cubes solvable on paper and impossible in the hand.
const STANDING := EVERTED | SWAPPED | SWAPPED2


# Which solid a world selects: 0 for the first, 1..3 for the alternates.
#
# THE EVERTER SELECTS ONE TOO, AND THAT IS THE WHOLE MECHANIC.
#
# It used to flip a comparison — `evert ? d < cur.d : d > cur.d` — so a column
# stopped showing its nearest solid cell and showed its farthest. Nothing
# moved. Every square the player was looking at was replaced by a cell from the
# other end of the solid, and the per-cell turn on screen was a depiction of
# that rather than a thing happening to the machine.
#
# A cell is a small solid with six faces, and the one that matters is the one
# pointing at the camera. An everter turns every cell in place, at once, out of
# time with its neighbours, and a different face is now the surface. That is a
# SECOND ARRAY OF TYPES over the same cells — which is exactly what `alts`
# already is — under one added constraint: OCCUPANCY IS LOCKED.
#
# It shares alts[0] with the trigger rather than taking a slot of its own,
# because no cube carries both.
static func layout_of(world: int) -> int:
	return (1 if (world & (SWAPPED | EVERTED)) != 0 else 0) | (2 if (world & SWAPPED2) != 0 else 0)


static func glyph_bit(c: int) -> int:
	match c:
		PLATE_A:
			return 1
		PLATE_B:
			return 2
		EVERTER:
			return EVERTED
		TRIGGER:
			return SWAPPED
		TRIGGER2:
			return SWAPPED2
		_:
			return 0


static func is_glyph(c: int) -> bool:
	return c == PLATE_A or c == PLATE_B or c == EVERTER or c == TRIGGER or c == TRIGGER2


static func is_walk_type(t: int) -> bool:
	return t == TRACE or t == PLATE_A or t == PLATE_B or t == EVERTER or t == TRIGGER or t == TRIGGER2


static func eff_type(c: int, world: int) -> int:
	if is_glyph(c):
		return c
	var t := c
	if (world & 1) != 0:
		if t == TRACE:
			t = LATTICE
		elif t == LATTICE:
			t = TRACE
	if (world & 2) != 0:
		if t == LATTICE:
			t = VOID
		elif t == VOID:
			t = LATTICE
	return t


# Which base char yields footing in this world — the inverse of eff_type.
static func base_for_walk(world: int) -> int:
	return LATTICE if (world & 1) != 0 else TRACE


# Effective cells for a world. Derived, cached, and never mutated.
func eff(world: int) -> PoolByteArray:
	# sixteen rows, not four: the polarity bit is a no-op here and the LAYOUT
	# bit is not — it chooses which solid the swaps are applied to. Rows 0..7
	# and 8..15 are identical on a cube with one layout, which is most of them,
	# and eight wasted rows of cache beats a mask that means different things in
	# different files.
	world = world & 31
	if _eff.empty():
		_eff.resize(32)
	if _eff[world] != null:
		return _eff[world]
	var layout := layout_of(world)
	var from: PoolByteArray = alts[layout - 1] if layout > 0 and layout <= alts.size() else vox
	var outv := PoolByteArray()
	outv.resize(from.size())
	for i in range(outv.size()):
		outv[i] = eff_type(from[i], world)
	_eff[world] = outv
	return outv


func clear_eff() -> void:
	_eff = []


# Which world bit the cell under a foot flips. The overload with a world reads
# the LAYOUT the foot is standing in, because a trigger only exists in the
# solid that carries it — reading `vox` unconditionally would fire the swap off
# a cell that is not there.
func glyph_at(w: Vector3) -> int:
	return glyph_bit(vox[vidx(w)])


func glyph_at_world(w: Vector3, world: int) -> int:
	var layout := layout_of(world)
	var from: PoolByteArray = alts[layout - 1] if layout > 0 and layout <= alts.size() else vox
	return glyph_bit(from[vidx(w)])


func key_index_at(w: Vector3) -> int:
	for i in range(keys.size()):
		if keys[i] == w:
			return i
	return -1


func door_index_at(w: Vector3) -> int:
	for i in range(doors.size()):
		if doors[i] == w:
			return i
	return -1


func vox_string() -> String:
	return vox.get_string_from_ascii()
