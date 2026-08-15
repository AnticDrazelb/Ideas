# SINGULARITY ENGINE — Unity

*A spatial puzzle where you play as a black hole trapped inside a broken
machine. Fold the engine to align its circuits and collapse into the core.*

A Unity port of the single-file web build. Same rules, same cubes, same
numbers — verified, not assumed.

---

## Opening it

Open `unity/SingularityEngine` with **Unity 6 LTS** (pinned to `6000.0.23f1` in
`ProjectSettings/ProjectVersion.txt`; any 6000.x will offer to upgrade it, and
nothing here depends on the patch version). The render pipeline is **Built-in** —
there is no URP asset to go missing, and the shaders are hand-written ShaderLab.

Then press play. The scene is empty on purpose: everything is built in code from
`Bootstrap`, so there is no serialised reference anywhere that can come unhooked,
and the whole project is reviewable in a diff.

On first open the project applies its own settings (`Singularity → Apply Project
Settings` re-runs it). There is no hand-written `ProjectSettings.asset` in the
repository and that is deliberate: it is a seven-hundred-line serialised blob
whose diffs are unreadable and whose merges are unresolvable, so the settings
that matter to this game live in `Assets/Scripts/Editor/ProjectSetup.cs` as a
list with the reasons attached. Unity writes its own defaults for the rest.

The one to know about is **colour space: Gamma, not Linear.** The palette is nine
hex values picked by eye and the board is read off one property — a trace is
brighter than the lattice at every depth. Linear re-maps all of them and the two
depth ramps stop being the distances they were authored to be.

## First run: what to look at, and what to send me

The rules are proved; the presentation is not. So the useful bugs are all in one
half, and these are the ones most likely to be real:

1. **Does the board read?** A trace has to be visibly brighter than the lattice
   at every depth. If it does not, colour space is the first suspect.
2. **Does a fold look like a fold** — a solid turning, not squares sliding?
3. **Is the cube in the right place** — centred between the two HUD bands, not
   under them, in both orientations?
4. **Does a tap land on the cell you tapped**, including mid-fold and under a
   held MATRIX?
5. **Is the glyph set legible at phone size** — node, lock, core, and you?
6. **Does the audio arrive on the beat** — the footstep's ping eighty
   milliseconds behind the thud, the fold's knock at the detent?
7. **The Forge**: build a five-cube, verify, save, play it back.

When something is wrong, the thing that helps most is:

```sh
adb logcat -c && adb logcat -s Unity:V > se.log     # then reproduce, then Ctrl-C
```

plus a screenshot and the device name. Anything the game itself noticed is
prefixed `[Singularity]`. A stack trace beats a description; a screenshot beats
a stack trace for anything about layout or colour.

## Building an APK

```sh
./build-android.sh              # development build
./build-android.sh -release
UNITY=/path/to/Unity ./build-android.sh
```

or in the editor: **Singularity → Build Android APK** (⌘⇧B / Ctrl+Shift+B).
Output lands in `unity/SingularityEngine/Builds/SingularityEngine.apk`; install
it with `adb install -r`.

You need Unity's **Android Build Support** module including OpenJDK and the
SDK/NDK sub-options. The NDK is not optional: the player is IL2CPP so that it
can be ARM64, and IL2CPP compiles through the NDK. It signs with the debug
keystore, so a build needs no secrets to exist.

Both routes apply the project settings first, so a fresh clone on a machine that
has never opened this project builds the same thing as one that has. A build
that depends on somebody having clicked through the editor once is not a build.

> **This has not been run in an editor.** It was written and verified in an
> environment with no Unity install: the rules are proved identical to the
> original by running both engines and diffing, and the whole of `Assets/Scripts`
> type-checks against a stub of the engine API (see `tools/README.md`). What
> that cannot tell you is whether a material reads well or a layout fits a
> phone. Expect first-open notes on presentation, not on rules.

---

## The one idea

The engine is a solid cube of cells. The camera looks down one axis, and **every
cell sharing a screen column collapses to one square — the nearest solid one.
Everything behind it is discarded, not hidden.**

Two decks at opposite ends of the cube are neighbours on screen if their columns
are neighbours, and you may walk between them as if they touched, because on
screen they do. Fold the cube and a different axis becomes depth, so a wall
becomes a floor, a pit becomes a doorway, and the map you were stuck on is a
different map.

