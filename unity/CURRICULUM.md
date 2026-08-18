# The hundred and fifty

*One hundred and fifty cubes, ten chapters of fifteen, and nothing after them but
the daily. Every cube has a CLAIM — a statement about what it is for — and the
claim is proved on every check run. A cube that stops making its argument is a
failed build rather than a thing nobody notices.*

```sh
dotnet run --project unity/tools/UnityStubs curate     # cut them
dotnet run --project unity/tools/UnityStubs ladder     # what they came out as
dotnet run --project unity/tools/UnityStubs            # every claim re-proved
```

---

## Why this replaces three hundred

Three hundred cubes were chosen by a scoring function against generic proxies —
par band, decision density, depth. Those are good proxies and they are still
here, but a proxy cannot tell you what a cube is *about*. A level that scores
well can still be another one of those, and a hundred and twenty-five of them in
a row is where a puzzle game loses people.

A claim is the difference. `RequiresGlyph` says: neutralise the thing this cube
carries and it cannot be finished. `FootingGated` says: at the opening, exactly
one fold has anything under it. `DistantNeighbours` says: the route walks
between two squares that are touching on screen and far apart in the solid.
Those are arguments, and a cube either makes one or it does not.

Length is not a virtue in this genre. A hundred and fifty cubes that each say
something beats three hundred where half are practice.

---

## The claims

Every one is computable from the solver and the projection, which is what makes
it a gate rather than an intention. All of them are measured on the board as
`LevelSupply` hands it over, not on a model kept alongside it.

| claim | what it asserts |
|---|---|
| `MustFold` | no walk from the start reaches the core in the opening view — folding is not optional |
| `SoleOpening` | two or more folds have footing at the start and exactly one of them keeps par |
| `FootingGated` | exactly one fold has footing at all: where you stand has already taken the choice away |
| `FalseFloor` | a square you can reach and stand on now that the first fold of the optimal line takes away |
| `DistantNeighbours(d)` | the reachable board contains two touching squares whose world cells are ≥ d apart |
| `Detour` | the first fold of the optimal line moves the core further away on screen |
| `RequiresGlyph` | demote the plate and drop the second solid, and the cube has no route at all |
| `RequiresKey` | take the key away and the lock never opens — the node is load-bearing |
| `OrderGated` | two folds legal at the start where one order keeps par and the other loses it |

### Two claims that were written and then withdrawn

**`GlyphCheaper`** — solvable without the mechanic, and at least one fold cheaper
with it — failed on twenty cubes out of twenty. That is a definition rather than
a shortage: neutralising a plate, an everter or a trigger does not make a cube
*costlier*, it makes it *unsolvable*, because the cells that were only reachable
in the other world are simply gone. `GlyphCheaper` is the complement of
`RequiresGlyph` and has no cubes to describe. `RequiresKey` is the claim that
band actually wanted, and it is the one chapter IV now carries.

**`OrderGated`** is implemented, correct and unassigned. It held on five cubes in
fifteen, because it can only ask its question of cubes that open on a *fold* and
most cubes open on a *step*. It stays in the vocabulary — a future chapter that
specs for a two-fold opening can reach for it — but a claim that fails two thirds
of the time is a claim that silently downgrades to "whatever scored best", and
the point of the gate is that it does not do that.

Both were caught by printing the hold rate before asserting on it, which is the
same discipline that caught six hollow stubs and three measurement artefacts
elsewhere in this tree.

---

## The ten chapters

Board size climbs 5 → 9 without a gap. Par climbs monotonically. `openMax` — the
share of opening folds allowed to keep par — falls from 1.00 to 0.28, and that
is the axis that carries difficulty after the orientation group caps par.

Each chapter's fifteen slots are split into three fifths, and each fifth carries
one claim.

| # | chapter | levels | n | par | openMax | 1–5 | 6–10 | 11–15 |
|---|---|---|---|---|---|---|---|---|
| I | THE COLLAPSE | 1–15 | 5 | 1–4 | 1.00 | `MustFold` | `SoleOpening` | `Detour` |
| II | FOOTING | 16–30 | 5 | 2–5 | 0.85 | `FootingGated` | `FootingGated` | `FalseFloor` |
| III | DISTANT NEIGHBOURS | 31–45 | 6 | 3–6 | 0.78 | `DistantNeighbours(3)` | `DistantNeighbours(3)` | `FalseFloor` |
| IV | NODES AND LOCKS | 46–60 | 6 | 3–7 | 0.70 | `RequiresKey` | `RequiresKey` | `FalseFloor` |
| V | EMBERFALL | 61–75 | 6 | 4–8 | 0.62 | `RequiresGlyph` | `RequiresGlyph` | `FootingGated` |
| VI | SUBSTRATE | 76–90 | 7 | 5–9 | 0.56 | `RequiresGlyph` | `DistantNeighbours(4)` | `FalseFloor` |
| VII | THE FAR SIDE | 91–105 | 7 | 5–10 | 0.50 | `RequiresGlyph` | `FalseFloor` | `RequiresGlyph` |
| VIII | THRESHOLD | 106–120 | 8 | 6–11 | 0.44 | `RequiresGlyph` | `FalseFloor` | `RequiresGlyph` |
| IX | ASH TERRACE | 121–135 | 8 | 7–12 | 0.36 | `RequiresGlyph` | `FootingGated` | `RequiresGlyph` |
| X | SINGULARITY | 136–150 | 9 | 8–14 | 0.28 | `RequiresGlyph` | `FootingGated` | `DistantNeighbours(4)` |

