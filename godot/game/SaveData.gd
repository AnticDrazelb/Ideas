class_name SaveData
extends Reference
# THE SAVE, FIELD BY FIELD.
#
# `best` is fewest folds per cube, `tbest` is fastest clear in ms per cube,
# `vbest` is the posted total for a whole vault keyed by band. Missing keys are
# the honest record of a cube nobody has cleared, which is what makes "is this
# vault finished" a lookup rather than a guess.
#
# THE PAIRED LISTS ARE THE WIRE FORMAT AND NOT THE WORKING ONE. Unity's
# JsonUtility cannot serialise a Dictionary, so the C# save carries each map as
# two parallel lists and rehydrates them at load. Godot's JSON has no such
# limitation — but the format is what a player's phone already has on it, and a
# port that quietly rewrites the save is a port that cannot be rolled back. The
# lists stay, and Store rehydrates them exactly as the C# does.

var best_k := []
var best_v := []
# A BEST IS MEANINGLESS WITHOUT THE PAR IT WAS SCORED AGAINST. Par for a
# generated cube costs real time to mint, so a grid of ten cannot ask for it —
# but it is free at the moment the cube is cleared, which is the only moment
# anything needs it. Recorded then.
var par_k := []
var par_v := []
var tbest_k := []
var tbest_v := []
var vbest_k := []
var vbest_v := []

var reached := 1
var sound := 1
var haptic := 1
var fx := 1
var togo := 1
var depth := 0
var taught := 0
var saw_plate := 0
var saw_peek := 0
var peek_hinted := 0
var taught_forge := 0

# One bit a chapter: the word the machine says after it has been said. See
# ui/Chapters — going back for a par must not replay it.
var said_chapter := 0

# HOW MANY TIMES THE MACHINE HAS BEEN TAKEN TO THE CORE.
#
# This used to be readable off `reached` — a save past the last cube had
# finished — and that stopped being true the moment finishing SENDS YOU BACK.
# The two facts are different: `reached` is how far along this run you are,
# `runs` is whether the machine has ever been finished at all, and only the
# second one should change what the title offers.
var runs := 0

# THE WAY OUT OF THE SOFT LOCK, and it is a setting rather than a reward.
#
# Finishing relocks the ladder and hands it back at cube one, which is the right
# default — the machine says goodbye and starts again, and chapter II's word is
# `again.` for a reason. It is the wrong ONLY option: somebody who finished last
# month and wants to show a friend cube 150 should not have to solve a hundred
# and forty-nine cubes to get there.
var unlocked := 0

var buzz := 100
var bright := 100
var contrast := 100

# ---- access ---------------------------------------------------------------
#
# ONE SWITCH WAS ANSWERING TWO PEOPLE. `motion` is the camera and the bent clock
# — vestibular; `light` is sparks, bloom and the full-screen flash —
# photosensitive. They were both `fx`, they are different criteria with
# different answers, and each now has an OFF rather than a quieter setting.
var motion := 0                     # 0 full, 1 reduced, 2 still
var light := 0                      # 0 full, 1 reduced, 2 none
var legible := 0
var captions := 0
var assist := 0
var vol := 100
var room := 100

# A SAVE THAT PREDATES A SETTING HAS AN OPINION ABOUT IT ANYWAY.
#
# Motion used to be half of the EFFECTS bit, so a player who turned effects off
# had already said they wanted less movement — and a new field defaulting to
# zero would silently hand them full camera shake back on the update that was
# supposed to help them. The version rides with the save so that intent can be
# carried across exactly once.
var v := 0

var daily_day := -1
var daily_best := 0
var daily_solved := 0
var dhist_day := []
var dhist_folds := []
var streak_cur := 0
var streak_best := 0
var streak_last := -1
var post_w := -1
var post_m := -1
var post_s := -1

var made := []                      # Array of MadeCube
var draft := ""
var hidden := []


const _INTS := [
	"reached", "sound", "haptic", "fx", "togo", "depth", "taught",
	"runs", "unlocked", "buzz", "bright", "contrast",
	"motion", "light", "legible", "captions", "assist", "vol", "room", "v",
	"streak_cur", "streak_best", "streak_last", "post_w", "post_m", "post_s",
]

# The three names that differ between the C# field and the JSON key, because the
# C# spells them in camel case and this file spells them the way GDScript does.
# The KEY is what is on disk and must not move.
const _RENAMED := {
	"saw_plate": "sawPlate", "saw_peek": "sawPeek", "peek_hinted": "peekHinted",
	"taught_forge": "taughtForge", "said_chapter": "saidChapter",
	"best_k": "bestK", "best_v": "bestV", "par_k": "parK", "par_v": "parV",
	"tbest_k": "tbestK", "tbest_v": "tbestV", "vbest_k": "vbestK", "vbest_v": "vbestV",
	"daily_day": "dailyDay", "daily_best": "dailyBest", "daily_solved": "dailySolved",
	"dhist_day": "dhistDay", "dhist_folds": "dhistFolds",
	"streak_cur": "streakCur", "streak_best": "streakBest", "streak_last": "streakLast",
	"post_w": "postW", "post_m": "postM", "post_s": "postS",
}


static func _key_of(field: String) -> String:
	return _RENAMED[field] if _RENAMED.has(field) else field


func to_dict() -> Dictionary:
	var d := {}
	for f in _INTS:
		d[_key_of(f)] = int(get(f))
	for f in ["saw_plate", "saw_peek", "peek_hinted", "taught_forge", "said_chapter"]:
		d[_key_of(f)] = int(get(f))
	for f in ["best_k", "best_v", "par_k", "par_v", "tbest_k", "tbest_v",
			"vbest_k", "vbest_v", "dhist_day", "dhist_folds", "hidden"]:
		d[_key_of(f)] = get(f).duplicate()
	d["draft"] = draft
	var ms := []
	for m in made:
		ms.append(m.to_dict())
	d["made"] = ms
	return d


# THE READER LIVES IN Store, not here. GDScript will not let a class name
# itself, so a factory returning a SaveData cannot be written inside
# SaveData.gd — see Store._read_save, which is the same code with the one line
# that says the word moved across the file boundary.


# Is this a SaveData or only shaped like one?
#
# The paired lists are checked for agreeing lengths. The rehydrate already walks
# the shorter of the two, so a mismatch cannot crash — it silently drops the
# tail of somebody's records, which is worse, because nothing anywhere would
# ever say so.
func whole() -> bool:
	return best_k.size() == best_v.size() \
			and par_k.size() == par_v.size() \
			and tbest_k.size() == tbest_v.size() \
			and vbest_k.size() == vbest_v.size() \
			and dhist_day.size() == dhist_folds.size()
