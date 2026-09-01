extends SceneTree
# THE HARNESS, AND IT RUNS WITHOUT AN EDITOR.
#
#   tools/check.sh              the ordinary run
#   tools/check.sh full         the exhaustive ranges the C# suite asserts
#
# These are the port's contract with the original, and they are not checks that
# the engine is reasonable — they check that it is the SAME. Every cube in this
# game is a pure function of its number, and that is not a nice property, it is
# load-bearing: it is the entire reason the daily works with no server, the
# reason a cube number typed into a text field produces the same puzzle on
# somebody else's phone, and the reason the baked catalogue of already-minted
# identities means anything at all.
#
# THE FIXTURES BELOW ARE THE UNITY PROJECT'S OWN, verbatim, and they came to it
# from the JavaScript original. If this file passes, cube 4,127 in Godot is the
# same puzzle as cube 4,127 in the browser.
#
# WHY THERE IS A `full` SWITCH. Two of the C# tests mint fifty cubes each and
# one of them then walks 384 folds per plate cell. GDScript is an order of
# magnitude slower than IL2CPP, so at full range they are a ten-minute run and
# nobody would run them. The ranges are quoted in each test; the default is a
# representative slice of the same range and `full` is the C# range exactly.

var _pass := 0
var _fail := 0
var _full := false


# ---- fixtures --------------------------------------------------------------
#
# [level, par, steps, n, id]
const FIXTURES := [
	[11, 3, 3, 6, "brmN8qb8"],
	[15, 3, 6, 6, "nvtffdG0"],
	[19, 2, 6, 6, "bXJ6AF12"],
	[20, 4, 5, 6, "ekDErnwF"],      # the first cube to carry a plate
	[21, 2, 9, 6, "WvgBVWTo"],
	[25, 2, 17, 6, "mj_L66uK"],
	[31, 4, 9, 7, "D1eez7QI"],
	[41, 4, 9, 7, "-0Y1i5rc"],      # both plate kinds are available from here
	[48, 4, 15, 7, "56bZRLI6"],     # the daily's difficulty, forever
	[55, 4, 15, 7, "o5qTMPue"],
	[66, 4, 9, 7, "eESpYzqD"],
	[81, 6, 7, 8, "muV5zZO6"],      # the size ladder reaches eight
	[90, 4, 19, 8, "YyO8qG62"],     # the last authored-length vault
	[100, 5, 19, 8, "9Fr3nMCS"],
	[121, 6, 10, 8, "lI6C2MGY"],
	[151, 5, 12, 9, "PsupahAN"],    # and nine
	[500, 6, 12, 9, "rEAGbL8a"],
	[4127, 7, 12, 9, "8VGD1ej4"],
]


func _init() -> void:
	for a in OS.get_cmdline_args():
		if a == "full" or a == "--full":
			_full = true
	call_deferred("_go")


func _go() -> void:
	var t0 := OS.get_ticks_msec()
	_run()
	print("")
	print("%d passed, %d failed  (%.1fs%s)"
			% [_pass, _fail, (OS.get_ticks_msec() - t0) / 1000.0, "" if _full else ", short ranges"])
	quit(1 if _fail > 0 else 0)


func group(name: String) -> void:
	print("\n— " + name)


func ok(cond: bool, what: String) -> void:
	if cond:
		_pass += 1
	else:
		_fail += 1
		print("  FAIL  " + what)


func eq(a, b, what: String) -> void:
	if a == b:
		_pass += 1
	else:
		_fail += 1
		print("  FAIL  %s\n        expected %s\n        got      %s" % [what, str(b), str(a)])


func _run() -> void:
	_rng()
	_orientations()
	_generator()
	_promise()
	_projection_model()
	_identity_and_codes()
	_ladder()
	_forge_standard()
	_daily()
	_catalogue()
	_teaching()


# ---- the arithmetic under everything ---------------------------------------

