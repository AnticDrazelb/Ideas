class_name Store
# PERSISTENCE — one key, and it is allowed to fail.
#
# A device with storage disabled should still play; it just forgets. Every read
# and write here is wrapped, and a failure is a shrug rather than an exception,
# exactly as the original's localStorage handling was.
#
# Writes are debounced: a level clear touches several fields in a row and there
# is no reason to serialise the whole save five times for one event.
#
# THREE KEYS IN ONE ConfigFile, where Unity had three PlayerPrefs entries. The
# names are the same because the save format is the same.

const FILE := "user://singularity.cfg"
const SECTION := "save"
const KEY := "turnkey-v2"

# THE LAST SAVE THAT WAS KNOWN TO PARSE, AND THE ONE THAT DID NOT.
#
# A save is the only thing in this game a player cannot get back. The board is
# deterministic, the settings take a minute to redo — thirty vaults of bests are
# a hundred hours somebody spent, and there is no support channel to recover
# them from.
#
# Before this there was one key and one behaviour: a save that failed to parse
# was replaced with an empty one, and the next write — which is at most a
# quarter of a second later, because the debounce fires on the first setting the
# game touches — put the empty one over the top of it. The corrupt save was not
# ignored, it was DESTROYED, silently, with no copy anywhere.
#
# So BACKUP holds the previous string that parsed, written one step behind the
# live one, and is tried when the live one fails. QUARANTINE holds whatever
# could not be read at all, and is never written over — a player who lands there
# has lost their progress from the game's point of view and still has their
# bytes.
const BACKUP := "turnkey-v2-prev"
const QUARANTINE := "turnkey-v2-unreadable"

# Which of the three the game is actually running on. The interface asks,
# because a player whose save did not load has to be TOLD — starting somebody
# over without a word is the worst thing this code can do, and it is also the
# thing they will assume happened whenever a level list looks wrong.
enum Origin { FRESH, LIVE, RECOVERED, LOST }

# The single mutable cell this class has. Slot 0 is the SaveData, 1 the Origin,
# 2 the debounce deadline in milliseconds, 3 whether load() has run. See
# core/Turns.gd for why a const array is where a static variable lives.
const _S := []

const _BEST := {}
const _PARS := {}
const _TBEST := {}
const _VBEST := {}
const _DHIST := {}


static func _slots() -> Array:
	if _S.empty():
		_S.append(SaveData.new())
		_S.append(Origin.FRESH)
		_S.append(-1)
		_S.append(false)
	return _S


static func data() -> SaveData:
	return _slots()[0]


static func loaded_from() -> int:
	return _slots()[1]


static func load_save() -> void:
	var s := _slots()
	if s[3]:
		return
	s[3] = true

	var live := _read(KEY)
	var prev := _read(BACKUP)
	var had := live != "" or prev != ""

	if _parse(live):
		s[1] = Origin.FRESH if live == "" else Origin.LIVE
	elif _parse(prev):
		s[1] = Origin.RECOVERED
		_keep(live)
		push_warning("[Singularity] the save did not parse; recovered the previous one. "
				+ "The unreadable copy is under " + QUARANTINE + ".")
	else:
		s[1] = Origin.LOST if had else Origin.FRESH
		s[0] = SaveData.new()
		if had:
			_keep(live)
			push_error("[Singularity] neither the save nor its backup could be read. "
					+ "Starting fresh; the unreadable copy is under " + QUARANTINE + ".")

	# EFFECTS DEFAULT TO WHAT THE DEVICE ALREADY ASKED FOR. A player who has set
	# "reduce motion" at the OS level has already told every app they open how
	# much shake they want, and making them find a switch to say it again is not
	# a preference, it is a tax.
	#
	# ONLY MOTION. The legacy `fx` bit seeded both motion and light together
	# because it used to be one switch, and that conflation is exactly what the
	# access pass split apart: shake and a bent clock are vestibular, sparks and
	# a full-screen flash are photosensitive, and an animation scale of zero is a
	# statement about the first only.
	if not had:
		data().fx = 1
		if Platform.animations_off():
			data().motion = Grade.Motion.REDUCED

		# A SAVE WITH NO HISTORY HAS NOTHING TO MIGRATE, and saying so is what
		# stops the migration below from overwriting the line above: it reads the
		# legacy `fx` bit, finds the 1 that was just written, and sets motion back
		# to FULL. Migration is for saves written before the split, and this one
		# was written after it.
		data().v = 1

	_migrate()

	_rehydrate(data().best_k, data().best_v, _BEST)
	_rehydrate(data().par_k, data().par_v, _PARS)
	_rehydrate(data().tbest_k, data().tbest_v, _TBEST)
	_rehydrate(data().vbest_k, data().vbest_v, _VBEST)
	_rehydrate(data().dhist_day, data().dhist_folds, _DHIST)


