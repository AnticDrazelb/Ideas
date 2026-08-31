class_name Fx
extends MeshInstance
# THE DEBRIS AND THE FRONTS.
#
# EVERYTHING IN HERE IS A CLAIM ABOUT MASS. A footfall knocks grit off the block
# and the block answers with a square front one cell wide — small, but it is the
# difference between a marker moving and a thing landing on stone. A landed fold
# rains grit off the WHOLE board rather than off the player, because the mass
# that arrived was the cube's and not yours. A refusal gets chips off the stop it
# just hit and nothing else, because light means yes in this game and no has to
# be told in the dark.
#
# A SQUARE FRONT, NOT A CIRCLE. The thing that just moved was a grid, and a
# circle would be lying about that.
#
# It is one mesh, rebuilt each frame, drawn additively with no texture. The
# effects live in a screen-aligned plane in front of the board and do not rotate
# with it — they belong to the event, not to the geometry.

enum Kind { SPARK, CHIP, EMBER }

class Particle:
	var pos := Vector2.ZERO
	var vel := Vector2.ZERO
	var life := 0.0
	var max_life := 0.0
	var size := 0.0
	var gravity := 0.0
	var drag := 0.0
	var col := Color(1, 1, 1, 1)
	var kind := 0


class Ring:
	var pos := Vector2.ZERO
	var t := 0.0
	var dur := 0.0
	var r0 := 0.0
	var r1 := 0.0
	var width := 0.0
	var col := Color(1, 1, 1, 1)
	var square := false


var _parts := []
var _rings := []

# Quads drawn for exactly one frame — the orb's trail, and nothing else so far.
var _transient := []

var _z := 0.0
var _mat: ShaderMaterial

var _v := PoolVector3Array()
var _uv := PoolVector2Array()
var _kind := PoolVector2Array()
var _col := PoolColorArray()
var _tri := PoolIntArray()


# A hard cap, and a lower one when the light is turned down. A player who has
# asked for less still gets every beat — they just get it at forty per cent,
# because the beats are the game and the confetti is not. At NONE the debris
# stops entirely and the beat is carried by the sound, the haptic and the board
# itself, all three of which are still there.
static func cap() -> int:
	var l := Access.lighting()
	if l == Access.LightLevel.FULL:
		return 460
	return 130 if l == Access.LightLevel.REDUCED else 0


static func amount() -> float:
	return Access.light_amount()


static func build(under: Node) -> Fx:
	var fx = Make.of("res://game/Fx.gd")
	fx.name = "Fx"
	under.add_child(fx)
	fx._init_mat()
	return fx


func _init_mat() -> void:
	cast_shadow = GeometryInstance.SHADOW_CASTING_SETTING_OFF
	_mat = Shaders.material("res://shaders/fx.shader")
	_mat.render_priority = 4
	material_override = _mat


# The plane the debris lives in: just in front of the cube, whatever size it is.
#
# THE SIGN IS THE ENGINE'S. The C# camera looks down +Z and puts the plane at a
# negative z; here it looks down -Z, so the plane is positive and the cube is
# behind it exactly as before.
func set_board(n: int) -> void:
	_z = n * 0.87 + 1.0


func clear() -> void:
	_parts.clear()
	_rings.clear()
	_transient.clear()


# A soft round mark for this frame only. The orb's trail is POSITIONS rather than
# a stretched sprite, because the board is a grid and the path it took should
# read as the path it took.
func blob(at: Vector3, size: float, col: Color) -> void:
	_transient.append([at, size, col])


# INFALL. One mote spawned out on a ring and thrown INWARD, so what you see
# around the character is light being pulled in and going out at the horizon. The
# sideways kick makes it arrive on a spiral rather than falling straight down a
# drain.
func infall(from: Vector3, to: Vector2, angle: float, col: Color) -> void:
	if _parts.size() >= cap():
		return
	var at := Vector2(from.x, from.y)
	var inward := (to - at) * 2.1
	var kick := Vector2(cos(angle + 1.57), sin(angle + 1.57)) * 0.7

	var p := Particle.new()
	p.pos = at
	p.vel = inward + kick
	p.max_life = 0.52
	p.size = 0.09
	p.drag = 0.97
	p.col = col
	p.kind = Kind.EMBER
	_parts.append(p)


