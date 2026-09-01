class_name Sfx
extends Node
# SONAR AND A SERVER ROOM.
#
# Every sound here is the same two primitives from Synth, layered and offset in
# time — and the offsets are the point. The footstep is a sub-bass thud with a
# ping eighty milliseconds behind it, and that gap is exactly what makes it read
# as sonar rather than as a beep. The fold is a servo: a whirr that bends up and
# settles, static riding under it, and a mechanical knock when the detent
# catches.
#
# TWO BUSES, DRY AND WET. The original builds a room out of two delays at
# prime-ish spacings, damped and fed back a little, and sends each sound to it by
# a different amount — the node chime is almost all room, the footstep is almost
# none. Here that is two real audio buses with a delay and a lowpass on the wet
# one, and a sound is played on both with the send as its level.
#
# THE MIXER IS NOT OPTIONAL HERE, AND IN THE C# IT IS. That file carries a long
# note about a seam: an AudioMixer cannot be created from script in Unity, so the
# room is either one bus or forty per-source filters depending on whether
# somebody has clicked one together in the editor. Godot's AudioServer takes
# buses at runtime, so the good path is the only path — two groups, one reverb,
# and the two faders are the bus volumes rather than a multiply applied at the
# moment a sound is told to play.
#
# That last difference is worth stating because it fixes a real behaviour: a
# fader multiplied in at play time does nothing to what is already ringing, so
# dragging ROOM while the bed is humming moves nothing until the next footstep. A
# bus volume is the bus.

const VOICES := 20

# THE TWO FADERS, AS BUSES.
const BUS_INSTRUMENT := "Instrument"
const BUS_ROOM := "Room"

# ONE TRIM ON THE WHOLE BUS, BECAUSE THE MIX IS NOT WHAT IS WRONG.
#
# Not one cue in this game is loud. Every cue is three to five layers, they
# overlap, each is sent to a wet voice as well, the bed hums under all of it and
# both faders go to 150%. Nobody had added the numbers up. A fold over a plate —
# the densest legal overlap in the game, and legal BY DESIGN, because "every fold
# is legal on a plate" is the sentence the manual sells them with — sums to 1.10
# against a mixer that clips hard at 1.0. Not a headroom preference: the
# difference between a sound and a click, on the loudest moment the game has.
#
# A TRIM RATHER THAN QUIETER CUES. Turning the layers down would fix the number
# and change the mix — how loud a knock is against a chime is the design, and it
# is the one thing that must not move. Multiplying the whole bus preserves every
# ratio exactly and costs 1.4 dB at the top of a fader nobody is at.
const HEADROOM := 0.85

# EVERYTHING THE GAME SAYS WITH A SOUND, IT ALSO SAYS IN WORDS.
#
# Almost every cue in this game already has a picture — the flash, the vignette,
# the HUD count, the draining bar — and that is the stronger guarantee, because
# it costs a player nothing to switch on. Captions are for the two places the
# picture is not where the eyes are: the plate clock, which is deliberately a
# sound BECAUSE the eyes have to be on the board and the number is at the top
# edge, and the refusals, whose whole design is that they happen in the dark.
#
# The words live next to the sound they caption rather than in a table somewhere
# else, because the failure mode of a table is a cue that gets added and never
# captioned, and nobody notices in a build they can hear.
var caption: FuncRef = null

var _dry := []
var _wet := []
var _dry_at := 0
var _wet_at := 0
var _bed: AudioStreamPlayer
var _amb_band := -1
var _duck_t := -1.0
var _duck_hold := 0.0

# how many nodes have been taken in this vault — the glass chime climbs a fifth
# for each one, so the last is the brightest sound in the cube
var _node_n := 0
# and how many steps into the current walk, so the sonar thud shifts a few hertz
# per footfall instead of repeating exactly
var _step_n := 0

