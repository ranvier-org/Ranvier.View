/// <summary>
/// Writes native wrapper implementations and a mapping report from the canonical IDL model and shared contract assembly.
/// </summary>
module Ranvier.View.WebCore.Generate.Program

open System
open System.IO
open System.Reflection
open Ranvier.View.WebCore.Generate.Emit

let private repository =
    Path.GetFullPath (Path.Combine (__SOURCE_DIRECTORY__, "..", ".."))

let private kebab (name: string) =
    let text =
        Text.RegularExpressions.Regex.Replace (name, "[A-Z]", fun m -> "-" + m.Value.ToLowerInvariant ())

    if text.StartsWith "webkit-" then "-" + text else text

let private hand (dom: Type) (header: string) : Map<string, string> =
    let fixedBodies =
        [
            "EventTarget.addEventListener", "Events.Add(this, {0}, {1}, {2})"
            "EventTarget.removeEventListener", "Events.Remove(this, {0}, {1}, {2})"
            "EventTarget.dispatchEvent", "Events.Dispatch(this, {0})"
            "Event.currentTarget.get", "Events.CurrentTarget this"
            "Event.target.get", "Events.Target this"
            "MouseEvent.relatedTarget.get", "Events.RelatedTarget this"
            "FocusEvent.relatedTarget.get", "Events.RelatedTarget this"
            "KeyboardEvent.key.get",
            "Events.Init(this, \"key\", fun () -> Interop.text \"KeyboardEvent.key\" (fun b c l -> NativeDom.rv_dom_keyboard_event_get_key (this.Handle, b, c, l)))"
            "InputEvent.data.get",
            "Events.Init(this, \"data\", fun () -> Interop.text \"InputEvent.data\" (fun b c l -> NativeDom.rv_dom_input_event_get_data (this.Handle, b, c, l)))"
            "InputEvent.inputType.get",
            "Events.Init(this, \"inputType\", fun () -> Interop.text \"InputEvent.inputType\" (fun b c l -> NativeDom.rv_dom_input_event_get_input_type (this.Handle, b, c, l)))"
            "Document.createNodeIterator",
            "unbox<Ranvier.Dom.NodeIterator> (Traversal.Create(this, {0}, {1}, {2}, (fun target root show filter -> let mutable result = 0n in Interop.check \"Document.createNodeIterator\" (NativeDom.rv_dom_document_create_node_iterator (target, root, show, filter, &result)); result), (fun session handle -> NodeIteratorObject(session, handle) :> NativeObject)))"
            "Document.createTreeWalker",
            "unbox<Ranvier.Dom.TreeWalker> (Traversal.Create(this, {0}, {1}, {2}, (fun target root show filter -> let mutable result = 0n in Interop.check \"Document.createTreeWalker\" (NativeDom.rv_dom_document_create_tree_walker (target, root, show, filter, &result)); result), (fun session handle -> TreeWalkerObject(session, handle) :> NativeObject)))"
            "NodeIterator.filter.get",
            "Traversal.FilterOf(fun () -> let mutable result = 0n in Interop.check \"NodeIterator.filter\" (NativeDom.rv_dom_node_iterator_get_filter (Interop.handle (box this), &result)); result)"
            "TreeWalker.filter.get",
            "Traversal.FilterOf(fun () -> let mutable result = 0n in Interop.check \"TreeWalker.filter\" (NativeDom.rv_dom_tree_walker_get_filter (Interop.handle (box this), &result)); result)"
        ]

    let styles = []

    // The IDL constants, as the header's RV_DOM_<INTERFACE>_<NAME> literals.
    let constants =
        [
            for m in Text.RegularExpressions.Regex.Matches (header, @"(RV_DOM_\w+) = [^,]+, /\* (\w+)\.(\w+) \*/") ->
                $"{m.Groups[2].Value}.{m.Groups[3].Value}.get", $"float NativeDom.{m.Groups[1].Value}"
        ]

    let listeners = []

    Map.ofList (fixedBodies @ styles @ constants @ listeners)

