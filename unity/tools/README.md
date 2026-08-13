# tools

Two harnesses that let this project be checked without a Unity install.

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

It is deliberately **not** inside `Assets/`, so Unity never compiles it and
there is no chance of it shadowing the real engine.

### It also runs

`Mathf` is implemented for real rather than stubbed. Everywhere else a stub
returning zero is harmless — the call is a no-op whose result nobody reads.
There it would be a landmine: a `Mathf` that quietly answers 0 turns a runnable
check into one that passes for the wrong reason.

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
