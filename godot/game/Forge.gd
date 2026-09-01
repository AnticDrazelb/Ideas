extends Reference
class_name Forge

# THE FORGE — the editor, as a model.
#
# A CUBE IS EDITED ONE DECK AT A TIME. Direct manipulation of a projected solid is
# a lovely demo and a miserable tool: the thing you want to click is behind the
# thing you can see, and on a phone your thumb covers both. A deck is an n-by-n
# grid you can hit accurately, and the deck stepper is the only spatial reasoning
# the editor asks for.
#
# CUSTOM CUBES ARE NOT RANKED, and that is not an oversight. Every board in this
# game is defended by par being the solver's proven minimum on a cube whose
# identity can be checked. A cube the player wrote themselves has neither property
# — you could author a one-fold cube and post a perfect score in ten seconds. They
# keep their own bests locally and touch nothing else.
#
# Nothing from the engine here on purpose: the whole editor can be driven from a
# test.

# The tools, as [byte, name]. The bytes are Level's own cell codes.
const TOOLS := [
	[Level.VOID, "VOID"], [Level.LATTICE, "LATTICE"], [Level.TRACE, "TRACE"],
	[Level.PLATE_A, "PLATE A"], [Level.PLATE_B, "PLATE B"],
	[83, "START"], [71, "EXIT"], [75, "NODE"],
	# WIPE, not WIPE DECK. bracketed() renders [ WIPE DECK ] at 109.2 units into a
	# slot of 101 and it printed [ WIPE DEC. The deck it wipes is named twice on the
	# same screen — the stepper above it says DECK 1/5 — so the second word was
	# costing eight units to repeat something the player is already looking at.
	[68, "LOCK"], [67, "WIPE"],
]

# The four tools that are not cell types, spelled out so no reader has to decode
# a byte to follow Apply.
const T_START := 83     # S
const T_EXIT := 71      # G
const T_NODE := 75      # K
const T_LOCK := 68      # D
const T_WIPE := 67      # C

# The generator reaches nine, so the Forge does.
const SIZES := [5, 6, 7, 8, 9]

# The mark codes MarkAt answers in. 0 is "no mark".
const M_START := 83     # S
const M_EXIT := 69      # E
const M_NODE := 78      # N
const M_LOCK := 76      # L

var n := 0
var vox: PoolByteArray
# GDScript has no nullable struct, so an unplaced mark is a null rather than a
# Vector3 — every read of these is guarded exactly where the C# checks HasValue.
var start = null
var goal = null
var keys := []          # Array of Vector3
var doors := []         # Array of Vector3
var layer := 0
var tool := Level.TRACE
var name := ""

# The id of the saved cube being edited, or null for a new one.
var from = null

# A VERIFY THAT FAILED, so the coach can say the other thing. The step that asks
# for a verify has to survive being wrong: a first cube very often will not solve,
# and "hit verify" repeated at somebody who just did is the least useful sentence
# available. Failure is part of the lesson, so the step stays open and changes what
# it says.
var missed := false

# Set by a passing verify and cleared by any edit.
var proven := false


# ---- construction ----------------------------------------------------------

static func make(size: int = 5) -> Forge:
	var f: Forge = Make.of("res://game/Forge.gd")
	f.n = size
	f.vox = VoxRef.void_cells(size * size * size, Level.VOID)
	return f


# Start and goal are optional here and nowhere else. A SAVED cube always has both
# — nothing without them passes validation — but a DRAFT is a cube in the middle of
# being built, and the first thing a player does is place voxels, not a start.
static func from_record(rec: MadeCube) -> Forge:
	var lv: Level = ShareCode.decode(rec.code)
	if lv == null:
		return make(5 if rec.n <= 0 else rec.n)
	var f := make(lv.n)
	f.vox = lv.vox
	f.start = lv.start
	f.goal = lv.goal
	f.keys = lv.keys.duplicate()
	f.doors = lv.doors.duplicate()
	f.name = rec.name
	f.from = rec.key
	return f


