class_name SolveClock
# THE CLOCK — deciding what "time taken" is allowed to mean.
#
# IT RUNS FROM THE CUBE APPEARING, NOT FROM THE FIRST TOUCH. Starting on first
# input would be the easy read and the wrong one: this is a thinking puzzle, so
# a player who studies a cube for two minutes and then executes six perfect
# folds would post a better time than one who solved it in forty seconds.
# Thinking IS the solve. The clock counts it.
#
# IT IS MONOTONIC. OS.get_ticks_msec cannot be moved by an NTP correction, a
# timezone change, or a player setting the system clock back, all of which the
# wall clock will happily believe.
#
# BACKGROUNDING STOPS IT; A MENU DOES NOT. A phone call in the middle of a cube
# is not part of the solve. A pause screen is a judgement call and the safe one
# is to keep counting — a clock you can stop by opening a menu is a clock you
# can plan against.
#
# RETRIES KEEP THE BEST, not the last and not the total: a vault total is the
# sum of each cube's fastest clear, so a bad first run at cube nine can always be
# improved without wiping the save.

# acc, mark, on, live
const _S := []


static func _s() -> Array:
	if _S.empty():
		_S.resize(4)
		_S[0] = 0.0
		_S[1] = 0.0
		_S[2] = false
		_S[3] = false
	return _S


static func _now() -> float:
	return float(OS.get_ticks_msec())


static func reset() -> void:
	var s := _s()
	s[0] = 0.0
	s[1] = _now()
	s[2] = true
	s[3] = true


static func hold() -> void:
	var s := _s()
	if s[2]:
		s[0] += _now() - s[1]
		s[2] = false


static func go() -> void:
	var s := _s()
	if s[3] and not s[2]:
		s[1] = _now()
		s[2] = true


static func stop() -> void:
	hold()
	_s()[3] = false


static func ms() -> int:
	var s := _s()
	return int(round(s[0] + (_now() - s[1] if s[2] else 0.0)))
