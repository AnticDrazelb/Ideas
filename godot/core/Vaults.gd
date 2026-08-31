class_name Vaults
# Ten chapters of fifteen. A hundred and fifty cubes, and that is the game.
#
# THEY USED TO BE SIX VAULTS of uneven length followed by a generated tail that
# grew forever, because the ladder was minted on the device and had no last
# cube. It has one now: the catalogue is cut offline, chosen rather than
# accepted, and the vault list is the shape it was cut in — one mechanic a
# chapter at rising size and tightening decision density, with the four verbs
# taught in the first eight and nothing new after.
#
# IT WAS TWENTY VAULTS AND FIVE HUNDRED CUBES, then twelve of twenty-five, and
# the argument for shortening it twice was the same one. The cut refuses any
# cube a player can brick, and a gate that refuses candidates needs slots with
# candidates to spare. Fewer slots is the same generator asked an easier
# question.
#
# THE SECOND CUT WENT FURTHER, AND FOR A DIFFERENT REASON. Three hundred cubes
# chosen by a scoring function is a hundred and fifty more than the game has
# things to say, and length is not a virtue in this genre — it is what makes
# somebody stop at cube sixty and never come back.
#
# Fifteen is a sitting, and it divides the rack's five columns exactly: three
# full rows, no ragged last one.

const AUTHORED_NAMES := [
	"THE COLLAPSE",
	"FOOTING",
	"DISTANT NEIGHBOURS",
	"NODES AND LOCKS",
	"EMBERFALL",
	"SUBSTRATE",
	"THE FAR SIDE",
	"THRESHOLD",
	"ASH TERRACE",
	"SINGULARITY",
]
const AUTHORED_SIZES := [15, 15, 15, 15, 15, 15, 15, 15, 15, 15]

const VAULT_A := ["IRON", "SALT", "GLASS", "ASH", "BONE", "SLATE", "AMBER", "TIDE", "EMBER", "FROST", "COPPER", "SHALE"]
const VAULT_B := ["REACH", "WELL", "SPINE", "GATE", "HOLLOW", "WORKS", "MARCH", "VAULT", "TERRACE", "CANT"]
const NAME_A := ["THE", "LOW", "HIGH", "OLD", "DEEP", "LONG", "BLIND", "CLOSE", "FAR", "LOST"]
const NAME_B := ["CANT", "HINGE", "LATCH", "WARD", "DROP", "SPINE", "SEAM", "FOLD", "TURN", "GATE", "WELL", "STEP", "CROSS", "MOUTH", "KEEP"]

# PAST THE AUTHORED TEN A VAULT WOULD GROW. Short vaults rank sooner and rank
# more people, so the early ones stay small; growing by a constant splits the
# difference — the ABSOLUTE step stays flat while the RELATIVE one collapses on
# its own. VaultGrow is zero today because the ladder ends at the tenth.
const VAULT_TAIL0 := 15
const VAULT_GROW := 0
const RANKED_VAULTS := 10

# The last cube, and there is nothing after it but the daily. A hundred and
# fifty that each say something beats three hundred where half are practice.
const LAST_CUBE := 150

# THE CATALOGUE ENDS AT A HUNDRED AND FIFTY and the seed box ends with it. This
# was a hundred thousand, which was right when the ladder was a pure function of
# its number and wrong now that it is a list: typing 900 used to mint a cube and
# would now fall through to a generated one that no other player has, which is
# the opposite of what a fixed ladder is for.
const MAX_CUBE := LAST_CUBE

# THE LAST VAULT, AND THERE IS ONE OF THOSE NOW TOO.
#
# vault_size and vault_start still answer for any band handed to them, because
# they are the arithmetic of a ladder that used to be infinite and a save
# written under that ladder has to keep meaning what it meant. But nothing may
# BROWSE past this: the rack's arrow had no ceiling, so pressing it enough times
# reached VAULT 45 · EMBER SPINE — a made-up name over a hundred and fifty
# cards, none of which is a cube.
const LAST_BAND := RANKED_VAULTS - 1

const _AT := []          # cumulative cube counts; see _ensure
const _BAKED := []       # the Baked script, loaded late; see _baked


# THE AUTHORED CUBES, REACHED AT RUNTIME RATHER THAN NAMED AT COMPILE TIME.
#
# level_name asks Baked whether a cube has an authored name, and a BakedLevel
# asks Vaults which chapter it is in. Naming each other as global classes is a
# cycle Godot refuses outright — both scripts then fail to load, with an error
# that names neither the pair nor the reason. Deferring one side to a load()
# breaks it, and the script is kept because loading it per call would be a file
# lookup inside a name lookup.
static func _baked():
	if _BAKED.empty():
		_BAKED.append(load("res://core/Baked.gd"))
	return _BAKED[0]


static func _ensure() -> void:
	if not _AT.empty():
		return
	_AT.append(0)
	for i in range(AUTHORED_SIZES.size()):
		_AT.append(_AT[i] + AUTHORED_SIZES[i])


