class_name Session
extends Reference
# THE CUBE IN PLAY: where you stand, how the engine is folded, what you carry,
# and which world the plates have put you in.
#
# Everything here is rules. It never touches a mesh, a material or an audio
# player — it raises a signal and lets the presentation layer decide what that
# looks like. That split is what lets the whole of this class be exercised by a
# headless test with no scene loaded, and it is the reason the port could be
# proved identical to the original before a single pixel existed.
#
# THE C# CARRIES A `SessionEvents` OBJECT OF DELEGATES AND THIS CARRIES SIGNALS.
# Same shape, same names, and the one difference is worth stating: a signal with
# no listener is a no-op, where a null delegate had to be guarded at every call
# site with `?.Invoke`. Twenty-eight of those guards are gone.

enum LoadKind { VAULT, DAILY, MADE, PRACTICE }

signal toast(msg)
signal stepped(cell, surf)
signal landed(cell)                 # the last step of a walk, which lands rather than steps
signal door_opened(cell, i)
signal key_taken(cell, i)
signal plate_fired(cell, bit)       # cell, bit
signal reverted()
signal thrown_to_plate(cell)
signal fold_landed(t)               # turn index
signal fold_refused(t)              # turn index
signal denied(u, v, col)            # u, v, colour
signal cube_won()
signal stuck_changed()
signal settled()
signal plate_tick(seconds)          # seconds remaining, 3..1
signal finished()

const PLATE_MS := 5000
const HINTS_PER_CUBE := 3

# ---- state ----------------------------------------------------------------
var lv: Level = null
var n := 0
var m: Ori = Ori.new()
var pos := Vector3.ZERO
var kmask := 0
var doors := 0
var turns := 0
var world := 0

var surf := []
var reach := []
var reach_dist := []

var level_no := 1
var kind = LoadKind.VAULT
var made_key := ""
var won := false
var stuck := false
var hints_left := HINTS_PER_CUBE
var plate_t := 0.0


# WHICH WAY THE ENGINE IS BEING LOOKED THROUGH.
#
# Bits 1 and 2 of `world` are the plates, and they change what a cell IS. Bit 4
# changes nothing about any cell — it selects the other array of faces, so every
# column shows its FAR side. One accessor rather than the mask spelled out at
# each site, because the three places that project are the three places that must
# never disagree about it.
func everted() -> bool:
	return (world & Level.EVERTED) != 0


func is_daily() -> bool:
	return kind == LoadKind.DAILY


func is_made() -> bool:
	return kind == LoadKind.MADE


func is_practice() -> bool:
	return kind == LoadKind.PRACTICE


# ---- motion ---------------------------------------------------------------
class Walk:
	var queue := []
	var from := Vector3.ZERO
	var t := 0.0


class Anim:
	var axis_x := false
	var ang := 0.0
	var from := 0.0
	var to := 0.0
	var t := 0.0
	var dur := 0.0
	var turn := -1        # -1 for a spring-back or a refusal
	var seat := false     # the detent curve
	var hard := false     # the refusal curve


class Drag:
	var has_axis := false
	var axis_x := false
	var ang := 0.0


var walking = null
var anim = null
var drag = null

var _undo := []

# The live readout, which is a search on a worker thread. See RemainSolver.
var remain := RemainSolver.new()

var _last_plate_tick := -1


# ---- loading --------------------------------------------------------------