func _rng() -> void:
	group("the random source")

	# THE FIRST EIGHT DRAWS FROM SEED 12345, AS NUMERATORS OVER 2^32.
	#
	# The C# suite writes them as decimal literals — 0.9797282677609473 and so on
	# — and asserts equality with a delta of zero. That cannot be done here: a
	# mulberry32 draw is exactly k/2^32 for an integer k, and GDScript's float
	# literal parser lands one unit in the last place BELOW the decimal form of
	# the first of them, so a correct implementation fails against its own
	# fixture. The numerator is the value the algorithm actually produces, it is
	# exact in both languages, and it is the more honest way to write down what
	# is being asserted.
	var want := [
		4207900869, 1317490944, 2079646450, 3513001552,
		2187978186, 1492380277, 316786230, 3291647763,
	]
	var rng := Rng.mulberry32(12345)
	for i in range(want.size()):
		eq(rng.next(), want[i] / 4294967296.0, "mulberry32 draw %d diverged" % i)

	eq(Rng.hash_seed(1), 1228498187, "hash_seed(1)")
	eq(Rng.hash_seed(2), 3709338170, "hash_seed(2)")
	eq(Rng.hash_seed(5), 103210291, "hash_seed(5)")

	eq(Bits.imul(-1, -1), 1, "imul(-1,-1) wraps to 1")
	eq(Bits.imul(65536, 65536), 0, "imul overflows to zero at 2^32")
	eq(Bits.ushr(-1, 28), 15, "ushr is logical, not arithmetic")

	# NEVER SHUFFLE WITH A COMPARATOR — one draw per element, always.
	var a := [0, 1, 2, 3]
	var r2 := Rng.mulberry32(7)
	r2.shuffle(a)
	a.sort()
	eq(a, [0, 1, 2, 3], "a shuffle is a permutation")


func _orientations() -> void:
	group("orientations")
	eq(Turns.count(), 24, "there are exactly twenty-four orientations")
	var seen := {}
	for m in Turns.oris():
		seen[m.key()] = true
	eq(seen.size(), 24, "and no two of them are the same")


# ---- the generator is the same generator -----------------------------------

func _generator() -> void:
	group("minted cubes match the original")
	for f in FIXTURES:
		var lv := Generator.mint(f[0])
		eq(lv.n, f[3], "cube %d changed size" % f[0])
		eq(lv.par, f[1], "cube %d changed par" % f[0])
		eq(lv.steps, f[2], "cube %d changed route length" % f[0])
		eq(Identity.canon_id(lv), f[4], "cube %d is a different cube" % f[0])

	group("mint is a pure function of its number")
	for level in [11, 37, 48]:
		var a := Generator.mint(level).vox_string()
		var b := Generator.mint(level).vox_string()
		eq(a, b, "cube %d is not a pure function of its number" % level)


# ---- the promise attached to every cube ------------------------------------

