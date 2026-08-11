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

### Plates — the second verb

**From cube 20, some cubes carry a PLATE.** Step on one and the cube's
*material* inverts: every deck becomes bedrock, every wall you have been
walking around becomes floor. The geometry does not move. What changes is
which of it you may stand on — so the projection you spent four turns
learning is now its own negative, and the route you were walking is a wall.
Step on it again and it goes back. **A plate is a door between two versions
of the same cube**, and the way out is usually only reachable from one of them.

Two kinds, two bits, **four material states** — and because the second is
applied after the first, the pair composes into a third layout neither makes
alone:

```
world 0    deck +     bedrock #     void .      as carved
world 1    deck #     bedrock +     void .      INVERT — the negative
world 2    deck +     bedrock .     void #      DRAIN — stone to air,
world 3    deck .     bedrock +     void #      both — a third world
```

DRAIN joins at vault V. The state space is now position × orientation × keys
× doors × **world**, and that is what lets a cube ask for turns the rotation
graph alone would never give up.

**A plate is always the surface of its column.** It's cut clean through the
rock, so it's legible from every face in every world — which is what makes
planning with them possible at all. That has a second consequence, and it's
the best thing about them: since a plate always gives footing, **every turn is
legal while you stand on one. Plates are pivots.** In a game where where you
stand decides which turns you have, a square that gives you all four is worth
walking a long way for. `tools/plate.js` asserts it across every orientation ×
turn combination.

Two rules fall out and both are deliberate: a plate may only be a path's
*destination*, never a cell it runs through — stepping on one rewrites the
board every later step was planned against, which also makes standing on a
plate a decision rather than something that happens to you on the way past.
And a plate you *turn* onto does not fire; you press it with a foot.

### Five seconds, and then back to the plate

**A flipped world lasts five seconds.** Then the rock springs back to how it
was carved, and you are put back on **the nearest plate you can stand on.**

The plate was the biggest verb in the game and it cost nothing: flip, look
around, think, flip back. The clock turns it from a place you visit into a
thing you commit to — you read the inverted cube *before* you press it,
because afterwards there is only time to walk the line you already have.

**Where you land is not a detail, it is the whole design.** Measured across
the plate cubes:

> **39 of 41 have an exit that does not exist in world 0.**

The way out is carved into cells that are rock until the material inverts —
that is the point of the second verb. So a spring-back that dropped you on the
nearest walkable *square* dropped you into the world where the level cannot be
finished, several moves from the only thing that could put you back. It read
exactly as it played: gates and an exit sitting on stone, forever out of
reach. A plate is exempt from the flip and always the surface of its column,
so one always qualifies as a landing — there is no world in which the thing
that gets you out is itself out of reach. **Running out of time costs you the
walk, never the cube.**

**Tapping the plate under you presses it again.** Without that, re-firing
would need a walkable neighbour to step off to and back onto, and a plate cut
through a wall of rock does not always have one — that is a soft lock, stood
on the one thing that can change the board and unable to use it. Being *put*
on a plate does not fire it (same rule as turning onto one: things that happen
to you are not you pressing it); tapping it does, because that is a foot.

Two more rules keep it a challenge rather than a punishment:

- **The expiry takes an undo point first,** and hands back a full five
  seconds rather than the tenth you had left. Undo is a retry, so it is a
  whole run at it.
- **It runs on game time and stops for a card.** The flip spends a hitstop and
  most of a second of slow motion and you cannot act in either. Pause, the
  manual and the win screen are not play. A held peek *is* play, and the clock
  keeps running — thinking is the thing being rationed.

**The solver has no clock in it,** so par stays a lower bound: the carved
route is still in the cube, the keys still open the doors in some order, and
the HUD number is still the fewest turns the *geometry* can be beaten in. What
the clock adds is a demand on the hands, not a change to the cube. Putting it
in the search would mean carrying real time in the state key — a 0-1 BFS over
a few thousand states becoming a search over a continuum — to answer a
question the player is better placed to answer than the solver.

### Things that are not in this world are not drawn

A key, a gate and the way out are carved into cells, and a plate rewrites what
those cells are *made of*. They used to be drawn regardless, which was the
single most misleading thing on the board: **a gate sitting on stone with no
way to reach it does not read as "not in this world yet", it reads as a broken
game** — and a player is right to read it that way, because nothing on screen
distinguished it from one they simply had not found the route to.