func load_cube(source: Level, number: int, how: int, made_key_in: String = "") -> void:
	kind = how
	made_key = made_key_in
	level_no = Daily.SPEC_LEVEL if (how == LoadKind.DAILY or how == LoadKind.MADE) else int(max(1, number))

	lv = source.clone()
	lv.name = source.name
	lv.par = source.par
	n = lv.n

	m = Ori.new()
	pos = lv.start
	kmask = 0
	doors = 0
	turns = 0
	world = 0
	lv.clear_eff()
	plate_t = 0.0

	var k0 := lv.key_index_at(pos)
	if k0 >= 0:
		kmask |= 1 << k0

	_undo.clear()
	walking = null
	anim = null
	drag = null
	won = false
	stuck = false
	hints_left = HINTS_PER_CUBE
	remain.reset()

	settle()

	# The clock starts with the cube — including the intro wave, which every
	# player and every device pays identically.
	SolveClock.reset()

	# AND UNLOCKING EVERYTHING MUST NOT SPEND THE LADDER. With the switch on,
	# every cube loads as a vault run — so opening 140 would otherwise drag the
	# frontier to 140 and the soft lock would be gone for good the first time
	# somebody looked at a late cube. The switch decides what is PLAYABLE; only
	# playing forward decides what is reached.
	if how == LoadKind.VAULT and level_no > Store.data().reached and Store.data().unlocked == 0:
		Store.data().reached = level_no
		Store.save()


# ---- projection bookkeeping ------------------------------------------------

# Recompute everything the projection decides, after any settled change.
func settle() -> void:
	surf = Projection.project(n, lv.eff(world), m)
	compute_reach()
	emit_signal("settled")


func compute_reach() -> void:
	reach = []
	reach.resize(n * n)
	reach_dist = []
	reach_dist.resize(n * n)
	for i in range(n * n):
		reach[i] = false
		reach_dist[i] = 0
	var v := Projection.view_of(n, m, pos)
	var q := [int(v.x) * n + int(v.y)]
	reach[q[0]] = true
	var i := 0
	while i < q.size():
		var u: int = q[i] / n
		var w: int = q[i] % n
		for t in range(4):
			var u2: int = u + Turns.DX[t]
			var w2: int = w + Turns.DY[t]
			if u2 < 0 or w2 < 0 or u2 >= n or w2 >= n:
				continue
			var k := u2 * n + w2
			if reach[k]:
				continue
			if Projection.walkable(lv, surf, u2, w2, doors) == null:
				continue
			reach[k] = true
			reach_dist[k] = int(min(255, reach_dist[q[i]] + 1))
			q.append(k)
		i += 1

	# ONE CHOKE POINT. Everything that can change what you may reach — a landed
	# fold, a step, a plate firing, an undo, a cube loading — comes through here,
	# so the live par is asked for here and nowhere else.
	remain.schedule(self)


func keys_held() -> int:
	return Solver.popcount(kmask) - Solver.popcount(doors)


# ---- undo -----------------------------------------------------------------

func _push_undo() -> void:
	_undo.append([pos, Turns.ori_index(m), kmask, doors, turns, world])
	if _undo.size() > 200:
		_undo.remove(0)


func undo_depth() -> int:
	return _undo.size()


func undo() -> bool:
	if _undo.empty() or won:
		return false
	var s: Array = _undo.pop_back()
	pos = s[0]
	m = Turns.ori(s[1])
	kmask = s[2]
	doors = s[3]
	turns = s[4]
	world = s[5]

	# Undoing INTO a flipped world hands back a full five seconds rather than
	# whatever was left when the snapshot was taken. Storing the remainder would
	# be the more literal restore and the worse one: it would drop the player
	# back in with a fifth of a second on the clock, which is not a position
	# anybody can play out of, and undo would be a button that re-served the same
	# death. This is a retry, so it is a whole run at it.
	plate_t = PLATE_MS if (world & Level.PLATES) != 0 else 0.0

	walking = null
	anim = null
	drag = null
	won = false
	stuck = false
	settle()
	return true


# ---- pathing ---------------------------------------------------------------

