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
    Css                  the shipped :root, ported clamp for clamp
    Flow                 the centred column every .layer is
    UiKit                every control, built from its own CSS rule
    Trk                  letter-spacing, which uGUI does not have
    Svg, Art             the shipped drawings, and the rasteriser for them
    Screens, Hud, ForgeScreens

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

## Matching the shipped build, exactly

The reference is the APK, and the APK is a WebView wrapper: `assets/game/index.html`
inside it is the whole game. Unzip it and the `:root` block is a complete design
token set — every colour, radius, control height and type step the interface uses.

```sh
unzip -o SE.apk -d apk && sed -n '/^:root{/,/^}/p' apk/assets/game/index.html
```

**One canvas unit is one CSS pixel**, and there is no conversion factor at all.
Chromium defines one CSS px as one Android dp, so a canvas at
`ConstantPixelSize` with the device's own density makes a canvas unit a dp makes
it a CSS px, and the stylesheet applies verbatim — `clamp()`, `vw` and all.

That replaces an earlier arrangement worth naming, because the arithmetic was
wrong in a way that reads as right. The canvas was 720×1280 reference units with
`matchWidthOrHeight = 0.5`, alongside the claim that one CSS pixel was two
canvas units. Those two statements cannot both hold: a 0.5 match is a geometric
blend of the width fit and the height fit, so the canvas is 720 units across
only on a device whose aspect is exactly 720:1280. On an ordinary 1080×2400
phone the scale factor is √(1.5 × 1.875) ≈ 1.677, the canvas comes out 644 units
wide, and the conversion is 1.79 — every metric in the interface landing about a
tenth small, by a different amount on every device.

**And the tokens are clamps, not numbers.** Every size in that `:root` is
`clamp(min, Nvw, max)`. Writing down the single value each one produces at a 360
CSS px viewport is right on exactly one device: `--t-micro` is 8.5px at 360 wide
and 10px — its ceiling — on anything past 455, and `--h-btn` moves six points
across the same range. So `Css.cs` ports the clamps and re-evaluates them against
the real viewport, including the `html.tv` ladder for a docked display.

What the APK has already settled, so nobody re-litigates it:

- **The colours are exact**, checked value by value. `:root` carries 26
  colour-valued tokens — 20 hex and 6 rgba.
- The APK's HTML differs from the source this port was written against in
  **two non-visual ways only** — touchscreen-based television detection, and the
  adaptive resolution controller. Both are here and match constant for constant.
  The gap was never a stale source; it was fidelity.
- `--edge` is rust at **.34**, not .70. `--r-btn` is **7px**, and there are six
  radii in the sheet rather than one. `.btn` is `h-btn + 6` and `.btn.primary` is
  `h-btn + 14`. The smallest type in the interface is `--t-micro`, floored at
  8.5 CSS px, and nothing may be smaller than whatever that clamp is returning.
- The primary's glow is **not** camera bloom and never could be: the interface is
  a screen-space overlay and draws after the camera's post pass. In the shipped
  build it is one `box-shadow`. It is drawn.

### What the sheet turned out to be made of

Four things the interface is built from that this port did not have at all, and
each of them changes every screen rather than one:

- **Tracking.** Every rule in the sheet carries a `letter-spacing` — `.26em` on
  the wordmark down to `.055em` on a cube's name, with `.1em` as the body's
  floor. uGUI has no such property, so it is applied to the generated mesh in
  `Trk.cs`, alignment re-done per line the way a browser measures one. Monospace
  capitals set solid read as a serial number; the tracking is most of what makes
  them read as an instrument's label.
- **The typeface.** The sheet's stack is eight fixed-pitch faces and then
  `monospace`. This port was asking for `LegacyRuntime.ttf` — Unity's built-in
  *proportional* face — behind a Courier fallback that could never be reached,
  because the builtin never returns null.
- **The column.** A `.layer` is a centred flex column with two elastic
  pseudo-elements that split the leftover height, so a screen composes the same
  on a tall phone and a short one and scrolls when it does not fit. The port was
  placing children at absolute offsets from the top of a 1280-unit canvas, which
  is that composition transcribed at one screen size. `Flow.cs` is the rule
  instead of one of its outputs.
- **The drawings.** Seven manual diagrams, four object tiles and twelve icons,
  all inline SVG. They are carried over as the shipped path data in `Art.cs` and
  rasterised by `Svg.cs`; round joins and caps fall out of measuring distance to
  the skeleton, which is exactly the shape `stroke-linejoin:round` asks for.
  There is not one bezier in the shipped artwork — every curve is a circular arc.

Still to be measured against the CSS rather than derived: the Forge editor's
coach band, and the landscape (`html.vpLand`) grid the title screen switches to.

## What is not here yet

Three things, none of them rules.

Everything else is here: the rules, the three verbs, the vault ladder, the daily
and its streak, the Forge with its verify pass and share codes, the seed box,
the manual, the plate lesson, calibrate (including the brightness and contrast
filters), the win card, the attract cube, the arrive/leave transitions, the
reveal sweep, the cage, the depth readout, the character, time bending, the
particle work, audio, haptics, safe areas and persistence.
