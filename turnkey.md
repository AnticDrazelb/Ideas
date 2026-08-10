# TURNKEY

*Codename: Fezelda. A top-down dungeon crawler that is secretly a cube — you
read one flat face at a time, and turning it rewrites what connects to what.*

*(Turn + key. Also a jailer. Also "ready to use." All three apply.)*

---

## What each half actually brings

**FEZ's mechanic is not "rotation."** Rotation is the input. The mechanic is:
*the projection is the physics.* Gomez lives on a 2D read of a 3D world, depth is
discarded, and **anything that lines up in the current view is touching** — even
if it's a hundred metres away in the world you can't see. You cross a chasm not by
jumping it but by finding an angle where it isn't there.

**Zelda's mechanic is the locked door.** A dungeon is a graph of rooms with edges
you can't traverse until you own the thing that traverses them, and the pleasure
is the moment the map folds together in your head.

Put them together and you get a dungeon where **the connectivity graph itself is
the puzzle, and turning the world is how you edit it.** The key isn't on the other
side of the door. The key is *behind* the door in a direction you aren't currently
looking, and from one face over, "behind" doesn't mean anything.

---

## The top-down version is better than FEZ's own

This is the part that convinced me, and it's worth being precise about.

FEZ is side-on, so it has **gravity**. That's a permanent tax on the mechanic:
every rotation has to answer "does the player now fall?", edges have to be
handled, and Polytron spent enormous effort making the collapse not throw you into
a pit by accident. Gravity fights the trick constantly.

Zelda is top-down. **There is no down.** You walk on whatever plane faces the
camera, and that's it. Which means the collapse can be total and merciless with
no special cases:

> The dungeon is a solid cube of tiles. The camera looks down one axis. Every tile
> sharing the other two coordinates **projects to the same square, and the same
> square is one place.** Turn the cube 90° and a different axis becomes depth.

A pillar from above is a dot; from the side it's a corridor you can walk the length
of. A pit from above is a doorway from the side. A staircase is a diagonal one way
and a single flat tile the other. The tower and the well are the same object seen
from opposite faces.

And the pure FEZ moment survives intact: **you can walk across a chasm because
something far behind it lines up.** The floor holding you up is a wall in another
room. You will never quite trust the ground, which is the correct feeling.

Six faces. Four turns per axis. One piece of geometry, and a completely different
map each time you turn it.

---

## What Zelda contributes that FEZ refuses

FEZ is a metroidvania of *knowledge* — Gomez never gains an ability, only you do.
That's a beautiful, purist design, and it's also why FEZ has no difficulty curve
and no fights. Zelda's half of this is the ladder FEZ deliberately doesn't have.

**Items don't cross gaps — rotation already does that. Items modify the turn.**

- **The Plumb** — pins one axis. That part of the world holds still while the rest
  re-projects around it. This is the hookshot of this game: the first time you get
  it, half the dungeon you'd written off opens.
- **The Anchor** — set an object down and it stays put in *world* space rather than
  view space, so you can carry things between faces. Block-pushing puzzles, in 3D,
  read through a 2D window.
- **The Lens** — the other faces, ghosted over the current one. This is the map
  item, and it's the difference between the game being fair and being cruel. Gate
  it early.
- **The Wedge** — an illegal 45°, held, not locked. Everything is briefly ambiguous
  and two things that never align are aligned for a second.

**Keys and doors survive unchanged**, and get much better: a key on one face and
its door on another turns a fetch into a routing problem.

**Enemies get genuinely nasty.** A monster is a 3D thing you only see projected. In
this view it's behind a wall and irrelevant. Turn the cube and it is standing next
to you, because it always was — you were looking down the axis that separated you.
Every rotation is a re-read of the threat map, and that is a kind of tension no
Zelda has had.

**Bosses fight the verb.** A weak point exposed in exactly one orientation, and an
attack pattern whose whole purpose is to make you turn away from it. You're
fighting the rotation as much as the monster.

---

## The second layer, which is the real reason to build it

FEZ's other half is the part people still write about fifteen years on: a
constructed alphabet, tetromino glyphs, codes that live on paper, secrets that
aren't in the inventory because they're in a notebook next to you.

Zelda has the same thing in an older form — burn this bush, bomb this wall, *it's
a secret to everybody*.

So this game gets a **notebook layer**, and it's the thing almost nobody ships any
more: markings on the dungeon walls that only align into a readable glyph from one
face; a numbering system you work out yourself; a door with no key at all that
opens to a sequence of turns. Not achievements — things you write down.

You've already built the reading surfaces for this twice over. RINGSHIFT's flight
manual is a browsable ship's console with text arriving at the speed someone is
saying it, and ECHO keeps a log that thaws over 246 levels. The muscle is there.

