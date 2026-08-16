#!/usr/bin/env python3
"""
Raw RGBA out of the two PNGs, so the C# port of the cut can be checked without
teaching the harness to decode a PNG.

UnityStubs takes no dependencies on purpose — a fresh clone with no network
still runs it — and a PNG decoder would be one. So the pictures come in as
width, height, then w*h*4 bytes, which is eight lines of C#.

    python3 dump.py /tmp/chassis
    CHASSIS_RAW=/tmp/chassis dotnet run --project ../UnityStubs
"""
import os
import struct
import sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PAIRS = [
    ("mockup.rgba", os.path.join(HERE, "mockup.png")),
    ("chassis.rgba", os.path.join(HERE, "..", "..", "SingularityEngine",
                                  "Assets", "Resources", "chassis.png")),
]


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "/tmp/chassis"
    os.makedirs(out, exist_ok=True)
    for name, path in PAIRS:
        im = Image.open(path).convert("RGBA")
        w, h = im.size
        with open(os.path.join(out, name), "wb") as f:
            f.write(struct.pack("<ii", w, h))
            f.write(im.tobytes())
        print("%-14s %4d x %4d  -> %s" % (name, w, h, os.path.join(out, name)))


if __name__ == "__main__":
    main()
