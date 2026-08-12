# SINGULARITY — audit, 2026-08-12

Asked: is this as good as it should be, and is it revolutionary for its genre.

Short answer: **the engineering is top-decile and two or three things in it are
genuinely rare. The game design has a hard ceiling that the content structure
ignores, and there is one severe performance defect.** It is not currently
revolutionary. It is a very well built game with roughly fifty levels of real
content wearing a two-thousand-level costume.

Everything below is measured, not asserted. Numbers come from the shipped
file on desktop Chromium; a phone is 3-5x slower.

---

## 1. The difficulty curve flatlines at about level 50

This is the finding that matters most, because everything else — thirty
vaults, thirty leaderboards, 2,070 ranked cubes — is built on top of it.

Par by level, one mint each:

    level      1    8   20   35   50  100  220  450  800 1300 2070
    par        2    3    4    5    4    4    6    6    6    4    4
    cube n     5    5    6    7    7    7    7    7    7    7    7

Par distribution over 45 cubes sampled evenly from levels 120-2060:

    par 3   ####                4
    par 4   ################   16
    par 5   #############      13
    par 6   ############       12

**There is no trend.** Level 2,060 is drawn from the same distribution as
level 120. Par never exceeds 6 anywhere in the game.

That ceiling is not a generator weakness, it is the mechanic. The generator's
own notes record it: one plate lifts the ceiling from 4 to 6 on a 7-cube, and
a *second* plate lowers it again to 5, because more freedom to reach any world
is more ways to shortcut. The puzzle space of "rotate the cube, walk the
surface, reach the exit" tops out around six required turns.

A mechanic with a six-turn ceiling can carry perhaps sixty to a hundred
curated levels. It cannot carry 2,070. Vaults I-IV are the actual game;
vaults V-XXX are two thousand cubes of statistically identical difficulty,
and the thirty per-vault leaderboards rank who ground through 140 same-difficulty
cubes fastest.

**I got this wrong earlier and should correct it.** I told you `specFor`
saturates at level 111 so "every cube past 111 draws from one spec". The
*shape* saturates there; `minSteps` keeps climbing, so every level does have a
distinct spec. But the thing that matters — what a player actually feels —
flattens even earlier, around level 50.

Related: `minSteps` is `5 + band*2 + w` with an **uncapped** band, so by level
2070 it asks for a 426-step walk on a 343-cell cube. Achieved step counts are
5-21. The demand has been unreachable since roughly level 60, which means the
term has been silently inert in the candidate scorer for 97% of the game.

### CORRECTION: the ceiling is the CUBE SIZE, not the mechanic

The paragraph above said a six-turn ceiling is a property of the mechanic.
**That is wrong, and it was wrong for a measurable reason.**

`mint` solves each candidate with `solve(lv, spec.parHi)` — a BOUNDED search
that discards anything harder than the band it was asked for. Every ceiling
measurement was therefore taken through a filter set at 6. Re-running the
generator with the bound removed, 900 candidates at each size:

    n     solved   max par   par>=6      par>=7        ms/solve   code
    7      849        7      5  (0.6%)   1 in 849       12.3ms    175
    8      857        7      19 (2.2%)   1 in 214       18.8ms    250
    9      843        9      40 (4.7%)   1 in  56       37.3ms    346
    10     721        8      54 (7.5%)   1 in  48       55.6ms    467

**A bigger cube raises the ceiling and raises it a lot.** n=9 reaches par 9
against n=7's 6, and makes a par-7-or-better cube **fifteen times more
likely** to exist. The generator caps n at 7 (`b < 1 ? 5 : b < 3 ? 6 : 7`),
so the game has never once cut a cube at a size where hard cubes are common.

Why size matters when the rotation group does not: the orientation graph has
24 nodes, 4 generators and a **diameter of 4**, so pure rotation distance can
never exceed four turns. The extra turns come from FOOTING — you cannot turn
where you have nowhere to stand, so a large cube forces detours through
orientations you did not want. More surface is more places for the route to be
forced, and that is what the ceiling was actually made of.

A 9-cube also already works: it renders and fits at 390x844, 320x568 and in
landscape, tiles come out 30-38px, `draw()` stays at 0.45ms, no errors.

### What to do

**Extend the size curve past 7.** Keep 5 and 6 where they are, run 7 through
the mid game, then 8 and 9 for the late vaults, and raise `parHi` with them.
That is a real difficulty ramp built from parts that already exist, and it
uses the vault and leaderboard structure rather than retiring it.

The costs are real and none of them are blockers:

- **Minting gets much slower.** Finding a par-7 cube at n=9 takes about 56
  solves at 37ms — two seconds on this desktop, plausibly six to ten on a
  phone. This makes the Web Worker in section 2 **mandatory rather than
  merely advisable**.
- **Share codes grow** from 175 to ~350 characters. Still pasteable.
- **Tap targets shrink** to 30px on a 320px-wide phone. Workable, tight.
- **The baked identity index must be regenerated**, and the bake gets slower
  in proportion.
- **The editor needs 8 and 9 in `ED_SIZES`**, and a 9x9 deck grid on the
  smallest phone gives ~29px cells.

Doing nothing still leaves 1,950 cubes that exist only to be counted.

---

## 2. FIXED — was: a half-second freeze on every level load

Frame deltas over four seconds after a level loads:

    408ms   116ms   587ms      (three stalls, the worst 1.8s after load)

`mintCache` afterwards holds the current cube *and the next one*. The stall is
`prebuild(levelNo + 1)` minting the next cube **synchronously on the main
thread**, which costs 200-500ms on this desktop.

