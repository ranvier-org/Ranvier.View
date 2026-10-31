module internal NativeApiConformance.HighlightChecks

open System
open Ranvier.Dom

/// <summary>The engine whose highlight hit testing the fixture expects.</summary>
type Engine =
    /// <summary>The pinned WebKit, which has no <c>highlightsFromPoint</c>.</summary>
    | WebKit
    /// <summary>Chromium 156, which has <c>highlightsFromPoint</c>.</summary>
    | Chromium

let private Text = "X X X X X X X X"

/// <summary>
/// Checks the registry's maplike order, a Highlight's setlike order, snapshots under mutation, priority and type,
/// object identity of returned highlights and ranges, and that highlighting leaves the text unchanged; the result
/// is the assertion count. The expected values are Chromium 156's
/// (Ranvier.WebCore tests/fixtures/custom-highlights-oracle.json).
/// </summary>
let run (create: unit -> HighlightService) (document: Document) (engine: Engine) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith $"{label}: expected {expected}, got {actual}"

    let same label (expected: obj) (actual: obj) =
        checks <- checks + 1

        if not (obj.ReferenceEquals (expected, actual)) then
            failwith $"{label}: a different object"

    let raises label (action: unit -> unit) =
        checks <- checks + 1

        let raised =
            try
                action ()
                false
            with :? ArgumentNullException ->
                true

        if not raised then
            failwith $"{label}: no ArgumentNullException"

    use service = create ()
    let paragraph = document.createElement "p"
    paragraph.textContent <- Text
    document.body.appendChild paragraph |> ignore
    let text = paragraph.firstChild

    let range start finish =
        let value = document.createRange ()
        value.setStart (text, uint32 start)
        value.setEnd (text, uint32 finish)
        value :> AbstractRange

    let staticRange start finish =
        let selection = document.getSelection ()
        selection.setBaseAndExtent (text, uint32 start, text, uint32 finish)
        let value = selection.getComposedRanges ()
        selection.removeAllRanges ()
        equal "one composed range" 1 value.Length
        value[0] :> AbstractRange

    try
        let registry = service.Registry
        same "CSS.highlights is one registry" (box registry) (box service.Registry)

        let h1, h2, h4 =
            service.Create [||], service.Create [| range 0 1 |], service.Create [||]

        let label (value: Highlight) =
            if obj.ReferenceEquals (value, h1) then "h1"
            elif obj.ReferenceEquals (value, h2) then "h2"
            elif obj.ReferenceEquals (value, h4) then "h4"
            else "?"

        let keys () =
            service.Entries registry
            |> Array.map fst
            |> List.ofArray

        equal "registry.empty" 0 (service.Count registry)
        service.Set (registry, "a", h1)
        service.Set (registry, "b", h2)
        service.Set (registry, "c", h1)
        equal "registry.order" ([ "a"; "b"; "c" ], 3) (keys (), service.Count registry)
        service.Set (registry, "a", h4)
        equal "registry.replace keeps the position" [ "a"; "b"; "c" ] (keys ())

        equal
            "registry.get after replace"
            "h4"
            (service.TryGet (registry, "a")
             |> Option.map label
             |> Option.defaultValue "-")

        equal "registry.delete" (true, false) (service.Remove (registry, "b"), service.Remove (registry, "b"))
        equal "registry after delete" ([ "a"; "c" ], false, true) (keys (), service.Contains (registry, "b"), service.Contains (registry, "a"))
        equal "registry.get of an unregistered name" None (service.TryGet (registry, "zz"))
        service.Set (registry, "b", h2)
        equal "registry.reinsert goes last" [ "a"; "c"; "b" ] (keys ())

        equal
            "registry.entries"
            [ "a", "h4"; "c", "h1"; "b", "h2" ]
            (service.Entries registry
             |> Array.map (fun (key, value) -> key, label value)
             |> List.ofArray)

        same "registry.get returns the created highlight" (box h1) (box (service.TryGet (registry, "c")).Value)
        let snapshot = service.Entries registry
        service.Remove (registry, "c") |> ignore
        service.Set (registry, "d", h1)
        equal "registry.entries is a snapshot" [ "a"; "c"; "b" ] (snapshot |> Array.map fst |> List.ofArray)
        equal "registry after mutation" [ "a"; "b"; "d" ] (keys ())
        service.ClearRegistry registry
        equal "registry.clear" (0, [], 1) (service.Count registry, keys (), service.Size h2)

        let r1, r2, r3, r4 = range 0 1, range 2 3, range 4 5, range 8 9
        let s1 = staticRange 6 7

        let name (value: AbstractRange) =
            [ r1, "r1"; r2, "r2"; r3, "r3"; r4, "r4"; s1, "s1" ]
            |> List.tryFind (fun (candidate, _) -> obj.ReferenceEquals (candidate, value))
            |> Option.map snd
            |> Option.defaultValue "?"

        let names (value: Highlight) =
            service.Ranges value
            |> Array.map name
            |> List.ofArray

        equal "highlight.emptyConstructor" 0 (service.Size (service.Create [||]))
        let h = service.Create [| r1; r2; r1 |]
        equal "highlight.constructor adds a repeated range once" (2, [ "r1"; "r2" ]) (service.Size h, names h)
        equal "highlight defaults" (0, HighlightType.Highlight) (h.priority, h.``type``)
        service.Add (h, r3)
        service.Add (h, r1)
        equal "highlight.add keeps the position" ([ "r1"; "r2"; "r3" ], 3) (names h, service.Size h)
        service.Add (h, s1)
        equal "highlight.add a StaticRange" [ "r1"; "r2"; "r3"; "s1" ] (names h)
        equal "highlight.delete" (true, false) (service.Delete (h, r2), service.Delete (h, r2))
        equal "highlight.has" (false, true) (service.Has (h, r2), service.Has (h, r1))
        let before = service.Ranges h
        service.Delete (h, r3) |> ignore
        service.Add (h, r4)
        equal "highlight.values is a snapshot" [ "r1"; "r3"; "s1" ] (before |> Array.map name |> List.ofArray)
        equal "highlight after mutation" [ "r1"; "s1"; "r4" ] (names h)

        equal
            "highlight.priority"
            [ 5; -1; Int32.MaxValue; Int32.MinValue; 0 ]
            ([ 5; -1; Int32.MaxValue; Int32.MinValue; 0 ]
             |> List.map (fun value ->
                 h.priority <- value
                 h.priority))

        equal
            "highlight.type"
            [
                HighlightType.SpellingError
                HighlightType.GrammarError
                HighlightType.Highlight
            ]
            ([
                HighlightType.SpellingError
                HighlightType.GrammarError
                HighlightType.Highlight
             ]
             |> List.map (fun value ->
                 h.``type`` <- value
                 h.``type``))

        service.Set (registry, "red", h)
        equal "a registered highlight leaves the text unchanged" (Text, 1) (paragraph.textContent, paragraph.childNodes.length |> int)
        service.Clear h
        equal "highlight.clear" (0, 0) (service.Size h, (service.Ranges h).Length)

        let hits = service.HighlightsFromPoint (5.0, 5.0)

        equal
            "highlightsFromPoint"
            (engine = Chromium)
            (match hits with
             | HighlightHitTest.Hits _ -> true
             | HighlightHitTest.Unsupported -> false)

        raises "Create with a null array" (fun () -> service.Create null |> ignore)
        raises "Create with a null range" (fun () -> service.Create [| r1; null |] |> ignore)
        raises "Add a null range" (fun () -> service.Add (h, null))
        raises "Set with a null name" (fun () -> service.Set (registry, null, h))
        raises "Set a null highlight" (fun () -> service.Set (registry, "n", null))
        raises "Entries of a null registry" (fun () -> service.Entries null |> ignore)
        service.ClearRegistry registry
        checks
    finally
        paragraph.remove ()
