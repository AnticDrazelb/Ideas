#!/bin/sh
# Builds TrimPlay for the TrimUI Smart Pro. Needs Docker. On an x86 PC, first
# enable ARM emulation once:
#   docker run --privileged --rm tonistiigi/binfmt --install arm64
# Output: trimplay/dist/TrimPlay.zip (unzip into SD:/Apps/)
set -eu
HERE=$(cd "$(dirname "$0")/.." && pwd)
mkdir -p "$HERE/dist"
docker build --platform linux/arm64 -t trimplay-build "$HERE/build"
docker run --rm --platform linux/arm64 \
	-v "$HERE:/src:ro" -v "$HERE/dist:/out" \
	trimplay-build sh /src/build/inside.sh
