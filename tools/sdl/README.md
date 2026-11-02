# Optional SDL IBus surrounding text

`ibus-surrounding.patch` applies to SDL commit
`ea7a2dabfd1ab6b0a5720568ffd207f5825e47f1`, the SDL submodule used by
`ppy.SDL3-CS` version `2026.1002.1`. It adds the optional
`SDL_Ranvier_SetIbusSurrounding` symbol to SDL's existing IBus connection.
The window reports whether its backend currently has valid surrounding text
through `Window.ImeSurroundingAvailable`; it is false after a clear, including
when a password field is focused.

Build a shared SDL library from that commit with the patch applied and with
`SDL_IBUS`, `SDL_DBUS`, and the relevant Linux video backend enabled. The
private qualification used CMake Release, `SDL_SHARED=ON`, `SDL_STATIC=OFF`,
`SDL_WAYLAND=ON`, and `SDL_X11=ON`. Install the resulting `libSDL3.so` in the
application's own native runtime directory so that it is the library loaded
by SDL3-CS. The original SDL binary remains a valid fallback: the symbol is
optional and surrounding-text delivery is unavailable when it is absent.

`bash tools/sdl/build-linux.sh <pinned-sdl-checkout> <output-directory>` builds this variant
from a clean archive of the pinned commit with one compile job. The build host
must supply SDL's Linux development dependencies, including IBus headers; the
script verifies the X11 configuration and extension symbols before copying
the library and SDL license to the output directory.

This patch supports SDL's direct IBus context. On X11, set
`SDL_RANVIER_IBUS=1` before SDL initializes to select direct IBus; if that
context cannot initialize, SDL falls back to XIM and surrounding text is
unavailable. An explicitly selected different input method is not replaced.
Wayland text-input-v3 and XIM use different owners and are not supplied by
this extension. The direct IBus fallback on a nested Wayland compositor was
tested separately; it is not proof of a default WSLg text-input-v3 path.
`package/Ranvier.View.WebCore.Sdl.Ibus.csproj` builds an opt-in package from a
qualified patched SDL binary. Set `IbusSdlLibrary` to the binary when packing.
An application that references this package and publishes with the explicit
`linux-x64` runtime identifier receives that binary in place of the SDL runtime
from SDL3-CS. Single-file publish is not supported by this package yet. Other
runtime identifiers keep their existing SDL runtime.

The privately qualified x64 SDL binary and the matching P7 x64 WebCore hosts
both import glibc 2.38. The package does not establish support for older glibc
versions or other architectures. `package/tests` contains a loader probe for
regular and NativeAOT publish; actual input-method qualification is separate.
The package and binary have not been published.
