"""Cut the case out of the mock-up: drop the background, drop the glass."""
from PIL import Image
import numpy as np
from collections import deque

SRC = 'Untitled.png'
DROP_ROWS = 1024          # the mock-up ends in five rows of pure white
CROP = (50, 6, 511, 1024) # the case's own bounding box, x0 y0 x1 y1 (exclusive)
GLASS = (88.0, 66.0, 472.0, 961.0)   # the recess trough, all four sides
GLASS_R = 15.0

im = np.asarray(Image.open(SRC).convert('RGB')).astype(np.float64)[:DROP_ROWS]
L = im @ [0.299, 0.587, 0.114]
h, w = L.shape

# ---- the silhouette: flood the background in from the border ----------------
dark = L < 9
out = np.zeros_like(dark)
q = deque()
for x in range(w):
    for y in (0, h - 1):
        if dark[y, x] and not out[y, x]: out[y, x] = True; q.append((y, x))
for y in range(h):
    for x in (0, w - 1):
        if dark[y, x] and not out[y, x]: out[y, x] = True; q.append((y, x))
while q:
    y, x = q.popleft()
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        ny, nx = y + dy, x + dx
        if 0 <= ny < h and 0 <= nx < w and dark[ny, nx] and not out[ny, nx]:
            out[ny, nx] = True; q.append((ny, nx))
case = (~out).astype(np.float64)

# one texel of feather, so the cut edge is not a staircase
k = np.array([0.25, 0.5, 0.25])
case = np.apply_along_axis(lambda m: np.convolve(m, k, mode='same'), 0, case)
case = np.apply_along_axis(lambda m: np.convolve(m, k, mode='same'), 1, case)

# ---- the glass: a rounded rectangle, taken out --------------------------------
x0, y0, x1, y1 = GLASS
cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
hx, hy = (x1 - x0) / 2, (y1 - y0) / 2
X, Y = np.meshgrid(np.arange(w) + 0.5, np.arange(h) + 0.5)
qx = np.abs(X - cx) - (hx - GLASS_R)
qy = np.abs(Y - cy) - (hy - GLASS_R)
d = (np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2)
     + np.minimum(np.maximum(qx, qy), 0) - GLASS_R)
glass = np.clip(0.5 - d, 0, 1)

# ---- the case is a MATTE, and the surround is opaque -------------------------
#
# THE ONLY TRANSPARENT PART OF THIS ASSET IS THE GLASS, and that is not a
# shortcut — it is the fix for a bug that looked like a rendering fault.
#
# The board is drawn by the CAMERA, across the whole display, and the case is the
# only thing in front of it. So every pixel the case's silhouette cuts away — the
# notch in the middle of the top edge, the four corner chamfers, the waist, the
# foot — is a hole straight through the machine. At rest nothing shows through
# it, because the cube is fitted well inside the aperture. Mid-fold and under a
# held matrix the cube is half again as wide as its own face, and the cage came
# out through the notch and ran off the top of the case into the editor's
# background.
#
# The camera clears to Palette.Void, which is #000000, so painting the surround
# black is pixel-for-pixel what was already there and closes the hole. The
# silhouette survives as a MATTE on the colour instead of as alpha: the metal
# fades to black exactly where it used to fade to nothing, feathered edge and
# all, and the case still reads as a shape rather than a rectangle.
rgb = im * case[..., None]
alpha = np.clip(1 - glass, 0, 1)

# ---- bleed the colour outward, so filtering never fetches a black neighbour --
solid = alpha > 0.02
for _ in range(4):
    known = solid.astype(np.float64)
    acc = np.zeros_like(rgb); cnt = np.zeros_like(known)
    for dy, dx in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)):
        acc += np.roll(np.roll(rgb * known[..., None], dy, 0), dx, 1)
        cnt += np.roll(np.roll(known, dy, 0), dx, 1)
    fill = (~solid) & (cnt > 0)
    rgb[fill] = (acc[fill] / cnt[fill][..., None])
    solid = solid | fill

x0c, y0c, x1c, y1c = CROP
outp = np.dstack([rgb, alpha * 255.0])[y0c:y1c, x0c:x1c]
Image.fromarray(np.clip(outp, 0, 255).astype(np.uint8), 'RGBA').save('chassis-art.png')
print('art', x1c - x0c, 'x', y1c - y0c)
print('insets from the case: left', GLASS[0] - x0c, 'right', x1c - 1 - GLASS[2],
      'top', GLASS[1] - y0c, 'bottom', y1c - 1 - GLASS[3])
