class_name Scanlines
# THE DISPLAY HAS ROWS.
#
# The case says the interface is mounted in a machine and the glass says there is
# something between you and it. This says the picture itself is being SCANNED —
# that what you are reading is a signal on a panel with a line pitch, rather than
# a page. It is the cheapest of the three effects and it does the most per unit:
# an image with rows in it is a readout, and the same image without them is a
# screenshot.
#
# IT GOES UNDER THE GLASS AND OVER EVERYTHING ELSE, which is the whole of its
# place in the stack and is not arbitrary. The rows are the panel, so they are
# behind the dirt on the outside of the pane and in front of every pixel the
# panel is displaying — the board, the readout, and any menu that has come
# forward. Chassis 5, HUD 10, screens 20, this 24, glass 25.
#
# IT CAN ONLY DARKEN, AND ONLY EVERY THIRD ROW. Black at a third alpha over one
# row in three, which is an eleven percent dimming averaged over the eye and
# nothing at all on a ground that is already black. That matters because the
# access audit measures the interface's type against four dark grounds: a layer
# that lifted the ground would flatten every one of those pairs, and a layer that
# dims the foreground by a ninth moves Ink on Void from 16.8:1 to 15.0:1 — still
# more than twice the AAA floor.
#
# AND IT IS BUILT IN DEVICE PIXELS, not canvas units, which is the only detail
# here worth any care. A three-unit pitch stretched by a canvas scaler that is
# not an integer gives you lines that are two pixels wide in one band of the
# screen and one in the next, and the beat between them crawls when anything
# moves. So the texture is one texel wide by exactly as many rows as the opening
# has device pixels, drawn at one to one, and the pitch is rounded to whole
# pixels once. It is rebuilt when the display changes shape and never otherwise.

# Line pitch and line weight, in canvas units. Rounded to whole device pixels.
const PITCH := 3.0
const WEIGHT := 1.0

# How far a line darkens what is under it.
#
# Chosen by looking, at a fifth and at two fifths, against a stand-in board of
# lit cells. A fifth is invisible on anything but the brightest cell; two fifths
# starts to read as blinds over the picture rather than as the picture being
# scanned. This is a third, which shows on a lit cell and disappears on the void.
const STRENGTH := 0.34

const _PANE := []
const _FIT := []


# Light on the glass when the machine is switched on.
static func warm_up() -> void:
	if not _FIT.empty():
		_FIT[0].sweep(1.05, 0.55)


# The rows, on their own layer under the glass.
static func build() -> CanvasLayer:
	var c := UiKit.canvas("Scanlines", 24)

	var rt := UiKit.rect(UiKit.root_of(c), "rows", Vector2.ZERO, Vector2.ONE,
			Vector2(Chassis.inset_left(), Chassis.inset_bottom()),
			Vector2(-Chassis.inset_right(), -Chassis.inset_top()))
	rt.set_script(load("res://game/ScanlineFit.gd"))
	rt.call("setup")

	_PANE.clear()
	_PANE.append(rt)
	_FIT.clear()
	_FIT.append(rt)

	refresh()
	return c


# Off under legibility, on otherwise. Safe to call before build.
static func refresh() -> void:
	if _PANE.empty():
		return
	var want := not Access.legible()
	if _PANE[0].visible != want:
		_PANE[0].visible = want
