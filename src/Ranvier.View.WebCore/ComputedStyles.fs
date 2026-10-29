namespace Ranvier.View.WebCore

open System
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Providers for live computed CSS declarations owned by a WebCore host session.</summary>
module ComputedStyles =
    /// <summary>Creates a borrowed provider whose operations run on the live host's creating thread.</summary>
    /// <param name="host">The host that owns each queried element and returned declaration.</param>
    let forHost (host: WebCoreHost) : ComputedStyleService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "dom-computed-style") then
            Interop.unsupported<unit> "dom-computed-style"

        let session = host.Session
        let document = host.Document
        let documentOwner = box document :?> NativeObject

        { new ComputedStyleService with
            member _.Get(element, pseudoElement) =
                host.EnsureActive ()

                if isNull (box element) then
                    nullArg (nameof element)

                let owner =
                    match box element with
                    | :? NativeObject as target when obj.ReferenceEquals (target.Session, session) ->
                        Interop.handle (box element) |> ignore
                        target
                    | _ -> invalidArg (nameof element) "The element belongs to another host or provider"

                if not (obj.ReferenceEquals (element.ownerDocument, document)) then
                    invalidArg (nameof element) "The element belongs to another document"

                let bytes, length =
                    match pseudoElement with
                    | None -> Array.empty<byte>, -1
                    | Some text when isNull text -> Array.empty<byte>, -1
                    | Some text ->
                        let bytes = Encoding.UTF8.GetBytes text
                        bytes, bytes.Length

                let mutable result = 0n

                NativeDom.rv_dom_window_get_computed_style (documentOwner.Handle, owner.Handle, bytes, length, &result)
                |> Interop.check "Window.getComputedStyle"

                session.Object (
                    result,
                    Kind.Style,
                    owner,
                    Func<Session, nativeint, NativeObject>(fun ownerSession handle -> Classes.CSSStyleDeclaration (ownerSession, handle))
                )
                |> unbox<CSSStyleDeclaration>
        }