So they are not drawn until they are real. The exit arrives with the flip,
which is also the moment it becomes true.

**The test is the material, not the route,** and that distinction is
load-bearing. Showing you a way out you cannot walk to *from this face* is the
game — it is the whole of what the first two cubes teach, and hiding it
because it is far away would delete the lesson. Hiding it because it is not
made of floor yet is the opposite: it stops the board claiming something that
is not so. (`walkable()` would have been the wrong test for the mirror reason
— it refuses shut doors, so every locked gate would have vanished, which is
precisely the thing you most need to see.)

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

**Plates are what raised it.** A second transform multiplies the state space
instead of extending it, and it moves the measured ceiling by two:

```
                  no plate      one plate
n=6                max par 3     max par 5
n=7                max par 4     max par 6
and roughly 2.5x as many cubes land at par 3 or better
```

Measured across the running curve: **vault I averages par 2.5, vaults V+
average 4.4, and par 6 turns up regularly** — against an old ceiling that
plateaued at 3.9 and never produced a 5.

Two counter-intuitive results, both now recorded in the code because both cost
a rebuild to learn:

- **Two plates is worse than one** — max par falls back to 5. It's the same
  trap as the decoy pass and the leg length: more freedom to reach any world
  is more ways to shortcut, and the search finds them. So exactly one plate
  is carried, and past vault V the variety comes from *which* plate it is.
- **The demand is deliberately not raised for carrying one.** Asking for the
  new ceiling put two thirds of mints outside their own band, because the tail
  of the distribution is thin and a 240 ms search can't be relied on to find
  it. The band stays where it's reliably met and the scorer reaches for the
  top of it — so plates show up as *fours where the old curve gave threes*,
  rather than as a promise the generator misses.

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

### The art pass — making a block a solid

The board was legible and it was flat. Forty-nine squares of material in a
grid is a *chart*: material alone says what a thing is made of and nothing
about its shape, and none of the shape information was anywhere, because the
pattern is tiling detail and tiling detail has no edges.

**The pattern grid and the render grid are now different numbers.** The
material is still authored at 16 — bricks four to a face, mortar one texel
wide — and that chunkiness is the look. But lighting is not made of texels,
and at 16 a bevel is a staircase. So a block bakes at **64**, the pattern is
point-sampled into it (every brick stays exactly as crisp as authored), and
only the smooth field is drawn at the finer grid. Four things go into it:

| | |
|---|---|
| **Bevel** | the top edge catches, the underside falls away — "this has thickness" in one term |
| **Occlusion** | the outer seventh darkens toward the block's own boundary, so two neighbours get a seam and the grid becomes a wall of separate stones |
| **Rim** | one thin bright line, and *only* along the two edges facing the light. The first version ran it all the way round, which is not a solid catching a light, it is a button |
| **Sheen** | a broad weak highlight on decks only, because a laid floor is smoother than shot rock |

Plus **detail at more than one scale** — a coarse mottle across a face and a
fine tooth below the size of anything nameable. With only the authored middle
scale present, a block reads as a printed swatch however well it is lit.

**The middle of every block is left exactly alone**, and that is not an
aesthetic choice. Cast shadows in this renderer stop at 42% of a tile so the
centre always shows its true material undarkened — that is what keeps the
deck/bedrock band readable under shadow. This field obeys the same law: the
multiplier at a block's centre is exactly 1.

**It is baked into the texture, not laid over the board.** There are two
renderers — the flat board at rest and the honest solid mid-turn — and the one
thing they must never do is disagree, because the handover happens on the
first pixel of a drag. A screen-space overlay would have to be written twice,
in two coordinate systems, and would pop the instant a finger moved.

**Every stone is laid separately.** The block field gives a block a top, an
underside and a thickness; inside it the material was still perfectly flat.
The fix needed no new art, because **the height map was already there** — a
pattern is a field of multipliers, and the places it dips are the places the
surface dips: mortar courses, cobble seams, the gaps between planks. Read as
height, differentiated at the render grid off a *bilinear* lift of the coarse
pattern, and lit from the same direction the bevel says the light is. The
albedo stays point-sampled and hard-edged while the slope it implies is four
texels wide and smooth, so a mortar course keeps its crisp dark line and gains
a lit shoulder on one side and a shaded one on the other. It works for every
material kit in the game — including ones nobody has written yet — without a
line of per-material code, and it is built once per vault.

