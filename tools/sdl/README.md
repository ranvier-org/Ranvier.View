# Optional SDL IBus surrounding text

`ibus-surrounding.patch` applies to SDL commit
`ea7a2dabfd1ab6b0a5720568ffd207f5825e47f1`, the SDL submodule used by
`ppy.SDL3-CS` version `2026.1002.1`. It adds the optional
`SDL_Ranvier_SetIbusSurrounding` symbol to SDL's existing IBus connection.
The window reports whether that backend accepted the latest update through
`Window.ImeSurroundingAvailable`.

Build a shared SDL library from that commit with the patch applied and with
`SDL_IBUS`, `SDL_DBUS`, and the relevant Linux video backend enabled. The
private qualification used CMake Release, `SDL_SHARED=ON`, `SDL_STATIC=OFF`,
`SDL_WAYLAND=ON`, and `SDL_X11=ON`. Install the resulting `libSDL3.so` in the
application's own native runtime directory so that it is the library loaded
by SDL3-CS. The original SDL binary remains a valid fallback: the symbol is
optional and surrounding-text delivery is unavailable when it is absent.

This patch supports SDL's direct IBus context. SDL's X11/XIM and Wayland
text-input-v3 paths use different owners and are not supplied by this patch.
The prebuilt optional runtime and its automated distribution are not included
in this source prototype.
