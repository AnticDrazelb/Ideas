class_name Bloom
extends Node
# THE BOARD IS LIT, NOT PRINTED.
#
# The web original draws every trace into a second canvas and composites that
# over the frame at the end, and the Unity port did not have it for a while —
# which was most of why the two builds looked like different games from across a
# room. It is not decoration. A trace is not merely a brighter blue than the
# lattice; it is a brighter blue that SPILLS onto what is beside it, and the
# spill is what makes a grid of squares read as something powered.
#
# It also does a specific job for the marker. The photon ring is one thin bright
# circle a couple of pixels wide, and a couple of bright pixels on a phone is a
# hairline — until it blooms, at which point it is an edge with light coming off
# it, which is the entire claim that object makes.
#
# QUARTER RESOLUTION, AND THAT IS NOT A COMPROMISE. A bloom is a low-frequency
# signal by construction: resolving it finely spends samples on detail the effect
# exists to destroy. A sixteenth of the pixels, four blur taps, and on a
# mid-range phone it is comfortably inside a frame.
#
# THREE VIEWPORTS WHERE THE C# HAS THREE BLITS. Unity hands a camera effect the
# frame as a RenderTexture and lets it ping-pong; Godot's unit of "render this
# through a shader" is a Viewport, so the chain is knee, across, down — the same
# three passes at the same quarter resolution, reading the world viewport that
# the board is drawn into.
#
# IT COMPOSES BEFORE THE BRIGHTNESS FILTER so the two land in the order the
# player expects: the glow is part of the picture, and turning the brightness up
# raises the picture including its glow.

# Where light starts, and how much of it comes back.
#
# These were 0.62 and 1.15, which put the knee below the trace colour's own
# brightness and then added more than the original back on top — so every trace,
# every cage edge and every particle bloomed at nearly full strength and the
# board came out as overlapping halos with the cells lost inside them. A glow
# that swallows the thing it is glowing off is not a glow.
#
# The knee sits above the lattice and inside the trace, so the two materials are
# still separated by the one property the whole board is read off — and the
# amount is well under one, because this is a rim on the picture rather than a
# second copy of it.
#
# The knee is public because one thing outside this file has to stay under it: a
# star in the sky behind the board. See Sky.STAR_PEAK.
const THRESHOLD := 0.78
const INTENSITY := 0.45

# HOW FAR THE KNEE COMES DOWN WHILE THE MACHINE IS BEING LOOKED THROUGH.
#
# 0 is the settled board, where a glow that swallows the thing it is glowing off
# is the failure mode and the numbers above are tuned against it. 1 is a held
# MATRIX, which is the opposite picture: the fill is gone, what is left is wire,
# and a wire with no bloom is a hairline. The knee drops under the wire colour
# and the amount goes past one, so the lines blow out where they cross — which is
# not decoration, it is what makes a lattice of them read as depth rather than as
# a flat tangle.
#
# Driven by the hold, scaled by the light setting, and it is the ONLY part of the
# schematic that is: the x-ray itself is the readout MATRIX exists to give, so a
# player who has turned light down loses the halo and keeps the answer.
var lift := 0.0

const LIFT_THRESHOLD := 0.40
const LIFT_INTENSITY := 1.10

# How many halvings before blurring. Two is quarter res on each axis.
const DOWN := 2

var _world: Viewport
var _knee: Viewport
var _blur_x: Viewport
var _blur_y: Viewport
var _mat_knee: ShaderMaterial
var _mat_blur_x: ShaderMaterial
var _mat_blur_y: ShaderMaterial
var _mat_add: ShaderMaterial
var _plate: TextureRect
var _size := Vector2.ZERO


static func attach(under: Node, world: Viewport, board_layer: CanvasLayer) -> Bloom:
	var b = Make.of("res://game/Bloom.gd")
	b.name = "Bloom"
	b._world = world
	under.add_child(b)
	b._compose(board_layer)
	return b


func _compose(board_layer: CanvasLayer) -> void:
	_knee = _stage("knee", "res://shaders/bloom_knee.shader")
	_mat_knee = _knee.get_child(0).material

	_blur_x = _stage("blurX", "res://shaders/bloom_blur.shader")
	_mat_blur_x = _blur_x.get_child(0).material
	_mat_blur_x.set_shader_param("dir", Vector2(1, 0))

	_blur_y = _stage("blurY", "res://shaders/bloom_blur.shader")
	_mat_blur_y = _blur_y.get_child(0).material
	_mat_blur_y.set_shader_param("dir", Vector2(0, 1))

	# THE PICTURE THE PLAYER ACTUALLY SEES: the world's own texture with the glow
	# added back. Everything else in the game draws over this.
	_plate = TextureRect.new()
	_plate.name = "board"
	_plate.expand = true
	_plate.stretch_mode = TextureRect.STRETCH_SCALE
	_plate.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_plate.anchor_right = 1.0
	_plate.anchor_bottom = 1.0
	_mat_add = Shaders.material("res://shaders/bloom_add.shader")
	_plate.material = _mat_add
	board_layer.add_child(_plate)