### I · THE COLLAPSE — 1–15, n=5
*Fold, walk, core. Everything that lines up, touches.*

The first cubes show you a way out you cannot walk to, which is the whole game in
one screen. Nothing here carries anything. **1–10 are hand-sculpted** rather than
cut, and they answer to their own arguments; `Coach` teaches on top of them, in
context, with no modal.

### II · FOOTING — 16–30, n=5
*Where you stand decides which folds you have.*

The quiet rule made loud, and then made to cost something. `FootingGated` says
exactly one fold is legal, which makes a cube a **corridor** at its opening —
one thing to do, and it cannot be got wrong. Cut that way for two thirds of the
chapter, FOOTING measured *easier* than the tutorial chapter before it: 1 in 16
against 1 in 27 on the chance of parring a route by luck. A ladder with a step
back in it at chapter two is a ladder a player learns not to trust.

So the corridor teaches and `FalseFloor` closes — the same rule from the other
end, where the fold you *can* take is the one that removes the ground you were
counting on. It does not pin the opening to a single legal fold, so the chapter
ends on a decision rather than an instruction.

### III · DISTANT NEIGHBOURS — 31–45, n=6
*Two decks at opposite ends of the machine are neighbours on screen.*

The projection's one gift, and the chapter that proves it is not a trick of the
art. The board grows to six here, which is what gives the collapse enough depth
for a three-cell gap to exist at all.

### IV · NODES AND LOCKS — 46–60, n=6
*A door you cannot pass and a key that is behind it.*

`RequiresKey` is the sharpest claim in the vocabulary: demote the node to plain
trace — still walkable, no longer a key — and there is no route. The lock is not
decoration.

### V · EMBERFALL — 61–75, n=6
*The plate: every trace goes dead and every dead cell lights.*

The first glyph. Three chapters carry a plate and this is the one that only ever
has the one kind, so what the plate does can be learned without anything else
moving.

### VI · SUBSTRATE — 76–90, n=7
*The other plate, which opens the solid rather than closing it.*

Seven-cell boards, and `DistantNeighbours(4)` in the middle fifth — the plate and
the collapse asked about together.

### VII · THE FAR SIDE — 91–105, n=7
*The everter: every cell turns in place, and the circuit on the board rewires.*

The four cubes searched specifically for this mechanic sit at **91–94**, authored:
THE FAR SIDE, TWO SHADOWS, NOTHING UNDERNEATH, TURNED INSIDE OUT. Each carries a
second face array and each was kept only because it makes an argument the other
three do not. The eleven after them are cut against `RequiresGlyph` and
`FalseFloor`.

### VIII · THRESHOLD — 106–120, n=8
*The trigger: the board itself is exchanged.*

Eight-cell boards. The last new verb.

### IX · ASH TERRACE — 121–135, n=8
*Nothing new. Everything at once.*

No chapter after this introduces a rule; the vocabulary is closed and the
remaining thirty cubes are recombination under falling `openMax`.

### X · SINGULARITY — 136–150, n=9
*The last fifteen, and then the machine comes apart.*

Nine-cell boards, par to fourteen, and one opening fold in four keeping par. 150
is the hardest cube the cutter can prove, and the one the ending fires on.

---

## How a slot is filled

`Curate.Pick(slot, budget)` mints candidates against the chapter's spec, scores
them, shortlists ten, prunes each at varying depths, re-scores on the opening
decision density, and gates every survivor on `Safety.Audit` — a cube that can be
bricked is not a candidate at any score.

The claim is a **preference, not a hard gate**. An earlier version refused any
candidate that did not make its chapter's claim and left fourteen slots empty,
which is worse than a cube that scores well and argues weakly. So `Pick` keeps
the best claim-making candidate if it found one and the best-scoring candidate if
it did not, and `ClaimChecks` reports the split on every run rather than hiding
it.

---

## What is not here

**No procedural tail.** The ladder stops at a hundred and fifty and the seed box
stops with it. The generator survives for one reason only: the daily, which is
computed from the date so that it is the same puzzle on every phone with no
server. A cube typed past a hundred and fifty used to mint something no other
player had, which is the opposite of what a fixed ladder is for.

**No difficulty that is only size.** Board size is a lever and it is used, but a
chapter earns its place by the claim its cubes make. The audit that matters is
`ladder`, and the column that matters in it is the chance of parring a whole
route by luck — par and decision density compounded, which is what a player
actually feels.
