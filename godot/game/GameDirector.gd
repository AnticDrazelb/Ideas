extends Node
class_name GameDirector

# The thing that owns a run: it loads a cube, pumps the session, and turns the
# session's events into light and noise.
#
# The split is deliberate and it is the whole architecture. Session decides what
# is true. This decides what that looks like. Nothing in Session ever touches a
# mesh, which is why the rules can be tested with no scene loaded and why the port
# could be proved identical to the original before a single pixel existed.

const _I := []


static func i() -> GameDirector:
	return _I[0] if not _I.empty() else null


var s: Session = null
var view: CubeView = null
var rig: CameraRig = null

var hud = null
var filter: ScreenFilter = null
var glow: Bloom = null
var sfx: Sfx = null

var _sky: DeepSky
var _input: InputRouter
var _fx: Fx
var _orb: PlayerOrb
var _screen_up := true      # a menu is over the board
var _last_w := 0
var _last_h := 0

# THE BOARD IS DRAWN OFFSCREEN AND COMPOSITED.
#
# Unity hands a camera effect the frame it just rendered; Godot's unit of "render
# this and then read it" is a Viewport. So the whole 3D board — the camera, the
# solid, the debris, the marker and the sky behind them — lives in one, and what
# the player sees is that viewport's texture with the glow added back over it.
# Everything else in the game is a CanvasLayer drawing on top of that plate.
var _world: Viewport
var _board_layer: CanvasLayer

# The board's composite sits under the housing, which is 5.
const BOARD_ORDER := 0


func _ready() -> void:
	_I.clear()
	_I.append(self)
	Store.load_save()

	_build_world()

	s = Session.new()
	rig = CameraRig.new()
	rig.name = "CameraRig"
	add_child(rig)
	rig.init(_world)

	view = CubeView.new()
	view.name = "CubeView"
	_world.add_child(view)
	view.init(s)

	sfx = Sfx.build(self)
	_fx = Fx.build(_world)
	_orb = PlayerOrb.build(_world, s, view, rig.cam, _fx)
	_input = InputRouter.new(s, view, rig.cam)
	_input.sfx = sfx

	# AND THE SKY UNDER EVERYTHING, before the housing goes on. It is a canvas
	# layer inside the board's own viewport, which is what BG_CANVAS makes into
	# that viewport's background — see _build_world.
	_sky = DeepSky.build(_world)

	# THE HOUSING FIRST, so everything after it is mounted IN something. Order 5,
	# behind the HUD's 10 and the screens' 20, and it is twelve pieces around the
	# edge rather than a plate — see Chassis.
	Chassis.build()

	hud = Hud.build(self)

	# THE PANEL'S OWN ROWS AT 24 AND THE PANE ACROSS THE FRONT AT 25, both in
	# front of the menus as well — see Scanlines and Glass. Built after the HUD
	# only because it is easier to read in the order the light arrives; the layer
	# number is what actually decides, not where these two lines sit.
	Scanlines.build()
	Glass.build()

	# The sound's words go to the readout. Sfx does not know what a Hud is and must
	# not: it raises the caption and something else decides where words go, exactly
	# as Session raises events and this decides what they look like.
	sfx.caption = funcref(hud, "caption")

	# ORDER MATTERS: bloom first, so the brightness control raises the finished
	# picture rather than the picture the glow was computed from.
	glow = Bloom.attach(self, _world, _board_layer)
	filter = ScreenFilter.attach(self)

	_wire()
	Screens.build(self)
	Screens.show_title()   # which starts the attract cube

	# and the panel comes up: one band down the glass, once, because a machine that
	# is switched on does something and a picture does not
	Scanlines.warm_up()

	set_process(true)


# THE SKY IS A CANVAS LAYER, AND A CANVAS LAYER IS IN FRONT.
#
# Godot draws every 2D layer over the 3D pass, whatever its order, so the sky's
# layer at -100 would sit on top of the cube instead of behind it. BG_CANVAS is
# the one setting that changes that: the viewport's background becomes its own
# canvas up to `background_canvas_max_layer`, and everything above that layer
# draws over the 3D as usual. -1 is exactly the boundary — the sky is below it and
# nothing else in this viewport is a canvas at all.
func _build_world() -> void:
	_world = Viewport.new()
	_world.name = "World"
	_world.own_world = true
	_world.transparent_bg = false
	_world.render_target_update_mode = Viewport.UPDATE_ALWAYS
	_world.render_target_v_flip = true
	_world.size = Layout.screen_size()
	# the interface has its own hit testing and the board's is CellAtPoint, which
	# projects rather than picks — nothing here needs a physics query
	_world.physics_object_picking = false
	add_child(_world)

	var env := Environment.new()
	env.background_mode = Environment.BG_CANVAS
	env.background_canvas_max_layer = -1
	# No ambient light of its own: every colour on the board is written by the cell
	# shader, unlit, exactly as the original's materials are.
	env.ambient_light_color = Color(0, 0, 0, 1)
	env.ambient_light_energy = 0.0

	var we := WorldEnvironment.new()
	we.name = "WorldEnvironment"
	we.environment = env
	_world.add_child(we)

	_board_layer = CanvasLayer.new()
	_board_layer.name = "Board"
	_board_layer.layer = BOARD_ORDER
	add_child(_board_layer)


