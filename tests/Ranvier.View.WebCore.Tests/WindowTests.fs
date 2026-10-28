module Ranvier.View.WebCore.Tests.WindowTests

open System
open Expecto
open Fable.Core.TS.Dom
open Microsoft.FSharp.NativeInterop
open SDL
open Ranvier
open Ranvier.View
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl
open Ranvier.View.WebCore.Tests.Support

#nowarn "9" "51"

let private square =
    Component.define (fun _ (count: Signal<int>) -> count) (fun t ->
        t.button
            [|
                t.Attrs.attribute ("style", "position:absolute;left:0;top:0;width:100px;height:100px;border:0;background:rgb(255,0,0)")
                t.Events.on ("click", fun (count: Signal<int>) (_: Event) -> count.Value <- count.Value + 1)
            |]
            [| t.text (fun (count: Signal<int>) -> string count.Value) |])

// A hidden window drawn by SDL's software renderer, so the test needs no GPU, on the WebCore host thread.
let private windowCase (name: string) (body: Window -> unit) =
    testCase name (fun () ->
        onHostThread (fun () ->
            use window =
                Window.Create ("Ranvier test", 200, 150, hidden = true, renderer = "software")

            body window
            Expect.isEmpty (List.ofSeq window.Host.Errors) "listener, microtask and timer errors"))

let private steps (window: Window) count =
    for _ in 1..count do
        window.Step 0 |> ignore

let private pixel (struct (width: int, _: int, bytes: byte array)) x y =
    let at = (y * width + x) * 4
    bytes[at], bytes[at + 1], bytes[at + 2]

// Queues a left button press or release at x, y in the window, as SDL reports one from the platform.
let private press (window: Window) (kind: SDL_EventType) x y =
    let mutable event = SDL_Event ()
    let down = kind = SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN
    event.button.``type`` <- kind
    event.button.windowID <- SDL3.SDL_GetWindowID (NativePtr.ofNativeInt<SDL_Window> window.SdlWindow)
    event.button.button <- byte SDL3.SDL_BUTTON_LEFT
    event.button.down <- SDLBool.op_Implicit down
    event.button.clicks <- 1uy
    event.button.x <- x
    event.button.y <- y

    if not (SDLBool.op_Implicit (SDL3.SDL_PushEvent &&event): bool) then
        failwithf "SDL_PushEvent failed: %s" (SDL3.SDL_GetError ())

[<Tests>]
let tests =
    testList
        "Window"
        [
            windowCase "a mounted component reaches the window's texture"
            <| fun window ->
                use graph = new Graph ()
                let count = graph.Run (fun () -> Api.createSignal 0)
                use _mounted = mount window.Host graph square count
                steps window 3

                let image = window.ReadPixels ()
                let struct (width, height, _) = image
                Expect.isGreaterThan width 0 "the window has a texture"
                Expect.isGreaterThan height 0 "the window has a texture"
                Expect.equal (pixel image 90 90) (255uy, 0uy, 0uy) "the button's background is on screen"
                Expect.equal (pixel image 150 120) (255uy, 255uy, 255uy) "the page's background is on screen"

            windowCase "SDL mouse events reach the page as a click"
            <| fun window ->
                use graph = new Graph ()
                let count = graph.Run (fun () -> Api.createSignal 0)
                use _mounted = mount window.Host graph square count
                steps window 2

                press window SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN 50.0f 50.0f
                press window SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP 50.0f 50.0f
                steps window 2
                Expect.equal (graph.Run (fun () -> count.Value)) 1 "the click handler ran"

            test "keys map to DOM codes and names" {
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_A) "KeyA" "a letter"
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_0) "Digit0" "zero"
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_7) "Digit7" "a digit"
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_F13) "F13" "a high function key"
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_KP_5) "Numpad5" "a keypad digit"
                Expect.equal (Keys.code SDL_Scancode.SDL_SCANCODE_RALT) "AltRight" "a modifier"
                Expect.equal (Keys.key SDL_Scancode.SDL_SCANCODE_LEFT SDL_Keymod.SDL_KMOD_NONE) "ArrowLeft" "a named key"
                Expect.equal (Keys.key SDL_Scancode.SDL_SCANCODE_F5 SDL_Keymod.SDL_KMOD_NONE) "F5" "a function key's name"

                Expect.equal
                    (Keys.modifiers (
                        SDL_Keymod.SDL_KMOD_LSHIFT
                        ||| SDL_Keymod.SDL_KMOD_RCTRL
                    ))
                    3
                    "shift and control bits"
            }

            test "input method offsets count code points as UTF-16" {
                Expect.equal (Sdl.utf16Offset "a😀b" 2) 3 "a surrogate pair is two units"
                Expect.equal (Sdl.utf16Offset "abc" 9) 3 "an offset past the end clamps"
            }
        ]
