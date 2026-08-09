# What Comes Next

A read on *Dreidel Royale* and *RINGSHIFT*, and five games that follow from them.

---

## Part 1 — What these two games have in common

They look nothing alike. A Hanukkah folk game and a Wipeout-shaped tunnel runner
share no genre, no audience and no camera. But they were made by the same hands and
they share six habits, and the habits are the thing worth building on — not the genres.

**1. One object, rendered like it has mass.**
Dreidel Royale's dreidel has real geometry, a real tumble, a scuff canvas the tip
draws into as it walks, dust, and coins that take a radiating impulse when a gimel
sweeps the pot. RINGSHIFT's ship is assembled from separate pieces specifically so
it can come apart on death instead of flashing. Both games spend their rendering
budget on making one thing feel physical rather than on making many things exist.

**2. A familiar form, plus exactly one new verb.**
Dreidel is a luck game with no decisions; Rising Stakes adds a convergence pressure
that turns it into a game with a shape. RINGSHIFT is Colour Switch / Super Hexagon —
and its own comments name the lineage — with **resonance** bolted on: the ability to
reach forward and rewrite the world instead of only surviving it. Neither game
invents a genre. Both add one verb to a genre and then build everything around it.

**3. No server, ever.**
PeerJS with well-known beacon peer IDs as rendezvous slots for matchmaking. A daily
conduit that is identical for everyone because it is seeded, not served. Local
save, local IAP ownership, local stats-driven unlocks. This is a real constraint you
have kept twice, and it has shaped both designs for the better.

**4. The interface is part of the fiction.**
RINGSHIFT's design header says it outright — "the interface is the readout" —
and colour is assigned meaning before it is assigned beauty. Dreidel Royale goes
further: pick the Grass Block dreidel with the Blocky Biome table and the *chrome
itself* goes voxel; Backyard plus Blue Pup turns the whole UI storybook-flat. Very
few small studios ship a second skin for their own UI, let alone a third.

**5. Progression with a voice and an end.**
ECHO — a mind that opens clipped and procedural and thaws as the distance falls,
across 246 levels and a counter that starts at 2.5 million light years and ends
measured in AU. Grades that fold *attempts* back into the star rating, because
anyone can three-star a level on the fortieth go. Hulls that are strict side-grades
so the early ships never become dead content. This is authored progression, not a
treadmill, and it is rarer than it sounds.

**6. You write your design documents as code comments.**
The ad policy section is a product decision with four placements, four rules, a 90s
global cooldown, a 120s session floor, and "RETRY IS SACRED" in capitals. The
safe-area block explains why an Android WebView reads `env(safe-area-inset-top)` as
zero and plumbs a seam for the host to write real insets. Par is defined as *a
statement about overdrive* so it self-tunes if the pace curve is ever retuned. The
next game should be picked partly on whether it gives this habit something to chew on.

**The gap.** Between them you have covered pure luck (Dreidel) and pure execution
(RINGSHIFT). You have not built anything where the interesting information is in
another person's head, and you have not built anything slow.

---

## Part 2 — Five games

Ordered by conviction, not by size.

### 1. Conduit Duel — RINGSHIFT, head to head

**The pitch.** Two ships, one seed, the same conduit. Resonance stops being a
convenience and becomes a social verb.

Right now resonance is the most original thing in either game and it is aimed at
nobody. You spend charge to carve a corridor through what is coming — for yourself.
The moment there is a second pilot in the same conduit, the same pulse becomes a
question with a person on the other end of it: convert a wedge to *your* colour and
you have made your opponent's next ring harder without touching them, because their
glyph is not your glyph. Fire it late in an inverted stretch, where the glyph
advances on its own and gaps are a deliberate hold, and you have taken away the one
ring they were rotating toward.

That is a genuinely new game, and it is mostly a recombination of parts you already
own: the PeerJS layer, lobby, quick match and share-invite flow lift out of Dreidel
Royale; the conduit, hulls, resonance and inversion are RINGSHIFT as it stands.

