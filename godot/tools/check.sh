#!/bin/sh
# THE CHECK, AND IT NEEDS NO EDITOR OPEN.
#
# Godot 3.5 resolves every `class_name` through two tables in project.godot, and
# the only thing that writes them is the editor's project scan — so a headless
# run of the tests used to have to be preceded by `godot --editor --quit`. That
# pass wants a writable project, re-imports every asset, and on a machine with no
# display can sit there indefinitely rather than failing.
#
# tools/classes.py writes the same two tables from the source tree, which is
# where all three fields of every entry already are. So the registry is a build
# product with a generator, the tests read it, and nothing here needs an editor.
#
#   GODOT=/path/to/godot3.5 tools/check.sh
set -e
DIR=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot3}
python3 "$DIR/tools/classes.py" >/dev/null
"$GODOT" --path "$DIR" -s tests/compile.gd
exec "$GODOT" --path "$DIR" -s tests/run.gd