var _bed_from := 0.0
var _bed_to := 0.0
var _bed_t := -1.0
var _bed_dur := 0.0


static func build(under: Node) -> Sfx:
	var s = Make.of("res://game/Sfx.gd")
	s.name = "Audio"
	under.add_child(s)
	s._init_bus()
	return s


func _say(words: String) -> void:
	if Access.captions() and caption != null and caption.is_valid():
		caption.call_func(words)


# The bed's level, which is the room's level.
func _bed_level() -> float:
	return 0.055 * HEADROOM


func _init_bus() -> void:
	_ensure_buses()

	var dry_root := Node.new()
	dry_root.name = "dry"
	add_child(dry_root)
	for _i in range(VOICES):
		_dry.append(_voice(dry_root, BUS_INSTRUMENT))

	var wet_root := Node.new()
	wet_root.name = "wet"
	add_child(wet_root)
	for _i in range(VOICES):
		_wet.append(_voice(wet_root, BUS_ROOM))

	_bed = AudioStreamPlayer.new()
	_bed.name = "bed"
	# THE BED IS THE ROOM, so it belongs on the room's fader. It is the one sound
	# in the game that is not an event, and a player turning ROOM down is asking
	# for exactly this to go away.
	_bed.bus = BUS_ROOM
	_bed.volume_db = -80.0
	add_child(_bed)

	apply_faders()
	set_process(true)


# THE ROOM IS ONE BUS. The delay spacing and damping are the original's, and the
# lowpass is what stops the tail turning into hiss after a few passes.
func _ensure_buses() -> void:
	if AudioServer.get_bus_index(BUS_INSTRUMENT) < 0:
		AudioServer.add_bus()
		var i := AudioServer.bus_count - 1
		AudioServer.set_bus_name(i, BUS_INSTRUMENT)
		AudioServer.set_bus_send(i, "Master")

	if AudioServer.get_bus_index(BUS_ROOM) < 0:
		AudioServer.add_bus()
		var j := AudioServer.bus_count - 1
		AudioServer.set_bus_name(j, BUS_ROOM)
		AudioServer.set_bus_send(j, "Master")

		# THE TAPS ARE ADDRESSED BY PATH, NOT BY FIELD. AudioEffectDelay exposes
		# `tap1/active`, not `tap1_active` — the slash is part of the property name
		# rather than an inspector grouping, so a plain assignment is a silent
		# nothing on a release build and a thrown "invalid set index" on a debug
		# one. set() takes the real name.
		var echo := AudioEffectDelay.new()
		echo.dry = 0.0
		echo.set("tap1/active", true)
		echo.set("tap1/delay_ms", 83.0)
		echo.set("tap1/level_db", -9.4)      # 0.34 as a ratio
		echo.set("tap2/active", false)
		echo.set("feedback/active", true)
		echo.set("feedback/delay_ms", 83.0)
		echo.set("feedback/level_db", -12.0)
		AudioServer.add_bus_effect(j, echo)

		var lp := AudioEffectLowPassFilter.new()
		lp.cutoff_hz = 1900.0
		AudioServer.add_bus_effect(j, lp)


# A 0..1.5 fader as decibels. Zero is not "very quiet", it is OFF, and -80 is the
# floor the engine treats as silence — the log of zero is negative infinity and
# the setter will take it, which mutes the bus in a way that then will not come
# back cleanly.
static func db(linear: float) -> float:
	return -80.0 if linear <= 0.0005 else clamp(20.0 * log(linear) / log(10.0), -80.0, 20.0)


# Push the two sliders onto the buses. Called whenever they move and once at
# startup.
func apply_faders() -> void:
	var i := AudioServer.get_bus_index(BUS_INSTRUMENT)
	var j := AudioServer.get_bus_index(BUS_ROOM)
	if i >= 0:
		AudioServer.set_bus_volume_db(i, db(Access.volume()))
		AudioServer.set_bus_mute(i, Store.data().sound == 0)
	if j >= 0:
		AudioServer.set_bus_volume_db(j, db(Access.room()))
		AudioServer.set_bus_mute(j, Store.data().sound == 0)


