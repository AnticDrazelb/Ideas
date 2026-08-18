# SINGULARITY ENGINE — the design argument

*Everything here is written against measurements rather than taste. Every number
is reproducible; the command that prints it is next to it.*

```sh
dotnet run --project unity/tools/UnityStubs content    # par, openings, the curve
dotnet run --project unity/tools/UnityStubs buried     # what the solid is hiding
dotnet run --project unity/tools/UnityStubs gravity    # what a fall rule would cut
dotnet run --project unity/tools/UnityStubs arc        # search for teaching cubes
```

---

## 1. The one idea, and whether the content uses it

Everything else this harness measures is craft. None of it is why anybody
remembers a puzzle game. What is remembered is whether the central idea is the
thing you actually have to think about, cube after cube — and that is a property
of the **content**, not of the code.

```sh
dotnet run --project unity/tools/UnityStubs content
```

Ten cubes are authored. Everything from eleven on is minted. The audit solves
the first 240 and asks three questions.

**Does the solution require a fold at all?** A cube you can walk from start to
core without folding once is a maze, not this game — and it would still verify,
still have a par, still look right. **Zero of 240.** The mechanic is load-bearing
in every cube. This is the gate that matters most and it passes outright.

**Is the opening a decision?** Of the folds legal from the start, 46.7% keep you
on par. Classified: 18.8% of cubes have no legal opening fold at all (you must
step first), 55.4% have an opening where some folds lose par — a real decision —
and **25.8% have an opening where every legal fold keeps par**, which is a first
move that does not matter.

**Does it get harder, or only bigger?** This is the one that should worry you.

```
  tenth   mean n   mean folds   steps/fold
     1       5.9         2.71          2.2
     3       7.0         4.38          2.8
     5       8.0         5.17          2.4
     7       8.8         5.67          2.6
     9       9.0         5.83          2.4
    10       9.0         5.54          2.7
```

Par asymptotes at about 5.8 folds by the fourth tenth and then stops. The tenth
tenth is *easier* than the ninth. Steps per fold is flat at ~2.6 throughout, so
the ratio of thinking to searching never shifts either.

The cause is three lines in `Generator.SpecFor`, and they are deliberate
individually:

```csharp
int b = Math.Min(band, 11);                              // saturates at cube 111
int n = ... : band < 15 ? 8 : 9;                          // caps at 9 from cube 151
int carveTurns = Math.Min(9, 3 + b + ...);                // caps at 9
int parSpan = ... n == 9 ? 8;   // parHi = 10, and a harder candidate is DISCARDED
```

From **cube 151 onward every generation parameter is at its ceiling** — same
size, same carve ambition, same density, same lock cap — and `MaxCube` is
100,000. The last 99,850 cubes are drawn from one fixed difficulty distribution,
with a hard maximum of ten folds that is never approached in practice.

`VaultSize` grows the *number* of cubes per vault (25 + 5b) but nothing grows
their difficulty, so vaults 16 through 30 are fifteen vaults of the same cube.

### Everything above is about the GENERATOR, and the generator is no longer the ladder

```sh
dotnet run --project unity/tools/UnityStubs ladder    # what actually ships
```

`Bootstrap` loads `catalogue.txt` and `LevelSupply` serves it, so the first three
hundred cubes — every cube most players will ever see — are **curated**: chosen
out of a pool per slot against a per-vault spec, pruned, and safety-audited. The
generator is what happens after cube three hundred. `content` audits the
generator; `ladder` audits what ships, through `LevelSupply.Get`, so the fourteen
authored cubes are the authored ones.

```
vault  cubes   n   par  keep-par  folds/pt  depth   par by luck
I       25  5.3   2.96       36%       8.4     22%       1 in 48
II      25  5.0   4.00       36%       4.4     28%       1 in 90
III     25  6.0   4.20       37%       8.2     29%      1 in 125
IV      25  6.0   5.00       44%       5.4     31%      1 in 128
V       25  6.0   5.00       39%      14.3     49%      1 in 343
VI      25  6.0   4.60       35%       9.7     44%      1 in 396
VII     25  7.0   6.36       45%       5.7     43%      1 in 192
VIII    25  7.0   6.28       30%       9.8     58%   1 in 10,920
IX      25  8.0   7.76       34%       8.8     64%   1 in 39,723
X       25  8.0   8.44       35%       8.2     65%   1 in 30,925
XI      25  9.0   9.20       39%       6.8     65%   1 in 72,000
XII     25  9.0  10.04       40%       7.6     65%  1 in 197,741
```

