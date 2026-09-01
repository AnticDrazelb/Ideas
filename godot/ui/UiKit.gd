class_name UiKit
# THE INTERFACE, BUILT IN CODE.
#
# There are no scenes and no scene-authored canvases anywhere in this project,
# and that is a deliberate choice rather than a shortcut. Every screen in the
# original is a composition of type, hairlines and nine colours with no images at
# all; expressing that as serialised .tscn would make it unreviewable in a diff
# and unmergeable by two people at once, while expressing it as code keeps the
# reasons next to the values — which is how the original was written and most of
# why it reads the way it does.
#
# THREE PLANES, AND THE DIFFERENCE BETWEEN THEM IS SMALL ON PURPOSE. Void is the
# ground. Panel is anything you can press. Card is anything that has come forward
# and taken the screen. Black is a background, not a material.

const FONT_PATH := "res://assets/mono.ttf"

# Standard control height — .btn min-height.
const BTN_H := 104.0
# The one lit object on a screen — .btn.primary min-height.
const PRIMARY_H := 120.0
# --h-row, for list rows.
const ROW_H := 100.0

# Every canvas this kit has made, so legibility can repaint the interface where
# it stands, and so the ending can take the whole instrument away at once. A list
# rather than a scene search because there are exactly five of them and they are
# all made right here — searching the tree for something you handed out yourself
# is how a sixth canvas somebody adds later quietly stops being repainted.
const _CANVASES := []
const _FONTS := {}
const _HAS_MONO := []


# --edge. The border weight every control shares.
static func edge() -> Color:
	var r := Palette.rust()
	return Color(r.r, r.g, r.b, 0.34)


# The primary's inset rim: rust-hi at 55%.
static func edge_on() -> Color:
	var r := Palette.RUST_HI
	return Color(r.r, r.g, r.b, 0.55)


# ---- the face --------------------------------------------------------------

# THE FACE THIS INTERFACE SPEAKS IN. IT IS A REAL ONE.
#
# It is called Mono, the design notes call it monospace, the CSS it was ported
# from used a real one — and for the whole of the Unity port's first life it
# returned Unity's built-in Arial. Not a taste decision: it was the only font
# obtainable while importing an asset was against the rules. That rule is gone,
# so this is JetBrains Mono, and the licence it ships under is in assets beside
# it.
#
# WHAT CHANGED, MEASURED RATHER THAN HOPED FOR. Every glyph is 0.6em, so the HUD
# chips tabular-align and a counter ticking 9 to 10 stops shoving the slash
# beside it. Across the thirty-one single-line labels in the game the median line
# got NARROWER — x0.963 — because uppercase is where a proportional face is
# widest and a monospace is not.
#
# THE FALLBACK IS NOT DECORATION. A font the engine could not parse, a stripped
# build, someone deleting it: a Label with no font draws NOTHING. Silent, total,
# every screen. So the engine's own default stays underneath as the thing that
# keeps the game readable rather than blank.
static func font(size: int) -> Font:
	if _FONTS.has(size):
		return _FONTS[size]
	var data = load(FONT_PATH)
	if data == null:
		_HAS_MONO.clear()
		_HAS_MONO.append(false)
		var fallback := DynamicFont.new()
		# There is nothing to load it FROM on a build with no font, so this is
		# the theme's default face at the right size rather than nothing at all.
		var d := Control.new()
		fallback.font_data = null
		d.free()
		_FONTS[size] = fallback
		return fallback
	if _HAS_MONO.empty():
		_HAS_MONO.append(true)
	var f := DynamicFont.new()
	f.font_data = data
	f.size = size
	# The type is authored in canvas units and the canvas is scaled as a whole,
	# so the face is rasterised at its authored size and scaled with everything
	# else — which is what keeps a 22-unit label 22 units on every display.
	f.use_filter = true
	_FONTS[size] = f
	return f


# Whether the imported face is the one being drawn, or the game fell back. Only
# the colophon asks — it should not credit a font it is not showing you.
static func has_mono() -> bool:
	font(22)
	return not _HAS_MONO.empty() and _HAS_MONO[0]