func _voice(under: Node, bus: String) -> AudioStreamPlayer:
	var a := AudioStreamPlayer.new()
	a.name = "v"
	a.bus = bus
	under.add_child(a)
	return a


# ---- scheduling ------------------------------------------------------------
#
# THE OFFSETS ARE THE SOUND. Several of these cues are two layers whose SPACING
# is the cue — eighty milliseconds for the sonar return, two hundred and ten for
# the detent knock — so a frame-timed delay would smear them by up to a frame
# each and make the same event sound different at 30fps and 120.
#
# The C# schedules on the DSP clock. Godot's player has no scheduled start, so a
# delayed layer is a one-shot timer on the scene tree's own clock, which is
# accurate to well under a millisecond and does not depend on the frame rate.
func _play(clip, gain: float, send: float, delay: float = 0.0) -> void:
	if Store.data().sound == 0 or clip == null:
		return
	if delay > 0.0:
		# A TIMER RATHER THAN A COROUTINE, because a coroutine here would be a
		# frame-quantised delay and the whole point of these offsets is that they
		# are not.
		var t := get_tree().create_timer(delay, false)
		t.connect("timeout", self, "_play_now", [clip, gain, send], 4)
		return
	_play_now(clip, gain, send)


func _play_now(clip, gain: float, send: float) -> void:
	# TWO LEVELS, BECAUSE THERE ARE TWO BUSES AND THEY ARE NOT THE SAME SOUND.
	# The dry bus is the instrument — the thud, the knock, the chime — and the wet
	# bus is the room it is in. A player who needs the cues louder does not
	# necessarily want more reverb, and a player on a small speaker usually wants
	# the room turned down rather than everything turned up.
	#
	# The FADERS are not applied here: a bus's volume is the bus itself, so
	# multiplying by the slider as well would apply it twice. What stays is the
	# per-cue gain and the send, because those are the MIX and not the faders:
	# they are how loud a knock is against a chime.
	var d: AudioStreamPlayer = _dry[_dry_at % VOICES]
	_dry_at += 1
	d.stream = clip
	d.volume_db = db(gain * HEADROOM)
	d.play()

	if send > 0.001:
		var w: AudioStreamPlayer = _wet[_wet_at % VOICES]
		_wet_at += 1
		w.stream = clip
		w.volume_db = db(gain * send * HEADROOM)
		w.play()


# THE RECORDING, IF SOMEBODY HAS PUT ONE THERE.
#
# One line at the top of a cue: if assets/audio/<name> exists it is played and
# the cue returns, and if it does not the cue falls through to the synthesis
# underneath it. That shape rather than a table of overrides because it keeps the
# recording's NAME next to the sound it replaces, and because a cue that grows a
# fourth layer next year does not need anybody to remember this file exists.
#
# A recording is one clip where the synthesis is two or three layers at measured
# offsets, so the send is the whole cue's send rather than any one layer's — a
# recorded footstep already HAS its ping in it.
func _sample(clip_name: String, gain: float, send: float) -> bool:
	var clip = Bank.get_clip(clip_name)
	if clip == null:
		return false
	_play(clip, gain, send)
	return true


func _tone(f: float, dur: float, type: int, gain: float, slide_to: float = 0.0,
		send: float = 0.18, at: float = 0.0) -> void:
	_play(Synth.tone(f, dur, type, 1.0, slide_to), gain, send, at)


func _noise(dur: float, cut: float, gain: float, send: float = 0.22,
		sweep_to: float = 0.0, at: float = 0.0) -> void:
	_play(Synth.noise(dur, cut, 1.0, sweep_to), gain, send, at)


# ---- the set ---------------------------------------------------------------

