#!/usr/bin/env python3
"""THE LAUNCHER ICON, GENERATED.

project.godot named res://assets/icon.png and the file was never there. Godot
falls back to its own robot, which is a small thing on a desktop and the whole
first impression on a phone home screen.

It is written here rather than drawn because that is this project's rule for
everything except the case: an asset nobody can regenerate is a blob nobody can
review. Sixty-four pixels of the game's own palette — the void, the rust of the
housing, the arc of the singularity — and the one shape the game is about: a
square lattice with a hole in it.

    python3 tools/icon.py
"""
import struct, zlib, os

N = 128
VOID = (5, 7, 9)
EDGE = (154, 74, 42)   # the housing, one step down from the rust
RUST = (234, 88, 12)   # Palette.RUST_SHIPPED
ARC = (34, 211, 238)
CELL = (58, 74, 92)


def rounded(x, y, x0, y0, x1, y1, r):
    """Inside a rounded rectangle."""
    cx = min(max(x, x0 + r), x1 - r)
    cy = min(max(y, y0 + r), y1 - r)
    return (x - cx) ** 2 + (y - cy) ** 2 <= r * r


def ring(x, y, x0, y0, x1, y1, r, w):
    return rounded(x, y, x0, y0, x1, y1, r) and not rounded(
        x, y, x0 + w, y0 + w, x1 - w, y1 - w, max(1, r - w))


def pixel(x, y):
    # the housing: a rounded frame around everything
    if ring(x, y, 3, 3, N - 4, N - 4, 22, 7):
        return EDGE
    if not rounded(x, y, 3, 3, N - 4, N - 4, 22):
        return None

    # three by three of the lattice, inside it
    pad, gap = 22, 6
    span = N - pad * 2
    cell = (span - gap * 2) / 3.0
    for cy in range(3):
        for cx in range(3):
            ax = pad + cx * (cell + gap)
            ay = pad + cy * (cell + gap)
            if not rounded(x, y, ax, ay, ax + cell, ay + cell, 5):
                continue
            # the core sits in the middle cell, and it is a hole
            if cx == 1 and cy == 1:
                mx, my = ax + cell / 2.0, ay + cell / 2.0
                d = ((x - mx) ** 2 + (y - my) ** 2) ** 0.5
                if d <= cell * 0.30:
                    return VOID
                if d <= cell * 0.38:
                    return ARC
                return CELL
            # two lit cells, so the thing reads as a circuit rather than a grid
            if (cx, cy) in ((2, 0), (0, 2)):
                return RUST
            return CELL
    return VOID


def png(path):
    rows = bytearray()
    for y in range(N):
        rows.append(0)
        for x in range(N):
            c = pixel(x + 0.5, y + 0.5)
            rows += bytes(c + (255,)) if c else bytes((0, 0, 0, 0))

    def chunk(tag, data):
        out = struct.pack(">I", len(data)) + tag + data
        return out + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    body = (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", N, N, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(rows), 9))
            + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(body)


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    out = os.path.join(here, "..", "assets", "icon.png")
    png(out)
    print("wrote", os.path.normpath(out), N, "x", N)