Four rules and no fifth:

| | |
|---|---|
| **Tap** | walk. Bright cells are traces; dark ones are lattice; gaps are nothing. |
| **Swipe** | fold. Past halfway it commits, short of it springs back. |
| **Brightness** | is distance. |
| **Folding needs footing** | you may only fold if your own column still has a trace on the far side. |

That last one is the quiet one, and it is where the game lives: **where you
stand decides which folds you have, and which fold you take decides where you
can stand.**

**Plates** are the second verb, from cube 20. Step on one and the cube's
*material* inverts for five seconds: every trace goes dead, every dead cell
lights up. The geometry does not move — what changes is which of it you may
stand on. A plate is cut clean through the lattice and is always the surface of
its column, which means **every fold is legal while you stand on one**. Plates
are pivots.

---

## What Unity changed, and what it did not

The rules did not change at all. Cube 4,127 is the same cube here as in the
browser, byte for byte, and the daily is the same puzzle on the same day.

One thing did get simpler, and it is the reason this port is worth having.

The web build carries **two renderers that have to agree**: a flat grid of rects
when the cube is square to the camera, and an honest 3D solid while it is
folding. Both draw the same front faces from the same numbers, and the moment a
fold lands the first takes over mid-frame so nothing moves. That is careful work
and it is a standing risk — two code paths, one truth.

Unity needs one. An **orthographic camera looking straight down an axis makes
the depth buffer perform the collapse itself**: the nearest solid cell in a
column is the one that gets drawn, which is exactly the rule the solver plays
by. The renderer and the rules cannot drift apart, because they have become the
same statement.

The parallel projection is not a look, it is the rule. A perspective camera
would show you cells the solver says are one square landing on different pixels.

---

## Layout

```
Assets/Scripts/Core/     the rules. No engine reference at all — asserted by the
                         asmdef, which is what lets it be diffed against the
                         original and minted on a worker thread.
  Rng, Ori, Level        the projection model and the collapse
  Solver                 0-1 BFS: a step is free, a fold costs one
  Generator              carve, widen, score, mint
  Vaults, Baked          the ladder, and the ten authored cubes
  Identity, ShareCode    what makes two cubes the same cube, and how one travels
  Validation, Daily

Assets/Scripts/Game/     everything that draws, listens or remembers
  Session                the play state and every rule-level action. Raises
                         events; never touches a mesh.
  GameDirector           turns those events into light and noise
  CubeView, CubeMesh     the solid, and the handedness conversion
  Fx                     the debris and the fronts — one mesh, rebuilt a frame
  Synth, Sfx             every sound in the game, made from two primitives
  Forge                  the editor's model, with no UnityEngine in it
  CameraRig, InputRouter, RemainSolver, LevelSupply, Store, ...
  UI/                    built in code; no prefabs anywhere

Assets/Tests/EditMode/   the port's contract with the original
Assets/Shaders/          Cell (the solid + the reveal), Glyph (the four
                         objects), Fx (sparks, chips, fronts)
tools/                   type-check and parity harnesses — see tools/README.md
```

The split between `Core` and `Game` is the whole architecture. `Session` decides
what is true; `GameDirector` decides what that looks like. Nothing in `Session`
touches a mesh, which is why the rules can be exercised by an edit-mode test
with no scene loaded, and why the port could be proved identical to the original
before a single pixel existed.

---

## Threading

Two things run off the main thread, and both can because `Core` has no engine
dependency to marshal:

- **The cutter.** Minting a cube costs a couple of hundred milliseconds. The web
  original did this on the main thread and measured three stalls per level of
  408, 116 and 587ms — plausibly one and a half to three seconds of frozen game,
  every level, on a mid-range phone. The next cube is cut in the background while
  the current one is played. The synchronous path still exists and is still
  correct, for a cube reached faster than it could be cut.
- **The live "to go" readout.** The fewest folds still possible from where you
  stand, re-solved on every state change, cached so undo is instant.

---

## The character

You are **a hole, and something on the other side of it**.

This had a face for a while — a glowing ball with two eyes, on the sound
argument that a circle with eyes is a character and a glowing ball is an effect.
It is gone, and the reason is worth keeping: a black hole does not have a face,
and hanging accretion rings off one made it a planet. The ring ended up doing
the work of being found, which left the hole itself as decoration on its own
character.

