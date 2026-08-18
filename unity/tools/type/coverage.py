#!/usr/bin/env python3
"""
WHICH STRINGS ON SCREEN HAVE NEVER BEEN MEASURED.

reflow.py holds every box and label somebody has thought to add to it, and it
has caught real overflows — a seventeen-point vault name that wrapped to three
lines, a calibrate hint printing past the panel, a forge tool reading [ WIPE DEC.
What it cannot do is tell you what is MISSING from it, and every one of those
bugs was found because somebody happened to look at that screen.

So this reads the UI source, pulls out every string that reaches a Label, a
Bracketed button, a Link or a Switch, and prints the ones reflow.py has never
seen. It cannot know how wide their boxes are — that is a layout question and it
belongs in reflow.py, written down by hand next to the reason. It can tell you
the size of the gap, which is the thing nobody knew.

    python3 tools/type/coverage.py
"""
import re, sys, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
UI = [
    "SingularityEngine/Assets/Scripts/Game/UI/Screens.cs",
    "SingularityEngine/Assets/Scripts/Game/UI/ForgeScreens.cs",
    "SingularityEngine/Assets/Scripts/Game/UI/Hud.cs",
    "SingularityEngine/Assets/Scripts/Game/UI/Coach.cs",
    "SingularityEngine/Assets/Scripts/Game/UI/Chapters.cs",
]

# WHICH ARGUMENT OF WHICH CALL IS THE THING A PLAYER READS. Every one of these
# was checked against its own signature, because index 1 of a UiKit call is the
# GameObject's name and picking that up fills the report with ids nobody sees.
#
# `bracket` marks the calls whose label is drawn inside [ ], because that is the
# form reflow.py has to be given — [ MENU ] is nine characters wide and MENU is
# four, and measuring the short one is measuring nothing.
CALLS = [
    (r'UiKit\.Label\s*\(',      [2], False),
    (r'UiKit\.Bracketed\s*\(',  [2], True),
    (r'UiKit\.Link\s*\(',       [2], False),
    (r'(?<![.\w])Toggle\s*\(',   [2, 3], False),
    (r'(?<![.\w])AccToggle\s*\(',[2, 3], False),
    (r'(?<![.\w])Bar\s*\(',      [2, 3], False),
    (r'(?<![.\w])Link\s*\(',     [2, 3], False),
    (r'(?<![.\w])Third\s*\(',    [2], True),
    (r'(?<![.\w])Row\s*\(',      [3], True),
    (r'(?<![.\w])Quad\s*\(',     [2], True),
]

def args(text, i):
    """Split the argument list starting at the '(' index, at depth-0 commas."""
    depth, out, cur, instr, esc = 0, [], "", False, False
    while i < len(text):
        c = text[i]
        if instr:
            cur += c
            if esc: esc = False
            elif c == '\\': esc = True
            elif c == '"': instr = False
        elif c == '"':
            instr = True; cur += c
        elif c in "([{":
            depth += 1
            if depth == 1 and c == '(': cur = ""
            else: cur += c
        elif c in ")]}":
            depth -= 1
            if depth == 0: out.append(cur.strip()); return out
            cur += c
        elif c == ',' and depth == 1:
            out.append(cur.strip()); cur = ""
        else:
            cur += c
        i += 1
    return out

def literals(path):
    text = (ROOT / path).read_text()
    found = set()
    for pat, idxs, bracket in CALLS:
        for m in re.finditer(pat, text):
            a = args(text, m.end() - 1)
            for idx in idxs:
                if len(a) <= idx: continue
                v = a[idx].strip()
                if not (v.startswith('"') and v.endswith('"')): continue
                v = v[1:-1]
                if len(v) < 2: continue
                found.add("[ " + v + " ]" if bracket else v)
    return found

def measured():
    text = (ROOT / "tools/type/reflow.py").read_text()
    # ONE LINE AT A TIME. reflow.py is full of triple-quoted prose, so a pattern
    # that may cross a newline matches from one stray quote to the next and
    # swallows the whole file — which reported every string in the game as
    # unmeasured, including the ones sitting in the table three lines up.
    return set(re.findall(r'"([^"\n]{2,})"', text))

# STRINGS THAT ARE NOT A LABEL, with the reason. Everything else in the report
# is a thing somebody has to measure; these are not, and leaving them in the
# output forever is how a report stops being read.
IGNORE = {
    "BACK": "drawn bracketed — measured as [ BACK ]",
    "MENU": "drawn bracketed — measured as [ MENU ]",
    "OPEN": "drawn bracketed — measured as [ OPEN ]",
    "/0": "a fragment of the fold counter, appended to a number",
}

def wrapped(s):
    """Prose that wraps: reflow measures those as BOXES, by their own names."""
    return "\\n" in s or len(s) > 60

def main():
    seen, want = measured(), set()
    per = {}
    for p in UI:
        f = literals(p)
        per[p.rsplit("/", 1)[-1]] = f
        want |= f

    def gap_of(strings):
        return sorted(s for s in strings
                      if s not in seen and s not in IGNORE and not wrapped(s))

    missing = gap_of(want)
    print("UI STRINGS THAT REFLOW HAS NEVER MEASURED")
    print()
    for f, strings in per.items():
        gap = gap_of(strings)
        if not gap: continue
        print(f"  {f}  —  {len(gap)} of {len(strings)}")
        for s in gap:
            print(f"      {len(s):>3}  {s}")
        print()
    print(f"{len(want) - len(missing)} of {len(want)} measured, {len(missing)} not")
    return 0

sys.exit(main())