# THE COACH, REACHED AT RUNTIME. This file needs exactly one pure function from
# it — "a save that has been past the first cube is taught" — and the coach needs
# the whole save, on every mark it draws. Naming each other as global classes is
# a ring that takes the whole game down with it: Store, Access, Palette, Session
# and half the interface all fail to load together, with an error that names none
# of them. The one call is deferred; the many are not.
const _COACH := []


static func _coach():
	if _COACH.empty():
		_COACH.append(load("res://ui/Coach.gd"))
	return _COACH[0]


static func _cfg() -> ConfigFile:
	var c := ConfigFile.new()
	c.load(FILE)                                  # a missing file is a first run
	return c


static func _read(key: String) -> String:
	var c := _cfg()
	var v = c.get_value(SECTION, key, "")
	return str(v) if v != null else ""


# Try one stored string. Empty means "there was nothing here", which is a clean
# first run and not a failure — so it succeeds, with defaults.
#
# A PARSE ERROR IS THE EASY CASE. The one that actually loses progress is JSON
# that parses and is the wrong SHAPE, so from_dict refuses anything whose paired
# lists are not lists, and whole() refuses anything whose pairs disagree in
# length.
static func _parse(raw: String) -> bool:
	var s := _slots()
	if raw == "":
		s[0] = SaveData.new()
		return true
	var j := JSON.parse(raw)
	if j.error != OK:
		return false
	var d = _read_save(j.result)
	if d == null or not d.whole():
		return false
	s[0] = d
	return true


# A SAVE, OUT OF WHAT WAS ON DISK, or null for anything that is not one — which
# is what makes a corrupt save recoverable rather than silently replaced.
#
# It is here rather than on SaveData because GDScript will not let a class name
# itself, and a factory returning a SaveData has to.
static func _read_save(d):
	if not (d is Dictionary):
		return null
	var s := SaveData.new()
	for f in SaveData._INTS:
		var k: String = SaveData._key_of(f)
		if d.has(k):
			s.set(f, int(d[k]))
	for f in ["saw_plate", "saw_peek", "peek_hinted", "taught_forge", "said_chapter"]:
		var k2: String = SaveData._key_of(f)
		if d.has(k2):
			s.set(f, int(d[k2]))
	for f in ["best_k", "best_v", "par_k", "par_v", "tbest_k", "tbest_v",
			"vbest_k", "vbest_v", "dhist_day", "dhist_folds", "hidden"]:
		var k3: String = SaveData._key_of(f)
		if d.has(k3):
			# AN EXPLICIT null IS NOT AN ABSENT FIELD. A missing key keeps the
			# empty list the constructor made; a key whose value is null would put
			# a null where a list has to be, and a null list reaching the
			# rehydrate is a crash at startup, before anything has drawn. The game
			# would not fail to load a save; it would fail to launch.
			if not (d[k3] is Array):
				return null
			s.set(f, (d[k3] as Array).duplicate())
	if d.has("draft"):
		s.draft = str(d["draft"])
	if d.has("made"):
		if not (d["made"] is Array):
			return null
		for m in d["made"]:
			if m is Dictionary:
				s.made.append(_read_made(m))
	return s


static func _read_made(d: Dictionary) -> MadeCube:
	var m := MadeCube.new()
	m.key = str(d.get("key", ""))
	m.name = str(d.get("name", ""))
	m.id = str(d.get("id", ""))
	m.code = str(d.get("code", ""))
	m.n = int(d.get("n", 0))
	m.par = int(d.get("par", 0))
	m.best = int(d.get("best", -1))
	m.tbest = int(d.get("tbest", -1))
	return m


# Put an unreadable save somewhere it cannot be written over. Once only: a
# second bad launch must not overwrite the quarantine with the empty string the
# first one left behind.
static func _keep(raw: String) -> void:
	if raw == "":
		return
	var c := _cfg()
	if c.has_section_key(SECTION, QUARANTINE):
		return
	c.set_value(SECTION, QUARANTINE, raw)
	c.save(FILE)


# Carry an old save's intent into the settings that did not exist when it was
# written. Runs once per save, and the version is what makes "once" true — a
# migration that runs every load is a setting the player cannot change.
static func _migrate() -> void:
	var d := data()

	# EVERY LAUNCH, NOT ONCE — this one is not a version step, it is a reading of
	# the save that is true whenever it is asked. A player who has cleared a cube
	# has been past the two the coach speaks on, and must never be handed a
	# tutorial on their four-hundredth. See Coach.
	d.taught = _coach().migrate(d.taught, d.reached)

	# A SAVE THAT FINISHED UNDER THE OLD RULE HAS FINISHED. It stored `reached`
	# one past the last cube, which is a state the ladder no longer has:
	# finishing now records a run and hands the ladder back at one. Read the old
	# fact, write the new one, and the player lands where a player finishing
	# today would.
	if d.reached > Vaults.LAST_CUBE:
		if d.runs < 1:
			d.runs = 1
		d.reached = 1
		save()

	if d.v >= 1:
		return
	# EFFECTS OFF MEANT BOTH LESS MOVEMENT AND LESS LIGHT, and it still means
	# both — it splits into two settings that start where it left them, rather
	# than into two settings that start switched on.
	var was_on := d.fx != 0
	d.motion = Grade.Motion.FULL if was_on else Grade.Motion.REDUCED
	d.light = Grade.Light.FULL if was_on else Grade.Light.REDUCED
	d.v = 1
	save()