# Growing keeps everything and adds empty space; shrinking keeps what still fits
# and drops what does not, marks included — silently keeping a start that is now
# outside the cube would fail validation with no way to see why.
func resize(to: int) -> void:
	var v := VoxRef.void_cells(to * to * to, Level.VOID)
	var m: int = to if to < n else n
	for y in range(m):
		for z in range(m):
			for x in range(m):
				v[Level.vidx_n(to, x, y, z)] = vox[Level.vidx_n(n, x, y, z)]

	var k2 := []
	var d2 := []
	for idx in range(min(keys.size(), doors.size())):
		if _fits(keys[idx], to) and _fits(doors[idx], to):
			k2.append(keys[idx])
			d2.append(doors[idx])

	proven = false
	n = to
	vox = v
	keys = k2
	doors = d2
	if not _fits(start, to):
		start = null
	if not _fits(goal, to):
		goal = null
	if layer >= to:
		layer = to - 1
	save_draft()


static func _fits(w, to: int) -> bool:
	return w != null and w.x < to and w.y < to and w.z < to


# ---- marks -----------------------------------------------------------------

func mark_at(w: Vector3) -> int:
	if start != null and start == w:
		return M_START
	if goal != null and goal == w:
		return M_EXIT
	if keys.find(w) >= 0:
		return M_NODE
	if doors.find(w) >= 0:
		return M_LOCK
	return 0


# A mark can only sit on something you can stand on, so emptying a cell has to
# take its role with it rather than leave a start floating in a void.
func _unmark(w: Vector3) -> void:
	if start != null and start == w:
		start = null
	if goal != null and goal == w:
		goal = null
	var idx := keys.find(w)
	if idx >= 0:
		keys.remove(idx)
		if idx < doors.size():
			doors.remove(idx)
	var j := doors.find(w)
	if j >= 0:
		doors.remove(j)
		if j < keys.size():
			keys.remove(j)


func apply(x: int, z: int) -> void:
	# ANY EDIT UNPROVES IT. A par is the solver's answer about one exact cube, so
	# the moment a cell changes the old answer is about a cube that no longer exists
	# — and a stale VERIFIED is worse than none.
	proven = false

	var w := Vector3(x, layer, z)
	var idx := Level.vidx_n(n, x, layer, z)

	if tool == T_START or tool == T_EXIT:
		if not Level.is_walk_type(vox[idx]):
			vox[idx] = Level.TRACE
		if tool == T_START:
			if goal != null and goal == w:
				goal = null
			start = w
		else:
			if start != null and start == w:
				start = null
			goal = w
	elif tool == T_NODE or tool == T_LOCK:
		# Nodes and locks are added and removed IN PAIRS, because a node with no lock
		# is decoration and a lock with no node is a wall.
		var at: int = keys.find(w) if tool == T_NODE else doors.find(w)
		if at >= 0:
			if at < keys.size():
				keys.remove(at)
			if at < doors.size():
				doors.remove(at)
		else:
			if not Level.is_walk_type(vox[idx]):
				vox[idx] = Level.TRACE
			if tool == T_NODE:
				keys.append(w)
			else:
				doors.append(w)
	elif tool == T_WIPE:
		clear_deck()
	else:
		vox[idx] = tool
		if not Level.is_walk_type(tool):
			_unmark(w)

	save_draft()


func clear_deck() -> void:
	proven = false
	for z in range(n):
		for x in range(n):
			vox[Level.vidx_n(n, x, layer, z)] = Level.VOID
			_unmark(Vector3(x, layer, z))
	save_draft()


# ---- the draft -------------------------------------------------------------
#
# AN UNSAVED CUBE SURVIVES THE APP BEING KILLED.
#
# Android reclaims a backgrounded Activity whenever it likes, and half an hour of
# placing voxels is exactly the kind of work a player does not get to do twice.
# The draft mirrors the editor after every change; it is not a save, it is the
# editor's own memory of where it was — an unverified, unnamed, half-built cube is
# not a thing the shelf can hold, and it is the thing most worth not losing.
#
# Store already had the field. Nothing ever wrote it, which is the worst of both:
# the save file carried a promise the editor never kept.
#
# Its own format rather than a share code, because a share code needs a start and
# an exit and the whole point of a draft is the cube that does not have them yet.
# Every field is either a number, a base64url id or a name already folded to
# capitals, digits, spaces and dashes — so no separator can ever appear inside a
# value.

const D_FIELD := "|"
const D_LIST := ","
const D_AXIS := "."


