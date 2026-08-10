# TURNKEY

*A dungeon that is a cube. You read one flat face at a time — and everything
that lines up, touches.*

The playable answer to the question `../turnkey.md` ends on: **does the collapse
read as magic or as a bug?**

`index.html` is the whole game. One file, ~99 KB, no network, no dependencies,
no build step at runtime. Open it in a browser or point a WebView at it.

---

## The rule

The dungeon is a solid cube of cells. The camera looks down one axis, and
**every cell sharing a screen column collapses to one square — the nearest
solid one. Everything behind it is discarded, not hidden.**

Two decks at opposite ends of the cube are neighbours on screen if their
columns are neighbours, and you may walk between them as if they touched,
because on screen they do. Turn the cube and a different axis becomes depth,
so a wall becomes a floor, a pit becomes a doorway, and the map you were
stuck on is a different map.

Four rules and no fifth:

| | |
|---|---|
| **Tap** | walk. Pale tiles are decks; dark ones are bedrock; gaps are nothing. |
| **Drag** | turn the cube. Past halfway it commits, short of it springs back. |
| **Brightness** | is distance. The drop-shadows mark a fall you're allowed to ignore. |
| **Turning needs footing** | you may only turn if your own column still has a deck on the far side. |

That last one is the quiet one, and it is where the game lives: **where you
stand decides which turns you have, and which turn you take decides where you
can stand.** The lock and the key, falling out of the geometry rather than
being placed on top of it.

---

## Endless, and provably so

**Vault I — ten authored cubes.** Verified offline, carrying the teaching
beats. The first two show you a way out you cannot walk to, which is the whole
lesson and is not something a generator can be trusted to stage.

**Level 11 to infinity — cut on demand.** A level is minted from its *number*,
so cube 4,127 is the same cube on every phone on earth, with no server. Ten
cubes to a vault; a vault owns one difficulty step and one look.

The promise attached to every one of them:

> **No cube reaches the player without the solver proving it winnable.**

That is structural, not a filter. Solvability is *constructed* — the generator
carves a route by walking and turning, so the route that built the cube is
always in it. The solver's job is the other half: proving the route survived
the decoy pass, that the opening cell is not buried behind rock, that the keys
really do open the doors in some order, and reporting the true minimum, which
is usually shorter than the carve and is what par becomes. Then the gate:
whatever candidate wins, the level is solved once more from scratch before it
is handed over, and anything that fails falls back to a cube that cannot fail.

There is no path through `mint()` that returns an unwinnable level — not on a
bad seed, not when the search budget expires, not at level nine million. The
budget bounds the search for *quality* only; running out means an easier cube
than the vault asked for, never a broken one.

Minting costs a couple of hundred milliseconds, so the next level is always cut
in the background while the current one is being played.

### The difficulty curve, and its ceiling

The first curve asked for nine-turn cubes by vault ten and got threes, at every
budget. That is not a tuning miss — it is the mechanic's ceiling, and it is
worth writing down.

**Turn-par is bounded by the diameter of the cube's orientation graph.** Any
orientation is a handful of quarter-turns from any other, so once a deck set is
well connected you can get anywhere in three or four turns no matter how big
the cube or how many locks are on it. Measured over thousands of candidates:

```
n=5   par tops out at 2         locks barely move par at all:
n=6   par tops out at 3           n=7, 0 locks -> max 4
n=7   par tops out at 4           n=7, 3 locks -> max 4
n=7 at density .60 -> 5         density is the real lever, not locks
```

So difficulty scales on the axes that actually respond, and par is allowed to
stay the small elegant number it is:

- **size** 5³ → 7³ — more cube to read
- **density** .42 → .62 — more bedrock, fewer decks exposed per face, tighter
  routes. The one thing that genuinely forces more turns.
- **locks** 0 → 3 — sequencing. Locks don't raise the turn count; they raise
  how much has to be true at once for a route to work.
- **route length** — a four-turn solution that runs forty steps is a different
  animal to one that runs eight.

Measured across vaults: par 1–2 → 4–5, locks 0 → 3. It plateaus around vault 8,
and past there the vaults change their look and their lock count but not their
fundamental difficulty. Raising that ceiling needs a new mechanic, not a bigger
number — which is what the items in `../turnkey.md` were for.

*(One experiment is recorded in the code as a comment because it failed
instructively: scaling the carve's leg length with the vault made routes longer
but laid down more deck, more deck meant more shortcuts, and par* fell *from 4
to 1 at vault 21. The two axes fight. Turn-par is the one worth keeping.)*

---

## The look

Eight authored vault palettes — THE SHALLOWS, THE IRONWORKS, GLASSWORKS, THE
ORCHARD, CINDERS, THE SALT, THE VEIN, THE DEEP — and past those the palette
keeps going by rotating the authored hues, so vault ninety has a look nobody
chose but nobody has seen either.

**Colour that means something never changes.** You are always rust, the way out
is always jade, keys are always gold. A player who learns those on cube three
must still read cube nine hundred at a glance. What changes with the vault is
the *material*: what the deck is made of, what the bedrock is made of, and what
colour the nothing behind it is.

Everything the board's legibility rests on is one property — **the darkest deck
is brighter than the brightest bedrock** — and for generated palettes that is
not trusted, it is enforced: every style has its bedrock ramp walked down until
the gap clears the deepest cast shadow the renderer can draw, with a margin.
A test asserts it holds across forty vaults, with and without shadow. It caught
a genuine break at vault 22.