func _promise() -> void:
	# The C# range is 11..60. See the note at the top about `full`.
	var levels := range(11, 61) if _full else [11, 17, 20, 26, 33, 41, 48, 55, 60]

	group("no cube reaches the player unwinnable  (%d cubes)" % levels.size())
	for level in levels:
		# This is the one guarantee the whole supply rests on, and it is
		# structural rather than a filter: solvability is CONSTRUCTED by the
		# carve and then proved by the solver before the cube is handed over.
		var lv := Generator.mint(level)
		var r := Solver.solve(lv, 60)
		ok(r.ok, "cube %d is a lie" % level)
		eq(r.turns, lv.par, "cube %d claims a par it cannot meet" % level)
		# The fallback exists so that "every cube has been proved winnable" has
		# no exceptions. It is deliberately boring, and it should never be what a
		# player is handed.
		ok(not lv.fallback, "cube %d fell through to the fallback" % level)

	group("every authored cube solves at its claimed par")
	for i in range(Baked.LEVELS.size()):
		var b: BakedLevel = Baked.try_at(i + 1)
		var r := Solver.solve(b.to_level(i + 1), 60)
		ok(r.ok, "%s does not solve" % b.name)
		eq(r.turns, b.par, "%s is not the par it claims" % b.name)

	group("every fold is legal while standing on a plate")
	# A plate is always the surface of its column, cut clean through the rock.
	# That is what makes plates PIVOTS: in a game where where you stand decides
	# which folds you have, a square that gives you all four is worth walking a
	# long way for. If this ever stops being true the mechanic quietly stops
	# working and nothing else would notice.
	#
	# The C# range is 20..70; the default here is the first three plate cubes,
	# which is 384 folds each and already several seconds.
	var plate_levels := range(20, 71) if _full else [20, 21, 22]
	var checked := 0
	var refused := 0
	for level in plate_levels:
		var lv := Generator.mint(level)
		for i in range(lv.vox.size()):
			if not Level.is_glyph(lv.vox[i]):
				continue
			var n: int = lv.n
			var y := i / (n * n)
			var rr := i - y * n * n
			var z := rr / n
			var cell := Vector3(rr - z * n, y, z)
			for world in range(4):
				for oi in range(Turns.count()):
					for t in range(4):
						var m2 := Turns.apply(t, Turns.ori(oi))
						if Projection.landing(lv, m2, cell, ~0, world) == null:
							refused += 1
			checked += 1
	eq(refused, 0, "a fold was refused while standing on a plate")
	ok(checked > 0, "no plates were found to check")


# ---- the projection model --------------------------------------------------

func _projection_model() -> void:
	group("view and world are exact round trips")
	for n in [5, 6, 7, 8, 9]:
		var bad := 0
		for oi in range(Turns.count()):
			var m: Ori = Turns.ori(oi)
			for y in range(n):
				for z in range(n):
					for x in range(n):
						var p := Vector3(x, y, z)
						if Projection.world_of(n, m, Projection.view_of(n, m, p)) != p:
							bad += 1
		eq(bad, 0, "round trip failed for n=%d" % n)


# ---- identity and share codes ----------------------------------------------

func _identity_and_codes() -> void:
	group("a cube folded is the same cube")
	# A cube's identity is the smallest of the twenty-four ways it can be written
	# down, so rebuilding one lying on its side is still rebuilding the same cube
	# — and a plain byte compare would not notice.
	var lv := Generator.mint(37)
	var id := Identity.canon_id(lv)
	var wrong := 0
	for oi in range(Turns.count()):
		if Identity.canon_id(_rotate(lv, Turns.ori(oi))) != id:
			wrong += 1
	eq(wrong, 0, "identity is not fold-invariant")

	group("share codes round trip")
	for level in [11, 20, 41, 55]:
		var c := Generator.mint(level)
		var back = ShareCode.decode(ShareCode.encode(c))
		ok(back != null, "cube %d failed to decode" % level)
		if back == null:
			continue
		eq(back.vox_string(), c.vox_string(), "cube %d lost its cells" % level)
		eq(back.start, c.start, "cube %d lost its start" % level)
		eq(back.goal, c.goal, "cube %d lost its exit" % level)
		eq(back.keys, c.keys, "cube %d lost its nodes" % level)
		eq(back.doors, c.doors, "cube %d lost its locks" % level)

	group("a mistyped share code is refused")
	# The checksum is the whole point: a code that has lost a character must fail
	# rather than decode into a different, probably broken cube.
	ok(ShareCode.decode("AAAAAAAAAAAA") == null, "a nonsense code decoded")
	ok(ShareCode.decode("") == null, "an empty code decoded")
	ok(ShareCode.decode("not a code at all") == null, "a sentence decoded")
	var good := ShareCode.encode(Generator.mint(11))
	ok(ShareCode.decode(good) != null, "a good code was refused")
	ok(ShareCode.decode(good.substr(0, good.length() - 1)) == null, "a truncated code decoded")

	group("smart punctuation in a pasted code is repaired")
	# The alphabet contains a hyphen and the world is full of things that
	# "helpfully" replace one.
	var g2 := ShareCode.encode(Generator.mint(20))
	ok(ShareCode.decode(g2.replace("-", "—")) != null, "an em-dashed code was thrown away")


