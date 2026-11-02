module AotPicker

open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Text
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

[<DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)>]
extern nativeint createWindow(
    uint32 exStyle,
    string className,
    string title,
    uint32 style,
    int x,
    int y,
    int width,
    int height,
    nativeint parent,
    nativeint menu,
    nativeint instance,
    nativeint parameter
)

[<DllImport("user32.dll")>]
extern int DestroyWindow(nativeint window)

[<Struct; StructLayout(LayoutKind.Sequential)>]
type Message =
    val mutable Window: nativeint
    val mutable Kind: uint32
    val mutable WParam: nativeint
    val mutable LParam: nativeint
    val mutable Time: uint32
    val mutable X: int32
    val mutable Y: int32
    val mutable Private: uint32

[<DllImport("user32.dll", EntryPoint = "PeekMessageW")>]
extern int peekMessage(Message& message, nativeint window, uint32 first, uint32 last, uint32 remove)

[<DllImport("user32.dll")>]
extern int TranslateMessage(Message& message)

[<DllImport("user32.dll", EntryPoint = "DispatchMessageW")>]
extern nativeint dispatchMessage(Message& message)

let pump () =
    let mutable message = Message ()

    while peekMessage (&message, 0n, 0u, 0u, 1u) <> 0 do
        TranslateMessage &message |> ignore
        dispatchMessage &message |> ignore

let require value message =
    if not value then
        failwith message

let click (host: WebCoreHost) =
    host.Cycle ()
    host.Frame ()
    host.Click (20, 20) |> ignore
    host.Cycle ()

let custom () =
    use host = WebCoreHost.Create (320, 240)
    let events = ResizeArray<FilePickerEvent>()
    use provider = FilePickers.install host events.Add
    let input = host.Document.createElement "input" :?> HTMLInputElement
    input.setAttribute ("type", "file")
    input.setAttribute ("accept", ".txt,image/png")
    input.setAttribute ("multiple", "")
    input.setAttribute ("style", "position:absolute;left:0;top:0;width:100px;height:40px")
    host.Document.body.appendChild input |> ignore
    click host

    let request =
        match events[0] with
        | Show value -> value
        | _ -> failwith "no picker request"

    require
        (request.Multiple
         && request.Extensions = [| ".txt" |]
         && request.MimeTypes = [| "image/png" |])
        "picker request fields"

    if not System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported then
        let rendered = events[0].ToString()

        require (rendered = "Show Ranvier.View.WebCore.FilePickerRequest") "typed picker event diagnostics"

        require ((Invalidate UInt64.MaxValue).ToString().Contains "18446744073709551615UL") "picker request id diagnostic"
        require ((Show Unchecked.defaultof<FilePickerRequest>).ToString().Contains "null") "null picker request diagnostic"

    let path = Path.GetTempFileName ()

    try
        require (request.Complete (Selected [| path |])) "picker selection"
        require (input.files.length = 1u) "selected file"
        require (not (request.Complete Cancelled)) "terminal completion is once"
        click host

        let pending =
            match events[events.Count - 1] with
            | Show value -> value
            | _ -> failwith "no cancellation request"

        require (pending.Complete Cancelled) "picker cancellation"
        require (input.files.length = 1u) "cancellation preserves selection"
    finally
        File.Delete path

    Console.WriteLine "CUSTOM_PICKER_PASS"

[<UnmanagedFunctionPointer(CallingConvention.Winapi)>]
type EnumWindowCallback = delegate of nativeint * nativeint -> bool

[<DllImport("user32.dll")>]
extern bool EnumWindows(EnumWindowCallback callback, nativeint state)

[<DllImport("user32.dll")>]
extern bool EnumChildWindows(nativeint parent, EnumWindowCallback callback, nativeint state)

[<DllImport("user32.dll")>]
extern uint32 GetWindowThreadProcessId(nativeint window, uint32& processId)

[<DllImport("user32.dll")>]
extern nativeint GetWindow(nativeint window, uint32 command)

[<DllImport("user32.dll")>]
extern nativeint GetParent(nativeint window)

[<DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)>]
extern int getClassName(nativeint window, StringBuilder name, int capacity)

[<DllImport("user32.dll")>]
extern int GetDlgCtrlID(nativeint window)

[<DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)>]
extern nativeint sendText(nativeint window, uint32 message, nativeint wParam, string text)

[<DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)>]
extern int getWindowText(nativeint window, StringBuilder text, int capacity)

[<DllImport("user32.dll")>]
extern bool PostMessageW(nativeint window, uint32 message, nativeint wParam, nativeint lParam)

