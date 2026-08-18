# SINGULARITY ENGINE — Unity

*A spatial puzzle where you play as a black hole trapped inside a broken
machine. Fold the engine to align its circuits and collapse into the core.*

A Unity port of the single-file web build. Same rules, same cubes, same
numbers — verified, not assumed.

- **[DESIGN.md](DESIGN.md)** — what the game is about, measured: whether the
  central idea is load-bearing in the content, where the difficulty ladder stops,
  and eversion, the mechanic that answers it.
- **[tools/README.md](tools/README.md)** — the harness that runs without Unity.

```sh
dotnet run --project tools/UnityStubs             # every check
dotnet run --project tools/UnityStubs content     # the difficulty curve
python3   tools/type/reflow.py                    # does the type still fit, prose and labels
```

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

## If the build is black after the splash

**This is almost certainly the shaders, and it is the empty scene's one real
cost.** Every shader here is reached by `Shader.Find` and *nothing in the project
references any of them* — no scene objects, no materials, no prefabs. Unity
decides what to include in a player by reference, so a shader with zero
references is a shader that does not ship. In the editor every asset is
loaded, so `Shader.Find` works and there is no symptom at all; in a player it
returns null, every material is built on nothing, and the game runs perfectly
while drawing nothing.

```sh
adb logcat -c && adb logcat -s Unity:V | grep Singularity
```

You want the line `shaders missing from this build: …`. Then:

1. **Singularity → Apply Project Settings** — `EnsureShaders` adds every one of
   them to Always Included Shaders.
2. Rebuild.

`build-android.sh` and the editor build item both apply project settings first,
so a fresh clone gets this without anyone knowing it was ever a problem. A build
made before this fix existed will not.

The game now says so on screen rather than going black, in Unity's own UI shader
— the one thing that cannot itself be the missing piece.

**This is the exact class of bug the stub harness can never catch.** It is not a
missing member or a wrong overload; every call is correct and the asset simply is
not there to be found. *A stub proves the shape of a call, never the existence of
the thing being called* — this is that sentence collecting.

## If the screen is a flat white sheet, or the orange has gone blue

Turn **CALIBRATE → BRIGHTNESS** and **CONTRAST** back to 100 and it will come
back. That is the screen filter, and it had two bugs worth writing down because
the shape of each recurs.

**The blue one is an inversion.** `Blend DstColor Zero` multiplies; the filter
was written with a literal `4` for `DstColor`, and four is `OneMinusDstColor`,
so every quad inverted the screen instead of scaling it. It fired the instant
either slider left 100 and the rust interface came out blue.

The check that walks this arithmetic *could not see it*, because it held the
same literal — both halves faithfully computed an inversion and agreed to the
last bit. **A number that has to match something outside this program has to be
read from that thing, not copied.** The game names `UnityEngine.Rendering.BlendMode`
now, so in a build the value comes from Unity and cannot be wrong; the stub's
transcription of that enum is pinned in `FilterChecks` against the documented
values, which is the only copy left and the only one worth reviewing.

**The white one is a decomposition.** The filter is the formula
`out = in*b*c + 0.5*(1 - c)`, performed in blend modes across a stack of overlay
quads. It shipped once with that decomposed wrongly —
a gain of `c` and an offset of `(b - 1)` — which turns the brightness control
from a multiply into an addition. At the top of its travel that adds 0.6 to every
pixel of a game whose background is 0.05, so every dark value in the palette
collapses into the same white and the result looks like a corrupted framebuffer
rather than like a setting.

Nothing about it was visible in a diff, and no compiler could have had an opinion.
What catches it is `FilterChecks`, which writes out the blend stage — factor times
source, factor times destination, and the clamp between every pair — and compares
the result to the camera shader it replaced at all 8181 slider positions × 256
input levels. It agrees exactly. Change the decomposition and it says so, in units
of 255.

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
7. **The Forge**: build a five-cube, verify, save, play it back. Follow the coach
   from an empty grid to a saved cube without reading anything else — that is the
   one path it exists for, and it was broken: the coach never mentioned decks, so
   doing exactly what it said produced a flat cube the validator refused.

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

## Assets, prefabs and scenes are allowed

This project was written under a rule it inherited from the web build: **no
files.** One HTML page, offline, nothing to download — so every texture, every
sound and every glyph is generated at runtime, the scene is empty, and there is
not a prefab in the repository. Most of what follows in this file argues for
that, at length, and it was right when the deliverable was a single page.

**It is not the rule any more.** Assets, prefabs and scenes are permitted, and
where they produce a better result they are preferred. The bar is the result,
not the purity.

Two things survive the change, because they were never really arguments about
files:

- **A diff has to stay readable.** A seven-hundred-line YAML blob whose merge
  conflicts cannot be resolved is a bad artifact whatever it contains. Where a
  thing can be expressed as code with the reasons next to the values, it usually
  still should be — that is a preference now rather than a law, and it loses to
  a materially better result.
- **An asset nobody can regenerate is a liability.** The chassis is imported and
  the script that cut it and the render it came from are both in the repository,
  which is what makes it reviewable. That standard holds for anything that gets
  added.

**And the first thing it bought was a typeface, which turned out to be a bug
report.** Every screen in this game is described as speaking in a monospace face
— `UiKit.Mono`, `--mono`, "the same monospace the rest of the interface speaks".
It did not. `Mono` returned `LegacyRuntime.ttf`, which is Unity's built-in
**Arial**; the Courier New fallback beneath it only fires if that is missing,
and it never is. So ninety-four labels, every chip, every bracketed button and
every number in the HUD rendered in a proportional face, and the design language
had been describing something the game did not do since the port was written.
Not a decision — the only font available without an asset.

