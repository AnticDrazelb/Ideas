extends Node

# THE ONE SCENE, AND EVERYTHING ELSE IS BUILT IN CODE.
#
# The original has no prefabs either: a single MonoBehaviour wakes up and
# constructs the camera, the board, nine canvases and every screen from script,
# because a layout that is authored in an inspector cannot be read, diffed or
# reasoned about — and this game's interface is three thousand lines of measured
# offsets that all have to agree with each other.
#
# So Main.tscn is one empty node with this on it, and this is four lines: hand the
# director the tree and get out of the way. Everything the board needs — its
# offscreen viewport, its environment, its sky — belongs to the director, because
# the director is what takes it apart again.

var director = null


# AND IT IS ONE FRAME LATE, WHICH IS THE WHOLE OF THIS FILE'S REMAINING CONTENT.
#
# When Godot opens a project it adds the main scene to the tree's root, and a
# node that is having children added is BUSY: any add_child on it from inside
# that call fails outright with "Parent node is busy setting up children". The
# director's boot makes five canvases — the housing, the readout, the scanlines,
# the glass and the screens — and a CanvasLayer belongs to the tree's root by
# definition, because it is screen space rather than anything in the world.
#
# So all five were refused. The interface was built, laid out, and reported
# correct sizes for every rect in it, and not one pixel of it was ever drawn:
# exactly the failure UiKit.canvas already carries a note about, reached from a
# direction that note did not anticipate. The board renders, the stars render,
# and there is nothing on top of them.
#
# THE HARNESSES COULD NOT SEE IT, and that is worth writing down next to the fix.
# Every one of them instances Main.tscn and adds it to the root ITSELF, from a
# SceneTree script — and a root that is not mid-add is not busy, so the five
# add_child calls that fail for a player all succeeded for the tests. The tests
# were running the game; they were not running the game the way Godot runs it.
# tools/check.sh does that now, and fails on any engine error.
func _ready() -> void:
	call_deferred("_build")


func _build() -> void:
	director = load("res://game/GameDirector.gd").new()
	director.name = "GameDirector"
	add_child(director)