# ---- canvases --------------------------------------------------------------

# A LAYER, AND THE CONTROL THAT PUTS IT IN REFERENCE UNITS.
#
# Unity's CanvasScaler is reproduced rather than approximated: the root Control
# is scaled by Layout.canvas_scale and sized to the screen divided by it, so a
# child anchored at 0.5 with an offset of 128 units lands exactly where the C#
# puts it. Layout computes the same factor for its pixel arithmetic, from the
# same two reference numbers, so a pixel rectangle and a canvas rectangle can be
# put in the same sentence.
static func canvas(canvas_name: String, order: int) -> CanvasLayer:
	var layer := CanvasLayer.new()
	layer.name = canvas_name
	layer.layer = order

	var root := UiRect.new()
	root.name = "scaled"
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.set_script(load("res://ui/UiCanvasRoot.gd"))
	layer.add_child(root)

	# AND IT GOES INTO THE TREE HERE, because a canvas that is not in one draws
	# nothing and says nothing about it.
	#
	# The C# gets this free: `new GameObject(...)` is already in the scene, so no
	# caller of UiKit.Canvas passes a parent and none of the nine of them has
	# anywhere to put one. A Godot node is not in the tree until something adds it,
	# and an orphaned CanvasLayer accepts children, lays them out, reports the
	# right sizes and is simply never drawn — which is the whole interface missing
	# with no error anywhere.
	#
	# The tree's own root is the right parent rather than the caller's node: a
	# CanvasLayer is screen space by definition, it is the direct equivalent of the
	# overlay canvas the original makes, and hanging it off the director would put
	# the entire interface inside the board's offscreen viewport if the director
	# ever moved.
	var tree := Engine.get_main_loop()
	if tree != null and tree is SceneTree:
		(tree as SceneTree).root.add_child(layer)
	root.call("setup")

	_CANVASES.append(layer)
	return layer


# The scaled root of a canvas, which is what everything is parented to. The layer
# itself has no size and no scale.
static func root_of(layer: CanvasLayer) -> Control:
	return layer.get_node("scaled") as Control


# TAKE THE INSTRUMENT AWAY. Every canvas this kit has made, faded as one — the
# HUD, the chassis, the glass, all of it — leaving the board alone in the dark.
#
# It exists for exactly one moment in the game and it is written to be harmless
# the rest of the time: at 1 nothing is touched.
#
# AND AN INVISIBLE BUTTON IS STILL A BUTTON. Alpha is a paint setting; it does
# not stop a press, so a faded chassis would go on eating taps on a PAUSE nobody
# can see and open a card over the ending. The moment the instrument is not fully
# drawn it stops taking input as well — the two are the same statement about
# whether it is there.
static func dim(a: float) -> void:
	var live := a >= 0.999
	for i in range(_CANVASES.size() - 1, -1, -1):
		var c = _CANVASES[i]
		if not is_instance_valid(c):
			_CANVASES.remove(i)
			continue
		var root: Control = root_of(c)
		if root == null:
			continue
		root.modulate = Color(1, 1, 1, a)
		_set_input_enabled(root, live)


static func _set_input_enabled(node: Node, live: bool) -> void:
	if node is UiButton:
		node.enabled = live
	for child in node.get_children():
		_set_input_enabled(child, live)


# Swap the interface between the shipped palette and the legible one, in place.
# See Palette.TYPE_SWAPS for why type and surfaces are walked separately and why
# doing it twice is harmless.
#
# Alpha is carried through untouched, so the hairline that is rust at .20 is
# still the same hairline at .20 afterwards, and a colour that is in neither list
# — an interpolated tint, a button's pressed state — is left exactly where it was.
static func repaint() -> void:
	for i in range(_CANVASES.size() - 1, -1, -1):
		var c = _CANVASES[i]
		if not is_instance_valid(c):
			_CANVASES.remove(i)
			continue
		_repaint_node(root_of(c))