func _rotate(lv: Level, m: Ori) -> Level:
	var n: int = lv.n
	var vox := PoolByteArray()
	vox.resize(n * n * n)
	for y in range(n):
		for z in range(n):
			for x in range(n):
				var v := Projection.view_of(n, m, Vector3(x, y, z))
				vox[Level.vidx_v(n, v)] = lv.vox[Level.vidx_n(n, x, y, z)]
	var outp := Level.new()
	outp.n = n
	outp.vox = vox
	outp.par = lv.par
	outp.start = Projection.view_of(n, m, lv.start)
	outp.goal = Projection.view_of(n, m, lv.goal)
	for k in lv.keys:
		outp.keys.append(Projection.view_of(n, m, k))
	for d in lv.doors:
		outp.doors.append(Projection.view_of(n, m, d))
	return outp


# ---- the vault ladder ------------------------------------------------------

func _ladder() -> void:
	group("vault boundaries are consistent both ways")
	var bad := 0
	for band in range(40):
		var start := Vaults.vault_start(band)
		if Vaults.vault_of(start) != band:
			bad += 1
		if Vaults.vault_of(start + Vaults.vault_size(band) - 1) != band:
			bad += 1
		if band > 0 and Vaults.vault_of(start - 1) != band - 1:
			bad += 1
	eq(bad, 0, "a vault does not contain its own cubes")

	group("generated vault names use the whole word list")
	# The stride has to be coprime with the list length or most of the list never
	# appears — this was band*5 against ten words, so every generated vault was
	# called REACH or WORKS.
	var seen := {}
	for band in range(Vaults.authored_count(), Vaults.authored_count() + 40):
		seen[Vaults.vault_name(band)] = true
	ok(seen.size() > 30, "the generated vault names repeat far too soon")

	group("level names are never blank")
	# hash_seed is unsigned 32-bit, and a signed shift here indexed negatively
	# and named half the cubes past level 40 "undefined".
	var blank := 0
	for level in range(1, 401):
		var name := Vaults.level_name(level)
		if name.strip_edges() == "" or name.find("undefined") >= 0:
			blank += 1
	eq(blank, 0, "a cube has no name")


# ---- the Forge's standard --------------------------------------------------

func _forge_standard() -> void:
	group("validation rejects a corridor")
	# A cube you can finish without folding is a corridor. The generator can
	# never produce one; the Forge has no floor of its own, and par is the whole
	# scoring system, so a par of zero makes every clear worse than perfect and
	# the win card reads as nonsense.
	var v := Validation.validate(Generator.fallback_level(1))
	ok(not v.ok, "a corridor validated")
	ok(v.why.find("CORRIDOR") >= 0, "a corridor was refused for the wrong reason")

	group("validation accepts every generated cube without a plate")
	# A hand-built cube is held to exactly the standard a generated one is, so
	# every generated one has to clear the hand-built checks —
	#
	# UP TO THE POINT WHERE PLATES ARRIVE, AND NOT PAST IT. This looks like a
	# hole and is not one. Validation is the FORGE's validator, and the Forge
	# authors in world zero: it asks whether the exit is on a trace, meaning a
	# '+' in the cube as written down. The carve does not work that way. Once it
	# drops a plate it keeps carving in the world that plate opened, where
	# footing is spelled '#' — so the exit of a plate cube is very often a '#' in
	# the base representation and perfectly walkable in the world the route
	# actually arrives through.
	for level in range(11, Generator.GLYPH_FROM):
		var vv := Validation.validate(Generator.mint(level))
		ok(vv.ok, "cube %d fails the Forge's own standard: %s" % [level, vv.why])

	group("a plate cube carries its exit in a flipped world")
	# The other half of the note above, asserted rather than assumed: if this
	# ever stops being true, the carve has stopped continuing past the plate, and
	# the plate has gone back to being a footnote on the last leg.
	var any_flipped := false
	for level in (range(Generator.GLYPH_FROM, 41) if _full else [20, 21, 22, 23, 24, 25]):
		var lv := Generator.mint(level)
		if lv.vox[Level.vidx_v(lv.n, lv.goal)] == Level.LATTICE:
			any_flipped = true
		ok(Solver.solve(lv, 60).ok, "cube %d is not winnable" % level)
	ok(any_flipped, "no plate cube routed through a flipped world")