### The face

`Assets/Resources/mono.ttf` is **JetBrains Mono 2.304**, under the SIL Open Font
License 1.1. The licence travels with it twice: `mono-licence.txt` sits beside
it in `Resources`, so it is inside the shipped player and not only in this
repository, and the manual carries a colophon naming the authors — gated on the
font having actually loaded, so it cannot credit a face the game is not drawing
in. The builtin stays underneath as a fallback, because `Resources.Load`
returning null would otherwise mean a `Text` with no font, which draws nothing
at all on every screen at once.

**What the swap actually cost, measured rather than guessed.** Across the
thirty-one single-line literal labels the median line got *narrower* — ×0.963 —
because uppercase is where a proportional face is widest and a monospace is not.
Lowercase prose went the other way, up to ×1.5, and the line box is taller too:
1.32em against Arial's 1.15. Both together broke exactly one screen, the plate
lesson, where four bodies carrying **authored line breaks** turned into four
lines each in a box built for two. The fix is the one the manual had already
had: delete the breaks — they were placed where a *proportional* face ran out of
plate — and let it wrap. Three lines, a row pitch of 130 instead of 118, and the
column still ends 180 units clear of the button.

### Changing the case

**Singularity → Chassis.** Pick a picture of a machine, place the opening
against a live overlay, press Cut; it writes `Assets/Resources/chassis.png` and
`chassis.asset` together. The twelve measurements the housing is built from used
to be `const`s in `Chassis.cs` with a comment saying a Python script had printed
them — true, and a trap, because the numbers and the picture they describe could
drift apart with nothing to notice. They are measurements of an asset, so they
live with the asset now, and `Chassis` falls back to the shipped case's numbers
if the file is missing.

The cut itself is a port of `tools/chassis/cut.py` into `ChassisCut.cs`, with no
`UnityEngine` types in it, and the harness proves the two agree **byte for byte
across all 1.87 MB** of the shipped asset. It also proves the crop is found
rather than typed: the silhouette's own bounding box is the same four numbers
`cut.py` carried as a hand-measured literal.

What it will not pretend to do is find the opening. On a mock-up with the
machine's screen turned *on* — which is every useful reference — the glass is
not reliably darker than the metal around it, and each automatic detector tried
against this one found a chamfer groove or the foot. So that stays yours, seeded
from the current proportions, with the case drawn assembled underneath so a bad
corner or a shallow band is visible before anything is written.

### The scene stays empty, and you can still see it

`Main.unity` has no GameObjects in it. Everything is built in code at
`RuntimeInitializeOnLoadMethod`, so there is no serialised reference anywhere
that can come unhooked, and that is worth more than anything a hand-authored
scene would buy. **This did not change, and it should not.** A `.unity` full of
GameObjects is a file nobody can review in a diff, two people cannot merge, and
which drifts out of agreement with the code that expects it — and the specific
version of that failure this project would hit is a camera or a canvas
serialised with settings that no longer match the ones `GameDirector` sets.

What the empty scene actually costs is the thing people mean when they ask for a
real one: **you cannot see any of it in the editor.** Does the case fit this
aspect, does the glass line up with the bezel, do the scanlines land inside the
opening — every one of those needs a play session, and the Game view sits there
showing nothing.

So **Singularity → Housing → Show** populates the scene on demand, from the same
code the game runs: chassis, scanlines and glass, built by their own `Build`
methods, under one root. Drag the Game view to any aspect and the housing
re-fits live. Everything it creates is `HideFlags.DontSave`, so it cannot be
serialised into `Main.unity` by somebody pressing Ctrl-S with the preview up —
the empty scene stays empty by construction rather than by care.

The runtime needed exactly one change for this: `[ExecuteAlways]` on the two
`Fit` components, so they lay out when the panel's size changes in edit mode as
well as in play mode. Nothing in the preview reimplements any of the layout,
which is the point — a preview that is a second implementation is a preview that
can be right while the game is wrong.

### Sound: a recording, if there is one

Every cue in the game is synthesised — two primitives in `Synth`, layered at
measured offsets, and the offsets are what make a thud plus a ping read as sonar
rather than as two beeps. That was the only option while importing an asset was
against the rules, and it is also the ceiling: there is no synthesised eighty
milliseconds of servo that sounds like a servo does.

So there is a seam. Drop a clip at `Assets/Resources/Audio/<name>` and that cue
plays the recording; leave it out and it synthesises itself, exactly as now.
Nothing is all-or-nothing — a recorded footstep and fifteen synthesised cues is
a valid state and probably the state this passes through. The names are in
`Bank.Cues`, and each cue names its own file at the point it would otherwise
synthesise, so a cue that grows a fourth layer next year does not need anybody
to remember a table exists. Two of them take more than one file: `node2`…`node6`
if you want the chime to keep climbing per node taken, and `bed0`…`bed29` if you
want the room's hum tuned per vault the way the synthesis tunes it.

**The mixer has to be made by hand, once.** `Bus` looks for
`Assets/Resources/Audio/mixer` and routes the dry voices, the wet voices and the
bed through it; without one it multiplies volumes per source exactly as before,
and the harness runs in that state. It is not in the repository because an
`AudioMixer` **cannot be created from script** — `AudioMixerController` is
internal to the editor, so nothing supported makes a group or exposes a
parameter. Five minutes of clicking:

