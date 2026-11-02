using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;

Console.WriteLine($"SDL version: {Native.SDL_GetVersion()}");
var expectedLibrary = Path.Combine(AppContext.BaseDirectory, "libSDL3.so");
if (!File.ReadAllText("/proc/self/maps").Contains(expectedLibrary, StringComparison.Ordinal))
    throw new Exception("SDL P/Invoke did not load the published runtime");
if (!NativeLibrary.TryLoad("SDL3", typeof(SDL.SDL_Window).Assembly, null, out var library))
    throw new Exception("SDL load failed");
try
{
    if (!NativeLibrary.TryGetExport(library, "SDL_Ranvier_SetIbusSurrounding", out _)
        || !NativeLibrary.TryGetExport(library, "SDL_Ranvier_GetIbusDeleteEventType", out _))
        throw new Exception("optional IBus entry points missing");
    Console.WriteLine("PASS optional IBus SDL loaded");
}
finally { NativeLibrary.Free(library); }

static class Native
{
    [DllImport("SDL3")]
    internal static extern int SDL_GetVersion();
}