# Where a cell is, for the debris. The effects live in a screen-aligned plane and
# do not rotate with the cube — they belong to the event, not to the geometry — so
# only the projected position matters.
func at(cell: Vector3) -> Vector3:
	var v := Projection.view_of(s.n, s.m, cell)
	var c := (s.n - 1) * 0.5
	return Vector3(v.x - c, v.y - c, 0.0)


func at_player() -> Vector3:
	return at(s.pos)


# IF THE SAVE DID NOT LOAD, SAY SO.
#
# Starting somebody over without a word is the worst thing this code can do — and
# it is also what a player will ASSUME happened any time a vault list looks wrong,
# so the silence costs trust even on the runs where nothing went wrong. Store knows
# which of the three copies it is running on; this is the one place that asks.
#
# A toast rather than a modal. There is nothing to decide: the bytes are already
# quarantined and there is no undo to offer, so an OK button is a speed bump in
# front of a fact. RECOVERED is the good outcome and reads like one; LOST is the
# bad one and says the word.
func _report_save() -> void:
	if Store.loaded_from() == Store.Origin.RECOVERED:
		hud.toast("SAVE RESTORED FROM BACKUP")
	elif Store.loaded_from() == Store.Origin.LOST:
		hud.toast("SAVE UNREADABLE · STARTING OVER")


func _wire() -> void:
	_report_save()
	s.connect("toast", self, "_on_toast")
	s.connect("settled", self, "_on_settled")
	s.connect("stepped", self, "_on_stepped")
	s.connect("landed", self, "_on_landed")
	s.connect("door_opened", self, "_on_door_opened")
	s.connect("key_taken", self, "_on_key_taken")
	s.connect("fold_landed", self, "_on_fold_landed")
	s.connect("fold_refused", self, "_on_fold_refused")
	s.connect("denied", self, "_on_denied")
	s.connect("plate_fired", self, "_on_plate_fired")
	s.connect("reverted", self, "_on_reverted")
	s.connect("thrown_to_plate", self, "_on_thrown_to_plate")
	s.connect("stuck_changed", self, "_on_stuck_changed")
	s.connect("plate_tick", self, "_on_plate_tick")
	s.connect("cube_won", self, "_on_win")


func _on_toast(msg: String) -> void:
	hud.toast(msg)


func _on_settled() -> void:
	view.rebuild()
	view.update_reach()
	rig.fit(s.n)
	_fx.set_board(s.n)
	hud.refresh(s)


func _on_stepped(cell: Vector3, surf: Surf) -> void:
	rig.shake(0.045)
	sfx.step()
	_fx.footfall(at(cell), Palette.tile(surf.t, surf.d, s.n))


func _on_landed(cell: Vector3) -> void:
	rig.kick(3, 0, 1)
	rig.shake(0.10)
	sfx.land()
	_fx.land(at(cell), Palette.GRID_HI)
	_orb.hop()
	_orb.set_mood("land", 260.0)
	# WHAT RETIRES THE LESSON IS THE PLAYER DOING IT. Not the prompt being shown,
	# not a card being dismissed — see Coach.
	Coach.learned(Coach.LEARNED_STEP)


func _on_door_opened(cell: Vector3, _idx: int) -> void:
	rig.kick(5, 0, 1)
	rig.shake(0.34)
	rig.punch(0.030)
	hud.flash(Palette.NODE, 0.30)
	sfx.lock()
	_fx.lock_open(at(cell))
	_orb.set_mood("happy", 800.0)
	TimeBend.hitstop(46.0)


func _on_key_taken(cell: Vector3, _idx: int) -> void:
	rig.shake(0.20)
	rig.punch(0.022)
	hud.flash(Palette.NODE, 0.26)
	sfx.node()
	_fx.node_taken(at(cell))
	_orb.set_mood("happy", 900.0)
	TimeBend.hitstop(30.0)
	# taking one is the lesson; see Coach
	Coach.learned(Coach.LEARNED_NODE)


func _on_fold_landed(t: int) -> void:
	# THE LANDING IS THE VERB, and it gets four things at once on purpose: an AIMED
	# kick along the fold's own axis so the shove has a direction, TRAUMA on top so
	# the whole frame is briefly unsafe, a PUNCH of zoom which is what makes it read
	# as a hit rather than a slide, and a square front leaving the cube at board
	# scale.
	var sgn: float = 1.0 if (t == 1 or t == 2) else -1.0
	var axis_x: bool = t < 2
	rig.kick(9, -sgn if axis_x else 0.0, 0.0 if axis_x else sgn)
	rig.shake(0.62)
	rig.punch(0.055)
	rig.squash(axis_x, 0.075)
	hud.flash(Palette.TRACE_NEAR, 0.20)
	sfx.fold()
	_fx.fold_landed(s.n, at_player())
	TimeBend.hitstop(52.0)
	view.rebuild()
	view.restart_reveal()
	Coach.learned(Coach.LEARNED_FOLD)


func _on_fold_refused(t: int) -> void:
	# A REFUSAL IS A COLLISION. More trauma than a successful fold and none of the
	# reward: the frame jolts, the walls close in for a beat, and there is no flash
	# at all. Light means yes in this game, so no has to be told in the dark.
	var sgn: float = 1.0 if (t == 1 or t == 2) else -1.0
	var axis_x: bool = t < 2
	rig.kick(5, -sgn if axis_x else 0.0, 0.0 if axis_x else sgn)
	rig.shake(0.5)
	rig.punch(-0.012)
	rig.squash(axis_x, 0.045)
	hud.vignette(1.0)
	sfx.deny()
	_fx.fold_refused(s.n, t)
	_orb.set_mood("wide", 620.0)
	# a refusal gets a LONGER stop than a landing and none of the reward: the cube
	# hit a wall, and a wall is the one thing in this game that should take a frame
	# away from you
	TimeBend.hitstop(78.0)


