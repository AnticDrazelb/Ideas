cmake_minimum_required(VERSION 3.13)
project(trimplay C CXX)

# Path to a chiaki-ng checkout (with submodules). build/Dockerfile provides one.
set(CHIAKI_DIR "" CACHE PATH "chiaki-ng source checkout")
if(NOT EXISTS "${CHIAKI_DIR}/lib/CMakeLists.txt")
	message(FATAL_ERROR "Set -DCHIAKI_DIR=/path/to/chiaki-ng (with submodules initialised)")
endif()

# Only libchiaki is needed: no Qt GUI, no CLI, no chiaki-side video decoder
# (TrimPlay decodes H.264 itself with settings tuned for the A133P).
foreach(opt CHIAKI_ENABLE_GUI CHIAKI_ENABLE_CLI CHIAKI_ENABLE_TESTS CHIAKI_ENABLE_STEAM_SHORTCUT)
	set(${opt} OFF CACHE BOOL "" FORCE)
endforeach()
foreach(opt CHIAKI_ENABLE_SETSU CHIAKI_ENABLE_STEAMDECK_NATIVE CHIAKI_ENABLE_FFMPEG_DECODER
		CHIAKI_ENABLE_PI_DECODER CHIAKI_ENABLE_SPEEX)
	set(${opt} OFF CACHE STRING "" FORCE)
endforeach()
set(CHIAKI_LIB_MINIUPNPC_EXTERNAL_PROJECT ON CACHE BOOL "" FORCE)

add_subdirectory("${CHIAKI_DIR}" chiaki EXCLUDE_FROM_ALL)

find_package(PkgConfig REQUIRED)
pkg_check_modules(SDL REQUIRED IMPORTED_TARGET sdl2 SDL2_ttf)
pkg_check_modules(AV REQUIRED IMPORTED_TARGET libavcodec libavutil)

add_executable(trimplay
	src/main.c
	src/stream.c
	src/input.c
	src/ui.c
	src/store.c)
set_property(TARGET trimplay PROPERTY C_STANDARD 11)
target_compile_options(trimplay PRIVATE -Wall -Wextra -Wno-format-truncation)
target_link_libraries(trimplay PRIVATE chiaki-lib PkgConfig::SDL PkgConfig::AV)
# Find bundled libraries next to the binary without needing LD_LIBRARY_PATH.
set_target_properties(trimplay PROPERTIES BUILD_RPATH "\$ORIGIN/lib" INSTALL_RPATH "\$ORIGIN/lib")
