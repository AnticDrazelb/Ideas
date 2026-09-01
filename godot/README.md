# SINGULARITY ENGINE — Godot 3.5

*A spatial puzzle where you play as a black hole trapped inside a broken
machine. Fold the engine to align its circuits and collapse into the core.*

A Godot 3.5 port of the Unity build in [`../unity`](../unity). Same rules, same
cubes, same numbers — verified, not assumed.

```sh
GODOT=/path/to/godot3.5 tools/check.sh
```

Three passes, cheapest first: every file parses, the whole machine comes up, and
the rules still say what the original says.

---

## Opening it

Open `godot/` with **Godot 3.5.3** and press play. The scene is empty on purpose:
`game/Main.tscn` is one node with `game/Boot.gd` on it, and everything else — the
camera, the board, nine canvases and every screen — is built in code, so there is
no serialised reference anywhere that can come unhooked and the whole project is
reviewable in a diff. That is the Unity build's decision too, kept for the same
reason.

The renderer is **GLES2** and the window is 405×720 with stretch **disabled**.
That last one matters: the interface reproduces Unity's CanvasScaler itself, in
`ui/UiCanvasRoot`, so a stretch mode on top of it would scale the picture twice.

## What is different, and why

Everything below is a place where the two engines disagree about something and
the port had to choose. Nothing else was changed on purpose.

**`ui/UiRect` — Y grows the other way.** Unity's UI counts Y upward and Godot's
counts it down, and this game is three thousand lines of measured anchors and
offsets. So the numbers stay exactly as the C# writes them and one node converts
them, once:

```
godot.anchor_left   = unity.anchorMin.x       godot.margin_left   =  offsetMin.x
godot.anchor_right  = unity.anchorMax.x       godot.margin_right  =  offsetMax.x
godot.anchor_top    = 1 - unity.anchorMax.y   godot.margin_top    = -offsetMax.y
godot.anchor_bottom = 1 - unity.anchorMin.y   godot.margin_bottom = -offsetMin.y
```

It also carries Unity's `pivot`, `sizeDelta` and `anchoredPosition`, which Godot
has no equivalent of at all. `InputRouter` makes the same flip at the door for
the same reason: every pointer position in that file is in the original's
convention, because the file is full of signed arithmetic that decides which way
a fold goes.

**The board is drawn offscreen.** Unity hands a camera effect the frame it just
rendered; Godot's unit of "render this and then read it" is a Viewport. So the
whole 3D board lives in one, and what the player sees is that viewport's texture
with the glow added back over it. Every canvas draws on top of that plate.

**The sky needs `BG_CANVAS`.** 2D always draws over 3D inside a viewport, so a
canvas layer at -100 would sit on top of the cube. The board viewport's
environment uses the canvas as its background up to layer -1, which is exactly
the boundary the sky sits below.

**The post pass is one shader.** The C# does brightness and contrast in blended
quads because a Unity camera effect cannot reach screen-space UI — and ships two
bugs doing it. A canvas layer at 200 reading `SCREEN_TEXTURE` reaches everything,
so it is one formula in one place.

**Two real audio buses.** The C# carries an optional mixer seam that does nothing
without an `.asset` no script can create. Godot takes buses at runtime, so the
seam is the only path: INSTRUMENT and ROOM, one delay and one lowpass, one room
instead of forty per-voice filters.