# Pathing treats a shut lock as a wall, EXCEPT as the final square: you open a
# lock by walking into it deliberately, one at a time, never by having a route
# swallow two of them and your last node on the way past.
func path_to(tu: int, tv: int):
	var start := Projection.view_of(n, m, pos)
	var from := []
	from.resize(n * n)
	var seen := []
	seen.resize(n * n)
	for i in range(n * n):
		from[i] = -1
		seen[i] = false
	var q := [int(start.x) * n + int(start.y)]
	seen[q[0]] = true

	var i2 := 0
	while i2 < q.size():
		var u: int = q[i2] / n
		var v: int = q[i2] % n
		if u == tu and v == tv:
			break
		for t in range(4):
			var u2: int = u + Turns.DX[t]
			var v2: int = v + Turns.DY[t]
			if u2 < 0 or v2 < 0 or u2 >= n or v2 >= n:
				continue
			var k := u2 * n + v2
			if seen[k]:
				continue

			var cell = Projection.walkable(lv, surf, u2, v2, doors)
			var target: bool = u2 == tu and v2 == tv

			# A PLATE MAY ONLY BE THE DESTINATION. Stepping on one rewrites the
			# board, so every step planned after it was planned against a world
			# that no longer exists. Routing stops there and you plan again —
			# which also makes standing on a plate a decision rather than
			# something that happens to you on the way past.
			if cell != null and Level.is_glyph(cell.t) and not target:
				continue

			if cell == null:
				if not target:
					continue
				var raw: Surf = surf[k]
				if not raw.has or not Level.is_walk_type(raw.t):
					continue
				var di := lv.door_index_at(raw.w)
				if di < 0 or (doors & (1 << di)) != 0 or keys_held() < 1:
					continue

			seen[k] = true
			from[k] = q[i2]
			q.append(k)
		i2 += 1

	var end := tu * n + tv
	if not seen[end]:
		return null
	var outp := []
	var cur := end
	var guard := 0
	var start_k := int(start.x) * n + int(start.y)
	while cur != start_k:
		outp.push_front(cur)
		cur = from[cur]
		guard += 1
		if cur < 0 or guard > n * n + 4:
			return null
	return outp


# ---- the three verbs -------------------------------------------------------

func tap_cell(u: int, v: int) -> void:
	if walking != null or anim != null or won:
		return

	var s: Surf = surf[u * n + v]
	if not s.has:
		emit_signal("toast", "VOID — UNRENDERED")
		emit_signal("denied", u, v, Palette.fault())
		return
	if not Level.is_walk_type(s.t):
		emit_signal("toast", "GRID — NO FOOTING ON THIS FACE")
		emit_signal("denied", u, v, Palette.fault())
		return

	var di := lv.door_index_at(s.w)
	var shut: bool = di >= 0 and (doors & (1 << di)) == 0
	if shut and keys_held() < 1:
		emit_signal("toast", "LOCKED — COLLECT EVERY NODE")
		Haptics.buzz(Haptics.FAULT)
		emit_signal("denied", u, v, Palette.LOCK)
		return

	var path = path_to(u, v)
	if path == null:
		emit_signal("toast", "NO WAY TO THAT LOCK" if shut else "NOT CONNECTED — FOLD THE ENGINE")
		emit_signal("denied", u, v, Palette.fault())
		return

	if path.empty():
		# Tapping the cell you already stand on. On a plate that PRESSES IT AGAIN
		# — the spring-back puts you back on the plate, and without this the only
		# way to fire it again would be to step off and step back on, which needs
		# a walkable neighbour. A plate cut through a wall of rock does not always
		# have one, and that is a soft lock: standing on the one thing that can
		# change the board, unable to use it.
		if Level.is_glyph(s.t):
			_push_undo()
			fire_plate(Level.glyph_bit(s.t))
			_after_action()
		return

	_push_undo()
	walking = Walk.new()
	walking.queue = path
	walking.from = pos
	walking.t = 0.0


