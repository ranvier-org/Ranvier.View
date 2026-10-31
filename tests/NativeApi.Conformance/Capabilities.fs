module internal NativeApiConformance.Capabilities

open System
open System.Threading
open System.Threading.Tasks
open Ranvier.Dom

/// <summary>Checks portable registry facts, snapshots, failures, cancellation and real provider extension boundaries.</summary>
let run (create: unit -> CapabilityService) =
    task {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

        let key =
            Ranvier.Dom.Capabilities.key CapabilityCategory.PlatformFacility "h07-fixture"

        let unknown id =
            Ranvier.Dom.Capabilities.unknown key id "fixture" "portable registry" "unobserved"

        let provider id observation =
            {
                Id = id
                KnownKeys = [| key |]
                Observe = fun _ _ -> Task.FromResult (Some observation)
            }

        let known id state =
            { unknown id with
                Implemented =
                    {
                        State = state
                        Reason = "fixture fact"
                    }
            }

        let read (service: CapabilityService) =
            service.Observe (key, CancellationToken.None)

        use empty = Ranvier.Dom.Capabilities.create ()
        let! emptyResult = read empty
        equal "empty unknown" CapabilityState.Unknown emptyResult.Implemented.State
        equal "empty provenance" 0 emptyResult.Observations.Length
        use yes = empty.Register (provider "yes" (known "yes" CapabilityState.Yes))
        let! onlyYes = read empty
        equal "known implementation" CapabilityState.Yes onlyYes.Implemented.State
        equal "exported remains independent" CapabilityState.Unknown onlyYes.Exported.State
        equal "enabled remains independent" CapabilityState.Unknown onlyYes.Enabled.State
        equal "permission remains independent" CapabilityState.Unknown onlyYes.Permitted.State
        equal "availability remains independent" CapabilityState.Unknown onlyYes.Available.State
        use no = empty.Register (provider "no" (known "no" CapabilityState.No))
        let! conflict = read empty
        equal "conflict becomes unknown" CapabilityState.Unknown conflict.Implemented.State
        equal "conflict evidence order" [| "yes"; "no" |] (conflict.Observations |> Array.map _.ProviderId)

        equal
            "conflict retains both facts"
            [| CapabilityState.Yes; CapabilityState.No |]
            (conflict.Observations
             |> Array.map _.Implemented.State)

        no.Dispose ()
        let! restored = read empty
        equal "registration removal" CapabilityState.Yes restored.Implemented.State

        try
            empty.Register (provider "yes" (unknown "yes"))
            |> ignore

            failwith "Expected duplicate rejection"
        with :? ArgumentException ->
            checks <- checks + 1

        let keys = empty.KnownKeys ()
        keys[0] <- Ranvier.Dom.Capabilities.key CapabilityCategory.Abi "changed"
        equal "known keys copied" [| key |] (empty.KnownKeys ())
        equal "MIME normalization" "image/png" (Ranvier.Dom.Capabilities.key CapabilityCategory.ImageFormat "IMAGE/PNG").Name
        equal "font normalization" "woff2" (Ranvier.Dom.Capabilities.key CapabilityCategory.FontFormat "WOFF2").Name
        equal "ABI case preserved" "dom:Window" (Ranvier.Dom.Capabilities.key CapabilityCategory.Abi "dom:Window").Name

        for category, name in
            [
                CapabilityCategory.Abi, ""
                CapabilityCategory.Abi, "x\000y"
                CapabilityCategory.FontFormat, "woff)or(font-format(woff2"
                CapabilityCategory.ImageFormat, "image/png;bad"
            ] do
            try
                Ranvier.Dom.Capabilities.key category name
                |> ignore

                failwith "Expected malformed key rejection"
            with :? ArgumentException ->
                checks <- checks + 1

        for value in
            [
                None
                Some (CapabilityValue.Boolean false)
                Some (CapabilityValue.Number 0.)
                Some (CapabilityValue.Text "")
            ] do
            use domain = Ranvier.Dom.Capabilities.create ()

            use registration =
                domain.Register (provider "value" { unknown "value" with Value = value })

            let! report = read domain
            equal "absent/false/zero/empty remain distinct" value report.Value

        let names = [| "original" |]
        use copied = Ranvier.Dom.Capabilities.create ()

        use namesRegistration =
            copied.Register (
                provider
                    "names"
                    { unknown "names" with
                        Value = Some (CapabilityValue.Names names)
                    }
            )

        let! snapshot = read copied
        names[0] <- "changed"
        equal "provider array copied" (Some (CapabilityValue.Names [| "original" |])) snapshot.Value

        match snapshot.Value with
        | Some (CapabilityValue.Names array) -> array[0] <- "caller change"
        | _ -> failwith "Expected Names"

        equal "report array separate from provenance" (Some (CapabilityValue.Names [| "original" |])) snapshot.Observations[0].Value
        use failed = Ranvier.Dom.Capabilities.create ()

        use failureRegistration =
            failed.Register
                {
                    Id = "failure"
                    KnownKeys = [| key |]
                    Observe = fun _ _ -> failwith "provider failure"
                }

        let! failure = read failed
        equal "provider failure stays unknown" CapabilityState.Unknown failure.Implemented.State
        equal "provider failure provenance" true (failure.Observations[0].Implemented.Reason.Contains "provider failure")
        use context = Ranvier.Dom.Capabilities.create ()

        use contextRegistration =
            context.Register
                {
                    Id = "context"
                    KnownKeys = [| key |]
                    Observe = fun _ _ -> raise (CapabilityContextException "closed target")
                }

        try
            let! _ = read context
            failwith "Expected context error"
        with CapabilityContextException _ ->
            checks <- checks + 1

        use cancellation = new CancellationTokenSource ()
        cancellation.Cancel ()

        try
            let! _ = empty.Observe (key, cancellation.Token)
            failwith "Expected pre-cancellation"
        with :? OperationCanceledException ->
            checks <- checks + 1

        use pendingService = Ranvier.Dom.Capabilities.create ()
        let pending = TaskCompletionSource<CapabilityObservation option>()

        use pendingRegistration =
            pendingService.Register
                {
                    Id = "pending"
                    KnownKeys = [| key |]
                    Observe = fun _ _ -> pending.Task
                }

        use during = new CancellationTokenSource ()
        let request = pendingService.Observe (key, during.Token)
        during.Cancel ()

        try
            let! _ = request
            failwith "Expected in-flight cancellation"
        with :? OperationCanceledException ->
            checks <- checks + 1

        pending.SetResult (Some (known "pending" CapabilityState.Yes))
        do! Task.Delay 1

        try
            let! _ = request
            failwith "Expected persistent cancellation"
        with :? OperationCanceledException ->
            checks <- checks + 1

        use disposedService = Ranvier.Dom.Capabilities.create ()
        let late = TaskCompletionSource<CapabilityObservation option>()

        use lateRegistration =
            disposedService.Register
                {
                    Id = "late"
                    KnownKeys = [| key |]
                    Observe = fun _ _ -> late.Task
                }

        let disposedRequest = read disposedService
        disposedService.Dispose ()

        try
            let! _ = disposedRequest
            failwith "Expected pending disposal"
        with CapabilityContextException _ ->
            checks <- checks + 1

        late.SetResult (Some (known "late" CapabilityState.Yes))
        do! Task.Delay 1

        try
            let! _ = disposedRequest
            failwith "Expected persistent disposal"
        with CapabilityContextException _ ->
            checks <- checks + 1

        use service = create ()
        use peer = create ()
        let! unrecognized = read service
        equal "actual provider unknown implementation" CapabilityState.Unknown unrecognized.Implemented.State
        equal "actual provider unknown availability" CapabilityState.Unknown unrecognized.Available.State

        use custom =
            service.Register (provider "extension" (known "extension" CapabilityState.Yes))

        let! extended = read service
        equal "actual provider extension" CapabilityState.Yes extended.Implemented.State
        equal "extension does not claim readiness" CapabilityState.Unknown extended.Available.State
        let! independent = read peer
        equal "registrations belong to their service" CapabilityState.Unknown independent.Implemented.State

        for category, name in [ CapabilityCategory.Abi, "x\000y"; CapabilityCategory.FontFormat, "woff)bad" ] do
            try
                let! _ =
                    service.Observe ({ Category = category; Name = name }, CancellationToken.None)

                failwith "Expected input rejection"
            with :? ArgumentException ->
                checks <- checks + 1

        try
            let! _ = service.Observe (Unchecked.defaultof<CapabilityKey>, CancellationToken.None)
            failwith "Expected null key rejection"
        with :? ArgumentNullException ->
            checks <- checks + 1

        return checks
    }
