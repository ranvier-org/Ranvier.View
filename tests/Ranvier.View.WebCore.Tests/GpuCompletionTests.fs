module Ranvier.View.WebCore.Tests.GpuCompletionTests

open Expecto
open Ranvier.View.WebCore.Sdl

[<Tests>]
let tests =
    testCase "GPU completion polls one retained fence without waiting"
    <| fun () ->
        let mutable starts, waits, releases = 0, 0, 0
        let mutable ready = false

        let fence =
            CompletionFence (
                (fun () ->
                    starts <- starts + 1
                    42n),
                (fun handle ->
                    Expect.equal handle 42n "retained handle"
                    ready),
                (fun _ ->
                    waits <- waits + 1
                    ready <- true),
                (fun _ -> releases <- releases + 1)
            )

        Expect.isFalse (fence.TryComplete ()) "pending"
        Expect.isFalse (fence.TryComplete ()) "still pending"
        Expect.equal (starts, waits, releases) (1, 0, 0) "no wait or duplicate submission"
        ready <- true
        Expect.isTrue (fence.TryComplete ()) "completed"
        Expect.equal releases 1 "released once"
        ready <- false
        fence.Complete ()
        Expect.equal (starts, waits, releases) (2, 1, 2) "only explicit cleanup waits"
