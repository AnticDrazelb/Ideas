class_name Glyphs
# THE FOUR OBJECTS, DRAWN RATHER THAN LOADED.
#
# A packaged app should not pay a download for four shapes it can draw, and the
# shapes are the ones the manual teaches: a diamond is a NODE, a slashed hex is a
# LOCK, concentric rings are the CORE, and the filled disc with a horizon around
# it is YOU. They are generated once into textures at a size that stays sharp on
# a dense phone screen, in white, and tinted where they are drawn — because the
# colour of a thing is data (see Palette) and belongs to the thing, not to its
# picture.

const R := 128     # texture resolution; one glyph is one cell wide

const _CACHE := {}


static func get_tex(role: String) -> ImageTexture:
	if _CACHE.has(role):
		return _CACHE[role]
	var t := _build(role)
	_CACHE[role] = t
	return t


static func _build(role: String) -> ImageTexture:
	var img := Image.new()
	img.create(R, R, false, Image.FORMAT_RGBA8)
	img.lock()
	var ss := 3
	for y in range(R):
		for x in range(R):
			# sample on a -1..1 square, with a little supersampling so the
			# diagonals do not stair-step at small sizes
			var acc := Color(0, 0, 0, 0)
			for sy in range(ss):
				for sx in range(ss):
					var u := ((x + (sx + 0.5) / ss) / float(R)) * 2.0 - 1.0
					# THE TEXTURE IS WRITTEN DOWNWARD AND THE FIELD IS MEASURED
					# UPWARD. Every sample function below is symmetric in v
					# except the arrow and the two-plate icon, and those two are
					# the ones that would have come out upside down.
					var v := 1.0 - ((y + (sy + 0.5) / ss) / float(R)) * 2.0
					if role == "halo":
						acc += _halo(u, v)
					else:
						var a := _sample(role, u, v)
						acc += Color(1, 1, 1, a)
			acc /= ss * ss
			img.set_pixel(x, y, Color(clamp(acc.r, 0, 1), clamp(acc.g, 0, 1),
					clamp(acc.b, 0, 1), clamp(acc.a, 0, 1)))
	img.unlock()
	var t := ImageTexture.new()
	t.create_from_image(img, Texture.FLAG_FILTER)
	return t


