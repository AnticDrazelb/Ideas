#!/bin/sh
# THE INTERFACE, MEASURED AT EVERY SHAPE THE GAME CLAIMS TO ANSWER.
#
# tests/ui.gd audits one window. This runs it across the set, one process each,
# because a screen is built once at boot and the honest question is what it looks
# like when it was built for THIS display rather than resized onto it.
#
# Six phones and three turned shapes. The turned ones are the whole reason the
# list is written down: a change that helps landscape and quietly costs portrait
# is a change nobody would notice until it shipped.
#
#   xvfb-run -a -s "-screen 0 2400x2000x24" tools/ui.sh
#   SHAPES="1280x720" tools/ui.sh
set -e
DIR=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot3}
SHAPES=${SHAPES:-"320x568 360x640 405x720 414x896 480x800 540x960 1280x720 800x480 1024x768"}
python3 "$DIR/tools/classes.py" >/dev/null
bad=0
for s in $SHAPES; do
	out=$(UI_SIZE="$s" "$GODOT" --path "$DIR" -s tests/ui.gd 2>&1) || true
	line=$(printf '%s\n' "$out" | grep -E "faults$|faults " | tail -1)
	printf '%-10s %s\n' "$s" "${line:-DID NOT REPORT}"
	printf '%s\n' "$out" | grep -E "^  ! " | sed 's/^/           /' || true
	case "$line" in
		*" 0 faults") ;;
		*) bad=$((bad + 1)) ;;
	esac
done
[ "$bad" -eq 0 ] || { echo "$bad shape(s) with faults"; exit 1; }
echo "every shape clean"