1. `Assets/Resources/Audio/mixer.mixer` — Create → Audio Mixer.
2. Two groups under Master, named exactly **Instrument** and **Room**.
3. Right-click each group's Volume → *Expose to script*, and rename the exposed
   parameters to **Instrument** and **Room** as well. This is the step that
   half-works if you skip it: the routing is fine and the faders do nothing,
   which reads as a broken slider rather than as a missing one.
4. Put the reverb on **Room** — one effect on one bus.
5. Put a **limiter on Master**, and take `Sfx.Headroom` back to 1.0 when you do.

**The headroom is not optional and it is currently a constant.** Nobody had
added up what the game's loudest moment sums to: a fold over a plate — the
densest overlap the rules allow, and legal *by design*, since "every fold is
legal on a plate" is how the manual sells them — is five layers plus three,
each also sent to a wet voice, over a humming bed, with both faders at 150%.
That comes to **1.10 against a mixer that clips hard at 1.0**. `Sfx.Headroom`
trims the whole bus by 0.85, which preserves every ratio in the mix exactly and
costs 1.4 dB. A limiter on Master is the real answer, because it also catches
the layer somebody adds next year; the constant cannot. `SoundChecks` prints
the number every run.

That last point is most of why this is worth doing. Without a mixer the room is
twenty wet voices each carrying its own `AudioEchoFilter` and its own
`AudioLowPassFilter`: forty DSP nodes, on a phone, to approximate one send. It
also makes the two faders **decibels instead of a multiply**, so half is six down
rather than half, and it makes them audible *while they move* — a level applied
at play time cannot reach the bed that is already humming.

### The save is the only thing a player cannot get back

The board is deterministic, the settings take a minute to redo, a cube can be
built again. Thirty vaults of bests are a hundred hours somebody spent, there is
no server holding a copy, and there is no support channel to recover them from.

The save path had never been executed by anything — the harness stubbed
`PlayerPrefs` to forget and `JsonUtility` to return the default, so "does a
corrupt save recover" was not a question that could be asked. Both are real now,
and three defects came out of asking it:

- **A corrupt save was destroyed, not ignored.** The `catch` replaced it with an
  empty one and the next write — at most a quarter-second later, because the
  debounce fires on the first setting the game touches — put the empty one over
  the top. No backup, no copy, no message.
- **A null list crashed the launch.** `"bestK": null` is valid JSON, and
  `Rehydrate` was outside the try. The game did not fail to load a save, it
  failed to start.
- **Valid JSON of the wrong shape lost data with no exception at all.**
  `JsonUtility` does not object to a missing field, so nothing anywhere could
  see it.

Now: `turnkey-v2-prev` holds the last string that parsed and is tried when the
live one fails; `turnkey-v2-unreadable` holds whatever could not be read and is
**never written over**, including by a second bad launch. `Store.LoadedFrom`
says which of the three the game is running on, and the HUD says so out loud —
starting somebody over without a word is the worst thing this code can do, and
it is what a player assumes happened any time a vault list looks wrong.

Both fixes are **mutation-tested**: removing the shape check reproduces the
launch crash, removing the quarantine guard fails the second-bad-launch case.
A check that passes whether or not the guard exists is not a check, and the
first version of the quarantine test was exactly that.

### A share code is the save format, not a share feature

`Forge` stores a made cube as its **code** and decodes it again every time the
cube is opened. So a round trip that loses anything does not cost a player a
message they sent to a friend — it costs them the cube they built, out of their
own shelf, silently, the next time they press it.

The existing test covered four generated cubes and five fields. The harness now
mints **120**, checks every field, and asserts the code **re-encodes to itself**
— and prints the voxel census, because a round-trip test proves nothing about a
state its sample never contains. It turns out to be fine: 101 of the 120 carry a
plate, so `A` and `B` were covered. All 120 are byte-identical.

`Decode` is solid — checksum, version, bounds on `n`, on the pair count, and on
every read. `Encode` was not: it wrote `keys.Count` and then indexed `doors[i]`
underneath it, so a level with one more node than lock threw an
`ArgumentOutOfRangeException` **out of a share button**. Not reachable through
the Forge, which places and removes them in pairs — reachable by the next thing
that builds a `Level`. It writes the minimum of the two now, which is lossless
for every valid level, so **every code already in the wild is unchanged**.

### The setting said none and something still flashed

`Hud.Flash` is careful: it scales by the light setting, refuses a fourth inside
a second, and NONE turns it off outright. `Hud.Vignette` was one line with no
gate on it at all — so a player who set the light channel to **NONE**, which is
the setting a photosensitive player is told to use, still got the whole screen
pulsing to 42% black on every refused fold and every dead end.

It is scaled now rather than switched, like `Flash`, so REDUCED gets a subtler
one instead of a cliff. It is deliberately **not** in the three-per-second
budget: 2.3.1's threshold is a *pair* of opposing luminance changes of at least
a tenth of maximum, and this is a darkening on a screen that is already nearly
black. Sharing the budget would let a burst of refusals spend the flash
allowance on vignettes and leave a genuine flash unable to fire.

Nothing is lost by gating it — every event that raises a vignette also raises a
sound, a caption and the orb's mood.

### The design argument lives in DESIGN.md

Whether the game's own idea is load-bearing in its content, where the difficulty
ladder stops and why, which mechanics were measured and which were rejected, and
what eversion is — all of it is in **[DESIGN.md](DESIGN.md)**, with the command
that reproduces every number.


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
  UI/                    built in code (a preference now, not a rule)