**"Par by luck" is the column that means anything, and it took three tries to
measure.** It is the chance of parring the WHOLE route by choosing at random at
every fold — the product of the per-decision ratios rather than their average —
because difficulty is not a property of one decision. A cube with three
decisions at 28% and one with ten at 40% have the ratio pointing one way and the
puzzle pointing hard the other.

The three wrong turns are worth recording, because each produced a number that
looked like a finding:

- **The ratio cannot tell a corridor from a meadow.** One legal fold that keeps
  par scores the same 100% as four that do. `RouteOpen` reports the branching
  factor beside it now: 4 to 14 landings per decision, so these are meadows.
- **A walk that loses its line was reported as infinite difficulty** rather than
  as unmeasured, which put eleven of one vault's twenty-five below one in a
  trillion and made a teaching vault look like a spike.
- **The audit read only `vox`, and a trigger cube is two solids.** Half of every
  cube in five vaults was invisible.

**So the headline of §1 is false of the shipped game.** Par does not asymptote at
5.8 and stop: it climbs from 2.96 to 10.04, board size goes 5 to 9, depth goes
22% to 65%, and the chance of parring a route by luck falls from 1 in 48 to 1 in
197,741 — four orders of magnitude. The curation fixed the thing this section was
written to complain about. The section stays because it is still true of cubes
301 and up.

### What replaced it is a different flat line, and this one is load-bearing

`Curate`'s own argument for the back half is that par runs into a ceiling the
orientation group imposes, so **decision density** carries the rest: `openMax`
falls from 1.00 to 0.20 across the ladder, which is "the difference between a
cube where every opening fold keeps par and one where four in five throw the
route away."

The gate falls **5.0 points a vault**. The cubes it was supposed to select
**rise 0.1 points a vault** — 28% in vault I, 40% in vault XII, wandering
between with no trend at all. Decision density is not the back half's difficulty
axis in the shipped ladder, whatever the ladder was designed around; par, size
and depth carry every bit of it, and vault XII ends the game on the *widest*
opening decisions in it rather than the tightest.

**And it is not a scoring bug.** The obvious suspicion is that the 900-point
`routeOpen` term is being outvoted, or that the shortlist — which is chosen
before `routeOpen` is measured — selects against it. Both were measured and both
are innocent: the shortlist comes out at 28% against its pool's 33%, so it is
slightly *better* than what it was drawn from.

```sh
dotnet run --project unity/tools/UnityStubs ladder gate      # cube 300's slot
```

The answer is a conflict between the two axes:

```
  candidates that solve      58 of 60
  inside the gate            19 of 58  (33%)
  in the par band UNPRUNED    0 of 58
  in the band AND the gate    0
```

**Nothing is both.** The carve cannot reach a par of twelve at n=9 on its own —
zero of fifty-eight candidates land in the band — so the cut must prune to get
there, and `Pick`'s own comment records what pruning costs: *"pruning improves
the opening figure from 29% to 15% and worsens the route figure from 32% to
44%."* The gate is not missed because a scorer lost a vote. It is missed because
the ladder asks for two things the carve cannot supply at once.

### What was actually wrong, and it was not the gate

The gate is a symptom. The ladder used to end like this:

```
   VIII  1 in 37,074      XI   1 in 900,000
   IX    1 in 315,592     XII  1 in 10,752      <- the finale
```

**The last vault was the third easiest of the back half** — softer than the vault
that introduces the trigger, a hundred cubes earlier. A game that ends on its
easiest hard cubes has no ending.

Two vaults were specified to carry a PAIR of world-changers rather than one:
ASH TERRACE as "a plate under a trigger", SINGULARITY as "the everter with a
trigger", on the grounds that both combinations measured cleanest together. They
were also the two vaults that measured easier than the vault before them.

That sentence was written against the comparison in `Curate` that measured pairs
against stacks of three. Nobody had asked whether a pair beats a *single*.

```sh
dotnet run --project unity/tools/UnityStubs ladder spec 11    # SINGULARITY's own slots
```

```
                     raw                 every layer load-bearing
  E  x1   par 1.90   1 in 9              2 of 39    1 in 2
  T  x1   par 3.48   1 in 220           34 of 40    1 in 314
  ET x2   par 2.97   1 in 24            15 of 38    1 in 57
```

A single trigger is **nine times** the raw material of the pair the vault shipped
with, at the same size, the same par band and the same seeds. And the right-hand
column is the part that settles it: keeping only candidates where *every* layer
changes par on its own, a working pair is **still five and a half times weaker**
than one trigger. So it is not that the second mechanic is dead scenery and a
gate on live layers would fix it — two world-changers make each other smaller
even when both are doing work. That is `Curate`'s rotation argument holding one
level deeper than it was originally made.