static func _sample(role: String, u: float, v: float) -> float:
	match role:
		"node":
			# a diamond, with a solid pip at its centre
			var d := abs(u) + abs(v)
			var ring := _band(d, 0.86, 0.13)
			var pip := 1.0 if d < 0.34 else 0.0
			return max(ring, pip)
		"lock":
			# a hexagonal barrier with two bars struck through it
			var hex := _hex(u, v)
			var ring2 := _band(hex, 0.86, 0.12)
			var bar1 := _bar(u, v, -0.16, 0.085)
			var bar2 := _bar(u, v, 0.16, 0.085)
			var bars: float = max(bar1, bar2) * (1.0 if hex < 0.9 else 0.0)
			return max(ring2, bars)
		"core":
			# the way out: an aperture with a horizon, plus four ticks
			var r := sqrt(u * u + v * v)
			var outer := _band(r, 0.92, 0.07) * 0.5
			var mid := _band(r, 0.62, 0.09)
			var centre := 1.0 if r < 0.22 else 0.0
			var ticks := 0.0
			if abs(v) < 0.05 and abs(u) > 0.72 and abs(u) < 0.99:
				ticks = 1.0
			if abs(u) < 0.05 and abs(v) > 0.72 and abs(v) < 0.99:
				ticks = 1.0
			return max(max(outer, mid), max(centre, ticks))
		"plate":
			var r3 := sqrt(u * u + v * v)
			var ring3 := _band(r3, 0.80, 0.10)
			var slash: float = _bar(u, v, 0.0, 0.075) * (1.0 if r3 < 0.84 else 0.0)
			return max(ring3, slash)
		"evert":
			# A RING TURNING THROUGH ITSELF. The plate is a ring with a bar across
			# it — one thing crossed out. This is a ring with a smaller ring
			# INSIDE it and a gap on each side, which reads as a surface passing
			# through its own centre, because that is what everting a solid is.
			#
			# Its silhouette does the work rather than its colour: a mark on a lit
			# trace is 1.2 to 1.4 against the cell it sits on, for every mark this
			# game has, so the shape is what makes it an everter and not a node.
			var r4 := sqrt(u * u + v * v)
			var outer2 := _band(r4, 0.86, 0.085)
			var inner := _band(r4, 0.40, 0.115)
			# two bites out of the outer ring, left and right, so the silhouette
			# is broken where a node's and a lock's are closed
			var gap := 1.0 if (abs(v) < 0.20 and abs(u) > 0.62) else 0.0
			return max(outer2 * (1.0 - gap), inner)
		"eye":
			# a rounded slab, so squashing it for a blink or a smile still reads
			# as an eye rather than as a line
			var d2: float = max(abs(u) - 0.45, 0.0)
			var r5 := sqrt(d2 * d2 + v * v)
			return 1.0 if r5 < 0.82 else clamp(1.0 - (r5 - 0.82) / 0.12, 0.0, 1.0)
		# ---- the manual's two building blocks -------------------------------
		#
		# Every diagram in the manual is made of these. That is not laziness — the
		# manual is explaining a board built out of square cells and a gesture
		# made of directions, so a picture assembled from the same two marks is
		# the subject rather than an illustration of it.
		"sq":
			var e: float = max(abs(u), abs(v))
			return _band(e, 0.82, 0.11)
		"sqfill":
			var e2: float = max(abs(u), abs(v))
			return 1.0 if e2 < 0.80 else clamp(1.0 - (e2 - 0.80) / 0.08, 0.0, 1.0)
		"swap":
			# TWO SQUARES TRADING PLACES, which is what a trigger does: one board
			# leaves and another arrives in the same space. Not a ring and not a
			# diamond — those are taken by things that change a cell, and this
			# changes all of them.
			var a1: float = max(abs(u + 0.34), abs(v + 0.34))
			var a2: float = max(abs(u - 0.34), abs(v - 0.34))
			return max(_band(a1, 0.46, 0.11), _band(a2, 0.46, 0.11))
		"arrow":
			# a head and a shaft, pointing up
			var head := 1.0 if (v > 0.06 and abs(u) < (0.92 - v) * 0.70) else 0.0
			var shaft := 1.0 if (v <= 0.10 and abs(u) < 0.15) else 0.0
			return max(head, shaft)

		# ---- the calibrate marks --------------------------------------------
		#
		# Eight small icons, one per setting. They are not decoration: a list of
		# eight rows that differ only in their wording is read by reading, and a
		# list where each row carries a mark is read by recognition — which is
		# what you want on a screen somebody opens to change one thing they
		# already have in mind.
		"i.sound":
			# a cone, and two arcs coming off it
			var cone := 0.0
			if (u < -0.1 and abs(v) < 0.34) or (u < 0.34 and abs(v) < 0.34 + (0.34 - u) * 0.7 and u > -0.1):
				cone = 1.0
			var r6 := sqrt(u * u + v * v)
			var arc: float = (_band(r6, 0.62, 0.07) + _band(r6, 0.92, 0.07)) * (1.0 if u > 0.35 else 0.0)
			return max(cone, arc)
		"i.haptic":
			# a handset, with a shiver either side
			var body := 1.0 if (abs(u) < 0.30 and abs(v) < 0.78) else 0.0
			var hole := 1.0 if (abs(u) < 0.18 and abs(v) < 0.62) else 0.0
			var b2 := 1.0 if (abs(u) > 0.52 and abs(u) < 0.66 and abs(v) < 0.34) else 0.0
			return max(body - hole, b2)
		"i.fx":
			# a four-point sparkle: the star every interface uses for "extra"
			var k: float = pow(abs(u), 0.55) + pow(abs(v), 0.55)
			return 1.0 if k < 1.0 else clamp(1.0 - (k - 1.0) / 0.12, 0.0, 1.0)
		"i.togo":
			var r7 := sqrt(u * u + v * v)
			return max(_band(r7, 0.86, 0.10), max(_band(r7, 0.50, 0.10), 1.0 if r7 < 0.18 else 0.0))
		"i.depth":
			# two plates, one behind the other — a distance, drawn
			var a := 1.0 if (abs(u + 0.22) < 0.44 and abs(v + 0.22) < 0.44) else 0.0
			var ai := 1.0 if (abs(u + 0.22) < 0.32 and abs(v + 0.22) < 0.32) else 0.0
			var b := 1.0 if (abs(u - 0.26) < 0.40 and abs(v - 0.26) < 0.40) else 0.0
			var bi := 1.0 if (abs(u - 0.26) < 0.28 and abs(v - 0.26) < 0.28) else 0.0
			return max(a - ai, b - bi)
		"i.buzz":
			# a waveform: amplitude, which is the thing the slider sets
			var wave := sin(u * 5.2) * 0.52
			return 1.0 if (abs(v - wave) < 0.13 and abs(u) < 0.92) else 0.0
		"i.bright":
			var r8 := sqrt(u * u + v * v)
			var core2 := 1.0 if r8 < 0.42 else 0.0
			var ang := atan2(v, u)
			var spoke := 1.0 if (abs(sin(ang * 4.0)) > 0.94 and r8 > 0.58 and r8 < 0.94) else 0.0
			return max(core2, spoke)
		"i.contrast":
			# a disc half filled — the only honest picture of contrast
			var r9 := sqrt(u * u + v * v)
			if r9 > 0.88:
				return 0.0
			return 1.0 if u > 0.0 else _band(r9, 0.82, 0.09)

		"ring":
			# THE EDGE DOES THE FINDING. One thin, very bright circle right on the
			# silhouette. On a pale deck this ring is the whole difference between
			# "a hole in the world" and "a hole in the floor", and it is the only
			# reason a black object is allowed to be the player marker in a game
			# where dark already means impassable.
			var ra := sqrt(u * u + v * v)
			return _band(ra, 0.965, 0.0525)          # lineWidth rr*0.105
		"arc":
			# Brighter over one arc, travelling: the whole of "it is turning".
			var rb := sqrt(u * u + v * v)
			var band := _band(rb, 0.965, 0.025)      # lineWidth rr*0.05
			if band <= 0.0:
				return 0.0
			var aa := atan2(v, u)
			if aa < 0.0:
				aa += PI * 2.0
			# two radians of it, feathered at both ends rather than butt-capped
			var span := 2.0
			var fade := 0.10
			if aa > span + fade:
				return 0.0
			var ends: float = min(clamp(aa / fade, 0.0, 1.0), clamp((span + fade - aa) / fade, 0.0, 1.0))
			return band * ends
		_:
			# "hole" — the event horizon itself.
			#
			# A HOLE, NOT A BALL. This is drawn opaque and black, so it does not
			# cover the deck it stands on — it REMOVES it, down to the same
			# nothing the void columns are. A dark disc would be a spot of black
			# ON the board; this is an absence OF board, and the eye reads the
			# difference long before it can name it.
			var rc := sqrt(u * u + v * v)
			return 1.0 if rc < 0.985 else clamp(1.0 - (rc - 0.985) / 0.015, 0.0, 1.0)