func _on_denied(u: int, v: int, col: Color) -> void:
	hud.vignette(0.55)
	rig.shake(0.10)
	sfx.deny()
	var c := (s.n - 1) * 0.5
	_fx.deny(Vector3(u - c, v - c, 0.0), col)


func _on_plate_fired(cell: Vector3, bit: int) -> void:
	# AND THE SAME RULE FOR THE THREE THE BOARD DOES TO YOU. The bit says which, so
	# one handler retires all three and nothing here has to know what chapter it is.
	# Plate A is absent on purpose — it has a card of its own.
	if bit == 2:
		Coach.learned(Coach.LEARNED_SUBSTRATE)
	elif bit == Level.EVERTED:
		Coach.learned(Coach.LEARNED_EVERTER)
	elif bit == Level.SWAPPED or bit == Level.SWAPPED2:
		Coach.learned(Coach.LEARNED_TRIGGER)

	# AN EVERTER IS NOT A PLATE AND MUST NOT FEEL LIKE ONE.
	#
	# A plate changes the BOARD — every trace goes dead, every dead cell lights —
	# and it earns the loudest moment in the game. An everter changes nothing about
	# any cell; it changes which face of the solid you are standing on. So it gets
	# its own weight: a long, symmetrical turn rather than a bang, violet rather
	# than rust, and a stop at the CROSSING rather than at the strike, because the
	# moment the solid is edge-on is the moment the two boards trade places.
	if bit == Level.EVERTED:
		view.rebuild(true)
		rig.punch(0.055)
		rig.squash(false, 0.11)
		hud.flash(Palette.EVERT, 0.30, 0.62)
		sfx.evert()
		Scanlines.warm_up()
		_fx.plate_fired(s.n, at(cell))
		_orb.set_mood("wide", 900.0)
		# the hold lands halfway through the turn, where the solid is edge-on —
		# CubeView.EVERT_SECONDS is the whole flip, so half of it is the crossing
		Chassis.pulse(Palette.EVERT, 0.55)
		TimeBend.hitstop(90.0)
		TimeBend.slowmo(0.42, CubeView.EVERT_SECONDS * 1000.0)
		view.restart_reveal(0.26 * 26.0)
		hud.toast("EVERTED — THE FAR SIDE" if s.everted() else "RESTORED — THE NEAR SIDE")
		return

	# THE BIGGEST EVENT IN THE GAME, and the only one that changes the board rather
	# than the view of it.
	view.rebuild(true)
	rig.kick(13, 0, 1)
	rig.shake(0.95)
	rig.punch(0.075)
	rig.squash(true, 0.09)
	hud.flash(Palette.rust(), 0.24)
	sfx.plate(bit)
	# THE WORLD TURNING INSIDE OUT IS THE MACHINE'S EVENT, not the board's, so it
	# is the one that reaches the housing.
	Chassis.pulse(Palette.rust(), 0.9)
	_fx.plate_fired(s.n, at(cell))
	_orb.set_mood("wide", 700.0)
	# the world turning inside out is worth a held breath: a long stop, then a third
	# of speed easing back over most of a second
	TimeBend.hitstop(120.0)
	TimeBend.slowmo(0.34, 620.0)
	# the reachable sweep follows the front out from the plate rather than racing
	# it, so the two read as one event
	view.restart_reveal(0.26 * 26.0)
	hud.toast("INVERT — FIVE SECONDS" if s.world != 0 else "RESTORED")


func _on_reverted() -> void:
	view.rebuild(true)
	rig.shake(0.42)
	hud.flash(Palette.rust(), 0.18)
	sfx.plate(0)


func _on_thrown_to_plate(cell: Vector3) -> void:
	rig.shake(0.42)
	rig.kick(9, 0, 1)
	sfx.deny()
	_fx.deny(at(cell), Palette.fault())
	_orb.hop()
	_orb.set_mood("wide", 620.0)


func _on_stuck_changed() -> void:
	hud.vignette(1.0)
	sfx.stuck()
	_orb.set_mood("wide", 1400.0)


# one tick a second over the last three, so the plate clock can be HEARD while the
# eyes are on the board — where they have to be, and where the number is not
func _on_plate_tick(seconds: int) -> void:
	sfx.tick_clock(seconds)


# ---- the attract cube ------------------------------------------------------
#
# THE FRONT OF THE GAME IS NEVER A STILL PICTURE.
#
# This used to run once, at boot, and play() turned it off — so the first visit to
# the menu had a turning cube and every visit after it had the dead board of
# whatever was last played. Showing the title is what asks for it now, so there is
# no way to arrive at that screen without it.
#
# Root two is the worst silhouette a bounded three-quarter pose presents; the rest
# is air, so a corner never touches the rule above it or the primary below.
# Root two is the worst silhouette a bounded three-quarter pose presents and the
# rest is air. 1.53 is the C#'s number and it is four pixels short on a taller
# phone, where the title band is a different shape — measured across the whole
# cycle by tests/ui.gd rather than judged from one pose.
#
# AND SIX PIXELS SHORT AGAIN WHEN THE DEVICE TURNS, for a reason worth writing
# down: held, the title's band is six hundred wide and a hundred and eighty tall,
# so the fit takes the height and a lean has three hundred units of spare WIDTH
# to lean into. Turned the band is very nearly square — the cube is the left
# panel of the front screen now — and there is no spare axis at all, so the pose
# that was always the worst one is the first pose that has ever been measured
# against a rectangle its own shape. 1.68 is what it costs, everywhere, and four
# per cent off the attract cube is not a thing anyone can see.
const ATTRACT_MARGIN := 1.68