# ---- spawning --------------------------------------------------------------
#
# A DICTIONARY WHERE THE C# HAS A STRUCT WITH NAMED FIELDS. GDScript 3.5 has no
# struct and no named arguments, and eleven positional floats at a call site is
# how a speed ends up in a gravity. The keys are the field names; anything absent
# takes the default the C# struct's zero gives it.
func burst(world: Vector3, n: int, o: Dictionary) -> void:
	# Scaled rather than floored at one: at NONE the answer is none, and a
	# max(1, ...) here is how "no particles" quietly becomes "one".
	n = int(round(n * amount()))
	if n <= 0:
		return
	var at := Vector2(world.x, world.y)
	var lim := cap()

	var kind: int = o.get("kind", Kind.SPARK)
	var speed: float = o.get("speed", 0.0)
	var life: float = o.get("life", 0.0)
	var size: float = o.get("size", 0.0)
	var gravity: float = o.get("gravity", 0.0)
	var lift: float = o.get("lift", 0.0)
	var fade: float = o.get("fade", 0.0)
	var col: Color = o.get("col", Color(1, 1, 1, 1))
	var angle: float = o.get("angle", 0.0)
	var spread: float = o.get("spread", 0.0)

	for _i in range(n):
		if _parts.size() >= lim:
			break
		var a: float
		if spread > 0.0:
			a = angle + (randf() - 0.5) * spread
		else:
			a = randf() * PI * 2.0
		var sp := speed * (0.45 + randf() * 0.85)

		var p := Particle.new()
		p.pos = at
		p.vel = Vector2(cos(a) * sp, sin(a) * sp + lift)
		p.max_life = life * (0.7 + randf() * 0.6)
		p.size = size * (0.6 + randf() * 0.8)
		p.gravity = gravity
		p.drag = 0.86 if fade <= 0.0 else fade
		p.col = col
		p.kind = kind
		_parts.append(p)


func ring2(world: Vector3, r0: float, r1: float, dur: float, col: Color,
		width: float, square: bool) -> void:
	if Access.lighting() == Access.LightLevel.NONE:
		return
	if Access.lighting() == Access.LightLevel.REDUCED and _rings.size() > 3:
		return
	var r := Ring.new()
	r.pos = Vector2(world.x, world.y)
	r.dur = dur
	r.r0 = r0
	r.r1 = r1
	r.width = width
	r.col = col
	r.square = square
	_rings.append(r)


# ---- the frame -------------------------------------------------------------

func tick(dt: float) -> void:
	for i in range(_parts.size() - 1, -1, -1):
		var p: Particle = _parts[i]
		p.life += dt
		if p.life >= p.max_life:
			_parts.remove(i)
			continue
		p.vel.y -= p.gravity * dt
		p.vel *= pow(p.drag, dt * 60.0 * 0.016)
		p.pos += p.vel * dt

	for i in range(_rings.size() - 1, -1, -1):
		var r: Ring = _rings[i]
		r.t += dt
		if r.t >= r.dur:
			_rings.remove(i)

	_rebuild_mesh()