**The board sits in a vault.** The edge used to be two hairlines, which is a
diagram's convention: it says "the picture ends here" and nothing else. A
bezel gives the board a *reason* to end where it does, frames the composition
so the eye is delivered to the puzzle, and is the one surface in the game
allowed to be made of something other than the cube. It does not turn with the
cube — drawn in screen space in both renderers, so a drag rotates the cube
*inside* a vault that stays put, which is the correct reading of the gesture
and also means it cannot pop at the handover. Bezel, corner hardware, outer
shadow and the darkness it throws onto the board all bake into one sprite.

Its thickness and clearance are declared **in tiles**, and `layout()` reads the
same two numbers when it decides how large a tile may be — so the tiles give
back exactly what the frame takes and it can never grow off the edge of a
screen. The fit test now measures that footprint rather than the tile rect,
because the moment a bezel existed, measuring the tiles alone was measuring
the wrong rectangle.

**The board also stopped floating.** Every block throws a soft shadow down and
out, drawn under all of them, so it survives only in the void columns and
around the outside — exactly the places the eye was being told nothing. And
the sky got a far side: a handful of very large, very faint blooms, painted
once per vault, so no two regions of the screen are the same value.

**Grain baked inside the vignette.** Not the per-tile material noise deleted
long ago — that one claimed the stone was rough and fought the pixels it sat
on. This is the *image*: a radial falloff over flat dark sky bands visibly on
an OLED, and noise dithers the steps away. It lives inside the vignette sheet
because both are full-screen operations and a full-screen operation at DPR 3
is three million pixels; fused and cached they cost **one blit, less than the
gradient fill alone used to.** The trade is that the grain does not move —
moving grain reads as film, still grain reads as texture on the glass, and
film would cost a second full-screen blend every frame forever to animate
something at the limit of visibility.

Measured after all of it: **0.4 ms resting, 2.9 ms mid-turn** on the largest
cube, against an 8 ms budget.

Two performance traps were found by shipping it, both worth recording:

- **The shade field was transcendentals per texel.** Invisible at rest, since
  textures are cached — and a stutter at the worst possible moment, because a
  plate flip rebakes every block at once, mid-flip, while the frame is already
  spending a hitstop and a slow-motion. It is a lookup now: one multiply per
  texel, ~0.3 ms a block cold.
- **Fill rate, three times, and `draw()` timing never saw any of it.** The
  grain went from 32 tiled `overlay` draws, to one `overlay` draw, to living
  inside the vignette on the ordinary blend path; the contact shadow went from
  49 blurred sprites a frame to one cached silhouette. None of it moved the
  `draw()` numbers, because the cost lands in the rasteriser rather than the
  loop being timed. What caught it was the **plate clock**: a countdown is a
  real-time measurement, so it is also an accidental frame-health monitor, and
  it failed the build over a graphics change by losing a fifth of its five
  seconds to frames over the 64 ms dt ceiling. Measured on real `rAF` pacing,
  frames over that ceiling went **80/180 → 1/180**, better than before the art
  pass began.

### The interface, and one wrong turn on the way to it

Asked for chrome with the confidence of a shipped console game, the first
attempt grew a moulded face, a lit lip, and a hard offset shadow standing in
for the *side* of a plaque. That is a real look, and it belongs to a real
genre — the chunky, bevelled, high-saturation panel of a free-to-play mobile
title. It is precisely the register the request was trying to get away from.
Worse, it was a fixed cool navy, so on a warm vault it did not read as part of
the game at all: it read as a component library dropped on top of one.

**Nintendo's own interface is the opposite of chunky.** It is restraint: few
elements, generous air, exact type, one accent, and chrome that gets out of
the way of the thing you are looking at. So:

- No bevel, no offset, no gradient pretending to be a moulding.
- The plate is **glass over the world** and blurs what is behind it, so it
  belongs to whatever vault it is sitting on rather than to a stylesheet.
- A hairline, brighter along the top edge only, because that is where the
  light is everywhere else in this game.
- **The tint comes from the vault palette at runtime** — one line in
  `setStyle()` — so the chrome warms and cools with the stone, and every pane
  in the game takes the same value. Otherwise it ships two chromes: warm
  readouts over a warm vault with a cold toast sitting between them.
