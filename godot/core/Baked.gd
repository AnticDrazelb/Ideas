class_name Baked
# VAULT I IS AUTHORED: ten cubes, verified offline, carrying the teaching
# beats. The first two show you a way out you cannot walk to, which is the
# entire lesson and is not something a generator can be trusted to stage.
#
# THE FAR SIDE is four more, and they exist because of a measurement. The
# content audit shows par flatlining at 5.8 folds from about cube 100 — by 151
# every generation parameter is at its ceiling, so the ladder stops getting
# harder and only gets bigger. A new idea has to arrive there or nothing does,
# and a new idea has to be TAUGHT: one cube at a time, by cubes that cannot be
# solved while ignoring it.
#
# Everything else is cut on demand by Generator.mint, or served from the
# catalogue, which is what a player actually climbs.
#
# A BAKED CUBE IS AN ARRAY AND NOT A CLASS, because GDScript 3.5 will not put a
# constructed object in a `const` — so the fourteen literals below are plain
# data in a fixed order and to_level() is the one place that knows what each
# slot means. The order is:
#
#   [n, name, par, vox, start, goal, keys, doors, voxB]
#
# voxB is THE OTHER FACE, and empty on every cube that does not turn. An
# everter is a second array of types over the same cells — see Level.layout_of
# — so a cube that teaches one cannot be written down as a single voxel string.
# The four in THE FAR SIDE could not be stored at all until this existed: the
# search emitted them, the paste block dropped their other half, and every
# lesson they were found for evaporated.

const N := 0
const NAME := 1
const PAR := 2
const VOX := 3
const START := 4
const GOAL := 5
const KEYS := 6
const DOORS := 7
const VOXB := 8

# The head of THE FAR SIDE, where the everter is introduced.
#
# This was 146 — the first four of a vault IX that no longer exists. The ladder
# is ten chapters of fifteen now and the everter's chapter opens at ninety-one,
# so that is where the four cubes that teach it go. A teaching cube in the wrong
# chapter is a cube teaching a mechanic the player met sixty levels ago.
const ARC_START := 91

# EVERSION, IN FOUR CUBES. Introduce, complicate, bound, invert.
#
# Not drawn — SEARCHED, and each one is PROVED to require its lesson by solving
# the same solid twice: once with the everter, once with that cell demoted to
# ordinary trace. The difference between the two answers is the lesson.
#
#   THE FAR SIDE          no route at all without everting
#   TWO SHADOWS           a route either way, one fold cheaper everted
#   NOTHING UNDERNEATH    a square you can stand on until you evert
#   TURNED INSIDE OUT     a fold that only one polarity permits
#
# THE FIRST WAS SCULPTED RATHER THAN PLACED. Dropping an everter onto a working
# cube cannot make it impossible without one, so trace was removed — one cell at
# a time, keeping every cut that left the everted route intact — until the
# un-everted route died. That is what authoring a puzzle actually is: taking
# away everything the intended solution does not need.
const ARC := [
	[6, "THE FAR SIDE", 2,
		"#.#.#.E.##..#..#..#+#.+.+.##..+..###+.####...+#.#+....####.+.#..++#.++.#++#.##++.#.###.##...###+##+..+...#....#...#..####..#.+..+#..##.#+.#...#+#..##.#####..#.#....##.#..#####.####..###..##.######...#.##...#####.##.#",
		Vector3(2, 3, 3), Vector3(0, 0, 4), [], [],
		"#.+.#.E.+#..+..#..+##.#.+.+#..#..####.####...##.#+....####.#.#..###.+#.####.###+.#.###.##...######+..#...+....#...#..####..#.#..+#..##.+#.#...###..##.#+++#..+.#....##.#..#####.+###..++#..+#.###+++...#.##...#####.##.#"],
	[6, "TWO SHADOWS", 2,
		"###.#..#.##+...##....##.......###....#.####.#.###.####.######..#++..#.......+..#...#.##.#####.#+...+..#..++.+#.####..###.#..##.###.#+##.#+.#..+#....+.++##.+..##.###.+.##.#.+.#.#.E.+.#.+.#.##.+.....####.++#+....##..++",
		Vector3(4, 3, 5), Vector3(0, 3, 0), [], [],
		"###.#..#.###...##....++.......###....#.####.#.###.+##+.#+##+#..+++..#.......#..+...#.+#.##+##.##...#..#..##.++.####..###.#..##.##+.####.##.#..++....#.####.#..##.###.+.##.#.#.#.#.E.#.#.#.#.##.#.....####.####....##..+#"],
	[6, "NOTHING UNDERNEATH", 2,
		"+.#.+.E.##..#..#..++#.+.+.##..+..###+.####...+#.#+....####.+.#..++#.++.#++#.##++.#.###.##...###+##+..+...#....#...#..####..#.+..+#..##.#+.#...#+#..##.#####..#.#....##.#..#####.####..###..##.######...#.##...#####.##.#",
		Vector3(2, 3, 3), Vector3(0, 0, 4), [], [],
		"#.+.#.E.+#..+..#..+##.#.+.+#..#..####.####...##.#+....####.#.#..###.+#.####.###+.#.###.##...######+..#...+....#...#..####..#.#..+#..##.+#.#...###..##.#+++#..+.#....##.#..#####.+###..++#..+#.###+++...#.##...#####.##.#"],
	[6, "TURNED INSIDE OUT", 2,
		".+++++.####+.+#..#....##+..###+.###.#...#+#.#+..+###.E...+#+.+.+....+..##...##..###.....#..+#...+.#.##+++#.#.##.....###.###.######.##.##...+..###.###.##.##...#..###.##.#.###.###.#..##..###..#...#####+###.##.####..###",
		Vector3(3, 1, 4), Vector3(3, 0, 0), [], [],
		".##++#.###++.##..+....++#..##+#.###.#...+##.#+..####.E...###.#.+....#..##...++..#++.....+..##...#.#.######.#.##.....###.###.+#####.##.##...#..###.###.##.##...#..###.##.#.###.###.#..##..###..#...#########.##.####..###"],
]


