# tools

Harnesses that let this project be checked without a Unity install.

## `UnityStubs`

A compile-time stand-in for the engine. It declares the Unity API surface the
game actually touches — with the real signatures and no behaviour at all — so
that everything under `Assets/Scripts` can be type-checked by a plain C#
compiler:

```sh
dotnet build unity/tools/UnityStubs
```

This catches the entire class of mistake that otherwise only shows up when
somebody opens the editor: a misspelled member, a wrong overload, a signature
that drifted. It is not a substitute for opening the project — it cannot tell
you whether a material looks right or a layout fits a phone — but a build that
fails here is broken for certain, and one that passes is at least well-formed.

It is also an honest inventory of what this game asks of Unity. If a new call
needs a declaration adding, that is the point: the list should stay short.

### What it cannot do, learned the hard way

**A stub proves the SHAPE of a call, never the EXISTENCE of the thing being
called.** The first time this project was opened in a real editor, it failed to
compile on two lines naming `IconKind.AdaptiveForeground` and
`IconKind.AdaptiveBackground`. Those members do not exist — adaptive icons go
through `SetPlatformIcons`, not `SetIcons` — and the harness had agreed with the
code because the same person wrote both sides of it.

So the failure mode is specific and worth naming: the harness catches typos and
signature drift, and it is blind to anything invented wholesale. The mitigation
is not more stub, it is discipline about which APIs get used at all — prefer the
call you are sure of over the one that would be nicer, when nobody can run it.

Two things changed as a result. `IconKind` now has no adaptive members, so that
exact mistake fails here first. And `AndroidSdkVersions` carries Unity's own
`[Obsolete]` attributes, so a deprecation that Unity would warn about warns here
instead of arriving on somebody else's machine.

It is deliberately **not** inside `Assets/`, so Unity never compiles it and
there is no chance of it shadowing the real engine.

### It also runs

`Mathf` is implemented for real rather than stubbed. Everywhere else a stub
returning zero is harmless — the call is a no-op whose result nobody reads.
There it would be a landmine: a `Mathf` that quietly answers 0 turns a runnable
check into one that passes for the wrong reason.

`Color` and `ColorUtility` are real for exactly the same reason, and it is worth
recording that they were *not* until the access audit was written. `Color`'s
arithmetic all returned its left operand and `TryParseHtmlString` answered black,
which is harmless while nothing reads the result and a landmine the moment
something does — and the audit reads every one of them. A stubbed `Lerp` turns
"is a trace brighter than the lattice at every depth" into a comparison of two
identical values; a parser that answers black turns twenty contrast assertions
into twenty comparisons of black against black. Both pass. Neither means
anything, and **a green check that means nothing is worse than no check.** The
first thing `AccessChecks` does is prove the parser and the ratio on values whose
answers are known, so that failure mode cannot come back quietly.

With it real, the harness can **execute** the pure-logic half of the game layer,
not merely compile it:

```sh
dotnet run --project unity/tools/UnityStubs
```

runs the Forge's model — name folding, resizing, mark lifetimes, node/lock
pairing, the verify pass and import — against the real `Singularity.Core`. The
stubbed `PlayerPrefs` returns nothing, so `Store` falls back to a fresh
in-memory save, which is exactly the "device with storage disabled" path the
original is written to survive. It is not a special case invented for the test.

The same assertions exist in `Assets/Tests/EditMode/ForgeTests.cs`. They are
duplicated rather than replaced: the harness proves the logic, and the Test
Runner proves it still works with Unity's serialisation and lifecycle underneath.

## `TestCheck`

The same trick pointed at `Assets/Tests`, which `UnityStubs` deliberately does
not compile:

```sh
dotnet build unity/tools/TestCheck
```

It exists because the gap was real rather than theoretical. `AccessTests` was
written with two calls to `Assert.AreNotEqual` passing a tolerance as the third
argument — NUnit has no such overload, `AreEqual` has one and `AreNotEqual` does
not, so the float bound to the `message` parameter and the file did not compile.
Nothing in this directory could have caught it, because nothing in this
directory had ever seen NUnit. It went to a real editor to be found, which is
exactly the round trip these harnesses exist to prevent.

So this project references the NUnit package Unity's Test Framework carries, and
an overload that resolves here resolves in the editor. It is **separate** from
`UnityStubs` on purpose: it takes a dependency, and `UnityStubs`' whole virtue is
that it takes none. A fresh clone with no network still gets the harness; this is
the extra mile when there is a network to walk it on.

It found a second thing on its first run, and that one is the older lesson
again: `ColorUtility.ToHtmlStringRGB` was missing from the stub, because until
something called it nobody had had to declare it.

## Looking at a texture

```sh
dotnet run --project unity/tools/UnityStubs -- preview .
```

writes `chassis.png` — the panel's 128px tile, repeated three by three so a seam
has somewhere to show.

Everything else here can be judged by reading it. A generated texture cannot:
"worn gunmetal" is a claim about how a few noise weights LOOK, and no amount of
care in choosing them substitutes for seeing the result. The chassis went through
three passes that a guess would have shipped — one too dark to read as metal, one
whose scratches ran across the grain and looked like a CRT, and one with a tonal
step at every tile boundary because its noise layers only wrapped on one axis.

It calls the game's own `Chassis.Texel`, not a copy of it, which is why the pixel
maths is a separate method from the `Texture2D` that fills with it. A preview
that reimplements what it previews is a second source of truth, and this file has
a long note further down about what those cost.

## Parity against the original

`Singularity.Core` has no engine references at all — that is what its
`noEngineReferences` asmdef setting is for — so it can be compiled and run as
an ordinary console program and diffed against the JavaScript it was ported
from.

This is how the port was verified. Every cube in this game is a pure function of
its number, which is the entire reason the daily works with no server and a cube
number can be typed into a text field and produce the same puzzle on someone
else's device. A port that merely plays the same is a different game that looks
like it. So the standard is: run both engines and diff the bytes.

What was checked, and what it protects:

| checked | why it can break |
|---|---|
| mulberry32's first draws | `\|0`, `>>>` and `Math.imul` are 32-bit wrapping; C# `int` maths is not the same by accident |
| `hashSeed` | the multiply happens in double precision *before* the truncation to int32 |
| the difficulty curve at 14 levels | `Math.floor` on doubles, and integer division that is not the same as `\|0` on a negative |
| the ten authored cubes | the solver's state ordering, which decides which of two equal-cost lines is found first |
| cubes 11–120, byte for byte | Fisher-Yates consuming exactly one draw per element, and the carve's world bookkeeping |
| cubes at n=9, out to 99,999 | the size ladder, and 32-bit overflow in the seed at large level numbers |
| canonical ids | ordinal string comparison — a culture-aware sort silently reorders the 24 candidate forms |
| share codes, and a corrupt one refused | the checksum, and the base64url alphabet's hyphen |

The handedness conversion is proved rather than eyeballed. The rules use a
right-handed view basis (R right, U up, F **toward** the camera) and Unity is
left-handed, so the naive conversion is a reflection with determinant −1 that no
quaternion can express. Flipping the cube's own Z as well makes it a similarity,
`M = J A J`, determinant +1. All 24 orientations were checked exhaustively
against every cell of the four cube sizes.
