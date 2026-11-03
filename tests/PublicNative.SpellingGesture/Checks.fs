module internal PublicNativeSpellingGesture

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

let private passed = ResizeArray<string>()
let private failures = ResizeArray<string>()

let private require label condition =
    if not condition then
        failwith label

let private equal label expected actual =
    require label (expected = actual)

let private collect () =
    GC.Collect ()
    GC.WaitForPendingFinalizers ()
    GC.Collect ()

let private run name action =
    Console.WriteLine ("BEGIN " + name)

    try
        action ()
        passed.Add name
        Console.WriteLine ("PASS " + name)
    with error ->
        failures.Add name
        Console.Error.WriteLine ("FAIL " + name + " " + error.ToString ())

let private editor (host: WebCoreHost) =
    let edit = host.Document.createElement ("div") :?> HTMLElement
    edit.setAttribute ("contenteditable", "true")
    edit.textContent <- "🙂 speling suffix"
    host.Document.body.appendChild (edit) |> ignore
    host.SetFocused (true)
    edit.focus ()
    host.Frame ()
    edit

let private wordRange (host: WebCoreHost) (edit: HTMLElement) finish =
    let range = host.Document.createRange ()
    range.setStart (edit.firstChild, 3u)
    range.setEnd (edit.firstChild, finish)
    range

let private replacementUndo () =
    for replacement in [ "spelling"; "a longer correction"; "correct🙂" ] do
        use host = WebCoreHost.Create (400, 180)
        let edit = editor host
        let range = wordRange host edit 10u
        require "range-replacement capability" (WebCoreHost.Has ("range-replacement"))
        require "correction applied" (DataTransfers.replaceRange host range replacement)
        equal "exact UTF16 prefix/suffix" ("🙂 " + replacement + " suffix") edit.textContent
        require "one correction undo" (host.Undo ())
        equal "undo restores original word" "🙂 speling suffix" edit.textContent
        require "no extra correction history" (not (host.Undo ()))
        require "correction redo" (host.Redo ())
        equal "redo restores correction" ("🙂 " + replacement + " suffix") edit.textContent

let private cancellation () =
    use host = WebCoreHost.Create (400, 180)
    let edit = editor host
    let range = wordRange host edit 10u
    let events = ResizeArray<string>()

    let before (event: Event) =
        let input = event :?> InputEvent
        equal "typed inputType" "insertReplacementText" input.inputType
        events.Add "beforeinput"
        event.preventDefault ()

    let after (_: Event) =
        events.Add "input"

    edit.addEventListener ("beforeinput", before)
    edit.addEventListener ("input", after)
    require "cancelled correction unapplied" (not (DataTransfers.replaceRange host range "spelling"))
    equal "cancelled correction text" "🙂 speling suffix" edit.textContent
    equal "no input after cancellation" [| "beforeinput" |] (events.ToArray ())
    require "cancelled correction no undo" (not (host.Undo ()))
    edit.removeEventListener ("beforeinput", before)
    edit.removeEventListener ("input", after)
    require "removed cancellation no longer runs" (DataTransfers.replaceRange host range "spelling")
    equal "listener removal preserves old log" 1 events.Count

let private composition () =
    use host = WebCoreHost.Create (400, 180)
    let edit = editor host
    let selection = host.Document.getSelection ()
    selection.removeAllRanges ()
    selection.addRange (wordRange host edit 10u)
    let mutable ends = 0
    edit.addEventListener ("compositionend", fun (_: Event) -> ends <- ends + 1)
    host.SetComposition ("spel", 4, 4)
    equal "preedit installed" "🙂 spel suffix" edit.textContent
    let mutable refused = false

    try
        DataTransfers.replaceRange host (wordRange host edit 7u) "spelling"
        |> ignore
    with :? NotSupportedException ->
        refused <- true

    require "active composition correction explicitly unavailable" refused
    equal "preedit retained" "🙂 spel suffix" edit.textContent
    equal "correction did not end composition" 0 ends
    host.CommitComposition ("speling")
    equal "composition ends once" 1 ends
    require "correction applies after composition" (DataTransfers.replaceRange host (wordRange host edit 10u) "spelling")

