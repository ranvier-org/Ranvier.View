module CapabilityNativeProof

open System
open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Ranvier.Dom
open Ranvier.View.WebCore

let mutable checks = 0

let expect condition message =
    checks <- checks + 1

    if not condition then
        failwith message

let get (value: Task<'T>) =
    value.GetAwaiter().GetResult()

let key category name =
    Ranvier.Dom.Capabilities.key category name

let observe (service: CapabilityService) category name =
    service.Observe (key category name, CancellationToken.None)
    |> get

let extensionKey = key CapabilityCategory.PlatformFacility "test-async-owner"

let contribution id =
    { Ranvier.Dom.Capabilities.unknown extensionKey id "fixture" "native lifecycle" "unobserved" with
        Implemented =
            {
                State = CapabilityState.Yes
                Reason = "Controlled async provider completion."
            }
    }

[<EntryPoint>]
let main _ =
    use host = WebCoreHost.Create (320, 240)
    use service = Ranvier.View.WebCore.Capabilities.forHost host
    let owner = Environment.CurrentManagedThreadId
    expect (service.KnownKeys().Length > 300) "Complete loaded native catalogue"
    let scripts = observe service CapabilityCategory.RendererSetting "scripts"
    expect (scripts.Value = Some (CapabilityValue.Boolean false)) "Scripts report actual host setting false"
    expect (scripts.Available.State = CapabilityState.Unknown) "Setting false does not manufacture availability"
    let scale = observe service CapabilityCategory.RendererSetting "device-scale"
    expect (scale.Value = Some (CapabilityValue.Number 1.)) "Actual initial device scale"
    let abi = observe service CapabilityCategory.Abi "dom:Document"

    expect
        (abi.Exported.State = CapabilityState.Yes
         && abi.Implemented.State = CapabilityState.Unknown)
        "ABI surface does not prove all document behavior"

    let unknown =
        observe service CapabilityCategory.PlatformFacility "unrecognized-future"

    expect
        (unknown.Exported.State = CapabilityState.Unknown
         && unknown.Implemented.State = CapabilityState.Unknown)
        "Unknown facility remains unknown"

    for category, name in
        [
            CapabilityCategory.ImageFormat, "image/png"
            CapabilityCategory.FontFormat, "woff2"
        ] do
        let report = observe service category name
        expect report.Value.IsSome "Actual engine format-support query returns a typed value"
        expect (report.Available.State = CapabilityState.Unknown) "Format query does not prove arbitrary decode/render"

    let pending =
        TaskCompletionSource<CapabilityObservation option>(TaskCreationOptions.RunContinuationsAsynchronously)

    let mutable callbackThread = -1

    use registration =
        service.Register
            {
                Id = "worker"
                KnownKeys = [| extensionKey |]
                Observe =
                    fun _ _ ->
                        callbackThread <- Environment.CurrentManagedThreadId
                        pending.Task
            }

    let previous = SynchronizationContext.Current
    SynchronizationContext.SetSynchronizationContext null
    let request = service.Observe (extensionKey, CancellationToken.None)
    SynchronizationContext.SetSynchronizationContext previous
    expect (callbackThread = owner) "Injected callbacks start on the owner thread"

    let workerThread =
        Task.Run (fun () ->
            pending.SetResult (Some (contribution "worker"))
            Environment.CurrentManagedThreadId)
        |> get

    expect (workerThread <> owner) "Provider really completed off the owner thread"
    expect (not request.IsCompleted) "Off-thread completion waits for explicit owner queue"
    let timeout = Stopwatch.StartNew ()

    while not request.IsCompleted
          && timeout.ElapsedMilliseconds < 5000L do
        host.Cycle ()
        Thread.Sleep 1

    expect request.IsCompleted "Owner Cycle publishes async result without captured synchronization context"
    expect ((get request).Implemented.State = CapabilityState.Yes) "Async contribution preserved through owner dispatch"
    registration.Dispose ()

    let ignored = TaskCompletionSource<CapabilityObservation option>()

    use ignoredRegistration =
        service.Register
            {
                Id = "ignored"
                KnownKeys = [| extensionKey |]
                Observe = fun _ _ -> ignored.Task
            }

    use cancellation = new CancellationTokenSource ()
    let cancelled = service.Observe (extensionKey, cancellation.Token)
    cancellation.Cancel ()
    expect cancelled.IsCanceled "Cancellation completes without host pumping or provider cooperation"
    ignored.SetResult (Some (contribution "ignored"))
    host.Cycle ()
    expect cancelled.IsCanceled "Late provider completion cannot publish cancelled report"
    ignoredRegistration.Dispose ()

    let wrongThread =
        Task.Run (fun () ->
            try
                service.KnownKeys () |> ignore
                false
            with :? InvalidOperationException ->
                true)
        |> get

    expect wrongThread "Native adapter rejects reads from another thread"

    let afterDispose = TaskCompletionSource<CapabilityObservation option>()

    use disposalRegistration =
        service.Register
            {
                Id = "disposed"
                KnownKeys = [| extensionKey |]
                Observe = fun _ _ -> afterDispose.Task
            }

    let disposalRequest = service.Observe (extensionKey, CancellationToken.None)
    (host :> IDisposable).Dispose()
    expect disposalRequest.IsCompleted "Host disposal completes pending report without further Cycle"

    try
        get disposalRequest |> ignore
        failwith "Expected disposal error"
    with CapabilityContextException _ ->
        checks <- checks + 1

    afterDispose.SetResult (Some (contribution "disposed"))
    expect disposalRequest.IsFaulted "Late provider cannot revive disposed report"
    Console.WriteLine $"CAPABILITY_NATIVE_LIFECYCLE_CHECKS={checks}"
    0
