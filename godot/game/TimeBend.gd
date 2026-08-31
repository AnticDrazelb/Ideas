class_name TimeBend
# THE CLOCK BENDS, AND THAT IS MOST OF WHAT AN IMPACT IS.
#
# A hit that only shakes the camera reads as a camera effect. A hit that stops
# time for fifty milliseconds first reads as something ARRIVING, because the
# frame you were looking at is held long enough for you to notice it did not
# continue.
#
# Two knobs, used at three scales:
#
#   a landed fold    52ms of stop. Enough to feel, too short to see.
#   a plate firing   120ms of stop, then a third of speed easing back over most
#                    of a second — the world turning inside out is worth a held
#                    breath.
#   the collapse     150ms and 470, so the board comes apart at the pace of
#                    something ending rather than something being dismissed.
#
# The solve clock is NOT affected by any of this — it runs on its own monotonic
# stopwatch, so a player cannot buy themselves thinking time by triggering
# impacts.

# freeze, scale, from, slow_t, slow_dur
const _S := []


static func _s() -> Array:
	if _S.empty():
		_S.resize(5)
		_S[0] = 0.0
		_S[1] = 1.0
		_S[2] = 1.0
		_S[3] = 0.0
		_S[4] = 0.0
	return _S


# A BENT CLOCK IS MOTION, even though nothing on screen has moved.
#
# It belongs with the camera rather than with the sparks: what a stop and a
# slowmo do is change the rate the whole picture arrives at, and that is the
# same complaint as a shaking frame for the same player. At STILL both knobs go
# to zero and the game runs at one speed, all the time — which is a legitimate
# way to play it and not a lesser one.
static func _amount() -> float:
	return Access.motion_amount()


# Hold the frame. Milliseconds.
static func hitstop(ms: float) -> void:
	var s := _s()
	s[0] = max(s[0], ms * _amount())


# Drop to `scale` and ease back over `ms`.
static func slowmo(scale: float, ms: float) -> void:
	var s := _s()
	s[2] = lerp(1.0, scale, _amount())
	s[3] = 0.0
	s[4] = ms
	s[1] = s[2]


static func reset() -> void:
	var s := _s()
	s[0] = 0.0
	s[1] = 1.0
	s[2] = 1.0
	s[3] = 0.0
	s[4] = 0.0


# Turn a real frame delta into a game one. Returns milliseconds, because every
# duration in the ported rules is in milliseconds.
static func step(real_delta_seconds: float) -> float:
	var s := _s()
	var raw := real_delta_seconds * 1000.0

	# A stop eats the frame rather than scaling it. Scaling to zero would make
	# the easing curves divide by nothing; eating it keeps every animation
	# exactly where it was, which is what a stop is.
	if s[0] > 0.0:
		s[0] -= raw
		return 0.0

	if s[4] > 0.0:
		s[3] += raw
		var p: float = clamp(s[3] / s[4], 0.0, 1.0)
		s[1] = lerp(s[2], 1.0, p * p)              # ease back in, not out
		if p >= 1.0:
			s[4] = 0.0
			s[1] = 1.0

	return raw * s[1]


static func scale() -> float:
	return _s()[1]