static func _pt(w) -> String:
	return "" if w == null else (str(int(w.x)) + D_AXIS + str(int(w.y)) + D_AXIS + str(int(w.z)))


static func _un_pt(s: String):
	if s == "":
		return null
	var a := s.split(D_AXIS)
	if a.size() != 3:
		return null
	for part in a:
		if not part.is_valid_integer():
			return null
	return Vector3(int(a[0]), int(a[1]), int(a[2]))


static func _pt_list(l: Array) -> String:
	var out := PoolStringArray()
	for w in l:
		out.append(_pt(w))
	return out.join(D_LIST)


static func _un_pt_list(s: String) -> Array:
	var l := []
	if s == "":
		return l
	for part in s.split(D_LIST):
		var w = _un_pt(part)
		if w != null:
			l.append(w)
	return l


func save_draft() -> void:
	var parts := PoolStringArray([
		"1", str(n), str(layer),
		"" if from == null else from,
		name,
		vox.get_string_from_ascii(),
		_pt(start), _pt(goal),
		_pt_list(keys), _pt_list(doors),
	])
	Store.data().draft = parts.join(D_FIELD)
	Store.save()


static func drop_draft() -> void:
	Store.data().draft = ""
	Store.save()


# The editor the player left, or a fresh one. A draft that does not parse is a
# draft from an older build or a corrupted save, and the answer to both is the
# same as the answer to no draft at all.
static func resume() -> Forge:
	var d: String = Store.data().draft
	if d == "":
		return make()

	var f := d.split(D_FIELD)
	if f.size() < 10 or f[0] != "1":
		return make()
	if not f[1].is_valid_integer():
		return make()
	var size := int(f[1])
	if size < 2 or size > 9:
		return make()
	if f[5].length() != size * size * size:
		return make()

	var lay: int = int(f[2]) if f[2].is_valid_integer() else 0
	var fg := make(size)
	fg.vox = f[5].to_ascii()
	fg.from = null if f[3] == "" else f[3]
	fg.name = f[4]
	fg.start = _un_pt(f[6])
	fg.goal = _un_pt(f[7])
	fg.keys = _un_pt_list(f[8])
	fg.doors = _un_pt_list(f[9])
	fg.layer = 0 if (lay < 0 or lay >= size) else lay
	return fg


# ---- what comes out --------------------------------------------------------

# A NAME IS THE ONLY FREE TEXT IN THIS GAME, so it is folded to the character set
# the interface already speaks — capitals, digits, spaces and a dash. That is a
# house-style decision and, usefully, it also means no authored string can ever be
# markup: a cube called `<img onerror=...>` imported from a stranger's share code
# is simply not representable.
static func clean_name(s: String) -> String:
	var out := ""
	var last_space := false
	for c in s.to_upper():
		var ok: bool = (c >= "A" and c <= "Z") or (c >= "0" and c <= "9") or c == " " or c == "-"
		if not ok:
			continue
		if c == " ":
			if last_space or out.length() == 0:
				continue
			last_space = true
		else:
			last_space = false
		out += c
		if out.length() >= 18:
			break
	return out.strip_edges()


func to_level() -> Level:
	var lv := Level.new()
	lv.n = n
	lv.vox = vox
	lv.start = Vector3.ZERO if start == null else start
	lv.goal = Vector3.ZERO if goal == null else goal
	lv.keys = keys.duplicate()
	lv.doors = doors.duplicate()
	lv.par = 0
	lv.name = "UNTITLED" if name == "" else name
	return lv


