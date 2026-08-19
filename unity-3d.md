# Going 3D

Can a full Unity project be built from a design document and the Synty POLYGON
Sci-Fi Space pack? Yes — with one real limitation, and a shopping list.

---

## Part 1 — The honest answer about what I can build

**What I can do without you in the loop:** all of the C#. Gameplay systems,
procedural conduit generation, the seeded daily, hull side-grades, resonance,
the grade/attempt formula, ECHO's dialogue system, save/load, the ad policy with
its 90-second cooldown and its sacred retry, IAP ownership, settings, the level
select for 246 levels. Shaders — ShaderLab and HLSL are plain text and I write
them reliably. ScriptableObject data. Editor tooling. The whole `.gitignore` and
LFS setup.

**What I cannot do:** open the editor and look at it. There is no Unity in this
container, no license, no play mode, no screenshot. I cannot tell you that the
bloom is too strong or that the ship reads badly at speed, because I cannot see it.

**Why that matters more than it sounds.** Unity scenes and prefabs are YAML files
full of GUID cross-references into `.meta` files. Hand-authoring them blind is
the single most fragile thing you can do in this position — one stale GUID and
the scene opens empty with no error worth reading. So the project should not
have hand-authored scenes at all.

**The shape that fixes it.** Build the project code-first:

- Scenes are *generated*, not stored. An editor menu item — `RINGSHIFT ▸ Rebuild
  Scene` — walks a manifest, finds prefabs by path, and assembles the hierarchy in
  code. The committed `.unity` file is close to empty; the scene's real source of
  truth is a C# file I can write and diff and review.
- Prefab wiring the same way, where it matters. Anything that must be authored by
  hand in the editor gets a small, documented, one-time setup step — and there
  should be very few of those.
- All tuning lives in ScriptableObjects or a plain-text config, never in scene
  YAML, so a balance change is a readable diff instead of a wall of GUIDs.

This is not a workaround. It is a better way to build a procedural game with a
deterministic daily, and it happens to be the way that survives me not having eyes.