let findDialog owner =
    let mutable found = 0n

    let callback =
        EnumWindowCallback (fun window _ ->
            let mutable pid = 0u
            GetWindowThreadProcessId (window, &pid) |> ignore
            let name = StringBuilder 128

            getClassName (window, name, name.Capacity)
            |> ignore

            if
                pid = uint32 Environment.ProcessId
                && name.ToString () = "#32770"
                && GetWindow (window, 4u) = owner
            then
                found <- window

            true)

    EnumWindows (callback, 0n) |> ignore
    GC.KeepAlive callback
    found

let driveDialog owner path accept =
    let deadline = Stopwatch.StartNew ()
    let mutable dialog = 0n

    while dialog = 0n
          && deadline.ElapsedMilliseconds < 20000L do
        dialog <- findDialog owner

        if dialog = 0n then
            Thread.Sleep 20

    require (dialog <> 0n) "system file dialog did not appear"

    Console.WriteLine (
        if accept then
            "DIALOG_ACCEPT_OPEN"
        else
            "DIALOG_CANCEL_OPEN"
    )

    if accept then
        let mutable edit = 0n
        let mutable button = 0n

        let callback =
            EnumWindowCallback (fun child _ ->
                let name = StringBuilder 128

                getClassName (child, name, name.Capacity)
                |> ignore

                let mutable ancestor = GetParent child
                let mutable filename = false

                while ancestor <> 0n && ancestor <> dialog do
                    if GetDlgCtrlID ancestor = 0x47c then
                        filename <- true

                    ancestor <- GetParent ancestor

                if filename && name.ToString () = "Edit" then
                    edit <- child

                if GetDlgCtrlID child = 1 then
                    button <- child

                true)

        while (edit = 0n || button = 0n)
              && deadline.ElapsedMilliseconds < 20000L do
            EnumChildWindows (dialog, callback, 0n) |> ignore

            if edit = 0n then
                Thread.Sleep 20

        GC.KeepAlive callback
        require (edit <> 0n && button <> 0n) "system file-name edit or accept button not found"
        Thread.Sleep 1000
        sendText (edit, 0x000Cu, 0n, path) |> ignore
        Thread.Sleep 500
        let value = StringBuilder 1024

        getWindowText (edit, value, value.Capacity)
        |> ignore

        require (value.ToString () = path) "system dialog filename readback"
        Console.WriteLine "DIALOG_FILENAME_SET"
        require (PostMessageW (button, 0x00F5u, 0n, 0n)) "system dialog accept"
    else
        require (PostMessageW (dialog, 0x0111u, 2n, 0n)) "system dialog cancel"

let system () =
    let owner =
        createWindow (0u, "STATIC", "Ranvier AOT picker", 0x10CF0000u, 100, 100, 480, 320, 0n, 0n, 0n, 0n)

    require (owner <> 0n) "owner window"
    let path = Path.GetTempFileName ()

    try
        use host = WebCoreHost.Create (320, 240)
        use provider = WindowsFilePickers.install host owner
        let input = host.Document.createElement "input" :?> HTMLInputElement
        input.setAttribute ("type", "file")
        input.setAttribute ("style", "position:absolute;left:0;top:0;width:100px;height:40px")
        host.Document.body.appendChild input |> ignore
        let mutable cancellations = 0
        input.addEventListener ("cancel", (fun (_: Event) -> cancellations <- cancellations + 1), AddEventListenerOptions ())

        let runDialog accept completed =
            Console.WriteLine (
                if accept then
                    "SYSTEM_SELECT_START"
                else
                    "SYSTEM_CANCEL_START"
            )

            let mutable failure: exn option = None

            let worker =
                Thread (
                    ThreadStart (fun () ->
                        try
                            driveDialog owner path accept
                        with e ->
                            failure <- Some e)
                )

            worker.IsBackground <- true
            worker.Start ()
            click host
            let deadline = Stopwatch.StartNew ()

            while not (completed ())
                  && failure.IsNone
                  && host.Errors.Count = 0
                  && deadline.ElapsedMilliseconds < 25000L do
                pump ()
                host.Cycle ()
                Thread.Sleep 20

            require (worker.Join 1000) "dialog automation thread"

            match failure with
            | Some error -> raise error
            | None -> ()

            if host.Errors.Count > 0 then
                raise host.Errors[0]

            require
                (completed ())
                ("system picker completion (cancellations="
                 + cancellations.ToString ()
                 + ")")

        runDialog true (fun () -> input.files.length = 1u)
        require ((input.files.item 0u).name = Path.GetFileName path) "system-selected filename"
        runDialog false (fun () -> cancellations = 1)
        require (input.files.length = 1u && host.Errors.Count = 0) "system cancellation preserves file"
        Console.WriteLine "SYSTEM_PICKER_PASS"
    finally
        File.Delete path
        DestroyWindow owner |> ignore

[<EntryPoint>]
let main _ =
    custom ()
    system ()
    0
