# Custom window shells: design record

Status: implemented in `Ranvier.View.WebCore.Sdl`, with `examples/Ranvier.CustomShell` as the complete app. The prototype
behind these decisions is on branch `prototype/custom-shell`. Measured on Windows 11 x64 only, with real OS input, at 96
DPI for the implementation and at 96 and 120 DPI for the prototype.

## Core and view state

**Core primitives used**

- `Signal`, `Memo`: `Window.Placement` and `Window.Focused` are read-only memos over private signals.
- `Graph`, `flush`: the window's graph holds them, and SDL events write the signals and flush.
- `createRoot`: owns the memos, disposed with the window's other reactive state.

**View-owned state**

- The current window flags live in SDL, so `Placement` is written from SDL events.
- Regions live in the DOM as `data-window-region` attributes. The window keeps no region table.
- `SnapButtonSubclass` keeps the previous window procedure, whether the pointer is over the button, and whether a press
  is held. All three are Win32 state with no core equivalent.
- `onPage`, `callbackDepth` and `disposePending` track the native call stack, which the core cannot observe.

**For the core:** a computation on graph A that reads a signal from graph B links silently and never re-runs. Probe:
effect on `appGraph`, signal on `windowGraph`; the effect saw `normal` only, through writes and flushes on either graph.
A same-graph control re-ran. A cross-graph read that raises would have surfaced this. That belongs in `src/Ranvier`.

## What an app writes

```fsharp
use window =
    Window.Create ("Shell", 900, 600, hidden = true, frame = WindowFrame.Custom 6, minimumSize = struct (480, 320))

use mounted = mount window // Mounting.mount window.Graph ...
window.Present ()
window.Show ()
window.Run ()
```

```fsharp
t.div [| t.Attrs.windowRegion WindowRegion.Drag |] [|
    t.div [| t.Attrs.windowRegion WindowRegion.Content |] [|
        t.button [| t.Events.on ("click", fun s _ -> s.Window.Minimize ()) |] [| ... |]
        t.button [|
            t.Attrs.windowRegion WindowRegion.Maximize
            t.Events.on ("click", fun s _ -> s.Window.ToggleMaximize ())
        |] [| t.text (fun s -> if s.Window.Placement.Value = WindowPlacement.Maximized then "❐" else "□") |]
        t.button [| t.Events.on ("click", fun s _ -> s.Window.Close ()) |] [| ... |]
    |]
|]
```

App code holds no SDL, Win32 or geometry.

The package README holds the member contracts. These behaviours have no API, because each is only ever wanted one way:

- The page keeps rendering during a platform move or resize.
- A right click on a drag region opens the window menu.
- A custom frame on Windows gets rounded corners and the compositor's shadow.

## Decisions, and the evidence for each

### 1. Regions are a DOM attribute, read from the page at hit-test time

The alternatives were an app callback, or registered rectangles kept current by observing layout. Asking the page with
`elementFromPoint` and `closest` removes that state and its staleness:

- An overlay over the title bar stops the drag with no code.
- A resized or reflowed title bar is correct at once.

`t.Attrs.windowRegion` writes the attribute, so a typo is a compile error. An unknown value is `Content`.

Prototype cost: 38 to 273 us on average per run, 1.4 ms at worst, including forced layout. A hit test runs per mouse
message over the window. Those runs had 5 to 119 hit tests from injected input, so a long session with a real mouse is
unmeasured. A message reads the page once: the Win32 subclass passes its answer to SDL's hit test beneath it.

### 2. The maximize button is an ordinary button

`Maximize` tells Windows to treat the element as `HTMAXBUTTON`, which SDL's hit test cannot report. A subclass answers
`WM_NCHITTEST` and sends the button's non-client mouse input back to the page, so `:hover`, `click` and the app's handler
all work as for any other button.

A press takes mouse capture, as a browser does, so the page receives the release wherever it lands and a release off the
button clicks nothing. Forwarding the press to `DefWindowProc` instead enters Windows' own button tracking loop.

### 3. Rounded corners come from the corner preference alone

| DWM calls | Rounded corners and shadow | Snap Layouts flyout |
| --- | --- | --- |
| None | No | Yes |
| `DWMWA_WINDOW_CORNER_PREFERENCE` | Yes | **Yes** |
| `DwmExtendFrameIntoClientArea`, top 1 | Yes | **No** |

`DwmExtendFrameIntoClientArea` is the common recipe, and it is the call that suppresses the flyout. The flyout result held
at 96 and 120 DPI.

### 4. Live resize is built in, for every window

The platform's modal loop blocks `Step` for a whole move or resize gesture. SDL raises `SDL_EVENT_WINDOW_EXPOSED` with
`data1 = 1` inside that loop, and an event watch runs a page turn from it.

