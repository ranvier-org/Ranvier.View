module internal NativeApiConformance.SheetConstruction

open System
open System.Threading
open Ranvier.Dom

/// <summary>Checks constructed sheets, ordered adoption and asynchronous completion on a live engine.</summary>
/// <param name="document">The fixture's page document.</param>
/// <param name="createService">Creates a provider owned by this fixture.</param>
/// <param name="checkpoint">Completes at a microtask checkpoint on the owning loop.</param>
/// <param name="computed">Reads a computed CSS property after style resolution.</param>
/// <param name="disposalError">Describes the observable disposed-service error in this compiler profile.</param>
/// <param name="expectedDisposalError">The exact provider-profile error descriptor required after disposal.</param>
let run
    (document: Document)
    (createService: unit -> StyleSheetService)
    (checkpoint: unit -> Async<unit>)
    (computed: HTMLElement -> string -> string)
    (disposalError: exn -> string)
    (expectedDisposalError: string)
    =
    async {
        use service = createService ()
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

        let same label expected actual =
            checks <- checks + 1

            if not (obj.ReferenceEquals (expected, actual)) then
                failwith $"{label}: object identity changed"

        let start work token =
            let mutable outcome: Choice<CSSStyleSheet, exn, OperationCanceledException> option =
                None

            Async.StartWithContinuations (
                work,
                (fun value -> outcome <- Some (Choice1Of3 value)),
                (fun error -> outcome <- Some (Choice2Of3 error)),
                (fun error -> outcome <- Some (Choice3Of3 error)),
                cancellationToken = token
            )

            fun () -> outcome

        let cancelled label read =
            match read () with
            | Some (Choice3Of3 _) -> equal label true true
            | other -> failwith $"{label}: expected cancellation, got {other}"

        let original = service.GetAdopted document
        let baseNode = document.createElement "base" :?> HTMLBaseElement
        baseNode.href <- "https://example.test/default/"
        let inlineStyle = document.createElement "style" :?> HTMLStyleElement
        inlineStyle.textContent <- ".inline-probe { width: 73px; color: red; } @media all { .inline-probe { height: 29px; } }"
        let failureProbe = document.createElement "div" :?> HTMLElement
        failureProbe.className <- "inline-probe"
        let probe = document.createElement "div" :?> HTMLElement
        probe.className <- "c07-probe"
        probe.style.setProperty ("width", "17px")
        let shadowHost = document.createElement "div" :?> HTMLElement
        document.head.appendChild baseNode |> ignore
        document.head.appendChild inlineStyle |> ignore
        document.body.appendChild probe |> ignore
        document.body.appendChild failureProbe |> ignore
        document.body.appendChild shadowHost |> ignore

        try
            let sheet = service.Create StyleSheetOptions.Default
            equal "default enabled" false sheet.disabled
            equal "default media" "" sheet.media.mediaText
            equal "initial empty rules" 0u sheet.cssRules.length
            equal "no owner node" true (isNull (box sheet.ownerNode))
            sheet.replaceSync ".c07-probe { width: 31px !important; background-image: url(pixel.png); }"
            service.SetAdopted (document, [| sheet |])
            equal "document adoption layout" 31 probe.clientWidth
            equal "default base URL" "url(\"https://example.test/default/pixel.png\")" (computed probe "background-image")
            let first = service.GetAdopted document
            equal "one adopted sheet" 1 first.Length
            same "adopted identity" sheet first[0]

            let explicitBase =
                if service.SupportsBaseUrl then
                    Some "https://example.test/explicit/"
                else
                    None

            let other =
                service.Create
                    { StyleSheetOptions.Default with
                        Disabled = true
                        Media = MediaText "print"
                        BaseUrl = explicitBase
                    }

            equal "constructor disabled" true other.disabled
            equal "constructor media text" "print" other.media.mediaText
            first[0] <- other
            same "adoption snapshot independence" sheet ((service.GetAdopted document)[0])

            let copiedMedia =
                service.Create
                    { StyleSheetOptions.Default with
                        Media = MediaList other.media
                    }

            other.media.mediaText <- "screen"
            equal "constructor copies media list" "print" copiedMedia.media.mediaText
            other.disabled <- false
            other.replaceSync ".c07-probe { width: 43px !important; background-image: url(second.png); }"
            service.SetAdopted (document, [| sheet; other; sheet |])
            let duplicate = service.GetAdopted document
            equal "duplicate count" 3 duplicate.Length
            same "ordered second sheet" other duplicate[1]
            same "duplicate identity" duplicate[0] duplicate[2]
            equal "adoption cascade order" 31 probe.clientWidth
            service.SetAdopted (document, [| other |])

            if service.SupportsBaseUrl then
                equal "explicit base URL" "url(\"https://example.test/explicit/second.png\")" (computed probe "background-image")
            else
                let mutable rejected = false

                try
                    service.Create
                        { StyleSheetOptions.Default with
                            BaseUrl = Some "https://example.test/explicit/"
                        }
                    |> ignore
                with :? NotSupportedException ->
                    rejected <- true

                equal "unsupported explicit base URL is rejected" true rejected

            let nullBase =
                service.Create
                    { StyleSheetOptions.Default with
                        BaseUrl = Some null
                    }

            nullBase.replaceSync ".c07-probe { width: 43px !important; background-image: url(third.png); }"
            service.SetAdopted (document, [| nullBase |])
            equal "Some null base URL is the document base URL" "url(\"https://example.test/default/third.png\")" (computed probe "background-image")
            service.SetAdopted (document, [| other |])

            let mutable invalidBase = false

            try
                service.Create
                    { StyleSheetOptions.Default with
                        BaseUrl = Some "http://["
                    }
                |> ignore
            with
            | StyleSheetException (name, _) -> invalidBase <- name = "NotAllowedError"
            | :? NotSupportedException when not service.SupportsBaseUrl -> invalidBase <- true

            equal "invalid base URL" true invalidBase

            let mutable invalidAdoption = false

            try
                service.SetAdopted (document, [| sheet; inlineStyle.sheet |])
            with StyleSheetException (name, _) ->
                invalidAdoption <- name = "NotAllowedError"

            equal "reject non-constructed adoption" true invalidAdoption
            same "failed adoption preserves old list" other ((service.GetAdopted document)[0])
            service.SetAdopted (document, [||])
            equal "cleared document layout" 17 probe.clientWidth

            let init = ShadowRootInit ()
            init.mode <- ShadowRootMode.Open
            init.slotAssignment <- SlotAssignmentMode.Named
            let shadow = shadowHost.attachShadow init
            shadow.appendChild probe |> ignore
            service.SetAdopted (shadow, [| sheet |])
            equal "shadow adoption layout" 31 probe.clientWidth
            same "shadow adopted identity" sheet ((service.GetAdopted shadow)[0])
            service.SetAdopted (shadow, [||])
            equal "cleared shadow layout" 17 probe.clientWidth

            let cold = service.ReplaceAsync (sheet, ".cold { opacity: 0.2; }")
            equal "replace is cold" ".c07-probe" ((sheet.cssRules.item 0u :?> CSSStyleRule).selectorText)
            let firstReplacement = start cold CancellationToken.None
            equal "completion is asynchronous" true (firstReplacement().IsNone)
            equal "parse starts with operation" ".cold" ((sheet.cssRules.item 0u :?> CSSStyleRule).selectorText)
            sheet.replaceSync ".between { opacity: 0.3; }"
            sheet.insertRule (".inserted {}", 0u) |> ignore

            let secondReplacement =
                start (service.ReplaceAsync (sheet, ".second { opacity: 0.4; }")) CancellationToken.None

            do! checkpoint ()

            match firstReplacement (), secondReplacement () with
            | Some (Choice1Of3 first), Some (Choice1Of3 second) ->
                same "first async identity" sheet first
                same "second async identity" sheet second
            | other -> failwith $"Concurrent replacements failed: {other}"

            equal "last replacement wins" ".second" ((sheet.cssRules.item 0u :?> CSSStyleRule).selectorText)

            let failureSheet = inlineStyle.sheet
            let failureRules = failureSheet.cssRules

            let retainedRules =
                [|
                    for index in 0 .. int failureRules.length - 1 -> failureRules.item (uint32 index)
                |]

            let serializedRules () =
                [|
                    for index in 0 .. int failureRules.length - 1 -> (failureRules.item (uint32 index)).cssText
                |]

            let beforeRules = serializedRules ()
            let retainedStyle = (retainedRules[0] :?> CSSStyleRule).style
            let beforeDeclaration = retainedStyle.cssText
            let beforeWidth = computed failureProbe "width"
            let beforeHeight = computed failureProbe "height"
            equal "failed replacement fixture rule count" 2 retainedRules.Length
            equal "failed replacement baseline width" "73px" beforeWidth
            equal "failed replacement baseline grouped height" "29px" beforeHeight

            let preserved label =
                equal (label + " complete rule text") beforeRules (serializedRules ())
                same (label + " sheet identity") failureSheet inlineStyle.sheet
                same (label + " list identity") failureRules failureSheet.cssRules

                for index in 0 .. retainedRules.Length - 1 do
                    same (label + " retained rule identity") retainedRules[index] (failureRules.item (uint32 index))
                    same (label + " rule parent sheet") failureSheet retainedRules[index].parentStyleSheet

                same (label + " declaration identity") retainedStyle (retainedRules[0] :?> CSSStyleRule).style
                equal (label + " full declaration text") beforeDeclaration retainedStyle.cssText
                equal (label + " matching width") beforeWidth (computed failureProbe "width")
                equal (label + " matching grouped height") beforeHeight (computed failureProbe "height")

            let failure =
                start
                    (service.ReplaceAsync (
                        failureSheet,
                        ".inline-probe { width: 199px; color: blue; } @media all { .inline-probe { height: 101px; } }"
                    ))
                    CancellationToken.None

            equal "error completion is asynchronous" true (failure().IsNone)
            preserved "pending failed replacement"
            do! checkpoint ()

            match failure () with
            | Some (Choice2Of3 (StyleSheetException (name, _))) -> equal "replace error name" "NotAllowedError" name
            | other -> failwith $"Expected asynchronous stylesheet error: {other}"

            preserved "rejected replacement"
            do! checkpoint ()
            preserved "late failed replacement checkpoint"

            use beforeStart = new CancellationTokenSource ()
            beforeStart.Cancel ()

            let preCancelled =
                start (service.ReplaceAsync (sheet, ".should-not-parse {}")) beforeStart.Token

            cancelled "cancel before start" preCancelled
            equal "cancelled cold operation did not parse" ".second" ((sheet.cssRules.item 0u :?> CSSStyleRule).selectorText)
            use during = new CancellationTokenSource ()

            let interrupted =
                start (service.ReplaceAsync (sheet, ".parsed-before-cancel {}")) during.Token

            during.Cancel ()
            do! checkpoint ()
            cancelled "cancel before delivery" interrupted
            equal "cancel preserves parsed result" ".parsed-before-cancel" ((sheet.cssRules.item 0u :?> CSSStyleRule).selectorText)

            let disposableService = createService ()

            let disposedWork =
                start (disposableService.ReplaceAsync (sheet, ".parsed-before-dispose {}")) CancellationToken.None

            disposableService.Dispose ()
            do! checkpoint ()
            cancelled "service disposal terminates pending work" disposedWork
            do! checkpoint ()
            cancelled "late completion stays cancelled" disposedWork

            for label, action in
                [|
                    "create",
                    (fun () ->
                        disposableService.Create StyleSheetOptions.Default
                        |> ignore)
                    "get adoption", (fun () -> disposableService.GetAdopted document |> ignore)
                    "set adoption", (fun () -> disposableService.SetAdopted (document, [||]))
                |] do
                let mutable reason = "success"

                try
                    action ()
                with error ->
                    reason <- disposalError error

                equal ("disposed service rejects " + label) expectedDisposalError reason

            let refusedReplacement =
                start (disposableService.ReplaceAsync (sheet, ".after-dispose {}")) CancellationToken.None

            match refusedReplacement () with
            | Some (Choice2Of3 error) -> equal "disposed service rejects replacement" expectedDisposalError (disposalError error)
            | other -> failwith $"Expected disposed replacement rejection: {other}"
        finally
            service.SetAdopted (document, original)

            for node in
                [|
                    baseNode :> Node
                    inlineStyle :> Node
                    probe :> Node
                    failureProbe :> Node
                    shadowHost :> Node
                |] do
                if not (isNull (box node.parentNode)) then
                    node.parentNode.removeChild node |> ignore

        return checks
    }