func advance_walk(dt_ms: float) -> void:
	if walking == null:
		return
	walking.t += dt_ms / 105.0
	if walking.t < 1.0:
		return

	if walking.queue.empty():
		walking = null
		settle()
		return
	var k: int = walking.queue[0]
	walking.queue.remove(0)
	var cell: Surf = surf[k]
	if not cell.has:
		walking = null
		settle()
		return                                     # the board moved under a queued step

	walking.from = pos
	pos = cell.w

	var di := lv.door_index_at(pos)
	if di >= 0 and (doors & (1 << di)) == 0:
		doors |= 1 << di
		emit_signal("door_opened", pos, di)
		emit_signal("toast", "LOCK OPEN")
		Haptics.buzz(Haptics.MOVE)
	else:
		emit_signal("stepped", pos, cell)

	var ki := lv.key_index_at(pos)
	if ki >= 0 and (kmask & (1 << ki)) == 0:
		kmask |= 1 << ki
		emit_signal("key_taken", pos, ki)
		emit_signal("toast", "NODE")
		Haptics.buzz(Haptics.MOVE)

	# THE LAYOUT THE FOOT IS IN, NOT THE ONE THE CUBE WAS CARVED IN.
	#
	# This read the one-argument glyph_at, which takes `vox` and only `vox` — and
	# the overload beside it exists precisely because a glyph lives in the solid
	# that carries it. Once a trigger had swapped the board, every plate in the
	# second solid was stepped on and did nothing: the walk arrived, the cell
	# under the foot was a plate on the board being played and plain rock in the
	# board being read, and the bit came back zero. The world never flipped, the
	# lattice the route needed stayed solid, and the cube could not be finished
	# from a position the solver had reached legally.
	var gb := lv.glyph_at_world(pos, world)
	if gb != 0:
		fire_plate(gb)

	walking.t = 0.0
	if walking.queue.empty():
		walking = null
		emit_signal("landed", pos)
		settle()
		_after_action()
	else:
		compute_reach()


func can_turn(t: int) -> bool:
	if walking != null or anim != null or won or lv == null:
		return false
	return Projection.landing(lv, Turns.apply(t, m), pos, doors, world) != null


func try_turn(t: int) -> bool:
	if walking != null or anim != null or won:
		return false
	var m2 := Turns.apply(t, m)
	if Projection.landing(lv, m2, pos, doors, world) == null:
		return false

	var axis_x: bool = t == 0 or t == 1
	var to: float = PI / 2.0 if (t == 1 or t == 2) else -PI / 2.0
	var from: float = drag.ang if (drag != null and drag.has_axis and drag.axis_x == axis_x) else 0.0
	drag = null
	_push_undo()
	anim = Anim.new()
	anim.axis_x = axis_x
	anim.ang = from
	anim.from = from
	anim.to = to
	anim.t = 0.0
	anim.dur = 180.0 + 150.0 * (abs(to - from) / (PI / 2.0))
	anim.turn = t
	anim.seat = true
	return true


# A REFUSED FOLD IS A PHYSICAL EVENT, not an error message. The cube commits a
# few degrees, hits the stop, and slams back — so the answer arrives in the hand
# before the toast arrives in the eye.
func refuse_turn(t: int) -> void:
	var axis_x: bool = t == 0 or t == 1
	var sgn: float = 1.0 if (t == 1 or t == 2) else -1.0
	var from: float = drag.ang if (drag != null and drag.has_axis and drag.axis_x == axis_x) else sgn * 0.14
	drag = null
	anim = Anim.new()
	anim.axis_x = axis_x
	anim.ang = from
	anim.from = from
	anim.to = 0.0
	anim.t = 0.0
	anim.dur = 260.0
	anim.turn = -1
	anim.hard = true
	emit_signal("fold_refused", t)
	emit_signal("toast", "NO FOOTING THAT WAY")
	Haptics.buzz(Haptics.FAULT)


func spring_back() -> void:
	if drag == null or not drag.has_axis:
		drag = null
		return
	anim = Anim.new()
	anim.axis_x = drag.axis_x
	anim.ang = drag.ang
	anim.from = drag.ang
	anim.to = 0.0
	anim.t = 0.0
	anim.dur = 190.0
	anim.turn = -1
	drag = null


