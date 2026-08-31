class_name Shaders
# DID THE SHADERS ACTUALLY SHIP?
#
# Every shader in the Unity project is reached by Shader.Find, and nothing in it
# references any of them — the scene is empty on purpose, there are no materials
# and no prefabs. That is the property the whole project is built on, and this is
# what it costs there: Unity decides what to include in a player by REFERENCE, so
# five shaders with zero references are five shaders that do not ship. In the
# editor every asset is loaded, so Shader.Find works and there is no symptom at
# all. In a player it returns null, every material is built on nothing, and the
# game runs perfectly while drawing nothing. Splash, then black.
#
# GODOT DOES NOT HAVE THAT FAILURE, and it is worth saying why rather than
# quietly dropping the file. A .shader here is a resource loaded by path, and an
# exported project packs res:// wholesale — there is no reference-tracing step
# to fall through. What CAN still happen is a shader that fails to COMPILE on a
# device whose driver rejects something the desktop accepted, which produces
# exactly the same symptom: a game that runs perfectly and draws nothing.
#
# So the check stays, pointed at the failure this engine actually has, and so
# does the rule behind it: A BLACK SCREEN MUST NEVER BE THE ERROR MESSAGE.
#
# AND THE COMPLAINT IS DRAWN IN THE ENGINE'S OWN DEFAULT MATERIAL, deliberately.
# Anything of ours is exactly what is being reported missing, so the message has
# to be made of something that cannot itself be the missing piece.

# The six, in the order they matter. `cell` first: without it there is no game.
const REQUIRED := [
	"res://shaders/cell.shader",
	"res://shaders/wire.shader",
	"res://shaders/glyph.shader",
	"res://shaders/fx.shader",
	# the field behind the machine. It is not load-bearing for the game being
	# playable, and it is demanded anyway: a player whose sky did not ship gets a
	# black rectangle behind glass and no way to know that is not how it looks.
	"res://shaders/sky.shader",
	"res://shaders/bloom_knee.shader",
	"res://shaders/bloom_blur.shader",
	"res://shaders/bloom_add.shader",
	# `filter` is NOT here, for the same reason it is not in the C# list.
	# Brightness and contrast are an overlay above every canvas rather than a
	# camera pass, and demanding a shader at boot that the default settings never
	# instantiate would be demanding something nothing uses.
]

const _CACHE := {}


# The shader at a path, loaded once. Null only if it is genuinely not there.
static func get_shader(path: String):
	if _CACHE.has(path):
		return _CACHE[path]
	var s = load(path)
	_CACHE[path] = s
	return s


# A MATERIAL, WITH ITS SHADER ALREADY ON IT. Every material in the game is made
# here so that a missing shader is one message rather than nine silent nulls.
static func material(path: String) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = get_shader(path)
	return m


# THE PLATE'S OWN COPY OF THE CELL SHADER, with one line changed.
#
# A plate is cut clean through the lattice and is legible from every face in
# every world — that is the rule the whole mechanic is built on, not a rendering
# convenience, and it is why every fold is legal while you stand on one. In
# ShaderLab it is a material property: the plate material sets _ZTest to Always
# and the same shader obeys it.
#
# Godot's depth test is a render_mode, which is compiled in, so the same
# statement needs a second shader. Making it from the first's source rather than
# writing it out is what keeps there being ONE cell shader: the arithmetic cannot
# drift between the plate and the lattice it is cut through, because there is
# only one copy of it.
static func cell_always() -> Shader:
	if _CACHE.has("cell_always"):
		return _CACHE["cell_always"]
	var base: Shader = get_shader("res://shaders/cell.shader")
	var s := Shader.new()
	if base != null:
		s.code = base.code.replace(
				"render_mode unshaded, cull_disabled, depth_draw_opaque;",
				"render_mode unshaded, cull_disabled, depth_draw_opaque, depth_test_disable;")
	_CACHE["cell_always"] = s
	return s


# THE HOLE IS OPAQUE AND THE LIGHT AROUND IT ADDS, and they are the same shader.
#
# The marker is a black disc that REMOVES the board under it — not a dark spot
# ON the board, an absence OF board — with a photon ring and a lensing halo that
# are light and must add. One statement, two blends, and Godot's blend mode is
# compiled in, so the additive one is made from the mixing one the same way the
# plate's is made from the lattice's.
static func glyph_add() -> Shader:
	if _CACHE.has("glyph_add"):
		return _CACHE["glyph_add"]
	var base: Shader = get_shader("res://shaders/glyph.shader")
	var s := Shader.new()
	if base != null:
		s.code = base.code.replace(
				"render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disable, blend_mix;",
				"render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disable, blend_add;")
	_CACHE["glyph_add"] = s
	return s


# Returns "" when every shader is there, or a comma-separated list of the ones
# that are not.
static func missing() -> String:
	var out := ""
	for path in REQUIRED:
		if get_shader(path) == null:
			out = path if out == "" else out + ", " + path
	return out


# Say it on the screen, at a size that survives a phone, in the one material that
# cannot itself be the thing that is missing.
static func complain(into: Node, what: String) -> void:
	push_error("[Singularity] shaders missing from this build: " + what
			+ ". Nothing will draw.")

	var layer := CanvasLayer.new()
	layer.name = "Shaders missing"
	layer.layer = 128
	into.add_child(layer)

	var ground := ColorRect.new()
	ground.color = Color(0, 0, 0, 1)
	ground.anchor_right = 1.0
	ground.anchor_bottom = 1.0
	layer.add_child(ground)

	var t := Label.new()
	t.text = "SHADERS MISSING FROM THIS BUILD\n\n" + what.replace(", ", "\n") \
			+ "\n\nNothing can be drawn."
	t.align = Label.ALIGN_CENTER
	t.valign = Label.VALIGN_CENTER
	t.autowrap = true
	t.add_color_override("font_color", Color(0.90, 0.86, 0.82))
	t.anchor_right = 1.0
	t.anchor_bottom = 1.0
	t.margin_left = 48
	t.margin_top = 48
	t.margin_right = -48
	t.margin_bottom = -48
	layer.add_child(t)