**Design notes**
- Ships share a conduit but not a lane. You see the other ship ahead or behind you
  on the same rings — that is the whole tension, and it is free rendering.
- Host authority, exactly as Dreidel Royale does it. The ring schedule is seeded,
  so the only thing on the wire is input and pulse events. Bandwidth is trivial.
- Score is distance plus chain, and death is not instant elimination — you respawn a
  few rings back and lose your multiplier. A twitch game where one player's early
  mistake ends the match is a bad party game.
- Hulls are already side-grades, which means they are already a matchup chart. HALO
  opening at x2 with gaps breaking the chain is a genuinely different opponent to
  WRAITH regrowing a shield every 20 rings.
- **Async first, if the sync version scares you.** The daily conduit already gives
  everyone the same level with no server. Add a ghost — their run replayed against
  yours — and the GIF share card becomes a challenge you send someone rather than a
  trophy you post.

**Why it is first.** It converts your one original mechanic into a reason for two
people to open the app, it makes your existing 246 levels more valuable rather than
less, and it needs no technology you have not already shipped.

**Risk.** Real-time P2P twitch is much less forgiving of jitter than a turn-based
dreidel spin. Ship the async ghost version first and treat live duels as the
follow-up — the ghost is most of the social value at a tenth of the netcode risk.

---

### 2. The Seder Night game

**The pitch.** Dreidel Royale's actual competitive advantage is not the dreidel. It
is that you built a beautiful thing for a specific night when a family is around a
table with phones in their pockets. There is a bigger night, and nobody has built
for it properly.

Passover is a long evening, largely sitting down, with children present who are
explicitly given a job to do. It already contains games: the four questions, the
hunt for the afikoman, the plagues, the songs at the end with counting and animals
in them. It does not need gamifying — it needs the same treatment you gave the
dreidel, which is to render something familiar with weight and care and get out of
the way.

**The AR hook is the one that justifies itself.** You already ship WebXR
`immersive-ar` with surface detection, placement and gestures, and — being honest —
in Dreidel Royale it is a lovely flourish rather than a reason to play. An afikoman
hidden in the actual room is the opposite: it is a real thing people really do, in a
place AR can actually see, at a moment when the adults would love to hand the kids
twenty minutes of structured chaos. That is the rare AR feature with a reason to
exist.

**Design notes**
- One host device hides it (or places it), phones hunt. Proximity warmth, no map.
  The P2P lobby is the same lobby you already have.
- Around it, a small table of side games in the Dreidel Royale idiom: a Ma Nishtana
  reader, a plagues round, a Chad Gadya / Echad Mi Yodea counting game. Small,
  optional, skippable.
- Keep it firmly on the parts that are already play. The ritual is not content.
- The calendar is a distribution strategy: an annual, dated, searched-for spike, and
  a sibling app that cross-promotes with the one you already have in December.

**Why it is second.** It is the highest-leverage thing you can do with assets that
are currently decorative, and it turns "I made a Hanukkah game" into "this studio
makes the games for the nights your family is together" — which is a much better
thing to be.

**Risk.** Tone. The afikoman hunt and the songs are play; most of the rest of the
evening is not, and the app should visibly know the difference.

---

### 3. A daily whose share card moves

**The pitch.** You wrote a GIF89a encoder from scratch so a run could be pasted
into a chat. You also built a deterministic daily that is the same for everyone
with no server. Those two things together are a distribution engine, and right now
they are serving a 246-level campaign that does not really need them.

Every daily puzzle on earth shares a grid of coloured squares. None of them share
three seconds of animation, because nobody else has an in-app encoder and a renderer
worth recording. That is an unfair advantage sitting idle.

**Design notes**
- The game wants to be **one object, one seed, one shot per day** — a physics
  problem, not a twitch problem, so the GIF is legible at three seconds and the
  result is comparable between people. Think a single throw, roll, drop or launch
  into a generated arrangement, scored on what it does.