**It is a hole, not a disc.** The horizon is drawn opaque and black, so it
*removes* the deck under it rather than covering it — an absence of board, which
is exactly what a void column is. Stand beside a gap in the cube and the two are
made of the same nothing.

**The edge does the finding.** A photon ring — one thin, very bright circle
right on the silhouette — with a soft lensing halo outside it and a brighter arc
travelling round. On a pale deck that ring is the whole difference between "a
hole in the world" and "a hole in the floor", and it is the only reason a black
object is allowed to be the player marker in a game where dark already means
impassable.

**It reacts without a face.** Everything the character used to say with eyes now
happens as gravity: the rim flares and the halo swells on a node or a gate, and
it flinches on a refusal. A thing with no face that answers what you did is not
less expressive than one with eyes — it is expressive in the only language it
has. It also still **gets bored**, bouncing once on the spot after five and a
half seconds of a player thinking: no sound, no particles, nothing that could be
mistaken for the game asking for something.

The debris around it falls **inward**. Motes are spawned out on a ring and
thrown at the centre with a sideways kick, so what you see is light being pulled
in and going out at the horizon. Same particle system, same cost — the velocity
is simply aimed the other way. A lamp sheds; this is the opposite of a lamp.

---

## The clock bends

A hit that only shakes the camera reads as a camera effect. A hit that stops
time for fifty milliseconds *first* reads as something arriving, because the
frame you were looking at is held long enough for you to notice it did not
continue.

| | |
|---|---|
| a landed fold | 52ms of stop. Enough to feel, too short to see. |
| a refusal | 78ms — longer, and none of the reward. A wall is the one thing that should take a frame away from you. |
| a plate firing | 120ms, then a third of speed easing back over most of a second. The world turning inside out is worth a held breath. |
| the collapse | 150ms and 900, so the board comes apart at the pace of something *ending* rather than something being dismissed. |

Everything that **moves** runs on the bent clock; everything that **measures**
runs on the real one. The solve clock is monotonic and untouched, so a player
cannot buy thinking time by triggering impacts.

---

## The reveal

Worth calling out because it is the one effect that is also a teaching tool.

After every settled change the lit reachable set **sweeps outward from the
player in BFS order** rather than snapping on. The sweep *is* the connectivity
graph being traced, so the player watches the answer to "what did that fold buy
me" get drawn one cell at a time. Juice and teaching, the same effect.

It costs nothing per frame. The geometry only changes when the *world* does, so
a settle rewrites only the vertex colour stream — reachability in red, BFS
distance in green — and the shader compares the distance against a front that
runs out in real time. No re-triangulation, no per-cell draw calls.

---

## Two reads that change nothing

Both are in, and both are worth having precisely because they cost nothing.

**The antipode.** The cell diametrically opposite the one you are standing in —
*through* the centre, not around the outside. If something is sitting there you
can see it faintly through the horizon, and that is all it does: no reach, no
route, no bearing on any fold. A player who never notices has lost nothing; a
player who does has found out what the thing they are moving actually is.

**Near specials.** The four cells one step away, checked for whether one of them
is the core, an uncollected node, or a lock. It tells the eye where the next
useful step is before the hand has to work it out.

---

## The daily's windows

A missed day is not a zero, it is a **miss** — and it costs more than the worst
honest attempt, because if it were free then skipping a hard cube would be the
optimal play. Turning up and playing badly always beats not turning up, and that
is the ordering a habit board has to have.

Week, month and season totals are computed and shown locally; a window with
nothing at all in it scores −1 rather than a wall of penalties, so a fresh
install does not open by charging you for a month it was not installed for.

The one thing that needs a host is the last step. `DailyBoards.Submit` is a
hook, deliberately not a stub of somebody's SDK: wire a ranking service to it
and the packed scores start flowing; leave it alone and the game is exactly as
complete as it is now, minus the ranking. The packing puts the **period above
the score**, so the newest completed window outranks every older one by
construction — a service that keeps only a player's best entry becomes
current-period standings on its own, with no reset and no server-side job.

---

## AAA — three senses, three grades, and every grade is a number