func show_attract() -> void:
	if view.attract:
		return      # already turning; do not reload under it
	_attract()


func _attract() -> void:
	yield(get_tree(), "idle_frame")
	# VAULT I ON PURPOSE. The attract loop is the title screen playing itself, so it
	# wants a small, legible, authored cube — not whatever the player has reached,
	# and not one of vault IX's arc cubes, whose whole point is a mechanic nobody
	# has been taught yet.
	var level: int = int(clamp(Store.data().reached, 1, Baked.LEVELS.size()))
	s.load_cube(LevelSupply.get_level(level), level, Session.LoadKind.PRACTICE)
	SolveClock.stop()
	view.attract = true
	view.whole()
	rig.pull = 0.0
	view.rebuild(true)
	view.skip_wave()
	# INTO THE BAND THE TITLE ACTUALLY LEAVES, AND FILLING IT.
	#
	# This was fit(n, 2.30) — the play window, with enough margin that a freely
	# tumbling cube never showed its diagonal past the gradient's clear stripe. Two
	# things were wrong with that and both were stale. The window it framed into is
	# the HUD's, whose two bands are different heights and are not drawn on this
	# screen at all, so the subject of the title sat seventy units low. And the
	# margin was paying for a full tumble AND for a paragraph of pitch that has
	# since been deleted from the screen.
	rig.fit_to(Layout.title_rect(), s.n, ATTRACT_MARGIN)
	_fx.set_board(s.n)


# ---- loading ---------------------------------------------------------------

func play(level: int, how: int = Session.LoadKind.VAULT, made_key: String = "") -> void:
	_play_routine(level, how, made_key)


func _play_routine(level: int, how: int, made_key: String) -> void:
	# Jumping somewhere far from the cache costs a real mint, so say so rather than
	# dropping a frame and hoping. One paint, then the work.
	var needs_cut := false
	if how != Session.LoadKind.DAILY:
		needs_cut = (how == Session.LoadKind.VAULT or how == Session.LoadKind.PRACTICE) \
				and not LevelSupply.has(level)

	if needs_cut:
		hud.toast("CUTTING VAULT " + str(level) + "…")
		yield(get_tree(), "idle_frame")
		yield(get_tree(), "idle_frame")

	var src: Level = null
	if how == Session.LoadKind.DAILY:
		src = Daily.data()
	elif how == Session.LoadKind.MADE:
		src = _decode_made(made_key)
	else:
		src = LevelSupply.get_level(level)

	if src == null:
		hud.toast("THAT CUBE IS NO LONGER HERE")
		Screens.show_title()
		return

	if how != Session.LoadKind.MADE and how != Session.LoadKind.DAILY:
		src.name = Vaults.level_name(level)

	# The node chime climbs a stack of fifths across a cube, and the bed is pitched
	# off the vault — so both are reset here, where a cube begins. The daily and a
	# forged cube borrow vault V's difficulty, so they borrow its room as well.
	sfx.reset_nodes()
	var band := Vaults.vault_of(level if (how == Session.LoadKind.VAULT
			or how == Session.LoadKind.PRACTICE) else Daily.SPEC_LEVEL)
	sfx.ambience(band)

	# and the board wears the same vault the room is pitched to. The lattice
	# corrodes as the ladder climbs — see Palette — so a cube that borrows vault
	# five's difficulty borrows its metal too, which is the same rule the ambience
	# above is already following.
	view.push_palette(band)

	# AND THE CASE WEARS THE SAME VAULT. The lattice already corrodes as the ladder
	# climbs and the housing is the same machine — a player ten chapters in should
	# be holding a visibly worse instrument than the one they started with, without
	# ever having read a number to know it. One line, from the band that was
	# already computed.
	Chassis.set_band(band)

	s.load_cube(src, level, how, made_key)
	view.attract = false
	# the last cube ended by being thrown across the room; this one is whole, square
	# to the camera, and the camera is back where it lives
	view.whole()
	rig.pull = 0.0
	view.rebuild(true)
	# the board arrives one cell at a time, outward from where the player is about
	# to be standing
	view.start_wave(s.lv.start, true)
	view.restart_reveal(0.12 * 26.0)
	_fx.set_board(s.n)
	# a new cube starts on a clean stage: debris, rings, a half-decayed shake and a
	# bent clock all belong to the cube that is over
	_fx.clear()
	_orb.reset()
	TimeBend.reset()
	rig.fit(s.n)
	hud.refresh(s)
	set_screen_up(false)

	# cut the next one while this one is played
	if how == Session.LoadKind.VAULT:
		LevelSupply.prebuild(level + 1)

	# TEACH IT WHEN IT SHOWS UP, ONCE. Not in the manual four screens earlier, not
	# as a toast that has gone before it is read — on the cube that has one, with the
	# thing itself waiting behind the card.
	if Store.data().saw_plate == 0 and _has_plate(s.lv):
		Store.data().saw_plate = 1
		Store.save()
		yield(get_tree().create_timer(0.26), "timeout")
		if not s.won:
			Screens.show_plate_teach()
		return

	# THE MATRIX IS THE COACH'S NOW, not a toast from here.
	#
	# The C# raises one 1.6-second line on cube three and never again — and spends
	# the offer BEFORE raising it, so a load landing during a walk burns the only
	# offer there will ever be on words nobody saw. Answering a missable prompt
	# with a second missable prompt is not an answer. Coach.next carries it
	# instead: a line that stays up for as long as the exit is out of sight and
	# retires itself the moment the player holds. Same place the two verbs are
	# taught, same rules about not stacking. See Coach.PEEK.
	pass