- The share card is the design constraint, not a feature bolted on afterwards.
  If the run does not read as an animation, the run is wrong.
- Same seed for everyone worldwide, one attempt, a streak, and a result you cannot
  brag about without also showing the failure. That last part is why the animated
  card beats the grid — a grid hides how ugly the solve was.
- Ships as a small free app. It is a funnel and a habit, not a campaign.

**Why it is third.** Lowest cost of the five, highest ratio of reach to effort, and
it exercises exactly the machinery you have already paid for.

---

### 4. A game where you have to read a person

**The pitch.** The axis neither game touches. Dreidel Royale is pure luck — an
honest, deliberate choice, and the reason Rising Stakes had to be invented to give
it a shape. RINGSHIFT is pure execution. Nothing you have made yet has the property
that the most valuable information in the room is in someone else's head.

The natural form is a hidden-information dice game — Liar's Dice, Perudo, Skull —
and the fit with your stack is almost suspicious:

- **Turn-based, so P2P latency stops mattering.** The one real weakness of the
  PeerJS approach disappears entirely.
- **Host authority already exists** and is already the model for hidden state.
- **The tactile object work transfers directly.** A cup slammed down, dice spilling
  under it, the lift at the reveal. That is the dreidel's tumble-and-scuff craft
  pointed at new geometry, and it is the whole feel of the game.
- **Elimination and rising stakes are already built.** Losing a die per round is
  structurally the same as bleeding gelt, and you have already thought hard about
  making that converge instead of dragging.

**Risk.** It is a well-served category on both stores, so this one lives or dies on
presentation rather than design — which, on the evidence of these two files, is the
bet to make. But it is a fair reason to rank it below the three above.

---

### 5. Decision Dreidel, set free

**The pitch.** The most interesting thing hiding inside Dreidel Royale is the mode
that is not a game: the same 3D dreidel and the same spin, four faces carrying the
player's own text, no pot and no turns. A toy that is useful on a Tuesday.

Generalise it. A free app of beautifully simulated randomisers — a coin with real
edge-on tumble, a die, a top, a bottle, a wheel, an eight-ball — all with the weight
and sound and settle you already know how to do. People search for these constantly
and every result is a flat 2D nothing with an ad on it.

**Why it earns a place on a list of games.** It is a weekend of work from parts you
already have; it never goes stale and never needs updating; it feeds installs to
everything else you make; and it is a natural home for the same earned-cosmetics
economy Dreidel Royale already runs. It is the cheapest permanent asset on this list.

---

## Part 3 — A stretch, and two things not to build

**The stretch.** The RINGSHIFT audio engine describes itself as "a Wipeout-shaped
techno engine," and the renderer already drives a ship down a lane at speed with a
horizon every ten rings. There is a one-thumb anti-gravity racer very close to the
surface of that codebase. It is not on the main list because it needs tracks — real
authored geometry, which is a content pipeline neither of these games has ever
required — and that is a different kind of project to everything you have shipped.
Worth knowing it is there.

**Do not build a third solo twitch game.** RINGSHIFT is a five-year game. Another
one competes with it for the same players and the same hours, and would be the first
thing you have made that is not a new muscle.

**Do not build the thing that needs a server.** Both games are better for the
constraint. The moment there is a backend there is a bill, an outage, a privacy
policy, an account system and a dead app the day the bill stops being paid — and
what you would buy with it, matchmaking and leaderboards, you have already faked
convincingly twice with beacon peers and seeded dailies.

---

## The short version

If you want the biggest game: **Conduit Duel**, because resonance was always
secretly a two-player mechanic.

If you want the best business: **the Seder night game**, because a studio that owns
two nights of the calendar is a much stronger thing than one that owns one.

If you want something shipped in a fortnight: **the animated daily**, because the
encoder is already written.
