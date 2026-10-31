module internal NativeApiConformance.ScreenChecks

open System
open Ranvier.Dom

/// <summary>How an engine sets the screen and what its orientation lock does.</summary>
type Driver =
    {
        /// <summary>Sets a screen of width, height and orientation, all of it available and 24 bits deep.</summary>
        Set: int * int * ScreenOrientationType -> Async<unit>
        /// <summary>Lets queued events run.</summary>
        Settle: unit -> Async<unit>
        /// <summary>Whether lock("portrait") resolves: Chromium headless resolves it, the native host has no locking.</summary>
        LockResolves: bool
    }

// The Chromium 156 oracle of Ranvier.WebCore tests/fixtures/screen-oracle.json: type and angle pairs of the natural
// landscape orientation.
let private cases =
    [ 1920, 1080, ScreenOrientationType.LandscapePrimary, 0
      1080, 1920, ScreenOrientationType.PortraitPrimary, 90
      2560, 1440, ScreenOrientationType.LandscapeSecondary, 180
      768, 1024, ScreenOrientationType.PortraitSecondary, 270 ]

/// <summary>
/// Checks Screen and ScreenOrientation values, change delivery and orientation locking on one provider; the result is
/// the assertion count. The order of change and resize events is never asserted.
/// </summary>
let run (create: unit -> ScreenService) (driver: Driver) =
    async {
        let mutable checks = 0

        let equal label (expected: 'T) (actual: 'T) =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected %A{expected}, got %A{actual}"

        let raises label (action: unit -> unit) =
            checks <- checks + 1

            let raised =
                try
                    action ()
                    false
                with _ ->
                    true

            if not raised then
                failwith $"{label}: expected an exception"

        let service = create ()
        let delivered = ResizeArray<ScreenInfo>()
        let subscription = service.Subscribe delivered.Add

        for width, height, orientation, angle in cases do
            do! driver.Set (width, height, orientation)
            do! driver.Settle ()
            let info = service.Read ()
            let label = $"{width}x{height} {ScreenContract.orientationName orientation}"
            equal $"{label} size" (width, height) (info.Width, info.Height)
            equal $"{label} work area" (0, 0, width, height) (info.AvailLeft, info.AvailTop, info.AvailWidth, info.AvailHeight)
            equal $"{label} depth" (24, 24) (info.ColorDepth, info.PixelDepth)
            equal $"{label} orientation" orientation info.Orientation
            equal $"{label} angle" angle info.Angle
            equal $"{label} change delivered" (Some info) (Seq.tryLast delivered)

        let count = delivered.Count
        let width, height, orientation, _ = List.last cases
        do! driver.Set (width, height, orientation)
        do! driver.Settle ()
        equal "an unchanged screen delivers nothing" count delivered.Count
        subscription.Dispose ()
        let first, firstHeight, firstOrientation, _ = List.head cases
        do! driver.Set (first, firstHeight, firstOrientation)
        do! driver.Settle ()
        equal "a disposed subscription receives nothing" count delivered.Count
        raises "a null handler" (fun () -> service.Subscribe (Unchecked.defaultof<_>) |> ignore)
        let! locked = service.Lock "portrait"

        equal "lock(portrait)" driver.LockResolves (
            match locked with
            | ScreenLockOutcome.Locked -> true
            | _ -> false
        )

        equal "lock outcome" (if driver.LockResolves then ScreenLockOutcome.Locked else ScreenContract.unsupportedLock) locked

        service.Unlock ()
        raises "an unknown lock type" (fun () -> service.Lock "sideways" |> ignore)
        service.Dispose ()
        raises "Read after Dispose" (fun () -> service.Read () |> ignore)
        return checks
    }
