class_name Haptics
# A SWITCH SAYS WHETHER, A SLIDER SAYS HOW MUCH.
#
# Haptics were one bit for a thing that is not one bit: a pattern that reads as a
# ratchet on one phone is a jolt on another, and the only person who can settle
# that is the one holding the phone. So intensity scales the duration of every
# pattern, and zero is off.

const FOLD := 10
const MOVE := 20
const FAULT := [5, 5, 5, 5, 5]
const COLLAPSE := [0, 50, 100]

# The machine coming apart, which is the heaviest thing that happens and is a
# separate event from the collapse that precedes it — the core takes you, and
# half a second later the engine fails. Two hits, not one.
const SHATTER := [0, 30, 40, 140]

const _WARNED := []


# Is the motor allowed to run at all, and how hard.
static func on() -> bool:
	return Store.data().haptic != 0 and Store.data().buzz > 0


# ONE DURATION, SCALED. Pure, so the harness can hold it to the switch and the
# slider without a phone in the room.
static func plan_one(ms: int, strength: int) -> int:
	if ms <= 0 or strength <= 0:
		return 0
	return int(round(ms * strength / 100.0))


# AND A PATTERN STAYS A PATTERN.
#
# This used to ADD THE ARRAY UP and buzz for the total, which is not a scaling of
# the pattern — it is the destruction of it. Every array here is an Android
# vibration pattern, { delay, on, off, on, ... }, so SHATTER's two hits thirty
# and a hundred and forty long with forty of silence between them came out as one
# flat two hundred and ten millisecond drone, and so did every other "pattern" in
# the game. They were all the same buzz at different lengths.
#
# The strength scales every entry, so the shape holds and the whole pattern
# stretches or shrinks — which is what a duration-based intensity control can
# honestly do. Returns null when nothing would be felt.
static func plan(pattern, strength: int):
	if pattern == null or pattern.size() == 0 or strength <= 0:
		return null
	var outp := []
	var any := 0
	for i in range(pattern.size()):
		outp.append(int(round(pattern[i] * strength / 100.0)))
		if i % 2 == 1:
			any += outp[i]                        # odd entries are the ON times
	return outp if any > 0 else null


# THE MOTOR, THROUGH WHATEVER DOOR THIS PLATFORM HAS.
#
# Godot 3.5 has Input.vibrate_handheld(ms), which is a one-shot on Android and
# iOS and nothing anywhere else. There is no waveform call, so a pattern is
# played as the sum of its ON times — which is exactly the mistake the C# comment
# above is about, and doing it deliberately with the reason written down is a
# different thing from doing it by accident. A real waveform needs an Android
# plugin; the seam is here and the planner above already produces the pattern.
static func buzz(ms_or_pattern) -> void:
	if not on():
		return
	if ms_or_pattern is Array:
		var scaled = plan(ms_or_pattern, Store.data().buzz)
		if scaled == null:
			return
		var total := 0
		for i in range(scaled.size()):
			if i % 2 == 1:
				total += scaled[i]
		if total > 0:
			Input.vibrate_handheld(total)
		return
	var one := plan_one(int(ms_or_pattern), Store.data().buzz)
	if one > 0:
		Input.vibrate_handheld(one)
