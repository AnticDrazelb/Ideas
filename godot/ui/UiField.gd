class_name UiField
extends UiRect
# THE ONLY FREE TEXT IN THE GAME: a cube's name, and a pasted share code. Both
# are folded to the character set the interface already speaks before they go
# anywhere, which is a house-style decision that also means no authored string
# can ever be markup.

var line: LineEdit


func build(placeholder: String) -> void:
	mouse_filter = Control.MOUSE_FILTER_STOP
	UiButton.kit().framed(self, Palette.PANEL, UiButton.kit().edge())

	line = LineEdit.new()
	line.name = "line"
	line.placeholder_text = placeholder
	line.add_font_override("font", UiButton.kit().font(22))
	line.add_color_override("font_color", Palette.INK)
	line.add_color_override("font_color_selected", Palette.VOID)
	line.add_color_override("cursor_color", Palette.rust())
	line.add_color_override("selection_color", Palette.rust())
	# The frame is drawn by UiButton.kit().framed; the field must not draw a second one
	# over it, which is what a default LineEdit does.
	var flat := StyleBoxEmpty.new()
	line.add_stylebox_override("normal", flat)
	line.add_stylebox_override("focus", flat)
	line.add_stylebox_override("read_only", flat)
	line.anchor_right = 1.0
	line.anchor_bottom = 1.0
	line.margin_left = 14
	line.margin_right = -14
	add_child(line)


func text() -> String:
	return line.text if line != null else ""


func set_text(t: String) -> void:
	if line != null:
		line.text = t