static func _repaint_node(node: Node) -> void:
	if node is Label:
		var got = _swap(Palette.TYPE_SWAPS, node.get("custom_colors/font_color"))
		if got != null:
			node.add_color_override("font_color", got)
	elif node is ColorRect:
		var got2 = _swap(Palette.FILL_SWAPS, node.color)
		if got2 != null:
			node.color = got2
	elif node is NinePatchRect or node is TextureRect:
		var got3 = _swap(Palette.FILL_SWAPS, node.modulate)
		if got3 != null:
			node.modulate = got3
	for child in node.get_children():
		_repaint_node(child)


static func _swap(list: Array, had):
	if had == null or not (had is Color):
		return null
	var legible := Access.legible()
	for s in list:
		var from: Color = s[0] if legible else s[1]
		var to: Color = s[1] if legible else s[0]
		if _same(had, from):
			return Color(to.r, to.g, to.b, had.a)
	return null


# Equal to the byte a colour was authored as. Ignores alpha.
static func _same(a: Color, b: Color) -> bool:
	return abs(a.r - b.r) < 0.002 and abs(a.g - b.g) < 0.002 and abs(a.b - b.b) < 0.002


# ---- the building blocks ---------------------------------------------------

static func rect(parent: Node, node_name: String,
		anchor_min: Vector2, anchor_max: Vector2,
		offset_min: Vector2, offset_max: Vector2) -> UiRect:
	var r := UiRect.new()
	r.name = node_name
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(r)
	r.anchor_to(anchor_min, anchor_max, offset_min, offset_max)
	return r


static func panel(parent: Node, node_name: String, col: Color,
		anchor_min: Vector2, anchor_max: Vector2,
		off_min: Vector2, off_max: Vector2) -> ColorRect:
	var rt := rect(parent, node_name, anchor_min, anchor_max, off_min, off_max)
	var img := ColorRect.new()
	img.name = "fill"
	img.color = col
	img.mouse_filter = Control.MOUSE_FILTER_IGNORE
	img.anchor_right = 1.0
	img.anchor_bottom = 1.0
	rt.add_child(img)
	return img


# ALIGNMENT, IN THE C#'s OWN VOCABULARY. Unity's TextAnchor is one value for both
# axes; Godot's Label has two properties. The nine names are kept because every
# screen in the game is written in them.
enum Anchor {
	UPPER_LEFT, UPPER_CENTER, UPPER_RIGHT,
	MIDDLE_LEFT, MIDDLE_CENTER, MIDDLE_RIGHT,
	LOWER_LEFT, LOWER_CENTER, LOWER_RIGHT,
}


# A label.
#
# OVERFLOW IS THE DEFAULT AND IT IS THE RIGHT ONE for what most of this interface
# is: a number in a chip, a word on a button, a caption under an icon. Those are
# laid out by their centre and are routinely wider than the rect they are centred
# in, deliberately.
#
# It is exactly wrong for a SENTENCE, and that difference is what `wrap` is for. A
# sentence in a fixed box with overflow on does not get smaller or break — it
# keeps going, out of the plate, over the bezel and off the machine, and the
# reader loses the end of every line. Anything written as prose asks to wrap.
static func label(parent: Node, node_name: String, text: String, size: int,
		col: Color, anchor: int,
		anchor_min: Vector2, anchor_max: Vector2,
		off_min: Vector2, off_max: Vector2, wrap: bool = false) -> Label:
	var rt := rect(parent, node_name, anchor_min, anchor_max, off_min, off_max)
	var t := Label.new()
	t.name = "text"
	t.text = text
	t.add_font_override("font", font(size))
	t.add_color_override("font_color", col)
	t.mouse_filter = Control.MOUSE_FILTER_IGNORE
	t.anchor_right = 1.0
	t.anchor_bottom = 1.0
	t.autowrap = wrap
	t.clip_text = false
	match anchor % 3:
		0:
			t.align = Label.ALIGN_LEFT
		1:
			t.align = Label.ALIGN_CENTER
		_:
			t.align = Label.ALIGN_RIGHT
	match int(anchor / 3):
		0:
			t.valign = Label.VALIGN_TOP
		1:
			t.valign = Label.VALIGN_CENTER
		_:
			t.valign = Label.VALIGN_BOTTOM
	rt.add_child(t)
	return t