- And the type does the work. Label small, wide, dim; number a rung and a half
  above it, tight, lit and tabular so a 1 and a 4 do not shuffle the pill's
  width. Hierarchy instead of decoration.

One bug fell out of giving the pills a real edge: the level name's fade mask
sat on the **pill** rather than the label, so a long name did not read as
trimmed — it read as a readout with its right-hand end missing.

### The marker is a hole, and it is a real one

**You are a black hole, and what is inside it is the sky behind the board.**
Not a starfield of its own, not a magnified copy, not a tinted one — the same
pixels that would be there if the deck were not, in register with the void
columns to the pixel. Stand beside a gap in the cube and the stars run
straight across from one to the other, because they are one starfield seen
through two holes.

The deck under the marker is *removed* rather than covered: `clearRect` obeys
the clip, so the disc is punched clean out of everything already painted, down
to bare canvas — which is exactly what a void column is. Then the sky patch
goes in, blitted in **screen** space rather than board space, because the
starfield deliberately sits outside the camera. A shake has to slide the board
across a hole whose contents do not budge; sampling under the camera transform
would have dragged the sky along with the board and quietly undone the whole
effect.

That registration is the point, and it is why none of the obvious
enhancements survive. The interior was a 2.6× copy for a while, turning slowly
at half strength, with a fall to absolute black across the inner two thirds
and seven painted "lensed" points to give the resulting smear some structure.
Every one of those was a reason the picture inside could not be the picture
behind — and a hole showing its own private sky is not a hole, it is a
porthole onto somewhere else. The eye reads the difference long before it can
name it. What is left is an edge (a photon ring on the silhouette, a lensing
halo outside it, a contact shadow on the block), the sky, and whatever is
directly opposite you in the cube, glimpsed in front of it.

**The conduit is gone**, and it is worth writing down why. A lit throat used
to run from the cell underfoot, through the centre of the cube, to the cell
opposite, drawn only under a peek — on the argument that a black hole which is
a way through ought to show the way through. It was wrong twice. In code: the
ring generator it called was never written, so every peek held past the fade
threw out of the frame, and the loop's own guard caught it, reset the walk and
re-settled the board, once per frame, for as long as the finger stayed down —
never a crash, just a game that quietly stopped obeying you. And in design:
the antipode is a *look* and nothing else — no reach, no route, no bearing on
any turn. A tube between two cells promises you can travel it, and the game
has exactly one way to move and it is not that.

---

## Feel

Every beat is built toward one specific sensation rather than "add
particles". There are five, and the whole feel layer is downstream of them:

| | |
|---|---|
| a **step** | purposeful, weighted. Not a cursor moving. |
| a **drag** | heavy. You are turning a stone vault, and it resists — it creaks under the thumb, louder near the detent, so the commit threshold is something you *hear coming*. |
| a **landing** | a mechanism seating. The most important feel in the game, because it is the verb. |
| a **refusal** | immediate and physical. The cube tries, hits the stop, slams back. |
| the **exit** | earned. The board comes apart toward you. |

**The landing gets the most work,** and the single biggest thing in it is the
easing curve. A plain ease-out arrives at 90° and stops, which is what a
slideshow does. A real latch goes slightly *past* its seat and is pulled back
into it — so the turn overshoots by a few degrees and settles, and that
half-frame of coming back is most of what makes it feel like a mechanism
instead of a transition. Around it: a camera kick along the turn's own axis
(so the shake tells you which way the mass went), grit shaken off the whole
cube, dust off the player's tile, and a three-layer sound — the latch, the
stone it's set in, and the mass arriving underneath a beat late.

**The kick moves the board, never the HUD.** A shaken readout is an unreadable
one; the DOM chrome staying nailed down is what lets the impact be as big as
it is.

**The reveal sweep is juice and teaching at once.** After every turn the lit
reachable set sweeps outward from the player in BFS order rather than snapping
on — so what you are watching is the connectivity graph being traced, one tile
at a time. It is the prettiest thing on screen and it is also the answer to
"what did that turn buy me", drawn.

**One mechanism serves both ends of a level.** Entering, the board assembles
outward from where you stand; clearing it, the board comes apart outward from
the door you just walked through — with you drawn down into it, spinning, over
a quarter of a second. Same code, sign flipped, which is why arriving and
leaving feel like the same place doing the same thing in opposite directions.
The clear card doesn't appear over a still image of a solved puzzle; it appears
after the puzzle has left.

