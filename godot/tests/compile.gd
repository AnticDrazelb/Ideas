extends SceneTree
# LOAD EVERY SCRIPT, AND SAY WHICH ONE DID NOT.
#
# The editor's own scan reports a parse error against whichever file first tried
# to USE the broken class, which on a project this size is rarely the file that
# is wrong. This walks the tree and loads each script on its own, so the name in
# the error is the name of the thing to fix.

func _init() -> void:
	var bad := 0
	var n := 0
	for path in _walk("res://"):
		n += 1
		var s = load(path)
		if s == null:
			print("  FAILED  " + path)
			bad += 1
	print("%d scripts, %d failed" % [n, bad])
	quit(1 if bad > 0 else 0)


func _walk(dir_path: String) -> Array:
	var out := []
	var d := Directory.new()
	if d.open(dir_path) != OK:
		return out
	d.list_dir_begin(true, true)
	var f := d.get_next()
	while f != "":
		var p := dir_path.plus_file(f) if dir_path != "res://" else "res://" + f
		if d.current_is_dir():
			out += _walk(p)
		elif f.ends_with(".gd"):
			out.append(p)
		f = d.get_next()
	d.list_dir_end()
	out.sort()
	return out
