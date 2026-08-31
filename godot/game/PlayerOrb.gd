class_name PlayerOrb
extends Spatial
# YOU: A HOLE, AND SOMETHING ON THE OTHER SIDE OF IT.
#
# This had a face for a while — a glowing ball with two eyes, on the sound
# argument that a circle with eyes is a character and a glowing ball is an
# effect. It is gone, and the reason is worth keeping next to the code that
# replaced it: a black hole does not have a face, and hanging accretion rings off
# one made it a planet. The ring ended up doing the work of being found, which
# left the hole itself as decoration on its own character.
#
# What is left has to earn its legibility the way the real thing does: with an
# edge, and with what can be seen through it.
#
#   IT IS A HOLE, NOT A DISC. The horizon is drawn opaque and black, so it
#   REMOVES the deck under it rather than covering it — an absence of board,
#   which is exactly what a void column is. Stand beside a gap in the cube and
#   the two are made of the same nothing.
#
#   THE EDGE DOES THE FINDING. A photon ring — one thin, very bright circle right
#   on the silhouette — with a soft lensing halo outside it and a brighter arc
#   travelling round. On a pale deck that ring is the whole difference between "a
#   hole in the world" and "a hole in the floor", and it is the only reason a
#   black object is allowed to be the player marker in a game where dark already
#   means impassable.
#
#   IT REACTS WITHOUT A FACE. Everything the character used to say with eyes now
#   happens as gravity: the rim flares and the halo swells on a node or a gate,
#   and it flinches on a refusal. A thing with no face that answers what you did
#   is not less expressive than one with eyes — it is expressive in the only
#   language it has.
#
#   AND IT STILL GETS BORED. Every five and a half seconds of a player thinking
#   it bounces once on the spot: no sound, no particles, nothing that could be
#   mistaken for the game asking for something. A character that does something
#   when you do nothing.
#
# INFALL, NOT EMISSION. The debris around it is spawned out on a ring and thrown
# INWARD, so what you see is light being pulled in and going out at the horizon.
# Same particle system, same cost — the velocity is simply aimed the other way. A
# lamp sheds; this is the opposite of a lamp.

# The marker's radius in cells, before breathing. The original's sz*0.325.
const RAD := 0.325

const TRAIL_MS := 300.0

var _s: Session
var _view: CubeView
var _cam: Camera
var _fx: Fx

var _root: Spatial
var _hole: GlyphQuad
var _ring: GlyphQuad
var _arc: GlyphQuad
var _halo: GlyphQuad

# ---- what it has to say ----------------------------------------------------
var _mood := ""
var _mood_t := 0.0
var _mood_dur := 0.0
var _idle := 0.0

# The bounce. 1 means at rest; 0 restarts it.
var _hop := 1.0

var _trail := []
var _emit := 0.0


static func build(under: Node, s: Session, view: CubeView, cam: Camera, fx: Fx) -> PlayerOrb:
	var p = Make.of("res://game/PlayerOrb.gd")
	p.name = "Player"
	under.add_child(p)
	p._s = s
	p._view = view
	p._cam = cam
	p._fx = fx
	p._compose()
	return p


func _compose() -> void:
	_root = Spatial.new()
	_root.name = "orb"
	add_child(_root)

	# THE STACK, AND WHY IT IS THIS ORDER.
	#   96  the hole      opaque black; takes the deck away
	#   98  the antipode  drawn in CubeView, INSIDE the hole it shows through
	#   99  the halo      light bending round the outside
	#  100  the ring      the silhouette
	#  101  the arc       the bright part of the ring, travelling
	_hole = _quad("hole", "hole", Palette.VOID, 96, false)
	_halo = _quad("halo", "halo", Color(1, 1, 1, 1), 99, true)
	_ring = _quad("ring", "ring", Palette.ARC, 100, true)
	_arc = _quad("arc", "arc", Color("#a5f3fc"), 101, true)


func _quad(node_name: String, glyph: String, col: Color, order: int, additive: bool) -> GlyphQuad:
	var q := GlyphQuad.new()
	q.name = node_name
	_root.add_child(q)
	q.build(glyph, col, 1.0, order, additive)
	return q


# ---- events ----------------------------------------------------------------

func set_mood(mood: String, dur_ms: float = 420.0) -> void:
	_mood = mood
	_mood_t = 0.0
	_mood_dur = dur_ms


# Restart the bounce. Landing does this; so does boredom.
func hop() -> void:
	_hop = 0.0


func reset() -> void:
	_trail.clear()
	_mood = ""
	_mood_dur = 0.0
	_idle = 0.0
	_hop = 1.0


# ---- the frame -------------------------------------------------------------

func tick(dt_ms: float, shown: bool) -> void:
	if _s == null or _s.lv == null or not shown:
		_root.visible = false
		return
	_root.visible = true

	_step_mood(dt_ms)
	_step_trail(dt_ms)
	_place()


func _step_mood(dt: float) -> void:
	if _mood_dur > 0.0:
		_mood_t += dt
		if _mood_t >= _mood_dur:
			_mood = ""
			_mood_dur = 0.0

	if _s.walking != null or _s.anim != null or _s.drag != null:
		_idle = 0.0
	else:
		_idle += dt

	# and it gets bored
	if _idle > 5500.0 and _hop >= 1.0 and not _s.won:
		_idle = 4000.0
		hop()

	_hop = min(1.0, _hop + dt / 300.0)