The game speaks in light, in sound and in the motor. A player who cannot take
delivery of one of those three is not playing a harder version of this game,
they are playing a broken one — the plate clock is a five-second timer with no
display to somebody deaf, and a board read off brightness alone is unplayable on
a phone held in sunlight by anybody at all.

So each sense has a grade, each grade is a measurement rather than an intention,
and every measurement is asserted by a test that runs with no Unity install.

| | |
|---|---|
| **APPARENT** | every printed pair reaches **7:1** (1.4.6 AAA); every graphical object reaches **3:1** against its neighbour (1.4.11); nothing is carried by hue alone |
| **AUDIBLE** | everything said with a sound is also said in words; the instrument and the room have separate levels |
| **ATTAINABLE** | every control is **88 units** on its shortest side (2.5.5 AAA, 44 CSS px); every verb has a form that needs no swipe and no hold; motion has an **off**, not a quieter setting |

`Assets/Scripts/Game/Access.cs` is the whole standard: the settings, WCAG's own
arithmetic, a dichromat simulation, and the list of ink-on-ground pairs the
interface actually prints. The audit runs against **the game's palette**, not
against a copy of it, which is what makes it notice a hint printed on a card
instead of on the void.

**The default is the shipped build, exactly.** Nine of the twenty-one printed
pairs were below AAA and two below AA — `Dim2` on the void is **2.03:1**, and
that is the title footer and every field placeholder; black on the primary's
rust is **5.90:1**, and that is the one button every screen is built around.
Those are measurements, and a game's answer to them is a *mode*, not a repaint:
the colours stay matched to the APK value by value, and `LEGIBILITY` moves the
five that have to move. Each new value is the smallest step in lightness, at the
same hue and saturation, that clears 7:1 on all four dark grounds — solved for,
not picked.

Two things came out of writing it down:

- **The band guarantee is true.** "A trace is brighter than the lattice at every
  depth, so the board survives a colour-blind eye" is now simulated rather than
  believed — Viénot/Brettel/Mollon, all three dichromacies, every depth of every
  cube size. It holds, with the narrowest margin under deuteranopia. What it did
  *not* hold was 1.4.11: the worst pair is 2.56:1, so legibility sinks the near
  lattice fifteen per cent and takes it to 3.03:1, widening the game's own band
  from 48 to 59 out of 255.
- **One switch was answering two people.** `EFFECTS` bundled camera shake and the
  bent clock (vestibular) with sparks, bloom and the flash (photosensitive) —
  two different criteria with two different answers — and it bottomed out at 40%,
  so there was no way to ask this game to hold still. They are now `MOTION` and
  `LIGHT`, three steps each, and both reach zero. An old save's `EFFECTS OFF`
  migrates into both rather than into neither.

Also fixed on the way through: the flash is capped at 2.3.1's three a second
(every flash is player-triggered, so a fast hand could outrun it without the game
doing anything wrong); the switch and the slider were 42 and 28 units against a
target of 88; and **a slider had no raycast target of its own**, so a tap
anywhere on the track did nothing at all and the only way to move a value was to
catch a 28-unit disc.

Settings live on `ACCESS`, reached from `CALIBRATE`. Legibility repaints the
interface where it stands rather than rebuilding it — type and surfaces are
walked separately, because the shipped build spends one token on both the quiet
type and the inert track, and only under legibility do those two want to go in
opposite directions.

## Matching the shipped build, exactly

The reference is the APK, and the APK is a WebView wrapper: `assets/game/index.html`
inside it is the whole game. Unzip it and the `:root` block is a complete design
token set — every colour, radius, control height and type step the interface uses.

**One CSS pixel is two canvas units.** The viewport in the WebView is about 360
CSS px wide; the `CanvasScaler` here references 720. That single conversion is all
the arithmetic involved, and it turns "does this look right" into a lookup.

```sh
unzip -o SE.apk -d apk && sed -n '/^:root{/,/^}/p' apk/assets/game/index.html
```

What that has already settled, so nobody re-litigates it:

- **The colours are exact.** All twenty-one, checked value by value.
- The APK's HTML differs from the source this port was written against in
  **two non-visual ways only** — touchscreen-based television detection, and the
  adaptive resolution controller. Both are here and match constant for constant.
  The gap was never a stale source; it was fidelity.