# MAKE THIS ONE FIT, WHATEVER IT SAYS.
#
# For the labels whose text is DATA rather than authored copy — a vault's name, a
# cube's name, whatever somebody typed into the Forge — where the box is fixed by
# the layout around it and the string is not known when the screen is written.
# "VAULT I · CALIBRATION" at thirty-two between two arrows is four characters
# wider than the gap, so it slid under both of them and lost its first and last
# letter.
#
# Shrinking is the honest answer there and truncation is not: a name with its end
# cut off is a different name, and this game has cubes whose names differ in the
# last word. It is bounded below so it can never shrink to something nobody can
# read — if it would have to, the layout is wrong and should be seen to be wrong.
#
# Godot has no best-fit on a Label, so this is the same search done once: the
# largest size from the authored one down to `minimum` whose measured width fits
# the box. It is re-run whenever the text changes, through set_fitted.
static func fit(t: Label, minimum: int) -> Label:
	t.set_meta("fit_min", minimum)
	t.set_meta("fit_max", _size_of(t))
	_refit(t)
	return t


static func set_fitted(t: Label, text: String) -> void:
	if t.text == text:
		return
	t.text = text
	if t.has_meta("fit_min"):
		_refit(t)


static func _size_of(t: Label) -> int:
	var f = t.get_font("font")
	return int(f.size) if f is DynamicFont else 22


# THE BOX IS THE PARENT'S, AND MEASURING THE LABEL'S OWN RECT IS WHY THIS NEVER
# WORKED.
#
# A Label that does not wrap reports its TEXT as its minimum size, and a Control
# is never laid out smaller than its minimum — so a label whose string is wider
# than the rect it was given quietly grows past it, and `rect_size.x` comes back
# as the width of the text rather than the width of the box. Fitting the text to
# that is fitting the text to itself: the loop's very first candidate passes, the
# size never comes down, and the string sits over whatever is beside it.
#
# It is invisible at the authored aspect, where every box is wide enough that the
# minimum never bites, and it is why the vault heading printed over both of its
# arrows on a taller phone with fit() reporting that everything was in order.
#
# THE PARENT IS NOT THE ANSWER EITHER, and this is the second half of the same
# bug. A label parented to a box that is exactly its own rect gets the right
# number from the parent; a label ANCHORED to a fraction of a wider parent — the
# vault name in the play rail is half of it, the readout's identity line is a
# third — gets the whole parent back and believes it has twice the room it has.
# It then never shrinks, and the string runs off the end of the plate with fit()
# reporting that everything is in order.
#
# So the box is what the ANCHORS say: the share of the parent this label was
# given, plus its own two offsets. That is the rectangle the layout would have
# handed it if the text were not pushing back, which is the only honest answer to
# "how much room is there" — and it is the same answer as the parent's width in
# the case where the label fills its parent, which is what the note above was
# reaching for.
static func _box_of(t: Label) -> float:
	var p := t.get_parent() as Control
	if p == null:
		return t.rect_size.x
	var w: float = p.rect_size.x * (t.anchor_right - t.anchor_left) \
			+ t.margin_right - t.margin_left
	return w if w > 1.0 else p.rect_size.x


static func _refit(t: Label) -> void:
	var lo: int = t.get_meta("fit_min")
	var hi: int = t.get_meta("fit_max")
	var box := _box_of(t)
	if box <= 1.0:
		# Called before the first layout, which is where every screen builds its
		# labels. Re-asked on the next resize.
		# CONNECT_ONESHOT is an Object member and this is a static function, so
		# the flag is its own value — 4 — rather than the name. One of GDScript
		# 3.5's sharper edges, and the error it gives names the constant rather
		# than the rule.
		t.connect("resized", UiKitRefit.instance(), "on_resized", [t], 4)
		return
	for size in range(hi, lo - 1, -1):
		var f := font(size)
		if f.get_string_size(t.text).x <= box or size == lo:
			t.add_font_override("font", f)
			return


