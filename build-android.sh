#!/usr/bin/env bash
#
# Build SINGULARITY ENGINE as an Android APK, without opening the editor.
#
#   ./build-android.sh                 # development build
#   ./build-android.sh -release        # release build
#   UNITY=/path/to/Unity ./build-android.sh
#
# It applies the project settings first, so a fresh clone on a machine that has
# never opened this project builds the same thing as one that has. A build that
# depends on somebody having clicked through the editor once is not a build.
#
# REQUIREMENTS
#   Unity 6 LTS with the ANDROID BUILD SUPPORT module, including OpenJDK and the
#   SDK/NDK sub-options. The NDK is not optional here: the player is IL2CPP so
#   that it can be ARM64, and IL2CPP compiles through the NDK.
#
set -euo pipefail

cd "$(dirname "$0")"
PROJECT="$PWD/unity/SingularityEngine"
LOG="$PWD/unity/build.log"

# Find Unity. UNITY= wins; otherwise look where each platform's Hub puts it.
if [[ -n "${UNITY:-}" ]]; then
  UNITY_BIN="$UNITY"
else
  UNITY_BIN="$(
    ls -d \
      "$HOME/Unity/Hub/Editor/"*/Editor/Unity \
      "/Applications/Unity/Hub/Editor/"*/Unity.app/Contents/MacOS/Unity \
      "/opt/unity/Editor/Unity" \
      "/c/Program Files/Unity/Hub/Editor/"*/Editor/Unity.exe \
      2>/dev/null | sort -V | tail -1 || true
  )"
fi

if [[ -z "${UNITY_BIN:-}" || ! -x "$UNITY_BIN" ]]; then
  cat >&2 <<'EOF'
Could not find a Unity editor.

Set it explicitly:

  UNITY="/path/to/Unity" ./build-android.sh

On macOS that is usually
  /Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity
and on Linux
  ~/Unity/Hub/Editor/<version>/Editor/Unity
EOF
  exit 1
fi

echo "Unity:   $UNITY_BIN"
echo "Project: $PROJECT"
echo "Log:     $LOG"
echo

set +e
"$UNITY_BIN" \
  -batchmode -nographics -quit \
  -projectPath "$PROJECT" \
  -executeMethod Singularity.EditorTools.BuildAndroid.CommandLine \
  -logFile "$LOG" \
  "$@"
STATUS=$?
set -e

if [[ $STATUS -ne 0 ]]; then
  echo
  echo "Build failed (exit $STATUS). The interesting lines are usually these:"
  echo
  grep -E "error CS|Error building|BuildFailedException|Exception|Singularity\]" "$LOG" | tail -30 || true
  echo
  echo "Full log: $LOG"
  exit $STATUS
fi

APK="$PROJECT/Builds/SingularityEngine.apk"
echo
if [[ -f "$APK" ]]; then
  echo "Built $APK ($(du -h "$APK" | cut -f1))"
  echo
  echo "Install it with:  adb install -r \"$APK\""
else
  echo "Unity reported success but no APK is at $APK — see $LOG"
  exit 1
fi
