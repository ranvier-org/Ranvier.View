#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
    printf 'usage: %s <pinned-sdl-checkout> <output-directory>\n' "$0" >&2
    exit 2
fi

source_dir=$(cd "$1" && pwd -P)
mkdir -p "$2"
output_dir=$(cd "$2" && pwd -P)
script_dir=$(cd "$(dirname "$0")" && pwd -P)
revision=ea7a2dabfd1ab6b0a5720568ffd207f5825e47f1
if [ "$(git -C "$source_dir" rev-parse HEAD)" != "$revision" ]; then
    printf 'SDL checkout must be at %s\n' "$revision" >&2
    exit 1
fi

work=$(mktemp -d "${TMPDIR:-/tmp}/ranvier-sdl-ibus.XXXXXXXX")
cleanup() { rm -rf -- "$work"; }
trap cleanup EXIT
mkdir -p "$work/source"
git -C "$source_dir" archive "$revision" | tar -x -C "$work/source"
git -C "$work/source" apply --check "$script_dir/ibus-surrounding.patch"
git -C "$work/source" apply "$script_dir/ibus-surrounding.patch"
cmake -S "$work/source" -B "$work/build" -DCMAKE_BUILD_TYPE=Release \
    -DSDL_SHARED=ON -DSDL_STATIC=OFF -DSDL_DBUS=ON -DSDL_IBUS=ON \
    -DSDL_X11=ON -DSDL_X11_XSCRNSAVER=OFF -DSDL_WAYLAND=ON -DSDL_TEST_LIBRARY=OFF
cmake --build "$work/build" --parallel 1
nm -D --defined-only "$work/build/libSDL3.so" > "$work/exports"
grep -q 'SDL_Ranvier_SetIbusSurrounding' "$work/exports"
grep -q 'SDL_Ranvier_GetIbusDeleteEventType' "$work/exports"
grep -q '^#define SDL_VIDEO_DRIVER_X11 1' "$work/build/include-config-release/build_config/SDL_build_config.h"
grep -q '^#define HAVE_DBUS_DBUS_H 1' "$work/build/include-config-release/build_config/SDL_build_config.h"
grep -q '^#define HAVE_IBUS_IBUS_H 1' "$work/build/include-config-release/build_config/SDL_build_config.h"
grep -q '^SDL_DBUS:BOOL=ON$' "$work/build/CMakeCache.txt"
grep -q '^SDL_IBUS:BOOL=ON$' "$work/build/CMakeCache.txt"
cp "$work/build/include-config-release/build_config/SDL_build_config.h" "$output_dir/SDL_build_config.h"
cp "$work/build/CMakeCache.txt" "$output_dir/CMakeCache.txt"
cp -L "$work/build/libSDL3.so" "$output_dir/libSDL3.so"
cp "$work/source/LICENSE.txt" "$output_dir/SDL-LICENSE.txt"
sha256sum "$output_dir/libSDL3.so"