Assets/Tests/EditMode/   the port's contract with the original
Assets/Shaders/          Cell (the solid, the reveal and the x-ray — two passes
                         over one vertex stage, shared via Cell.cginc), Glyph
                         (the four objects), Fx (sparks, chips, fronts)
tools/                   type-check and parity harnesses — see tools/README.md
```

The split between `Core` and `Game` is the whole architecture. `Session` decides
what is true; `GameDirector` decides what that looks like. Nothing in `Session`
touches a mesh, which is why the rules can be exercised by an edit-mode test
with no scene loaded, and why the port could be proved identical to the original
before a single pixel existed.

### Two rules the screens keep getting wrong

**A band that is switched off gives its space back.** Every column in this game
is a cursor running down from the top, which cannot overlap by construction —
but a band whose *contents* are hidden while its height stays reserved is a hole,
and the hole comes out of whichever band wanted the room. The Forge's DELETE row
has nothing to delete until the cube has been saved once and its share code is an
empty bordered gap until VERIFY proves one; together that was 138 units of
nothing under the buttons, taken straight out of the deck. The win card had the
same shape of bug at the other end. Both flow at *show* time now, not build time.

**A grid of cells is square because the thing it represents is.** The Forge's
deck was anchored as fractions of a band 545 wide and 333 tall, so a five-cube
drew cells 109 across and 67 high. Every cell you place is a cube; a squashed
one is a lie about where the trace is going to be. The grid is the largest square
the band will hold, and the spare width is the price.

### And measure the string that is drawn

`reflow.py` measured prose — sentences in boxes, which is the shape of thing
somebody thinks to check. Every type bug so far has been in the other shape: one
word in a slot, where the slot is a fraction someone wrote down once and the word
is four characters longer than it looks because `UiKit.Bracketed` renders
`[ LABEL ]`. All six stops on ACCESS overflowed, ran under each other and out
through the side of the panel — and the note that had sized that row claimed the
slots were "more than `[ NONE ]` needs", on the strength of having measured
`NONE`. The tool measures labels now, as the drawn string, against the
arithmetic that produced the slot.

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
| the collapse | 150ms and 470, so the core taking you lands at the pace of something *ending*. It stops there on purpose — the exit that follows runs in real time. |
| the engine breaking | 80ms, half a second later. The whole picture holds — solid still whole, grit stopped mid-air — and then all of it moves at once. |

Everything that **moves** runs on the bent clock; everything that **measures**
runs on the real one. The solve clock is monotonic and untouched, so a player
cannot buy thinking time by triggering impacts.

---

## The schematic

A held MATRIX used to *thin* the board: the fill went to 22%, the rim came up,
and the far side of each cell appeared as edges. What it could not do was show
you what was **behind** — the depth buffer performs the projection, so every cell
in a column but the nearest was already gone before the fragment shader had an
opinion. "The lattice goes to glass" was a promise the renderer could not keep.

It keeps it now, and not by relaxing the surface pass — by drawing a **second
piece of geometry**. `CubeMesh.BuildWire` emits one twelve-edge wire box per cell
for **every cell in the volume, the empty ones included**, and `Singularity/Wire`
draws them additively with no depth test at all.

It has to be its own mesh, and that is the interesting part. `CubeMesh.Build`
meshes away every face with something next to it — right, because the inside of a
solid is not visible from anywhere — so a cell buried in the lattice has *no
vertices*, and a void has none by definition. A schematic drawn from face borders
can only ever outline the walls that already happened to exist. It cannot draw a
complete cell, and it cannot draw the space at all.

**The space is the point.** A void is not absence in this game, it is the route:
the corridors are what you fold to line up. Drawing the whole n³ volume as a faint
lattice and the material as brighter boxes on top says where the matter *isn't* —
which is the question somebody holding MATRIX is actually asking, and the one
thing the board has never been able to answer, because a void and the outside of
the cube looked identical.

**The surface pass never stops obeying the projection**, and that is the whole
licence for this. The wire draws nothing but lines, and a line is not a surface —
`Singularity/Cell` still decides what the board *is* and still obeys the depth
buffer to the letter. This only says where the machine has material.

It shares the surface's vertex stage through `Cell.cginc`, and it has to: the
arrival wave, the eversion and the exit burst all move cells individually, and a
wire that did not travel with the cell it belongs to would come off it the moment
anything moved. The class of each cell rides in on vertex **alpha**, because the
other three channels are rewritten by the reach pass on every settle and this one
is a property of the cube rather than of the moment.

Four things come on together:

| | |
|---|---|
| the tile stops being a tile | corner radius → 0.004 and the rim inset → 0.020, so a rounded circuit plate becomes a sharp wire box. Same arithmetic, two ends of one lerp. |
| the fill goes | 94% out, not 78%. A fifth of a fill is still a surface, and a surface is what stops the far side reading as far. |
| the cage becomes the brightest line | rust → the trace's wire colour at full hold. The outline of the object should not read as less present than its contents. |
| the bloom knee drops under the wire | 0.78 → 0.40 and the amount past one, so the lines blow out where they cross. That is not decoration: it is what makes a lattice of them read as depth rather than a flat tangle. |

The schematic has its own two colours — cool where the housing is rust, because
a drawing in the same ink as the object is a drawing nobody can tell apart from
it. They still separate the way everything here separates: `WireTrace` is 0.26 of
luminance above `WireLattice`, which is wider than the board's own band gap of
0.169 and asserted rather than trusted. The lattice wire also sits 31° of hue off
the everter's violet, because an everter's glyph is drawn over it.

Only the **bloom** is gated on the light setting. The x-ray is the readout MATRIX
exists to give, so a player who has turned light down loses the halo and keeps
the answer.

It also fires for half a second in the middle of an **eversion** — `_Peek` has
always meant "the material is out of the way", and the flip already used it. The
machine turning through itself, seen as wire.

The three strengths are the whole balance and they are an order of magnitude
apart on purpose. Every cell draws a box, so the empty ones are by far the most
numerous thing on screen — that is the point — and they have to sum to a faint
field rather than a wall.

| per box, at full hold | near | far | stacked before white |
|---|---|---|---|
| empty | 0.027 | 0.007 | 37 |
| lattice | 0.083 | 0.022 | 12 |
| trace | 0.342 | 0.089 | 2 |

Worst case is a solid 8-cube with a ray straight through it: 0.42, which just
reaches the lifted bloom knee. Real cubes are about half void, and a ray only
accumulates the edges it actually crosses rather than whole boxes, so those
numbers are a bound and a loose one. Two coincident near trace wires clipping to
white is intended — that is the bright line where two trace cells meet.

*Unverified on device.* The dials are the three `_Gain*` values and `_WireFar` in
`CubeView.PushPalette`, and `WireHalf` in `CubeMesh` for how far the boxes sit
apart.

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

## The exit

The win used to shrink every cell to a point, running out from the core. It was
correct and it was quiet: the board did not come apart, it was switched off one
cell at a time — which is what you do to make room for a card, and is exactly
how it read.

It is thrown now, in three moves.

| | |
|---|---|
| **lean** | 0.40s. The solid turns into three quarters and the camera eases back off it. Nothing breaks yet: an explosion only lands if the eye has just been told the thing is three-dimensional and made of parts. It is the one moment in the game where the board is looked *at* rather than played. |
| **break** | One frame with everything on it — kick, shake, a *negative* punch that snaps the frame outward instead of in, the rumble, the crack — then 80ms where the whole picture holds still together. |
| **throw** | 1.30s. Every cell straight out from the core, tumbling about an axis of its own, staggered by **distance from the core** so the break travels outward as a front. The camera holds back while they go. The card arrives into an empty room afterwards. |

**Nothing is drawn over it.** The collapse, the shatter and the perfect line each
used to throw an expanding square front and a circular one — three more shapes
arriving in the same half-second as the shape that matters, which is the machine
coming apart into its own cells. The rings are gone from all three. The exit is
the board; anything on top of it is competing with it.

The throw is the eversion's machinery pointed somewhere else, which is the
argument for having built that per cell in the first place: the board was
already made of individually addressable cubes, so the explosion is four
uniforms in `Cell.shader` and **nothing on the CPU moves at all**.

At the shipped stagger the cell on the core leaves immediately and the eight
corners hold until the throw is 35% done. The ease is *squared*, not cubed: at
the third power a cell was two thirds of the way out a third of the way through
its own throw, which reads as a thing that has already happened rather than a
thing happening. Every cell lands exactly at the end
whatever the spread, because the front is scaled up by it and each cell
subtracts its own share.

Two things it deliberately does *not* do. It does not raise `_Peek` — MATRIX
takes the board to glass so you can see through it, and debris you can see
through is not debris. And it does not run on the bent clock: the collapse's
slow-motion is shortened to 470ms so it is over before the break, because the
geometry is on the unscaled clock and `Fx` is on the bent one, and the debris
crawling while the cells it came off flew is the one way this can look wrong.

---

## The rack, and a rect that draws nothing

Two bugs on one screen, and they are the same bug seen from either end: a number
that was right for a layout that no longer exists.

**The rack was three columns wide.** That was chosen when the early vaults held
ten cubes. Every vault holds twenty-five now, and three columns is *nine rows* —
the height between the heading and the seed box divides to 69 units against 182
of width, and a card at 0.38 does not read as a tile in a rack, it reads as a bar
in a chart. It wasted the height twice over, because the cards were capped by the
**width** long before they had spent it, leaving 160 units of black under the
last row. Five divides twenty-five exactly, so there is no ragged last row at all
and the cell comes out 109 × 125.

That costs the names, and paying for it is where the measurement earns its place.
A card is 97 units wide inside its inset; `NOTHING UNDERNEATH` is 184 at
seventeen point, so the label has to wrap — and *at seventeen the wrap does not
save it either*, because `TURNED INSIDE OUT` breaks into **three** lines in a
97-unit box and 67 units of text will not go into a 41-unit band. Fifteen is the
largest size at which every name the game can print comes out at two lines or
fewer. That is an output of `tools/type/reflow.py`, not an input to it: the table
now measures all six of the long names, the generated `CLOSE CROSS` among them,
because the generator builds names out of word lists nobody thinks to check.

**And the browser had no ceiling.** Pressing the arrow enough times reached
`VAULT 45 · EMBER SPINE`: a made-up name over a hundred and fifty cards, none of
which is a cube, drawn four rows deep into each other. `Vaults.LastBand` is the
stop, both arrows go inert at the ends rather than vanishing, and a sweep proves
the vaults tile the whole ladder exactly — every vault starting where the last
one ended, every cube in the catalogue, the last one landing on `LastCube`. The
numerals stopped at XII too, so any vault past the twelfth read as an arabic
number; the ladder is ten chapters now (see `CURRICULUM.md`) and the sweep is
what keeps that claim true after a re-cut rather than at the time one was made.

### A rect with negative height

The pause card lost every one of its five controls, and there was nothing on
screen to say so — the name and the vault line were anchored normally and drew
fine, so the screen looked deliberate. You could only leave it by solving the
cube.

`offsetMin` is the bottom-left corner and `offsetMax` the top-right, and on an
axis whose two anchors are the **same** the difference between them is the whole
size. The pause stack passed them the other way round, so every row was minus 88
units tall and laid its children out inside nothing. `FlowWin`, which it was
copied from, has it the right way up.

`UiKit.Rect` now refuses to build one. It asks about the anchors first, because
where they *differ* the axis is a stretch and the offsets are insets against two
different edges — `offsetMax` below `offsetMin` is ordinary and correct there.
Where they match, an inverted pair is logged as an error and swapped: a screen
that works while shouting beats a screen that is silently absent.

---

## The ending

The last cube does not exit. Everything the ordinary exit does is wrong for the last
one: the throw says *and now the next one*, the card asks a question, and the
vault chime congratulates you on a boundary you are not crossing.

So **nothing is thrown**. The board is left exactly as it was solved and the
picture closes in on the one square you reached. What goes away is everything
*around* it.

| | |
|---|---|
| **close** | 2.4s. The instrument and the camera move together, so the screen does not empty and *then* move — one gesture, ending with a cell alone in the dark. `UiKit.Dim` fades every canvas the kit ever made; `CubeView.Dim` takes the deck, the schematic, the cage and every glyph down as one; `CameraRig.Close` aims at the goal cell and drops the aperture to a sixth. |
| **grow** | 2.2s. The singularity has spent the whole ladder being the smallest thing on the board and stops being small. White, because every other colour in this palette means something and none of them mean this. Cubed easing: slow, then not slow — a star does not swell at a constant rate. |
| **blow** | One frame with everything on it, then 0.55s of front. The shockwave is the ring the ordinary exits gave up; it was competing with debris there and here there is nothing for it to compete with. |
| **black** | 3s. Not a fade to a menu — a stop. The only silence in this game longer than the collapse's fifth of a second, and the whole reason the ending reads as one. |

**Every size in the last three moves is measured off the frame, not written
down.** At full close the camera is at a sixth of its usual aperture, so the
world units the rest of the game is tuned in mean something completely
different: a 90-unit ring crosses the closed frame in a fortieth of its life and
is never seen, and a blob that swells to 26 fills the screen a third of the way
through the growth and spends the rest of it white on white. Both are the same
mistake — a number that only made sense at one zoom. The finale reads
`orthographicSize` off the camera doing the framing and scales the blob, both
rings, the spark speed and the spark size out of it.

The shake moves with it too, and that is `CameraRig`'s problem rather than the
finale's. Trauma is a screen gesture stored in world units, and the two agree at
exactly one aperture: half a world unit against a four-unit frame is a fifth of
the height and reads as a hit; the same half unit against the closed frame is
most of the screen, and the shockwave meant to rattle the star would instead
fling it out of shot and shake an empty picture. Every offset now travels with
the aperture, so the amplitude **on screen** is what stays constant — which is
the only part of it anybody can see.

The sound is told how long the picture is. `Sfx.Duck` holds for its argument
plus 2.4s and then takes 1.2s to bring the bed back, so the finale passes it the
whole ending minus that lead-in and the hum arrives under the title rather than
under the shockwave.

And progress stops at the end of the ladder. Clearing cube *n* stores *n+1*,
which is right for four hundred and ninety-nine of them and off the end of the
world for the last: 501 is a cube the catalogue does not have, and the supply
answers a miss by **minting** one out of the generator that exists for the daily.
`Vaults.Resume` clamps what is played and `Vaults.Cleared` is the separate
question of whether the machine is finished — the title reads `[ THE CORE ]`
rather than `[ CONTINUE ]`, and a sweep over every save value from −3 to 540
proves none of them resume onto a cube that is not in the ladder.

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

Week, month and season totals are computed and kept locally; a window with
nothing at all in it scores −1 rather than a wall of penalties, so a fresh
install does not open by charging you for a month it was not installed for.

**Nothing shows them.** There was a BOARDS screen — every row a local record,
saying LOCAL ONLY out loud so it could not be mistaken for a leaderboard — and
it is gone, along with the link to it on the pause card. A screen of the
player's own numbers, with no ranking to be in and nobody to be ranked against,
is a page about the game rather than a part of it. The model stays exactly as it
is: `DailyBoards` still records, prunes and packs on every solve, `Submit` is
still the hook a ranking service wires into, and the day the boards mean
something there is a screen's worth of data waiting for them.

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
  cube size, **and every vault**. It holds, with the narrowest margin under
  deuteranopia. What it did *not* hold was 1.4.11: the worst pair is 2.56:1, so
  legibility sinks the near lattice fifteen per cent and takes it to 3.03:1,
  widening the game's own band from 48 to 59 out of 255.

  Every vault, because the lattice now ages down the ladder — the machine
  corroding as you go deeper, which is the same thing that happened to the case.
  The trace does not move: it is the signal, and every assertion is anchored to
  it. Ageing the lattice walks it toward the trace, so the audit prints the
  narrowest margin it found and where: **0.1537 at vault 1, 0.1362 at vault 30**,
  which is eleven per cent of the band spent on thirty vaults of weathering.
- **Two reads were sharing one channel.** Depth is brightness and material is
  brightness, and *reachability* — the question the player asks after every fold —
  was a flat dim to 46% on top of both. It drains as well as darkening now:
  value still says how far away a cell is, saturation says whether you can get to
  it. The dim is unchanged, so every measured pair is exactly where it was — the
  grey a drained cell moves toward is its own luminance, which is what makes it
  free — and a colourblind player still has the dim underneath.
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
  root and every screen's plate are inset to the opening *and clipped to it*, so
  there is nowhere on the metal that a word can go however long it turns out to
  be. The clip is a floor and not a fix — a word cut off at the edge of the glass
  is still a bug, and text that has to fit wraps (`UiKit.Label(wrap: true)`) or
  shrinks (`UiKit.Fit`).
- **It is never behind the board.** The canvases are screen-space overlays and
  draw after the camera, so anything painted across the middle paints over the
  cube. The case is twelve pieces — eight corner arms and four sides — and the
  reason it is twelve rather than eight is exactly this: a square piece at each
  corner would be simpler and would hang a clear quad over four corners of the
  board. There is no quad over the middle at all.
- **And nothing is behind the case either.** The silhouette is a matte on the
  colour, not a hole in the alpha, so the surround ships opaque black — the same
  `#000000` the camera already clears to. The board is drawn by the camera across
  the whole display, so every pixel the silhouette cut away used to be a hole
  straight through the machine: mid-fold the cube is half again as wide as its
  own face, and the matrix cage came out through the notch in the top edge and
  ran off the case into the desktop behind it. The glass is the only transparent
  part of the asset.

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

