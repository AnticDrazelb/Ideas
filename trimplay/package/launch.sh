#!/bin/sh
# CrossMix / TrimUI app launcher for TrimPlay.
APPDIR="$(cd "$(dirname "$0")" && pwd)"
cd "$APPDIR" || exit 1

# Our bundled libs first, then wherever the firmware keeps SDL2.
export LD_LIBRARY_PATH="$APPDIR/lib:/usr/trimui/lib:/usr/lib:${LD_LIBRARY_PATH:-}"
export HOME="$APPDIR"

# Software H.264 decoding wants every MHz the A133P has while streaming.
GOV=/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor
OLD_GOV="$(cat "$GOV" 2>/dev/null)"
echo performance > "$GOV" 2>/dev/null

./trimplay > "$APPDIR/log.txt" 2>&1

[ -n "$OLD_GOV" ] && echo "$OLD_GOV" > "$GOV" 2>/dev/null
sync
