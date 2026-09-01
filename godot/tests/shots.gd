extends SceneTree
# A PICTURE OF EVERY SCREEN, AT ANY SHAPE.
#
# The audit says whether a word lands on a control. It cannot say whether the
# thing looks like a machine, and the whole argument for the case is that it
# does — so there has to be a way to LOOK at it that does not involve a phone.
#
# One process, one shape, one png per screen, into user://shots. It is the same
# boot the audit performs and for the same reason: ask for the window, wait until
# it has stopped arguing, and only then build anything.
#
#   UI_SIZE=1280x720 SHOT_DIR=turned godot --path godot -s tests/shots.gd

const OUT := "user://shots/"
const SCREENS := ["title", "vaults", "manual", "plate", "chapter", "pause",
		"calibrate", "access", "win", "forge", "forgeEdit"]

var _root: Node = null
var _step := 0
var _wait := 0
var _seen := Vector2.ZERO
var _still := 0
var _want := Vector2.ZERO
var _dir := ""
var _shots := 0


func _init() -> void:
	PerfWatch.pin()
	Directory.new().remove(Store.FILE)
	_dir = OS.get_environment("SHOT_DIR")
	var spec := OS.get_environment("UI_SIZE")
	var bits := spec.to_lower().split("x")
	if bits.size() == 2 and bits[0].is_valid_integer() and bits[1].is_valid_integer():
		_want = Vector2(int(bits[0]), int(bits[1]))
		OS.set_window_size(_want)
	if _dir == "":
		_dir = "%dx%d" % [_want.x, _want.y] if _want.x > 0.0 else "shots"
	var d := Directory.new()
	d.make_dir_recursive(OUT + _dir)


func _settled() -> bool:
	var now := Layout.screen_size()
	if now == _seen:
		_still += 1
	else:
		_seen = now
		_still = 0
	return _still >= 8


func _idle(_dt: float) -> bool:
	if _wait > 0:
		_wait -= 1
		return false
	_run()
	return false


func _run() -> void:
	if _step == 0:
		if not _settled():
			return
		_root = load("res://game/Main.tscn").instance()
		get_root().add_child(_root)
		_step += 1
		_wait = 80
		return
	if _step == 1:
		Store.data().reached = 40
		Store.data().runs = 1
		Store.set_best(1, 2)
		Store.set_par(1, 1)
		Store.save()
		_step += 1
		_wait = 10
		return

	var idx := _step - 2
	if idx < SCREENS.size():
		_show(SCREENS[idx])
		_step += 1
		_wait = 24
		if _wait == 24:
			call_deferred("_later", SCREENS[idx])
		return
	# THE BOARD WITH A CUBE ON IT, not the attract pose. Every other frame here is
	# a screen; this one is the game, and a picture of the play window with
	# nothing in it says nothing about whether the window is the right shape.
	if idx == SCREENS.size():
		Screens.show(null)
		_root.director.play(Vaults.resume(1))
		_step += 1
		_wait = 60
		call_deferred("_later", "board")
		return

	print("%d frames into user://shots/%s at %s" % [_shots, _dir, str(_seen)])
	quit(0)


# The capture has to happen after the wait, not with it: a screen that has just
# been asked for has not been drawn yet, and a png of the frame before it is a
# picture of the screen you were on.
func _later(nm: String) -> void:
	yield(self, "idle_frame")
	for _i in range(20):
		yield(self, "idle_frame")
	var img: Image = get_root().get_texture().get_data()
	img.flip_y()
	img.save_png(OUT + _dir + "/" + nm + ".png")
	_shots += 1


func _show(id: String) -> void:
	var sc = Screens.i()
	match id:
		"win":
			Screens.show_win(_root.director.s, _root.director)
		"pause":
			Screens.show_pause(_root.director)
		"vaults":
			sc._open_vaults()
		"forge":
			ForgeScreens.open_shelf()
		"forgeEdit":
			ForgeScreens.open_editor(Forge.resume())
		"chapter":
			Screens.show_chapter_word("test.", null)
		"title":
			Screens.show_title()
		_:
			Screens.show(id)
