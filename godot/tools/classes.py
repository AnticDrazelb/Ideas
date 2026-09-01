#!/usr/bin/env python3
"""Rewrite project.godot's class registry from the source tree.

WHY THIS EXISTS, AND WHY IT IS NOT A SHORTCUT.

Godot 3.5 resolves a `class_name` through two tables in project.godot, and the
only thing that writes them is the EDITOR's project scan. A headless run of the
tests therefore has to be preceded by `godot --editor --quit` — which is fine
on a workstation and is a liability everywhere else: it wants a writable project
directory, it re-imports assets, and on a machine with no display it can sit
there indefinitely rather than failing.

The tables are not clever. Each entry is the class name, the literal word after
`extends`, and the path — all three of which are in the file itself. So this
writes them, the tests read them, and the checked-in project.godot is a build
product with a generator rather than an editor artefact nobody may touch.

    tools/classes.py            rewrite in place
    tools/classes.py --check    exit 1 if it would change anything
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT = os.path.join(ROOT, "project.godot")
DIRS = ("core", "game", "ui")

NAME = re.compile(r"^class_name\s+([A-Za-z_][A-Za-z0-9_]*)", re.M)
BASE = re.compile(r"^extends\s+([A-Za-z_][A-Za-z0-9_]*)", re.M)


def scan():
    out = []
    for d in DIRS:
        folder = os.path.join(ROOT, d)
        if not os.path.isdir(folder):
            continue
        for f in sorted(os.listdir(folder)):
            if not f.endswith(".gd"):
                continue
            path = os.path.join(folder, f)
            with open(path, encoding="utf-8") as fh:
                src = fh.read()
            m = NAME.search(src)
            if not m:
                continue
            b = BASE.search(src)
            out.append((m.group(1), b.group(1) if b else "Reference",
                        "res://%s/%s" % (d, f)))
    out.sort(key=lambda e: e[0])
    return out


def render(entries):
    body = ", ".join(
        '{\n"base": "%s",\n"class": "%s",\n"language": "GDScript",\n"path": "%s"\n}'
        % (base, name, path) for name, base, path in entries)
    classes = "_global_script_classes=[ %s ]\n" % body
    icons = "_global_script_class_icons={\n%s\n}\n" % "\n".join(
        '"%s": "",' % name for name, _, _ in entries)
    return classes, icons


def main():
    entries = scan()
    classes, icons = render(entries)

    with open(PROJECT, encoding="utf-8") as fh:
        text = fh.read()

    text2 = re.sub(r"_global_script_classes=\[.*?\] *\n", classes, text,
                   count=1, flags=re.S)
    text2 = re.sub(r"_global_script_class_icons=\{.*?\} *\n", icons, text2,
                   count=1, flags=re.S)

    if "--check" in sys.argv:
        if text2 != text:
            sys.stderr.write("project.godot is out of date — run tools/classes.py\n")
            return 1
        return 0

    if text2 != text:
        with open(PROJECT, "w", encoding="utf-8") as fh:
            fh.write(text2)
    print("%d classes" % len(entries))
    return 0


if __name__ == "__main__":
    sys.exit(main())