**The rows** — `Assets/Scripts/Game/Scanlines.cs`. Black at a third alpha over
one device row in three, across the opening, under the glass and over everything
else: the board, the readout, and any menu that has come forward. An image with
rows in it is a readout; the same image without them is a screenshot.

It can only darken, and only every third row — eleven percent averaged over the
eye, and nothing at all on a ground that is already black, which is what keeps
it clear of the audit. A layer that *lifted* the ground would flatten every pair
in it; dimming the foreground by a ninth moves Ink on Void from 16.8:1 to
15.0:1, still more than twice the AAA floor.

The one detail worth care is that it is built in **device pixels**, not canvas
units. A three-unit pitch stretched by a scaler that is not an integer gives
lines two pixels wide in one band of the screen and one in the next, and the
beat between them crawls when anything moves. So the texture is one texel wide
by exactly as many rows as the opening has device pixels, point-filtered, drawn
one to one, and rebuilt only when the display changes shape.

**The glass in front of it** — `Assets/Scripts/Game/Glass.cs`. The same
photographed machine, from the front: fingerprints, dust, hairline scratches, a
sleeve-wipe smear and the ghost of the pixel lattice, over the whole opening and
in front of everything including the menus, because that is where glass is.

It only ever ADDS light, and that is the whole design rather than a detail of it.
Alpha-blending a near-black photograph over this interface would darken it
everywhere the dirt is dark — which is most of it — and take the access audit's
four dark grounds down with it. Additive cannot: dust catches light, it does not
remove it, so nothing that was legible before the layer stops being. The picture
has its pedestal subtracted before it ships, so half the glass contributes
exactly zero and the worst single speck of dust adds 0.29 of full white across a
few units. And it turns off under legibility, because that setting is a promise
that the interface will stop performing and a layer of grease is the most
performing thing in the game.

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