**Audio is a bus, not six loose oscillators.** Everything lands on
master → compressor → out with a send to a cheap feedback-delay reverb, because
the difference between "a game with sounds in it" and "a game that feels like a
place" is that the sounds share a room. A dry click in silence is a UI beep;
the same click with 180 ms of dark tail is stone. Under it, an ambient bed of
two detuned saws through a heavy lowpass, pitched off the vault number, at a
level you notice only when it stops. Steps drift in pitch so a run of them
sounds like walking rather than a stuck record.

**The wordmark is cut from the same stone.** It was type in a system font
with letter-spacing on it, which is a fine way to label a screen and a poor
way to open a game about stone cubes. It's now a 5×7 bitmap per letter with
every lit cell extruded, bevelled and mortared — rendered once into an
offscreen canvas at boot and handed to an `<img>`, so it costs one canvas and
zero frames and still scales with the layout. The extrusion is stamped once
per pixel of depth rather than built face-by-face: nine stamps of a flat shape
cannot have a seam, and the face-by-face version came out smeared on every
diagonal.

**The title screen is a stage, not a pane of glass.** It used to blur the
whole plate, which meant the cube — the one thing the screen is about — came
out as mush behind the type. The chrome now owns a band at each end and the
middle is left completely alone: no blur, no scrim. In play the camera is
square to a face because that's the surface the puzzle is read on; the attract
screen has no such duty, so it gets a real three-quarter hero pose on a
plinth, fitted by projecting the eight corners and scaling to whatever box
they actually came out as.

**Light from inside, and only there.** A warm gradient is drawn *before* the
cube and never masked, so the only places it survives are the void columns —
the gaps you're looking straight through. The cube appears lit from within and
it costs one gradient, because the geometry does the masking for free. It was
briefly on the play board too, and that was a mistake worth recording: an
additive layer under the tiles lifts *bedrock* as much as deck, which eats the
luminance separation the whole board is read off — and the palette test
couldn't see it, because it measures `tileFill` and this was painted
underneath. Atmosphere doesn't get to cost legibility on the surface the
puzzle is solved on.

**The title screen is the engine.** It used to be type on a gradient; it is now
type over a real cube tumbling behind the glass, with the play furniture hidden
and the scrim thin enough to see through. It costs nothing — the renderer
already existed and the menus were drawing nothing at all — and it means the
first thing anyone sees is the one idea the game has.

Nothing here makes the player wait. Every transition is under 420 ms and none
of them gate input that could have been taken.

---

## Mobile is the target, not a viewport

This is built to be an app, and everything below exists because it went wrong
on an actual phone rather than in a headless browser.

**The board always fits.** The attract cube was sized at 1.12× the smaller
screen axis, which hung **23–26 px of board off both edges of every phone
tested** — and a tile you cannot see is a tile you cannot tap. The board is
square and a phone is not, so the width always binds; `layout()` now clamps to
both axes and spends the width completely. Eleven viewports × four states are
asserted in `tools/mobile.js`; the tightest margin is 6 px on an iPhone SE.

**The slack goes under the thumb.** A square board on a 19.5:9 screen leaves
~45 % vertical. Rather than centre it and crowd the controls, the board is
biased upward so the gap below is larger than the gap above — that's where the
hand is, and where the three things it presses live.

**Readouts and controls are sized differently.** The level name and turn
counter aren't buttons, so they use a shorter rung and give the board back the
difference. Anything *tappable* keeps the hard 44 px floor, and a test walks
every control to prove it.

**`visualViewport`, not `innerHeight`.** On iOS Safari `innerHeight` includes
the strip the URL bar is sitting on, so the board was being sized against space
the player can neither see nor touch — and Safari collapses and expands those
bars while you play.

**The dimmest deck has a floor.** A deck at the far end of the cube was coming
out near-black-brown and reading as *bedrock* in real light — the one mistake
this game cannot afford, since "may I stand there" is the question every frame
is asked. The deck ramp now has a luminance floor and the bedrock ramp is
walked down from wherever that lands. Deck-to-bedrock separation went from 17.6
to 41.3, and from 4.0 to 20.7 under the deepest cast shadow.

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
- **`plate.js`** — plates appear only from cube 20, exactly one per cube,
  stepping on one really changes the walkable set, the world it leaves is
  solvable with valid footing, undo restores the world, and the pivot property
  holds across every orientation × turn. Then the clock: it starts at five
  seconds and drains on the HUD as well as in the state, the world springs
  back on its own, and the landing is staged rather than waited for — the
  player is put on a cell the carve gives no footing to and the world is
  sprung back under them, which must leave them on a plate, must not fire it,
  and must leave that plate pressable.