**No lambdas.** GDScript 3.5 has none, and these screens are almost nothing but
closures. Three answers, each where it fits: named methods where the capture is a
small fixed set, `ui/UiAct` where it is a value (a Node holding it, parented to
the control so a FuncRef's weak handle cannot outlive it), and a pair of
field-name tables for the fourteen settings rows, whose get/set pairs were only
ever naming one field of the save.

**No 32-bit wrap.** GDScript integers are 64-bit, so every hash, seed and share
code goes through `core/Bits`, which does the wrapping by hand. `Rng` is the same
mulberry32 the original uses and the fixtures are written as exact numerators
over 2^32, because a decimal literal lands a unit in the last place away from the
value the generator actually produces.

**Known: the vault heading clashes with its arrows.** The rack's title box spans
120–505 of the plate and the two arrows sit at 40–140 and 485–585, so twenty
units of the first and last letter are painted over. The C# has the identical
numbers and the identical draw order; `UiKit.fit` shrinks the string to its box
exactly as it should, and the box is the thing that is too wide. It is
reproduced rather than fixed because it looks the same in both, which is what
this port is for — but it is the original's bug, not the fitter's.

**The case is not inert.** It was twelve pieces of photographed metal that never
changed, on a quarter of the display, in a game where the board is barely half.
Three things reach it now, and none of them is new art:

*It corrodes across the ladder.* `Palette.vault_age` already returns 0→1 across
the ten vaults and the lattice already wears it; the case wears the same number,
so a player ten chapters in is holding a visibly worse machine than the one they
started on and never had to read anything to know it. One line, beside the board's
own palette push.

*Its inner edge takes light from the two events that are about the machine rather
than the board* — a plate turning the world inside out, and the core taking you.
Additive only, on the same argument the glass is built on: a layer that can darken
is a layer that can take a control below its contrast floor, and this one draws
over the housing every screen is mounted in. Scaled by the light setting like
every other flash in the game.

*And the plate's five seconds are a lamp as well as a bar.* The bar is at the top
of the window and the number is under it; neither is where the eyes are, which is
the board. The case is the whole edge of the display and it is in peripheral
vision the entire time.

**The bezel thins on a bigger machine.** The canvas scaler holds the canvas area
constant, which is what makes one set of authored offsets land on every phone —
and it also means the case costs the same 23.5% of the display on a five-inch
handheld and on a thirteen-inch panel. Only the first of those is a machine you
are holding. `Chassis.bezel_factor` is the authored thickness up to 6.5 inches and
halves by 10; a platform that will not report a believable DPI gets the case the
game was built with, because an X server's nominal 96 is not a figure any handheld
reports and guessing from it would thin the bezel on every desktop.

**Known: 4:3 is outside the envelope.** The plate is 1128 canvas units tall and
the scaler holds canvas AREA constant, so a display wider than about 0.68 has
fewer units of height than the layout needs and every screen anchored to the
plate's bottom hangs off it. Six phone shapes from 0.46 to 0.60 lay out with 323
controls and no faults; a 4:3 tablet does not, and `tests/ui.gd` says so once
rather than reporting fifty-four symptoms of it. The original targets a portrait
phone and so does this.

**And the interface is measured rather than eyeballed.** `tests/ui.gd` found
eight faults at the authored size and six more that only appear on other phones.
The ones worth naming:

*`UiKit.fit` could never shrink anything.* A Label that does not wrap reports its
own text as its minimum size, and a Control is never laid out smaller than its
minimum — so a label whose string is wider than its rect quietly grows past it and
`rect_size.x` comes back as the width of the TEXT. Fitting the text to that is
fitting the text to itself: the first candidate always passed. It is invisible at
the authored aspect, where every box is wide enough that the minimum never bites.

*The vault heading is two lines now, because one will not go.* "VAULT III ·
DISTANT NEIGHBOURS" is thirty characters and the gap between the arrows is 262
units on a tall phone, which holds twenty-four at the readable floor. No amount of
shrinking fits it and widening the box takes the room the arrows need. They are
two different facts anyway, and every other screen in the game already sets them
as two lines.

*Both settings panels overflowed every row.* The panel divides what is left into
eight and centres an 88-unit control in each; what is left is 720, so the row is
90, its own insets take it to 86, and every control overflows by a unit at each
end. Invisible at the authored size, and the reason the rows collide outright on a
shorter plate. Sixteen units — six from the air above the feet, ten from the gap
under the sub-heading — makes the row 92 and the control fit inside it.

*Four controls were under the tap target*: the manual's GOT IT and the plate
card's STEP ON IT at 70 units, and the Forge shelf's import row at 66. The Forge
EDITOR is exempt and says why: it is ten bands over a deck that takes what is
left, giving every band 88 costs 172 units, and the deck's own floor is 240 — the
column would run off the plate. A dense tool trades band height for the size of
the thing being edited, deliberately.

*And a hint ran into its own control.* The settings hints stop at a fraction of
the row while the control they must clear is a fixed distance from the edge; on a
wider row the fraction moves and the control does not. They are measured from the
same edge now, and they shrink rather than running on.

**The attract cube is visible.** The C# fills the title's plate with an opaque
black and then lays the two stage ramps over it — so the cube that GameDirector
loads, poses, frames and turns on every visit to the title has never once been
seen. The screen's own note calls it "the best argument the title has" and
explains that the ninety units under the masthead are left empty for it. What was
there was a hole. The ramps already protect the type on their own; the sheet only
had to stop being there.

**Three teaching faults are fixed rather than reproduced.** All three are in the
C# too; they were found by playing the port and are the only places where this
build deliberately behaves better than the one it came from.

*The glass is taught by the Coach instead of by a toast.* Hold anywhere and the
lattice goes to glass — the one gesture that says the board is a solid rather than
a grid of squares, and the only thing on the screen that can answer "where is the
exit" when the exit is behind something. The C# raises one 1.6-second line on cube
three and never again, and it spends the offer BEFORE raising it, so a load
landing during a walk burns the only offer there will ever be on words nobody saw.
Answering a missable prompt with a second missable prompt is not an answer, so it
is a Coach lesson now: the same place the two verbs are taught, a line that stays
up for exactly as long as the exit is out of sight, and retires itself the moment
the player holds. Cube three is still where it first lands, and that was never
arbitrary — measured through `LevelSupply`, cubes one and two draw their core and
cube three does not. The cube is called BURIED.

`Coach.next` carries it, last, behind the rules: a cube that introduces a plate
teaches the plate and the view waits for a cube that is not busy.

*A relocked ladder says what it kept.* Finishing sets `reached` back to one. Every
best, par and time survives and the rack still shows the pips you earned — but the
cubes are shut, and a hundred and fifty locked cards with your own scores on them
have exactly one reading. The C# answers it nowhere; the way back in is the eighth
row of a settings screen. The title now says so, in the two lines that were naming
the cube you are returning to.

*And the front door stops demoting the player who finished.* `SaveData` says the
rule where `runs` is declared — "`reached` is how far along this run you are,
`runs` is whether the machine has ever been finished at all, and only the second
one should change what the title offers" — and then the title tests `reached <= 1`,
so the relock takes the Forge away from the one save that has certainly earned it
and offers the beginner's manual instead. `Screens.never_cleared` asks both.

**One shipped bug is repaired rather than reproduced.** The vault screen's seed
box and its message are written with `offsetMin` and `offsetMax` the wrong way
round in the C#, which makes them rects of negative height — they are not on the
shipped screen at all. `UiRect` exists to make that class of mistake impossible
and would have caught them silently; they are written the right way round instead,
so the guard stays a guard. It is called out in `ui/Screens.gd` where it happens.

## The device turns

The C# answers one shape: a phone held upright. This one answers two, and the
second is not the first stretched.

**The case turns with the device**, because the case is a photograph of the
device. A phone's brow and chin are deeper than its sides — seventy-five and
seventy-seven against forty-seven and a half — and that is a fact about the
object, not about the screen it is pointed at. Turn the phone and the deep bands
are on the left and right. The twelve pieces hang off a frame inside the panel,
so the turn is one rotation of one node: give the frame the panel's height for
its width, spin it a quarter turn clockwise about its own centre, and put that
centre back. Nothing inside it knows.

It is also the way round the layout wants. Turned, height is the scarce axis:
leaving the deep bands top and bottom would spend a hundred and fifty of the
seven hundred and twenty units that are hard to find and ninety-five of the
twelve hundred and eighty that are not.

**The readout becomes a rail.** Two bands across a landscape display spend the
height the board wants and none of the width beside it. The four readings go
two-by-two down the right, then which cube and whose, then the three controls off
the bottom on the thumb's side. Same seven things, same order, stood up. It is
laid out against the height it was GIVEN rather than the one it wanted: sixteen
by nine leaves five hundred and seventy-five units of plate, twenty by nine
leaves five hundred and seven, and twenty-one by nine four hundred and
seventy-eight. What gives at each step is a ranking of what the rail is for — the
three controls never give, the chips shrink first, and the vault's name goes onto
the cube's line last.

**Every screen is two panels**, and it is the same shape each time: what the
screen is about on the left, what you can do about it down the right. The machine
and the words; the rack and the seed box; the deck and the palette; which cube
and what to do about it. The settings panels break into as many columns as the
height needs, the manual breaks at the seam its own writing already has, and the
heading and the three exits share one bar instead of taking three hundred and
ninety-two units off the top and bottom of a six-hundred-unit plate.

**A rotation is a rebuild, not a re-fit.** All of that is decided while a screen
is being built, so the two canvases are thrown away and the other ones are built,
once, on the frame the orientation flips. Nothing in them is state — the state is
in the Store and in the Session, and every screen reads it on the way up — so
what is lost is the fade a card arrives with and whatever is half-typed in the
seed box. The back stack survives.

One number had to stop being a number. The attract cube is framed into the gap
the title leaves for it with a margin that reserves room for a lean, and 1.60 was
measured on the one band that band has ever been: six hundred wide and a hundred
and eighty tall, where the height binds the fit and the width has three hundred
units to spare. A near-square band — 602 by 573 on a sixteen-by-nine display
turned, 504 by 550 on a twenty-one-by-nine held, 357 by 684 on a four-by-three
turned — has no spare axis, so the cube is sized from the width and then leans
into a width it has already used up. The margin is worked out per axis now, from
two measured spreads and the band's own shape, and the reference phone gets the
same 1.60 it always did.

One bug fell out of the whole exercise that had been there since the port.
`CameraRig.fit_to` offsets the camera by the drift between the board's centre and
the screen's, with one sign for both axes; the y sign is right and the x sign is
not, because moving a camera right sends what it is looking at left and the
board's viewport is read with `render_target_v_flip`, which cancels the minus
vertically and not horizontally. Nothing caught it because until the display
could turn, no rectangle in this game was ever off centre horizontally — every
drift ever asked for was vertical.

The one shape neither arrangement answers is between them: a four-by-three tablet
in **portrait**, from an aspect of 0.68 up to the 1.18 where the case calls itself
turned. Too wide for a column of full-width rows, not wide enough for two of them.
The audit says so once rather than reporting fifty symptoms of it.

## If it comes up with no interface

The board draws, the stars draw, and there is nothing on top of them. That is
always the same shape of fault and it is worth knowing how to read it.

GDScript has no exceptions. A failed call prints one error and abandons the
function it was in, so anything after it in the director's boot simply does not
happen — and the boot builds the housing, then the readout, then the screens, in
that order, after the board. One failure in the middle and the player gets a
board with nothing on it.

What the console then shows is a hundred and fifty of the SECOND problem: the
frame loop calling into a readout that was never built, sixty times a second,
with the one line that matters scrolled off the top. So the boot writes down
what it finished and the first frame checks:

```
[Singularity] boot stopped after chassis
```

That names the stage that completed, so the fault is in the one after it. The
first error above that line is the cause; everything below it is noise.

## The class registry

Godot 3.5 resolves every `class_name` through two tables in `project.godot`, and
the only thing that writes them is the editor's project scan — so a headless run
of the tests would be gated on `godot --editor --quit`, which wants a writable
project, re-imports every asset, and on a machine with no display can sit there
indefinitely rather than failing.

`tools/classes.py` writes the same two tables from the source tree, where all
three fields of every entry already are. `project.godot` is therefore a build
product with a generator rather than an editor artefact nobody may touch.

```sh
tools/classes.py            # rewrite in place
tools/classes.py --check    # exit 1 if it would change anything
```

Run it after adding, renaming or moving any file with a `class_name` in it.
`tools/check.sh` runs it first, every time.

## The six harnesses

**`tests/compile.gd`** loads every script individually. Godot's own scan reports
a parse error against whichever file happened to *consume* the broken one, which
sends you to the wrong file; this names the file that is actually wrong.

**`tests/smoke.gd`** runs `Main.tscn` for two hundred and forty frames headless
and asserts the machine is standing afterwards — the director's seven subsystems,
the canvases, and all eleven screens. Nothing is drawn, which is the point:
everything that goes wrong here is a wiring fault, and a wiring fault is the only
class of bug the other two cannot see. It found four on its first run, including
an orphaned CanvasLayer that made the entire interface invisible while reporting
correct sizes for all of it.

Two engine errors during that run are the headless build itself and not the
game: the dummy rasterizer has no render targets, so the two viewport textures
the bloom chain binds resolve to RIDs it never created.

**`tests/play.gd` and `tests/win.gd`** run the game for real, under a virtual
display, and press it. `play` walks, holds for the matrix, tries all four folds,
and visits every screen; `win` asks the session for its own advice — the solver
behind the HINT button — and performs it as gestures until a cube is solved,
which is the only way to reach the collapse, the exit and the win card. Both
drive the pointer through `Input.parse_input_event`, so every gesture goes down
the path a thumb does. Frames land in `user://shots`.

```sh
xvfb-run -s "-screen 0 1280x1024x24" godot --path godot -s tests/win.gd
```

They are not in `tools/check.sh` because they want a display. They found four
bugs in their first ten minutes, three of which drew nothing and said nothing:
a bloom shader that would not compile, a mesh whose every index was thrown away,
and a camera pointed at the empty half of the world.

**`tests/ui.gd`** measures the interface where it is drawn. The Unity harness
checks Layout's arithmetic across device sizes, which is the most it can do
without an engine: it asks whether the numbers add up. This asks the built tree —
every screen is constructed at runtime out of three thousand lines of anchors and
offsets, and the question that decides whether a screen is any good, does this
control land on that word, is a question about rects that exist only once
something has laid them out. It boots the game, visits all eleven screens plus the
readout, and checks four things: a word drawn over a control, every pressable
thing against the tap target it owes a finger, everything inside the plate, and
any rect built with no width or height. Then it walks the attract cube through 240
poses against the band it is allowed to occupy.

It asks for its window size from inside the process and waits until the root
viewport has reported the same rectangle eight frames running before it builds
anything, because `--resolution` is a request made before a window exists and an
X server with no window manager is free to answer with something else — 540x960
came back as 477x847 on one run and 540x960 on the next. It also pins dynamic
resolution: under a software rasteriser the frame budget correctly drops the
render size a fifth of the way through the run, and every screen built before
that point was then measured against a smaller viewport.

**Then it turns the device over and measures again**, in the same process, on the
interface the running game built for the new shape — a rotation rebuilds both
canvases, and a rebuild is exactly the kind of thing that works when it is the
only thing that has ever happened and falls over the second time.

`tools/ui.sh` runs it across the set, one process per shape:

```sh
xvfb-run -s "-screen 0 2400x2000x24" tools/ui.sh
SHAPES="1280x720" tools/ui.sh
```

Twelve shapes from 320x568 to 2560x1080, each measured and then turned and
measured again: 646 controls per shape, no faults. Every fault it found the first
time is listed under "What is different" below; three of them were invisible at
the authored aspect and broke on ordinary phones.

Two of its own blind spots are worth naming, because both let a screen pass
without being looked at. The bounds check had two sides — "no word ever sits on
the metal" has four, and a column that overruns a portrait phone runs out of the
bottom, which had never happened. And the chapter word, the one screen with no
way out drawn on it, took itself down seconds later in the middle of measuring
some other screen; every check iterates over what was gathered, so an empty
screen was indistinguishable from a clean one. A screen that measures nothing is
a fault now.

**`tests/shots.gd`** takes a picture of every screen at any shape, and then again
after turning it over, because the audit can say whether a word lands on a
control and cannot say whether the thing looks like a machine.

```sh
UI_SIZE=1280x720 xvfb-run -s "-screen 0 2400x2000x24" godot --path godot -s tests/shots.gd
```

**`tests/run.gd`** is the rules, against the original's own fixtures: the random
source, all twenty-four orientations, every minted cube's size, par, step count
and canonical id, the whole authored catalogue decoding and solving at its
written par, share codes round-tripping, the day boundary, and the vault
arithmetic in both directions.

## Layout

```
core/      the rules. No engine types at all — this is the layer the fixtures test.
game/      the board, the camera, the effects, the sound, the save, the editor's model.
ui/        the kit, the readout, and every screen that is not the board.
shaders/   nine of them: cells, wire, debris, glyphs, sky, three bloom passes, the filter.
assets/    the catalogue, the case, the glass, the face and its licence.
tests/     compile, smoke, rules, the interface audit, two playthroughs, screenshots.
tools/     the class registry generator, the check script and the shape sweep.
```
