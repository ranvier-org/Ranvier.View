module internal NativeApiConformance.StyleMapLifecycle

open System
open System.Threading
open Ranvier.Dom
open Ranvier.View.WebCore

/// <summary>Checks native map context, retained values, thread boundaries and borrowed-provider ownership.</summary>
let run () =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith (sprintf "%s: expected %A, got %A" label expected actual)

    let failure action =
        try
            action ()
            "accepted"
        with error ->
            error.GetType().Name

    let same label expected actual =
        equal label true (obj.ReferenceEquals (expected, actual))

    equal "null host" "ArgumentNullException" (failure (fun () -> StyleMaps.forHost Unchecked.defaultof<_> |> ignore))

    equal
        "null document"
        "ArgumentNullException"
        (failure (fun () ->
            StyleMaps.forDocument Unchecked.defaultof<_>
            |> ignore))

    let host = WebCoreHost.Create (64, 64)
    let session = host.Session
    let service = StyleMaps.forHost host

    try
        let document = host.Document
        let target = document.createElement "div" :?> HTMLElement
        let map = service.Attribute (target :> ElementCSSInlineStyle)
        map.set ("width", CSSStyleValueOrUSVString.USVString "17px")
        let value = service.Get (map, "width") |> Option.get

        let mutable getError, entriesError, mapError, disposeError =
            "not-run", "not-run", "not-run", "not-run"

        let worker =
            Thread (
                ThreadStart (fun () ->
                    getError <- failure (fun () -> service.Get (map, "width") |> ignore)
                    entriesError <- failure (fun () -> service.Entries map |> ignore)
                    mapError <- failure (fun () -> map.getAll "width" |> ignore)
                    disposeError <- failure service.Dispose)
            )

        worker.Start ()
        worker.Join ()
        equal "service get wrong thread" "InvalidOperationException" getError
        equal "service entries wrong thread" "InvalidOperationException" entriesError
        equal "map read wrong thread" "InvalidOperationException" mapError
        equal "provider disposal wrong thread" "InvalidOperationException" disposeError

        do
            use foreignHost = WebCoreHost.Create (16, 16)
            use foreign = StyleMaps.forHost foreignHost

            let foreignMap =
                foreign.Attribute (foreignHost.Document.body :> ElementCSSInlineStyle)

            equal
                "foreign element"
                "ArgumentException"
                (failure (fun () ->
                    service.Computed foreignHost.Document.body
                    |> ignore))

            equal "foreign map get" "ArgumentException" (failure (fun () -> service.Get (foreignMap, "width") |> ignore))
            equal "foreign map entries" "ArgumentException" (failure (fun () -> service.Entries foreignMap |> ignore))

        let secondary = document.implementation.createHTMLDocument "secondary"
        use secondService = StyleMaps.forDocument secondary
        let secondElement = secondary.createElement "div" :?> HTMLElement
        let secondMap = service.Attribute (secondElement :> ElementCSSInlineStyle)
        secondMap.set ("width", CSSStyleValueOrUSVString.USVString "27px")
        same "secondary element acquisition identity" secondMap (secondService.Attribute (secondElement :> ElementCSSInlineStyle))

        equal
            "secondary map shared-service read"
            "27px"
            ((secondService.Get (secondMap, "width")
              |> Option.get)
                .toString())

        equal
            "secondary map entries"
            [| "width" |]
            (secondService.Entries secondMap
             |> Array.map _.Property)

        use sheets = StyleSheets.forHost host
        let sheet = sheets.Create StyleSheetOptions.Default
        sheet.replaceSync ".c13-ownerless { width:31px }"
        let rule = sheet.cssRules.item 0u :?> CSSStyleRule
        let declared = secondService.Declared rule
        same "ownerless cached across services" declared (service.Declared rule)
        declared.set ("width", CSSStyleValueOrUSVString.USVString "32px")
        equal "ownerless declared actual edit" "32px" (rule.style.getPropertyValue "width")

        equal
            "ownerless entries"
            [| "32px" |]
            ((service.Entries declared).[0].Values
             |> Array.map (fun v -> v.toString ()))

        sheet.deleteRule 0u
        same "detached cached across services" declared (service.Declared rule)
        equal "detached entries context remains usable" 1 (secondService.Entries declared).Length
        let style = document.createElement "style" :?> HTMLStyleElement
        style.textContent <- ".c13-replaced { width:41px }"
        document.head.appendChild style |> ignore
        let retainedSheet = style.sheet
        let retainedRule = retainedSheet.cssRules.item 0u :?> CSSStyleRule
        let retainedMap = service.Declared retainedRule
        style.textContent <- ".c13-new { height:42px }"
        equal "replacement clears old sheet owner" true (isNull retainedSheet.ownerNode)
        same "replacement cached across services" retainedMap (secondService.Declared retainedRule)
        retainedMap.set ("width", CSSStyleValueOrUSVString.USVString "43px")
        equal "replaced owner map edit" "43px" (retainedRule.style.getPropertyValue "width")

        equal
            "replaced owner snapshot"
            [| "width" |]
            (secondService.Entries retainedMap
             |> Array.map _.Property)

        style.remove ()
        secondService.Dispose ()
        equal "disposed second provider leaves cached map" "32px" ((declared.getAll "width").[0].toString())
        service.Dispose ()
        equal "returned value survives provider disposal" "17px" (value.toString ())
        equal "map survives provider disposal" 1u map.size
        equal "idempotent disposal" "accepted" (failure service.Dispose)
        use borrowed = StyleMaps.forHost host
        (host :> IDisposable).Dispose()
        equal "retained map after host disposal" "ObjectDisposedException" (failure (fun () -> map.size |> ignore))
        equal "retained value after host disposal" "ObjectDisposedException" (failure (fun () -> value.toString () |> ignore))
        equal "borrowed entries after host disposal" "ObjectDisposedException" (failure (fun () -> borrowed.Entries map |> ignore))
        equal "factory after host disposal" "ObjectDisposedException" (failure (fun () -> StyleMaps.forHost host |> ignore))
    finally
        service.Dispose ()
        (host :> IDisposable).Dispose()

    equal "host teardown" 0 session.LiveObjects
    use wrapperHost = WebCoreHost.Create (16, 16)
    use borrowed = StyleMaps.forHost wrapperHost

    let wrapperMap =
        borrowed.Attribute (wrapperHost.Document.body :> ElementCSSInlineStyle)

    wrapperHost.Session.Dispose ()
    equal "disposed session provider" "ObjectDisposedException" (failure (fun () -> borrowed.Get (wrapperMap, "width") |> ignore))
    equal "disposed session map" "ObjectDisposedException" (failure (fun () -> wrapperMap.size |> ignore))
    equal "document session teardown" 0 wrapperHost.Session.LiveObjects
    checks
