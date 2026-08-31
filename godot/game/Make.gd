class_name Make
# A CLASS MAY NOT NAME ITSELF, AND MOST OF THEM WANT A FACTORY.
#
# Nearly every object in this game is built by a static function on its own class
# — Fx.build, Bloom.attach, Sky.build, ScreenFilter.attach — because the C# does
# it that way and because a constructor with six arguments is worse. GDScript 3.5
# refuses every one of those outright: "using own name in class file is not
# allowed", because `Fx.new()` inside Fx.gd is a class naming itself.
#
# The workaround is always the same — go through the script rather than the name
# — so it is written once here instead of eight times, with the reason attached.
# `Make.of("res://game/Fx.gd")` is `new Fx()` and nothing else.
static func of(path: String):
	return load(path).new()
