class_name Platform
# THE ONE QUESTION THIS GAME ASKS THE DEVICE ABOUT THE PERSON HOLDING IT.
#
# The save loader carried this comment for as long as it has existed:
#
#     EFFECTS DEFAULT TO WHAT THE DEVICE ALREADY ASKED FOR. A player who has set
#     "reduce motion" at the OS level has already told every app they open how
#     much shake they want, and making them find a switch to say it again is not
#     a preference, it is a tax.
#
# and under it, in the C#, this line:
#
#     Data.fx = SystemInfo.deviceType == DeviceType.Handheld ? 1 : 1;
#
# Both branches are one. The conditional decides nothing, the OS is never asked,
# and every fresh install starts at full motion however the person has set their
# phone up. A comment promising an accessibility behaviour over code that does
# not do it is worse than neither, because it is the reason nobody goes looking.
#
# Android has no "reduce motion" switch as such. What it has — and what every
# accessibility guide points at, and what the platform's own animations honour —
# is the three animation scales under Developer options and Accessibility.
# Setting any of them to zero is how a person on Android says "stop moving
# things", and `animator_duration_scale` is the one that governs in-app
# animation rather than window transitions.
#
# IT IS ONLY EVER A DEFAULT. It seeds the setting on a save that has never been
# written; it is not consulted again, and it never overrides a choice the player
# has made in CALIBRATE.
#
# AND A FAILURE IS A SHRUG. The same rule Store's persistence follows and the
# same one Haptics follows: a device that will not answer leaves the default
# exactly where it was.

const _ASKED := []


# Has the person asked their device to stop animating? Cached, because it is
# read once at load and cannot change what it seeds afterwards.
static func animations_off() -> bool:
	if _ASKED.empty():
		_ASKED.append(_ask())
	return _ASKED[0]


static func _ask() -> bool:
	# GODOT 3.5 HAS NO WAY TO READ Settings.Global FROM GDSCRIPT, and that is
	# worth stating rather than pretending. The C# reaches it through
	# AndroidJavaClass; the equivalent here is a GDNative or Android plugin, which
	# is a build artefact this project deliberately does not have — everything
	# else in it is source you can read in a diff.
	#
	# So the honest answer on a device is "not asked", and the honest answer
	# everywhere else is the same one the C# gives on a desktop. The seam is
	# `override` below: a plugin that can answer this sets it at boot and every
	# consumer is already written against it.
	return false


# For the harness, which has no device to ask, and for a platform plugin that
# can answer where GDScript cannot.
static func override(off) -> void:
	_ASKED.clear()
	if off != null:
		_ASKED.append(bool(off))