Also in the pass: a parallaxed starfield behind the cube so the void columns
read as distance rather than a hole; grain on the tiles so stone reads as
stone; **cast shadows at every depth step** — the light is at the camera, so
those are the true shadows, and they turn the flat grid into a relief you can
read at a glance; additive halos on the three semantic colours; drifting motes;
and a flash on the cage when a turn lands, so the detent you can hear has
something to look at.

Cast shadows reach 42% of a tile and stop, so the *centre* of every tile always
shows its true material undarkened. That is not a nicety — a shadow deep enough
to push a deck under the bedrock line would be the renderer lying about the
rules.

---

## Android

Built for a WebView. The three seams a packaged host needs are on one object:

```js
window.TURNKEY.onBack()                  // true = handled, false = finish the Activity
window.TURNKEY.pause() / .resume()       // lifecycle
window.TURNKEY.setInsets(t, r, b, l)     // the real display cutout, in CSS px
```

`setInsets` exists because **an Android WebView does not know where the notch
is** — `env(safe-area-inset-*)` is plumbed through Chromium's own display-cutout
handling, and a WebView is a View inside somebody else's Activity, so every one
of those reads zero however correctly the host sets `viewport-fit`. The
stylesheet reads insets through variables that `env()` only supplies the
*default* for, and `setInsets` is the seam that lets the host write the truth.

No external requests at all (works offline and on a plane), `touch-action:none`
on the play surface so a drag is never a scroll, a hard 44px floor on anything
tappable, DPR capped at 2.5, render paused on `visibilitychange`, and a fluid
type and control scale so a 320dp and a 430dp phone get the same layout at two
sizes rather than two different layouts.

---

## The tooling is the point

`tools/` is where the claim in `../turnkey.md` gets paid for.

- **`core.js`** — the projection model, a 0–1 BFS **solver** (a step costs
  nothing, a turn costs one, so first-reach is provably the fewest turns
  possible), the generator, the vault curve, and `mint()`.
- **`gen.js`** — builds the ten authored cubes of Vault I.
- **`test.js`** — 15 assertions on the model: the 24 orientations, the
  world/view round trip on every cell of every orientation, the collapse, the
  burial, and that one key cannot open two doors.
- **`mint-test.js`** — mints 128 levels spanning vaults 2–13 plus samples out
  to level 1,000,001, re-solves every one from scratch, and asserts par is
  exactly as claimed, the fallback never fires, nothing exceeds its time bound,
  and the same level number is always the same cube.
- **`infinite.js`** — the same guarantee *in the running game*: 68 levels
  loaded through `loadLevel`, each solved as served, plus the palette-band
  invariants, determinism, and frame cost.
- **`probe.js`** — undo fidelity, 400 random turns against the legality and
  footing invariants, the back-gesture chain, inset injection, the dead-end
  detector, performance, and that no single bad tap can brick the app.
- **`drive.js`** — launches the real game in Chromium at 412×915, asks its own
  solver for each answer, and plays levels 1–16 **through the real input path**
  — mouse drags past the detent, taps on tile centres. Levels 11–16 are minted
  live during the run.

```sh
cd tools && npm install    # playwright, for the browser suites only
npm run all                # model -> mint guarantee -> build -> in-game -> probes -> real input
```

The core is inlined into `index.html` **byte-identical** to the module the
generator and tests run against, so the solver that verified a cube and the
game that serves it can never be different code. *(The build truncates
`core.js` at its Node export shim, so that shim must stay last in the file —
it once sat mid-file and the browser build silently lost every function after
it, while Node's hoisting kept the tests green. The comment above it says so.)*

Measured: **0.36 ms a frame resting, 0.72 ms mid-turn** with the full graphics
pass on the largest cube. Worst level load, minted cold, 421 ms.

The suites earn their keep. Three defects in this round were found only by
running the real thing rather than by reading it:

- A palette invariant that genuinely broke at **vault 22** — a deck in deep
  shadow fell below the brightest bedrock, making the two materials
  confusable. Now enforced per palette rather than trusted.
- A signed-shift bug that named half the cubes past level 40 *"undefined"*,
  caught in a screenshot, then swept across 4,000 levels.
- **The bad one.** Tapping the tile you already stand on produced an empty
  path — truthy, so it started a walk with nothing in it, and the next frame
  read `surf[undefined].w`. The exception escaped `loop()` before the next
  `requestAnimationFrame` was queued, so the chain died: no crash screen, no
  error, just a frozen game that ignored every tap until restart. It surfaced
  as two levels in the autoplay run doing nothing at all. Fixed twice over —
  the empty path is a no-op, and **the frame is now wrapped so the next one is
  always armed**, because no single defect should be able to brick the app.
  The console error is still emitted, so a harness watching for it fails
  loudly instead of passing quietly.

---

## Where it is weak

**Difficulty plateaus around vault 8.** Documented above with the measurements.
Vaults past it are a change of clothes and a lock count, not a change of
demand. Honest fix: new verbs, not bigger numbers.

**Generated cubes never have a joke in them.** They test the mechanic
competently and none of them will ever be *the* level people describe to each
other — the one whose whole point is a single absurd adjacency. Those still
have to be authored, and Vault I is the only place any exist.

**Nothing from the design doc's item list exists.** No Plumb, no Anchor, no
Lens, no enemies, no bosses. The verb is finished; the ladder Zelda's half was
supposed to contribute is not started — and it is exactly what would break the
par ceiling.

**And AR is not in here.** Rotating the cube by walking around a real table is
a WebXR session and a different input path, and it remains the single next
thing worth building.