On a mid-range phone that is plausibly 1.5-3 seconds of completely frozen
game, about two seconds into every level. In a review this reads as "stutters
constantly" and nothing about the rest of the craft will survive it.

Fixes, in order of preference:

1. **Move minting to a Web Worker.** The generator is pure and already proven
   portable across JS engines — it is close to the ideal worker payload. The
   only work is passing the level number in and the cube out.
2. **Chunk it across idle callbacks.** `mint` loops over candidates; yielding
   between them would cap any single block at one candidate solve.
3. **Drop prebuild entirely** and mint on demand behind the existing
   "CUTTING…" toast. Simplest, and costs a visible wait between levels.

This is the single highest-value fix in the codebase.

### Fixed, 2026-08-12

Option 1. Minting runs on a Blob worker built from **this file's own source**:
there is one `<script>` in the document and the generator sits between two
markers inside it with no DOM access anywhere between them, so the worker
gets the slice verbatim rather than a copy that could drift. Two generators
disagreeing by one RNG draw would produce different cubes for the same level
number and destroy the determinism silently, so `sgcutter.js` mints six levels
BOTH ways and compares canonical identities.

Measured again over a level load and its prebuild:

    before   408ms   116ms   587ms      worst 587
    after    no frame over 100ms        worst  90

`levelData` keeps its synchronous path for a level reached faster than it
could be cut, and every worker failure — CSP, file:// origin, old WebView —
falls back to the timer this replaced. The game is never worse than it was.

---

## 3. What is genuinely rare, and is not overstated

**Par is a machine-proven minimum, not a designer's guess.** Every cube in the
game — authored, generated, daily, or built by a player in the editor — has
its par established by an exhaustive 0-1 BFS over position x orientation x
keys x doors x world. Almost no puzzle game does this. It is what makes "you
solved it perfectly" a fact rather than a claim, and it is the foundation the
whole scoring system stands on.

**Determinism is real and verified, not assumed.** Level N is byte-identical
on every device forever, proven by re-minting all 2,070 cubes in a separate
browser session and comparing canonical identities. The one bug that would
have broken it — `sort()` with an inconsistent comparator consuming a
different number of RNG draws under different V8 builds — was found and fixed.
That class of bug normally ships.

**The editor validates the way nothing else in this space does.** Solver-backed
solvability, a rotation-invariant identity taken over all 24 orientations, and
duplicate denial against a baked index of the entire ranked catalogue — all
on-device, with no server. Most UGC puzzle games either don't verify
solvability at all or need a backend to do it.

**Rendering is excellent.** `draw()` costs **0.6ms median, 1.1ms worst**. Cold
start to a playable cube is **774ms**. There is enormous headroom.

**The test suite is unusually good for a game.** 159 assertions that test
behaviour rather than implementation, and they earned their keep: they caught
a null crash on draft restore, a layout that scrolled sideways at 320px, a
board with a viewer and no submitter, and a code that silently failed to
decode after passing through a share sheet.

---

## 4. What is not revolutionary, stated plainly

- **The core mechanic is a known space.** Rotate-the-world, gravity-shift and
  perspective puzzles are a populated genre. The execution is better than most;
  the idea is not new.
- **Daily + streak is Wordle-derived** and, in 2026, thoroughly worked over.
  Doing it well is table stakes, not differentiation.
- **The diagnostic/terminal aesthetic is a known style.** Well executed, and
  not a new visual language.
- **The Forge has no competitive hook.** Custom cubes are deliberately unranked
  — correct, since an author can trivially make a one-fold cube — but it means
  the editor produces content with nowhere to go except a pasted code. No
  browse, no featured, no play-count. That is the difference between a level
  editor and a level *community*, and the latter is what makes UGC games large.

---

## 5. Smaller findings

| | Finding | Severity |
|---|---|---|
| 5.1 | `minSteps` unreachable past ~level 60; scorer term inert | medium |
| 5.2 | Forge cubes cannot be browsed or discovered, only pasted | medium |
| 5.3 | No audio/haptic verification anywhere in the suite | low |
| 5.4 | Nothing has run on a real device or in WebKit; Safari bugs found by hand twice | medium |
| 5.5 | Android project has never been compiled (sandbox blocks dl.google.com) | medium |
| 5.6 | `vox` is a string on baked cubes and an array on minted ones | low |
| 5.7 | Report email address ships in plain text in the HTML | low |

---

## 6. Verdict

**Craft: exceptional.** The determinism work, the solver-proven par, the
editor validation and the test discipline are all well above what this genre
normally ships, and several are things most studios would not attempt.

**Design: capped by a parameter, not by the idea.** Everything past level 50
is the same difficulty and the vault/leaderboard architecture was built as
though that were not true — including, in fairness, by me over the last
several sessions. But the cap is `n <= 7` and a bounded solver call, not the
mechanic: at n=9 the ceiling is par 9 and hard cubes are fifteen times more
common. The curve that the structure needs is available and has simply never
been switched on.

**Revolutionary: not yet — but closer than the first draft of this audit
said.** It is an unusually well engineered puzzle game whose difficulty range
has been running at roughly two thirds of what the mechanic supports. Fix the
freeze, extend the size curve, and the 2,070-cube structure stops being a
costume.

Three things, in order, and nothing else on this list comes close:
1. ~~Move minting to a Web Worker.~~ **Done.** Worst frame 587ms -> 90ms, and
   worker-cut cubes proven identical to main-thread ones.
2. Extend the size curve past n=7 and raise `parHi` with it. Now unblocked.
3. Re-bake the identity index and re-measure the curve to confirm it ramps.
