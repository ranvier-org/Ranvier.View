module internal NativeApiConformance.Native

open Ranvier.View.WebCore

[<EntryPoint>]
let main _ =
    let host = WebCoreHost.Create (320, 240)
    let session = host.Session

    try
        let viewTables = ViewTables.run host.Document
        System.Console.WriteLine $"NATIVE_API D10_VIEW PASS assertions={viewTables}"
        let cssEscaping = CssEscaping.run host.Document
        System.Console.WriteLine $"NATIVE_API C05 PASS assertions={cssEscaping}"
        let checks = Tables.run host.Document
        System.Console.WriteLine $"NATIVE_API D10 PASS assertions={checks}"
        let hyperlinks = Hyperlinks.run host.Document
        System.Console.WriteLine $"NATIVE_API HYPERLINKS PASS assertions={hyperlinks}"
        let reflection = Reflection.run host.Document
        System.Console.WriteLine $"NATIVE_API D11 PASS assertions={reflection}"
        let namespaces = Namespaces.run host.Document (fun () -> host.Frame ())
        System.Console.WriteLine $"NATIVE_API D09 PASS assertions={namespaces}"
        let svgStylesheets = Stylesheets.runSvg host.Document
        System.Console.WriteLine $"NATIVE_API SVG_CSSOM PASS assertions={svgStylesheets}"
        let stylesheets = Stylesheets.run host.Document
        System.Console.WriteLine $"NATIVE_API CSSOM PASS assertions={stylesheets}"

        let ownerContext = System.Threading.SynchronizationContext.Current

        let checkpoint () =
            Async.FromContinuations (fun (success, _, _) ->
                host.QueueMicrotask (fun () -> ownerContext.Post (System.Threading.SendOrPostCallback (fun _ -> success ()), null)))

        let computed (element: Ranvier.Dom.HTMLElement) property =
            let buffer = Array.zeroCreate<byte> 4096
            let length = [| 0 |]
            let handle = (box element :?> NativeObject).Handle

            let status =
                RanvierWebCoreAbi.Native.rv_element_computed_style (handle, property, buffer, buffer.Length, length)

            if status <> 0 then
                failwith $"Computed style returned {status}"

            System.Text.Encoding.UTF8.GetString (buffer, 0, length[0])

        let mutable construction: Result<int, exn> option = None

        Async.StartImmediate (
            async {
                try
                    let! checks =
                        SheetConstruction.run host.Document (fun () -> StyleSheets.forHost host) checkpoint computed

                    construction <- Some (Ok checks)
                with error ->
                    construction <- Some (Error error)
            }
        )

        let deadline = System.Diagnostics.Stopwatch.StartNew ()

        while construction.IsNone
              && deadline.Elapsed.TotalSeconds < 20.0 do
            host.Cycle 1

        match construction with
        | Some (Ok checks) -> System.Console.WriteLine $"NATIVE_API C07 PASS assertions={checks}"
        | Some (Error error) -> raise error
        | None -> failwith "Stylesheet construction fixture did not complete within 20 seconds"

        let svg =
            host.Document.getElementById ("native-api-svg") :?> Ranvier.Dom.HTMLOrSVGElement

        svg.focus ()

        if not (host.Key (0, "Tab", "Tab", "", 0, false)) then
            failwith "Native Tab traversal was not handled"

        let math = host.Document.getElementById ("native-api-math")

        if not (host.Document.activeElement.isSameNode (math)) then
            failwith "Native Tab did not move from SVG to MathML"

        System.Console.WriteLine "NATIVE_API D09 TAB PASS"

        let lifetimeService = StyleSheets.forHost host
        let lifetimeSheet = lifetimeService.Create Ranvier.Dom.StyleSheetOptions.Default
        let mutable wrongThreadRejected = false

        let worker =
            System.Threading.Thread (
                System.Threading.ThreadStart (fun () ->
                    Async.StartWithContinuations (
                        lifetimeService.ReplaceAsync (lifetimeSheet, ".wrong-thread {}"),
                        ignore,
                        (fun error -> wrongThreadRejected <- error :? System.InvalidOperationException),
                        ignore
                    ))
            )

        worker.Start ()
        worker.Join ()

        if not wrongThreadRejected then
            failwith "Stylesheet replacement was not rejected off the owning thread"

        let mutable terminal = ""

        Async.StartWithContinuations (
            lifetimeService.ReplaceAsync (lifetimeSheet, ".before-host-dispose {}"),
            (fun _ -> terminal <- "success"),
            (fun _ -> terminal <- "error"),
            (fun _ -> terminal <- "cancel")
        )

        if terminal <> "" then
            failwith "Stylesheet replacement completed without a checkpoint"

        (host :> System.IDisposable).Dispose()

        if terminal <> "cancel" then
            failwith $"Host disposal did not terminate pending replacement: {terminal}"

        if (box lifetimeSheet :?> NativeObject).Handle <> 0n then
            failwith "Disposed host retained a constructed stylesheet handle"

        System.Console.WriteLine "NATIVE_API C07 LIFETIME PASS assertions=4"
    finally
        (host :> System.IDisposable).Dispose()

    if session.LiveObjects <> 0 then
        failwith $"Shared API fixtures leaked {session.LiveObjects} managed native objects"

    System.Console.WriteLine "NATIVE_API TEARDOWN PASS groups=D09,D10,D11,CSSOM"
    0