| Gesture of about 4 s, held still for 1.5 s | Without the watch | With the watch (prototype) |
| --- | --- | --- |
| Longest frame gap, drag | 3,433 ms | 109 ms |
| Longest frame gap, resize | 3,393 ms | under 100 ms, the logging threshold |
| Mid-resize content | Stale `900 x 600`, black in the new area | Reflowed to `1020 x 600` |

The implementation's longest gap across a whole driven run under NativeAOT was 76 ms, with the page at `1020 x 600` during
the held resize.

A tick presents only when the page wants a frame, so an idle page does no work during a move. Three rules make a turn
safe inside a native callback:

- A tick with a page call on the stack waits for the next turn. Without this rule, a frame callback that reaches a tick
  recurses until the stack overflows.
- A failure surfaces from the next `Step`, because an exception cannot cross a native frame.
- `Dispose` from page code in a callback closes the window at once and finishes in the next `Step`. Tearing down there
  hangs inside SDL.

### 5. Apps mount on `window.Graph`

The cross-graph result above rules out signals on a private graph. `Window`'s graph is the natural owner, and a shared
graph removes a second `new Graph ()` from every app. Multiple windows sharing state need a further decision: either
`Window.Create (..., ?graph)`, or the core raising on cross-graph reads.

## Verification

`tests/Ranvier.View.WebCore.Tests/WindowShellTests.fs` runs in the suite. Windows-only cases send real messages to the
window's procedure on a hidden window. Its modal ticks are pushed SDL events, and its placement and window menu use
internal stand-ins, because a real maximize shows a hidden window and a real menu is modal.

Real input against the published NativeAOT example at 96 DPI:

| Behaviour | Result |
| --- | --- |
| Drag on the title bar | Moved by the exact delta |
| Edge and corner resize; minimum size | Worked; stopped at `480 x 320` |
| Tab inside the drag region | Received the click; the window stayed put |
| Snap Layouts on the maximize button | The flyout appeared |
| Press on maximize, release elsewhere | No maximize; a later click worked |
| Maximize and restore through the button | Worked, restoring the exact rectangle |
| Double click on the title bar | Maximized and restored |
| Minimize button; restore from outside | Worked |
| Close button | Process exited with 0 |

The prototype also covered, at 96 and 120 DPI: Aero Snap and unsnap, resize cursors, the window menu, page size when
maximized, and typing in a page input. Those were not repeated on the implementation.

## Gaps outside `Window`

- **`contextmenu` never fires.** A right click on page content logged nothing, and the ABI's mouse actions are move, down
  and up. Custom right-click menus are a common shell need, so this belongs in `Ranvier.WebCore` or needs a synthesized
  event from the view.
- **Translucency is still blocked**: the swap chain ignores alpha and `Window` cannot request a transparent window.

## Not built: other common paths

These follow the same rule of a typed member on `Window`, with state on `Graph`. The shapes are sketches.

| Path | Today | Sketch |
| --- | --- | --- |
| OS file drop in | No `SDL_EVENT_DROP_*` handling; `rv_host_drag` is unwrapped | Forward to the page's DOM `drop` by default |
| Drag out | `rv_host_set_drag_start` is unwrapped | `window.OnDragStart` |
| `<select>`, tooltips | No popup handler, so `<select>` opens no menu | Default handlers on `Window`, using `SDL_CreatePopupWindow` |
| File dialogs | `SDL_ShowOpenFileDialog` exists; unwrapped here | `window.OpenFiles (...)` returning a task |
| Size and position | No setters | `window.Size`, `window.Position`, `window.Center ()` |
| Fullscreen | `Placement` reports it; no command | `window.ToggleFullscreen ()` |
| Several windows | Each needs its own `Step`, and only that window presents | `Window.RunAll ()` |

`Window.run` passes setup the `WebCoreHost`, so setup cannot reach any of this. It should pass the `Window`; that is a
breaking change, so an app uses `Window.Create` and `Run` for now.

## Unverified

- **macOS and Linux.** Nothing ran there with a custom frame. macOS borderless loses the traffic lights, which likely needs
  its own frame option. Wayland compositors decide more of the hit-test behaviour. CI will run the shared tests there.
- **Several windows.** A modal turn runs only the moving window, so others freeze during a gesture, as they do without
  this work.
- **`Dispose` during a real gesture.** Tests cover a pushed tick; closing from a frame callback during a real drag has not
  been driven.
- **Touch and pen on regions.**
- **Cost under a real mouse** and on heavy pages. `elementFromPoint` forces layout, and a page mid-animation may pay more.
- **The corner call on Windows 10**, where the attribute does not exist and the call's failure is ignored.
- **The hit-test window at 120 DPI on the implementation.** The prototype passed; the coordinate code is unchanged.