func _rebuild_mesh() -> void:
	_v.resize(0)
	_uv.resize(0)
	_kind.resize(0)
	_col.resize(0)
	_tri.resize(0)

	var amt := amount()

	for p in _parts:
		var k: float = 1.0 - p.life / p.max_life
		var c: Color = p.col
		c.a = k * (0.95 if p.kind == Kind.CHIP else 0.85) * amt
		var s: float = p.size * (0.6 + k * 0.6 if p.kind == Kind.EMBER else k * 0.6 + 0.4)
		_quad(p.pos, s, s, c, 1.0 if p.kind == Kind.CHIP else 0.0)

	for r in _rings:
		var k2: float = r.t / r.dur
		var rad: float = lerp(r.r0, r.r1, 1.0 - pow(1.0 - k2, 3.0))
		var c2: Color = r.col
		c2.a = (1.0 - k2) * 0.8 * amt

		# An outline out of four bars, because that is what an outline is and it
		# costs the same as a textured ring without needing one.
		var w: float = r.width
		if r.square:
			_quad(r.pos + Vector2(0, rad), rad * 2.0 + w, w, c2, 1.0)
			_quad(r.pos + Vector2(0, -rad), rad * 2.0 + w, w, c2, 1.0)
			_quad(r.pos + Vector2(rad, 0), w, rad * 2.0 + w, c2, 1.0)
			_quad(r.pos + Vector2(-rad, 0), w, rad * 2.0 + w, c2, 1.0)
		else:
			# A RING IS AN ANNULUS, NOT A NECKLACE.
			#
			# This was twenty round dots spaced around the circumference, which
			# works at a one-cell radius and disappears at any other: the collapse
			# ring grows to several cells across, so twenty specks a tenth of a
			# cell wide ended up more than a cell apart and the round front simply
			# was not there. What you saw was the square front alone — which is
			# why the biggest moment in the game came out square when the original
			# draws both.
			#
			# Built from the geometry instead, so the width is the width at every
			# radius. The uv sits at the centre of the shape mask so the fragment
			# is solid: the triangles ARE the ring, and there is nothing for a
			# mask to round off.
			var seg := 48
			var ri: float = rad - w * 0.5
			var ro: float = rad + w * 0.5
			for i in range(seg):
				var a0 := i / float(seg) * PI * 2.0
				var a1 := (i + 1) / float(seg) * PI * 2.0
				var d0 := Vector2(cos(a0), sin(a0))
				var d1 := Vector2(cos(a1), sin(a1))
				_band(r.pos + d0 * ri, r.pos + d1 * ri, r.pos + d1 * ro, r.pos + d0 * ro, c2)

	for t in _transient:
		_quad(Vector2(t[0].x, t[0].y), t[1], t[1], t[2], 0.0)
	_transient.clear()

	if _v.size() == 0:
		mesh = null
		return
	var m := ArrayMesh.new()
	var arr := []
	arr.resize(ArrayMesh.ARRAY_MAX)
	arr[ArrayMesh.ARRAY_VERTEX] = _v
	arr[ArrayMesh.ARRAY_TEX_UV] = _uv
	arr[ArrayMesh.ARRAY_TEX_UV2] = _kind
	arr[ArrayMesh.ARRAY_COLOR] = _col
	arr[ArrayMesh.ARRAY_INDEX] = _tri
	m.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	mesh = m


# Four corners, filled flat. The uv is pinned to the middle of the shape mask so
# every fragment is solid — this is for shapes the TRIANGLES describe, where a
# disc or square mask would only eat the edges off it.
func _band(a: Vector2, b: Vector2, c: Vector2, d: Vector2, col: Color) -> void:
	var i := _v.size()
	_v.append(Vector3(a.x, a.y, _z))
	_v.append(Vector3(b.x, b.y, _z))
	_v.append(Vector3(c.x, c.y, _z))
	_v.append(Vector3(d.x, d.y, _z))

	var mid := Vector2(0.5, 0.5)
	for _j in range(4):
		_uv.append(mid)
		_kind.append(Vector2(0, 0))
		_col.append(col)

	_tri.append(i)
	_tri.append(i + 1)
	_tri.append(i + 2)
	_tri.append(i)
	_tri.append(i + 2)
	_tri.append(i + 3)


func _quad(at: Vector2, w: float, h: float, c: Color, square: float) -> void:
	var b := _v.size()
	var hw := w * 0.5
	var hh := h * 0.5
	_v.append(Vector3(at.x - hw, at.y - hh, _z))
	_v.append(Vector3(at.x + hw, at.y - hh, _z))
	_v.append(Vector3(at.x + hw, at.y + hh, _z))
	_v.append(Vector3(at.x - hw, at.y + hh, _z))

	_uv.append(Vector2(0, 0))
	_uv.append(Vector2(1, 0))
	_uv.append(Vector2(1, 1))
	_uv.append(Vector2(0, 1))

	for _j in range(4):
		_kind.append(Vector2(square, 0))
		_col.append(c)

	_tri.append(b)
	_tri.append(b + 1)
	_tri.append(b + 2)
	_tri.append(b)
	_tri.append(b + 2)
	_tri.append(b + 3)