static func _rehydrate(k: Array, v: Array, into: Dictionary) -> void:
	into.clear()
	for i in range(min(k.size(), v.size())):
		into[int(k[i])] = v[i]


static func _dehydrate(from: Dictionary, k: Array, v: Array) -> void:
	k.clear()
	v.clear()
	for key in from:
		k.append(key)
		v.append(from[key])


# IS THIS CUBE OPEN — playable as a vault run, with its best recorded?
#
# One place, because there were two: the seed box asked one way and the rack
# asked another, and a soft lock that only half the screens know about is a lock
# a player walks around by accident. A cube past the frontier is still PLAYABLE
# — it always was — it is played as practice, which records nothing.
static func open(level: int) -> bool:
	# The frontier of the run you are on, which on a first run is every cube you
	# have cleared plus the one in front of you.
	if level <= data().reached:
		return true

	# AND WHAT YOU EARNED, GIVEN BACK. The switch does not hand out cubes nobody
	# has solved — it restores access to the ones this save already has a best
	# for, which is why it is useless on a first run and why it is not a cheat on
	# any run. Finishing the machine relocks the ladder to cube one; this is the
	# sentence that makes that a joke rather than a hundred and forty-nine cubes
	# of homework.
	return data().unlocked != 0 and _BEST.has(level)


# Has this cube ever been cleared, on any run?
static func ever_cleared(level: int) -> bool:
	return _BEST.has(level)


static func save() -> void:
	_slots()[2] = OS.get_ticks_msec() + 220


# Called once a frame; performs the debounced write.
static func tick() -> void:
	var s := _slots()
	if s[2] < 0 or OS.get_ticks_msec() < s[2]:
		return
	s[2] = -1
	flush()


static func flush() -> void:
	var d := data()
	_dehydrate(_BEST, d.best_k, d.best_v)
	_dehydrate(_PARS, d.par_k, d.par_v)
	_dehydrate(_TBEST, d.tbest_k, d.tbest_v)
	_dehydrate(_VBEST, d.vbest_k, d.vbest_v)
	_dehydrate(_DHIST, d.dhist_day, d.dhist_folds)

	var c := _cfg()

	# ONE STEP BEHIND, AND ONLY EVER FROM A STRING THAT PARSED.
	#
	# The backup is the PREVIOUS live value, not a second copy of the one being
	# written — two copies of the same corruption are one copy of it. It is taken
	# from the key rather than re-serialised so that whatever the last successful
	# write actually put on disk is what gets kept, including the bytes a partial
	# write left.
	var had = c.get_value(SECTION, KEY, "")
	if str(had) != "" and loaded_from() != Origin.LOST:
		c.set_value(SECTION, BACKUP, had)

	c.set_value(SECTION, KEY, JSON.print(d.to_dict()))
	c.save(FILE)                                  # a device that cannot remember still gets to play


# ---- typed accessors ------------------------------------------------------

static func try_best(level: int):
	return _BEST.get(level)


static func set_best(level: int, folds: int) -> void:
	_BEST[level] = folds
	save()


# The par this level was proved to have, remembered from the run that cleared it.
static func try_par(level: int):
	return _PARS.get(level)


static func set_par(level: int, par: int) -> void:
	_PARS[level] = par
	save()


static func try_time_best(level: int):
	return _TBEST.get(level)


static func set_time_best(level: int, ms: int) -> void:
	_TBEST[level] = ms
	save()


static func try_vault_best(band: int):
	return _VBEST.get(band)


static func set_vault_best(band: int, total: int) -> void:
	_VBEST[band] = total
	save()


static func daily_history() -> Dictionary:
	return _DHIST


static func set_daily(day: int, folds: int) -> void:
	_DHIST[day] = folds
	save()


static func made(key: String):
	for m in data().made:
		if m.key == key:
			return m
	return null


static func prune_daily(today: int, keep_days: int) -> void:
	var drop := []
	for d in _DHIST:
		if today - d > keep_days:
			drop.append(d)
	for d in drop:
		_DHIST.erase(d)


# FOR THE HARNESS ONLY: forget everything, including that a load ever happened.
# Nothing in the game calls this — a save that can be dropped from inside the
# game is a save that will be.
static func _reset_for_tests() -> void:
	_S.clear()
	_BEST.clear()
	_PARS.clear()
	_TBEST.clear()
	_VBEST.clear()
	_DHIST.clear()
