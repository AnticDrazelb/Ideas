extends Reference
class_name Ending

# WHAT THE STAGE HAS TO BE ABLE TO DO, AND NOTHING ELSE.
#
# The ending is the only sequence in this game that runs for eight seconds with no
# input in it, drives five subsystems at once, and can never be reached in testing
# without first solving a hundred and fifty cubes. It shipped having never executed
# a single line — and when it did not work there was nothing to look at but the
# source, which was correct.
#
# So the choreography is separated from the things it choreographs, on the same
# argument the rest of this port is built on: Session decides what is true and
# GameDirector decides what that looks like, and now Ending decides what happens
# WHEN while the director decides what each of those is made of. That makes the
# sequence a pure function of a clock, which is a thing the harness can run to
# completion at sixty frames a second and assert the whole trajectory of.
#
# ---- THE STAGE ------------------------------------------------------------
#
# The C# states this as an interface; GDScript has none, so it is written down
# here and the stage is any object with these methods. There is deliberately no
# Camera, no CanvasLayer and no Material among them: a stage that took those would
# only be runnable by the engine, which is the position this file exists to get out
# of.
#
#   dt() -> float           Unscaled seconds since the last frame. The only clock.
#   half_height() -> float  Half the visible world height, off the framing camera.
#   aspect() -> float       Width over height of the picture.
#   ui(a)                   The instrument: every canvas, faded as one. 1 present, 0 gone.
#   board(a)                The board: deck, schematic, cage and every glyph, as one.
#   close(k)                0 the framing the game is played in, 1 one cell filling
#                           the aperture.
#   rumble(amt)             Hold the frame at this much trauma. NOT an impulse.
#   blob(size, alpha)       A white disc at the goal, this many world units across.
#   ring(r0, r1, dur, width, core)
#   sparks(speed, size)
#   bang()                  One frame with everything on it: kick, punch, rumble,
#                           haptics, the crack, the stop.
#   chime()                 The tone the star arrives on.
#   hush(seconds)           Take the bed down and hold it down for this long.
#   input(on)
#   clear()
#   title()
#   say(word)               One word, typed, on the black. The ending does not wait
#                           on it — the silence below is already long enough to hold
#                           it, and a sequence that blocked on a screen a touch can
#                           dismiss would have two clocks.
#
# ---- THE END OF THE MACHINE -----------------------------------------------
#
# A hundred and fifty cubes and this is the last one, so it is the only place in
# the game that gets to be an ENDING rather than a transition. Everything the
# ordinary exit does is wrong here: the throw says "and now the next one", the card
# asks a question, and the vault chime congratulates you on a boundary you are not
# crossing.
#
# SO NOTHING IS THROWN. The board is not broken open — it is left exactly as it was
# solved, and the picture closes in on the one square you reached instead. What
# goes away is everything AROUND it: the readout, the housing, the glass, the whole
# instrument the game has been played through, until what is left on screen is a
# cell and the dark.
#
# Then it grows. The singularity has spent a hundred and fifty cubes being the
# smallest thing on the board and it stops being small — white, because every other
# colour in this palette means something and none of them mean this, and bright
# enough that the bloom takes it. The shockwave is the ring the ordinary exits gave
# up: it was competing with debris there and here there is nothing for it to
# compete with.
#
# And then black, and three seconds of it, and the title. A game that has an ending
# should be allowed to stop for a moment before offering you anything.

# The four moves, written down once, because the sound has to know how long the
# picture is.
const CLOSE := 2.4
const GROW := 2.2
const BLOW := 0.55
const BLACK := 3.0

# hush() holds for its argument plus 2.4s and then takes 1.2s to bring the bed
# back, so what it wants is the whole ending minus that lead-in — which lands the
# hum under the title rather than under the shockwave.
const SILENCE := CLOSE + GROW + BLOW + BLACK - 2.4

# A hard stop, so a stage whose clock never advances cannot hang the harness that
# is driving it. The sequence itself yields every frame and so can never hang the
# game; this is the number of resumes a caller should give up after.
const FRAME_CAP := 4000


