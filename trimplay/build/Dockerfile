# Build environment for TrimPlay on the TrimUI Smart Pro (aarch64).
# Debian bullseye's glibc (2.31) is old enough to run on the stock/CrossMix
# root filesystem. Build with:  ./build/build.sh   (works on x86 via QEMU)
FROM --platform=linux/arm64 debian:bullseye

RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
	build-essential cmake git ca-certificates curl xz-utils pkg-config nasm \
	python3 python3-protobuf protobuf-compiler \
	libssl-dev libopus-dev libjson-c-dev libevent-dev \
	libsdl2-dev libsdl2-ttf-dev fonts-dejavu-core zip file patchelf \
	&& rm -rf /var/lib/apt/lists/*

# A tiny FFmpeg: only the H.264 decoder. Keeps the bundle small and avoids
# dragging in Debian's FFmpeg and its dozens of codec libraries.
ARG FFMPEG_VERSION=6.1.2
RUN curl -fsSL https://ffmpeg.org/releases/ffmpeg-${FFMPEG_VERSION}.tar.xz | tar -xJ -C /tmp \
	&& cd /tmp/ffmpeg-${FFMPEG_VERSION} \
	&& ./configure --prefix=/opt/ffmpeg --enable-shared --disable-static \
		--disable-everything --disable-programs --disable-doc --disable-network \
		--disable-avdevice --disable-avformat --disable-avfilter --disable-swscale --disable-swresample \
		--disable-postproc --enable-decoder=h264 --enable-parser=h264 --enable-neon \
	&& make -j"$(nproc)" && make install && rm -rf /tmp/ffmpeg-*
ENV PKG_CONFIG_PATH=/opt/ffmpeg/lib/pkgconfig

# chiaki-ng provides libchiaki (discovery, pairing, Takion streaming, crypto).
ARG CHIAKI_REPO=https://github.com/streetpea/chiaki-ng.git
ARG CHIAKI_REF=8179a2d1964c75dc32614fd27e51c2381093f539
RUN git init /opt/chiaki-ng && cd /opt/chiaki-ng \
	&& git remote add origin ${CHIAKI_REPO} \
	&& git fetch --depth 1 origin ${CHIAKI_REF} && git checkout FETCH_HEAD \
	&& git submodule update --init --depth 1 third-party/nanopb third-party/jerasure third-party/gf-complete third-party/curl
