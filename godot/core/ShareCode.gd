class_name ShareCode
# SHARE CODES — and why they are not seeds.
#
# A generated cube has a seed because a seed is what made it: the level number
# goes in, mint runs, the cube comes out. An AUTHORED cube was not made by
# anything. There is no number that reproduces it, and no amount of cleverness
# produces one, because the whole point of an editor is that the player chose
# the voxels rather than a generator choosing them.
#
# So the code carries the cube. Voxel states pack three to a byte, points are
# two bytes each while n stays under ten, and the whole thing is base64url with
# a checksum.
#
# THE EIGHT-CHARACTER ID IS THE NAME, THE CODE IS THE CARGO. The id says which
# cube this is and is short enough to say out loud; it cannot rebuild one. Both
# are shown, and they are not interchangeable.

# THE ALPHABET GREW BY ONE AND THAT IS A FORMAT CHANGE.
#
# Five states packed three to a byte because 5^3 = 125 fits. Six fit too —
# 6^3 = 216 — so the everter costs nothing but a different base, and the packing
# is unchanged.
#
# It is still a different number in the same byte, so a v1 code read as v2
# decodes into a DIFFERENT CUBE rather than failing: the checksum covers the
# bytes, and the bytes are identical. That is the one failure mode worth
# engineering against here, because a made cube is stored as its code and a
# player would open their own creation and find a stranger. So the version leads
# the payload and each version keeps its own alphabet forever — every code
# already in the wild still decodes to exactly the cube it named.
const VOXC := ".#+ABE"
const VOXC_V1 := ".#+AB"
const VERSION := 3

# THE ALPHABET DOES NOT GROW FOR THE TRIGGER, and it cannot: the packing is
# three voxels to a byte, so the radix has a hard ceiling at six — 6^3 is 216
# and fits, 7^3 is 343 and does not. Widening it would cost every cube in the
# game a third more bytes to carry a character that appears once or twice.
#
# So a trigger is written as ordinary trace in the voxel block and its POSITIONS
# are listed separately, which is cheap because there are never many, and the
# second solid follows in the same packing as the first.
const VERSION_TRIGGER := 3


static func _idx(alpha: String, ch: int) -> int:
	return alpha.find(char(ch))


static func encode(lv: Level) -> String:
	var n: int = lv.n
	var cells := n * n * n

	# A CUBE THAT USES NOTHING NEW IS STILL WRITTEN AS v1, so a code shared
	# today opens in a build from yesterday. Only a cube carrying an everter
	# needs the wider alphabet, and only it pays for it.
	var has_trigger := lv.alts.size() > 0
	var needs_v2 := false
	for i in range(cells):
		var c := lv.vox[i]
		if c == Level.TRIGGER or c == Level.TRIGGER2:
			has_trigger = true
		elif c == Level.EVERTER:
			needs_v2 = true
	for a in lv.alts:
		for i in range(a.size()):
			if a[i] == Level.EVERTER:
				needs_v2 = true
				break

	var ver := VERSION_TRIGGER if has_trigger else (2 if needs_v2 else 1)
	var alpha := VOXC if ver >= 2 else VOXC_V1
	var radix := alpha.length()
	var bytes := [ver, n]

	# A trigger is not in the alphabet — see VERSION_TRIGGER — so it is written
	# as the trace it is walkable as, and put back from the position list on the
	# way in.
	_pack(bytes, lv.vox, cells, alpha, radix)

	_put(bytes, n, lv.start)
	_put(bytes, n, lv.goal)

	# PAIRS, AND THE MINIMUM OF THE TWO LISTS RATHER THAN THE LENGTH OF ONE.
	#
	# A node and its lock are one record here: the count is written once and the
	# two points follow it, which is right, because a lock with no node is not a
	# puzzle. This walked keys.Count and indexed doors[i] underneath it, so a
	# level with one more node than lock threw — out of encode, which is called
	# from a share button and from every save of a made cube. Every VALID level
	# has equal counts, so the minimum is lossless for all of them.
	var pairs: int = min(lv.keys.size(), lv.doors.size())
	bytes.append(pairs)
	for i in range(pairs):
		_put(bytes, n, lv.keys[i])
		_put(bytes, n, lv.doors[i])

	# ---- and the second solid, if there is one --------------------------
	if ver >= VERSION_TRIGGER:
		var trig := []
		for i in range(cells):
			if lv.vox[i] == Level.TRIGGER or lv.vox[i] == Level.TRIGGER2:
				trig.append(i)
		bytes.append(int(min(255, trig.size())))
		for i in trig:
			bytes.append((i >> 8) & 255)
			bytes.append(i & 255)
			bytes.append(1 if lv.vox[i] == Level.TRIGGER2 else 0)
		bytes.append(lv.alts.size())
		for a in lv.alts:
			_pack(bytes, a, cells, alpha, radix)

	var sum := 0
	for b in bytes:
		sum = (sum * 31 + b) & 255
	bytes.append(sum)

	var outp := ""
	var i2 := 0
	while i2 < bytes.size():
		var b0: int = bytes[i2]
		var b1: int = bytes[i2 + 1] if i2 + 1 < bytes.size() else 0
		var b2: int = bytes[i2 + 2] if i2 + 2 < bytes.size() else 0
		var have: int = bytes.size() - i2
		var t := (b0 << 16) | (b1 << 8) | b2
		outp += B64.A[(t >> 18) & 63]
		outp += B64.A[(t >> 12) & 63]
		if have > 1:
			outp += B64.A[(t >> 6) & 63]
		if have > 2:
			outp += B64.A[t & 63]
		i2 += 3
	return outp


