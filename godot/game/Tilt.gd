class_name Tilt
# WHICH WAY THE PHONE IS BEING HELD, AND IT IS A RELATIVE QUESTION.
#
# The sky behind the machine parallaxes when the device moves, which needs one
# small vector: how far the player has leaned the phone, left/right and
# top/bottom, in a range the sky can multiply by an amplitude.
#
# GRAVITY, NOT ROTATION RATE. A gyroscope answers "how fast are you turning",
# which has to be integrated to become "how are you held" — and an integrated
# rate drifts, so the sky would slide away over a long session and never come
# back. Gravity is an absolute reading of the same question and it cannot drift.
# The fused channel is preferred where there is one, because it is far quieter
# than the raw accelerometer; where there is not, the accelerometer answers the
# same question with more noise, and the filter below is sized for that case
# rather than the good one.
#
# THE CENTRE IS WHEREVER YOU ARE HOLDING IT. This is the part that decides
# whether the effect is pleasant or infuriating. Absolute tilt pinned to true
# vertical means somebody lying down, or reading at the angle a person actually
# holds a phone, gets the sky jammed against one corner and no travel in that
# direction at all. So the rest pose FOLLOWS, slowly: quick movement is
# parallax, and anything held for a few seconds becomes the new centre. The
# horizon is where you are looking.
#
# AND A DEVICE THAT WILL NOT ANSWER IS A SHRUG, the same rule Platform, Store
# and Haptics follow. No sensor, an editor, a desktop: `look` stays at zero, the
# sky stands still, and nothing anywhere has to ask whether this device has a
# gyroscope.

# Below this a reading is the sensor declining rather than a pose. Real gravity
# is always about one; a fifth of that is not a lean, it is a channel that is not
# there.
const DEAD := 0.04

# Frames of silence before the fused channel is given up on.
const GIVE_UP := 20

# How far a lean has to go to read as full deflection. A quarter of a right
# angle: enough that an ordinary shift in a chair moves the sky visibly, small
# enough that the range is not off the end of what somebody will actually do with
# a phone in their hands.
const FULL_LEAN := 0.38

# SECONDS FOR THE CENTRE TO CATCH UP, and it is the one number that decides how
# this feels. Too fast and the sky springs back while the phone is still turning,
# which reads as the effect fighting the hand. Too slow and a change of posture
# leaves it stuck at the edge.
const RECENTRE := 3.5

# How hard the reading itself is filtered, per second.
const SMOOTH := 7.0

# look, live, gyro, rest, raw, seeded, silent
const _S := []


static func _s() -> Array:
	if _S.empty():
		_S.append(Vector2.ZERO)      # 0 look
		_S.append(false)             # 1 live
		_S.append(true)              # 2 gyro: prefer the fused channel until it declines
		_S.append(Vector2.ZERO)      # 3 rest
		_S.append(Vector2.ZERO)      # 4 raw
		_S.append(false)             # 5 seeded
		_S.append(0)                 # 6 silent
	return _S


# The lean, roughly -1..1 per axis, centred on however the phone is being held. X
# is a roll — the left edge dropping is negative. Y is a pitch — the top edge
# tipping away from the face is positive.
static func look() -> Vector2:
	return _s()[0]


# Whether anything on this device is answering at all.
static func live() -> bool:
	return _s()[1]


static func _read(use_gyro: bool) -> Vector3:
	# Godot's gravity vector points DOWN in device space, so a phone held upright
	# reads about (0, -1, 0) either way — the same convention Unity's does, which
	# is why the axis arithmetic below is unchanged from the C#.
	return Input.get_gravity() if use_gyro else Input.get_accelerometer()


# Read the device once for this frame. Called from the director's own process so
# there is exactly one reading per frame however many things come to ask for it.
static func sample(dt: float) -> void:
	var s := _s()
	var g: Vector3 = _read(s[2])

	# ---- AND A CHANNEL THAT WILL NOT ANSWER IS NOT A DEVICE THAT WON'T -------
	#
	# The fused gravity channel is synthesised from the gyro and the accelerometer
	# together, and on a good many handsets that have a gyroscope it answers a
	# flat zero — the hardware is there, the fusion is not. The C# latched its
	# choice once at startup and never looked again, so those phones took the
	# dead branch on every frame for the whole session: the sky nailed to the
	# middle of the screen, with a perfectly good accelerometer next to it that
	# was never asked.
	#
	# A ZERO IS UNAMBIGUOUS, which is what makes the demotion safe. Gravity has a
	# magnitude of one whichever way a phone is held — face up, edge on, upside
	# down — so no pose reads zero. A zero is the channel declining to answer, and
	# the only question left is whether the other one will.
	#
	# It waits a few frames because a sensor is not instant: the first reads on a
	# device whose fusion is perfectly good come back zero, and demoting on frame
	# one would give every phone the noisier sensor forever.
	if s[2] and g.length_squared() < DEAD:
		s[6] += 1
		if s[6] >= GIVE_UP and Input.get_accelerometer().length_squared() >= DEAD:
			s[2] = false
			s[5] = false
			g = _read(false)
	else:
		s[6] = 0

	# A dead sensor answers exactly zero, and so does a desktop with no device
	# attached. One length check tells both from a real reading.
	if g.length_squared() < DEAD:
		s[1] = false
		s[0] = (s[0] as Vector2).move_toward(Vector2.ZERO, dt * 2.0)
		return
	s[1] = true

	# ROLL AND PITCH, NOT X AND Y. The phone's own x is the roll axis directly;
	# the pitch has to come off z, because y is the axis gravity is mostly ON and
	# it barely moves for the lean this reads.
	var now := Vector2(g.x, -g.z) / FULL_LEAN

	if not s[5]:
		s[4] = now
		s[3] = now
		s[5] = true

	s[4] = (s[4] as Vector2).linear_interpolate(now, 1.0 - exp(-SMOOTH * dt))
	s[3] = (s[3] as Vector2).linear_interpolate(s[4], 1.0 - exp(-dt / RECENTRE))

	var l: Vector2 = s[4] - s[3]
	s[0] = Vector2(clamp(l.x, -1.0, 1.0), clamp(l.y, -1.0, 1.0))


# Forget the pose. Called when the game comes back from the background, where the
# phone has very likely been put down, picked up or pocketed and the old centre
# is a memory of a different posture.
static func recentred() -> void:
	var s := _s()
	s[5] = false
	s[6] = 0
	s[0] = Vector2.ZERO