- `--edge` is rust at **.34**, not .70. `--r-btn` is **7px**. `.btn` is
  `h-btn + 6` and `.btn.primary` is `h-btn + 14`. The smallest type in the whole
  interface is `--t-micro`, 8.5 CSS px — **seventeen units here**, and nothing
  may be smaller.
- The primary's glow is **not** camera bloom and never could be: the interface is
  a screen-space overlay and draws after the camera's post pass. In the shipped
  build it is one `box-shadow`. It is drawn.

Still to be measured against the CSS rather than guessed: the Forge editor's band
heights, the Calibrate row rhythm, and the manual's gutters.

### Where it deliberately does not match

Two elements are here that are not in the shipped build. They are listed rather
than smuggled.

**The chassis.** A salvaged steel case, rusted through at the corners and bolted
at four points, with the glass sunk into a machined recess —
`Assets/Scripts/Game/Chassis.cs`. The fiction is that you are inside a broken
machine reading its diagnostics, and every screen already looked like an
instrument panel; it was just an instrument panel floating on nothing. Chrome
with no chassis is a costume.

Two rules keep it honest, and both are load-bearing:

- **No type ever sits on the metal.** The whole access audit is contrast measured
  against four dark grounds, and a rusted steel ground would invalidate every
  pair in it. This is now true by construction rather than by care: the HUD's
  root and every screen's plate are inset to the opening, so there is nowhere on
  the metal that a word *can* go.
- **It is never behind the board.** The canvases are screen-space overlays and
  draw after the camera, so anything painted across the middle paints over the
  cube. The case is twelve pieces — eight corner arms and four sides — and the
  reason it is twelve rather than eight is exactly this: a square piece at each
  corner would be simpler and would hang a clear quad over four corners of the
  board. There is no quad over the middle at all.

**It is the one imported asset in the project, and that is a real exception to a
real principle.** Everything else here is generated — glyphs, frames, glow, icon,
sound — and that has paid for itself many times over. The housing went through
four generated passes: flat grey, corrugated grey, pillowy grey, and a genuinely
decent milled bezel. None of them was the object in the reference, and the reason
is not effort. Rust does not come out of value noise, and neither does forty
years of somebody else's handling.

So the case is a photograph, cut to its own silhouette with the glass taken out
of the middle. The script that cut it and the render it was cut from are both in
`unity/tools/chassis`, because an asset nobody can regenerate is exactly the
unreviewable blob this project has spent its life avoiding. Its import settings
are code too — `Assets/Scripts/Editor/ChassisImport.cs` — for the same reason
there is no `ProjectSettings.asset`, and because Unity's default for a texture
this shape is to resample it to the nearest power of two, which would move every
measurement the layout is built on.

**The bezel is four numbers, not one.** The real object is deeper at the top and
bottom than at the sides, and deeper at the bottom than at the top. `Chassis`
publishes the four insets, `Layout` forwards them, and the HUD, the aperture,
the board rect and every screen's plate are inset by them individually. A single
averaged number put the metal over the plate on two edges and left a band of it
showing on the other two.

**The aperture** — a rounded stroke around the rectangle `Layout` reserves for
the board, in the same `--edge` rust as every control.

The reason is a shape the web build never had to answer. A square cube fitted to
a portrait *width* cannot fill a portrait *height*, so there is always leftover
vertical space, and on a phone it read as a layout that had not reached rather
than as a frame. Every other surface in this interface is a framed plate; the one
thing the game is about had no housing. The stroke costs one nine-sliced quad,
and the four fold marks hang on it — which is also what stopped them being
anchored to the screen edges by hand-written offsets that only happened to land
near the board.

It is a stroke and never a plate: the canvas is a screen-space overlay and draws
after the camera, so anything filled would paint over the cube. The void inside
it stays void.

## What is not here yet

Three things, none of them rules.

Everything else is here: the rules, the three verbs, the vault ladder, the daily
and its streak, the Forge with its verify pass and share codes, the seed box,
the manual, the plate lesson, calibrate (including the brightness and contrast
filters), the win card, the attract cube, the arrive/leave transitions, the
reveal sweep, the cage, the depth readout, the character, time bending, the
particle work, audio, haptics, safe areas and persistence.