static func _has_plate(lv: Level) -> bool:
	for c in lv.vox:
		if c == Level.PLATE_A or c == Level.PLATE_B:
			return true
	return false


static func _decode_made(key: String) -> Level:
	var m = Store.made(key)
	if m == null:
		return null
	var lv: Level = ShareCode.decode(m.code)
	if lv == null:
		return null
	lv.par = m.par
	lv.name = m.name
	return lv


# ---- winning ---------------------------------------------------------------

func _on_win() -> void:
	var folds := s.turns
	var ms := SolveClock.ms()

	if s.is_daily():
		# The daily keeps its own score and never touches the progression: it is not
		# the forty-eighth vault, it just borrows its difficulty.
		var today := Daily.day_index()
		if Store.data().daily_day != today:
			Store.data().daily_day = today
			Store.data().daily_best = folds
		else:
			Store.data().daily_best = int(min(
					999 if Store.data().daily_best == 0 else Store.data().daily_best, folds))
		Store.data().daily_solved = 1

		# and everything about windows, streaks and pruning lives in one place rather
		# than being half here and half in whatever reads it
		DailyBoards.record(folds)
	elif s.is_made():
		# A cube the player built keeps its own two records and touches nothing else —
		# no progression, no vault total, no board. Its par is real (the solver proved
		# it) but its AUTHOR is not trustworthy.
		var m = Store.made(s.made_key)
		if m != null:
			if m.best < 0 or folds < m.best:
				m.best = folds
			if m.tbest < 0 or ms < m.tbest:
				m.tbest = ms
			Store.save()
	elif s.is_practice():
		pass  # A cube visited by number is a cube seen, not one cleared.
	else:
		# Folds and time are kept apart on purpose. They are improved by different
		# play — one by a better route, one by a faster hand — and a single "best run"
		# column would make beating one cost the other.
		var b = Store.try_best(s.level_no)
		if b == null or folds < b:
			Store.set_best(s.level_no, folds)
		# and the par it was scored against, which the vault grid rates it by
		Store.set_par(s.level_no, s.lv.par)
		var tb = Store.try_time_best(s.level_no)
		if tb == null or ms < tb:
			Store.set_time_best(s.level_no, ms)

		# ---- AND FINISHING SENDS YOU BACK TO THE START ----------------------
		#
		# The ladder relocks. Not a reset — every best, every par and every time
		# survives, and `runs` remembers that the machine has been taken to the core —
		# but the cubes are handed back one at a time, from one, the way they were the
		# first time. The last thing the machine says is goodbye and the second
		# chapter's word is `again.`; this is the game agreeing with them.
		#
		# Calibrate carries the way out, for somebody who wants to go straight back to
		# a late cube. See SaveData.unlocked.
		if s.level_no >= Vaults.LAST_CUBE:
			Store.data().runs += 1
			Store.data().reached = 1
			Store.save()
		# THE FRONTIER MOVES ONE CUBE AT A TIME, and only from itself. This was
		# `level_no + 1 > reached`, which is the same statement while nothing can be
		# played out of order — and is not, with the unlock switch on, where clearing
		# cube 140 would have jumped the frontier past a hundred and thirty-nine cubes
		# nobody solved.
		elif s.level_no == Store.data().reached:
			Store.data().reached = s.level_no + 1
			Store.save()
		# and there is no cube after the last one to cut. Asking for it would put the
		# generator to work on a hundred and fifty-first that is not in the ladder and
		# is not supposed to exist.
		if s.level_no < Vaults.LAST_CUBE:
			LevelSupply.prebuild(s.level_no + 1)

	rig.kick(11, 0, 1)
	rig.shake(1.0)
	rig.punch(0.09)
	hud.flash(Palette.core(), 0.22)
	Chassis.pulse(Palette.core(), 0.85)
	Haptics.buzz(Haptics.COLLAPSE)
	_fx.collapse(s.n, at_player())
	# through to the card. The exit is a second and a half now, and a mood that
	# lapses two thirds of the way through it puts the orb back to idle over an
	# empty room.
	_orb.set_mood("win", 1700.0)
	# THE ONE PLACE THE CLOCK REALLY BENDS. A long stop, then a third of speed, so
	# the core taking you lands at the pace of something ending rather than
	# something being dismissed.
	#
	# IT HAS TO BE OVER BEFORE THE MACHINE BREAKS. This was 900ms, which ran straight
	# through the exit — and the exit's own geometry is on the unscaled clock while
	# Fx is on the bent one, so the debris would have crawled while the cells it came
	# off flew. 470 puts the clock back at 1.0 a frame or two before the break, which
	# is also the right shape: slow for the collapse, real time for the failure.
	TimeBend.hitstop(150.0)
	TimeBend.slowmo(0.30, 470.0)

	# THE LAST CUBE DOES NOT END LIKE THE OTHER HUNDRED AND FORTY-NINE. Everything
	# below — the throw, the card, the vault chime — is for a cube you are going to
	# leave and come back from.
	if Vaults.ends_the_machine(s.level_no, s.is_daily(), s.is_made()):
		_finale()
		return

	# THE CARD DOES NOT APPEAR OVER A STILL IMAGE OF A SOLVED PUZZLE. The machine
	# turns to show you it is a solid, and then it is thrown.
	_win_exit()

	# A PERFECT LINE IS A DIFFERENT EVENT. Clearing at par with the same exit as
	# clearing four over says the game does not care, and the card saying PERFECT
	# COLLAPSE forty frames later is a receipt, not a celebration. It goes arc, which
	# is the one step past gold that anybody reads without being told.
	if folds <= s.lv.par:
		_perfect_line()

	# TWO HUNDRED MILLISECONDS OF NOTHING, AND THEN ONE TONE. The bed is ducked the
	# instant the core takes you, the room goes quiet under a board that is still
	# whole and about to stop being, and the tone arrives into that gap. It is the
	# only silence in the game — and it is now also the gap the machine breaks into,
	# three hundred milliseconds later, with the tone still ringing under it.
	sfx.duck(0.20)
	_win_tone()

	var crossing: bool = not s.is_daily() and not s.is_made() \
			and Vaults.vault_of(s.level_no + 1) != Vaults.vault_of(s.level_no)
	if crossing:
		sfx.vault()
	# and the card is the last beat of _win_exit, not a timer racing it