# The detent, as a curve. A plain ease-out arrives at ninety degrees and stops,
# which is what a slideshow does. A real latch goes slightly past its seat and is
# pulled back into it, so this overshoots by a few degrees and settles — and that
# half-frame of coming BACK is most of what makes the fold feel like a mechanism
# instead of a transition.
static func ease_seat(p: float) -> float:
	var c := 1.9
	return 1.0 + (c + 1.0) * pow(p - 1.0, 3.0) + c * pow(p - 1.0, 2.0)


# A refusal gets no overshoot: arrive early, stop dead.
static func ease_slam(p: float) -> float:
	return 1.0 - pow(1.0 - p, 5.0)


func advance_anim(dt_ms: float) -> void:
	if anim == null:
		return
	anim.t += dt_ms
	var p: float = min(1.0, anim.t / anim.dur)
	var e: float
	if anim.hard:
		e = ease_slam(p)
	elif anim.seat:
		e = ease_seat(p)
	else:
		e = 1.0 - pow(1.0 - p, 3.0)
	anim.ang = anim.from + (anim.to - anim.from) * e
	if p < 1.0:
		return

	var t: int = anim.turn
	anim = null
	if t < 0:
		return

	m = Turns.apply(t, m)
	var land = Projection.landing(lv, m, pos, doors, world)
	pos = land.w

	var ki := lv.key_index_at(pos)
	if ki >= 0 and (kmask & (1 << ki)) == 0:
		kmask |= 1 << ki
		emit_signal("key_taken", pos, ki)
		emit_signal("toast", "NODE")

	turns += 1
	emit_signal("fold_landed", t)
	Haptics.buzz(Haptics.FOLD)
	settle()
	_after_action()


# ---- plates ----------------------------------------------------------------

func fire_plate(bit: int) -> void:
	world ^= bit
	# THE CLOCK IS FOR THE PLATE BITS ONLY. An everted world with no plate in it
	# is a standing state, not a countdown, and so is a triggered one — otherwise
	# stepping on either would start a five-second timer that takes back
	# something nobody asked to be temporary.
	#
	# This said "blind to bit 4" and was written as `world & ~EVERTED`, which is
	# the same statement only while 1, 2 and 4 are the whole world. The trigger's
	# bits 8 and 16 slipped straight through it. The mask is named now, in Level,
	# so the next bit has to be declared a plate rather than become one by
	# default.
	plate_t = PLATE_MS if (world & Level.PLATES) != 0 else 0.0
	lv.clear_eff()
	surf = Projection.project(n, lv.eff(world), m)
	emit_signal("plate_fired", pos, bit)
	compute_reach()
	emit_signal("settled")
	Haptics.buzz(Haptics.MOVE)


func step_plate_clock(dt_ms: float, screen_up: bool) -> void:
	# A CLOCK ONLY EXISTS WHILE A PLATE IS UP. This asked whether the world was
	# non-empty, which was the same question until the trigger arrived: with a
	# layout swapped and no plate fired, plate_t is zero and the world is not, so
	# every frame fell into the branch below and called revert_world — a shake, a
	# rust flash and a sound, sixty times a second, over a board that could no
	# longer get anywhere.
	if (world & Level.PLATES) == 0 or lv == null or won or screen_up:
		return

	# AN EXPIRY WAITS OUT A FOLD IN FLIGHT. A fold is a third of a second of the
	# board rotating toward a landing that was checked against the world it
	# started in; reverting the material half way through would resolve it
	# against a cube that no longer exists. So the clock is allowed to sit at
	# zero for the tail of a rotation, which costs the player nothing they could
	# have spent and costs the state machine one whole class of bug.
	if plate_t <= 0.0:
		if anim == null and drag == null:
			revert_world()
		return

	var was := plate_t
	plate_t = max(0.0, plate_t - dt_ms)
	var lo := int(ceil(plate_t / 1000.0))
	if lo != int(ceil(was / 1000.0)) and lo > 0 and lo <= 3 and lo != _last_plate_tick:
		_last_plate_tick = lo
		emit_signal("plate_tick", lo)


