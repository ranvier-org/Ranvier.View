module UserTimingBrowser

open Fable.Core
open Ranvier.Dom

[<Emit("document")>]
let private document () : Document = jsNative

/// <summary>Runs the shared fixture against the page's actual Performance buffer.</summary>
let run () =
    let deterministic = NativeApiConformance.UserTiming.deterministic ()

    let shared =
        NativeApiConformance.UserTiming.run (fun () -> Timing.forDocument (document ()))

    if deterministic <> 179 || shared <> 178 then
        failwith $"Timing fixture counts changed: {deterministic}/{shared}"

    {|
        shared = shared
        deterministic = deterministic
    |}

/// <summary>Exposes real provider operations for page and iframe lifecycle assertions.</summary>
let provider (document: Document) =
    let service = Timing.forDocument document

    {|
        now = fun () -> service.Now ()
        origin = fun () -> service.TimeOrigin
        mark =
            fun name ->
                service.Mark (
                    name,
                    { TimingMarkOptions.Default with
                        Detail = TimingDetail.Text "facade"
                    }
                )
        entries = fun () -> service.GetEntries ()
        dispose = fun () -> service.Dispose ()
    |}
