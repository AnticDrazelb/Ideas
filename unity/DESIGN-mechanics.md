# What would actually push this further

*Design proposals, ranked, with the measurements that support or refute each.*

```sh
dotnet run --project unity/tools/UnityStubs content    # par, openings, the curve
dotnet run --project unity/tools/UnityStubs gravity    # what a fall rule would cut
dotnet run --project unity/tools/UnityStubs buried     # what the solid is hiding
```

---

## The test a mechanic has to pass

Portal's portals could only exist in Portal. Baba's rules-as-objects could only
exist in Baba. A mechanic that could be lifted into another game is a feature; a
mechanic that falls out of *this* game's geometry is why anybody remembers it.

This game has exactly one original rule:

> **Every screen column shows the NEAREST solid cell. Everything behind it is
> discarded, not hidden.**

That single line is the asset. Every proposal below is judged on whether it is
made of that line, and on whether the measurements say it would bite.

---

## 1. EVERSION — the strongest, the cheapest, and it is measured

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

**Teaching arc, four cubes, no text:**
1. The core is visible but unreachable; the far side has a route to it. *There is
   another board.*
2. A route that exists in both, but is shorter in one. *Choose the shadow.*
3. A cell that is trace in front and lattice behind — evert and the floor you are
   standing on is gone. *The shadow can betray you.*
4. A fold that is only legal while everted. *The two systems interlock.*

---

## 2. CONSUMPTION — fixes the measurement nothing else fixes

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

## 3. CIRCUIT — the fiction is already promising this

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

## Rejected, and why — these are the useful ones

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

## The order to build them in

1. **EVERSION.** Measured, cheap, doubles all existing content, and reframes a
   rule the player already knows. It is the only proposal here where the evidence
   is already in.
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
