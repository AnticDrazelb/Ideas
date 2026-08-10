# TURNKEY

*A dungeon that is a cube. You read one flat face at a time — and everything
that lines up, touches.*

The playable answer to the question `../turnkey.md` ends on: **does the collapse
read as magic or as a bug?**

`index.html` is the whole game. One file, 74 KB, no network, no dependencies,
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
| **Tap** | walk. Pale bone tiles are decks; dark ones are bedrock; gaps are nothing. |
| **Drag** | turn the cube. Past halfway it commits, short of it springs back. |
| **Brightness** | is distance. The hairlines mark a drop you are allowed to ignore. |
| **Turning needs footing** | you may only turn if your own column still has a deck on the far side. |

That last one is the quiet one, and it is where the game lives: **where you
stand decides which turns you have, and which turn you take decides where you
can stand.** The lock and the key, falling out of the geometry rather than
being placed on top of it.

---

## What is in it

- 14 cubes, 5³ to 7³, par 1 to 8 turns.
- Keys and doors, which can be **buried** behind bedrock and only exist in the
  projections where nothing is in front of them.
- Turn-count par, best-per-cube, and three marks for clearing at the minimum.
- Undo, restart, and a hint that names only the next move.
- A dead-end detector: the solver re-runs from your live position after every
  action, so a cube you have made unwinnable says so at once.
- Procedural audio, haptics, an optional depth-numeral crutch.
- The reachable set drawn live — the connectivity graph, on screen, changing
  as you turn. It is the mechanic explaining itself with no words.

---

## Android

Built for a WebView, and the three seams a packaged host needs are published
on one object rather than scattered across globals:

```js
window.TURNKEY.onBack()                  // true = handled, false = finish the Activity
window.TURNKEY.pause() / .resume()       // lifecycle
window.TURNKEY.setInsets(t, r, b, l)     // the real display cutout, in CSS px
```

`setInsets` exists because **an Android WebView does not know where the notch
is** — `env(safe-area-inset-*)` is plumbed through Chromium's own display-cutout
handling, and a WebView is a View inside somebody else's Activity, so every one
of those reads zero however correctly the host sets `viewport-fit`. The
stylesheet therefore reads insets through variables that `env()` only supplies
the *default* for, and `setInsets` is the seam that lets the host write the
truth. In a plain browser nothing changes.

Everything else that matters on a phone: no external requests at all (works
offline and on a plane), `touch-action:none` on the play surface so a drag is
never a scroll or a pull-to-refresh, a hard 44px floor on anything tappable,
DPR capped at 2.5, render paused on `visibilitychange`, and a fluid type and
control scale so a 320dp phone and a 430dp phone get the same layout at two
sizes rather than two different layouts.

---

## The tooling is the point

`tools/` is where the claim in `../turnkey.md` gets paid for. The objection to
this design was always content: a tile placed for one face appears, unbidden,
in three others, and that is why FEZ took five years.

- **`core.js`** — the projection model, a 0–1 BFS **solver** (a step costs
  nothing, a turn costs one, so the first time it reaches the goal is provably
  the fewest turns possible), and a generator that carves a solution first and
  fills in around it.
- **`gen.js`** — the campaign curve. Each level is a *demand* — size, turns,
  locks — and cubes are generated against it until the solver agrees. A cube
  that can be walked without ever turning is thrown away; so is one where the
  decoy pass made the answer cheaper than the demand.
- **`test.js`** — 15 assertions on the model itself: the 24 orientations, the
  world/view round trip on every cell of every orientation, the collapse, the
  burial, and that one key cannot open two doors.
- **`drive.js`** — launches the real game in Chromium at 412×915, asks its own
  solver for each answer, and plays the whole campaign **through the real input
  path** — mouse drags past the detent, taps on tile centres. All 14 clear at
  exactly par.
- **`probe.js`** — undo fidelity, 400 random turns against the legality and
  footing invariants, the back-gesture chain, inset injection, the dead-end
  detector, and frame cost.

```sh
cd tools
npm install          # playwright, for drive/probe only
npm test             # the model
npm run gen          # regenerate + verify the campaign
npm run build        # assemble ../index.html
npm run drive        # play the campaign in a real browser
npm run probe        # invariants, hooks, performance
```

The core is inlined into `index.html` **byte-identical** to the module the
generator and the tests run against, so the solver that verified a cube and the
game that serves it can never be different code.

Measured on the largest cube mid-turn: **0.4 ms a frame, 572 quads.** At rest,
0.1 ms.

---

## Two things worth knowing about the renderer

**The board is drawn twice.** At rest the cube is square to the camera, every
side face has zero projected area, and the image *is* a flat grid — so it is
drawn as rects: crisper, cheaper, and the state a puzzle is actually read in.
The moment a drag begins, the honest 3D solid takes over. Both paths draw the
same front faces from the same numbers, so the hand-off is invisible.

**Perspective is eased in with the turn.** An orthographic rotation of
axis-aligned boxes produces no diagonals at all — halfway through a yaw the
front faces and the side faces are tilted by the same 45°, so lighting alone
cannot sell it and the turn reads as a smear of rectangles. So the projection
grows a little perspective as the turn opens and gives it all back as it lands,
scaled by |sin| of the angle. At rest the term is exactly zero, which is the
whole reason the two renderers agree.

---

## Where it is weak

**Fourteen cubes is a demo, not a campaign.** The generator can make more on
demand and the solver will vouch for them, but generated puzzles plateau: they
test the mechanic without ever having a *joke* in them. The set pieces — a cube
whose whole point is one absurd adjacency — still have to be authored, and
none here are.

**Nothing from the design doc's item list exists yet.** No Plumb, no Anchor, no
Lens, no enemies, no bosses. The verb is finished; the ladder Zelda's half was
supposed to contribute is not started. That is the right order — there was no
point building items for a collapse that might have read as a bug — but it
means this is one third of the game described in `../turnkey.md`.

**And AR, the thing that made this worth doing at all**, is not in here.
Rotating the cube by walking around a real table is a WebXR session and a
different input path, and it is the single next thing to build.
