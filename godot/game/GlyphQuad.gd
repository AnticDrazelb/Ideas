class_name GlyphQuad
extends MeshInstance
# ONE DRAWN MARK, FACING THE CAMERA.
#
# The C# uses a SpriteRenderer, which is a quad with a texture, a tint, a sorting
# order and no depth test. Godot's Sprite3D is the same object and cannot be
# given a custom shader with its own texture bound, so this is the quad written
# out: it is four vertices, a material, and the two properties every marker in
# the game sets — which colour it is and how far up the stack it draws.
#
# THE ORDER MATTERS AND IS NOT DEPTH. A node, a lock, the core and the player are
# all attached to cells, and the projection has already decided which of them is
# visible; the stack that remains is the one the C# spells as sortingOrder — the
# halo under the hole, the hole over the board, the ring over the hole, the
# antipode over all of it because it is seen THROUGH the hole.

var mat: ShaderMaterial
var _sprite_col := Color(1, 1, 1, 1)


func build(role: String, col: Color, size: float, order: int, additive: bool = false) -> void:
	var quad := QuadMesh.new()
	quad.size = Vector2(1, 1)
	mesh = quad

	mat = ShaderMaterial.new()
	mat.shader = Shaders.glyph_add() if additive else Shaders.get_shader("res://shaders/glyph.shader")
	mat.set_shader_param("tex", Glyphs.get_tex(role))
	mat.set_shader_param("tint", col)
	mat.render_priority = order
	material_override = mat

	_sprite_col = col
	scale = Vector3(size, size, size)
	cast_shadow = GeometryInstance.SHADOW_CASTING_SETTING_OFF


func set_role(role: String) -> void:
	mat.set_shader_param("tex", Glyphs.get_tex(role))


func set_colour(col: Color) -> void:
	if _sprite_col == col:
		return
	_sprite_col = col
	mat.set_shader_param("tint", col)


func colour() -> Color:
	return _sprite_col


# FACE THE CAMERA, EXACTLY. Not a billboard flag: the camera is orthographic and
# axis-aligned and the mark has to sit square to it to the pixel, which is the
# camera's own basis and nothing softer.
func face(cam: Camera) -> void:
	var t := global_transform
	t.basis = cam.global_transform.basis.scaled(scale)
	global_transform = t