# WHAT TO DO NEXT, IN ONE SENTENCE.
#
# An empty grid and ten unlabelled tools is not an editor, it is a puzzle about an
# editor. This reads the cube as it stands and names the single next thing standing
# between it and a saveable cube — always one thing, never a checklist, because a
# checklist is read once and a next step is read every time it changes.
#
# It lives here rather than in the screen for the same reason the rest of this
# class does: it is a statement about the cube, it needs no engine, and a test can
# ask it what it would say. Returns [step, say].
func advice() -> Array:
	var trace := 0
	for c in vox:
		if Level.is_walk_type(c):
			trace += 1

	# enough to be a route rather than a spot — the smallest cube the generator will
	# cut has eight, so the editor asks for eight
	var floor_traces := 8
	if trace < floor_traces:
		var short := str(floor_traces - trace)
		return [1, "TRACE IS WHAT YOU STAND ON. TAP CELLS TO LAY " + short + " MORE OF IT."]

	# THE TWO STEPS THAT WERE MISSING, AND WHY THE EDITOR LOOKED BROKEN.
	#
	# This went straight from "lay some trace" to "place a start", and a player who
	# did exactly what it said built one flat deck — which the validator then
	# refused, correctly, with NO FOLD NEEDED. Following the coach to the letter
	# produced a cube that could not be saved, and nothing anywhere said the word
	# DECK.
	#
	# A cube that folds needs height and it needs the exit somewhere a walk will not
	# reach. Those are the two ideas this whole editor is about, so they are steps
	# rather than something to find out from a refusal.
	if layer == 0 and decks() < 2:
		return [2, "A CUBE HAS DECKS. STEP UP ONE WITH THE ARROW ABOVE."]

	if decks() < 2 or trace < 12:
		return [3, "LAY TRACE UP HERE TOO. A FLAT CUBE FOLDS INTO NOTHING."]

	if start == null:
		return [4, "PICK START, THEN TAP THE CELL YOU BEGIN ON."]

	# THE COMMONEST REFUSAL IN THE EDITOR, SAID BEFORE YOU PRESS VERIFY.
	#
	# Across four hundred plausible cubes, more than half came back with START IS
	# BURIED — and it is a rule the deck grid cannot show you, because the deck grid
	# is one horizontal slice and burying is vertical. You are looking at the start;
	# the thing that makes it illegal is on the deck above, which is not on screen.
	#
	# It got worse the moment the coach started doing its job: the step before this
	# one asks for a second deck, and a second deck is exactly what buries a start.
	# So the same test the validator runs is run here, on the same projection, and
	# the answer arrives while the player is still looking at the cell rather than
	# three taps later.
	if not start_shows():
		return [4, "THAT CELL IS BEHIND ANOTHER ONE. THE START HAS TO BE A FACE THE CUBE SHOWS — THE RINGED CELLS."]

	if goal == null:
		return [5, "NOW EXIT — AND PUT IT WHERE A WALK WILL NOT REACH, SO GETTING THERE TAKES A FOLD."]

	if not proven:
		var say: String = "AN UNSOLVABLE CUBE IS NOT A HARD ONE, IT IS A BROKEN ONE. GIVE THE EXIT A ROUTE AND VERIFY AGAIN." if missed \
				else "HIT VERIFY. THE SOLVER HAS TO PROVE THIS CAN BE DONE BEFORE IT COUNTS."
		return [6, say]

	var done: String = "PROVEN. GIVE IT A NAME AND SAVE IT." if name == "" \
			else "PROVEN. SAVE IT — AND YOU GET A CODE, WHICH IS HOW SOMEBODY ELSE PLAYS IT."
	return [7, done]


# Is the start on a face of the unfolded cube? The validator's own test, on the
# validator's own projection — a second opinion here would be a second answer, and
# the player would get whichever one they asked last.
func start_shows() -> bool:
	if start == null:
		return true
	if not Level.is_walk_type(vox[Level.vidx_v(n, start)]):
		return false

	var lv := to_level()
	lv.clear_eff()
	var s0 := Projection.project(n, lv.eff(0), Ori.new())
	return Projection.surface_at(n, s0, Ori.new(), start)


# Is this cell one of the faces the cube shows, or is it behind another?
#
# THE DECK GRID IS A CROSS-SECTION, NOT A FLOOR PLAN, and that single misreading is
# behind most of what goes wrong in this editor. The cube is viewed along z, so the
# cell that hides another is not on the deck above — it is the one further up THIS
# deck, in plain sight, on the same screen. Anything solid in front of a cell hides
# it, gap or no gap, lattice as much as trace.
#
# The grid can therefore say so, which is the whole reason this is here: the
# frontmost solid of every column gets a lit ring and everything behind it does
# not. A player who can see which cells are faces stops putting starts on the ones
# that are not.
func on_face(x: int, z: int) -> bool:
	for zz in range(n - 1, z, -1):
		if vox[Level.vidx_n(n, x, layer, zz)] != Level.VOID:
			return false
	return true