# ---- the named events ------------------------------------------------------
#
# Each of these is one beat of the original's effects budget, kept as a named
# method rather than a pile of call-site parameters so that the reasons stay
# attached to the numbers.

# A footfall knocks grit off the block, and the block answers with a square front
# one cell wide.
func footfall(at: Vector3, chip: Color) -> void:
	burst(at + Vector3.UP * -0.2, 4, {
		"kind": Kind.CHIP, "speed": 0.34, "life": 0.42, "size": 0.11,
		"gravity": 2.6, "fade": 0.90, "col": chip})
	ring2(at, 0.30, 0.62, 0.26, Palette.ARC, 0.05, true)


# The arrival: a heavier knock, and the last of the walk's momentum going into
# the stone.
func land(at: Vector3, chip: Color) -> void:
	burst(at + Vector3.UP * -0.24, 7, {
		"kind": Kind.CHIP, "speed": 0.44, "life": 0.46, "size": 0.13,
		"gravity": 3.0, "fade": 0.80, "col": chip})
	burst(at, 5, {
		"kind": Kind.EMBER, "speed": 0.20, "lift": 0.26, "life": 0.70,
		"size": 0.11, "gravity": -0.18, "col": Palette.ARC})


# A LOCK OPENING IS THE LOUDEST THING THAT IS NOT A FOLD. It is the only moment a
# node stops being a number and becomes a thing that did something, so it gets
# the whole kit.
func lock_open(at: Vector3) -> void:
	burst(at, 26, {"kind": Kind.SPARK, "speed": 1.5, "life": 0.64, "size": 0.16,
			"gravity": 1.1, "col": Palette.NODE})
	burst(at, 10, {"kind": Kind.CHIP, "speed": 1.1, "life": 0.76, "size": 0.13,
			"gravity": 3.4, "col": Palette.NODE * 0.7})
	ring2(at, 0.2, 2.6, 0.52, Palette.NODE, 0.07, false)


func node_taken(at: Vector3) -> void:
	burst(at, 20, {"kind": Kind.SPARK, "speed": 1.3, "life": 0.56, "size": 0.15,
			"gravity": 0.2, "col": Palette.NODE})
	burst(at, 8, {"kind": Kind.EMBER, "speed": 0.26, "lift": 0.40, "life": 0.90,
			"size": 0.13, "gravity": -0.24, "col": Palette.NODE})
	ring2(at, 0.15, 1.9, 0.46, Palette.NODE, 0.06, false)


# The landing is the verb. A square front leaves the cube at BOARD scale, and
# then grit rains off the whole face — seeded across all of it rather than at the
# player, because the mass that arrived was the cube's.
func fold_landed(n: int, player_at: Vector3) -> void:
	ring2(Vector3.ZERO, n * 0.18, n * 0.92, 0.62, Palette.TRACE_NEAR, 0.10, true)
	for _i in range(9):
		var where := Vector3((randf() - 0.5) * n, (randf() - 0.5) * n, 0)
		burst(where, 2, {"kind": Kind.CHIP, "speed": 0.30, "life": 0.76, "size": 0.11,
				"gravity": 3.0, "fade": 0.65, "col": Palette.GRID_HI})
	burst(player_at, 10, {"kind": Kind.SPARK, "speed": 0.78, "life": 0.44, "size": 0.14,
			"gravity": 0.6, "col": Palette.ARC})


# A refusal is a collision: chips off the stop it just hit, and nothing else.
func fold_refused(n: int, turn: int) -> void:
	var sgn: float = 1.0 if (turn == 1 or turn == 2) else -1.0
	var axis_x: bool = turn < 2
	var at := Vector3(-sgn * n * 0.42 if axis_x else 0.0, 0.0 if axis_x else sgn * n * 0.42, 0.0)
	var ang: float
	if axis_x:
		ang = 0.0 if sgn > 0 else PI
	else:
		ang = -PI / 2.0 if sgn > 0 else PI / 2.0

	burst(at, 12, {"kind": Kind.CHIP, "speed": 1.2, "life": 0.62, "size": 0.12,
			"gravity": 4.2, "fade": 0.80, "col": Palette.GRID_HI,
			"angle": ang, "spread": 1.6})