**The loop that closes the gap.** Set up [GameCI](https://game.ci) on GitHub
Actions with your Unity license as a repo secret. Then every push gets compiled,
and I get real compiler errors and a build artifact back without you doing
anything. That single piece of plumbing is the difference between "Claude writes
plausible C#" and "Claude writes C# that is known to compile." Do this early —
it is an afternoon and it pays for itself in the first week.

For anything visual, the loop is you: press play, screenshot, tell me it looks
wrong. I am fast at the fixing and useless at the noticing.

---

## Part 2 — Which game to point it at

The pack is 660 assets of ships, characters, props, weapons, FX and environment.
The instinct is that a tunnel runner would waste nearly all of it. That instinct
is wrong, and the reason is specific.

**The modular spaceship kit is the thing you already needed.** Your own design
notes say RINGSHIFT's ship is assembled from separate pieces *specifically so it
can come apart on death instead of flashing*. Synty ships a modular kit built
from separate parts by construction. That is your death animation, free — throw
rigidbodies on the pieces and let it fall apart. It is also your hull line: HALO
and WRAITH stop being palette swaps and become genuinely different silhouettes,
which makes the side-grade design legible at a glance instead of only in a stats
panel.

**Warp gates are your rings.** The pack has them. That is the one piece of
authored geometry a tunnel runner actually wants, and buying it removes the only
art bottleneck in the design.

**The distance counter becomes visible.** ECHO counts down from 2.5 million light
years to a number measured in AU across 246 levels. The pack has planets,
asteroids, space stations, space clouds and space dust. A horizon every ten rings
that visibly *arrives* — a planet that is a smudge at level 40 and fills the frame
at level 240 — turns a number in the HUD into the whole point of the game. That
is the highest-value thing in the pack for this project and it is not the ships.

**The conduit itself stays procedural.** Do not buy tunnel geometry. The conduit
must be generated from a seed — that is what makes the daily identical for
everyone with no server, and it is load-bearing for the whole no-backend
constraint. Generated mesh, Synty materials on it, done.

So: **build RINGSHIFT properly in 3D.** You will use perhaps 40% of the pack, and
the 40% is the part that was blocking you.

**One warning.** If what you actually want is the anti-gravity racer hiding in the
audio engine, this pack does not get you there — there are no modular track pieces
in it, and track geometry is the entire content problem with that idea. Different
project, different purchase.

---

## Part 3 — The shopping list

### Buy — real gaps

**1. Audio. This is the biggest hole and the biggest spend.**
The Synty pack contains no sound at all. Your design describes "a Wipeout-shaped
techno engine," which means the audio is not garnish here, it is half the product.
Three separate things to source:

- *Music.* A techno/synthwave loop library with stems, so tracks can layer up
  under overdrive instead of just getting louder. Stems matter more than track
  count — six tracks that can breathe beat thirty that cannot.
- *SFX.* Sci-fi UI clicks, ring passes, the resonance pulse, the death crunch,
  engine layers. Search for sci-fi UI and spacecraft SFX libraries; buy one large
  general pack rather than three themed ones.
- *Middleware, maybe.* FMOD and Wwise both have free indie tiers and both do
  adaptive music properly. But for one game with one adaptive axis (speed), a
  layered-stem mixer in plain C# is maybe 300 lines and I can write it. Skip the
  middleware unless you already know it.

**2. A font.** TextMeshPro is free and built in, but the typeface is identity, and
"the interface is the readout" is your stated design line. Free and properly
licensed for embedding: **Orbitron**, **Chakra Petch**, **Rajdhani**, **JetBrains
Mono** — all Google Fonts, all OFL. Start there and only buy a display face if
none of them carry it.

**3. A deep-space skybox set.** Cheap, high impact, and the one thing that sells
"space" in the first half second. I can also generate one in a shader — but a good
purchased set will beat what I write, and it is a $15 problem.

### Maybe — buy only if the free path fails

**4. Post-processing.** URP's stock Volume stack gives you bloom, vignette,
chromatic aberration, motion blur and colour grading, which is most of a neon
tunnel runner's look. Try free first. If you hit the ceiling, **Beautify 3** is
the well-known upgrade and works on URP.

**5. DOTween Pro.** The free DOTween covers the animation; Pro adds an inspector
workflow and some path tooling. It is cheap and it removes a lot of UI animation
code. Worth it, not required.

### Do not buy

**6. A UI kit.** Your UI is the thing that reskins itself per hull and per theme —
Dreidel Royale already ships three chrome variants. A purchased kit fights that
on day one. Build it: UI Toolkit for layout, custom shaders for the look, and the
Synty HUD sprites as source material. This is identity work, not commodity work.

**7. VFX packs.** The pack already includes 48 effects — exhaust, flame boosters,
warp, explosion, debris, laser, electricity, space dust. Between those, Shuriken
systems built from code, and fullscreen shader work for speed lines, you are
covered. Note that VFX Graph `.vfx` files are another binary-ish format I cannot
author blind, so the code-and-shader path is also the path I can actually walk.

**8. Networking.** Ship the async ghost first, exactly as the design doc argues.
It needs no netcode at all — a recorded input trace against a shared seed is a
few kilobytes and replays deterministically. Live duels are a later, separate
decision, and the moment you reach for Unity Relay you should notice you have
bought a server.

**9. Odin Inspector.** Genuinely nice. Not necessary. Skip it until something hurts.

### Free, first-party, needed

URP · Input System · TextMeshPro · Cinemachine · Unity IAP · Unity LevelPlay (ads)
· Addressables (only if build size becomes a problem) · Burst + Jobs (only if
conduit generation shows up in the profiler, which it probably will not).

---

## Part 4 — The repo, and one licensing thing that matters

**You cannot put the Synty source files in a public repo.** Their EULA is explicit
that source assets must not be shared outside your team; contractors may access
them while working and must delete their copies afterwards. A public GitHub repo
with the raw `.fbx` and textures in it is redistribution, and it is the kind of
violation that is trivially discoverable by search.

**So: private repo.** That is the whole fix, and it costs nothing. Then:

- **Git LFS** for `*.fbx`, `*.png`, `*.tga`, `*.wav`, `*.mp3`, `*.psd`. Without it
  the repo is several gigabytes of history within a month.
- **A real Unity `.gitignore`** — `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`,
  `UserSettings/`, `*.csproj`, `*.sln`. These are generated and committing them
  causes merge pain forever.
- **Force text serialization** and **visible meta files** in Project Settings ▸
  Editor. Non-negotiable. Binary scene serialization makes every diff unreadable
  and every merge a coin flip.
- **Commit the Synty import once, as its own commit**, and do not reformat or
  reorganise it afterwards — you want pack updates to apply as clean diffs.

I will write the `.gitignore`, the `.gitattributes`, and the CI workflow. You
create the Unity project (two minutes in the Hub — 6000.x LTS, URP 3D template)
and commit it, because generating `ProjectSettings` YAML by hand is exactly the
fragile thing this whole plan is built to avoid.

---

## The short version

**Yes, and the pack is a better fit than it looks** — not for its ships, but for
its modular kit (your death animation), its warp gates (your rings), and its
planets (your distance counter made visible). The conduit stays procedural,
because the seeded daily depends on it.

**Buy audio, a font, and a skybox.** Everything else on the list is either already
in the pack, free and first-party, or identity work that should not be bought.

**Two things to set up before I write a line of gameplay code:** a private repo
with LFS and text serialization, and GameCI on Actions so my code gets compiled
by something other than optimism.

**The limitation is eyes, not hands.** I can build all of it and verify none of
it. Budget for a loop where you press play and tell me what is wrong — that loop
is short, but it is not optional.
