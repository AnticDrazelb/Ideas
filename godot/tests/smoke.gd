extends SceneTree
# BOOT THE WHOLE GAME, HEADLESS, AND SEE WHETHER IT COMES UP.
#
# tests/run.gd proves the rules and tests/compile.gd proves every file parses.
# Neither of them has ever CONSTRUCTED anything: the camera, the nine canvases,
# every screen in the game and the whole event wiring are built at runtime, and a
# null there is invisible to both. This runs Main.tscn for a few hundred frames
# and asserts the machine is standing afterwards.
#
# The headless build has no rasterizer, so nothing is drawn — which is exactly
# what this is for. Everything that goes wrong here is a wiring fault, and a
# wiring fault is the only class of bug the other two harnesses cannot see.

const FRAMES := 240

var _n := 0
var _root: Node = null


func _init() -> void:
	var packed = load("res://game/Main.tscn")
	if packed == null:
		printerr("smoke: Main.tscn will not load")
		quit(1)
		return
	_root = packed.instance()
	get_root().add_child(_root)


func _idle(_dt: float) -> bool:
	_n += 1
	if _n == 2:
		_check_up()
	if _n >= FRAMES:
		_report()
		quit(1 if _bad > 0 else 0)
		return true
	return false


func _fail(what: String) -> void:
	printerr("smoke: " + what)
	_bad += 1


var _bad := 0


func _check_up() -> void:
	var boot = _root
	if boot.get("director") == null:
		_fail("no director")
		return
	var d = boot.director
	for f in ["s", "view", "rig", "hud", "filter", "glow", "sfx"]:
		if d.get(f) == null:
			_fail("director." + f + " is null")

	# the nine canvases, by the names the kit gives them
	for nm in ["HUD", "Screens"]:
		if not _has_layer(nm):
			_fail("no canvas named " + nm)

	# every screen the director builds, and the two the Forge adds
	for id in ["title", "vaults", "manual", "plate", "chapter", "pause",
			"calibrate", "access", "win", "forge", "forgeEdit"]:
		if not Screens.i()._layers.has(id):
			_fail("no screen named " + id)


func _has_layer(nm: String) -> bool:
	for c in get_root().get_children():
		if _find_named(c, nm):
			return true
	return false


func _find_named(n: Node, nm: String) -> bool:
	if n.name == nm and n is CanvasLayer:
		return true
	for c in n.get_children():
		if _find_named(c, nm):
			return true
	return false


func _report() -> void:
	if _bad == 0:
		print("smoke: the machine came up (%d frames)" % _n)
	else:
		printerr("smoke: %d faults" % _bad)
