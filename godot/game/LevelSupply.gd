class_name LevelSupply
# THE SUPPLY, AND THE CUTTER — minting on a thread that is not the one drawing.
#
# This was the worst defect in the web original and it was invisible from inside
# it: cutting a cube costs real time, and the prebuild was doing it on the main
# thread about two seconds into every level. The fix there was a Blob worker
# running the file's own source, which needed an elaborate dance to guarantee
# there was never a second copy of the generator to drift out of step. Here it
# is a background thread calling the same static functions, because core/ touches
# no engine API and has no state that is not passed in.
#
# THE SYNCHRONOUS PATH STILL EXISTS AND IS STILL CORRECT. It runs when a level is
# reached faster than it could be cut. It blocks, exactly as it always did; it is
# simply no longer the path taken on every level.

const CACHE_LIMIT := 24

const _CACHE := {}
const _ORDER := []
const _IN_FLIGHT := {}
const _DONE := []
const _LIVE := []          # the Cutter objects still running, so they are not collected
const _GATE := []


static func _gate() -> Mutex:
	if _GATE.empty():
		_GATE.append(Mutex.new())
	return _GATE[0]


static func get_level(level: int) -> Level:
	var baked = Baked.try_at(level)
	if baked != null:
		return baked.to_level(level)

	# THE CATALOGUE FIRST, and for a hundred and fifty cubes it is the only
	# answer. The ladder is cut offline out of a hundred thousand candidates and
	# shipped as text — see core/Catalogue — so a cube is read rather than
	# minted, which is what lets it have been CHOSEN.
	#
	# The generator stays underneath for two reasons and both matter: the daily
	# still borrows it, and a build whose catalogue asset failed to import should
	# be a game with a worse ladder rather than no game.
	var from_cat = Catalogue.get_level(level)
	if from_cat != null:
		_put(level, from_cat)
		return from_cat

	_gate().lock()
	var hit = _CACHE.get(level)
	_gate().unlock()
	if hit != null:
		return hit

	var lv := Generator.mint(level)
	_put(level, lv)
	return lv


static func _put(level: int, lv: Level) -> void:
	_gate().lock()
	if not _CACHE.has(level):
		_ORDER.append(level)
	_CACHE[level] = lv
	while _ORDER.size() > CACHE_LIMIT:
		_CACHE.erase(_ORDER[0])
		_ORDER.remove(0)
	_gate().unlock()


static func has(level: int) -> bool:
	if Baked.try_at(level) != null:
		return true
	if Catalogue.has(level):
		return true
	_gate().lock()
	var got := _CACHE.has(level)
	_gate().unlock()
	return got


# Cut the next cube while this one is being played.
static func prebuild(level: int) -> void:
	if has(level):
		return

	_gate().lock()
	if _IN_FLIGHT.has(level):
		_gate().unlock()
		return
	_IN_FLIGHT[level] = true
	_gate().unlock()

	var c := Cutter.new(level, _gate(), _DONE, _IN_FLIGHT)
	c.thread = Thread.new()
	_LIVE.append(c)
	if c.thread.start(c, "run", 0, Thread.PRIORITY_LOW) != OK:
		# A DEVICE THAT WILL NOT GIVE US A THREAD STILL GETS TO PLAY. The
		# synchronous path is the same generator; it is only slower, and slower
		# is what the prebuild exists to hide rather than what it exists to make
		# possible.
		_gate().lock()
		_IN_FLIGHT.erase(level)
		_gate().unlock()
		_LIVE.erase(c)


# Main-thread pump. A thread has to be joined before it is dropped or Godot
# complains at exit, and the join is free here because the work is already done.
static func tick() -> void:
	while true:
		_gate().lock()
		var job = null
		if not _DONE.empty():
			job = _DONE[0]
			_DONE.remove(0)
		_gate().unlock()
		if job == null:
			break
		_put(job[0], job[1])

	for i in range(_LIVE.size() - 1, -1, -1):
		var c: Cutter = _LIVE[i]
		if c.thread != null and not c.thread.is_alive():
			c.thread.wait_to_finish()
			_LIVE.remove(i)


# Every cutter still running, joined. Called on the way out so a quit during a
# cut is a wait rather than a warning.
static func shutdown() -> void:
	for c in _LIVE:
		if c.thread != null and c.thread.is_active():
			c.thread.wait_to_finish()
	_LIVE.clear()
