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
there is no URP asset to go missing, and the two shaders are hand-written
ShaderLab.

Then press play. The scene is empty on purpose: everything is built in code from
`Bootstrap`, so there is no serialised reference anywhere that can come unhooked,
and the whole project is reviewable in a diff.

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

You are a black hole with a face, and the face is not decoration — it is the
only thing in the game that reacts to the *player* rather than to the rules,
and it does three jobs at once.

**It looks where you are going.** Ahead while walking, down while folding, back
to centre otherwise: a free readout of what you just asked for, delivered by
something you are already watching.

**It blinks** — ninety milliseconds closed, then a fresh irregular wait, so it
never falls into a rhythm you can hear ticking.

**And it gets bored.** Every five and a half seconds of a player thinking, it
bounces once on the spot. No sound, no particles, nothing that could be mistaken
for the game asking for something — just a small sign of life in the corner of
the eye of somebody staring at a puzzle. That is the whole of "surprise and
delight" in about eight lines: a character that does something when you do
nothing.

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

## What is not here yet

One thing, and it needs a server.

- **Ranked boards.** The BOARDS screen reads every local record and is worth
  opening on a plane, which is most of what it was for — but the daily's
  week/month/season rollups compute a ranking that has nowhere to go without a
  host. The arithmetic is ported in `Daily`; posting it is a backend, not a port.

Everything else is here: the rules, the three verbs, the vault ladder, the daily
and its streak, the Forge with its verify pass and share codes, the seed box,
the manual, the plate lesson, calibrate (including the brightness and contrast
filters), the win card, the attract cube, the arrive/leave transitions, the
reveal sweep, the cage, the depth readout, the character, time bending, the
particle work, audio, haptics, safe areas and persistence.