- **`hole.js`** — the two claims the player marker makes. That what is inside
  the horizon *is* the starfield behind the board, checked by compositing the
  sky by itself and demanding the pixels match (they do, to 0/255 across nine
  samples inside the disc). And that a peek held for a second neither throws
  nor moves anything — the regression test for a conduit that called a tunnel
  generator nobody ever wrote.
- **`portable.js`** — mints 67 levels in **Node and in Chromium** and demands
  the cubes be byte-identical. Two V8 builds is the closest thing available
  here to two different phones, and it exists because the same-environment
  determinism test passed twice while the promise was broken.
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
- **The same level number generated a different cube on different machines.**
  Two separate causes, and the existing determinism test could see neither
  because it only ever re-ran in one environment. First, the candidate search
  was *time-boxed*, so a faster device got through more candidates and picked
  a different winner — cube 41 was a five-turn puzzle in Chromium and a
  three-turn puzzle in Node. Second, the generator shuffled with
  `[0,1,2,3].sort(() => rng() - 0.5)`; a random comparator is **inconsistent**,
  so how many numbers it draws off the stream is a property of the engine's
  sort implementation, and Node's V8 and Chromium's V8 disagree. Two Android
  WebView versions would have too. Work is now *counted*, never timed, and
  shuffling is Fisher-Yates. `portable.js` is the test that can actually see
  this class of bug.
- **A curve that went flat because one field meant two things.** `spec.turns`
  is the *carve's* ambition — how many times the generator's own route turns
  while cutting the cube. `parLo` is the bottom of the *acceptance band*. They
  were the same field, so widening the band down to 2 quietly told the
  generator to cut two-turn routes, and a four-turn optimum cannot exist in a
  cube whose route never needed one. Three rounds of tuning the scorer, the
  decoys and the sample size moved nothing; printing the candidate histogram
  showed it in one line — `{0:7, 1:11, 2:16}` where a direct probe had shown a
  fifth of cubes at par 3 or better.
- **A door on a plate.** The generator placed locks on route cells and plates
  on route cells and never checked they weren't the *same* cell — so a shut
  door sat on a plate and blocked its own column, silently destroying the one
  property plates are built around. The pivot test caught it on its first run:
  192 of 576 orientation × turn checks failed, all with the same cause.
- **The worst one, found by a player on a real phone.** The attract cube on the
  title screen tumbled by rotating `M` — *the real game basis* — while `pos`
  stayed at the level's opening cell. Leaving the title then dropped you into a
  live level **standing inside the rock**: your cell was no longer the surface
  of its column, so the lit reachable set was stale, turn legality was computed
  from a position that did not exist, and the cube could be unwinnable. And
  `btnManualBack` skipped `loadLevel` entirely whenever a level object already
  existed — which it always did, because the attract cube had loaded one. So
  **every first-time player hit it**, since BEGIN → GOT IT was the only way in.
  The attract cube now owns `demoM`/`demoSurf` and cannot reach the rules at
  all; `tools/mobile.js` asserts footing is valid on all four routes into a
  level, and that six forced attract turns leave `M`, `pos` and `surf` byte-for-byte
  untouched.
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

**Difficulty still plateaus, just higher.** Plates moved the ceiling from 4 to
6 and the reliable band from 3 to 4, which bought several vaults — but the
same argument applies again one level up. A *third* transform would move it
again; more of the same two will not.

**Generated cubes never have a joke in them.** They test the mechanic
competently and none of them will ever be *the* level people describe to each
other — the one whose whole point is a single absurd adjacency. Those still
have to be authored, and Vault I is the only place any exist.

**The rest of the design doc's item list still doesn't exist.** No Plumb, no
Anchor, no Lens, no enemies, no bosses. Plates are the first rung of the ladder
Zelda's half was supposed to contribute, and they did exactly what that ladder
was predicted to do — so the remaining rungs are worth building for the same
reason.

**And AR is not in here.** Rotating the cube by walking around a real table is
a WebXR session and a different input path, and it remains the single next
thing worth building.
