# Gravity — the shape of it, and why it cannot go in yet

*A design note, written against measurements. Run them yourself:*

```sh
dotnet run --project unity/tools/UnityStubs content
dotnet run --project unity/tools/UnityStubs gravity
```

---

## The instinct is right

The game has one idea: **fold the solid, and a different axis becomes depth.**
Folding changes what you can *see*.

Gravity is that idea's sibling: **fold the solid, and a different axis becomes
down.** Folding changes where you can *stand*.

One action, two consequences — one optical, one physical. It costs a single
sentence to teach, it needs no new control, and it fits the fiction exactly: you
are a black hole inside a machine, and the only way up is to turn the machine.

It also lands on the right problem. `Generator.SpecFor` says in its own comment
that par above four folds can only come from footing — the orientation group has
a diameter of four, so pure rotation can never ask for more. Gravity is a footing
rule. It is aimed at the correct target.

---

## But the obvious version makes the game *easier*

Today, `Projection.Landing` is the whole footing rule:

> After a fold, you keep your screen column. The fold is legal **only if that
> column still has footing**.

The natural way to add gravity is to put it here — if your column has no footing,
*fall* to the next one below instead of refusing the fold. That is a **loosening**.
More folds become legal, so solutions get shorter and par goes *down*.

Worse, it would not even be felt. Steps are free and unlimited in all four screen
directions, so wherever a fold drops you, you simply walk back. A landing rule
that the walk can undo for free is not a rule.

---

## The version with teeth

Gravity has to constrain the **walk**, not the landing:

> **The engine has a down. You may step sideways and you may fall. You can never
> climb — the only way up is to fold.**

That single restriction changes the economy of the whole game. Falling is free and
irreversible; folding costs. So the fold stops being *a way to change the map* and
becomes *the only way to undo a free action*. That is the resource tension every
great puzzle game runs on, and this game has the pieces for it already.

It also gives the free step a job. Right now steps cost nothing and mostly decide
nothing — the audit measures **2.6 steps per fold, flat across the entire range**,
and **46.7% of opening folds keep you on par**. Under gravity every step is
positioning: you walk to the column you want to fall down before you spend a fold.
An inert verb becomes the planning verb without changing its price.

And it needs **no new failure state**. A fold that would drop you out of the
engine simply has no footing, and is refused exactly like any other. The tick
marks that say which folds have footing already exist. The refusal sound already
exists. The manual line — *"the ticks say which folds have footing"* — is already
true.

---

## And it will not work yet

I measured what gravity would actually take away. For every cube, at its starting
orientation: how much of the board can you reach walking freely, and how much if
you may only go sideways and down?

```
MEAN SHRINK IN WHAT ONE FOLD CAN REACH: 31.8%

   0- 9%   85 cubes   ################     <- gravity changes nothing
  10-19%   33
  20-59%   54
  60-89%   68 cubes   #############        <- gravity changes everything
```

**Bimodal.** For 35% of cubes gravity is invisible; for 28% it is transformative.
A headline mechanic that sometimes does not apply is a rule players cannot reason
with — which is worse than no rule.

The cause is the number underneath it:

```
   screen squares showing any cell       63.5
   of those, walkable                    13.0
   of those, reachable from the start     6.9   <- the board you actually have
   trace cells in the whole solid        37.0 of 520  (7.1%)
```

**You are looking at ~64 squares and standing on a corridor of ~7.** Ninety-three
per cent of the board is inert lattice. There is nothing to fall *through*,
nothing to fall *past*, and almost never a second route to fall down instead.

You cannot build a falling mechanic on a corridor.

---

## What is actually capping the game

This is the real finding, and gravity is downstream of it.

The generator carves the trace as a **single self-avoiding path** — `legMin`,
`legMax`, `carveTurns` — and it deliberately keeps that path short. Its own
comment says why:

```csharp
// Leg length is deliberately NOT scaled with the vault: a longer
// carve lays down more trace, more trace means more shortcuts, and
// par falls while route length barely moves.
```

That reasoning is correct *and it is the trap*. Par is being protected by
**starving the board**. A board with one route has no decisions on it, so the only
difficulty left is the search for that route — which is why:

- par flatlines at 5.8 folds from cube ~100 onward,
- steps per fold never moves off 2.6,
- a quarter of cubes have an opening where every legal fold keeps par,
- and gravity would do nothing to a third of them.

Every symptom in the content audit is the same symptom: **there is almost nothing
to decide, because there is almost nothing to stand on.**

---

## The ordering

1. **Make the boards spaces, not paths.** Multiple intersecting carves, junctions,
   genuine alternate routes, real vertical structure. Par will initially *fall* —
   that is expected, and it is the thing the current design is afraid of. Take the
   difficulty back with fold-depth targets, not with scarcity.
2. **Then gravity has something to bite on**, and its shrink distribution should
   be measured again. It wants to be reliably in the 40–70% band across the whole
   range, not 0% on a third of cubes.
3. **Then introduce it at a vault boundary** — cube 151 is where the plateau
   starts, and it is where the player has stopped being surprised. A per-level
   flag, so the ten authored cubes and everything before the boundary are
   untouched.

## The teaching arc, when it is time

Four authored cubes, in this order, and no text:

1. The obvious fold drops you **past** your target. *You fall.*
2. You must walk to the far column **before** folding, so you fall onto the right
   one. *Steps aim the fall.*
3. One of the four folds is refused, because you would fall out of the engine.
   *The tick marks already mean this.*
4. You must fall through a gap **on purpose** to reach a deck you cannot walk to.
   *Falling is a tool, not a hazard.*

That is the argument structure. The mechanic is introduced, complicated, bounded,
and then inverted — which is how every puzzle game worth remembering does it.

## The interaction worth saving for later

Plates invert the world: every trace goes dead, every dead cell lights up. That
changes *walkability* but not *solidity*, so under gravity an inversion can leave
you standing on something that is no longer floor — **and you fall, mid-plate,
with five seconds on the clock.**

That is a genuinely excellent second-order idea and it should not be in the same
vault as the first. Teach falling, then teach that the floor can be taken away.
