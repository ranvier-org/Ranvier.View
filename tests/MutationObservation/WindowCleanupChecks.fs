module Ranvier.View.WebCore.Tests.WindowCleanupChecks

open System
open System.Threading
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl

let runNative () =
    let mutable checks = 0

    let check condition detail =
        if not condition then
            failwith detail

        checks <- checks + 1

    let window =
        Window.Create ("Mutation cleanup control", 80, 60, hidden = true, renderer = "software")

    let host = window.Host
    let originalWindow = window.SdlWindow

    try
        let mutable failure: exn = null

        let requester =
            Thread (
                ThreadStart (fun () ->
                    try
                        (window :> IDisposable).Dispose()
                    with error ->
                        failure <- error)
            )

        requester.Start ()
        check (requester.Join 5000) "The off-thread window request terminates"
        check (failure :? InvalidOperationException) "Window disposal rejects the wrong owner thread"

        check
            (not host.Session.IsDisposed
             && window.SdlWindow = originalWindow)
            "A rejected request retains the live host and window"

        (window :> IDisposable).Dispose()
        check host.Session.IsDisposed "Owner retry must finish host teardown after a rejected request"
        (window :> IDisposable).Dispose()
    finally
        try
            (window :> IDisposable).Dispose()
        finally
            if not host.Session.IsDisposed then
                (host :> IDisposable).Dispose()

    checks