let private kinds =
    Map.ofList
        [
            "EventTarget", "Kind.Node"
            "Event", "Kind.Event"
            "CSSStyleDeclaration", "Kind.Style"
            "DOMTokenList", "Kind.TokenList"
            "DOMStringMap", "Kind.StringMap"
            "AbstractRange", "Kind.Range"
            "Selection", "Kind.Selection"
            "DOMSelection", "Kind.Selection"
            "DOMRectReadOnly", "Kind.Rect"
            "DOMPointReadOnly", "Kind.Point"
            "DOMMatrixReadOnly", "Kind.Matrix"
            "CSSTransformComponent", "Kind.TransformComponent"
            "SVGRect", "Kind.SVGRect"
            "SVGMatrix", "Kind.SVGMatrix"
            "SVGPoint", "Kind.SVGPoint"
            "SVGAnimatedRect", "Kind.SVGAnimatedRect"
            "SVGPreserveAspectRatio", "Kind.SVGPreserveAspectRatio"
            "SVGAnimatedPreserveAspectRatio", "Kind.SVGAnimatedPreserveAspectRatio"
            "SVGTransform", "Kind.SVGTransform"
            "SVGTransformList", "Kind.SVGTransformList"
            "SVGAnimatedTransformList", "Kind.SVGAnimatedTransformList"
            "SVGNumber", "Kind.SVGNumber"
            "SVGLength", "Kind.SVGLength"
            "SVGAngle", "Kind.SVGAngle"
            "SVGNumberList", "Kind.SVGNumberList"
            "SVGLengthList", "Kind.SVGLengthList"
            "SVGPointList", "Kind.SVGPointList"
            "SVGAnimatedNumber", "Kind.SVGAnimatedNumber"
            "SVGAnimatedLength", "Kind.SVGAnimatedLength"
            "SVGAnimatedLengthList", "Kind.SVGAnimatedLengthList"
            "SVGAnimatedNumberList", "Kind.SVGAnimatedNumberList"
            "SVGAnimatedEnumeration", "Kind.SVGAnimatedEnumeration"
            "SVGAnimatedString", "Kind.SVGAnimatedString"
            "SVGAnimatedBoolean", "Kind.SVGAnimatedBoolean"
            "SVGAnimatedInteger", "Kind.SVGAnimatedInteger"
            "SVGStringList", "Kind.SVGStringList"
            "SVGViewSpec", "Kind.SVGViewSpec"
            "SVGAnimatedAngle", "Kind.SVGAnimatedAngle"
            "DOMQuad", "Kind.Quad"
            "DOMRectList", "Kind.RectList"
            "NodeList", "Kind.NodeList"
            "HTMLCollection", "Kind.Collection"
            "DOMFormData", "Kind.FormData"
            "Blob", "Kind.Blob"
            "FileList", "Kind.FileList"
            "URL", "Kind.Url"
            "URLSearchParams", "Kind.UrlSearchParams"
            "URLPattern", "Kind.UrlPattern"
            "HTMLCollectionOf`1", "Kind.Collection"
            "ValidityState", "Kind.Validity"
            "DOMImplementation", "Kind.DOMImplementation"
            "NamedNodeMap", "Kind.NamedNodeMap"
            "CSSRule", "Kind.CSSRule"
            "CSSRuleList", "Kind.CSSRuleList"
            "StyleSheet", "Kind.StyleSheet"
            "StyleSheetList", "Kind.StyleSheetList"
            "MediaList", "Kind.MediaList"
            "CSSStyleValue", "Kind.StyleValue"
            "CSSVariableReferenceValue", "Kind.VariableReferenceValue"
            "StylePropertyMapReadOnly", "Kind.StyleMap"
            "NodeIterator", "Kind.NodeIterator"
            "TreeWalker", "Kind.TreeWalker"
            "CaretPosition", "Kind.CaretPosition"
            "VisualViewport", "Kind.VisualViewport"
        ]