# THE SPRING BACK. Everything fire_plate does, aimed the other way — plus the one
# thing a plate never has to do: deal with a player who is now inside the rock.
#
# It takes an undo point FIRST. The revert is the only thing in the game that
# moves you without you having asked, so the state you were in when it landed has
# to be recoverable, or a five-second limit would be a five-second trap.
func revert_world() -> void:
	_push_undo()
	# EVERSION SURVIVES THE CLOCK, AND THAT IS THE WHOLE DIFFERENCE.
	#
	# A plate is a five-second window: it changes the board and takes it back,
	# and the pressure is the countdown. An everter changes which FACE of the
	# solid is the surface, and nothing about that is temporary — you are
	# standing on the far side until you stand on another everter.
	#
	# It is not a preference. The solver carries `world` through its search and
	# has no notion of the clock at all, because the clock is real time and the
	# search is not. A polarity that expired would make every cube in the everter
	# chapter solvable on paper and impossible in the hand, and nothing in the
	# harness could have seen it.
	#
	# AND A TRIGGER IS THE SAME ARGUMENT. It changes which SOLID the cube is,
	# which is a larger claim than which face of it you are on, and it is just as
	# permanent. This read `world &= EVERTED`, so a plate lapsing quietly threw
	# away the layout the player had switched to — on half the catalogue, against
	# a solver that had already counted on it being there.
	world &= Level.STANDING
	plate_t = 0.0
	_last_plate_tick = -1
	walking = null      # any queued route dies with the world it was planned in
	lv.clear_eff()
	surf = Projection.project(n, lv.eff(world), m)
	emit_signal("reverted")
	throw_to_plate()
	compute_reach()
	emit_signal("settled")
	_after_action()


# Put the player back on the nearest plate if the world they have just been
# returned to has no footing where they stand.
#
# LANDING ON A PLATE DOES NOT FIRE IT. Same rule as folding onto one: you press a
# plate with a foot, and being put on one is not pressing it. Otherwise the
# spring-back would flip the world straight back and start a clock nobody asked
# for.
func throw_to_plate() -> bool:
	var v := Projection.view_of(n, m, pos)
	var hv := Projection.view_of(n, m, pos)
	var here := Projection.surface_at(n, surf, m, pos)
	var on_plate := false
	if here:
		var cell_here = Projection.walkable(lv, surf, int(hv.x), int(hv.y), doors)
		on_plate = cell_here != null and Level.is_glyph(surf[int(hv.x) * n + int(hv.y)].t)
	if on_plate:
		return false

	var found := false
	var best: Surf = null
	var bd := 0x7FFFFFFF

	for u in range(n):
		for w in range(n):
			var cell = Projection.walkable(lv, surf, u, w, doors)
			if cell == null:
				continue
			if not Level.is_glyph(cell.t):
				continue
			var d := (u - int(v.x)) * (u - int(v.x)) + (w - int(v.y)) * (w - int(v.y))
			if d < bd:
				bd = d
				best = cell
				found = true

	# A cube can only be in a flipped world if it has a plate in it, so the
	# search above cannot come up empty in practice. The fallback is the old
	# behaviour — nearest square of any kind — because a marker with nowhere to
	# stand would be a crash, and a crash is worse than a bad landing.
	if not found:
		if here and Projection.walkable(lv, surf, int(hv.x), int(hv.y), doors) != null:
			return false
		for u in range(n):
			for w in range(n):
				var cell2 = Projection.walkable(lv, surf, u, w, doors)
				if cell2 == null:
					continue
				var d2 := (u - int(v.x)) * (u - int(v.x)) + (w - int(v.y)) * (w - int(v.y))
				if d2 < bd:
					bd = d2
					best = cell2
					found = true

	if not found:
		return false
	pos = best.w
	emit_signal("thrown_to_plate", pos)
	emit_signal("toast", "RETURNED TO PLATE")
	Haptics.buzz(Haptics.FAULT)
	return true


# ---- two reads that change nothing -----------------------------------------