const LEVELS := [
	[5, "FOOTING", 1,
		"......#..#..+..+##+#.#..#...#.##..##.#...#..##+...#..#.#+..#.##.#.+.#.#+..###...#..##.#.###+...#........#....#+..#.#+.#+...+.",
		Vector3(1, 1, 4), Vector3(4, 4, 3), [], [],
		""],
	[5, "THE TURN", 1,
		".#.#.#..#.###.+#.#++#+#....##.#+.+##........###..+#........#.##...........+.###+...##.#.+#..#..+#...#.#.#.#.#.#.###.#.+..#.#.",
		Vector3(3, 1, 1), Vector3(3, 3, 2), [], [],
		""],
	[5, "BURIED", 2,
		"#####..##.##.##.#..###.##.....####.##...#....++##.#.##..+###..#.#..###+...+##+.+##.+#..##.+#....##.+.#..#..+.+#..#.+......#.#",
		Vector3(1, 1, 4), Vector3(2, 3, 0), [], [],
		""],
	[5, "TWO FACES", 2,
		"..#++.#.##.###+..##++#.+..##+...#.....+..#.+.+..++###.#.####+#......##.##.#.####++......+...#....+..#....#...###...#.#..+.#..",
		Vector3(1, 3, 1), Vector3(3, 1, 0), [], [],
		""],
	[6, "THE LONG WAY", 2,
		"#...###..+...+.#.#..+#...+.#.###.##....++.+.#..#.##.###..###..+....#.+.#..#.+.....##...#.#...#.+.#.+#...#...#....######.+.####...#..#...###..##......#.##....###..#.##.....##.#..#.#...##..#.#...##..##.#...#+#.....##.#",
		Vector3(2, 1, 4), Vector3(3, 0, 1), [Vector3(3, 2, 4)], [Vector3(2, 0, 3)],
		""],
	[6, "TUMBLER", 3,
		".#.##.##.##..####...##.#.#....#.##.........##.##...#.#..#.####.....++..+.....##+.##.#.##.#..##.##....##.++#++#..#####..#.....#.##..+#....####....+.....+...#.+..#..+.....+..+#..#.#..###.#.#.#....####+.###.##.#.+.+....",
		Vector3(3, 2, 5), Vector3(1, 4, 1), [], [],
		""],
	[6, "WARDEN", 3,
		"###...##.....##.+.##..##....#.#.##.###..#+###..##.#.#+.#...##+.#....#..##.###...#..#.#..#.+......##..#.+.#####.##.#..#.#....####.#..#..##.+#..#.+#....#.###...##...#..+.##..+..##.#+...#+.....##..#.++++#..+.##.++####.+",
		Vector3(1, 1, 4), Vector3(4, 5, 4), [Vector3(0, 2, 3)], [Vector3(4, 4, 3)],
		""],
	[6, "OUBLIETTE", 3,
		"##+.......#.+#..#.+.....#.##+..##....#..+#+#.###+##.##++..+.####..###.##.#.####..#+...#..###+.#.##.#...#..#...#..#.+...#####.####+..##...###.+.....###........#####...#+.++#.+...+####.#.#...#...##.#.#.#.+.....+....#..",
		Vector3(3, 3, 5), Vector3(0, 1, 3), [Vector3(1, 4, 4)], [Vector3(4, 1, 3)],
		""],
	[6, "THE CANT", 3,
		"##+.#.#....+...+###..+.#+..#....#....##+.+...#.+#.##.+....##.#...#.#.###...+##+.+###.#..#...#.##.#.......#####..#.##....#.#.##..#.#....+#++..+.....#...#####+.#.##.##.#..+.++.......#..#.#..#.#.##.###..#..#.....++..+..",
		Vector3(1, 4, 4), Vector3(5, 0, 1), [Vector3(0, 5, 5)], [Vector3(3, 1, 0)],
		""],
	[7, "SIX WAYS", 4,
		"......+##..+.+...#+..#.#.......##..#+....#...#..#..#.#.+....#+.#...+..###.####..#.#####..#..##.#..##...###..#.##.#.#.++.###.#..#####..+###..##.....###.##..#.###.#.####..##..##..#....#.#..#...######...##..##..#..#.#.#....###.###....+...#...##.#+...+..+..#...#..##++.+..+.###.+....#++#...#..+.##.+.##.#..#..#..#...#...####..##.##...+##.#.+..#..#",
		Vector3(1, 5, 5), Vector3(4, 1, 2), [Vector3(0, 6, 6)], [Vector3(6, 2, 2)],
		""],
]


# The authored cube for a level number, or null. A lookup rather than an index,
# because authored cubes no longer live only at the front of the ladder and the
# next arc will not either.
static func try_at(level: int):
	if level >= 1 and level <= LEVELS.size():
		return _entry(LEVELS[level - 1])
	if level >= ARC_START and level < ARC_START + ARC.size():
		return _entry(ARC[level - ARC_START])
	return null


# The raw array, wrapped in something with named fields so call sites read like
# the C# struct rather than like slot arithmetic. BakedLevel deliberately knows
# nothing about this class — see the note at the top of that file.
static func _entry(a: Array) -> BakedLevel:
	return BakedLevel.new(a)


static func to_level(a: Array, level: int) -> Level:
	return _entry(a).to_level(level)