# ---- the ending ------------------------------------------------------------

var _ending := false


# THE DIRECTOR, SEEN THROUGH THE ENDING'S EYES.
#
# Ending decides what happens when; this decides what each of those things is made
# of. Every method is one line of forwarding on purpose — the moment a decision
# creeps in here it is a decision the harness cannot run, which is the whole reason
# the split exists.
class Stage:
	var d = null
	var at := Vector3.ZERO
	# The one thing that is not forwarding: whether the sequence reached its last
	# beat. GDScript has no exceptions, so "it stopped early" cannot be caught —
	# but it can be observed, and _finale observes it here.
	var arrived := false

	func dt() -> float:
		return d.get_process_delta_time()

	func half_height() -> float:
		# `Camera.size` is the world height of the WHOLE screen; the original's
		# orthographicSize is half of it, and every size the ending computes is
		# measured off this one.
		return d.rig.cam.size * 0.5

	func aspect() -> float:
		var sz := Layout.screen_size()
		return sz.x / max(1.0, sz.y)

	func ui(a: float) -> void:
		UiKit.dim(a)

	func board(a: float) -> void:
		d.view.dim(a)

	func close(k: float) -> void:
		d.rig.close = k
		d.rig.at = at

	func rumble(amt: float) -> void:
		d.rig.rumble(amt)

	func blob(size: float, alpha: float) -> void:
		d._fx.blob(at, size, Color(1, 1, 1, alpha))

	func ring(r0: float, r1: float, dur: float, width: float, core: bool) -> void:
		d._fx.ring2(at, r0, r1, dur, Palette.core() if core else Color(1, 1, 1, 1), width, false)

	func sparks(speed: float, size: float) -> void:
		d._fx.burst(at, 60, {
			"kind": Fx.Kind.SPARK, "speed": speed, "life": 1.4,
			"size": size, "gravity": 0.0, "col": Color(1, 1, 1, 1),
		})

	func bang() -> void:
		d.rig.kick(22, 0, 1)
		d.rig.shake(1.0)
		d.rig.punch(-0.10)
		Haptics.buzz(Haptics.COLLAPSE)
		Haptics.buzz(Haptics.SHATTER)
		d.sfx.shatter()
		TimeBend.hitstop(120.0)

	func chime() -> void:
		d.sfx.vault()

	func hush(seconds: float) -> void:
		d.sfx.duck(seconds)

	func input(on: bool) -> void:
		d._input.enabled = on

	func clear() -> void:
		d._fx.clear()
		d._orb.reset()

	# show_title starts the attract cube itself — asking twice is how the second
	# call ends up being the one that matters.
	func title() -> void:
		arrived = true
		Screens.show_title()

	func say(word: String) -> void:
		Screens.show_chapter_word(word, null)


# PUMP THE ENDING, AND PUT THE SCREEN BACK IF IT DIES.
#
# The C# drives the enumerator by hand inside a try block, because a coroutine
# that throws is logged once and then simply stops, leaving whatever it had already
# set exactly where it was — a screen half faded, with no card, no throw and no way
# to tell anything went wrong. GDScript has no exceptions to catch, so the same
# guarantee is bought a different way: the stage records reaching its last beat,
# and anything that ends this pump without that flag — a runtime error inside the
# sequence, or a clock that never advanced past the frame cap — takes the recovery.
func _finale() -> void:
	_ending = true
	var stage := Stage.new()
	stage.d = self
	stage.at = at(s.lv.goal)

	var seq = Ending.run(stage)
	var guard := 0
	while guard < Ending.FRAME_CAP:
		guard += 1
		if not (seq is GDScriptFunctionState) or not seq.is_valid():
			break
		yield(get_tree(), "idle_frame")
		seq = seq.resume()

	_ending = false
	if stage.arrived:
		return

	# it either stopped early or ran away with itself; either way, do not strand the
	# player on a board they cannot see and cannot leave
	view.dim(1.0)
	rig.close = 0.0
	rig.pull = 0.0
	UiKit.dim(1.0)
	_input.enabled = true
	Screens.show_title()


