#!/usr/bin/env python3
"""
DOES THE LISTING FIT? — the same job reflow.py does for the screens.

Google Play truncates a short description at 80 characters and a full one at
4000, on the store, silently, after the upload. That is the wrong place to find
out, and the numbers are trivial to check here. The copy lives in STORE.md in
fenced blocks so there is one text rather than a file and a paste of it.

    python3 tools/type/listing.py        # non-zero if either is over
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
STORE = os.path.join(HERE, "..", "..", "STORE.md")

LIMITS = [("short description", 80), ("full description", 4000)]


def blocks(text):
    return re.findall(r"^```\n(.*?)^```", text, re.S | re.M)


def main():
    with open(STORE, encoding="utf-8") as fh:
        found = blocks(fh.read())

    if len(found) < len(LIMITS):
        print("listing: STORE.md has %d fenced blocks, expected %d" % (len(found), len(LIMITS)))
        return 1

    bad = 0
    for (name, limit), body in zip(LIMITS, found):
        # Play counts the text as pasted; a trailing newline from the fence is not it
        n = len(body.rstrip("\n"))
        over = n > limit
        bad += over
        print("%-20s %5d of %4d  %s" % (name, n, limit, "OVER" if over else "fits"))

    print("listing: " + ("something is over its limit" if bad else "both fit"))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