# The move: sub-bass thud, then the ping off the far wall.
func step() -> void:
	var f := 54.0 + (_step_n % 3) * 3.0      # barely moves — it is a thud
	_step_n += 1
	if _sample("step", 0.16, 0.14):
		return
	_tone(f, 0.20, Synth.Wave.SINE, 0.16, f * 0.72, 0.10)
	_noise(0.05, 220.0, 0.05, 0.10)
	_tone(1244.5, 0.14, Synth.Wave.SINE, 0.035, 0.0, 0.42, 0.080)


func land() -> void:
	_step_n = 0
	if _sample("land", 0.17, 0.16):
		return
	_tone(46.0, 0.30, Synth.Wave.SINE, 0.17, 34.0, 0.10)
	_tone(932.3, 0.22, Synth.Wave.SINE, 0.032, 0.0, 0.48, 0.086)


# The fold: a servo. Whirr, static under it, and a knock at the detent.
func fold() -> void:
	_say("FOLD")
	if _sample("fold", 0.13, 0.12):
		return
	_tone(78.0, 0.34, Synth.Wave.SAW, 0.055, 132.0, 0.10)
	_tone(157.0, 0.30, Synth.Wave.SQUARE, 0.016, 262.0, 0.08)
	_noise(0.30, 1800.0, 0.055, 0.14, 320.0)
	_noise(0.06, 900.0, 0.09, 0.08, 0.0, 0.210)
	_tone(58.0, 0.16, Synth.Wave.SINE, 0.13, 44.0, 0.08, 0.210)


# The mechanism complaining while it is being moved by hand.
func creak(v: float) -> void:
	# quantised, so the cache is a handful of clips rather than one per frame
	v = round(clamp(v, 0.0, 1.0) * 8.0) / 8.0
	if _sample("creak", 0.010 + v * 0.016, 0.10):
		return
	_tone(60.0 + v * 90.0, 0.09, Synth.Wave.SAW, 0.010 + v * 0.016, 0.0, 0.10)
	if v > 0.5:
		_noise(0.05, 700.0 + v * 1400.0, 0.012, 0.08)


# The refusal. Not a bonk and not a buzzer — a dropout. The bed cuts for a
# breath, broadband comes through, and it resolves onto a low note. It is the
# audio half of the frame that glitches.
func deny() -> void:
	_say("REFUSED")
	if _sample("deny", 0.075, 0.06):
		return
	_noise(0.10, 5200.0, 0.075, 0.06, 400.0)
	_tone(96.0, 0.22, Synth.Wave.SQUARE, 0.045, 62.0, 0.06)


# The node: one glassy partial, climbing a fifth-stack per node taken.
func node() -> void:
	_say("NODE")
	var f: float = 1174.7 * pow(1.5, min(5, _node_n))
	_node_n += 1
	# A RECORDING DOES NOT CLIMB, so the bank is allowed more than one. The
	# fifth-stack is the synthesis telling you how many nodes you have taken, and
	# a single sampled chime throws that away — so the second node looks for
	# node2, the third for node3, and any of them falls back to plain node.
	if _node_n > 1 and _sample("node" + str(_node_n), 0.075, 0.55):
		return
	if _sample("node", 0.075, 0.55):
		return
	_tone(f, 0.30, Synth.Wave.SINE, 0.075, 0.0, 0.55)
	_tone(f * 2.0, 0.16, Synth.Wave.SINE, 0.022, 0.0, 0.60)
	_noise(0.05, 9000.0, 0.020, 0.30)


# The lock: something in the machine being energised, and the barrier hissing
# out.
func lock() -> void:
	_say("GATE OPEN")
	if _sample("lock", 0.15, 0.30):
		return
	_tone(41.0, 0.85, Synth.Wave.SINE, 0.15, 110.0, 0.28)
	_tone(82.0, 0.70, Synth.Wave.TRIANGLE, 0.055, 220.0, 0.30)
	_noise(0.55, 400.0, 0.045, 0.34, 5200.0)


