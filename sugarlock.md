# SUGARLOCK

*Codename: Pezelda. A dungeon crawler where your inventory is a spring-loaded
candy dispenser — and you can see what's coming, but you cannot change the order.*

---

## Why the mashup works

Most mashups are a skin. This one isn't, because the candy dispenser is not a
shape — it's a **data structure with a spring in it**. A stack. You load from the
top, you dispense from the top, and you cannot reach the fourth brick without
spending the three above it. That is a hard constraint with a physical body, and
it lands directly on the one thing a Zelda-like is actually about.

Zelda's core question is *which item opens this door*. Once you own the hookshot,
you own it forever, and the puzzle is recognition — you see the gap, you remember
the tool. It's a lovely question and it's answered the moment you've played three
of them.

Put the inventory in a dispenser and the question changes to:

> **Not "do I have it?" but "when will I have it, and what do I burn to get there?"**

You still find a blue tablet that freezes water. But it's the ninth brick down,
the room needs it now, and the eight above it are yours to spend or waste. That's
a planning game hiding inside a dungeon crawl, and it is not a genre anyone has
built.

The rest of the toy carries its weight too, which is the test of a real mashup
rather than a joke:

| Pez, the actual object | Becomes |
| --- | --- |
| Stack: load top, dispense top | The inventory rule, and the whole design |
| ~12 bricks, then it's empty | The run's length, ammo, and health, all one number |
| Flavours are colours | Items are colours — red burns, blue freezes, white unlocks |
| The head tips back and *clicks* | The verb, and the game's entire feel |
| People collect the heads, not the candy | The meta-progression, and the storefront |

Nothing there is decoration. Every part of the toy generates a rule.

---

## The character is the dispenser

Not "a knight holding a dispenser." **You are the dispenser** — a hinged head on a
translucent body with a visible stack of bricks inside it.

This gives you the most extreme version of the principle RINGSHIFT already runs
on. There is no HUD. Your body *is* the readout:

- **Your remaining items** — visible as coloured bricks through your own chest.
- **Your remaining actions** — the same bricks.
- **Your remaining life** — the same bricks. An empty dispenser has nothing
  holding its head up, and a head with nothing under it flops. That's death.

One resource, one object, no overlay. Every puzzle you solve costs you exactly as
much as it costs you to survive, because they are the same tablets. That tension
is the game, and it's *legible at a glance* because it's a stack of coloured
bricks in a clear body.

**The sword is free.** Basic melee costs nothing — otherwise every fight is a
tax on your ability to open doors and the game becomes miserable. Tablets are the
*items*: fire, ice, grapple, spark, key. Zelda's grammar survives intact.

---

## The two moments

The loop has exactly two modes, and they're cleanly separated. That's rare and
worth protecting.

### Loading — the puzzle

You find a loader. It holds a pile of loose tablets and it lets you press them
into your body in **any order you like**. This is the planning phase, and it's
where the actual thinking happens. You are building a 12-step program you'll then
be forced to execute in order.

### Running — the execution

Now you can only pop. The stack is fixed, you can see it, and the dungeon does not
care what you hoped for.

Three things make this interesting rather than punishing:

**1. Full lookahead, zero reordering.** You can always see your whole stack. This
is deliberately *more* generous than RINGSHIFT's inversion, which gives you a
deterministic +1 and no more — but it's the same family of idea, and it makes this
a game of planning rather than reaction. That's on purpose; see "what this fills"
below.

**2. You can always eat the top one.** Burning down to the tablet you want is
possible, and it isn't free and isn't pure loss: eating gives you a short sugar
rush — faster moves, a few turns — and then a crash where you're slowed. So
"waste three to reach the blue" is a real tactical option with a real bill.

**3. Pick-ups go on top.** A tablet you find in a room is the tablet you must use
*next*, or eat. This is the good kind of cruel: level design can hand you the exact
answer and then make holding onto it the hard part.

### And then: adjacency

Pop two at once and the colours **mix**. Red + blue makes something neither one
does. That single rule turns loading from "what order" into "what pairs," which is
where the real depth is, and it costs one line of design to add and years to
exhaust.

---

## Make it turn-based on a grid

Strong recommendation, and it's the difference between this shipping and not.

Action-Zelda on a touchscreen is a solved problem in the sense that everyone has
solved it badly. Go the other way: **a grid, and turns.** Every tap is one move or
one pop. Think Hoplite or Into the Breach wearing a dungeon crawler's clothes.

Four things fall out of it, all good:

- **One thumb, no virtual stick, no dead zone, no apology.**
- **The planning game becomes legible.** You can't reason about stack order at
  60fps. You can reason about it beautifully when the world waits for you.
