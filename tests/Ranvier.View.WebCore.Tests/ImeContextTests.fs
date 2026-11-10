module Ranvier.View.WebCore.Tests.ImeContextTests

open Expecto
open Ranvier.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

[<Tests>]
let tests =
    testList
        "bounded IME context"
        [
            hostCase "control context carries a bounded origin and rejects stale deletion" (fun host ->
                if not (WebCoreHost.Has "ime-surrounding-window-v1") then
                    skiptest "requires windowed IME ABI"

                host.Document.body.innerHTML <- "<input id='input'>"
                let input = host.Document.getElementById "input" :?> HTMLInputElement
                input.value <- String.replicate 10000 "a" + "😀xyz"
                input.focus ()
                input.setSelectionRange (10000u, 10002u, "backward")
                let snapshot = ImeContext.read host 4096 |> ValueOption.get
                Expect.equal snapshot.State ImeContextState.Available "available"
                Expect.isLessThanOrEqual snapshot.Text.Length 4096 "bounded UTF16 context"
                Expect.equal (snapshot.Origin + snapshot.Cursor) 10000 "backward caret"
                input.value <- "changed"
                Expect.isFalse (ImeContext.tryDelete host snapshot.Token snapshot.Cursor snapshot.Anchor) "stale deletion rejected")
            hostCase "unsupported rich text and composition report unavailable" (fun host ->
                if not (WebCoreHost.Has "ime-surrounding-window-v1") then
                    skiptest "requires windowed IME ABI"

                host.Document.body.innerHTML <- "<textarea id='input'>abc</textarea><div id='rich' contenteditable>abc</div>"
                let input = host.Document.getElementById "input" :?> HTMLTextAreaElement
                input.focus ()
                host.SetComposition ("日本", 2, 2)
                Expect.equal (ImeContext.read host 4096 |> ValueOption.get).State ImeContextState.Unavailable "composition"
                host.CancelComposition ()
                (host.Document.getElementById "rich" :?> HTMLElement).focus()
                Expect.equal (ImeContext.read host 4096 |> ValueOption.get).State ImeContextState.Unavailable "no linear rich-text extraction")
        ]
