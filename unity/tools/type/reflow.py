#!/usr/bin/env python3
"""
DOES THE PROSE STILL FIT? — the measurement behind the typeface swap.

The interface used to render in Unity's builtin Arial and now renders in
JetBrains Mono. Uppercase got narrower and lowercase got wider, and the only way
to know which of the fifteen prose boxes in the game that breaks is to wrap each
one against the real advance widths of both faces and compare the result to the
height the box was given.

This is not a preview and it does not draw anything. It answers one question per
box — how many lines, how tall, does it overflow — for the same reason the
chassis has cut.py next to it: a number in a source comment that nobody can
reproduce is a number nobody can check.

WHAT IT MODELS, AND WHERE IT IS APPROXIMATE. Unity's Text does greedy word wrap
on spaces with forced breaks at \\n, and stacks lines at the font's hhea line
height; both are reproduced exactly. What is approximate is that Unity
rasterises through FreeType at an integer pixel size, so a real advance is the
hinted integer rather than the scaled design unit — a fraction of a percent per
glyph, which can move a wrap by one word on a line that was already within a
word of the edge. Treat a box that fits by less than a line's worth as unproven
and render it.

    python3 reflow.py                # both faces, every box
    python3 reflow.py 0.9            # ... with Text.lineSpacing applied

Needs fonttools. The comparison face is Liberation Sans, which is metrically
what LegacyRuntime.ttf is; point it somewhere else with LIB= if your machine
keeps it elsewhere.
"""
import os
import sys
import statistics
from fontTools.ttLib import TTFont

HERE = os.path.dirname(os.path.abspath(__file__))
JBM = os.environ.get("JBM", os.path.join(HERE, "..", "..", "SingularityEngine",
                                         "Assets", "Resources", "mono.ttf"))
LIB = os.environ.get("LIB", "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf")


class Face:
    """Advance widths and line height, in ems, straight out of the tables."""

    def __init__(self, path, name):
        self.name = name
        f = TTFont(path)
        upem = f["head"].unitsPerEm
        h = f["hhea"]
        self.line = (h.ascent - h.descent + h.lineGap) / upem
        hm = f["hmtx"].metrics
        self.adv = {chr(cp): hm[gn][0] / upem
                    for cp, gn in f.getBestCmap().items() if gn in hm}
        self.miss = hm[".notdef"][0] / upem if ".notdef" in hm else 0.5

    def width(self, s, size):
        return sum(self.adv.get(c, self.miss) for c in s) * size


def wrap(face, text, size, width):
    """Unity's greedy wrap: break on spaces, never break a word, honour \\n."""
    out = []
    for para in text.split("\n"):
        if not para:
            out.append("")
            continue
        line = ""
        for word in para.split(" "):
            trial = word if not line else line + " " + word
            if face.width(trial, size) <= width or not line:
                line = trial
            else:
                out.append(line)
                line = word
        out.append(line)
    return out