The everter is the sharpest case: at n=9 only two candidates in thirty-nine come
back with a live one. It is on the board and the solution does not touch it,
which is this file's own definition of scenery — so dropping it from the finale
removes scenery rather than variety. It keeps THE FAR SIDE at n=7, where it
measurably works.

**Both vaults now carry one trigger, and the back half climbs:**

```
   VIII  1 in 10,920     XI   1 in 72,000
   IX    1 in 39,723     XII  1 in 197,741    <- the finale, and now the hardest
```

The re-cut also found that **the shipped catalogue was not reproducible from the
source that claims to cut it** — every vault came back different, not just the
two whose spec changed, so the file had drifted from the code at some point
before this. It is reproducible now, and `catalogue: mean par by vault` prints
the curve on every check run so the next drift is visible the day it happens.

**The gate is still missed, and that is now a documented disagreement rather than
a bug.** Decision density does not tighten across the ladder — 36% in vault I,
40% in vault XII, the gate falling five points a vault underneath it. Par, board
size and depth carry all of the difficulty. The honest reading is that `openMax`
describes an axis this generator does not supply, and the two remaining options
are to drop it from the argument or to fix it upstream in §2: the carve is a
single self-avoiding path, so par is protected by starving the board.


---

## 2. Where the ladder stops

`DESIGN-gravity.md` is a design note written against measurements rather than
taste. Short version:

```sh
dotnet run --project unity/tools/UnityStubs gravity
```

Gravity is the right *kind* of idea — folding changes what is deep, gravity makes
folding change what is **down**, one action with two consequences. But the
obvious implementation, falling as part of `Projection.Landing`, makes the game
**easier**: it lets folds through that are currently refused, and free stepping
undoes the displacement anyway. It only has teeth if it takes away *climbing*, so
that the fold becomes the only way to gain height.

And it will not work yet, because there is nothing to fall through:

```
   screen squares showing any cell       63.5
   of those, walkable                    13.0
   of those, reachable from the start     6.9   <- the board you actually have
   trace cells in the whole solid        37.0 of 520  (7.1%)
```

You are looking at ~64 squares and standing on a corridor of ~7. Gravity's effect
is bimodal across the range — invisible on 35% of cubes, transformative on 28% —
and a headline mechanic that sometimes does not apply is worse than none.

**The real cap is upstream of gravity.** The generator carves the trace as a
single self-avoiding path and deliberately keeps it short, with a comment
explaining that a longer carve means more shortcuts and a lower par. That
reasoning is correct and it is the trap: par is being protected by *starving the
board*, and a board with one route has no decisions on it. Every symptom in the
content audit — flat par, flat steps-per-fold, a quarter of openings that do not
matter — is that one symptom.


---

Portal's portals could only exist in Portal. Baba's rules-as-objects could only
exist in Baba. A mechanic that could be lifted into another game is a feature; a
mechanic that falls out of *this* game's geometry is why anybody remembers it.

This game has exactly one original rule:

> **Every screen column shows the NEAREST solid cell. Everything behind it is
> discarded, not hidden.**

That single line is the asset. Every proposal below is judged on whether it is
made of that line, and on whether the measurements say it would bite.

---

## 3. Eversion — the mechanic, and it is built

### The rule

> **The engine turns inside out. Every column shows its FAR side.**

One comparison in `Projection.Project` — `d > cur.d` — decides which cell wins
its column. Reverse it and you have a second projection of the same solid. No new
cells, no new geometry, no new art, no new control. It is exactly as legible as
the rule it inverts, and the MATRIX view already teaches the player that there is
something back there.

**Is it a different board, or the same one seen twice?**

```
   walkable squares, nearest-wins (today)      12.8
   walkable squares, farthest-wins              9.8
   squares showing the SAME cell in both        0.8   (5.9% of today)
   union of the two boards                     19.4   (1.52x today)
```

**94% of it is new.** The two surfaces share less than one square. Eversion is not
a lighting effect — it is a second complete board hiding inside every cube that
has already been generated.

And it multiplies rather than adds: 24 orientations become **48 projections**.
Every cube in the game, including all ten authored ones, doubles for free.

It also composes with what is already there:
- The `world` bitmask that plates use is already two bits wide. Polarity is a
  third bit. `Level.Eff(world)` is already the seam.
- A plate that everts instead of inverting needs no new object, no new glyph
  vocabulary, and no new timer — it is the plate you already have with a
  different consequence.
- Footing still decides which folds are legal, so nothing about `Landing`
  changes.