static func authored_count() -> int:
	return AUTHORED_SIZES.size()


static func vault_end() -> int:
	_ensure()
	return _AT[AUTHORED_SIZES.size()]


# PROGRESS IS THE NEXT CUBE, AND PAST THE END THERE ISN'T ONE.
#
# Clearing cube n sets reached to n+1, which is the right shape for a hundred
# and forty-nine of them and off the end of the world for the last: reached
# becomes 151, and every consumer of it then asks for a cube the catalogue does
# not have. LevelSupply answers that by MINTING one — a hundred and fifty-first
# cube that no other player has, out of a generator that exists for the daily
# and nothing else.
#
# So the two questions are separated and both are asked here. Resume is what to
# PLAY, and it never leaves the ladder. Cleared is whether the machine is
# finished, which is a fact about the save and not a cube number.
static func resume(reached: int) -> int:
	if reached < 1:
		return 1
	return LAST_CUBE if reached > LAST_CUBE else reached


# HAS THE MACHINE EVER BEEN FINISHED, which is not a question about `reached`
# any more. Finishing relocks the ladder and hands it back at cube one, so a
# save that has been to the core looks exactly like a save that has not — except
# for this. See Store.runs.
static func cleared(runs: int) -> bool:
	return runs > 0


# DOES CLEARING THIS END THE MACHINE?
#
# A daily borrows the difficulty and is not the ladder; a made cube is
# somebody's own and ends nothing. Everything else that is the last cube gets
# the ending — INCLUDING A PRACTICE RUN, and that is the part that was wrong.
#
# What practice withholds is PROGRESS: no best, no par, no rank, nothing written
# down. It was never meant to withhold what a cube IS, and cube a hundred and
# fifty is the end of the machine whoever is standing on it. TYPING 150 IS ALSO
# HOW ANYBODY TESTS THIS — it is the only way to reach the ending without
# solving a hundred and forty-nine cubes first — so the one path a developer
# takes was the one path that silently did something else.
static func ends_the_machine(level_no: int, daily: bool, made: bool) -> bool:
	return not daily and not made and level_no >= LAST_CUBE


static func vault_size(band: int) -> int:
	if band < AUTHORED_SIZES.size():
		return AUTHORED_SIZES[band]
	return VAULT_TAIL0 + (band - AUTHORED_SIZES.size()) * VAULT_GROW


# First level number (1-based) of a vault.
static func vault_start(band: int) -> int:
	_ensure()
	if band < AUTHORED_SIZES.size():
		return _AT[band] + 1
	# the first b tail vaults hold VaultTail0*b + grow*(b(b-1)/2) cubes between
	# them, and b(b-1) is even at every b, so this is exact integer arithmetic
	var b := band - AUTHORED_SIZES.size()
	return vault_end() + VAULT_TAIL0 * b + VAULT_GROW * (b * (b - 1) / 2) + 1


# NO CLOSED FORM ON THE WAY BACK, ON PURPOSE. Inverting that sum wants a square
# root, and a float landing a hair either side of a boundary would put the same
# cube in different vaults on different devices — a different palette, a
# different board, and past this a different cube. This walks in integers and
# gives one answer everywhere.
static func vault_of(level: int) -> int:
	_ensure()
	var i := level - 1
	if i < vault_end():
		for b in range(AUTHORED_SIZES.size()):
			if i < _AT[b + 1]:
				return b
	var band := AUTHORED_SIZES.size()
	var end := vault_end()
	while true:
		end += vault_size(band)
		if i < end:
			return band
		band += 1
	return band


static func vault_name(band: int) -> String:
	if band < AUTHORED_NAMES.size():
		return AUTHORED_NAMES[band]
	# THE STRIDE HAS TO BE COPRIME WITH THE LIST LENGTH OR MOST OF THE LIST
	# NEVER APPEARS. This was band*5 against ten words, and 5 and 10 share a
	# factor, so every generated vault was called REACH or WORKS.
	return VAULT_A[(band * 7) % VAULT_A.size()] + " " + VAULT_B[(band * 3) % VAULT_B.size()]


static func level_name(level: int) -> String:
	var b = _baked().try_at(level)
	if b != null:
		return b.name
	# hash_seed is unsigned 32-bit; a signed shift here indexed negatively and
	# named half the cubes past level 40 "undefined".
	var h := Rng.hash_seed(level)
	return NAME_A[h % NAME_A.size()] + " " + NAME_B[(h >> 5) % NAME_B.size()]


# TWENTY OF THEM, BECAUSE THERE WERE TWENTY VAULTS. This stopped at XII, which
# was plenty when the ladder was open-ended and nobody was expected to browse
# the far end of it — but the rack now ends at a real vault. The fallback stays
# for a band that should not be reachable at all.
const ROMAN := [
	"I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
	"XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX",
]


static func roman_of(band: int) -> String:
	return ROMAN[band] if band < ROMAN.size() else str(band + 1)
