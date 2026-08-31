class_name DeepSky
extends CanvasLayer
# THE BLACK BEHIND THE MACHINE IS DEEP SKY NOW.
#
# NOT `Sky`, WHICH IS THE C#'s NAME FOR IT: Godot already has a Sky — the
# resource a PanoramaSky or ProceduralSky is one of — and a `class_name` that
# shadows a native class is refused outright.
#
# The camera cleared to the void and carried the argument for it: a void column
# is unrendered space — not a dark room, not deep sky, nothing — and anything
# painted there is the display claiming to have data it does not have. That
# argument was about AREA FILLS competing with the depth ramp, and it is kept
# here in the only form that matters: see sky.shader, whose one area fill is held
# below the dimmest cell the game draws. The stars are points, and a point is not
# a claim.
#
# WHY IT IS WORTH DOING AT ALL. The case is a photograph of a real object with a
# glass front, and behind the glass was a flat black rectangle. A black rectangle
# behind glass reads as a screen that is off. The same rectangle with a field of
# stars in it that MOVES WHEN THE PHONE MOVES reads as a window — and that is the
# whole fiction of this game, which is about a thing trapped inside a machine
# somewhere out there.
#
# IT IS FIXED IN THE WORLD, NOT IN THE HAND. Tilt gives the lean; the sky offsets
# against it, so tipping the phone right slides the field left and the parallax
# reads as looking round the edge of the window rather than as a texture sliding
# about on the glass. Three shells at different rates do the depth.
#
# AND IT IS GOVERNED BY CALIBRATE. At STILL the parallax is exactly zero and the
# field stands still — a player who asked the board to stop moving did not ask
# for a background that does. Under LEGIBLE there is no sky at all: that setting
# is a promise the interface will stop performing, and a starfield is the
# interface performing.
#
# IT IS A CANVAS LAYER UNDER THE BOARD, where the C# is a clip-space quad in the
# Background queue with bounds a million units wide so culling cannot reach it.
# Same picture and the same reason: the camera's size, offset, punch and shake
# move constantly, and the sky must not be able to see any of it.
#
# A 2D LAYER DRAWS OVER THE 3D WORLD, ALWAYS — that is not an ordering the layer
# index can change — so this only works because the viewport's environment is set
# to BG_CANVAS with its maximum background layer at -1. That is Godot's own name
# for exactly this: the canvas layers below the cut are the 3D background. Boot
# sets it, and it is the one line that makes the sky a sky rather than a curtain
# over the board.

# HOW FAR THE FIELD TRAVELS AT FULL LEAN, in the shader's own uv, where the whole
# screen height is 2.0. So the movement on glass is HALF this as a fraction of
# the screen — which is the conversion nobody did.
#
# THREE PER CENT WAS INVISIBLE AND THE CHECK SAID IT WAS FINE. It asserted the
# number sat between 0.005 and 0.05, a band with no meaning attached to it, and
# never converted it to anything a person could see. Three per cent of uv is 1.5%
# of the screen: twenty-nine pixels on a 1920-tall phone AT FULL LEAN, and full
# lean is a fast twenty-two degree tilt. Ordinary handling puts the lean around a
# third, which is NINE PIXELS of movement on small dim points against black. The
# sensor was working. The effect was not.
#
# Ten per cent is five per cent of the screen — about ninety pixels at full lean
# and twenty to forty in the hand. Still a background, and now a background that
# moves.
const TRAVEL := 0.100

# The one area fill in the shader, held below the dimmest cell the game draws.
const NEBULA_PEAK := 0.055

# THE BRIGHTEST A STAR GETS, AND IT IS UNDER THE BLOOM'S KNEE.
#
# A star is a point, and everything above rests on it staying one. The glow does
# not care: the bloom's threshold is 0.78, so a star over it comes back as a soft
# halo several times its own size and the claim about the size of a star stops
# being true of what is on the screen. Under the knee it is exactly as big as it
# is drawn.
#
# It is still bright — a dim star is a smudge, not a subtle star — and during a
# lift, when the machine is being looked through and the knee comes down to 0.40,
# the whole picture blooms and the sky goes with it. That is the effect doing
# what it is for, on a frame where nothing is being read off cell brightness
# anyway.
const STAR_PEAK := 0.74

# How much of the field carries a star in its nearest shell.
const DENSITY := 0.10

# THE RADIUS OF THE BIGGEST STAR, IN PIXELS, and it is a pixel count rather than
# a fraction of anything on purpose: a star is the one thing on this screen that
# should be exactly as big on a 1080 phone as on a 1440 one. The two further
# shells take 0.78 and 0.62 of it.
const STAR_PX := 1.7

var _mat: ShaderMaterial
var _rect: ColorRect
var _drift := Vector2.ZERO


# The wash, and it is the palette's own deep void rather than a new colour: the
# sky is the same cold blue the board's furthest cells fade into, which is what
# stops it reading as a second art direction.
static func tint() -> Color:
	return Color(Palette.TRACE.r * 0.42, Palette.TRACE.g * 0.42, Palette.TRACE.b * 0.42, 1.0)


static func build(under: Node) -> DeepSky:
	var s = Make.of("res://game/DeepSky.gd")
	s.name = "Sky"
	s.layer = -100
	under.add_child(s)
	s._compose()
	return s


func _compose() -> void:
	_rect = ColorRect.new()
	_rect.name = "field"
	_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_rect.anchor_right = 1.0
	_rect.anchor_bottom = 1.0
	add_child(_rect)

	var sh = Shaders.get_shader("res://shaders/sky.shader")
	if sh == null:
		push_error("[Singularity] the sky shader is missing")
		_rect.visible = false
		return

	_mat = ShaderMaterial.new()
	_mat.shader = sh
	_mat.set_shader_param("peak", STAR_PEAK)
	_mat.set_shader_param("nebula_peak", NEBULA_PEAK)
	_mat.set_shader_param("density", DENSITY)
	_mat.set_shader_param("star_px", STAR_PX)
	_mat.set_shader_param("tint", tint())
	_rect.material = _mat


func tick(dt: float) -> void:
	if _mat == null:
		return

	_mat.set_shader_param("screen_size", Layout.screen_size())

	# LEGIBLE TAKES THE SKY AWAY ENTIRELY, rather than dimming it. Half a
	# starfield is still a starfield behind the type.
	var fade := 0.0 if Access.legible() else 1.0
	_mat.set_shader_param("fade", fade)
	_rect.visible = fade > 0.0
	if fade <= 0.0:
		return

	# and STILL takes the movement without taking the picture
	var want := Tilt.look() * (TRAVEL * Access.motion_amount())

	# The reading is already filtered; this is the second, gentler one that stops
	# a single noisy accelerometer frame from being a visible step on a display
	# that is otherwise perfectly still.
	_drift = _drift.linear_interpolate(want, 1.0 - exp(-6.0 * dt))
	_mat.set_shader_param("drift", _drift)
