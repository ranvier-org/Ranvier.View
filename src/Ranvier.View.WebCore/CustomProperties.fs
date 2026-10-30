namespace Ranvier.View.WebCore

open System
open System.Runtime.InteropServices
open System.Text
open Ranvier.Dom
open RanvierWebCoreAbi

/// <summary>Providers for document-scoped custom-property registration over a WebCore host.</summary>
module CustomProperties =
    /// <summary>Creates a borrowed registration provider bound to the host's document and creating thread.</summary>
    /// <param name="host">The live host whose CSS registry validates and retains the definitions.</param>
    /// <remarks>Disposal invalidates this provider without unregistering definitions from the host's document.</remarks>
    let forHost (host: WebCoreHost) : CustomPropertyService =
        if isNull (box host) then
            nullArg (nameof host)

        host.EnsureActive ()

        if not (WebCoreHost.Has "dom-css-register-property") then
            Interop.unsupported<unit> "dom-css-register-property"

        let thread = Environment.CurrentManagedThreadId
        let document = host.Document
        let mutable disposed = false

        { new CustomPropertyService with
            member _.Register definition =
                host.EnsureActive ()

                if disposed then
                    raise (ObjectDisposedException "CustomPropertyService")

                if isNull (box definition) then
                    nullArg (nameof definition)

                let documentHandle = Interop.handle (box document)
                use inputs = new InputScope ()

                let text value =
                    let bytes = Encoding.UTF8.GetBytes (if isNull value then "null" else value)
                    let mutable span = NativeDom.RvDomUtf8StringInput ()
                    span.present <- 1
                    span.data <- inputs.Pin bytes
                    span.length <- bytes.Length
                    span

                let mutable descriptor = NativeDom.RvDomDOMCSSCustomPropertyDescriptor ()
                descriptor.size <- uint32 (Marshal.SizeOf<NativeDom.RvDomDOMCSSCustomPropertyDescriptor>())
                descriptor.name <- text definition.Name
                descriptor.syntax <- text definition.Syntax
                descriptor.inherits <- if definition.Inherits then 1 else 0

                match definition.InitialValue with
                | CustomPropertyInitialValue.Omitted -> ()
                | CustomPropertyInitialValue.Value value -> descriptor.initial_value <- text value

                try
                    NativeDom.rv_dom_css_register_property (documentHandle, [| descriptor |])
                    |> Interop.check "CSS.registerProperty"
                with WebCoreDomException (name, detail) ->
                    raise (CustomPropertyException (name, detail))

            member _.Dispose() =
                if Environment.CurrentManagedThreadId <> thread then
                    raise (InvalidOperationException "A custom-property service is disposed on its host thread")

                disposed <- true
        }