# A VIEWPORT'S TEXTURE, TOLD TO INTERPOLATE. It has to be asked for after the
# viewport is in the tree — the texture does not exist before that — and it is
# asked for on every stage of the chain including the world, because the last
# upscale is as much of the picture as the taps are.
static func filtered(vp: Viewport) -> void:
	var t := vp.get_texture()
	if t != null:
		t.flags = Texture.FLAG_FILTER


func _stage(stage_name: String, shader_path: String) -> Viewport:
	var vp := Viewport.new()
	vp.name = stage_name
	vp.usage = Viewport.USAGE_2D
	vp.render_target_v_flip = false
	vp.render_target_update_mode = Viewport.UPDATE_ALWAYS
	vp.transparent_bg = false
	vp.disable_3d = true
	add_child(vp)
	# AND IT IS FILTERED, WHICH IS NOT A PREFERENCE — IT IS WHAT THE BLUR IS.
	#
	# See bloom_blur: five reads for a nine-wide kernel, and the trick is that a
	# tap sitting 1.3846 texels out is fetched by the hardware as a weighted pair
	# of texels. A ViewportTexture arrives with filtering OFF, so every one of
	# those taps was snapping to whole texels and the kernel was a comb with
	# holes in it rather than a Gaussian — and then a quarter-resolution comb was
	# blown back up four times, nearest, over the picture.
	#
	# What that looked like was a blocky second copy of every lit cell, offset
	# and stair-stepped, with vertical banding through the bright ones. Reported
	# as ghosting, and it was: the glow was a low-resolution ghost of the board
	# because nothing in the chain was allowed to interpolate.
	filtered(vp)

	var rect := ColorRect.new()
	rect.name = "pass"
	rect.anchor_right = 1.0
	rect.anchor_bottom = 1.0
	rect.material = Shaders.material(shader_path)
	vp.add_child(rect)
	return vp


# Whether the picture gets a second copy of itself laid over it at all.
func refresh() -> void:
	var on := Access.bloom()
	if _plate != null:
		_mat_add.set_shader_param("amount", 1.0 if on else 0.0)
	_knee.render_target_update_mode = Viewport.UPDATE_ALWAYS if on else Viewport.UPDATE_DISABLED
	_blur_x.render_target_update_mode = _knee.render_target_update_mode
	_blur_y.render_target_update_mode = _knee.render_target_update_mode


func tick() -> void:
	if _world == null:
		return

	var full := _world.size
	if full != _size:
		_size = full
		var q := Vector2(max(1, int(full.x) >> DOWN), max(1, int(full.y) >> DOWN))
		_knee.size = q
		_blur_x.size = q
		_blur_y.size = q
		_mat_blur_x.set_shader_param("texel", Vector2(1.0 / q.x, 1.0 / q.y))
		_mat_blur_y.set_shader_param("texel", Vector2(1.0 / q.x, 1.0 / q.y))
		# A RESIZE MAKES A NEW RENDER TARGET, so the flag is set again here as
		# well as at build — this branch is the one that runs when the device is
		# turned over, and an unfiltered chain after a rotation would be the same
		# bug arriving late.
		filtered(_world)
		filtered(_knee)
		filtered(_blur_x)
		filtered(_blur_y)
		_knee.get_child(0).material.set_shader_param("src", _world.get_texture())
		_blur_x.get_child(0).material.set_shader_param("src", _knee.get_texture())
		_blur_y.get_child(0).material.set_shader_param("src", _blur_x.get_texture())
		_plate.texture = _world.get_texture()
		_mat_add.set_shader_param("board", _world.get_texture())
		_mat_add.set_shader_param("bloom", _blur_y.get_texture())

	var k: float = clamp(lift, 0.0, 1.0) * Access.light_amount()
	_mat_knee.set_shader_param("threshold", lerp(THRESHOLD, LIFT_THRESHOLD, k))
	_mat_add.set_shader_param("intensity", lerp(INTENSITY, LIFT_INTENSITY, k))
