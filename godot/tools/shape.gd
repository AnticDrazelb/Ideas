extends SceneTree
# ARE THESE 150 CUBES 150 DIFFERENT PUZZLES?
#
# The ladder is minted rather than written past vault I, and a generator that is
# filtered on "solvable at par N" will happily produce the same idea forty times
# with the walls in different places. This asks the question directly: it reduces
# every cube to a form that does not care which way up it was generated, and
# looks for cubes that are the same shape as each other.
#
# The canonical form is the voxel grid read out in VIEW coordinates under each
# of the twenty-four orientations, with the start and the core marked, and the
# smallest of the twenty-four strings kept. Two cubes that are the same solid
# seen from different sides produce the same string.
#
#   godot --path godot -s tools/shape.gd > shape.csv

func _canon(lv: Level) -> String:
	var n: int = lv.n
	var best := ""
	for o in range(Turns.count()):
		var m: Ori = Turns.ori(o)
		var buf := PoolByteArray()
		buf.resize(n * n * n)
		for x in range(n):
			for y in range(n):
				for z in range(n):
					var w := Vector3(x, y, z)
					var v: Vector3 = Projection.view_of(n, m, w)
					var t: int = lv.vox[Level.vidx_n(n, x, y, z)]
					# the two fixed points matter as much as the material
					if w == lv.start:
						t = 1
					elif w == lv.goal:
						t = 2
					buf[(int(v.x) * n + int(v.y)) * n + int(v.z)] = t
		var sig := Marshalls.raw_to_base64(buf)
		if best == "" or sig < best:
			best = sig
	return best


func _init() -> void:
	Catalogue._ensure()
	print("level,n,canon")
	for level in range(1, Catalogue.count() + 1):
		if not Catalogue.has(level):
			continue
		var lv: Level = Catalogue.get_level(level)
		if lv == null:
			continue
		print("%d,%d,%s" % [level, lv.n, _canon(lv)])
	quit()
