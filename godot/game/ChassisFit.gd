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


func setup() -> void:
	_piece.resize(12)
	set_process(true)


func _process(_dt: float) -> void:
	var w := rect_size.x
	var h := rect_size.y
	var c := Chassis.corner()
	if w < c * 2.0 + 8.0 or h < c * 2.0 + 8.0:
		return
	if abs(w - _fitted.x) < 1.0 and abs(h - _fitted.y) < 1.0:
		return
	_fitted = Vector2(w, h)

	if not _built:
		_compose()
		_built = true
	_stretch()


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
	_k = s.scale
	var aw := s.arm_w
	var ah := s.arm_h
	var sw := s.side_w
	var bh := s.band_h
	var cc := s.corner
	var w := float(s.art_w)
	var h := float(s.art_h)
	var kk := s.scale

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
	var rt: UiRect = Chassis.kit().rect(self, piece_name, Vector2(ax, ay), Vector2(ax, ay), Vector2.ZERO, Vector2.ZERO)
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
