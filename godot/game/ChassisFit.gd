extends UiRect
# FITTED WHEN THE PANEL'S SIZE IS KNOWN, WHICH IS NEVER AT CONSTRUCTION.
#
# The corners are placed at a fixed size and the four sides take up whatever is
# left, so the pieces have to be laid out against a real rect — and they have to
# be laid out again when it changes: a rotation, a fold, a desktop window dragged
# wider. The anchors do most of the work on their own; this exists for the sizes
# that anchors cannot express and to hold the guard for a panel too small to take
# the corners at all.

var _built := false
var _fitted := Vector2.ZERO
var _piece := []
var _art_h := 0.0
var _k := 1.0

# ---- the case, behaving like one ------------------------------------------
#
# The metal corrodes across the ladder and its inner edge takes light from the
# two events that are about the machine rather than about the board. Both are
# written here because both are one property of twelve pieces and one rim.

# How far along the ladder, 0..1. See Chassis.set_band.
var _age := 0.0
var _age_shown := -1.0

# Where the case is clean and where it has been in the ground for thirty years.
# The first vault lifts the photograph very slightly and cools it; the last takes
# it down and swings it into the rust the rest of the interface is drawn in.
const AGE_NEW := Color(1.06, 1.05, 1.04)
const AGE_OLD := Color(0.84, 0.68, 0.58)

# The twelve pieces and the rim hang off this rather than off the panel, so that
# turning the case is one rotation of one node instead of twelve re-placements.
# In portrait it is the panel; turned, it is the panel transposed and rotated.
var _frame: Control
var _turned := false

var _rim: NinePatchRect
var _flash := 0.0
var _flash_col := Color(1, 1, 1, 1)
var _clock := 0.0


func setup() -> void:
	_piece.resize(12)
	_frame = Control.new()
	_frame.name = "frame"
	_frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_frame)
	Chassis.attach(self)
	set_process(true)


func set_age(a: float) -> void:
	_age = clamp(a, 0.0, 1.0)
	_repaint_age()


func _repaint_age() -> void:
	if is_equal_approx(_age, _age_shown):
		return
	_age_shown = _age
	var tint := AGE_NEW.linear_interpolate(AGE_OLD, _age)
	for p in _piece:
		if p != null:
			p.modulate = tint


func flash(col: Color, amount: float) -> void:
	if amount <= 0.001:
		return
	_flash_col = col
	_flash = max(_flash, clamp(amount, 0.0, 1.0))


func set_clock(k: float) -> void:
	_clock = clamp(k, 0.0, 1.0)


# THE RIM IS ADDITIVE, so it can only ever put light ON the metal. The same
# argument the glass is built on: a layer that can darken is a layer that can
# take a control below its contrast floor, and this one draws over the housing
# every screen in the game is mounted in.
func step(dt: float) -> void:
	if _rim == null:
		return
	_flash = max(0.0, _flash - dt * 2.2)
	var a: float = _flash * 0.85
	if _clock > 0.001:
		# steady while there is time, and beating once a second over the last three
		var beat: float = 1.0 if _clock > 0.6 else (0.55 + 0.45 * sin(_clock * 40.0))
		a = max(a, 0.26 * _clock * beat)
	var col: Color = _flash_col if _flash > 0.001 else Palette.rust()
	_rim.modulate = Color(col.r, col.g, col.b, a)
	_rim.visible = a > 0.004


func _process(_dt: float) -> void:
	var w := rect_size.x
	var h := rect_size.y
	var turn := Chassis.turned()
	# The frame is the case's own rectangle: the panel in portrait, and the panel
	# transposed in landscape, because a quarter turn swaps what the twelve pieces
	# think their width and height are.
	var fw: float = h if turn else w
	var fh: float = w if turn else h
	var c := Chassis.corner()
	if fw < c * 2.0 + 8.0 or fh < c * 2.0 + 8.0:
		return
	if abs(w - _fitted.x) < 1.0 and abs(h - _fitted.y) < 1.0 \
			and turn == _turned and is_equal_approx(_k, Chassis.scale_now()):
		return
	_fitted = Vector2(w, h)
	_turned = turn

	if not _built:
		_compose()
		_build_rim()
		_built = true
	_turn_frame(Vector2(fw, fh), Vector2(w, h), turn)
	_stretch()
	_fit_rim()
	_age_shown = -1.0
	_repaint_age()