# How many decks have anything to stand on. A flat cube cannot fold.
func decks() -> int:
	var d := 0
	for y in range(n):
		var any := false
		for z in range(n):
			if any:
				break
			for x in range(n):
				if Level.is_walk_type(vox[Level.vidx_n(n, x, y, z)]):
					any = true
					break
		if any:
			d += 1
	return d


# THE ONE ANSWER THE EDITOR OWES YOU: solvable, and new.
#
# Both are refusals rather than warnings. An unsolvable cube has no par and nothing
# in this game can score it; a duplicate is a cube that already exists, and being
# told so before you name it is kinder than being told after.
#
# Returns { ok, message, level } — level carries the proven par when ok.
func verify() -> Dictionary:
	if start == null or goal == null:
		missed = true
		return {"ok": false, "message": "NEEDS A START AND AN EXIT", "level": null}

	var lv := to_level()
	var v := Validation.validate(lv)
	if not v.ok:
		missed = true
		return {"ok": false, "message": v.why, "level": null}

	lv.par = v.par
	lv.steps = v.steps

	var mine := {}
	for m in Store.data().made:
		mine[m.name if m.name != "" else m.key] = m.id

	var col = Identity.collision_of(lv, from, mine)
	if col != null:
		return {"ok": false, "message": "THIS CUBE ALREADY EXISTS — " + _describe(col), "level": null}

	proven = true
	missed = false
	return {
		"ok": true,
		"level": lv,
		"message": "VERIFIED · PAR " + str(v.par) + " · " + str(v.steps) + " STEPS",
	}


static func _describe(c: Dictionary) -> String:
	if c.kind == "baked":
		return "IT IS " + c.name + ", CUBE " + str(c.level)
	if c.kind == "minted":
		return "IT IS CUBE " + str(c.level) + " — " + c.name
	return "YOU ALREADY BUILT IT: " + c.name


static func record(lv: Level) -> MadeCube:
	var id := Identity.canon_id(lv)
	var rec: MadeCube = Make.of("res://game/MadeCube.gd")
	rec.key = id
	rec.id = id
	rec.name = lv.name
	rec.n = lv.n
	rec.par = lv.par
	rec.code = ShareCode.encode(lv)
	rec.best = -1
	rec.tbest = -1
	return rec


# Editing a cube and saving it REPLACES it rather than forking it — the old
# identity is gone the moment the geometry changed. Returns [ok, message].
func save() -> Array:
	name = clean_name(name)
	var v := verify()
	if not v.ok:
		return [false, v.message]

	var lv: Level = v.level
	lv.name = "UNTITLED" if name == "" else name
	var rec := record(lv)

	if from != null and from != rec.key:
		var old = Store.made(from)
		if old != null:
			# a rebuild keeps the records the player earned on the cube it grew out of,
			# because it is the same author's same attempt
			rec.best = old.best
			rec.tbest = old.tbest
			Store.data().made.erase(old)

	var existing = Store.made(rec.key)
	if existing != null:
		Store.data().made.erase(existing)
	Store.data().made.append(rec)
	from = rec.key
	Store.save()

	return [true, "SAVED · PAR " + str(rec.par)]


func delete() -> void:
	if from == null:
		return
	var m = Store.made(from)
	if m != null:
		Store.data().made.erase(m)
	from = null
	# the draft is the editor's memory of THIS cube; leaving it behind would
	# resurrect what was just deleted on the next visit
	Store.data().draft = ""
	Store.save()


# Take a cube in from a share code somebody else pasted. Returns [ok, message].
static func import_code(code: String) -> Array:
	var lv: Level = ShareCode.decode(code)
	if lv == null:
		return [false, "THAT IS NOT A CUBE CODE"]

	var v := Validation.validate(lv)
	if not v.ok:
		return [false, "THAT CUBE IS BROKEN — " + v.why]
	lv.par = v.par
	lv.steps = v.steps
	lv.name = clean_name(lv.name)
	if lv.name == "":
		lv.name = "SHARED"

	var id := Identity.canon_id(lv)
	if Store.made(id) != null:
		return [false, "YOU ALREADY HAVE THAT CUBE"]

	var rec := record(lv)
	Store.data().made.append(rec)
	Store.save()

	return [true, "LOADED · " + lv.name + " · PAR " + str(v.par)]