# THE BIGGEST EVENT IN THE GAME. Two square fronts leave the plate at different
# speeds and the room floods with the plate's own colour. The flash is
# deliberately short of a white-out: the whole point of the flip is that the old
# material is SEEN leaving.
func plate_fired(n: int, at: Vector3) -> void:
	ring2(at, 0.2, n * 1.05, 0.76, Palette.rust(), 0.13, true)
	ring2(at, 0.2, n * 0.62, 0.52, Palette.INK, 0.07, true)
	burst(at, 30, {"kind": Kind.SPARK, "speed": 1.7, "life": 0.78, "size": 0.18,
			"gravity": 0.0, "col": Palette.rust()})
	burst(at, 14, {"kind": Kind.SPARK, "speed": 0.56, "life": 1.0, "size": 0.24,
			"gravity": -0.16, "col": Palette.INK})
	burst(at, 16, {"kind": Kind.CHIP, "speed": 1.5, "life": 0.90, "size": 0.14,
			"gravity": 3.0, "col": Palette.GRID_HI})


# A refused tap points at itself: the toast says why, the ring says WHERE.
func deny(at: Vector3, col: Color) -> void:
	ring2(at, 0.62, 0.30, 0.34, col, 0.06, true)


# THE EXIT, and the only place the budget is spent without restraint — you are
# leaving, so nothing after this has to stay legible.
#
# NO RINGS. A square front and a circular one, expanding off the cell the core
# took you in, on top of a board that is about to come apart into its own cells —
# two more shapes arriving in the same half second as the shape that matters. The
# exit is the machine breaking; anything drawn over it is competing with it.
func collapse(n: int, at: Vector3) -> void:
	burst(at, 34, {"kind": Kind.SPARK, "speed": 1.9, "life": 0.80, "size": 0.18,
			"gravity": 0.0, "col": Palette.core()})
	burst(at, 18, {"kind": Kind.SPARK, "speed": 0.70, "life": 1.0, "size": 0.24,
			"gravity": -0.14, "col": Palette.RUST_HI})
	burst(at, 22, {"kind": Kind.EMBER, "speed": 0.14, "lift": 0.70, "life": 1.5,
			"size": 0.16, "gravity": -0.40, "fade": 0.8, "col": Palette.core()})


# THE ENGINE FAILING, half a second after the core took you.
#
# The cells themselves are thrown in the vertex shader — see cell.shader — so
# this is not the explosion, it is the DUST OFF it: grit thrown wide enough to
# still be in frame while the debris tumbles through it. Centred on the board
# rather than on the player, because it is the whole machine that goes.
func shatter(n: int, at: Vector3) -> void:
	burst(at, 46, {"kind": Kind.CHIP, "speed": 3.4, "life": 1.1, "size": 0.16,
			"gravity": 0.9, "fade": 0.85, "col": Palette.RUST_HI})
	burst(at, 30, {"kind": Kind.SPARK, "speed": 2.6, "life": 0.70, "size": 0.13,
			"gravity": 0.0, "col": Palette.core()})
	burst(at, 20, {"kind": Kind.EMBER, "speed": 0.40, "lift": 0.55, "life": 1.7,
			"size": 0.18, "gravity": -0.34, "fade": 0.8, "col": Palette.rust()})


# A PERFECT LINE IS A DIFFERENT EVENT, and it never once looked like one. Par is
# the whole scoring system; clearing at par with the same exit as clearing four
# over says the game does not care.
func perfect_line(n: int, at: Vector3) -> void:
	burst(at, 34, {"kind": Kind.CHIP, "speed": 2.1, "life": 1.5, "size": 0.14,
			"gravity": 1.8, "fade": 0.9, "col": Palette.ARC})
	burst(at, 22, {"kind": Kind.EMBER, "speed": 0.30, "lift": 1.2, "life": 1.8,
			"size": 0.16, "gravity": -0.46, "fade": 0.85, "col": Palette.ARC})
