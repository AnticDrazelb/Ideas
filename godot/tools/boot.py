#!/usr/bin/env python3
"""BOOT THE GAME THE WAY GODOT BOOTS IT, AND FAIL ON ANY ENGINE ERROR.

Every other harness in this project instances Main.tscn and adds it to the tree
ITSELF, from a SceneTree script. That is close enough to the real thing for
everything they were written to measure, and it is not the real thing: when
Godot opens a project it adds the main scene to the root, and a node that is
having children added is BUSY, so an add_child on the root from inside _ready
is refused.

Which is exactly what happened. All five canvases — housing, readout,
scanlines, glass, screens — were refused, the interface was built and laid out
and never drawn, and every test passed, because in the tests the root was not
busy. The tests were running the game. They were not running the game the way a
player runs it.

So this runs the main scene, with no arguments, for a few seconds, and treats
any engine error as a failure. Two families are allowed and both are the
headless build itself rather than the game: the dummy rasterizer has no meshes
or render targets, and a container has no sound card.

    GODOT=/path/to/godot tools/boot.py
"""
import os, re, subprocess, sys

SECONDS = 12

# Errors that belong to the headless build rather than to the game. Matched
# against the "at:" line, which names the engine file that raised it.
ALLOWED = re.compile(r"rasterizer_dummy|core/rid\.h|audio_driver_alsa|audio_server\.cpp")


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    project = os.path.normpath(os.path.join(here, ".."))
    godot = os.environ.get("GODOT", "godot3")

    try:
        p = subprocess.run([godot, "--path", project],
                           capture_output=True, text=True, timeout=SECONDS)
        out = p.stdout + p.stderr
    except subprocess.TimeoutExpired as e:
        # The game does not exit on its own, which is the point: it has to still
        # be standing when the clock runs out.
        out = (e.stdout or b"").decode("utf8", "replace") \
            + (e.stderr or b"").decode("utf8", "replace")
    except FileNotFoundError:
        print("boot: no godot at %r — set GODOT" % godot)
        return 2

    lines = out.splitlines()
    bad = []
    for i, line in enumerate(lines):
        if not line.startswith("ERROR:"):
            continue
        where = lines[i + 1] if i + 1 < len(lines) else ""
        if ALLOWED.search(where):
            continue
        bad.append(line + "\n  " + where.strip())

    for line in lines:
        if line.startswith("[Singularity] boot stopped") or "was refused by the tree" in line:
            bad.append(line)

    if bad:
        print("boot: the game came up with %d error(s) a player would get:" % len(bad))
        for b in bad:
            print("  " + b)
        return 1
    print("boot: the game came up clean over %d seconds" % SECONDS)
    return 0


if __name__ == "__main__":
    sys.exit(main())
