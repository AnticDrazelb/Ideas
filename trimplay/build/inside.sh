#!/bin/sh
# Runs inside the build container: compiles TrimPlay and stages the
# CrossMix app folder + zip into /out.
set -eu

SRC=/src
OUT=/out
BUILD=/tmp/build
APP="$OUT/TrimPlay"

cmake -S "$SRC" -B "$BUILD" -DCMAKE_BUILD_TYPE=Release -DCHIAKI_DIR=/opt/chiaki-ng \
	-DCMAKE_C_FLAGS="-mcpu=cortex-a53"
cmake --build "$BUILD" --target trimplay -j"$(nproc)"

rm -rf "$APP" "$OUT/TrimPlay.zip"
mkdir -p "$APP/lib" "$APP/res"
cp "$BUILD/trimplay" "$APP/"
strip "$APP/trimplay"
cp "$SRC/package/launch.sh" "$SRC/package/config.json" "$SRC/package/icon.png" "$APP/"
cp "$SRC/package/trimplay.ini" "$APP/trimplay.ini"
cp /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf "$APP/res/font.ttf"
cp "$SRC/README.md" "$APP/README.md"
cp /opt/chiaki-ng/COPYING "$APP/COPYING-chiaki-ng"
chmod +x "$APP/launch.sh" "$APP/trimplay"

# Bundle the libraries we built against, except the ones that must come from
# the device itself (glibc, and SDL2 + GPU/audio stacks the firmware ships).
ldd "$APP/trimplay" | awk '/=> \//{print $3}' | while read -r lib; do
	base=$(basename "$lib")
	case "$base" in
		ld-linux*|libc.so*|libm.so*|libdl.so*|librt.so*|libpthread.so*|libresolv.so*) continue ;;
		libSDL2-2.0.so*|libGL*|libEGL*|libGLES*|libgbm*|libdrm*|libasound*|libpulse*|libX*|libxcb*|libwayland*|libdbus*|libudev*|libsystemd*) continue ;;
		libstdc++*|libgcc_s*) continue ;;
	esac
	cp -L "$lib" "$APP/lib/"
done
for l in "$APP"/lib/*.so*; do patchelf --set-rpath '$ORIGIN' "$l" 2>/dev/null || true; done

echo "Bundled libraries:"; ls -1 "$APP/lib"
file "$APP/trimplay"
cd "$OUT" && zip -qr TrimPlay.zip TrimPlay
echo "Done: $OUT/TrimPlay.zip"
