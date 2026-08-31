class_name Grade
# THE TWO GRADES, IN A FILE OF THEIR OWN.
#
# Access owns what these MEAN — how much a camera impulse is multiplied by, what
# the light channel is allowed to draw — and the save owns the numbers. Both have
# to name them, which put Access and Store in a ring Godot refuses to load: an
# enum on Access that Store reads, and a field on Store that Access reads.
#
# A constant that two things share belongs to neither of them. Same argument as
# B64, one layer up.
#
# TWO DIFFERENT PEOPLE ARE ASKING FOR TWO DIFFERENT THINGS, and one switch called
# EFFECTS was answering both of them badly. Camera shake, the zoom punch and the
# bent clock are VESTIBULAR: they move the frame. Sparks, bloom and the
# full-screen flash are PHOTOSENSITIVE: they change the brightness of the whole
# picture, which is a different criterion (2.3.1) with a different answer.

enum Motion { FULL = 0, REDUCED = 1, STILL = 2 }
enum Light { FULL = 0, REDUCED = 1, NONE = 2 }
