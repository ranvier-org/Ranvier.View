module internal NativeApiConformance.Notifications

open System
open Ranvier.Dom

/// <summary>Checks the granted permission, notification fields, argument handling, signal-bound close and badges on one provider.</summary>
/// <remarks><c>service</c> belongs to a document whose permission is granted; <c>badges</c> accepts badges.</remarks>
let run (service: NotificationService) (badges: AppBadgeService) (cancellation: CancellationService) =
    task {
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith (String.Concat [| label; ": expected "; string expected; ", got "; string actual |])

        let raises label (action: unit -> unit) =
            checks <- checks + 1

            let raised =
                try
                    action ()
                    false
                with _ ->
                    true

            if not raised then
                failwith (label + ": expected an exception")

        let! permission = service.RequestPermission ()
        equal "Granted permission" NotificationPermission.Granted permission

        let options =
            { NotificationOptions.Default with
                Body = "Body"
                Tag = "t1"
                Lang = "en-GB"
                Icon = "https://example.test/icons/a.png"
                Dir = NotificationDirection.Rtl
                Silent = Some true
            }

        let shown = service.Show ("Title é", options, None)
        equal "Title" "Title é" shown.Title
        equal "Body" "Body" shown.Body
        equal "Tag" "t1" shown.Tag
        equal "Lang" "en-GB" shown.Lang
        equal "Icon" "https://example.test/icons/a.png" shown.Icon
        equal "Dir" NotificationDirection.Rtl shown.Dir
        equal "Silent" (Some true) shown.Silent
        shown.Close ()
        shown.Close ()

        let empty = service.Show ("", NotificationOptions.Default, None)
        equal "Default body" "" empty.Body
        equal "Default dir" NotificationDirection.Auto empty.Dir
        empty.Close ()

        raises "A null title is rejected" (fun () ->
            service.Show (null, NotificationOptions.Default, None)
            |> ignore)

        raises "A null option string is rejected" (fun () ->
            service.Show (
                "t",
                { NotificationOptions.Default with
                    Body = null
                },
                None
            )
            |> ignore)

        raises "A null event callback is rejected" (fun () ->
            empty.OnClick (Unchecked.defaultof<unit -> unit>)
            |> ignore)

        let controller = cancellation.CreateController ()

        let bound =
            service.Show ("Bound", NotificationOptions.Default, Some controller.Signal)

        let mutable errors = 0
        use _errors = bound.OnError (fun () -> errors <- errors + 1)
        controller.Abort ()
        equal "Abort closes without an error" 0 errors

        let aborted =
            service.Show ("Aborted", NotificationOptions.Default, Some (cancellation.Abort AbortReason.Abort))

        equal "An aborted signal still creates the notification" "Aborted" aborted.Title

        do! badges.Set (Some 3UL)
        do! badges.Set None
        do! badges.Clear ()
        checks <- checks + 1

        return checks
    }