# A COROUTINE, RESUMED BY WHOEVER OWNS THE CLOCK.
#
# `yield()` with no arguments is exactly `yield return null`: it suspends and hands
# back a function state, and one `resume()` is one frame. The director pumps it
# from its own step; a test pumps it from a loop and can run eight seconds of
# ending in a millisecond.
static func run(s):
	s.input(false)
	s.hush(SILENCE)

	# ---- one: everything but the board goes ---------------------------------
	#
	# The instrument fades and the camera closes at the same rate, so the screen
	# does not empty and THEN move — it is one gesture, and it ends with a cell
	# alone in the dark.
	var t := 0.0
	while t < CLOSE:
		var k: float = clamp(t / CLOSE, 0.0, 1.0)
		var e := k * k * (3.0 - 2.0 * k)
		s.ui(1.0 - e)
		s.close(e)
		# not all the way down: there has to be something for the star to be
		# standing on when it lights
		s.board(1.0 - e * 0.86)
		yield()
		t += s.dt()
	s.ui(0.0)
	s.close(1.0)
	yield()   # one frame for the rig to publish the closed frame

	# EVERY SIZE BELOW IS MEASURED OFF THE FRAME, NOT WRITTEN DOWN.
	#
	# The camera is at a sixth of its usual aperture by now, so the world units the
	# rest of the game is tuned in mean something completely different here: the
	# ordinary exit's ninety-unit ring would cross the closed frame in a fortieth of
	# its life and never be seen, and a blob that swells to twenty-six would fill the
	# screen a third of the way through the growth and spend the rest of it white on
	# white. Both are the same mistake — a number that only made sense at one zoom.
	var half: float = max(0.0001, s.half_height())
	# a square quad covering a frame of ANY aspect, corners included
	var fills: float = 2.0 * half * max(1.0, s.aspect()) * 1.15

	# ---- two: it is not small any more --------------------------------------
	s.chime()
	t = 0.0
	while t < GROW:
		var k: float = clamp(t / GROW, 0.0, 1.0)
		# slow, then not slow. A star does not swell at a constant rate and neither
		# should the thing that has been standing in for one.
		var e := k * k * k
		s.blob(lerp(half * 0.22, fills, e), min(1.0, 0.35 + e * 1.6))
		# A RISING HOLD, NOT A SHOVE A FRAME. A shake ADDS trauma and trauma decays
		# at 2.4 a second, so calling it every frame for two seconds pins the frame at
		# maximum from about a fifth of the way in and stays there — which is not a
		# star waking up, it is the picture coming off its mount. rumble() sets the
		# level instead.
		s.rumble(0.05 + e * 0.35)
		yield()
		t += s.dt()

	# ---- three: the shockwave -----------------------------------------------
	s.bang()
	# TWO RINGS, ONE BEHIND THE OTHER. The white one is the front and the
	# core-coloured one is what it leaves behind — the same trick the ordinary
	# collapse uses, at the scale of the frame it is crossing.
	s.ring(half * 0.1, fills * 1.6, 0.85, half * 0.30, false)
	s.ring(half * 0.1, fills * 1.1, 1.10, half * 0.13, true)
	s.sparks(fills * 1.3, half * 0.10)

	t = 0.0
	while t < BLOW:
		s.blob(fills * (1.0 + t * 5.5), max(0.0, 1.0 - t / BLOW))
		yield()
		t += s.dt()

	# ---- four: three seconds of nothing -------------------------------------
	#
	# Not a fade to a menu. A stop. It is the only silence in this game longer than
	# the two hundred milliseconds the ordinary collapse gets, and it is the whole
	# reason the ending reads as one.
	s.board(0.0)
	s.rumble(0.0)
	s.clear()

	# AND THE MACHINE SAYS THE LAST THING. Nine chapters have each had one word after
	# them; this is the tenth, and it is here rather than in front of the last
	# chapter because an interstitial before this sequence would compete with a beat
	# that is already tuned. It types itself inside the silence — see Chapters.
	s.say(Chapters.FAREWELL)
	t = 0.0
	while t < BLACK:
		yield()
		t += s.dt()

	# and the instrument comes back on with the title behind it
	s.board(1.0)
	s.close(0.0)
	s.ui(1.0)
	s.input(true)
	s.title()