# THE BOXES, AS THE SCREENS ACTUALLY DECLARE THEM.
#
# Widths are the label's rect after its offsets, in canvas reference units: the
# plate is 720 less the chassis inset either side, which is 625. A manual entry
# then takes 44 of margin and leaves a 190-unit gutter for its diagram, so its
# body measures 391. Heights are the box, not the type.
#
#   name, text, point size, box width, box height
BOXES = [
    # The title's three-line pitch used to be measured here. It has been removed
    # from the screen — the masthead closes on its rule and the turning cube is
    # the argument — so there is nothing left to wrap.

    ("manual ONE FACE AT A TIME",
     "Two traces that line up when you fold are connected, however far apart they look.",
     17, 391, 104 - 32),
    ("manual SWIPE TO FOLD",
     "Past halfway it commits. The ticks say which folds have footing.", 17, 391, 88 - 32),
    ("manual TAP TO MOVE",
     "The singularity walks to any trace connected to the one under it.", 17, 391, 88 - 32),
    ("manual COLLAPSE INTO THE CORE",
     "Reach it and the vault is solved.", 17, 391, 62 - 32),
    ("manual HOLD FOR MATRIX",
     "The lattice goes to glass. Keep holding and drag to turn it.", 17, 391, 88 - 32),
    ("manual PLATES INVERT",
     "Every trace goes dead and every dead cell lights up for five seconds. "
     "A plate is always footing.", 17, 391, 102 - 32),
    ("manual TO GO",
     "The fewest folds still possible from where you stand.", 17, 391, 82 - 32),
    ("manual colophon",
     "JetBrains Mono, by the JetBrains Mono Project Authors, under the SIL Open "
     "Font License 1.1. The licence ships with the game.", 17, 537, 76),

    ("plate pitch",
     "Everything you know how to do is about to be worth less.", 20, 529, 72),
    ("plate THE LATTICE INVERTS",
     "Every trace goes dead. Every dead cell lights up. Nothing moves — "
     "only what carries current.", 19, 529, 78),
    ("plate IT LASTS FIVE SECONDS",
     "Then the lattice springs back and puts you on the nearest plate. "
     "Running out costs you the walk, never the vault.", 19, 529, 78),
    ("plate TAP IT AGAIN",
     "A plate is a door between two versions of the same engine, and the core "
     "is usually only reachable in one of them.", 19, 529, 78),
    ("plate IT ALWAYS GIVES FOOTING",
     "Cut clean through the lattice, visible from every face. While you stand "
     "on a plate every fold is legal.", 19, 529, 78),

    ("forge coach", "TRACE IS WHAT YOU STAND ON. TAP CELLS TO LAY 8 MORE.", 19, 471, 58),

    # THE VAULT RACK'S CARDS. Five columns of a 545-unit grid is 109 a card, less
    # six of inset each side: ninety-seven units to hold a cube's name, over a
    # band that is 0.36 of a 113.5-unit card — forty-one units, two lines.
    #
    # THE SIZE IS AN OUTPUT OF THIS TABLE, NOT AN INPUT TO IT. At seventeen the
    # name still does not fit even wrapped: TURNED INSIDE OUT breaks into three
    # lines in a 97-unit box and needs sixty-seven. Fifteen is the largest size at
    # which every name the game can print comes out at two lines or fewer. Both
    # the baked names and the generated ones are here, longest first, because the
    # generator makes CLOSE CROSS out of word lists nobody thinks to check.
    ("vault card · NOTHING UNDERNEATH", "NOTHING UNDERNEATH", 15, 97, 41),
    ("vault card · TURNED INSIDE OUT", "TURNED INSIDE OUT", 15, 97, 41),
    ("vault card · THE FAR SIDE", "THE FAR SIDE", 15, 97, 41),
    ("vault card · TWO SHADOWS", "TWO SHADOWS", 15, 97, 41),
    ("vault card · CLOSE CROSS", "CLOSE CROSS", 15, 97, 41),
    ("vault card · OUBLIETTE", "OUBLIETTE", 15, 97, 41),

    # ACCESS — the two hints that print beside three named stops rather than a
    # switch, so they get half the row instead of most of it. These truncated by
    # two units under the mono and are the reason `hintTo` moved to 0.56.
    ("access MOTION hint", "SHAKE · ZOOM · CLOCK", 17, 260, 24),
    ("access LIGHT hint", "SPARKS · BLOOM · FLASH", 17, 260, 24),
]

# A LABEL IS NOT PROSE AND THAT IS EXACTLY HOW IT GETS AWAY.
#
# Everything above is a sentence in a box, which is the shape of thing somebody
# thinks to measure. The bugs have all been in the other shape: one word in a
# slot, where the slot is a fraction of a row somebody wrote down once and the
# word is four characters longer than it looks because the button adds brackets.
#
# The three stops on ACCESS overflowed for exactly that reason — the note that
# sized the row measured NONE and shipped [ NONE ]. So the labels are measured
# too, as the STRING THAT IS DRAWN, and the widths are the arithmetic that
# produced the slot rather than a number copied out of it.
#
#   name, text, point size, slot width
ACCESS_PANEL = 625 - 2 * 28              # the plate, less the ACCESS panel inset
ACCESS_STOPS = ACCESS_PANEL * (1 - 0.58) - 18   # `steps` runs 0.58 -> 1, less 18
ACCESS_SLOT = ACCESS_STOPS / 3 - 6       # worst case: the middle one loses both gutters

