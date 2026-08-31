class_name MadeCube
extends Reference
# One cube the player built. The key is the cube's own canonical id, so storing
# one twice is impossible.

var key := ""
var name := ""
var id := ""
var code := ""       # the share code — the cargo, where the id is only the name
var n := 0
var par := 0
var best := -1
var tbest := -1


func to_dict() -> Dictionary:
	return {"key": key, "name": name, "id": id, "code": code,
			"n": n, "par": par, "best": best, "tbest": tbest}


# THE OTHER DIRECTION LIVES IN SaveData, because a class may not name itself in
# GDScript and a factory returning a MadeCube has to say the word. See
# SaveData._made_from.