# A QUARTER TURN OF ONE NODE.
#
# Everything inside the frame is placed against a portrait rectangle and knows
# nothing about which way up the device is — the corner arms anchor to their own
# corner, the four sides stretch between them, the rim hangs off the opening. So
# the turn is not twelve decisions, it is this: give the frame the panel's height
# for its width, spin it ninety degrees clockwise about its own centre, and put
# that centre back where the panel's is.
#
# Clockwise, so the brow lands on the right. See Chassis.turned.
func _turn_frame(size: Vector2, panel: Vector2, turn: bool) -> void:
	if _frame == null:
		return
	_frame.rect_rotation = 90.0 if turn else 0.0
	_frame.rect_size = size
	_frame.rect_pivot_offset = size * 0.5
	_frame.rect_position = panel * 0.5 - size * 0.5


# THE TWELVE WINDOWS ONTO THE CASE, once.
#
# Each is a sub-rectangle of the ONE texture, which is why the seams between them
# cannot show: a region cut from the middle of a texture still filters against
# its neighbours' texels, so two pieces that are adjacent in the art and adjacent
# on the screen are continuous without a single pixel of overlap to hide the
# join.
func _compose() -> void:
	var tex = Chassis.texture()
	if tex == null:
		return

	var s := Chassis.spec()
	_art_h = s.art_h
	_k = Chassis.scale_now()
	var aw := s.arm_w
	var ah := s.arm_h
	var sw := s.side_w
	var bh := s.band_h
	var cc := s.corner
	var w := float(s.art_w)
	var h := float(s.art_h)
	var kk := Chassis.scale_now()

	# top left, and then the same L three more times. The vertical arm runs the
	# full depth of the corner; the horizontal arm takes the rest of the top, so
	# between them they cover every lit texel of the corner and none of the board.
	_place(tex, 0, "tl_v", 0.0, 0.0, aw, cc, 0, 1, 0.0)
	_place(tex, 1, "tl_h", aw, 0.0, cc - aw, ah, 0, 1, aw * kk)
	_place(tex, 2, "tr_v", w - aw, 0.0, aw, cc, 1, 1, 0.0)
	_place(tex, 3, "tr_h", w - cc, 0.0, cc - aw, ah, 1, 1, -aw * kk)
	_place(tex, 4, "bl_v", 0.0, h - cc, aw, cc, 0, 0, 0.0)
	_place(tex, 5, "bl_h", aw, h - ah, cc - aw, ah, 0, 0, aw * kk)
	_place(tex, 6, "br_v", w - aw, h - cc, aw, cc, 1, 0, 0.0)
	_place(tex, 7, "br_h", w - cc, h - ah, cc - aw, ah, 1, 0, -aw * kk)

	# and the four sides, which are the only pieces that stretch
	_place(tex, 8, "bandT", cc, 0.0, w - cc * 2.0, bh, 0, 1, 0.0)
	_place(tex, 9, "bandB", cc, h - bh, w - cc * 2.0, bh, 0, 0, 0.0)
	_place(tex, 10, "railL", 0.0, cc, sw, h - cc * 2.0, 0, 0, 0.0)
	_place(tex, 11, "railR", w - sw, cc, sw, h - cc * 2.0, 1, 0, 0.0)