**The reason it is top-20 shaped:** the player's model stops being *"I am walking
on a solid"* and becomes *"I am walking on a shadow, and I choose which shadow."*
That is a genuine reframe of a rule they already know, which is precisely the
move Portal makes with the second portal and Baba makes the first time you push
`IS`.

### The teaching arc, and it is authored

Four cubes at **146–149**, the first four of vault IX — which the audit says is
exactly where the ladder stops getting harder. They are in `Baked.Arc`.

| # | Cube | The claim it is proved to make |
|---|------|--------------------------------|
| 146 | THE FAR SIDE | **no route at all** without everting |
| 147 | TWO SHADOWS | a route either way, one fold **cheaper** everted |
| 148 | NOTHING UNDERNEATH | a square you can stand on **until** you evert |
| 149 | TURNED INSIDE OUT | a fold that **only one polarity** permits |

Introduce, complicate, bound, invert. No text — the cubes are the argument.

**They were searched for, not drawn.** A teaching cube is a *claim*: this one
cannot be finished while ignoring the mechanic. `ArcSearch` puts an everter on
each trace cell of a minted solid and solves it twice — once with the everter,
once with that same cell demoted to ordinary trace — and the difference between
the two answers *is* the lesson.

The first one had to be **sculpted**. Dropping an everter onto a working cube
cannot make it impossible without one, so trace was removed a cell at a time,
keeping every cut that left the everted route intact, until the un-everted route
died. That is what authoring a puzzle actually is: taking away everything the
intended solution does not need.

`ArcChecks` re-proves all four on every harness run. A cube that quietly stops
requiring its lesson is a failed check rather than a thing nobody notices — which
is the failure mode a hand-placed teaching level always has.

---

### Next: CONSUMPTION — fixes the measurement nothing else fixes

> **You are a black hole. The trace goes dead behind you.**

The content audit says free steps are inert: **2.6 steps per fold, flat across the
entire range**, and **46.7% of opening folds keep you on par**. Stepping costs
nothing and decides nothing. Every proposal that adds board without adding
consequence leaves that untouched.

Consumption makes the walk one-way. You can still go anywhere; you cannot go back.
So the *order* of a route becomes the puzzle, nodes and locks acquire a real
sequencing constraint, and a bad order makes a cube unsolvable — which is the
one-way-door planning that Stephen's Sausage Roll and Baba Is You live on.

It is also native twice over: the fiction is a black hole eating a machine, and —
this is the good part — **78% of solid cells reach the surface in some
orientation**, so the damage you do is *visible from other angles later*. You are
not solving a maze, you are sculpting a solid, and every fold shows you a
cross-section of your own excavation.

The risk is real and worth naming: consumption creates unsolvable states. That is
survivable — undo already exists, and the solver can verify any cube — but it
changes the game's temperature from *puzzle* to *commitment*, and that is a
decision about what this game wants to be rather than a free win.

---

### Next: CIRCUIT — the fiction is already promising this

> *"Fold the engine until its circuits align."*

The game's own strapline describes a **configuration** problem. The mechanic
delivers a **traversal** problem: you walk from A to B. Nothing in the game ever
asks you to *align* anything.

> **The core is dead until a live trace path connects it to a source, in the
> current projection.**

Now folding is for two different reasons at once: making a connection that does
not involve you, and making a route that does. Two nested goals on one board, and
the projection's strangest property — cells at opposite ends of the solid being
neighbours on screen — becomes *the point* rather than a curiosity, because your
circuit closes through things that are not near each other.

Cheapest of the three to build: no new geometry, no new state, one extra
connectivity query in the win condition.

---

### Rejected, and why — these are the useful ones

**Digging for buried content.** The obvious "black hole devours inward" mechanic
assumes the interior is full of unreached level. Measured:

```
   TRACE cells carved                             35.7
   ...visible in ANY of the 24 orientations       34.0   (95.4%)
   ...NEVER reachable from any angle               1.6   (4.6%)
```

Folding already exposes **95%** of the trace. Digging inward would unlock 4.6% of
one board. The interior is not hidden content, it is fill — so the value of any
depth mechanic is in changing *which cells are in front*, never in reaching new
ones. That single number is what turned this proposal from EXCAVATE into EVERT.

**Gravity as a landing rule.** A loosening, not a tightening — see
`DESIGN-gravity.md`. It makes currently-refused folds legal, so par goes *down*,
and free stepping undoes the displacement anyway.

**Gravity as a walk rule** (never climb, only fold to rise) is genuinely good and
still fails today, because there is nothing to fall through: the reachable board
is **6.9 squares out of 63.5 visible**, and the effect is bimodal — invisible on
35% of cubes, transformative on 28%.