# ---- the daily -------------------------------------------------------------

func _daily() -> void:
	group("the daily is the same cube for everybody on the same day")
	var day := 20500
	var seed_a := Daily.daily_seed(day)
	var a := Generator.mint(Daily.SPEC_LEVEL, seed_a)
	var b := Generator.mint(Daily.SPEC_LEVEL, seed_a)
	eq(a.vox_string(), b.vox_string(), "the daily is not deterministic")
	ok(seed_a != Daily.daily_seed(day + 1), "two days share a cube")

	group("the day boundary is global, not local")
	# A shared ranking of "today's cube" needs everyone to agree on which day it
	# is, so the boundary is one instant everywhere: midnight UTC-7.
	var midnight := 20500 * 86400000 + Daily.DAY_ANCHOR_MS
	eq(Daily.day_index_at(midnight), 20500, "the day starts late")
	eq(Daily.day_index_at(midnight + 86399999), 20500, "the day ends early")
	eq(Daily.day_index_at(midnight + 86400000), 20501, "the day does not end")


# ---- the shipped ladder ----------------------------------------------------

func _catalogue() -> void:
	group("the catalogue is the ladder")
	# THIS IS WHAT A PLAYER ACTUALLY CLIMBS. The generator survives for the daily
	# and nothing else, so the checks above prove the arithmetic and these prove
	# the content: every one of the hundred and fifty decodes, is the identity it
	# says it is, and solves at the par it claims.
	eq(Catalogue.count(), Vaults.LAST_CUBE, "the catalogue is not the length of the ladder")

	var bad_decode := 0
	var bad_id := 0
	var bad_par := 0
	var bad_steps := 0
	for level in range(1, Catalogue.count() + 1):
		var e = Catalogue.all()[level - 1]
		var lv = Catalogue.get_level(level)
		if lv == null:
			bad_decode += 1
			continue
		if Identity.canon_id(lv) != e.id:
			bad_id += 1
		var r := Solver.solve(lv, 60)
		if not r.ok or r.turns != e.par:
			bad_par += 1
		elif r.steps != e.steps:
			bad_steps += 1
	eq(bad_decode, 0, "cubes in the catalogue that do not decode")
	eq(bad_id, 0, "cubes whose identity is not the one written beside them")
	eq(bad_par, 0, "cubes that do not solve at the par they claim")
	eq(bad_steps, 0, "cubes whose route length is not the one written beside them")

	group("the catalogue is the one that shipped")
	# The stamp is the one number that says so. It is printed rather than
	# asserted against a literal, because the literal would have to be updated in
	# the same commit that changes the ladder — which is precisely the commit
	# where nobody would notice they had done it.
	print("  catalogue stamp: " + Catalogue.stamp())
	ok(Catalogue.stamp().length() == 8, "the stamp is not a checksum")


# ---- when the game offers to teach -----------------------------------------

