# the chassis

The two imported assets in the game — the case and the pane across the front of
it. This is where they come from and how to make them again.

```sh
pip install pillow numpy
python3 cut.py            # mockup.png       -> chassis-art.png   (the case)
python3 glass.py          # glass-source.jpeg -> glass-art.jpg    (the dirt)
```

## what it does

`mockup.png` is the reference: a render of the machine with the game running on
its screen, on a black background. Three things have to happen to it before it
can be a housing rather than a picture of one.

**The background comes off — as a MATTE, not as alpha.** A flood fill from the
border over everything below nine in luminance, which finds the black around the
case and cannot leak inside it: the case's own darkest pixels are twice that.
What is left is the silhouette, and it is not a rectangle — there is a notch in
the middle of the top edge, a waist where both sides step in, a foot at the
bottom, and eight bolts. Keeping that shape is most of why the housing reads as
an object.

The silhouette multiplies the colour rather than cutting the alpha, and the
surround ships opaque black. That looks like a mistake and is the fix for one.
The board is drawn by the CAMERA, across the whole display, and the case is the
only thing in front of it — so every pixel the silhouette cuts away is a hole
straight through the machine. At rest nothing shows through: the cube is fitted
well inside the aperture. Mid-fold and under a held matrix it is half again as
wide as its own face, and the cage came out through the notch in the top edge
and ran off the case entirely. The camera clears to `Palette.Void`, which is
`#000000`, so painting the surround black is pixel-for-pixel what was already
there and closes the hole. **The glass is the only transparent part of this
asset.**

**The glass comes out.** A rounded rectangle, `GLASS` in the script, measured to
the darkest point of the recess on each of the four sides — so the machined wall
the glass sits behind stays with the metal, and the cut lands where the picture
was already black. The game draws its own screen through the hole.

**The colour is bled outward** four pixels past the cut, into pixels that end up
fully transparent. Nothing samples them directly and it looks like a no-op; it
is not. A bilinear tap on the edge of the case mixes a texel that is inside with
one that is outside, and if the outside one is black the whole silhouette gets a
dark fringe that nobody put there.

## what it prints

```
art 461 x 1018
insets from the case: left 38.0 right 38.0 top 60.0 bottom 62.0
```

Those five numbers are the size of the art and how far it is from each edge of
the case to the glass. They are still not measured at runtime — a housing that
measures itself at startup is a housing whose layout can move when somebody
re-exports a texture — but they are no longer copied by hand into `Chassis.cs`
either. They live in `Assets/Resources/chassis.asset`, a `ChassisSpec`, written
by the editor tool below at the same moment the PNG is.

The bezel is deeper at the top and bottom than at the sides, and deeper at the
bottom than at the top. That is the real object being slightly asymmetric, and
it is carried through rather than averaged away — see the four `Inset` constants
in `Chassis.cs` and the four in `Layout`.

## the same cut, in the editor

`cut.py` is still here and still the reference, but it is no longer how you
change the case. **Singularity → Chassis** does the whole job inside Unity:
pick a picture, place the opening against a live overlay, press Cut, and it
writes `Assets/Resources/chassis.png` and the measurements together.

The C# is a straight port of this file — `Assets/Scripts/Editor/ChassisCut.cs`,
no `UnityEngine` types in it at all — and the harness proves the two agree:

```sh
python3 dump.py /tmp/chassis          # raw RGBA, because UnityStubs decodes no PNGs
CHASSIS_RAW=/tmp/chassis dotnet run --project ../UnityStubs
# chassis: the port reproduces the shipped asset exactly, all 1877192 bytes
```

Byte for byte, all 1.87 MB, including the truncation `numpy`'s `astype(uint8)`
does and this one had to be told to do. That check also asserts something the
Python only ever had as a hand-typed literal: **the crop is the silhouette's own
bounding box**, and the tool finds it — same four numbers as `CROP`, to the
pixel, which is why it is not a field anybody types any more.

What the tool cannot find is the opening. The obvious rule — the glass is the
dark rectangle in the middle — is false for exactly the pictures anybody would
use, because a mock-up of a machine has the machine's screen turned *on*. Half
this reference's screen is brighter than the metal around it, and every
automatic detector tried against it found a chamfer groove or the foot instead.
So the four edges are yours, seeded from the current case's proportions, with an
overlay to place them against.

Under that is the preview worth having: **the twelve pieces, assembled the way
the game will assemble them**, at a panel size you choose. A corner cut that
lands on the notch, or a band too shallow to hold the metal in its stretch, is
visible there and nowhere else short of a build.

The mock-up does not need to be in the project — there is no reason to import
two thirds of a megabyte of source art into a game that will never ship it. The
spec takes a path as well as an asset reference; `../../tools/chassis/mockup.png`
from the project folder is this one.

## `glass.py` — the dirt

`glass-source.jpeg` is a photograph of a filthy screen: fingerprints, dust,
hairline scratches, a sleeve-wipe smear, and the ghost of the pixel lattice
under it. `Glass.cs` composites it over the whole interface, ADDITIVELY, and two
things are done to it here so that it can be.

**The lit rim comes off.** Twenty-two pixels from the left, top and right — the
photographed phone's own chamfer catching the light, which is not dirt and would
draw a bright band just inside the bezel. There is none at the bottom; the photo
is already cut there.

**The pedestal comes off.** This is the one that matters. The picture's median
is about 20 of 255, so composited additively it would lift the entire display
off black before a single speck of dust was visible — and every contrast ratio
in the access audit is measured against grounds that are meant to *be* black.
Subtracting a flat floor and clamping at zero leaves the dirt and throws away
the glass, which is the only part of the picture anybody wanted.

What it prints is the honest accounting of what the layer can do, before
`Glass.Strength` scales it down further:

```
adds, as a fraction of full white, before the layer opacity:
   p50    0.000      half the glass is clean and contributes nothing
   p90    0.059
   p99    0.172
   max    0.867      one grain of dust, a few pixels across
```

Additive is not a stylistic choice. Alpha-blending a near-black photograph over
this interface would darken it everywhere the dirt is dark, which is most of it,
and would take the audit's four dark grounds down with it. Light that only adds
cannot make anything less legible than it was.
