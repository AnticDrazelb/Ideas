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

### What would fix it

Two honest options, and they are different products.

**(a) Accept the ceiling.** Sixty to a hundred hand-curated cubes plus the
daily. Drop to six or eight vaults and six or eight leaderboards. This is a
finished, tight, excellent small game and the structure stops lying.

**(b) Raise the ceiling with a mechanic.** Something that multiplies the state
space the way plates were supposed to: a second mobile piece, a fold that
changes the cube's shape, an exit that moves. Only worth doing if the ceiling
moves to 10-12, and that needs prototyping against the solver before any
content is built on it.

Doing neither leaves 1,950 cubes that exist only to be counted.

---

## 2. Severe: a half-second freeze on every level load

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

**Design: capped, and the structure does not admit it.** The mechanic tops out
at par 6. Everything past level 50 is the same puzzle at the same difficulty,
and the vault/leaderboard architecture was built as though that were not true —
including, in fairness, by me over the last several sessions.

**Revolutionary: no, not yet, and not on the current mechanic.** It is an
unusually well engineered small puzzle game. The gap between what it is and
what it is dressed as is the biggest single problem, and it is a design problem
rather than a code one.

The two things that would change the answer, in order: fix the prebuild
freeze, then decide between option (a) and option (b) in section 1. Nothing
else on this list matters as much as those two.