# THE EXIT, IN THREE MOVES.
#
# It used to be one: the cells shrank to points, running out from the core. That is
# a board being switched off, and switching a board off is what you do to make room
# for a card — which is exactly how it read.
#
# LEAN. The solid turns into three quarters and the camera eases back off it.
# Nothing breaks yet, and that is the point: an explosion only lands if the eye has
# just been shown that the thing is three-dimensional and made of parts. This is
# the one moment in the game where the board is looked AT rather than played.
#
# BREAK. One frame with everything on it — the kick, the shake, the counter-punch
# that snaps the frame outward instead of in, the rumble, the crack. The hit that
# starts the throw is a separate event from the collapse that started the sequence,
# because they are separate things: the core took you, and then the machine failed.
#
# THROW. Every cell out from the core on its own schedule, tumbling, and the camera
# holding back while they go. The card arrives into an empty room afterwards.
#
# All of it runs on the unscaled clock. The bent clock is still running underneath
# from the collapse and this is choreography, not physics — a sequence that slows
# down because the slow-motion it fired is still going is a sequence that fights
# itself.
func _win_exit() -> void:
	# let the collapse land first — the hitstop is still holding
	yield(get_tree().create_timer(0.17), "timeout")

	var t := 0.0
	while t < CubeView.LEAN_SECONDS:
		var k: float = clamp(t / CubeView.LEAN_SECONDS, 0.0, 1.0)
		view.win_lean = k * k * (3.0 - 2.0 * k)
		rig.pull = view.win_lean * 0.20
		yield(get_tree(), "idle_frame")
		t += get_process_delta_time()
	view.win_lean = 1.0
	rig.pull = 0.20

	rig.kick(16, 0, 1)
	rig.shake(1.0)
	# NEGATIVE punch: the orthographic size goes UP, so the frame jumps outward.
	# Every other punch in this game pulls in, because every other event is an
	# impact. This one is the board getting away from you.
	rig.punch(-0.055)
	rig.squash(false, 0.10)
	Haptics.buzz(Haptics.SHATTER)
	hud.flash(Palette.RUST_HI, 0.26, 0.75)
	# the whole machine goes, so this is centred on the board and not on the cell the
	# player happened to finish in
	_fx.shatter(s.n, Vector3.ZERO)
	sfx.shatter()
	# AND THE PICTURE HOLDS FOR EIGHTY MILLISECONDS. Everything is frozen together —
	# the solid still whole, the ring and the grit stopped where they were thrown —
	# and then the whole picture moves at once. Starting the throw inside the freeze
	# would move the cells while the sparks off them stood still, which is the same
	# clock mismatch the shortened slowmo above exists to avoid.
	TimeBend.hitstop(80.0)
	yield(get_tree().create_timer(0.08), "timeout")

	t = 0.0
	while t < CubeView.BURST_SECONDS:
		var k: float = clamp(t / CubeView.BURST_SECONDS, 0.0, 1.0)
		view.burst_amt = k
		# out with the debris and staying out, so the card lands on a room that is
		# bigger than the one the puzzle was played in
		rig.pull = 0.20 + k * 0.26
		yield(get_tree(), "idle_frame")
		t += get_process_delta_time()
	view.burst_amt = 1.0

	# AFTER THE LAST PIECE, NOT OVER IT. The card used to go up on a fixed 0.88s
	# timer, which was measured against an exit that no longer exists; it lives here
	# now so that there is exactly one description of how long the ending takes, and
	# moving any dial above moves the card with it. A beat of empty room first —
	# nothing is asked of the player until the machine has finished leaving.
	yield(get_tree().create_timer(0.16), "timeout")
	Screens.show_win(s, self)


func _perfect_line() -> void:
	yield(get_tree().create_timer(0.21), "timeout")
	_fx.perfect_line(s.n, at_player())
	hud.flash(Palette.ARC, 0.30, 0.9)
	rig.shake(0.7)
	rig.punch(0.05)
	sfx.vault()


func _win_tone() -> void:
	yield(get_tree().create_timer(0.20), "timeout")
	sfx.win()


func set_screen_up(up: bool) -> void:
	_screen_up = up
	_input.enabled = not up
	hud.set_visible(not up)
	if up:
		SolveClock.hold()
	else:
		SolveClock.go()


func screen_up() -> bool:
	return _screen_up


# ---- the frame -------------------------------------------------------------