**Slice folding** (rotate one layer, Rubik's-style). Native, enormous, and wrong:
it invites comparison to a solved genre, it breaks *the cube is one solid*, and
it explodes the state space in the direction of "big to search" rather than "deep
to think". Bigger is not harder — the content audit already proves that, since
board size grows to n=9 while par flatlines at 5.8.

---

### The order to build the rest in

1. ~~**EVERSION.**~~ **Done** — see §4.
2. **Fix the boards** — spaces, not single carved paths. See `DESIGN-gravity.md`;
   this gates everything physical.
3. **CIRCUIT**, which needs nothing but a win-condition change and finally makes
   the title literally true.
4. **CONSUMPTION or GRAVITY**, once the boards can carry them — and only one of
   them, because both are about making the walk cost something and two such rules
   at once is a different game rather than a deeper one.

None of these is worth anything without the authored levels to teach it. A
mechanic is not a feature you add; it is an argument you make one cube at a time,
and the argument is the game.


---

## 4. What shipped, and what did not

**Eversion is in.** `Level.Everted` is the third bit of the world mask, `Project`
takes a polarity, and `Baked.Arc` holds four authored cubes at 146–149 — the
first four of vault IX, which is exactly where the audit says the ladder stops
getting harder. `ArcChecks` re-proves on every run that each cube still *requires*
its own lesson.

**The visual is in, and it is the honest one.** `Cell.shader` opens by insisting
that the renderer and the rules are the same statement: an orthographic camera
down one axis makes the *depth buffer* perform the projection, so the nearest
cell in a column is the one drawn, which is exactly the rule the solver plays
by. Everting reverses that rule, so the renderer reverses the same way — a scale
of **−1 along the camera axis**. The far cells become the nearest ones. It does
not resemble eversion, it *is* eversion, and the depth buffer keeps doing its one
job.

It goes on `CubeView`'s own transform rather than on `cube`, because `cube`
carries the orientation and would flip along whichever axis the last fold pointed
at the camera. `Cull Off` was already set — the solid is closed and the peek path
draws back faces — so the inverted winding a negative scale produces costs
nothing.

**The turn happens one cell at a time, and that is the whole picture.** Mirroring
the solid in one move reads as an object being squashed through a plane. Doing it
per cell reads as what it is: every cell travels to its own mirror position and
turns a half-circle about its own centre on the way, staggered by depth, so the
turn sweeps from the face you are looking at to the face you are about to be
looking at. Every square on screen is a card flipping over, and on the back of it
is the cell that was hiding behind it — which is not a metaphor for the rule, it
*is* the rule.

A half-circle about a face axis maps a cube onto itself, so the settled board is
exactly the mirrored board with nothing to undo. The mesh never changes for an
eversion — a polarity changes no cell's *type*, only which one wins its column —
so the entire effect is three shader uniforms and nothing on the CPU moves at
all. The stagger is 0.75: at zero every cell flips together and it is a slab
turning over; at one the last cell starts as the first lands and it stops reading
as a single event.

Under it, the cube goes to glass and hardens again, quoting the MATRIX look the
player already knows. Matrix says "there is something behind this." Eversion is
that sentence finished. Violet is pushed into every material's near-colour across
the turn, there is a hitstop and a slow-mo timed to land at the crossing, and the
sound is two slides — one falling, one rising — that pass through each other.

**Eversion survives the plate clock, and that is the whole difference between the
two objects.** A plate is a five-second window; an everter is a standing state.
The solver carries `world` through its search and has no notion of the clock at
all, because the clock is real time and the search is not — so a polarity that
expired would make every cube in `Baked.Arc` solvable on paper and impossible in
the hand. `plateT` is armed by bits 1 and 2 and is blind to bit 4.

**What is still unseen:** nobody has opened this in an editor. The rule, the
arithmetic and the four cubes are proved; whether the turn *reads* at 60fps on a
phone is the one question the harness cannot answer.

**Gravity is not in**, and `DESIGN.md §2` says why: the boards are corridors and
there is nothing to fall through. It waits on the generator making spaces rather
than paths.

**Consumption and circuit are not in.** Both are argued rather than measured, and
both want the same fix to the boards first.

---

## 5. Measuring

None of this was eyeballed. `unity/tools/type/reflow.py` wraps every prose box
against the real `hmtx` advances of both faces and prints which ones overflow
their measured height — it is how the five overflows were found and how the new
numbers were chosen, and it exits non-zero while any box is over, so it is worth
re-running whenever a sentence on a screen changes.

---