## First contact — the two verbs nobody taught

The shipped web build showed the manual once, on the first press of PLAY:

```js
if(!store.taught){ store.taught = 1; save(); show('scManual'); return; }
```

The port dropped that line and kept the field. `taught` was declared in
`SaveData` and read by **nothing**, so a player opening this game for the first
time got a black square, four dim arrows and no sentence anywhere in the product
saying that a swipe folds the world. Every other mechanic here is taught — the
plate has a card, the matrix has its one nudge, the four cubes at 146–149 are
unsolvable without their lesson. The two verbs the game is *made of* were the
only two nobody taught.

Restoring the web build's answer would put a wall of type between a player and
the thing they opened, and the manual's own header says why that fails: nobody
reads a manual in a puzzle game. So `Assets/Scripts/Game/UI/Coach.cs` applies the
doctrine already written on the plate card — *teach it when it shows up, once, on
the cube that has one, with the thing itself waiting behind the card* — to the
verbs.

**The order is a measurement, not a preference.** The obvious script opens with
SWIPE TO FOLD, because folding is the idea. Solve the authored cubes and read the
first act of each optimal line:

```
  cube 1  FOOTING     par 1   step-up   step-up  step-up  FOLD-left
  cube 2  THE TURN    par 1   step-down FOLD-up  step-down
  cube 3  BURIED      par 2   step-left step-up  FOLD-left …
  cube 4  TWO FACES   par 2   step-left step-down FOLD-right …
```