LABELS = [
    ("access MOTION · FULL", "FULL", 17, ACCESS_SLOT),
    ("access MOTION · LESS", "LESS", 17, ACCESS_SLOT),
    ("access MOTION · STILL", "STILL", 17, ACCESS_SLOT),
    ("access LIGHT · FULL", "FULL", 17, ACCESS_SLOT),
    ("access LIGHT · LESS", "LESS", 17, ACCESS_SLOT),
    ("access LIGHT · NONE", "NONE", 17, ACCESS_SLOT),

    # THE TITLE'S PRIMARY, WHICH HAS THREE TEXTS AND ONLY EVER SHOWS ONE. It is
    # the widest button in the game — the whole 625 less the column's 48 a side —
    # and the label fills the plate edge to edge, so the slot is that number
    # exactly. THE CORE is the state a finished machine sits in and it had never
    # been measured against anything.
    ("title primary · INITIALISE", "[ INITIALISE ]", 30, 625 - 96),
    ("title primary · CONTINUE", "[ CONTINUE ]", 30, 625 - 96),
    ("title primary · THE CORE", "[ THE CORE ]", 30, 625 - 96),

    # and the four under it, two to a row out of the same column
    ("title quad · DAILY", "[ DAILY ]", 24, (625 - 96) / 2 - 6),
    ("title quad · VAULTS", "[ VAULTS ]", 24, (625 - 96) / 2 - 6),
    ("title quad · FORGE", "[ FORGE ]", 24, (625 - 96) / 2 - 6),
    # the same slot before the first cube is cleared — see Screens._makeQuad
    ("title quad · MANUAL", "[ MANUAL ]", 24, (625 - 96) / 2 - 6),
    ("title quad · CALIBRATE", "[ CALIBRATE ]", 24, (625 - 96) / 2 - 6),

    # CALIBRATE's own three-up feet, which are Bracketed and so carry them
    ("calibrate foot · MANUAL", "[ MANUAL ]", 24, (625 - 80) / 3 - 12),
    ("calibrate foot · BACK", "[ BACK ]", 24, (625 - 80) / 3 - 12),
    ("calibrate foot · MENU", "[ MENU ]", 24, (625 - 80) / 3 - 12),

    # PAUSE, whose three-up is the same shape in a narrower column
    ("pause · RESET", "[ RESET ]", 24, (625 - 144) / 3 - 12),
    ("pause · VAULTS", "[ VAULTS ]", 24, (625 - 144) / 3 - 12),
    ("pause · MENU", "[ MENU ]", 24, (625 - 144) / 3 - 12),

    # and its two links, which share the row half and half — MANUAL was added
    # because the rules were reachable only from inside the settings screen
    ("pause link · MANUAL", "MANUAL", 20, (625 - 144) / 2),
    ("pause link · CALIBRATE", "CALIBRATE", 20, (625 - 144) / 2),

    # THE COACH'S TWO LINES, under the window rather than on it. The box is the
    # aperture's own width, and the aperture is a SQUARE cut out of the glass now
    # — so the narrowest it ever gets is the tallest phone, where the square is
    # limited by a width the chassis has already taken 95 units out of. 21:9 at
    # 1080x2520 is the worst of the shapes LayoutChecks measures: 862 pixels at
    # 1.7185 units per pixel, less the stroke's own inset, is 470.
    ("coach · TAP TO MOVE", "TAP TO MOVE", 22, 470),
    ("coach · SWIPE TO FOLD", "SWIPE TO FOLD", 22, 470),

    # THE FORGE'S TEN TOOLS. Five columns of a 545-unit band, four units of
    # margin each side — a hundred and one units apiece, and [ WIPE DECK ] is a
    # hundred and nine. It printed [ WIPE DEC and this list is why it will not
    # again.
] + [
    ("forge tool · " + t, "[ %s ]" % t, 14, (625 - 80) / 5 - 8)
    for t in ("VOID", "LATTICE", "TRACE", "PLATE A", "PLATE B",
              "START", "EXIT", "NODE", "LOCK", "WIPE")
] + [
    # and the four that run down the right of the same screen
    ("forge · VERIFY", "[ VERIFY ]", 24, (625 - 80) / 2 - 6),
    ("forge · SAVE", "[ SAVE ]", 24, (625 - 80) / 2 - 6),
    ("forge · DELETE", "[ DELETE THIS CUBE ]", 22, 625 - 80),
    ("forge · size", "[ 5 ]", 24, (625 - 80) * 0.28),
]


def main():
    spacing = float(sys.argv[1]) if len(sys.argv) > 1 else 1.0
    jbm, lib = Face(JBM, "JetBrains Mono"), Face(LIB, "Liberation Sans")

    print("line height per em:  builtin %.3f   imported %.3f   (x%.3f)"
          % (lib.line, jbm.line, jbm.line / lib.line))
    if spacing != 1.0:
        print("Text.lineSpacing %.2f applied to the imported face" % spacing)
    print()
    print("%-30s %-16s %-16s %s" % ("", "builtin", "imported", "box"))

    over = []
    for name, text, size, bw, bh in BOXES:
        a, b = wrap(lib, text, size, bw), wrap(jbm, text, size, bw)
        ha = len(a) * size * lib.line
        hb = len(b) * size * jbm.line * spacing
        flag = "" if hb <= bh else "  OVER"
        if hb > bh:
            over.append(name)
        print("%-30s %2d ln %5.1f      %2d ln %5.1f      %3.0f%s"
              % (name[:30], len(a), ha, len(b), hb, bh, flag))
        if len(b) != len(a) or hb > bh:
            for ln in b:
                print("       | %s" % ln)

    print()
    print("%-30s %-16s %s" % ("", "imported", "slot"))
    for name, text, size, sw in LABELS:
        wd = jbm.width(text, size)
        flag = "" if wd <= sw else "  OVER"
        if wd > sw:
            over.append(name)
        print("%-30s %6.1f wide     %5.1f%s" % (name[:30], wd, sw, flag))

    print()
    if over:
        print("%d of %d overflow: %s"
              % (len(over), len(BOXES) + len(LABELS), ", ".join(over)))
        return 1
    print("all %d prose boxes and %d labels fit" % (len(BOXES), len(LABELS)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
