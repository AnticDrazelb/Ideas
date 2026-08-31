#!/bin/sh
# THE CHECK, AND IT NEEDS NO EDITOR OPEN.
#
# Godot 3.5 registers `class_name` scripts by scanning the project, and that
# scan is an editor job — so a headless run of the tests has to be preceded by
# one headless editor pass or every global class is "not declared in the
# current scope". The pass is idempotent and takes about a second.
#
#   GODOT=/path/to/godot3.5 tools/check.sh
set -e
DIR=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot3}
"$GODOT" --path "$DIR" --editor --quit >/dev/null 2>&1 || true
exec "$GODOT" --path "$DIR" -s tests/run.gd