- **The content gets cheaper.** A turn-based grid room is authored in minutes and
  is verifiable — you can *prove* a room is solvable with a given stack. That
  matters enormously, and I'll come back to it.
- **It's the slow game.** Dreidel Royale is pure luck, RINGSHIFT is pure
  execution, and I said last time that nothing you'd built turns on thinking with
  the clock stopped. This is that game, and it isn't a departure — it's the third
  corner.

---

## The heads

Collecting the dispensers, not eating the candy, is the entire culture of the
object. So the meta is a **shelf** — your heads rendered as real objects on a real
shelf, lit properly, tapped to turn.

And a head is not a hat. It changes what popping *does*:

- one **spits** the tablet as a projectile
- one **drops** it at your feet as a mine
- one **chews** it — no effect fired, but you keep the buff for several turns
- one **doubles** — pops two, always mixes, empties you twice as fast

Side-grades, never upgrades — the rule your hulls already follow, and for the same
reason: a later head should change how the game plays, not simply play it better,
or the early heads become dead content and the shelf becomes a balance problem.

Unlocks come from lifetime stats, exactly as the dreidel skins do. And this is the
rare game where a one-time "full collection" IAP is not a compromise but an
*authentic* one — completing a set is what the object is for. You've already
shipped that exact purchase once.

---

## The daily

Same dungeon, same twelve tablets, same order, everyone on earth, no server —
seeded, the way the daily conduit already is. Score is **fewest pops**, which is a
purer and more comparable number than time.

And the share card is already written: three seconds of GIF showing your solve.
A stack-planning puzzle is *exactly* the thing where watching someone else's
solution is interesting — you see them burn two tablets you didn't have to, or
find a mix you missed. Grid-based, turn-based, single-screen: it will read
perfectly at three seconds, which is more than can be said for a tunnel run.

---

## What this fills

Against the six habits both your games already share:

- **One object with mass** — the dispenser is the character. The spring, the hard
  stop of the hinge, the chalky translucent body, a missed tablet clattering off a
  wall and tumbling. That's the dreidel's craft pointed at a new object.
- **Familiar form, one new verb** — Zelda-like, plus "you may only pop."
- **The interface is the readout** — taken further than either game takes it. There
  is no interface.
- **No server** — none needed.
- **Authored progression** — a dungeon has an end, and something can be waiting at it.
- **Design docs as code** — a solvability prover, a lock grammar, a mixing table.
  This has plenty to chew on.

And it fills the two gaps: it's the **slow** game, and it's the first one where
you're thinking rather than reacting or hoping.

---

## Where it's weak

**Content is the real risk, and it's the same one that keeps the racer off the
list.** Neither of your games has ever needed authored levels — Dreidel Royale has
no levels at all, and RINGSHIFT generates 246 from a plan and a hazard schedule.
A dungeon crawler traditionally needs rooms drawn by a person, and that is a
different kind of project.

Three ways out, and I'd take all three:

1. **Single-screen rooms, not an overworld.** Sixty puzzle boxes is a tractable
   number. An overworld is not.
2. **Generate the rooms, author the grammar.** The generator places a door that
   needs blue and guarantees a reachable blue — the same engineering instinct as
   the difficulty contract and the hazard schedule, applied to locks. Then run the
   solver over every generated room and throw away the ones it can't beat.
3. **Hand-author only the bosses.** Six set pieces, not six hundred rooms.

**The second risk: is there enough game in it?** The stack is a great constraint,
but a constraint is not a game. It needs enough distinct tablet colours and enough
mixes that loading stays interesting at hour five — and that's a tuning problem you
can't answer on paper. The honest test is a prototype with four colours, one head,
and ten rooms. If ordering four tablets is already tense, it scales. If it isn't,
no amount of content will save it and you'll know inside a week.

**And the naming, briefly.** Pez is a live trademark and the specific licensed
heads are somebody's IP; Zelda obviously likewise. None of that touches what's
good here — a hinged spring-loaded dispenser is a *form*, and the mechanic is
yours. Design your own heads, pick your own name, don't go near the wordmark, and
nothing about the design above changes.

---

## The prototype that answers the question

One week, no art:

- A 9×9 grid, turn-based, one room at a time.
- Four colours: **red** (destroy a block), **blue** (freeze water into floor),
  **green** (pull yourself to a hook), **white** (open a door).
- A stack of eight, visible, poppable, eatable.
- Ten hand-drawn rooms, each solvable exactly one way.

If room seven makes you stop and count backwards from the door to work out what
order you needed at the loader — you've got it, and everything above is just
production. That's a very cheap question to answer for a game this different from
the two you've already shipped.