[<EntryPoint>]
let main argv =
    let modelPath, contractPath, interopPath, output =
        match argv with
        | [| model; contracts; interop |] -> model, contracts, interop, Path.Combine (repository, "src", "Ranvier.View.WebCore", "Generated")
        | [| model; contracts; interop; output |] -> model, contracts, interop, output
        | _ -> invalidArg "argv" "Expected dom-api.json, Ranvier.Dom.dll, Ranvier.WebCore.dll and optional output directory"

    let model = Metadata.read modelPath
    let contracts = Assembly.LoadFrom (Path.GetFullPath contractPath)
    let dom = contracts.GetType ("Ranvier.Dom.Node", true)
    let interop = Assembly.LoadFrom (Path.GetFullPath interopPath)
    let nativeDom = interop.GetType ("RanvierWebCoreAbi.NativeDom", true)
    let functions = Abi.readModel nativeDom model

    let tagMap =
        [
            for tag in Metadata.items "tagInterfaces" model ->
                Metadata.text "namespaceURI" tag, Metadata.text "tag" tag, Metadata.text "interface" tag
        ]

    let extra =
        let nativeTypes =
            Metadata.items "interfaces" model
            |> Array.choose (fun row ->
                contracts.GetType (
                    "Ranvier.Dom."
                    + Metadata.publicInterfaceName model (Metadata.text "name" row)
                )
                |> Option.ofObj)

        [
            "EventTarget"
            for t in contracts.GetTypes () do
                if
                    t.IsInterface
                    && t.Namespace = "Ranvier.Dom"
                    && (nativeTypes
                        |> Array.exists (fun native -> native.IsAssignableFrom t))
                then
                    t.Name
        ]

    let input =
        {
            Dom = dom
            Model = model
            Functions = functions
            Hand = hand dom ""
            Extra = extra
            Kinds = kinds
        }

    let code, entries = Emit.run input tagMap

    let count f =
        entries
        |> List.filter (fun e -> f e.Status)
        |> List.length

    let mapped =
        count (function
            | Mapped _ -> true
            | _ -> false)

    let handWritten = count ((=) Hand)

    let unmapped =
        count (function
            | Unmapped _ -> true
            | _ -> false)

    let usedFunctions =
        entries
        |> List.choose (fun e ->
            match e.Status with
            | Mapped f -> Some f
            | _ -> None)
        |> Set.ofList

    let used = usedFunctions.Count

    let classes =
        entries
        |> List.map (fun e -> e.Class)
        |> List.distinct
        |> List.length

    let summary =
        $"{classes} classes; {entries.Length} members: {mapped} mapped to {used} of {functions.Length} rv_dom_* functions, {handWritten} hand-written, {unmapped} unmapped"

    let header =
        [
            "// <auto-generated>"
            "// Written by tools/Ranvier.View.WebCore.Generate from the canonical WebKit IDL model and Ranvier.Dom contracts; do not edit."
            $"// {summary}."
            "// An unmapped member raises NotSupportedException; Generated/report.txt lists each one with the reason."
            "// </auto-generated>"
            "namespace Ranvier.View.WebCore"
            ""
            "open RanvierWebCoreAbi"
            ""
        ]

    Directory.CreateDirectory output |> ignore

    File.WriteAllText (
        Path.Combine (output, "Objects.fs"),
        String.concat "\r\n" header
        + code.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n")
    )

    let report =
        [
            summary
            ""
            "Native exports without generated member mappings (including hand-written adapters):"
            for fn in functions |> List.sortBy (fun fn -> fn.Name) do
                if not (usedFunctions.Contains fn.Name) then
                    $"  {fn.Name}: {fn.Interface}.{fn.Member} ({fn.Kind})"
            ""
            "Unmapped members by reason:"
            for reason, group in
                entries
                |> List.choose (fun e ->
                    match e.Status with
                    | Unmapped r -> Some (r, e)
                    | _ -> None)
                |> List.groupBy (fun (r, _) -> Text.RegularExpressions.Regex.Replace (r, @"argument \w+", "argument"))
                |> List.sortByDescending (fun (_, g) -> g.Length) do
                $"  {group.Length, 5}  {reason}"
            ""
            "Members by class (mapped / hand-written / unmapped):"
            for cls, group in
                entries
                |> List.groupBy (fun e -> e.Class)
                |> List.sortBy fst do
                let c f =
                    group
                    |> List.filter (fun e -> f e.Status)
                    |> List.length

                $"  {cls}: {c (function
                                | Mapped _ -> true
                                | _ -> false)} / {c ((=) Hand)} / {c (function
                                                                       | Unmapped _ -> true
                                                                       | _ -> false)}"
            ""
            "Unmapped members:"
            for e in entries do
                match e.Status with
                | Unmapped reason -> $"  {e.Class} {e.Interface}.{e.Member}: {reason}"
                | _ -> ()
        ]

    File.WriteAllLines (Path.Combine (output, "report.txt"), report)
    printfn "%s" summary
    0
