class_name UiScroll
extends UiRect
# A SCREEN THAT DOES NOT FIT IS NOT A LAYOUT PROBLEM, IT IS A SCROLL.
#
# The manual was tuned until it fitted a 1280-tall canvas, which is a number, not
# a phone: a short device or a large system font puts the last two entries under
# the button again, and the only honest answer to "how much text is there" is "as
# much as there is". Everything below goes in a clipped viewport with a content
# rect that is however tall it turns out to be.
#
# AND IT SAYS SO WHEN THERE IS MORE.
#
# A page that runs off the bottom of the glass with nothing at the bottom edge is
# a page that ends there, as far as anybody can tell: there is no bar, no shadow
# and no overscroll bounce — the movement is clamped on purpose, because a
# document that rubber-bands reads as a toy — so the ONLY evidence that the
# manual continues was already knowing it did. Half the objects on that page are
# below the fold.
#
# One arrow, at the foot, pointing the way the content goes, fading out over the
# last stretch of travel so it is gone by the time it would be lying. It is the
# game's own drawn mark rather than a font glyph, turned through half a circle,
# because the mono has no arrow and a "v" is a letter.

var scroll: ScrollContainer
var content: UiRect
var _arrow: TextureRect
var _base := Color(1, 1, 1, 1)


func build() -> UiRect:
	mouse_filter = Control.MOUSE_FILTER_STOP

	scroll = ScrollContainer.new()
	scroll.name = "view"
	scroll.scroll_horizontal_enabled = false
	scroll.scroll_vertical_enabled = true
	scroll.anchor_right = 1.0
	scroll.anchor_bottom = 1.0
	var flat := StyleBoxEmpty.new()
	scroll.add_stylebox_override("bg", flat)
	add_child(scroll)

	content = UiRect.new()
	content.name = "content"
	content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(content)

	var hint: UiRect = UiButton.kit().rect(self, "more", Vector2(0.5, 0), Vector2(0.5, 0), Vector2.ZERO, Vector2.ZERO)
	hint.set_size_delta(Vector2(34, 34))
	hint.set_anchored_position(Vector2(0, 26))
	_arrow = TextureRect.new()
	_arrow.name = "mark"
	_arrow.texture = Glyphs.get_tex("arrow")
	_arrow.expand = true
	_arrow.stretch_mode = TextureRect.STRETCH_SCALE
	_arrow.modulate = Palette.rust()
	_arrow.rect_rotation = 180.0
	_arrow.rect_pivot_offset = Vector2(17, 17)
	_arrow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_arrow.anchor_right = 1.0
	_arrow.anchor_bottom = 1.0
	hint.add_child(_arrow)
	_base = _arrow.modulate

	set_process(true)
	return content


# Tell a scroll how tall its content turned out to be.
func end_scroll(height: float) -> void:
	content.rect_min_size = Vector2(0, height)


# Fades the "there is more below" arrow out as the last of the travel is used up,
# and hides it outright on a page that fits.
func _process(_dt: float) -> void:
	if scroll == null or _arrow == null:
		return
	var over: float = content.rect_min_size.y - scroll.rect_size.y
	var left: float = 0.0
	if over > 1.0:
		left = max(0.0, over - scroll.scroll_vertical)
	# Full for the first page of travel and out over the last sixty units, so the
	# arrow disappears as the end arrives rather than snapping off at it.
	var a: float = clamp(left / 60.0, 0.0, 1.0)
	var c := Color(_base.r, _base.g, _base.b, _base.a * a)
	if _arrow.modulate != c:
		_arrow.modulate = c
	if _arrow.visible != (a > 0.01):
		_arrow.visible = a > 0.01