# ---- the frame -------------------------------------------------------------

# A plate and its edge, stretched over the whole of `rt`.
static func framed(rt: Control, fill: Color, edge_col: Color) -> NinePatchRect:
	var plate := NinePatchRect.new()
	plate.name = "plate"
	plate.texture = Sprites.frame(false)
	plate.patch_margin_left = Sprites.FRAME_SLICE
	plate.patch_margin_right = Sprites.FRAME_SLICE
	plate.patch_margin_top = Sprites.FRAME_SLICE
	plate.patch_margin_bottom = Sprites.FRAME_SLICE
	plate.modulate = fill
	plate.mouse_filter = Control.MOUSE_FILTER_IGNORE
	plate.anchor_right = 1.0
	plate.anchor_bottom = 1.0
	rt.add_child(plate)

	var line := NinePatchRect.new()
	line.name = "edge"
	line.texture = Sprites.frame(true)
	line.patch_margin_left = Sprites.FRAME_SLICE
	line.patch_margin_right = Sprites.FRAME_SLICE
	line.patch_margin_top = Sprites.FRAME_SLICE
	line.patch_margin_bottom = Sprites.FRAME_SLICE
	line.modulate = edge_col
	line.mouse_filter = Control.MOUSE_FILTER_IGNORE
	line.anchor_right = 1.0
	line.anchor_bottom = 1.0
	rt.add_child(line)

	return plate


# THE STROKE ON ITS OWN, with nothing filled behind it.
#
# `framed` is a plate AND its edge, which is what every control wants. The
# aperture wants only the second half: it is a window onto the board, and a fill
# on a screen-space layer would paint over the cube it is framing.
static func stroke(rt: Control, col: Color) -> NinePatchRect:
	var line := NinePatchRect.new()
	line.name = "edge"
	line.texture = Sprites.frame(true)
	line.patch_margin_left = Sprites.FRAME_SLICE
	line.patch_margin_right = Sprites.FRAME_SLICE
	line.patch_margin_top = Sprites.FRAME_SLICE
	line.patch_margin_bottom = Sprites.FRAME_SLICE
	line.modulate = col
	line.mouse_filter = Control.MOUSE_FILTER_IGNORE
	line.anchor_right = 1.0
	line.anchor_bottom = 1.0
	rt.add_child(line)
	return line


# A button is a bracketed label inside a lit frame: [ MENU ]. The brackets are
# part of the design system rather than decoration — they say "this is pressable"
# in the same monospace the rest of the interface speaks.
#
# A PRIMARY IS SOLID AND EVERYTHING ELSE IS A HOLE WITH AN EDGE. There is exactly
# one primary on any screen, and it is the only filled shape in the interface, so
# "the thing to press" needs no arrow, no pulse and no hint text — it is the one
# that is on.
static func bracketed(parent: Node, node_name: String, text: String,
		on_click: FuncRef, size: int = 26, primary: bool = false) -> UiButton:
	return _plated(parent, node_name, "[ " + text + " ]", on_click, size, primary)


# ONE STOP OF A SEGMENTED CONTROL, WHICH IS THE ONE BUTTON WITHOUT BRACKETS.
#
# Three stops in a third of a row is the tightest control in the game, and the
# brackets are four characters of pure overhead in it: FULL is 40.8 units at
# seventeen point and [ FULL ] is 81.6, against a slot of 67.7. Every stop on
# MOTION and LIGHT overflowed, ran under its neighbour and out through the side
# of the panel — and the note that sized the row claimed the slots were "more
# than [ NONE ] needs" on the strength of having measured NONE.
#
# Losing them costs nothing, because the brackets say "pressable" and this
# control has already said it twice: it is a plate, and the plate it is next to
# is lit. A segmented control is the one place in this interface where the frame
# is not the only thing carrying state.
static func segment(parent: Node, node_name: String, text: String,
		on_click: FuncRef, size: int = 26) -> UiButton:
	return _plated(parent, node_name, text, on_click, size, false)