# THE ANTIPODE: the cell diametrically opposite the one you are standing in —
# through the centre, not around the outside.
#
# STRAIGHT THROUGH, NOT ACROSS. Only the DEPTH of the view coordinates is
# flipped: same column, opposite end. The world-space diagonal opposite reads
# wrong — a hole you are looking INTO shows you what is directly behind it, not
# something off to one side. This one is behind you by definition from wherever
# you are reading, and it re-aims itself every time the engine folds.
func antipode() -> Vector3:
	var v := Projection.view_of(n, m, pos)
	return Projection.world_of(n, m, Vector3(v.x, v.y, n - 1 - int(v.z)))


enum Special { NONE, NODE, LOCK, CORE }


# What can be seen through the horizon at the far side of the world, and that is
# ALL it does: no reach, no route, no bearing on any fold. It is a look through a
# hole, and the only reward is noticing.
#
# Which is worth having precisely because it costs nothing. A player who never
# sees it has lost nothing; a player who does has found out what the thing they
# are moving actually is.
func through_look() -> int:
	if lv == null:
		return Special.NONE
	var a := antipode()
	# a thing that is rock in this world is not in this world, and the far side
	# of a hole is no exception — the same rule the board draws by
	if not Level.is_walk_type(Level.eff_type(lv.vox[lv.vidx(a)], world)):
		return Special.NONE
	if a == lv.goal:
		return Special.CORE
	for i in range(lv.keys.size()):
		if (kmask & (1 << i)) == 0 and lv.keys[i] == a:
			return Special.NODE
	for i in range(lv.doors.size()):
		if lv.doors[i] == a:
			return Special.LOCK
	return Special.NONE


# ONE STEP AWAY, NOT THROUGH THE CENTRE.
#
# The opposite of the antipode: real, walkable neighbours — the four cells a
# single step from where you stand — checked for whether one of them is the core,
# an uncollected node, or a lock. Nothing here changes what you may do; it only
# tells the eye where the next useful step is before the hand has to work it out.
func near_specials(into: Array) -> void:
	into.clear()
	if lv == null or surf.empty():
		return
	var p := Projection.view_of(n, m, pos)
	for t in range(4):
		var u2: int = int(p.x) + Turns.DX[t]
		var v2: int = int(p.y) + Turns.DY[t]
		var cell = Projection.walkable(lv, surf, u2, v2, doors)
		if cell == null:
			continue
		if cell.w == lv.goal:
			into.append([u2, v2, Special.CORE])
			continue
		var ki := lv.key_index_at(cell.w)
		if ki >= 0 and (kmask & (1 << ki)) == 0:
			into.append([u2, v2, Special.NODE])
			continue
		var di := lv.door_index_at(cell.w)
		if di >= 0:
			into.append([u2, v2, Special.LOCK])


# ---- the hint --------------------------------------------------------------

# The first move of an optimal line from here, and nothing more — WITHOUT
# SPENDING ANYTHING.
#
# Split out of hint() because the hint is two separate things wearing one name:
# the search, and the charge for it. The coach needs the first and must not make
# the second — a player being taught which way a fold goes has not asked for a
# hint and must not be billed three of them for reading the tutorial. Nothing
# here writes to the session, which is what makes it safe to ask from anywhere.
func advice(budget: int = 40):
	if lv == null or won:
		return null
	var r := Solver.solve(lv, budget,
			SolveState.new(pos, Turns.ori_index(m), kmask, doors, world))
	if not r.ok or not r.has_first:
		return null
	return r.first


# The same answer, charged for.
func hint():
	if hints_left <= 0:
		return null
	var a = advice()
	if a == null:
		return null
	hints_left -= 1
	return a


# ---- after every action ----------------------------------------------------

func _after_action() -> void:
	if pos == lv.goal:
		_finish()
		return
	# The dead-end check is the same question the live readout asks every time
	# the state changes, so there is one run and this is the trigger.
	remain.schedule(self)


func _finish() -> void:
	won = true
	SolveClock.stop()
	emit_signal("finished")
	emit_signal("cube_won")
