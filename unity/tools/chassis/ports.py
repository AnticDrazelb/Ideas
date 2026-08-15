#!/usr/bin/env python3
"""
THE CAMERA PORTS — one per cutout form that Android phones actually have.

    python3 ports.py        # chassis.png -> port-hole.png, port-pill.png,
                            #                port-tear.png, port-notch.png

WHY THERE ARE SEVERAL AND NOT ONE

A single collar drawn round whatever rectangle turns up is the lowest common
denominator of every form at once, and it looks like it. A bathtub notch wants a
machined slot that runs off the edge of the panel. A punch-hole wants a tight
port with a lip all the way round. A waterdrop wants a moulded dip that flares
into the bezel. Giving all three the same ring gives none of them the right one.

So each form is drawn separately, and the runtime is told which one it is
holding — see Chassis.Classify, which reads the form off Screen.cutouts rather
than off a list of phone names, so a handset nobody has seen still lands on the
right entry. Adding a form later is one call here and one row there.

WHY THE METAL IS SAMPLED AND NOT PAINTED

The same reason the case itself is a photograph: four generated passes at this
material failed, and rust does not come out of value noise. Every port is cut
out of REAL BEZEL — a clean patch of the top band of chassis.png, tiled — with
only the recess shading and the lip highlight computed. So a port belongs to the
panel it lands on because it is made of it.

THE CONTRACT WITH THE RUNTIME

Each sprite is EXACTLY TWICE THE HOLE in both axes, with the hole centred. That
is the whole interface: the runtime measures the cutout, doubles it, centres the
sprite on it, and the port lines up on any phone whatever the diameter. Nothing
here needs to know a device, and nothing there needs to know a form's
proportions.
"""

import numpy as np
from PIL import Image
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "..", "SingularityEngine", "Assets", "Resources", "chassis.png")
OUT = os.path.join(HERE, "..", "..", "SingularityEngine", "Assets", "Resources")

# A patch of the top band that is metal all the way across — measured: the band's
# first fully lit row is 21 and the recess starts around 55, so this sits inside
# both with room to spare.
PATCH = (170, 26, 292, 52)          # left, top, right, bottom

# The hole is half the sprite, so the collar is a quarter of the sprite on each
# side. Everything below is a fraction of that collar.
RES = 256                            # sprite is RES x RES for a square form


def metal(w, h):
    """A patch of real bezel, tiled to w x h and lightly mirrored so it does not band."""
    im = Image.open(SRC).convert("RGB").crop(PATCH)
    a = np.asarray(im).astype(np.float32)
    ph, pw = a.shape[:2]
    ty, tx = int(np.ceil(h / ph)) + 1, int(np.ceil(w / pw)) + 1
    row = np.concatenate([a if i % 2 == 0 else a[:, ::-1] for i in range(tx)], axis=1)
    tile = np.concatenate([row if j % 2 == 0 else row[::-1] for j in range(ty)], axis=0)
    return tile[:h, :w]


def sdf_round_rect(w, h, half_w, half_h, radius, cx=None, cy=None):
    """Signed distance to a rounded rectangle. Negative inside, in pixels."""
    cx = w / 2 if cx is None else cx
    cy = h / 2 if cy is None else cy
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    qx = np.abs(x - cx) - (half_w - radius)
    qy = np.abs(y - cy) - (half_h - radius)
    ax, ay = np.maximum(qx, 0), np.maximum(qy, 0)
    outside = np.sqrt(ax * ax + ay * ay)
    inside = np.minimum(np.maximum(qx, qy), 0)
    return outside + inside - radius


def port(name, hole_w, hole_h, radius, open_top=False, collar_scale=1.0):
    """
    One port. The sprite is twice the hole in each axis, hole centred.

    IT CARRIES SHADING AND NOT METAL, and the first attempt at this carried
    metal. Sampling a patch of bezel and pasting it round the camera cannot work:
    the band is rusted through in places and clean in others, so a patch cut from
    one part of it never matches the part it lands on. Every port read as a
    sticker, with a tile seam through it and a hard rectangular edge where the
    sampled metal stopped.

    So it is a TINT with a strength — black where the steel is machined away and
    white along the shoulder that catches the light — over ordinary alpha
    blending. Dark lowers whatever is underneath and light raises it, so a port
    takes the colour of the bezel it lands on, rusted or clean, and there is
    nothing to match because nothing is being pasted.
    """
    w, h = hole_w * 2, hole_h * 2

    # Bounded so the outer fade always completes INSIDE the sprite. The margin
    # each side is hole/2; the fade ends at 1.9 collars; 1.9 * 0.26 = 0.494 of
    # the hole, which fits with a little to spare. Getting this wrong is what
    # drew a visible box round the first version.
    collar = min(hole_w, hole_h) * 0.26 * collar_scale

    d = sdf_round_rect(w, h, hole_w / 2, hole_h / 2, radius)
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)

    # THE RECESS. Steel machined away leaves a wall darkest right at the opening,
    # recovering outward — exponential, because that is how a shadow in a groove
    # actually falls off.
    dark = 0.78 * np.exp(-np.maximum(d, 0) / (collar * 0.42))

    # THE LIP. The shoulder catches the light, and the light in this picture
    # comes from the top left — the same direction every highlight on the case
    # runs, which is what makes the port read as part of it rather than on it.
    dx, dy = x - w / 2, y - h / 2
    n = np.sqrt(dx * dx + dy * dy) + 1e-6
    facing = np.clip((-dx / n) * 0.72 + (-dy / n) * 0.72, 0, 1)
    light = 0.55 * facing * np.exp(-(((d - collar * 0.75) / (collar * 0.40)) ** 2))

    a = np.clip(dark + light, 0, 1)
    tint = 255.0 * light / (dark + light + 1e-6)

    a *= np.clip(d / 1.5, 0, 1)                                  # nothing in the hole
    a *= np.clip(1.0 - (d - collar) / (collar * 0.9), 0, 1)      # and out to nothing

    if open_top:
        # A notch or a waterdrop is part of the display's edge: the collar must
        # not close over the top of it, or the panel reads as having a ring
        # floating in it rather than a slot cut into its edge.
        a *= np.clip((y - h * 0.10) / (h * 0.14), 0, 1)

    g = np.clip(tint, 0, 255)
    out = np.dstack([g, g, g, np.clip(a, 0, 1) * 255]).astype(np.uint8)
    p = os.path.join(OUT, "port-%s.png" % name)
    Image.fromarray(out, "RGBA").save(p)
    print("  port-%-6s %4dx%-4d  hole %dx%d  collar %.0fpx" % (name, w, h, hole_w, hole_h, collar))


if __name__ == "__main__":
    print("ports, cut from the bezel of chassis.png:")
    # a round island — the common case, centred or in a corner
    port("hole", RES // 2, RES // 2, RES // 4)
    # two lenses under one racetrack opening
    port("pill", RES, RES // 2, RES // 4)
    # a waterdrop, joined to the edge
    port("tear", int(RES * 0.62), int(RES * 0.52), RES // 5, open_top=True, collar_scale=1.15)
    # the wide notch, a slot in the edge of the panel
    port("notch", int(RES * 1.6), int(RES * 0.46), RES // 6, open_top=True, collar_scale=1.35)
    print("wrote to Assets/Resources")
