extends Node
class_name UiAct

# A CLOSURE, WITH A NAME AND A LIFETIME.
#
# Every screen in the original is written with lambdas: a card that opens ITS
# level, one of three stops that sets ITS index, a slider that writes ITS field
# and repaints ITS reading. GDScript 3.5 has no lambdas, and a FuncRef is called
# with the control's own argument and nothing else — so the captured value has to
# live somewhere.
#
# It lives here. One of these per control, holding what the lambda would have
# closed over, calling one named method on one object with it.
#
# AND IT IS A NODE ON PURPOSE. A FuncRef holds its target by object id rather
# than by reference, so a Reference with no other owner is collected the moment
# the builder returns and the button quietly stops working. Parented to the
# control it belongs to, it lives exactly as long as the thing that can press it
# and goes when that goes — which is also what makes rebuilding the vault rack
# free of leaks.

var target: Object = null
var method := ""
var arg = null


# Pressed with nothing to say: a button.
func press():
	if target == null:
		return null
	return target.call(method, arg)


# Pressed with a value: a switch reports the state it wants to be in, a slider
# reports its number. The bound value goes first, because it is the one the
# reader of the call site is thinking of.
func pass_through(v):
	if target == null:
		return null
	return target.call(method, arg, v)


# Build one and hang it on the control it belongs to. `owner` must already be in
# the tree's shape — a slot rect built a line earlier is the usual caller.
# Make.of, because a class may not name itself in its own file — see Make.
static func on(owner: Node, target_in: Object, method_in: String, arg_in = null) -> UiAct:
	var a: UiAct = Make.of("res://ui/UiAct.gd")
	a.name = "act"
	a.target = target_in
	a.method = method_in
	a.arg = arg_in
	owner.add_child(a)
	return a