let private ownership () =
    use host = WebCoreHost.Create (400, 180)
    use other = WebCoreHost.Create (400, 180)
    let edit = editor host
    let foreign = editor other
    let mutable wrongDocument = false

    try
        DataTransfers.replaceRange host (wordRange other foreign 10u) "spelling"
        |> ignore
    with :? ArgumentException as error ->
        wrongDocument <- error.ParamName = "range"

    require "foreign range rejected" wrongDocument
    equal "foreign text intact" "🙂 speling suffix" foreign.textContent
    equal "owner text intact" "🙂 speling suffix" edit.textContent
    (other :> IDisposable).Dispose()
    let mutable threadError: Exception = null
    let target = wordRange host edit 10u

    let worker =
        Thread (fun () ->
            try
                DataTransfers.replaceRange host target "spelling"
                |> ignore
            with error ->
                threadError <- error)

    worker.Start ()
    require "wrong thread returned" (worker.Join (5000))
    require "wrong thread rejected" (threadError :? InvalidOperationException)
    equal "wrong thread did not edit" "🙂 speling suffix" edit.textContent
    (host :> IDisposable).Dispose()
    let mutable disposed = false

    try
        DataTransfers.replaceRange host target "spelling"
        |> ignore
    with :? ObjectDisposedException ->
        disposed <- true

    require "disposed owner rejected before native call" disposed

let private callbackErrors () =
    use host = WebCoreHost.Create (400, 180)
    let edit = editor host
    let marker = InvalidOperationException ("managed spelling callback marker")
    edit.addEventListener ("beforeinput", fun (_: Event) -> raise marker)

    DataTransfers.replaceRange host (wordRange host edit 10u) "spelling"
    |> ignore

    require
        "original managed callback exception retained"
        (host.Errors
         |> Seq.exists (fun error -> obj.ReferenceEquals (error, marker)))

    require "original callback stack retained" (not (String.IsNullOrWhiteSpace marker.StackTrace))

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private installRoot eventName (edit: HTMLElement) (calls: int ref) =
    let retained = obj ()

    let callback (_: Event) =
        calls.Value <- calls.Value + 1
        GC.KeepAlive (retained)

    edit.addEventListener (eventName, callback)
    WeakReference (callback)

let private teardown () =
    use host = WebCoreHost.Create (400, 180)
    let edit = editor host
    let calls = ref 0
    let weak = installRoot "beforeinput" edit calls
    collect ()
    require "live native listener roots managed callback" weak.IsAlive

    DataTransfers.replaceRange host (wordRange host edit 10u) "spelling"
    |> ignore

    equal "managed listener called once" 1 calls.Value
    (host :> IDisposable).Dispose()
    (host :> IDisposable).Dispose()
    collect ()
    require "host teardown releases callback root" (not weak.IsAlive)

let private reentrantDestroy () =
    use host = WebCoreHost.Create (400, 180)
    let edit = editor host
    let mutable entered = 0
    let mutable inputRefused = false

    edit.addEventListener (
        "beforeinput",
        fun (_: Event) ->
            entered <- entered + 1
            (host :> IDisposable).Dispose()

            try
                host.Click (10, 10) |> ignore
            with :? ObjectDisposedException ->
                inputRefused <- true
    )

    DataTransfers.replaceRange host (wordRange host edit 10u) "spelling"
    |> ignore

    equal "destroy callback ran once" 1 entered
    require "input during disposing callback rejected" inputRefused
    (host :> IDisposable).Dispose()

let private pointerSample kind id action x y pressure =
    let mutable sample = PointerSample.Create (kind, id, action, x, y)
    sample.Button <- if action = 1 || action = 2 then 0 else -1
    sample.Buttons <- if action = 2 || action = 3 then 0 else 1
    sample.Pressure <- pressure
    sample

