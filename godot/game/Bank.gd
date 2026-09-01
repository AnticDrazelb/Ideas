class_name Bank
# A RECORDING, IF THERE IS ONE.
#
# Every sound in this game is generated: two primitives in Synth, layered and
# offset in time, and the offsets are what make a thud plus a ping read as sonar
# rather than as two beeps. That was the only option while importing an asset was
# against the rules, and it is genuinely good — it is also the ceiling. There is
# no synthesised eighty milliseconds of servo that sounds like a servo the way a
# servo does.
#
# So this is the seam. Drop a clip at assets/audio/<name>.ogg or .wav and that
# cue plays the recording; leave it out and the cue synthesises itself, exactly
# as it does today. Nothing is all-or-nothing: a game with a recorded footstep
# and eighteen synthesised cues is a valid state and probably the state this
# passes through.
#
# WHY A LOOKUP RATHER THAN A FIELD PER CUE. A serialised list of twenty streams
# would be faster and would also be a scene, an inspector, and twenty chances to
# wire the fold sound to the undo slot. The names are in the cue methods next to
# the synthesis they replace, which is the same argument the captions won: the
# failure mode of a table somewhere else is a cue that gets added and never
# appears in it.
#
# MISSES ARE CACHED TOO, and that is not an optimisation, it is the whole reason
# this is safe to call from a cue. A load on a path with nothing at it is not
# free, and the common case here is nineteen misses, several times a second,
# forever.

const ROOT := "res://assets/audio/"
const EXTS := [".ogg", ".wav"]

const _CACHE := {}


# The recording for a cue, or null if the game should synthesise it.
static func get_clip(clip_name: String):
	if _CACHE.has(clip_name):
		return _CACHE[clip_name]
	var found = null
	for ext in EXTS:
		var path: String = ROOT + clip_name + ext
		if ResourceLoader.exists(path):
			found = load(path)
			if found != null:
				break
	_CACHE[clip_name] = found
	return found


static func has(clip_name: String) -> bool:
	return get_clip(clip_name) != null


# Forget everything. Nothing in the game calls this; a tool that lists the bank
# would.
static func forget() -> void:
	_CACHE.clear()


# EVERY CUE THAT WILL LOOK FOR A RECORDING, so that the answer to "what can I
# replace" is a list rather than a grep. Kept next to nothing — it is
# documentation, and the cues themselves name their own file. If the two disagree
# the cue wins and this is the thing that is wrong.
const CUES := [
	"step",       # the move: sub-bass thud, then the ping off the far wall
	"land",       # arriving after a fall
	"fold",       # the servo: whirr, static, and the detent knock
	"creak",      # the mechanism complaining while it is dragged by hand
	"deny",       # a refusal, in the dark
	"node",       # glass, a fifth higher for each one taken
	"lock",       # a barrier opening
	"win",        # the collapse
	"vault",      # a vault beginning
	"plate",      # the lattice inverting
	"plate-off",  # and springing back. A recording cannot be run backwards
	              # convincingly, so the two directions are two files.
	"tick",       # the plate clock, which is a sound because the eyes are elsewhere
	"undo",       # the tape running backwards
	"stuck",      # two clicks and a low held note
	"peek",       # the drive spinning up
	"peekoff",    # and down
	"evert",      # the engine turning through itself
	"shatter",    # and coming apart, half a second after the core takes you
	"bed",        # the room itself, looped
]
