class_name PerfWatch
# THE FRAME BUDGET, ENFORCED BY MEASUREMENT RATHER THAN BY GUESSING THE PHONE.
#
# A quality preset asks the device what it is and believes the answer. This asks
# what it is actually managing, which is the only question that matters: a
# four-year-old flagship and a new budget phone can report the same things and
# hold very different frame rates, and neither of them is the one running hot in
# somebody's pocket.
#
# It watches the median of forty-five frames rather than the mean, because the
# mean is decided by the worst two — a garbage collection and a shader compile —
# and neither of those is the steady state that resolution should be chosen for.
#
# THE STEP IS COMPUTED, NOT GROPED TOWARD. A fixed step has to be small enough
# not to overshoot a mildly slow device, which means a badly slow one takes four
# or five corrections and twenty seconds to settle, with the picture visibly
# changing under the player the whole way. Fill cost goes with AREA, so the scale
# needed to hit the target is the square root of the ratio of the times, and that
# lands in one step instead of five.
#
# Damped at 0.75 because the frame is not purely fill: there is a fixed CPU cost
# in there — the solver, the mesh rebuild — that no resolution change will touch,
# and taking the square root at face value would chase it straight to the floor.
#
# IT ONLY EVER GOES DOWN. Climbing back up when a heavy moment passes would make
# the picture breathe, and a board whose resolution changes while you are reading
# it is worse than a board that is slightly soft.

const FLOOR := 0.62
const WINDOW := 45
const TARGET_MS := 16.7      # 60fps
const SLACK_MS := 21.0       # about 48fps: good enough, leave it alone

# samples, n, hold, scale
const _S := []


static func _s() -> Array:
	if _S.empty():
		_S.append([])
		_S.append(0)
		_S.append(90)            # skip startup, and settle after a change
		_S.append(1.0)
		_S[0].resize(WINDOW)
	return _S


static func scale() -> float:
	return _s()[3]


# Called after anything that re-fits the view, so the next window is measured on
# the new picture.
static func settle() -> void:
	_s()[2] = 30


static func reset() -> void:
	var s := _s()
	s[1] = 0
	s[2] = 90
	s[3] = 1.0


static func tick(unscaled_delta_seconds: float) -> void:
	var s := _s()
	if s[3] <= FLOOR:
		return
	if s[2] > 0:
		s[2] -= 1
		return

	s[0][s[1]] = unscaled_delta_seconds * 1000.0
	s[1] += 1
	if s[1] < WINDOW:
		return
	s[1] = 0

	var sorted: Array = s[0].duplicate()
	sorted.sort()
	var median: float = sorted[WINDOW >> 1]
	if median <= SLACK_MS:
		return

	var want := sqrt(TARGET_MS / median)
	s[3] = max(FLOOR, s[3] * clamp(want, 0.75, 0.97))
	s[2] = 30
	_apply(s[3])


static func _apply(k: float) -> void:
	# THE BACKBUFFER, NOT THE CAMERA: this is the same thing the web build does
	# to its canvas backing store, and it costs nothing per frame — unlike a
	# render target, which would add a blit to every frame in order to save time
	# on some of them.
	#
	# Godot 3.5 spells it as a viewport size against a stretch mode that is
	# already scaling to the window, so shrinking the render size is exactly the
	# backing-store change and the window does not move.
	var tree := Engine.get_main_loop()
	if tree == null or not (tree is SceneTree):
		return
	var root: Viewport = (tree as SceneTree).root
	if root == null:
		return
	var w := int(max(320, round(OS.window_size.x * k)))
	var h := int(max(240, round(OS.window_size.y * k)))
	root.set_size_override(true, Vector2(w, h))
	root.set_size_override_stretch(true)
	print("[Singularity] frame budget: rendering at %0.2f (%dx%d)" % [k, w, h])
