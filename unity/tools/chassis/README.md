# the chassis

`Assets/Resources/chassis.png` is the only imported asset in the game. This is
where it comes from and how to make it again.

```sh
pip install pillow numpy
python3 cut.py            # mockup.png -> chassis-art.png
```

## what it does

`mockup.png` is the reference: a render of the machine with the game running on
its screen, on a black background. Three things have to happen to it before it
can be a housing rather than a picture of one.

**The background comes off.** A flood fill from the border over everything below
nine in luminance, which finds the black around the case and cannot leak inside
it — the case's own darkest pixels are twice that. What is left is the
silhouette, and it is not a rectangle: there is a notch in the middle of the top
edge, a waist where both sides step in, a foot at the bottom, and eight bolts.
Keeping that shape is most of why the housing reads as an object.

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

Those five numbers are the ones `Chassis.cs` is written against — the size of the
art, and how far it is from each edge of the case to the glass. **If the source
picture is ever replaced, the numbers change, and they have to be copied across.**
They are not read at runtime: a housing that measures itself at startup is a
housing whose layout can move when somebody re-exports a texture, and the whole
rest of the interface is anchored to those four insets.

The bezel is deeper at the top and bottom than at the sides, and deeper at the
bottom than at the top. That is the real object being slightly asymmetric, and
it is carried through rather than averaged away — see the four `Inset` constants
in `Chassis.cs` and the four in `Layout`.
