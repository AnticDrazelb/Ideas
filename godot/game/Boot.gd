extends Node

# THE ONE SCENE, AND EVERYTHING ELSE IS BUILT IN CODE.
#
# The original has no prefabs either: a single MonoBehaviour wakes up and
# constructs the camera, the board, nine canvases and every screen from script,
# because a layout that is authored in an inspector cannot be read, diffed or
# reasoned about — and this game's interface is 3000 lines of measured offsets
# that all have to agree with each other.
#
# So Main.tscn is one empty node with this on it. What it does that the C# does
# not have to is set the 3D environment up to let a CanvasLayer draw BEHIND the
# board: 2D always draws over 3D within a viewport, so the deep sky at layer
# -100 is only visible when the world environment is told to use the canvas as
# its background.

var director = null


func _ready() -> void:
	_sky_behind_the_board()
	director = load("res://game/GameDirector.gd").new()
	director.name = "GameDirector"
	add_child(director)


# THE SKY IS A CANVAS LAYER, AND A CANVAS LAYER IS IN FRONT.
#
# Godot draws every 2D layer over the 3D pass, whatever its order, so the sky's
# layer at -100 would sit on top of the cube instead of behind it. BG_CANVAS is
# the one setting that changes that: the viewport's background becomes the canvas
# up to `background_canvas_max_layer`, and everything above that layer draws over
# the 3D as usual. -1 is exactly the boundary — the sky is below it, the board's
# frame and the readout are above.
func _sky_behind_the_board() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_CANVAS
	env.background_canvas_max_layer = -1
	# No ambient light of its own: every colour on the board is written by the
	# cell shader, unlit, exactly as the original's materials are.
	env.ambient_light_color = Color(0, 0, 0, 1)
	env.ambient_light_energy = 0.0

	var world := WorldEnvironment.new()
	world.name = "WorldEnvironment"
	world.environment = env
	add_child(world)
