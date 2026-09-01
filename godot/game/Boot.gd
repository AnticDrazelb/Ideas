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


func _ready() -> void:
	director = load("res://game/GameDirector.gd").new()
	director.name = "GameDirector"
	add_child(director)