# One window. `ax` and `ay` say which corner of the panel it hangs off, and the
# piece is pivoted on that same corner so `ox` is simply how far along that edge
# it starts.
#
# THE SOURCE RECTANGLE IS IN THE ART'S PIXELS WITH Y MEASURED DOWN, because that
# is how the picture was measured. The C# has to flip it because Unity counts the
# other way; Godot counts down too, so the flip the C# performs is exactly the
# line that is absent here.
func _place(tex, i: int, piece_name: String, sx: float, sy: float, sw: float, sh: float,
		ax: int, ay: int, ox: float) -> void:
	var rt: UiRect = Chassis.kit().rect(_frame, piece_name, Vector2(ax, ay), Vector2(ax, ay), Vector2.ZERO, Vector2.ZERO)
	rt.set_pivot(Vector2(ax, ay))
	rt.set_size_delta(Vector2(sw * _k, sh * _k))
	rt.set_anchored_position(Vector2(ox, 0.0))

	var atlas := AtlasTexture.new()
	atlas.atlas = tex
	atlas.region = Rect2(sx, sy, sw, sh)
	atlas.filter_clip = true

	var img := TextureRect.new()
	img.name = "art"
	img.texture = atlas
	img.expand = true
	img.stretch_mode = TextureRect.STRETCH_SCALE
	img.mouse_filter = Control.MOUSE_FILTER_IGNORE
	img.anchor_right = 1.0
	img.anchor_bottom = 1.0
	rt.add_child(img)
	_piece[i] = rt


# THE INNER EDGE, WHICH IS THE ONLY PART OF THE CASE THAT MOVES.
#
# One rounded stroke laid just inside the glass, drawn additively so it cannot
# darken anything, at zero until something asks for it. It is the same nine-slice
# every framed control in the game uses, at the size of the display's own
# opening — so when the machine reacts, what lights up is unmistakably the
# housing rather than a rectangle somebody drew on top of it.
func _build_rim() -> void:
	_rim = NinePatchRect.new()
	_rim.name = "rim"
	# THE GLOW, NOT THE STROKE. A hairline around the opening is an outline of the
	# display; what this is for is light coming OFF it and falling on the metal,
	# which is the difference between the case being adjacent to the event and the
	# case being part of it. Same soft nine-slice the primary button is lit with.
	_rim.texture = Sprites.glow()
	var b := Sprites.glow_border()
	_rim.patch_margin_left = b
	_rim.patch_margin_right = b
	_rim.patch_margin_top = b
	_rim.patch_margin_bottom = b
	# THE BORDER ONLY. The glow's middle patch is solid, and stretched across the
	# whole opening it floods the board with light instead of edging it — the nine
	# slice is being used for its falloff, not for its fill.
	_rim.draw_center = false
	_rim.material = Glass.additive()
	_rim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_rim.modulate = Color(1, 1, 1, 0)
	_rim.visible = false
	_frame.add_child(_rim)


func _fit_rim() -> void:
	if _rim == null:
		return
	_rim.anchor_left = 0.0
	_rim.anchor_top = 0.0
	_rim.anchor_right = 1.0
	_rim.anchor_bottom = 1.0
	# Sized to the opening and then pushed OUTWARD, so the falloff lands on the
	# bezel rather than on the board.
	#
	# THE CASE'S OWN INSETS, NOT THE SCREEN'S. Inside the frame nothing has been
	# turned yet: the brow is still at the top of this rectangle whatever the
	# device is doing. Chassis.inset_* answer for the SCREEN, which is what every
	# other caller wants and the one thing that would put this stroke on its side.
	var reach := Sprites.GLOW_REACH
	var sp := Chassis.spec()
	var k := Chassis.scale_now()
	_rim.margin_left = sp.left * k - reach
	_rim.margin_top = sp.top * k - reach
	_rim.margin_right = -sp.right * k + reach
	_rim.margin_bottom = -sp.bottom * k + reach


# The four sides take whatever the corners left. Nothing else moves: the eight
# arms are anchored to their own corner at a fixed size, so a resize is four
# numbers.
func _stretch() -> void:
	if _piece[8] == null:
		return
	var c := Chassis.corner()
	for i in [8, 9]:
		var p: UiRect = _piece[i]
		p.anchor_left = 0.0
		p.anchor_right = 1.0
		p.margin_left = c
		p.margin_right = -c
	for i in [10, 11]:
		var p2: UiRect = _piece[i]
		p2.anchor_top = 0.0
		p2.anchor_bottom = 1.0
		p2.margin_top = c
		p2.margin_bottom = -c