static func _pack(bytes: Array, cells_in: PoolByteArray, cells: int, alpha: String, radix: int) -> void:
	var i := 0
	while i < cells:
		var t := 0
		for j in range(3):
			var ch: int = cells_in[i + j] if i + j < cells else Level.VOID
			if ch == Level.TRIGGER or ch == Level.TRIGGER2:
				ch = Level.TRACE
			var k := _idx(alpha, ch)
			t = t * radix + (0 if k < 0 else k)
		bytes.append(t)
		i += 3


static func _put(bytes: Array, n: int, w: Vector3) -> void:
	var v := (int(w.y) * n + int(w.z)) * n + int(w.x)
	bytes.append((v >> 8) & 255)
	bytes.append(v & 255)


# Returns null for anything that is not a valid, intact code.
static func decode(code: String):
	# THE ALPHABET CONTAINS A HYPHEN AND THE WORLD IS FULL OF THINGS THAT
	# "HELPFULLY" REPLACE ONE. A code pasted through a notes app or a messaging
	# field comes back with en and em dashes in it, and stripping those as junk
	# would silently shorten the payload and fail the checksum. They are put
	# back rather than thrown away.
	var s := ""
	for i in range(code.length()):
		var c: String = code[i]
		var o := ord(c)
		if (o >= 0x2010 and o <= 0x2015) or o == 0x2212:
			c = "-"
			o = 45
		if (o >= 65 and o <= 90) or (o >= 97 and o <= 122) or (o >= 48 and o <= 57) or o == 45 or o == 95:
			s += c
	if s.length() < 8:
		return null

	var bytes := []
	var i := 0
	while i < s.length():
		var have: int = int(min(4, s.length() - i))
		var t := 0
		for j in range(4):
			var k: int = B64.A.find(s[i + j]) if j < have else 0
			if j < have and k < 0:
				return null
			t = (t << 6) | k
		bytes.append((t >> 16) & 255)
		if have > 2:
			bytes.append((t >> 8) & 255)
		if have > 3:
			bytes.append(t & 255)
		i += 4

	var end: int = bytes.size() - 1
	if end < 2:
		return null
	var sum := 0
	for j in range(end):
		sum = (sum * 31 + bytes[j]) & 255
	if sum != bytes[end]:
		return null                                   # a mistyped code
	var ver: int = bytes[0]
	if ver < 1 or ver > VERSION:
		return null
	var alpha := VOXC_V1 if ver == 1 else VOXC
	var radix := alpha.length()
	var max_byte := radix * radix * radix - 1

	var n: int = bytes[1]
	# NINE, NOT EIGHT. The generator has cut nine-cubes since the size ladder
	# stopped saturating and nothing ever put one through a share code, so the
	# ceiling was never noticed — a fixed catalogue stored as codes puts every
	# one of them through it.
	if n < 3 or n > 9:
		return null
	var cells := n * n * n
	var need := 2 + (cells + 2) / 3
	if end < need + 5:
		return null

	var at := [2]
	var vox = _unpack(bytes, at, cells, alpha, radix, max_byte)
	if vox == null:
		return null

	var bad := [false]
	var start := _read_pt(bytes, at, n, cells, bad)
	var goal := _read_pt(bytes, at, n, cells, bad)
	if bad[0]:
		return null

	if at[0] >= bytes.size():
		return null
	var kn: int = bytes[at[0]]
	at[0] += 1
	if kn > 8:
		return null
	var keys := []
	var doors := []
	for j in range(kn):
		var k := _read_pt(bytes, at, n, cells, bad)
		var d2 := _read_pt(bytes, at, n, cells, bad)
		if bad[0]:
			return null
		keys.append(k)
		doors.append(d2)

	var alts_out := []

	# ---- the second solid, and where the triggers were ------------------
	if ver >= VERSION_TRIGGER:
		if at[0] >= bytes.size():
			return null
		var tn: int = bytes[at[0]]
		at[0] += 1
		var trig := []
		for j in range(tn):
			if at[0] + 2 >= bytes.size():
				return null
			var v: int = (bytes[at[0]] << 8) | bytes[at[0] + 1]
			var kind: int = Level.TRIGGER2 if bytes[at[0] + 2] == 1 else Level.TRIGGER
			at[0] += 3
			if v >= cells:
				return null
			trig.append([v, kind])

		if at[0] >= bytes.size():
			return null
		var n_alt: int = bytes[at[0]]
		at[0] += 1
		if n_alt > 3:
			return null
		for a in range(n_alt):
			var b = _unpack(bytes, at, cells, alpha, radix, max_byte)
			if b == null:
				return null
			alts_out.append(b)

		# A TRIGGER EXISTS IN BOTH SOLIDS, which is what makes the swap a door
		# rather than a cliff — see the carve. Restoring it into one array and
		# not the other would ship a one-way version of a mechanic whose whole
		# argument is that you can step back.
		for t in trig:
			vox[t[0]] = t[1]
			for a in range(alts_out.size()):
				alts_out[a][t[0]] = t[1]

	var lv := Level.new()
	lv.n = n
	lv.vox = vox
	lv.alts = alts_out
	lv.start = start
	lv.goal = goal
	lv.keys = keys
	lv.doors = doors
	lv.par = 0
	lv.name = "SHARED"
	return lv


static func _unpack(bytes: Array, at: Array, cells: int, alpha: String, radix: int, max_byte: int):
	var b := PoolByteArray()
	var i := 0
	while i < cells:
		if at[0] >= bytes.size():
			return null
		var t: int = bytes[at[0]]
		at[0] += 1
		if t > max_byte:
			return null
		var r2 := radix * radix
		var d := [t / r2 % radix, t / radix % radix, t % radix]
		for q in range(3):
			if i + q < cells:
				b.append(ord(alpha[d[q]]))
		i += 3
	return b


static func _read_pt(bytes: Array, at: Array, n: int, cells: int, bad: Array) -> Vector3:
	if at[0] + 1 >= bytes.size():
		bad[0] = true
		return Vector3.ZERO
	var v: int = (bytes[at[0]] << 8) | bytes[at[0] + 1]
	at[0] += 2
	if v >= cells:
		bad[0] = true
		return Vector3.ZERO
	return Vector3(v % n, v / (n * n), v / n % n)