# THE LENSING HALO, and the one detail that decides whether any of this works.
#
# A radial gradient FILLS ITS INNER CIRCLE with the stop-0 colour, so a "donut"
# whose first stop is bright is a filled disc with extra steps — which is exactly
# what floods the horizon and turns the one black thing on the board into a
# slate-grey disc. Stop 0 is transparent; the light starts a hair outside it. The
# original carries a paragraph about getting this wrong, and it is worth carrying
# the fix across with it.
#
# Sampled so r=1 is 2.1 marker radii, which is where the halo ends.
static func _halo(u: float, v: float) -> Color:
	var r := sqrt(u * u + v * v) * 2.1          # back into marker radii
	var t := (r - 0.86) / (2.1 - 0.86)
	if t <= 0.0 or t >= 1.0:
		return Color(0, 0, 0, 0)

	var c0 := Color(0.133, 0.827, 0.933, 0.0)   # #22d3ee, transparent
	var c1 := Color(0.133, 0.827, 0.933, 0.34)
	var c2 := Color(0.055, 0.647, 0.914, 0.15)  # #0ea5e9
	var c3 := Color(0.047, 0.290, 0.427, 0.0)   # #0c4a6e, transparent

	if t < 0.04:
		return c0.linear_interpolate(c1, t / 0.04)
	if t < 0.22:
		return c1.linear_interpolate(c2, (t - 0.04) / 0.18)
	return c2.linear_interpolate(c3, (t - 0.22) / 0.78)


static func _band(value: float, at: float, half_width: float) -> float:
	return clamp(1.0 - abs(value - at) / half_width, 0.0, 1.0)


static func _hex(u: float, v: float) -> float:
	# distance-ish field for a flat-top hexagon
	return max(abs(v), abs(u) * 0.8660254 + abs(v) * 0.5)


static func _bar(u: float, v: float, offset: float, half_width: float) -> float:
	# a 45-degree bar, offset along its normal
	var d := abs((u - v) * 0.70710678 - offset)
	return clamp(1.0 - d / half_width, 0.0, 1.0)