static func _plated(parent: Node, node_name: String, shown: String,
		on_click: FuncRef, size: int, primary: bool) -> UiButton:
	# THE GLOW GOES BEHIND, WHICH MEANS BESIDE. A child draws after its parent,
	# so the glow has to be a sibling ordered first — the same reason the C# has
	# to call SetAsFirstSibling.
	if primary:
		var gr := rect(parent, node_name + "_glow", Vector2.ZERO, Vector2.ONE,
				Vector2(-Sprites.GLOW_REACH, -Sprites.GLOW_REACH),
				Vector2(Sprites.GLOW_REACH, Sprites.GLOW_REACH))
		var g := NinePatchRect.new()
		g.name = "glow"
		g.texture = Sprites.glow()
		var b := Sprites.glow_border()
		g.patch_margin_left = b
		g.patch_margin_right = b
		g.patch_margin_top = b
		g.patch_margin_bottom = b
		var r := Palette.rust()
		g.modulate = Color(r.r, r.g, r.b, 0.70)
		g.mouse_filter = Control.MOUSE_FILTER_IGNORE
		g.anchor_right = 1.0
		g.anchor_bottom = 1.0
		gr.add_child(g)

	var btn := UiButton.new()
	btn.name = node_name
	parent.add_child(btn)
	btn.anchor_to(Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	btn.build_plated(shown, size, primary, on_click)
	return btn


# A PRESSABLE THING THAT IS NOT A BUTTON.
#
# Three framed buttons in a column are three equal offers, and a card that offers
# three equal things has not decided what it is for. The one action the screen
# exists for keeps its plate; everything else becomes a bracketed label with
# nothing behind it — still obviously pressable, because the brackets are what
# say so, and audibly quieter because it has no weight on the page.
static func link(parent: Node, node_name: String, text: String,
		on_click: FuncRef, size: int = 22, tint = null) -> UiButton:
	var btn := UiButton.new()
	btn.name = node_name
	parent.add_child(btn)
	btn.anchor_to(Vector2.ZERO, Vector2.ONE, Vector2.ZERO, Vector2.ZERO)
	btn.build_link("[ " + text + " ]", size, tint if tint != null else Palette.dim(), on_click)
	return btn


# MOVE THE ONE LIT THING, without rebuilding either control.
#
# A screen has exactly one primary and it is meant to be "the thing you came here
# to do" — but on a screen where that CHANGES, a primary baked in at construction
# is a promise the screen cannot keep. The Forge is the case: its coach names the
# next step, and the step is VERIFY until the cube is proved and SAVE afterwards,
# while the plate sat on SAVE from the moment the editor opened. A button lit
# before it can do anything is worse than no primary at all.
#
# Only a control BUILT primary can be lit again later — the glow is a sibling
# object that is created on request — so a screen that intends to move its
# primary builds both that way and switches one off.
static func set_primary(b: UiButton, on: bool) -> void:
	if b == null:
		return
	b.set_primary(on)


static func set_label(b: UiButton, text: String) -> void:
	if b != null:
		b.set_text("[ " + text + " ]")


# A switch, a slider, a field and a scroll. Each is its own file because each is
# forty lines of node-building; the four builders are here so that a screen only
# ever names UiKit.
static func toggle(parent: Node, node_name: String, on: bool, setter: FuncRef,
		anchor_min: Vector2, anchor_max: Vector2,
		off_min: Vector2, off_max: Vector2) -> UiSwitch:
	var s := UiSwitch.new()
	s.name = node_name
	parent.add_child(s)
	s.anchor_to(anchor_min, anchor_max, off_min, off_max)
	s.build(on, setter)
	return s


static func bar(parent: Node, node_name: String, lo: int, hi: int, now: int, setter: FuncRef,
		anchor_min: Vector2, anchor_max: Vector2,
		off_min: Vector2, off_max: Vector2) -> UiBar:
	var b := UiBar.new()
	b.name = node_name
	parent.add_child(b)
	b.anchor_to(anchor_min, anchor_max, off_min, off_max)
	b.build(lo, hi, now, setter)
	return b


static func field(parent: Node, node_name: String, placeholder: String,
		anchor_min: Vector2, anchor_max: Vector2,
		off_min: Vector2, off_max: Vector2) -> UiField:
	var f := UiField.new()
	f.name = node_name
	parent.add_child(f)
	f.anchor_to(anchor_min, anchor_max, off_min, off_max)
	f.build(placeholder)
	return f


# Returns the CONTENT to fill; call end_scroll on the returned scroll's owner
# with its final height when you have finished filling it.
static func scroll(parent: Node, node_name: String,
		anchor_min: Vector2, anchor_max: Vector2,
		off_min: Vector2, off_max: Vector2) -> UiScroll:
	var sc := UiScroll.new()
	sc.name = node_name
	parent.add_child(sc)
	sc.anchor_to(anchor_min, anchor_max, off_min, off_max)
	sc.build()
	return sc


# One of the drawn marks from Glyphs, in the interface.
static func icon(parent: Node, role: String, col: Color, size: float,
		anchor: Vector2, offset: Vector2) -> TextureRect:
	var rt := rect(parent, role, anchor, anchor, Vector2.ZERO, Vector2.ZERO)
	rt.set_size_delta(Vector2(size, size))
	rt.set_anchored_position(offset)
	var img := TextureRect.new()
	img.name = "mark"
	img.texture = Glyphs.get_tex(role)
	img.expand = true
	img.stretch_mode = TextureRect.STRETCH_SCALE
	img.modulate = col
	img.mouse_filter = Control.MOUSE_FILTER_IGNORE
	img.anchor_right = 1.0
	img.anchor_bottom = 1.0
	rt.add_child(img)
	return img


# A hairline. One pixel of rust at low alpha, the only border in the game.
static func rule(parent: Node, y: float, alpha: float = 0.34) -> ColorRect:
	var r := Palette.rust()
	return panel(parent, "rule", Color(r.r, r.g, r.b, alpha),
			Vector2(0, y), Vector2(1, y), Vector2(24, -1), Vector2(-24, 1))


# ---- the title's stage -----------------------------------------------------

# Where the last line of masthead type ends — the cube name's baseline box.
const STAGE_TYPE_BOTTOM := 322.0


# The alpha of the top piece, as a function of units down from the plate's top
# edge. Opaque over the type, then off across the ramp.
static func stage_top_alpha(down: float) -> float:
	var clear := Layout.TITLE_MAST_BOTTOM + Layout.TITLE_SCRIM_RAMP
	if down >= clear:
		return 0.0
	if down <= STAGE_TYPE_BOTTOM:
		return lerp(0.96, 0.92, clamp(down / STAGE_TYPE_BOTTOM, 0.0, 1.0))
	return lerp(0.92, 0.0, clamp((down - STAGE_TYPE_BOTTOM) / (clear - STAGE_TYPE_BOTTOM), 0.0, 1.0))


# The bottom piece, as a function of units UP from the plate's floor.
static func stage_bottom_alpha(up: float) -> float:
	var clear := Layout.TITLE_FOOT_TOP + Layout.TITLE_SCRIM_RAMP
	if up >= clear:
		return 0.0
	if up <= Layout.TITLE_FOOT_TOP:
		return lerp(1.0, 0.90, clamp(up / Layout.TITLE_FOOT_TOP, 0.0, 1.0))
	return lerp(0.90, 0.0, clamp((up - Layout.TITLE_FOOT_TOP) / (clear - Layout.TITLE_FOOT_TOP), 0.0, 1.0))


# THE TITLE SCREEN IS A STAGE, NOT A PANE OF GLASS.
#
# A flat scrim over the whole screen darkens the one thing the screen is actually
# about — the cube turning behind it. So the chrome occupies a band at each end
# and THE MIDDLE IS LEFT COMPLETELY ALONE: nothing at all between the player and
# the object. The gradient only darkens where words have to sit on top of it.
#
# IT IS TWO PIECES ANCHORED TO TWO EDGES. It was one gradient stretched over the
# whole plate, with its stops written as FRACTIONS. Everything it has to line up
# with is a fixed offset from an edge — the masthead is 346 units down whatever
# the plate is, the controls are 452 units up from the floor — so on any plate
# that is not the reference 1127.5 the ramp drifts away from the two things it
# exists to sit between. On a 20:9 phone the plate is a hundred units taller and
# every stop lands late.
#
# And the stops themselves were tuned against a three-line PITCH that has since
# been deleted from the screen, which left the clear stripe a tenth of the plate
# tall with a cube a fifth of the plate drawn under it. That is the whole reason
# the machine reads as a grey smudge on the front screen: it is not dark, it is
# under a scrim shaped for a paragraph that is not there.
static func stage(rt: Control) -> void:
	var top_h := Layout.TITLE_MAST_BOTTOM + Layout.TITLE_SCRIM_RAMP
	var bot_h := Layout.TITLE_FOOT_TOP + Layout.TITLE_SCRIM_RAMP

	# AND THE GROUND BETWEEN THEM IS THE BOARD, WHICH IS THE WHOLE POINT.
	#
	# The C# fills this plate with an opaque Palette.Void and then lays the two
	# ramps over it — so the attract cube, which GameDirector loads, poses,
	# frames and turns on every visit to the title, is drawn behind a sheet of
	# black and has never once been seen. The screen's own note calls that cube
	# "the best argument the title has" and explains that the ninety units of air
	# under the masthead are deliberately left empty for it; what is actually
	# there is a hole.
	#
	# The ramps are what protect the type, and they already do it alone: the top
	# runs at 0.96 down the masthead and off to nothing by 376, the bottom holds
	# 0.96 across the buttons and clears by 482 up. Between them is 270 units of
	# window, which is exactly the band Layout.title_rect frames the cube into.
	# Nothing needs to be added to keep a word readable; the sheet only had to
	# stop being there.
	_stage_piece(rt, "stageTop", true, top_h)
	_stage_piece(rt, "stageBottom", false, bot_h)




static func _stage_piece(parent: Control, node_name: String, top: bool, height: float) -> void:
	var helper = UiKitRefit.instance()
	var fr := funcref(helper, "stage_top" if top else "stage_bottom")
	helper.stage_height = height
	var tex := Sprites.ramp(node_name + str(int(height)), top, fr)

	var rt: UiRect
	if top:
		rt = rect(parent, node_name, Vector2(0, 1), Vector2(1, 1), Vector2(0, -height), Vector2.ZERO)
	else:
		rt = rect(parent, node_name, Vector2(0, 0), Vector2(1, 0), Vector2.ZERO, Vector2(0, height))

	var img := TextureRect.new()
	img.name = "ramp"
	img.texture = tex
	img.expand = true
	img.stretch_mode = TextureRect.STRETCH_SCALE
	img.mouse_filter = Control.MOUSE_FILTER_IGNORE
	img.anchor_right = 1.0
	img.anchor_bottom = 1.0
	rt.add_child(img)


# DID SOMEBODY JUST TOUCH THE SCREEN, ANYWHERE.
#
# Not a button and not a hit test — a screen with nothing on it to press still
# has to be dismissible, and the chapter word is the one screen in the game that
# draws no way out. The mouse is here for the desktop; on a phone only the first
# branch ever answers.
static func any_press(event: InputEvent) -> bool:
	if event is InputEventScreenTouch:
		return event.pressed
	if event is InputEventMouseButton:
		return event.pressed and event.button_index == BUTTON_LEFT
	return false