let private pointerFallback () =
    use host = WebCoreHost.Create (320, 240)
    require "typed pointer input supported" (WebCoreHost.Has ("pointer-input"))
    let pad = host.Document.createElement ("div") :?> HTMLElement
    pad.setAttribute ("style", "position:absolute;left:0;top:0;width:180px;height:180px;touch-action:none;background:green")
    host.Document.body.appendChild (pad) |> ignore
    host.SetFocused (true)
    host.Frame ()
    let positions = Dictionary<int, float * float>()
    let phases = ResizeArray<string>()
    let pressures = ResizeArray<float32>()
    let mutable capture = -1
    let mutable cancelled = false

    let down (event: Event) =
        let pointer = event :?> PointerEvent
        positions[pointer.pointerId] <- pointer.clientX, pointer.clientY
        capture <- pointer.pointerId
        pad.setPointerCapture (pointer.pointerId)
        phases.Add "begin"

    let move (event: Event) =
        let pointer = event :?> PointerEvent

        if positions.ContainsKey (pointer.pointerId) then
            positions[pointer.pointerId] <- pointer.clientX, pointer.clientY
            pressures.Add pointer.pressure
            phases.Add "update"

    let cancel (event: Event) =
        let pointer = event :?> PointerEvent
        positions.Remove (pointer.pointerId) |> ignore
        cancelled <- true
        phases.Add "cancel"

    let up (event: Event) =
        let pointer = event :?> PointerEvent
        positions.Remove (pointer.pointerId) |> ignore
        phases.Add "end"

    pad.addEventListener ("pointerdown", down)
    pad.addEventListener ("pointermove", move)
    pad.addEventListener ("pointercancel", cancel)
    pad.addEventListener ("pointerup", up)

    host.Pointer ([| pointerSample 1 7 1 20.0 20.0 0.25f |])
    |> ignore

    require "pen begins capture" (capture >= 0 && pad.hasPointerCapture (capture))

    host.Pointer ([| pointerSample 1 7 0 220.0 30.0 0.75f |])
    |> ignore

    require "captured pointer reaches pad outside bounds" (positions[capture] = (220.0, 30.0))
    equal "injected pressure delivered exactly" 0.75f pressures[pressures.Count - 1]

    host.Pointer ([| pointerSample 1 7 3 220.0 30.0 0.0f |])
    |> ignore

    Console.WriteLine (
        "PEN_CANCEL_DIAGNOSTIC cancelled="
        + string cancelled
        + " contacts="
        + string positions.Count
        + " phases="
        + String.Join (",", phases)
        + " capture="
        + string capture
    )

    require "withdrawn pen ends its native release path" (not cancelled && positions.Count = 0)
    require "capture released after cancel" (not (pad.hasPointerCapture (capture)))
    equal "pen release phases" [| "begin"; "update"; "end" |] (phases.ToArray ())
    pad.removeEventListener ("pointerdown", down)
    pad.removeEventListener ("pointermove", move)
    pad.removeEventListener ("pointercancel", cancel)
    pad.removeEventListener ("pointerup", up)
    let mutable mousePressure = -1.0f

    let mouse (_event: Event) =
        mousePressure <- (_event :?> PointerEvent).pressure

    pad.addEventListener ("pointerdown", mouse)

    host.Pointer ([| pointerSample 0 1 1 25.0 25.0 Single.NaN |])
    |> ignore

    equal "device without force uses specified pressed-pointer fallback" 0.5f mousePressure

    host.Pointer ([| pointerSample 0 1 2 25.0 25.0 Single.NaN |])
    |> ignore

    pad.removeEventListener ("pointerdown", mouse)
    require "touch fallback supported" (WebCoreHost.Has ("touch-input"))
    host.SetMaxTouchPoints (2)
    positions.Clear ()
    phases.Clear ()
    cancelled <- false
    pad.addEventListener ("pointerdown", down)
    pad.addEventListener ("pointermove", move)
    pad.addEventListener ("pointercancel", cancel)

    host.Pointer ([| pointerSample 2 11 1 20.0 50.0 0.0f; pointerSample 2 12 1 60.0 50.0 0.0f |])
    |> ignore

    equal "two active gesture contacts" 2 positions.Count

    let initial =
        positions
        |> Seq.map (fun pair -> pair.Key, pair.Value)
        |> dict

    host.Pointer ([| pointerSample 2 11 0 10.0 50.0 0.0f; pointerSample 2 12 0 90.0 50.0 0.0f |])
    |> ignore

    let ids = positions.Keys |> Seq.sort |> Seq.toArray

    let distance (ax, ay) (bx, by) =
        sqrt ((ax - bx) * (ax - bx) + (ay - by) * (ay - by))

    let ratio =
        distance positions[ids[0]] positions[ids[1]]
        / distance initial[ids[0]] initial[ids[1]]

    equal "application pinch ratio from native CSS coordinates" 2.0 ratio

    host.Pointer ([| pointerSample 2 11 3 10.0 50.0 0.0f; pointerSample 2 12 3 90.0 50.0 0.0f |])
    |> ignore

    require "pinch cancellation releases all application contacts" (cancelled && positions.Count = 0)

    require
        "pinch cancellation releases both captures"
        (ids
         |> Array.forall (fun id -> not (pad.hasPointerCapture (id))))

    pad.removeEventListener ("pointerdown", down)
    pad.removeEventListener ("pointermove", move)
    pad.removeEventListener ("pointercancel", cancel)
    let mutable malformed = pointerSample 1 9 1 20.0 20.0 0.5f
    malformed.Size <- 0
    let mutable refused = false

    try
        host.Pointer ([| malformed |]) |> ignore
    with :? NotSupportedException ->
        refused <- true

    require "malformed input rejected" refused
    Console.WriteLine ("FORCE_AVAILABILITY physical-device=unqualified injected-pressure=qualified fallback=mouse-0.5")

