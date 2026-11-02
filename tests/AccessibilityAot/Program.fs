module private AccessibilityAot

open System
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl

#if ATSPI_STYLE_TRIAL
let private checkStyle (host: WebCoreHost) =
    let html =
        System.Text.Encoding.UTF8.GetBytes "<p id='rich' aria-label='Rich'>A<span style='font-weight:700'>😀B</span>C</p>"

    if
        RanvierWebCoreAbi.Native.rv_host_load_html (host.Handle, html, html.Length)
        <> 0
    then
        invalidOp "The style trial failed to load its real native document."

    let identifier = System.Text.Encoding.UTF8.GetBytes "rich"

    let node =
        RanvierWebCoreAbi.Native.rv_document_get_element_by_id (host.Handle, identifier, identifier.Length)

    if node = 0n then
        invalidOp "The styled element is missing."

    try
        let accessible = RanvierWebCoreAbi.Native.rv_node_ax node

        if accessible = 0n then
            invalidOp "The styled accessible is missing."

        try
            let mutable start = -1
            let mutable finish = -1
            let length = [| -1 |]
            let bytes = Array.zeroCreate<byte> 16

            let status =
                RanvierWebCoreAbi.Native.rv_ax_text_style_run (accessible, 2, "font-weight", &start, &finish, bytes, bytes.Length, length)

            if
                status <> 0
                || start <> 1
                || finish <> 4
                || length[0] <> 3
                || System.Text.Encoding.UTF8.GetString (bytes, 0, length[0])
                   <> "700"
            then
                invalidOp "The actual native non-BMP style run differs from the frozen renderer contract."
        finally
            RanvierWebCoreAbi.Native.rv_ax_release accessible
    finally
        RanvierWebCoreAbi.Native.rv_node_release node
#endif

let private markup =
    "<svg xmlns='http://www.w3.org/2000/svg' width='80' height='60'><text aria-label='Accessible' x='1' y='20'>Accessible</text></svg>"

[<EntryPoint>]
let main arguments =
    let expectAttached = arguments |> Array.contains "--expect-attached"
    let mutable windows = 0
    let mutable hosts = 0

    for index in 1..3 do
        Console.WriteLine ("stage window-create " + string index)

        let window =
            Window.Create ("Accessibility ownership", 80, 60, hidden = true, renderer = "software")

        try
            Console.WriteLine ("stage load " + string index)
            window.Host.LoadSvg markup
            Console.WriteLine ("stage step " + string index)
            window.Step (0) |> ignore

            if window.AccessibilityAttached <> expectAttached then
                invalidOp "Optional accessibility attachment differs from the selected provider profile."

#if ATSPI_STYLE_TRIAL
            if expectAttached then
                checkStyle window.Host
#endif

            if index = 2 then
                Console.WriteLine ("stage direct-window-host-dispose " + string index)
                (window.Host :> IDisposable).Dispose()
        finally
            Console.WriteLine ("stage window-dispose " + string index)
            (window :> IDisposable).Dispose()
            Console.WriteLine ("stage window-dispose-repeat " + string index)
            (window :> IDisposable).Dispose()

        windows <- windows + 1

    for _ in 1..3 do
        Console.WriteLine ("stage direct-host-create")
        let host = WebCoreHost.Create (80, 60)
        host.LoadSvg markup
        Console.WriteLine ("stage direct-host-dispose")
        (host :> IDisposable).Dispose()
        (host :> IDisposable).Dispose()
        hosts <- hosts + 1

    Console.WriteLine (
        "{\"windows\":"
        + string windows
        + ",\"directHosts\":"
        + string hosts
        + ",\"expectedAttached\":"
        + (if expectAttached then "true" else "false")
        + ",\"dynamicCodeSupported\":"
        + (if System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
               "true"
           else
               "false")
        + "}"
    )

    if OperatingSystem.IsLinux () then
        for line in System.IO.File.ReadLines "/proc/self/maps" do
            if
                line.Contains ("libRanvierWebCoreHost.so", StringComparison.Ordinal)
                || line.Contains ("libRanvierWebCoreAtspi.so", StringComparison.Ordinal)
            then
                Console.WriteLine ("native-map " + line)

    0
