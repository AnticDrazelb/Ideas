class_name Spec
extends Reference
# What a vault asks of a cube. Every field is a pure function of the level
# number.

var n := 0
var glyphs := 0
var glyph_kinds := 0

# WHICH WORLD-CHANGING CELLS THIS CUBE MAY CARRY, as the characters themselves:
# "A", "AB", "E", "ABE". Empty keeps glyph_kinds' behaviour, which is what every
# shipped spec_for asks for and what the whole generated catalogue was minted
# under.
#
# It exists because glyph_kinds is a COUNT and the everter is not the third of
# anything — a cube that must teach the far side has to be able to demand one
# rather than hope a three-way roll lands on it.
var glyph_set := ""

# TWO SOLIDS, NOT ONE. A trigger exchanges the voxel array rather than
# re-reading it, so a cube that carries one needs a second array for the carve
# to continue into. One means the cube has a single layout and behaves exactly
# as every cube in the game has until now.
var layouts := 1

var turns := 0          # the CARVE's ambition, not the acceptance band
var locks := 0
var density := 0.0
var leg_min := 0
var leg_max := 0
var par_lo := 0
var par_hi := 0         # the band the solver's answer has to land in
var min_steps := 0
var tries := 0
var decoys := 0
var band := 0
var level := 0