# How hard it is reacting, 0..1. This is the whole emotional range of a thing
# with no face: the rim brightens and the halo swells, and that is the entire
# vocabulary. A node or the core gets the full flare; a refused fold gets a
# smaller one, because a flinch is not a cheer.
func _flare() -> float:
	if _mood_dur <= 0.0:
		return 0.0
	var left := 1.0 - _mood_t / _mood_dur
	if _mood == "happy" or _mood == "win":
		return left
	return 0.6 * left if _mood == "wide" else 0.0


func _step_trail(dt: float) -> void:
	for i in range(_trail.size() - 1, -1, -1):
		_trail[i][1] += dt
		if _trail[i][1] > TRAIL_MS:
			_trail.remove(i)

	if _s.won:
		return

	var at := _world_pos()

	# the trail is POSITIONS rather than a stretched sprite, because the board is
	# a grid and the path it took should read as the path it took
	if _s.walking != null and _trail.size() < 40:
		_trail.append([at, 0.0])

	_emit -= dt
	if _emit <= 0.0:
		_emit = 42.0 if _s.walking != null else 150.0
		_infall_one(at)


# One mote, spawned out on a ring and aimed back in on a spiral.
func _infall_one(at: Vector3) -> void:
	if _fx == null or not Access.light():
		return
	var a := randf() * PI * 2.0
	var r := 0.38 + randf() * 0.14
	var from := Vector3(at.x + cos(a) * r, at.y + sin(a) * r, 0.0)
	_fx.infall(from, Vector2(at.x, at.y), a, Color(0.59, 0.88, 1.0))


func _world_pos() -> Vector3:
	var v := Projection.view_of(_s.n, _s.m, _s.pos)
	var c := (_s.n - 1) * 0.5
	return Vector3(v.x - c, v.y - c, 0.0)


func _place() -> void:
	var t := float(OS.get_ticks_msec()) / 1000.0

	# IT BREATHES. Two beats at unrelated rates, so the size never settles into a
	# pulse you can count — the difference between something alive and something
	# animating.
	var br := 0.90 + 0.10 * sin(t * 3.1) + 0.045 * sin(t * 7.3)
	var rr := RAD * br

	var flare := _flare()
	var rim := 1.0 + flare * 0.9

	# SQUASH ON THE WAY DOWN, STRETCH ON THE WAY UP — applied to the whole marker
	# rather than to each piece, so the ring and the halo deform with the hole. A
	# squashed hole with a round ring on it is a sticker.
	var sqx := 1.0
	var sqy := 1.0
	var lift := 0.0
	if _s.walking != null:
		var vv := cos(_s.walking.t * PI)          # +1 rising, -1 falling
		sqy = 1.0 + vv * 0.10
		sqx = 1.0 - vv * 0.09
	elif _hop < 1.0:
		var hb := sin(_hop * PI * 1.6) * pow(1.0 - _hop, 2.2)
		sqy = 1.0 - hb * 0.30
		sqx = 1.0 + hb * 0.24
		lift = abs(sin(_hop * PI * 1.6)) * pow(1.0 - _hop, 2.0) * 0.10

	# The marker rides the cube's own projection while it folds, so it stays on
	# the cell it is standing on rather than hanging in the air. At rest that is
	# exactly the flat grid position.
	var toward := _cam.global_transform.basis.z
	var at: Vector3 = _view.cube.global_transform.xform(CubeGeometry.cell_to_object(_s.n, _s.pos)) \
			+ toward * 0.62

	var up := _cam.global_transform.basis.y
	# the slow bob, and the squash pivoting near the bottom rather than the
	# centre, so a compressed hole sits down instead of shrinking in place
	at += up * (0.022 * sin(t * 2.2) + lift - rr * (1.0 - sqy))

	_root.global_transform = Transform(
			_cam.global_transform.basis.scaled(Vector3(sqx, sqy, 1.0)), at)

	var d := rr * 2.0
	_hole.scale = Vector3(d, d, d)
	_ring.scale = Vector3(d, d, d)
	_arc.scale = Vector3(d, d, d)

	# the halo swells with the flare; its texture is baked out to 2.1 radii
	var hd := d * 2.1 * (1.0 + flare * 0.333)
	_halo.scale = Vector3(hd, hd, hd)

	_ring.set_colour(Color(Palette.ARC.r, Palette.ARC.g, Palette.ARC.b, min(1.0, 0.95 * rim)))
	_halo.set_colour(Color(1, 1, 1, min(1.0, rim)))

	# brighter over one arc, travelling: the whole of "it is turning"
	_arc.rotation = Vector3(0, 0, t * 0.7)
	_arc.set_colour(Color(0.647, 0.953, 0.988, min(1.0, 0.55 * rim)))


# The trail, handed to Fx so it draws in the same additive pass as everything
# else.
func emit_trail() -> void:
	if _fx == null:
		return
	for q in _trail:
		var a: float = 1.0 - q[1] / TRAIL_MS
		_fx.blob(q[0], 0.32 * a, Color(0.22, 0.84, 1.0, a * a * 0.34))