All four open on a **step**. A coach that opens with the fold is teaching the
second verb first and asking for a move the cube does not want yet. So the walk
is taught at the start, and the fold is taught at the state where folding *is*
the answer — a question the game can ask itself, because the solver is right
there. `Session.Advice` is the hint without the charge, split out of `Hint` so a
tutorial cannot bill a player three hints for reading it.

**What retires a lesson is evidence, not exposure.** The bits are written when
the player DOES the verb, so somebody who folds before being asked is never
asked, and somebody who backs out mid-lesson gets it again rather than having
spent it on a screen they left. A save that has ever cleared a cube starts
retired — `reached > 1`, the same reading the title's fourth door uses, so "has
never finished a cube" has one definition in this game rather than two.

**It never takes the input.** Every graphic is `raycastTarget = false` and there
is no dismiss button, because there is nothing to dismiss: the way out of the
lesson is to do the thing, and a player who already knows does it inside a second
and never reads a word.

`CoachChecks` plays cube one and cube two through the real `Session`, one move at
a time, and asks the coach what it would be showing at every state: TAP before
the player has walked, SWIPE at the first state whose next act is a fold, silence
on the daily, on a made cube, on practice, past cube two and over a win. The ring
is placed 68 times across 71 states of the ten authored cubes and never lands on
a glyph, a node or a lock — a tap onto a plate turns the board inside out for
five seconds, which is the largest event in the game going off inside a sentence
that says TAP TO MOVE. And neither coached cube carries a plate or an everter, so
there is only ever one teacher talking.

