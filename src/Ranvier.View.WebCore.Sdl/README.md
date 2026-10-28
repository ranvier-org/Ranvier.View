# Ranvier.View.WebCore.Sdl

Shows a [Ranvier.View.WebCore](../Ranvier.View.WebCore/README.md) page in an SDL3 window, through
[ppy.SDL3-CS](https://github.com/ppy/SDL3-CS). The window paints the page when it asks for a frame. It forwards
mouse, wheel, keyboard, input method and focus events to the page. It also sets the page's cursor and moves its
`text/plain` clipboard data through SDL.

Experimental, and Windows x64 only for now, like the backend.

```fsharp
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore.Sdl

Window.run ("Counter", 480, 320) (fun host ->
    let graph = new Graph ()
    let container = host.Document.createElement "div"
    host.Document.body.appendChild container |> ignore

    Mounting.mount graph None (fun errors -> raise (AggregateException errors)) container None (fun context ->
        Program.run (Component.render counter 0) context.Backend))
```

`Window.run` returns when the window closes. An app that owns its loop calls `Window.create` and then `Step` each turn
instead. `Step` waits for window events until the next timer is due, dispatches them, cycles the host and presents a
frame when one is wanted:

```fsharp
use window = Window.create ("Counter", 480, 320)
// mount into window.Host.Document
while window.Step () do
    () // the app's own work
```

The window's size is in the platform's window units. The page's CSS pixels follow the display's scale, so a 480 by
320 window shows 480 by 320 CSS pixels on any display. `NativeHandle` is the platform's window (an `HWND` on
Windows), for an accessibility bridge.

All calls go on the thread that created the window. That thread is the host's thread, and the main thread on macOS.

[`examples/Ranvier.NativeCounter`](../../examples/Ranvier.NativeCounter) is a complete app, published with NativeAOT.