func _teaching() -> void:
	group("the glass is taught where the screen stops answering")

	var V = Session.LoadKind.VAULT
	var NONE = Coach.Lesson.NONE
	var PEEK = Coach.Lesson.PEEK
	var ALL = Coach.LEARNED_EVERYTHING

	# WHERE IT CAN FIRE. Not on cubes one and two — those are first contact, and
	# the coach never stacks a third thing on the two verbs. Measured through
	# LevelSupply, which is the path the player gets, cubes one and two draw their
	# core and cube three does not: the first board that can ask "where is the
	# exit" is the first board that has the question.
	eq(Coach.next(ALL, V, 1, false, false, 0, true), NONE, "taught over first contact")
	eq(Coach.next(ALL, V, 2, false, false, 0, true), NONE, "taught over first contact")
	eq(Coach.next(ALL, V, 3, false, false, 0, true), PEEK, "not taught on the first buried exit")

	# AND ONLY WHILE THE EXIT IS OUT OF SIGHT. A board that shows you where you
	# are going has already answered the question the glass answers.
	eq(Coach.next(ALL, V, 40, false, false, 0, false), NONE, "taught with the exit in plain sight")

	# LAST, BEHIND THE RULES. A cube that introduces a plate teaches the plate;
	# the view can wait for a cube that is not busy.
	eq(Coach.next(0, V, 80, false, false, Coach.LEARNED_SUBSTRATE, true),
			Coach.Lesson.SUBSTRATE, "the view outranks the rules")

	# THE LADDER ONLY, and never over a solved board.
	eq(Coach.next(ALL, Session.LoadKind.DAILY, 40, false, false, 0, true), NONE,
			"the daily gets a tutorial")
	eq(Coach.next(ALL, Session.LoadKind.MADE, 40, false, false, 0, true), NONE,
			"a made cube gets a tutorial")
	eq(Coach.next(ALL, V, 40, true, false, 0, true), NONE, "taught over a won cube")

	# It has a line, and it explains the verb rather than naming the feature.
	ok(Coach.line_for(PEEK).length() > 0, "the lesson has nothing to say")

	group("what the front door offers")

	# THE FOURTH DOOR IS MANUAL UNTIL THE FIRST CLEAR AND FORGE AFTER IT. A level
	# editor is the least useful thing in this game to somebody who has not
	# finished a cube: it is a tool for making the thing they have not played.
	ok(Screens.never_cleared(1, 0), "a fresh save is offered the Forge")
	ok(not Screens.never_cleared(2, 0), "a save past cube one is still a beginner")

	# AND FINISHING THE MACHINE HANDS THE LADDER BACK AT CUBE ONE, so `reached`
	# alone says "beginner" about the one save that has certainly cleared a cube.
	ok(not Screens.never_cleared(1, 1), "the machine was finished and the Forge was taken away")
	ok(not Screens.never_cleared(1, 3), "three runs and still a beginner")

	group("the case is thinner on a bigger machine")

	# A TEN-INCH TABLET DOES NOT HAVE A TWO-INCH BEZEL. The canvas scaler keeps a
	# unit the same FRACTION of the display at every resolution, which is what
	# makes one set of offsets land everywhere — and it also means the case costs
	# the same quarter of the screen on a handheld and on a panel.
	eq(Chassis.bezel_factor(4.7), 1.0, "a phone loses part of its case")
	eq(Chassis.bezel_factor(6.5), 1.0, "the biggest handheld loses part of its case")
	ok(Chassis.bezel_factor(8.0) < 1.0, "an eight-inch tablet wears a phone's bezel")
	ok(Chassis.bezel_factor(8.0) > Chassis.bezel_factor(10.0), "it does not thin with size")
	eq(Chassis.bezel_factor(10.0), Chassis.PANEL_BEZEL, "a panel is not at the floor")
	eq(Chassis.bezel_factor(13.0), Chassis.PANEL_BEZEL, "it thins past the floor")

	# AND A PLATFORM THAT WILL NOT SAY GETS THE CASE THE GAME WAS BUILT WITH.
	# An X server's nominal 96 dpi is not a figure any handheld reports, and
	# guessing from it would thin the bezel on every desktop it was tested on.
	eq(Chassis.bezel_factor(0.0), 1.0, "an unknown display is guessed at")
	eq(Chassis.bezel_factor(-1.0), 1.0, "a nonsense diagonal is trusted")