**The rules stopped being reachable only from inside the settings screen.** The
pause card's link row carries MANUAL beside CALIBRATE — the pause card is where
somebody goes when they are stuck, which is exactly when a reference is worth
having — and the title's fourth door reads MANUAL until the first cube is
cleared, FORGE ever after. A level editor with a verify pass and share codes is
the least useful thing in this game to somebody who has not played it.

## The window is the shape of the thing in it

The play screen frames the board in a stroke and the camera contains the solid
inside the same rectangle. On 1440×2960 that rectangle was **1250 by 2075 around
a board 977 square**: 78% of its width and 47% of its height, with five hundred
pixels of nothing above it and five hundred below.

That is not air. It is a frame drawn around empty space, and the two read
completely differently — a stroke says *this is the window*, and a window twice
as tall as its contents says the contents failed to load. So the window is cut to
the square its contents are, about the same centre. `Fit` sizes the cube from the
shorter side and offsets it by the rect's centre, and this changes neither: **the
board does not move and does not change size.** The stroke comes in to meet it,
and the four fold marks that hang on it come in from the edges of the display to
the edges of the board.

It also makes a fold cost the same in both axes. `Room` contains the solid inside
this rect and a solid mid-fold is root two wide, so against 1250×2075 a
horizontal fold pulled the camera out 15% and a vertical fold pulled it out not
at all — the height was never the binding constraint. Against a square they are
the same 15%.

**Two definitions of one rectangle went with it.** The HUD drew the stroke from
its own sum of the two bands while the camera framed against `Layout`'s, and
`Layout` converted units to pixels with `Min(w/720, h/1280)` while the canvas
scaler uses the geometric mean of the same two ratios. On a 16:9 phone those
agree to within a per cent, which is why nothing ever looked wrong. On 1440×2960
they are **2.000 and 2.151**: the camera believed the readout ended thirty-six
pixels above where the readout drew itself, and in landscape it fitted the cube
to a rect that ran under both bands. `Layout.CanvasScale` is the scaler's own
factor now, and `Layout.ApertureInsets` hands the HUD the same rectangle the
camera frames against.

`LayoutChecks` measures the fill on six real phone shapes and one turned one:
**82–83% across and 81% down on every one**, and the stroke and the camera agree
to half a pixel.

## The front screen's one object

The title draws the machine in the gap between a masthead and a block of
controls. Three things have to agree about that gap and they were three different
opinions.

**The scrim was shaped for something that is not there.** Its clear window sat at
0.46–0.56 of the plate — a tenth of it — because it had been tuned against the
three-line pitch printed under the masthead. The pitch was deleted; the window
never moved. The cube drawn under it is a fifth of the plate, so most of it was
under black. It does not read as a grey smudge because it is dark. It reads that
way because it is under a gradient shaped for a paragraph.

And the stops were *fractions*, while everything they align with is a fixed
offset from an edge — the masthead is 346 units down whatever the plate is, the
controls are 452 units up from the floor. On a 20:9 phone the plate is a hundred
units taller and every stop lands late. So the scrim is two pieces anchored to
two edges with heights in units: black under the type, black over the controls,
nothing in between, identical on every phone.

**The camera was framing into the wrong rectangle.** The attract cube was fitted
to the play window, whose centre is pulled down by two HUD bands that are not
drawn on this screen at all — so the subject of the title sat seventy units low.
And it was framed for a solid that turns through *every* angle, because the pose
was a continuous yaw: a cube that presents its diagonal at some point in the
cycle has to be framed for root three of its own width. Bounded to a
three-quarter view that drifts rather than spins, the worst silhouette is root
two — which is 27% more cube on a 20:9 phone, and the difference between a
product shot and a screensaver.

`TitleChecks` walks the entire drift cycle at four thousand samples through the
camera's own containment arithmetic: the cube reaches **96% of its band at its
widest** without crossing it. It measures the scrim on three phone shapes and
requires over 0.60 of black across every line of type and under 0.02 across
everything of the machine that is not deliberately behind the opaque control
plates. On 16:9 the cube comes out 6% smaller than it was — it used to overflow
ninety units into the controls by accident, which is now the allowance it is
given on purpose, and it is fully in the clear instead of mostly under scrim.

## What is not here yet

Three things, none of them rules.

Everything else is here: the rules, the three verbs, the vault ladder, the daily
and its streak, the Forge with its verify pass and share codes, the seed box,
the manual, the plate lesson, first contact, calibrate (including the brightness
and contrast filters), the win card, the attract cube, the arrive/leave
transitions, the reveal sweep, the cage, the depth readout, the character, time
bending, the particle work, audio, haptics, safe areas and persistence.