func _process(dt: float) -> void:
	# Everything that MOVES runs on the bent clock; everything that MEASURES runs on
	# the real one. The solve clock is monotonic and untouched by any of this, so a
	# player cannot buy thinking time by triggering impacts.
	var ms := TimeBend.step(dt)

	Store.tick()
	LevelSupply.tick()

	# ONE READING OF THE DEVICE PER FRAME, whoever comes to ask. The sky is the only
	# thing that asks today.
	Tilt.sample(dt)
	_sky.tick(dt)

	# A ROTATION IS A RE-LAYOUT, NOT A RESIZE. The board is centred in the space the
	# HUD is not using, and that space is a different shape the instant the device
	# turns — so the fit is redone rather than stretched.
	var screen := Layout.screen_size()
	if int(screen.x) != _last_w or int(screen.y) != _last_h:
		_last_w = int(screen.x)
		_last_h = int(screen.y)
		# and the offscreen board is re-cut to match, or the composite would be a
		# stretched copy of the last shape the window had
		_world.size = screen
		# the window is cut to the board across and to the whole band down, so it is a
		# different rectangle in a different aspect — the stroke and the four fold
		# marks that hang on it are re-cut before the camera is re-fitted to them
		hud.relayout()
		if s.lv != null:
			# AND THE ATTRACT CUBE IS FRAMED BY ITS OWN RECT, which is the one thing
			# the C# forgets here: it re-fits to the PLAY window unconditionally, so
			# turning the device on the title screen would snap the cube to the
			# framing of a board that is not being played. Godot exposes it on every
			# launch rather than never, because `idle_frame` fires BEFORE `_process`
			# — the attract coroutine has already run and set its framing by the time
			# this branch first sees a screen size, where Unity's runs after Update
			# and this branch harmlessly skipped on a null level.
			if view.attract:
				rig.fit_to(Layout.title_rect(), s.n, ATTRACT_MARGIN)
			else:
				rig.fit(s.n)
			_fx.set_board(s.n)

	# THE PLATE'S FIVE SECONDS, ON THE HOUSING. The bar at the top of the window
	# says how long is left and the number under it says it again; neither is where
	# the eyes are, which is the board. The case is the whole edge of the display
	# and it is in peripheral vision the entire time.
	Chassis.set_clock((s.plate_t / Session.PLATE_MS) if (s.lv != null and s.world != 0
			and s.plate_t > 0.0) else 0.0)
	Chassis.tick(dt)

	# measured, not guessed: see PerfWatch
	PerfWatch.tick(dt)

	# THE WORD AFTER A CHAPTER, on the unscaled clock. The bent clock belongs to the
	# board and there is no board on that screen — a hitstop still decaying from the
	# collapse would otherwise stretch the typing. See Chapters.
	Screens.tick_chapter(dt)

	if s.lv != null:
		if not _screen_up:
			_input.tick(dt)
			s.advance_walk(ms)
			s.advance_anim(ms)
			s.step_plate_clock(ms, _screen_up)
		s.remain.tick(s)
		view.apply(rig.cam, dt)
		hud.tick(dt, s)
		hud.tick_depth(s, rig.cam, view)
		hud.tick_coach(s, rig.cam, view, dt)
		# AND THE SINGULARITY STOPS BEING A DOT. The finale grows a white giant out of
		# the cell the orb is standing on; leaving the orb drawn puts a black disc in
		# the middle of the star.
		_orb.tick(ms, not _screen_up and not _ending)
		_orb.emit_trail()
		_fx.tick(ms / 1000.0)

	# the attract cube spins free behind the title and has no window to stay inside,
	# so it is the one board the camera does not frame
	rig.tick(dt, view.cube, not view.attract)
	filter.refresh()
	glow.refresh()
	glow.tick()
	# the schematic's halo. The board is wire whenever the material is out of the way
	# — a held MATRIX, or the middle of an eversion — and a wire with no bloom is a
	# hairline. See Bloom.lift.
	glow.lift = view.glass_amount


# THE BACK GESTURE, AND IT REACHES EVERY SCREEN NOW. This was
# `Escape && !_screenUp && S.lv != null` — so back opened the pause card while
# playing and did nothing at all on the manual, the vault rack, calibrate, access,
# the Forge or its editor. See Screens.back_pressed for what each of them does with
# it, and why the title asks before it takes you at your word.
#
# Not during the ending: the machine is coming apart on its own clock and there is
# nothing to go back to until it has.
#
# `ui_cancel` is Godot's own name for Escape and for Android's back gesture, which
# is the pair the C# reads as KeyCode.Escape.
func _unhandled_input(event: InputEvent) -> void:
	if not event.is_action_pressed("ui_cancel") or _ending:
		return
	get_tree().set_input_as_handled()
	if not _screen_up and s.lv != null:
		Screens.show_pause(self)
	elif Screens.back_pressed():
		get_tree().quit()


func _notification(what: int) -> void:
	match what:
		# A phone call in the middle of a cube is not part of the solve. Godot raises
		# the app notifications on a handheld and the window ones on a desktop; both
		# mean the same thing to a solve clock.
		NOTIFICATION_APP_PAUSED, NOTIFICATION_WM_FOCUS_OUT:
			SolveClock.hold()
			Store.flush()
		NOTIFICATION_APP_RESUMED, NOTIFICATION_WM_FOCUS_IN:
			if not _screen_up:
				SolveClock.go()
			# AND THE SKY FORGETS HOW THE PHONE WAS BEING HELD. Whatever happened while
			# this game was not on screen, it was very likely a pocket — and coming back
			# to a sky jammed against one corner, with no travel in that direction, is
			# worse than coming back to it centred.
			Tilt.recentred()
		NOTIFICATION_WM_QUIT_REQUEST:
			Store.flush()
			LevelSupply.shutdown()
