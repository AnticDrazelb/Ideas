"""Turn the photograph of a filthy screen into a layer that only ADDS light."""
from PIL import Image
import numpy as np

SRC = 'glass-source.jpeg'
RIM = 22            # the phone's own lit chamfer, which is not dirt
FLOOR = 22          # the clean glass's own level, taken off so it contributes nothing

im = Image.open(SRC).convert('RGB')
w, h = im.size
im = im.crop((RIM, RIM, w - RIM, h))          # no rim at the bottom; the photo is cut there
a = np.asarray(im).astype(np.float64)

# THE PEDESTAL COMES OFF. Composited additively, a picture whose median is 20
# would lift the whole display off black before a single speck of dust showed —
# every contrast ratio in the access audit is measured against grounds that are
# meant to BE black. What is wanted is the dirt, which is what is left after the
# glass itself is subtracted.
a = np.clip(a - FLOOR, 0, 255)

out = Image.fromarray(a.astype(np.uint8), 'RGB')
out.save('glass-art.jpg', quality=94, optimize=True)

L = a @ [0.299, 0.587, 0.114]
print('art %dx%d' % out.size)
print('adds, as a fraction of full white, before the layer opacity:')
for p in (50, 90, 99, 99.9):
    print('   p%-5s %.3f' % (p, np.percentile(L, p) / 255))
print('   max   %.3f  over 0.10: %.3f%% of the glass' % (L.max() / 255, (L > 25.5).mean() * 100))
