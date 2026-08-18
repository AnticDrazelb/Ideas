# The listing

`PLAY.md §5` lists five things the store needs and says none of them exist. Three
of them are words and those are here, written to the character limits Play
actually enforces and checked against them. Two are pictures and they are
specified rather than drawn, because the best feature graphic a puzzle game can
have is the game, and nobody has photographed this one yet.

Every claim below is one the build can keep. That is not a style note: a store
listing that promises something the APK does not do is the single fastest way to
a one-star review, and this game's most unusual selling points — no network, no
ads, no purchases, no telemetry — are exactly the ones a reviewer will test.

---

## Short description — 80 characters, hard limit

```
A dungeon that is a cube. Fold it — and everything that lines up, touches.
```

**74 characters.** It is the repository's own subtitle, and it is the listing's
whole job: name the object, name the verb, state the rule. A short description
that says "challenging levels await" says nothing a thousand other listings do
not.

Two that were written and rejected, because knowing why is worth more than the
line that survived:

- *"Fold the engine until its circuits align, then collapse into the core."*
  70 characters, and it is the pitch — but it is the pitch in the game's own
  private vocabulary. "Circuits" and "the core" mean something after ten minutes
  of play and nothing at all in a search result.
- *"300 hand-cut puzzle cubes. No ads, no purchases, no internet."*
  61, and every word true — but it leads with what the game is *not*. That
  belongs in the full description where somebody has already decided they are
  interested.

---

## Full description — 4000 characters, hard limit

```
You are a black hole inside a broken machine.

The dungeon is a solid cube of cells, and the camera looks down one axis. Every
cell sharing a screen column collapses to a single square — the nearest one.
Everything behind it is discarded, not hidden.

So two decks at opposite ends of the machine are neighbours on screen if their
columns are neighbours, and you may walk between them as if they touched.
Because on screen, they do.

Fold the cube and a different axis becomes depth. A wall becomes a floor. A pit
becomes a doorway. The board you were stuck on is a different board, made of the
same cells you were already looking at.

THREE VERBS AND NO FOURTH

Tap to move. The singularity walks to any trace connected to the one beneath it.
Swipe to fold. Past halfway it commits; short of it, it springs back.
Hold to see through the machine, and drag to turn it over in your hand.

The quiet rule is the one the game lives on: you may only fold if your own column
still has something to stand on. Where you stand decides which folds you have,
and which fold you take decides where you can stand. The lock and the key, both
falling out of the geometry rather than placed on top of it.

THREE HUNDRED CUBES, CUT ONE AT A TIME

Twelve vaults. Every cube was chosen out of a pool of candidates, pruned, and
then proved solvable by the same solver that scores your line — so every par in
this game is a real minimum, and a cube nobody can finish cannot ship. Nothing
here is procedurally poured in front of you as you walk.

Plates invert the board for five seconds. Everters turn the machine inside out,
so every column shows its far side instead of its near one. Triggers exchange
the solid for another one. Each arrives on a cube built to teach it, and none of
them arrives in a paragraph.

A DAILY CUBE, THE SAME ONE FOR EVERYONE

Computed from the date rather than fetched from anywhere, so it needs no
account, no server and no connection — and it is still the same puzzle on every
phone on earth. Streaks are kept locally.

A FORGE

Build your own cubes. It verifies them before it lets you save one, and shares
them as a code short enough to send in a message.

BUILT FOR BEING READ

Every printed colour pair is measured against WCAG, and a legibility mode moves
the five that need to move. The board's brightness band survives all three kinds
of colour blindness — simulated, not assumed. Camera motion and full-screen
light are separate settings and both reach zero, because they answer to two
different people. Every control clears the 44-point touch minimum on its
shortest side, and every verb has a form that needs no swipe and no hold.

WHAT IT DOES NOT DO

No advertising. No purchases of any kind. No accounts, no sign-in, no
leaderboard. No analytics, no crash reporting, no telemetry.

It does not request the internet permission, so it cannot contact anything even
if it wanted to. The daily is computed from the date. The cubes are computed
from their numbers. Your save is a single local file.

You are a black hole inside a broken machine. Fold the engine until its circuits
align, then collapse into the core.
```

Run `python3 tools/type/listing.py` to re-check both against their limits after
any edit; it is four lines and it exists because a description truncated at 4000
characters loses its last paragraph silently on the store rather than loudly
here.

---

## The two pictures

**Feature graphic, 1024×500, required.** Not a logo on a gradient. The one image
that explains this game is a *fold in progress* — the solid caught at forty-five
degrees, one face still readable as a board and the other coming round — with
the wordmark small in a corner. That frame exists in the build: hold a swipe
halfway and the cube sits at the detent. Shoot it at the highest resolution the
editor will give and crop to 1024×500; the composition is already correct
because the camera contains the solid inside a frame at every angle.

**Phone screenshots, at least two, and the game is worth more than two.** In
order of what they have to prove:

1. **A board mid-game**, at seven or eight cells, with the readout visible. This
   is the only screenshot that says what the game looks like to play.
2. **A fold at the detent** — the same frame as the feature graphic, portrait.
   It is the one image nobody can mistake for another puzzle game.
3. **The matrix**, held: the lattice gone to glass with the cage around it. It
   answers "what is behind the board" without a word.
4. **The vault rack**, showing named cubes and their pips, which is the picture
   of how much game there is.
5. **Calibrate or Access**, because a listing that claims a legibility mode and
   never shows it is asking to be doubted.

Shoot them on a device rather than in the editor. The chassis, the scanlines and
the glass are all built in *device* pixels, and the editor's game view at a
non-integer scale will alias the row pattern into a moiré that is not in the
build.

---

## Before any of this goes up

`PLAY.md §2` has the one decision that cannot be undone: the application id is
`com.singularity.engine` and it is permanent for the life of the listing. If
that is not a domain under your control, change it in
`Assets/Scripts/Editor/ProjectSetup.cs` before the first upload and not after.
