class_name Cutter
extends Reference
# THE THING A THREAD CAN BE POINTED AT.
#
# Godot's Thread.start takes an object and a method name, and a static class is
# neither — so the work a C# lambda closed over lives on this instead. One
# instance per cut, holding the level number it was asked for and the mutex and
# queue it hands the answer back through.
#
# It is the whole reason a cut is not a stall. Minting costs real time, and the
# web original did it on the main thread about two seconds into every level:
# three stalls of 408, 116 and 587ms per load, measured. On a mid-range phone
# that is plausibly one and a half to three seconds of completely frozen game,
# every level, for the whole game. Nothing else about the craft survives that.

var level := 0
var gate: Mutex
var done: Array
var in_flight: Dictionary
var thread: Thread


func _init(level_in: int, gate_in: Mutex, done_in: Array, in_flight_in: Dictionary) -> void:
	level = level_in
	gate = gate_in
	done = done_in
	in_flight = in_flight_in


func run(_arg) -> void:
	var lv = null
	lv = Generator.mint(level)
	gate.lock()
	in_flight.erase(level)
	# A cube that arrived while the player was already past it is still worth
	# keeping: the cache is keyed by level, not by when it was asked for, and the
	# next visit is then free.
	if lv != null:
		done.append([level, lv])
	gate.unlock()