let private windowOwnership () =
    use window =
        Ranvier.View.WebCore.Sdl.Window.Create ("Public gesture ownership", 320, 240, hidden = true, renderer = "software")

    let host = window.Host
    let pad = host.Document.createElement ("div") :?> HTMLElement
    pad.setAttribute ("style", "position:absolute;left:0;top:0;width:180px;height:180px;touch-action:none")
    host.Document.body.appendChild (pad) |> ignore
    host.SetFocused (true)
    host.Frame ()
    let calls = ref 0
    let weak = installRoot "pointerdown" pad calls

    let capture (event: Event) =
        pad.setPointerCapture ((event :?> PointerEvent).pointerId)

    pad.addEventListener ("pointerdown", capture)
    Console.WriteLine ("WINDOW_BEFORE_DOWN")

    host.Pointer ([| pointerSample 1 3 1 20.0 20.0 0.5f |])
    |> ignore

    Console.WriteLine ("WINDOW_AFTER_DOWN")
    equal "owned Window receives native pointer" 1 calls.Value
    require "active pointer callback rooted" weak.IsAlive
    window.Present ()
    Console.WriteLine ("WINDOW_AFTER_PRESENT")
    (host :> IDisposable).Dispose()
    Console.WriteLine ("WINDOW_AFTER_DIRECT_HOST_DISPOSE")
    (window :> IDisposable).Dispose()
    Console.WriteLine ("WINDOW_AFTER_WINDOW_DISPOSE")
    (window :> IDisposable).Dispose()
    collect ()
    require "Window/direct Host teardown releases pointer callback" (not weak.IsAlive)
    let mutable refused = false
    Console.WriteLine ("WINDOW_BEFORE_POST_DISPOSE_INPUT")

    try
        host.Pointer ([| pointerSample 1 3 0 25.0 20.0 0.5f |])
        |> ignore
    with :? ObjectDisposedException ->
        refused <- true

    require "post Window teardown input rejected" refused

let private inputAdmission () =
    use host = WebCoreHost.Create (320, 240)

    let operations =
        [
            "mouse", fun () -> host.Mouse (0, 10, 10, -1, 0, 0) |> ignore
            "click", fun () -> host.Click (10, 10) |> ignore
            "wheel", fun () -> host.Wheel (10, 10, 0.0f, 1.0f, 0) |> ignore
            "key",
            fun () ->
                host.Key (0, "a", "KeyA", null, 0, false)
                |> ignore
            "type", fun () -> host.Type ("a", "KeyA", "a")
            "pointer",
            fun () ->
                host.Pointer ([| pointerSample 0 1 0 10.0 10.0 0.0f |])
                |> ignore
        ]

    let mutable wrongThreadChecks = 0
    let mutable threadError: exn option = None

    let worker =
        Thread (
            ThreadStart (fun () ->
                try
                    for name, operation in operations do
                        let mutable rejected = false

                        try
                            operation ()
                        with :? InvalidOperationException ->
                            rejected <- true

                        require (name + " rejects wrong owner thread") rejected
                        wrongThreadChecks <- wrongThreadChecks + 1
                with error ->
                    threadError <- Some error)
        )

    worker.Start ()
    require "wrong-thread checks complete without hanging" (worker.Join (TimeSpan.FromSeconds (10.0)))

    match threadError with
    | Some error -> raise error
    | None -> ()

    equal "all input admissions checked before native access" 6 wrongThreadChecks
    host.Click (10, 10) |> ignore
    (host :> IDisposable).Dispose()

    for name, operation in operations do
        let mutable rejected = false

        try
            operation ()
        with :? ObjectDisposedException ->
            rejected <- true

        require (name + " rejects disposed host") rejected

    (host :> IDisposable).Dispose()

/// <summary>Runs the public managed spelling and pointer fallback contract against the package graph.</summary>
[<EntryPoint>]
let main _ =
    run "spelling exact UTF16 replacement undo redo" replacementUndo
    run "spelling typed beforeinput cancellation and listener removal" cancellation
    run "spelling refuses active IME composition" composition
    run "spelling range owner thread and disposed checks" ownership
    run "spelling callback exception identity and stack" callbackErrors
    run "spelling managed callback roots end with host" teardown
    run "spelling callback can dispose real host" reentrantDestroy
    run "gesture pointer fallback capture cancellation and pressure" pointerFallback
    run "gesture real Window active-contact ownership teardown" windowOwnership
    run "all input methods reject wrong thread and disposed host" inputAdmission

    Console.WriteLine (
        "PUBLIC_WRAPPERS passed="
        + passed.Count.ToString (Globalization.CultureInfo.InvariantCulture)
        + " failed="
        + failures.Count.ToString (Globalization.CultureInfo.InvariantCulture)
    )

    if failures.Count = 0 then 0 else 1
