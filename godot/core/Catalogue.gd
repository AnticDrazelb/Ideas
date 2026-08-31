class_name Catalogue
# THE HUNDRED AND FIFTY, AND THEY ARE THE SAME HUNDRED AND FIFTY FOR EVERYBODY.
#
# The ladder used to be minted on the device: cube four hundred was a pure
# function of the number four hundred, so it was the same puzzle everywhere,
# which was the right instinct and cost the game its difficulty curve. The audit
# measured where — mean par climbs 2.71 to 5.17 by cube 120 and then stops,
# going backwards over the last two tenths while the board size sits at nine.
# From the halfway point the game was the same difficulty forever.
#
# So the ladder is cut once, offline, out of a hundred thousand candidates, and
# shipped as text. What that buys is not only a curve: it is the first time this
# game can END, and the first time a cube can be chosen for being GOOD rather
# than for clearing a band.
#
# IT IS A TEXT ASSET AND NOT A GDSCRIPT ARRAY, deliberately: a hundred and fifty
# literals is a file nobody can review and two people cannot merge. One cube a
# line — par, steps, identity, share code — so a change to the catalogue is a
# diff somebody can read.
#
# THE ORDER IS FROZEN THE DAY IT SHIPS. Progress is an index into this list.
# Reorder or replace a cube and every player's save now points at a different
# puzzle, with their bests and pars attached to numbers that changed meaning
# underneath them. After release it is append-only, and the harness pins the
# whole thing by checksum so an accidental edit fails a build instead of
# shipping.

const PATH := "res://assets/catalogue.txt"

const _ALL := []
const _IDS := {}
const _LOADED := []


class Entry:
	var par := 0
	var steps := 0
	var id := ""
	var code := ""


# Hand the catalogue its text. Boot calls this with the asset and the harness
# calls it from the file it just cut, which is what keeps the two reading
# exactly the same thing.
static func load_text(text) -> void:
	_ALL.clear()
	_IDS.clear()
	if not _LOADED.empty():
		_LOADED.clear()
	_LOADED.append(true)
	if text == null or text == "":
		return
	for raw in text.split("\n"):
		var line: String = raw.strip_edges()
		if line.length() == 0 or line[0] == "#":
			continue

		# A DASH IS A HOLE AND IT KEEPS ITS PLACE. A slot the cut could not fill
		# must still occupy its number, or every cube after it moves and every
		# save in the world points one puzzle to the left.
		if line == "-":
			_ALL.append(Entry.new())
			continue

		var p := line.split(" ", false)
		if p.size() < 4:
			_ALL.append(Entry.new())
			continue
		if not p[0].is_valid_integer() or not p[1].is_valid_integer():
			_ALL.append(Entry.new())
			continue
		var e := Entry.new()
		e.par = int(p[0])
		e.steps = int(p[1])
		e.id = p[2]
		e.code = p[3]
		_ALL.append(e)


# Read the shipped asset once. Boot does this explicitly; anything that asks
# before it has is served rather than handed an empty ladder, so a test or a
# tool needs no ceremony.
static func _ensure() -> void:
	if not _LOADED.empty():
		return
	var f := File.new()
	if f.open(PATH, File.READ) == OK:
		var text := f.get_as_text()
		f.close()
		load_text(text)
	else:
		_LOADED.append(true)


# Every cube, in order. Empty if the asset is missing.
static func all() -> Array:
	_ensure()
	return _ALL


static func count() -> int:
	return all().size()


static func has(level: int) -> bool:
	var a := all()
	return level >= 1 and level <= a.size() and a[level - 1].code != ""


# The cube at a level number, decoded. Null if there is not one — the caller
# falls back to the generator exactly as it always has, which is what keeps a
# missing asset a degraded game rather than no game.
static func get_level(level: int):
	if not has(level):
		return null
	var e: Entry = all()[level - 1]
	var lv = ShareCode.decode(e.code)
	if lv == null:
		return null
	lv.level = level
	lv.par = e.par
	lv.steps = e.steps
	lv.name = Vaults.level_name(level)
	return lv


# Every identity in the catalogue, for the Forge's duplicate check. Eight
# characters each — a kilobyte for the whole ladder — because computing them
# would be twenty-four canonical forms a cube and the better part of a minute on
# a phone.
static func ids() -> Dictionary:
	if not _IDS.empty():
		return _IDS
	for e in all():
		if e.id != "":
			_IDS[e.id] = true
	return _IDS


# A checksum over the whole ladder, in order. The one number that says "this is
# the catalogue that shipped" — see the note on the class about why the order is
# not allowed to move.
static func stamp() -> String:
	var h := 2166136261
	for e in all():
		var s: String = e.id if e.id != "" else "-"
		for i in range(s.length()):
			h = Bits.imul(h ^ ord(s[i]), 16777619) & 0xFFFFFFFF
		h = Bits.imul(h ^ e.par, 16777619) & 0xFFFFFFFF
	return "%08x" % h
