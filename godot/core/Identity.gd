class_name Identity
# IDENTITY — what makes two cubes the same cube.
#
# A CUBE FOLDED IS THE SAME CUBE. The game is played in twenty-four
# orientations and view_of is an exact integer round trip, so a cube's identity
# is the smallest of the twenty-four ways it can be written down. Rebuilding
# VAULT III / 34 lying on its side is still rebuilding VAULT III / 34, and a
# plain byte compare would not notice.
#
# Keys stay married to their locks through the transform. Sorting the two lists
# apart would let a cube with its pairings swapped — a completely different
# puzzle — hash as a duplicate.

# The alphabet itself lives in B64 — see the note there about the ring this
# used to close between ShareCode, Identity and Catalogue.
const ALPHA := B64.A

const _BAKED_IDS := []
const _BAKED_AT := []


static func level_form(lv: Level, m: Ori) -> String:
	var n: int = lv.n
	var vox := PoolByteArray()
	vox.resize(n * n * n)
	for y in range(n):
		for z in range(n):
			for x in range(n):
				var v := Projection.view_of(n, m, Vector3(x, y, z))
				vox[Level.vidx_v(n, v)] = lv.vox[Level.vidx_n(n, x, y, z)]

	var pairs := []
	for i in range(lv.keys.size()):
		pairs.append(_pt(n, m, lv.keys[i]) + ">" + (_pt(n, m, lv.doors[i]) if i < lv.doors.size() else "-"))
	# JS Array.prototype.sort with no comparator is a lexicographic sort of the
	# string forms by code unit — ordinal, not culture — and GDScript's default
	# String comparison is the same thing.
	pairs.sort()

	# THE SECOND SOLID IS PART OF WHAT THE CUBE IS. Two cubes with identical
	# first layouts and different second ones are two different puzzles — the
	# trigger sends you somewhere else — and a canonical form that stopped at
	# `vox` would call them the same and refuse to let the second one exist.
	var b := ""
	for a in lv.alts:
		var vb := PoolByteArray()
		vb.resize(n * n * n)
		for y in range(n):
			for z in range(n):
				for x in range(n):
					var v := Projection.view_of(n, m, Vector3(x, y, z))
					vb[Level.vidx_v(n, v)] = a[Level.vidx_n(n, x, y, z)]
		b += "|" + vb.get_string_from_ascii()

	return str(n) + "|" + vox.get_string_from_ascii() + b + "|" \
			+ _pt(n, m, lv.start) + "|" + _pt(n, m, lv.goal) + "|" + PoolStringArray(pairs).join(";")


static func _pt(n: int, m: Ori, w: Vector3) -> String:
	var q := Projection.view_of(n, m, w)
	return "%d,%d,%d" % [int(q.x), int(q.y), int(q.z)]


static func canon_form(lv: Level) -> String:
	var best := ""
	var have := false
	for i in range(Turns.count()):
		var f := level_form(lv, Turns.ori(i))
		if not have or f < best:
			best = f
			have = true
	return best


# A 32-bit unsigned multiply, which is what the FNV-ish mixing below is written
# in. It cannot be a plain `*`: 2^32 times 2246822507 overflows a signed 64-bit
# int, which GDScript would carry into a wrong hash rather than wrapping.
static func _umul(a: int, b: int) -> int:
	return Bits.imul(a, b) & 0xFFFFFFFF


# Forty-eight bits, eight characters, no padding to strip. Across the whole
# ranked catalogue the odds of two different cubes colliding are about one in a
# hundred and thirty million — a false accusation of plagiarism is the one
# failure this must not have.
static func canon_id(lv: Level) -> String:
	var s := canon_form(lv)
	var a := 0x811c9dc5
	var b := 0x9e3779b9
	for i in range(s.length()):
		var c := ord(s[i])
		a = _umul(a ^ c, 16777619)
		b = _umul(b ^ c, 2246822507)
		b = b ^ (b >> 13)
	a = a ^ (a >> 16)
	b = b ^ (b >> 15)

	var v := [(b >> 8) & 255, b & 255, (a >> 24) & 255, (a >> 16) & 255, (a >> 8) & 255, a & 255]
	var outp := ""
	for i in range(0, 6, 3):
		var t: int = (v[i] << 16) | (v[i + 1] << 8) | v[i + 2]
		outp += ALPHA[(t >> 18) & 63]
		outp += ALPHA[(t >> 12) & 63]
		outp += ALPHA[(t >> 6) & 63]
		outp += ALPHA[t & 63]
	return outp


static func _ensure_baked() -> void:
	if not _BAKED_IDS.empty():
		return
	# EVERY AUTHORED CUBE, NOT JUST THE ONES AT THE FRONT. This walked
	# Baked.LEVELS by index and reported `i + 1` as the level number, which was
	# the same thing while authored cubes were exactly cubes one to ten. They
	# are not any more — THE FAR SIDE holds four — and an index-is-a-level-number
	# assumption would have silently stopped catching a Forge cube that
	# duplicates one of them.
	for i in range(Baked.LEVELS.size()):
		_BAKED_IDS.append(canon_id(Baked.to_level(Baked.LEVELS[i], i + 1)))
		_BAKED_AT.append(i + 1)
	for i in range(Baked.ARC.size()):
		var arc_level: int = Baked.ARC_START + i
		_BAKED_IDS.append(canon_id(Baked.to_level(Baked.ARC[i], arc_level)))
		_BAKED_AT.append(arc_level)


# Returns null when the cube is new, or a dictionary describing what it already
# is: { kind = "baked" | "minted" | "mine", level, name, id }.
static func collision_of(lv: Level, skip_key, made_ids):
	var id := canon_id(lv)

	_ensure_baked()
	for i in range(_BAKED_IDS.size()):
		if _BAKED_IDS[i] == id:
			var hit: BakedLevel = Baked.try_at(_BAKED_AT[i])
			return {"kind": "baked", "level": _BAKED_AT[i], "name": hit.name, "id": id}

	# THE CATALOGUE, WHICH IS NOW WHAT "ALREADY EXISTS" MEANS. A hundred and
	# fifty fixed cubes, their identities shipped alongside them as eight
	# characters each, because computing them would be twenty-four canonical
	# forms a cube and the better part of a minute on a phone. MintedIds still
	# answers for the generated catalogue underneath, which the daily draws from.
	for i in range(Catalogue.count()):
		var cid: String = Catalogue.all()[i].id
		if cid != "" and cid == id:
			return {"kind": "baked", "level": i + 1, "name": Vaults.level_name(i + 1), "id": id}

	var minted := MintedIds.index()
	if minted.has(id):
		return {"kind": "minted", "level": minted[id], "name": Vaults.level_name(minted[id]), "id": id}

	if made_ids != null:
		for k in made_ids:
			if k != skip_key and made_ids[k] == id:
				return {"kind": "mine", "level": 0, "name": k, "id": id}

	return null
