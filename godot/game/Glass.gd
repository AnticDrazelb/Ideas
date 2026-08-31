class_name Glass
# THE SCREEN IS BEHIND SOMETHING, AND THE SOMETHING IS FILTHY.
#
# The case put the interface inside a machine. This is the pane across the front
# of it: thirty years of fingerprints, dust, hairline scratches, a smear where
# somebody wiped it with a sleeve, and the ghost of the pixel lattice underneath.
# It is the same photographed object the case came from and it does the other
# half of the same job — a display with nothing in front of it is a picture of a
# display, however good the bezel around it is.
#
# IT ONLY EVER ADDS LIGHT, AND THAT IS THE WHOLE DESIGN.
#
# Alpha-blending a photograph of dirty glass over this interface would be a
# catastrophe, and not a subtle one: the picture's median is near black, so
# blending it would DARKEN the display toward the dirt everywhere the dirt is
# dark — which is most of it — and the access audit's four dark grounds would go
# with it. Additive cannot do that. Dust catches light; it does not remove it.
# The layer can make a pixel brighter and has no way at all to make one dimmer,
# so nothing that was legible before it stops being.
#
# The pedestal is taken off the picture before it ever gets here — see
# unity/tools/chassis/glass.py — so the clean half of the glass contributes
# exactly zero rather than lifting the whole display off black. What is left is
# the dirt, at these strengths, as a fraction of full white:
#
#     half the glass   0.000      it is clean, and adds nothing
#     nine tenths      under 0.02
#     the worst speck  0.29       a grain of dust, a few units across
#
# IT IS IN FRONT OF EVERYTHING, INCLUDING THE SCREENS, because that is where
# glass is. The chassis is at 5 and the HUD at 10 and the menus at 20; the pane
# is at 25, and a menu that came forward is still behind it. This is the one
# layer in the project that is deliberately drawn over the board, which the
# chassis goes to some length to avoid — the difference is that the chassis is
# opaque and this is light.
#
# AND IT GOES AWAY WHEN ASKED. Legibility mode is a promise that the interface
# will stop performing, and a layer of grease on the screen is the most
# performing thing in the game.

# How much of the picture reaches the screen.
#
# Chosen by looking, at a third and at two thirds, against a stand-in board of
# lit cells and rust-edged controls. Two thirds is a good picture of a filthy
# screen and starts to speckle the lit cells; a third is a machine somebody has
# been using. This is a third, plus a little, because the first choice was
# invisible on an OLED.
const STRENGTH := 0.34

const TEX_PATH := "res://assets/glass.jpg"

const _PANE := []
const _MAT := []


# The pane, on its own layer in front of everything.
static func build() -> CanvasLayer:
	var c := UiKit.canvas("Glass", 25)

	var tex = load(TEX_PATH)
	if tex == null:
		push_warning("Glass: " + TEX_PATH + " is missing; the pane will not be drawn.")
		return c

	# Exactly the opening, because that is where the glass is.
	var rt := UiKit.rect(UiKit.root_of(c), "pane", Vector2.ZERO, Vector2.ONE,
			Vector2(Chassis.inset_left(), Chassis.inset_bottom()),
			Vector2(-Chassis.inset_right(), -Chassis.inset_top()))

	var img := TextureRect.new()
	img.name = "dirt"
	img.texture = tex
	img.expand = true
	img.stretch_mode = TextureRect.STRETCH_SCALE
	# A FULL-SCREEN GRAPHIC THAT EATS EVERY TAP IS THE CLASSIC WAY TO SHIP A GAME
	# NOBODY CAN PLAY. It is in front of the buttons.
	img.mouse_filter = Control.MOUSE_FILTER_IGNORE
	img.anchor_right = 1.0
	img.anchor_bottom = 1.0
	img.material = additive()
	# The alpha is the strength: the material adds premultiplied by it, so the
	# tint's alpha is a straight multiplier on how much light this adds and
	# nothing here is ever transparent.
	img.modulate = Color(1, 1, 1, STRENGTH)
	rt.add_child(img)

	_PANE.clear()
	_PANE.append(rt)
	refresh()
	return c


# Off under legibility, on otherwise. Safe to call before build.
static func refresh() -> void:
	if _PANE.empty():
		return
	var want := not Access.legible()
	if _PANE[0].visible != want:
		_PANE[0].visible = want


# THE ONE ADDITIVE MATERIAL THE 2D LAYERS SHARE.
#
# The pane and the panel's roll are the same statement — light landing on the
# front of the glass, which can only ever add — so they share it, and it is
# separate from the marker's so that changing one cannot silently change the
# other.
static func additive() -> CanvasItemMaterial:
	if _MAT.empty():
		var m := CanvasItemMaterial.new()
		m.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
		_MAT.append(m)
	return _MAT[0]