# The collapse. The silence before it is the sound: the bed is ducked to nothing
# and this arrives two hundred milliseconds into the gap. It is the only pure,
# sustained, unmodulated sound in the game, and a fanfare here would be one more
# loud thing among several — a hole in the sound is the most noticeable event
# audio can produce.
func win() -> void:
	_say("COLLAPSE")
	if _sample("win", 0.13, 0.58):
		return
	_tone(220.0, 2.2, Synth.Wave.SINE, 0.13, 0.0, 0.55)
	_tone(440.0, 2.0, Synth.Wave.SINE, 0.055, 0.0, 0.60)
	_tone(880.0, 1.4, Synth.Wave.SINE, 0.028, 0.0, 0.65, 0.240)


# THE ENGINE COMES APART. The crack, the fall, and the debris after it.
#
# It has to sit UNDER the win tone, which is already ringing by the time this
# fires — so it is broadband and short where the tone is narrow and long, and the
# only thing the two share is the sub, which arrives forty milliseconds late so
# the crack gets the transient to itself.
func shatter() -> void:
	_say("THE ENGINE COMES APART")
	if _sample("shatter", 0.12, 0.44):
		return
	_noise(0.09, 5200.0, 0.105, 0.18, 1400.0)                      # the crack
	_tone(150.0, 0.85, Synth.Wave.SAW, 0.060, 34.0, 0.40)          # the fall
	_noise(0.80, 2600.0, 0.050, 0.50, 180.0)                       # the debris
	_tone(44.0, 1.00, Synth.Wave.SINE, 0.095, 30.0, 0.14, 0.040)   # the floor going


func vault() -> void:
	_say("VAULT CLEARED")
	if _sample("vault", 0.13, 0.50):
		return
	_tone(55.0, 2.4, Synth.Wave.SINE, 0.13, 82.4, 0.55)
	_noise(1.4, 300.0, 0.035, 0.45, 4000.0)


# The lattice inverting: a sweep the direction the world went, and the rails
# re-seating.
func plate(bit: int) -> void:
	var up: bool = bit == 1
	_say("INVERTED" if up else "RESTORED")
	# TWO DIRECTIONS, TWO FILES. The synthesis sweeps the way the world went and
	# a recording cannot be run backwards convincingly, so the inversion and the
	# spring-back are separate cues in the bank.
	if _sample("plate" if up else "plate-off", 0.075, 0.32):
		return
	_tone(60.0 if up else 480.0, 0.75, Synth.Wave.SAW, 0.075, 480.0 if up else 60.0, 0.30)
	_noise(0.70, 400.0 if up else 6000.0, 0.06, 0.36, 7000.0 if up else 300.0)
	_noise(0.08, 1100.0, 0.075, 0.10, 0.0, 0.620)


# The plate clock, counting down. One tick a second over the last three, climbing
# in pitch, so the count can be HEARD while the eyes are on the board — where
# they have to be, and where the number is not.
func tick_clock(seconds_left: int) -> void:
	_say("PLATE · " + str(seconds_left) + "S")
	var i: int = int(clamp(3 - seconds_left, 0, 2))
	_tone(880.0 + i * 110.0, 0.09, Synth.Wave.TRIANGLE, 0.055, 0.0, 0.14)


# The tape running backwards.
func undo() -> void:
	_say("UNDONE")
	if _sample("undo", 0.045, 0.12):
		return
	_tone(180.0, 0.26, Synth.Wave.SAW, 0.045, 92.0, 0.12)
	_noise(0.20, 2400.0, 0.030, 0.12, 600.0)


