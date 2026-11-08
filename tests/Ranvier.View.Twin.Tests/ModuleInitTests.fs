module Ranvier.View.Twin.Tests.ModuleInitTests

open System
open System.Runtime.CompilerServices
open Expecto
open Ranvier.View
open Ranvier.View.Dom

let private flags =
    Reflection.BindingFlags.Static
    ||| Reflection.BindingFlags.NonPublic
    ||| Reflection.BindingFlags.Public
    ||| Reflection.BindingFlags.DeclaredOnly

let private startupTypes (assembly: Reflection.Assembly) =
    assembly.GetTypes ()
    |> Array.filter (fun t -> t.FullName.StartsWith "<StartupCode$")

let private recordingHost (posted: ResizeArray<unit -> unit>) =
    { new Native.IHost with
        member _.Document = Unchecked.defaultof<_>

        member _.QueueMicrotask callback =
            posted.Add callback

        member _.SetTimeout(_, _) = null
        member _.ClearTimeout _ = ()
        member _.RequestAnimationFrame _ = null
        member _.CancelAnimationFrame _ = ()
        member _.DeleteBetween(_, _) = ()
        member _.Listen(_, _, _) = ignore
        member _.OverrideCurrentTarget(_, _) = ()
    }

[<Tests>]
let tests =
    testList
        "Module initialisation"
        [
            testCase "Ranvier.View and Ranvier.View.Dom initialise on .NET with no host"
            <| fun () ->
                for assembly in [ typeof<Scheduling.Queue>.Assembly; typeof<ScopePhase>.Assembly ] do
                    for t in startupTypes assembly do
                        try
                            RuntimeHelpers.RunClassConstructor t.TypeHandle
                        with :? TypeInitializationException as e ->
                            failtestf "%s failed to initialise: %s" t.FullName (e.GetBaseException ()).Message

            // F# guards a class's static values with an `init@` field set by its file's initializer. Two threads that
            // start the class and the file together deadlock, and the CLR lets one read the class uninitialized.
            testCase "no class in Ranvier.View or Ranvier.View.Dom has a static initializer of its own"
            <| fun () ->
                let guarded =
                    [ typeof<Scheduling.Queue>.Assembly; typeof<ScopePhase>.Assembly ]
                    |> List.collect (fun assembly -> List.ofArray (assembly.GetTypes ()))
                    |> List.filter (fun t ->
                        not (t.FullName.StartsWith "<StartupCode$")
                        && t.GetFields flags
                           |> Array.exists (fun f -> f.Name.StartsWith "init@"))
                    |> List.map (fun t -> t.FullName)

                Expect.isEmpty guarded "classes with an init@ field"

            testCase "a set created while another thread initialises Native.fs reads an initialised comparer"
            <| fun () ->
                let path = typeof<InstancePath>.Assembly.Location
                let failures = ResizeArray<string>()

                for _ in 1..1000 do
                    let context = Runtime.Loader.AssemblyLoadContext ("module-init-race", true)
                    let assembly = context.LoadFromAssemblyPath path
                    let startup = assembly.GetType "<StartupCode$Ranvier-View>.$Ranvier.View.Native"
                    let native = assembly.GetType "Ranvier.View.Native"
                    let newSet = native.GetMethod ("newSet", flags)
                    use start = new Threading.Barrier (2)

                    let run (body: unit -> unit) =
                        Threading.Thread (fun () ->
                            start.SignalAndWait ()

                            try
                                body ()
                            with e ->
                                lock failures (fun () -> failures.Add (string (e.GetBaseException ()))))

                    let file = run (fun () -> RuntimeHelpers.RunClassConstructor startup.TypeHandle)
                    let set = run (fun () -> newSet.Invoke (null, [||]) |> ignore)
                    file.Start ()
                    set.Start ()
                    file.Join ()
                    set.Join ()
                    context.Unload ()

                Expect.isEmpty (List.ofSeq failures) (String.Join ("; ", Seq.distinct failures))

            testCase "reading the document with no host names TwinDocument"
            <| fun () ->
                let e = Expect.throwsC (fun () -> Native.document () |> ignore) id
                Expect.isTrue (e :? InvalidOperationException) "InvalidOperationException"
                Expect.stringContains e.Message "create a TwinDocument first" "message"

            testCase "an installed host serves the thread until it is disposed"
            <| fun () ->
                let posted = ResizeArray<unit -> unit>()

                do
                    use _ = Native.install (recordingHost posted)
                    Native.queueMicrotask ignore
                    Expect.equal posted.Count 1 "posted through the host"

                Expect.throwsT<InvalidOperationException> (fun () -> Native.queueMicrotask ignore) "uninstalled"

            testCase "disposing an installation on another thread or out of order raises"
            <| fun () ->
                let first = Native.install (recordingHost (ResizeArray ()))
                let second = Native.install (recordingHost (ResizeArray ()))
                Expect.throwsT<InvalidOperationException> (fun () -> first.Dispose ()) "out of order"
                let failure = ref null

                let thread =
                    Threading.Thread (fun () ->
                        try
                            second.Dispose ()
                        with e ->
                            failure.Value <- e)

                thread.Start ()
                thread.Join ()
                Expect.isTrue (failure.Value :? InvalidOperationException) "another thread"
                second.Dispose ()
                first.Dispose ()
                Expect.throwsT<InvalidOperationException> (fun () -> Native.queueMicrotask ignore) "both uninstalled"

            testCase "one host installed twice is disposed innermost first"
            <| fun () ->
                let host = recordingHost (ResizeArray ())
                let outer = Native.install host
                let inner = Native.install host
                Expect.throwsT<InvalidOperationException> (fun () -> outer.Dispose ()) "the outer installation first"
                inner.Dispose ()
                outer.Dispose ()
                Expect.throwsT<InvalidOperationException> (fun () -> Native.queueMicrotask ignore) "uninstalled"
        ]
