class_name HudMetrics
# THE NUMBERS THE HUD AND THE COACH BOTH HAVE TO AGREE ABOUT.
#
# In the C# these are constants on Hud, and Coach reads them while Hud reads
# Coach's line height back — a ring the compiler there does not mind and Godot
# refuses to load. They are also, honestly, not the HUD's: they describe the
# stack of things that live in the clear band inside the stroke, and three files
# put something in it.
#
# THE FIRST CLEAR UNIT INSIDE THE STROKE, AT EITHER END.
#
# Four lines used to live in the dead plate outside the window — the plate clock
# and the sound caption above it, the toast and the coach's line below — and
# there is no dead plate any more. So they come inside, and inside starts past
# the fold mark: the arrow's centre is FOLD_EDGE in and its glyph reaches half a
# box either side of that. Plus eight, which is the air between a mark and a
# word.
#
# MEASURED FROM THE STROKE RATHER THAN FROM THE GLASS, and hung on the aperture
# rather than on the root, so the whole stack travels with the window when the
# display turns or the bands change height. The numbers under it were
# glass-relative literals, which is the same second definition of the window that
# the stroke itself used to have.

# The fold arrow's box, in canvas units. The glyph draws to its edges.
const FOLD_ICON := 30.0

# HOW FAR THE ARROW'S CENTRE STANDS IN FROM THE APERTURE'S EDGE.
#
# It was half a tap target — forty-four units — because the mark and the
# pressable plate were one position, and the plate is what has to fit. That put
# the arrow twenty-nine clear units inside the stroke, floating in the window
# rather than sitting on the frame, and reading as four things beside the board
# instead of four marks on it.
#
# Half the glyph plus six, so the arrow's tip stands off the stroke by the width
# of the stroke and no more. The plate keeps its own number — see Hud.FOLD_SLOT —
# and the glyph is offset back out to here inside it.
const FOLD_EDGE := FOLD_ICON * 0.5 + 6.0

const MARK_BAND := FOLD_EDGE + FOLD_ICON * 0.5 + 8.0

# The plate clock's line, and the caption's under it.
const CLOCK_HEIGHT := 24.0
const CAPTION_GAP := 8.0
const CAPTION_HEIGHT := 36.0
const CAPTION_TOP := MARK_BAND + CLOCK_HEIGHT + CAPTION_GAP

# The coach's one line, under the board.
const COACH_LINE_HEIGHT := 38.0

# The toast's foot, up from the stroke: past the coach's line, which is the one
# thing between it and the down fold's arrow. The box above it is slack for a
# second line — the text is set from the BOTTOM.
const TOAST_FOOT := MARK_BAND + COACH_LINE_HEIGHT + 8.0
const TOAST_BOX := 80.0