# The machine reporting a dead end: two clicks and a low held note.
func stuck() -> void:
	_say("NO ROUTE FROM HERE")
	if _sample("stuck", 0.085, 0.20):
		return
	_noise(0.05, 1600.0, 0.06, 0.08)
	_noise(0.05, 1200.0, 0.05, 0.08, 0.0, 0.120)
	_tone(73.4, 0.9, Synth.Wave.SINE, 0.085, 69.0, 0.24, 0.240)


# THE ENGINE TURNING THROUGH ITSELF.
#
# Not a plate and it must not sound like one. A plate is a sweep — the world
# going one way — and this is a REVERSAL, so it is built as two slides that
# cross: one falling, one rising, meeting in the middle where the solid is
# edge-on. The ear hears the two pass through each other, which is the event.
func evert() -> void:
	_say("EVERTED")
	if _sample("evert", 0.11, 0.34):
		return
	_tone(320.0, 0.52, Synth.Wave.SINE, 0.070, 70.0, 0.30)          # falling
	_tone(70.0, 0.52, Synth.Wave.SINE, 0.070, 320.0, 0.30)          # rising, crossing it
	_noise(0.44, 900.0, 0.045, 0.34, 3600.0)
	_tone(52.0, 0.20, Synth.Wave.SINE, 0.100, 40.0, 0.10, 0.270)    # the detent, at the crossing


# The lattice going to glass and back: the drive spinning up, and down.
func peek() -> void:
	_say("MATRIX")
	if _sample("peek", 0.055, 0.22):
		return
	_tone(58.0, 0.60, Synth.Wave.SAW, 0.055, 175.0, 0.18)
	_noise(0.50, 600.0, 0.032, 0.30, 6000.0)


func peek_off() -> void:
	_say("MATRIX OFF")
	if _sample("peekoff", 0.045, 0.14):
		return
	_tone(140.0, 0.22, Synth.Wave.SAW, 0.045, 62.0, 0.14)
	_noise(0.16, 3000.0, 0.022, 0.12, 500.0)


# ---- the bed ---------------------------------------------------------------

func reset_nodes() -> void:
	_node_n = 0


func ambience(band: int) -> void:
	if Store.data().sound == 0:
		_bed.stop()
		return
	if band == _amb_band and _bed.playing:
		return
	_amb_band = band

	_bed.stop()
	# THE ROOM ITSELF, RECORDED, IF THERE IS ONE. Per band first — the hum is
	# tuned to the vault you are in, and a single loop across all ten throws that
	# away — then a plain bed, then the synthesis.
	var room = Bank.get_clip("bed" + str(band))
	if room == null:
		room = Bank.get_clip("bed")
	if room == null:
		room = Synth.bed(Synth.root_for(band))
	_bed.stream = room
	_bed.volume_db = -80.0
	_bed.play()
	_fade(_bed_level(), 1.2)


func stop_ambience() -> void:
	_amb_band = -1
	_bed.stop()


func _fade(to: float, seconds: float) -> void:
	# The inverse of db(): where the fade is starting from, as a ratio.
	_bed_from = 0.0 if _bed.volume_db <= -79.9 else pow(10.0, _bed.volume_db / 20.0)
	_bed_to = to
	_bed_t = 0.0
	_bed_dur = max(0.01, seconds)


# THE ROOM GOES QUIET. The only way to make silence in a game that is always
# humming — used once, by the collapse, and that is the point of it.
func duck(seconds: float) -> void:
	_fade(0.0001, 0.04)
	_duck_hold = seconds + 2.4
	_duck_t = 0.0


func _process(dt: float) -> void:
	if _bed_t >= 0.0:
		_bed_t += dt
		var k: float = clamp(_bed_t / _bed_dur, 0.0, 1.0)
		_bed.volume_db = db(lerp(_bed_from, _bed_to, k))
		if k >= 1.0:
			_bed_t = -1.0

	if _duck_t >= 0.0:
		_duck_t += dt
		if _duck_t >= _duck_hold and _bed_t < 0.0:
			_duck_t = -1.0
			_fade(_bed_level(), 1.2)