---

## Controls: you already solved this

RINGSHIFT's input line is *"tap to shift colour, drag to spin the ring."*

TURNKEY's is **"tap to move, drag to turn the world."**

That is the same hand, the same idiom, and the same one-finger ambiguity between a
tap and a drag that your own comments say you already took the timing clash out of.
Direct transfer.

And it means the cube wants **weight**: inertia in the spin, a detent that catches
at each 90°, a heavy settle. Which is exactly the craft in the dreidel's tumble and
in a ship built from separate pieces so it can come apart. **The one object with
mass, here, is the dungeon itself** — a puzzle box you turn in your hands. That
resolves the only place this concept looked like a poor fit for you.

---

## And then AR, which is the reason this is *your* game

You ship WebXR `immersive-ar` with surface detection, placement and gestures, and
in Dreidel Royale it's a lovely flourish rather than a reason to play.

Put the dungeon cube on a real table. **Rotate it by walking around it.**

The entire mechanic of FEZ, performed with your body. No drag, no button — you
lean, you crouch to read the bottom face, you walk to the far side of the table and
the map you were stuck on has become a different map. A game whose whole subject is
*a 2D creature discovering that its world has a hidden axis*, played by a 3D
creature moving through the axis the flat thing can't see.

That's not a feature list item. That's the trailer, and there is a very short list
of studios who already have the AR plumbing and the geometry chops to do it this
week.

---

## The daily

A single cube. One seed, everyone on earth, no server — the daily conduit's exact
model. Fewest turns wins, which is a cleaner and more comparable number than time.

And the GIF encoder you already wrote has never had better material: **a rotating
cube is the most legible three seconds of animation there is.** Someone else's
solution is genuinely worth watching, because the interesting part isn't dexterity,
it's the turn you didn't think of.

---

## The content objection, finally answered

I've now twice said the thing that keeps a project off your list is authored
content, because neither of your games has ever needed it — Dreidel Royale has no
levels and RINGSHIFT generates 246 from a plan and a hazard schedule.

**This is the design where that stops being true, and it's structural:**

> You author one cube of geometry and get four to six rooms out of it.

Every tile you place is load-bearing in multiple projections at once. The authoring
cost per *screen of gameplay* is a quarter of a normal dungeon's, and the density
comes free from the geometry rather than from drawing more. A 25-cube game is
somewhere between 100 and 150 distinct maps.

The cubes are also small — 8³ or so, voxel-ish, which your Blocky Biome mode
already proves you have the aesthetic for, and which is trivially cheap to render
next to what RINGSHIFT is doing every frame.

---

## Where it's weak

**Authoring is cheap per room but brutally hard per cube.** You place a tile for
one face and it appears, unbidden, in three others. This is the genuinely difficult
part and it doesn't get easier with practice — it's the reason FEZ took five years.

The answer is tooling, and it's the kind of tooling you clearly enjoy building: an
editor showing all six projections live, plus a solver that walks the connectivity
graph in every orientation and tells you what's reachable, what's *accidentally*
reachable, and whether the cube is still solvable after the tile you just moved.
Build the solver first. Without it this is unshippable; with it, it's a good time.

**Second: legibility.** If the player can't tell what's a wall, what's floor, and
what's a hundred metres behind the thing they're standing on, the collapse reads
as a bug rather than a rule. FEZ solved this with depth cues that were honest about
depth even though the physics wasn't. You'll need the same, and it is a
presentation problem — which, on the evidence of two files full of design tokens
and comments explaining why the hairline is that colour, is the right problem for
you to have.

**Third, briefly:** FEZ and Zelda are both live IP. The mechanics are nobody's
property; the names, the fez, and the pixel-art trade dress are. None of the above
depends on any of that.

---

## The prototype that answers the question

Two weekends, no art, and it settles it:

- One 8×8×8 cube of solid/empty voxels, hand-typed as text.
- Top-down orthographic view. Drag to rotate 90°. Tap a square to walk to it.
- Collapse rule: a square is walkable if *any* voxel along the depth axis is solid
  and the one above it (toward camera) is empty.
- One key, one door, no items, no enemies.

The whole thing rests on one question: **does the collapse read as magic or as a
bug?** The moment you walk out over a chasm on a floor that's actually a wall in a
room you can't see, either you laugh or you file a defect. You'll know in an hour,
and everything above is downstream of it.

---

*Note: `sugarlock.md` — the stack-inventory dungeon crawler from the Pez reading —
stays in the repo as its own idea. The mechanic there doesn't depend on the typo,
and the two aren't mutually exclusive: an inventory you can only pop from would sit
perfectly well inside a dungeon you can only turn.*
