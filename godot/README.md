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

**One shipped bug is repaired rather than reproduced.** The vault screen's seed
box and its message are written with `offsetMin` and `offsetMax` the wrong way
round in the C#, which makes them rects of negative height — they are not on the
shipped screen at all. `UiRect` exists to make that class of mistake impossible
and would have caught them silently; they are written the right way round instead,
so the guard stays a guard. It is called out in `ui/Screens.gd` where it happens.

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

## The three harnesses

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
tests/     compile, smoke, rules.
tools/     the class registry generator and the check script.
```
