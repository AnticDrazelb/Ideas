class_name RemainSolver
extends Reference
# FEWEST FROM HERE — par, live, for the position you are actually in.
#
# The fold counter says what a run has cost. Par says what the cube costs.
# Neither answers the question a player is actually holding: is the line I am on
# still a good one? Two folds over par is a fact you learn on the win card, ten
# moves after the mistake, when it is too late to be information and can only be
# a verdict.
#
# So the solver runs from the CURRENT state and the number goes on the HUD. Read
# it as a countdown, and read the colour as the only judgement it makes:
#
#   arc    folds spent + folds to come is still exactly par. Perfect line.
#   lock   it is not. You lost folds you cannot get back, and you knew the
#          instant it happened rather than at the end.
#   fault  there is no way on from here at all. Undo.
#
# IT IS NEVER ALLOWED TO COST A FRAME. The web version put this in
# requestIdleCallback; here it goes on a worker thread, which is better — core/
# touches no engine API, so there is nothing to marshal. States are cached, which
# makes undo instant and walking back and forth free.

var value := -2          # -2 = not answered yet, -1 = no route
var over := false        # the budget ran out, not the routes
var stale := false       # an answer is up but a newer one is coming
var stuck := false

var _cache := {}
var _gate := Mutex.new()
var _want := 0
var _have := -0x7FFFFFFFFFFFFFF
var _thread: Thread = null
var _pending_stuck_toast := false

# The snapshot the worker is solving, held here so the thread can be pointed at
# a method on this object rather than at a closure it cannot have.
var _job_level: Level
var _job_state: SolveState
var _job_key := 0


func reset() -> void:
	_join()
	_gate.lock()
	_cache.clear()
	_gate.unlock()
	value = -2
	over = false
	stale = false
	stuck = false
	_want = 0
	_have = -0x7FFFFFFFFFFFFFF


# THE SAME KEY THE SEARCH USES, because it is caching the search's answer. This
# was a second copy of the packing and it aliased exactly as the original did —
# five world bits folded onto `doors` — so the readout could hand back another
# world's remaining count the moment a cube had both a trigger and a lock. Two
# copies of one expression is how that happened; there is one now, and it lives
# with the search.
static func state_key(s) -> int:
	return Solver.state_key(s.n, SolveState.new(
			s.pos, Turns.ori_index(s.m), s.kmask, s.doors, s.world))


func schedule(s) -> void:
	if s.lv == null or s.won:
		return
	var k := state_key(s)
	_want = k

	_gate.lock()
	var hit = _cache.get(k)
	_gate.unlock()
	if hit != null:
		# seen this exact state before — undo is instant
		_apply(k, hit[0], hit[1])
		return

	# THE LAST ANSWER STAYS UP, DIMMED, while the next one is found. Blanking it
	# to a dot after every fold made the readout flicker at exactly the moment
	# the player was looking at it, and a number that is two hundred milliseconds
	# old and visibly greyed is more use than no number that is perfectly
	# current. The dot is only for the very first answer of a cube, when there is
	# nothing honest to leave up.
	stale = true

	if _thread != null and _thread.is_alive():
		return                                     # it will pick up _want when it finishes
	_join()

	_job_level = s.lv
	_job_state = SolveState.new(s.pos, Turns.ori_index(s.m), s.kmask, s.doors, s.world)
	_job_key = k
	_thread = Thread.new()
	if _thread.start(self, "_work", 0, Thread.PRIORITY_LOW) != OK:
		# NO THREAD, NO EXCUSE. The readout is the one number a player is
		# steering by, so a device that will not give us a worker gets it
		# computed inline rather than not at all.
		_thread = null
		_work(0)


func _work(_arg) -> void:
	var r := Solver.solve(_job_level, 40, _job_state)
	_gate.lock()
	if _cache.size() > 600:
		_cache.clear()                             # a long session, bounded
	_cache[_job_key] = [r.turns if r.ok else -1, (not r.ok) and r.over]
	_gate.unlock()


func _join() -> void:
	if _thread != null and _thread.is_active():
		_thread.wait_to_finish()
	_thread = null


# Main-thread pump. Picks up whatever the worker has finished.
func tick(s) -> void:
	if s == null or s.lv == null or s.won:
		return
	var k := _want
	if k == _have and not stale:
		return

	_gate.lock()
	var e = _cache.get(k)
	_gate.unlock()
	if e == null:
		# the worker finished a stale state; re-arm for the current one
		if _thread == null or not _thread.is_alive():
			_join()
			schedule(s)
		return

	if _thread != null and not _thread.is_alive():
		_join()

	_apply(k, e[0], e[1])
	if _pending_stuck_toast:
		_pending_stuck_toast = false
		s.emit_signal("toast", "NO ROUTE — UNDO")
		Haptics.buzz(Haptics.FAULT)
		s.emit_signal("stuck_changed")


func _apply(k: int, v: int, o: bool) -> void:
	var was_stuck := stuck
	_have = k
	value = v
	over = o
	stale = false
	stuck = v < 0 and not o
	if stuck and not was_stuck:
		_pending_stuck_toast = true


# What the pill should read, and how it should be judged.
# Returns [text, slip, dead, thinking].
func pill(s) -> Array:
	if value == -2:
		return ["·", false, false, true]
	if over:
		return ["40+", false, false, stale]
	if value < 0:
		return ["✕", false, true, stale]
	# A STALE NUMBER MUST NOT BE JUDGED. Folds have already gone up and the count
	# has not come down yet, so "spent + to come" reads one over for a few frames
	# — and a colour that means "you just lost the perfect line" is the one thing
	# that must never fire by accident.
	var slip: bool = (not stale) and (s.turns + value) > s.lv.par
	return [Num.of(value), slip, false, stale]
