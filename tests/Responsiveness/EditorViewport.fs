module Responsiveness.EditorViewport

open System
open Ranvier
open Ranvier.Dom
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore
open Responsiveness.ViewportModel

type private Row =
    {
        Slot: int
        Text: Signal<string>
        mutable Line: int
        mutable Revision: int64
    }

let private row =
    Component.define (fun _ (item: unit -> Row) -> item ()) (fun t ->
        t.element (HtmlTag "div") [| t.Attrs.className "editor-line" |] [| t.text (t.from (fun (row: Row) -> row.Text)) |])

let private rows =
    Component.define (fun _ (items: Signal<Row array>) -> items) (fun t ->
        t.element (HtmlTag "div") [||] [| t.each (fun (row: Row) -> row.Slot) (fun items -> items.Value) row |])

type EditorViewport
    (
        host: WebCoreHost,
        container: HTMLElement,
        total: int,
        ?unbounded: bool,
        ?textKind: string,
        ?document: Responsiveness.DocumentModel.DocumentBuffer
    ) =
    do
        if total < 1 then
            invalidArg "total" "positive line count required"

    let mutable total =
        match document with
        | Some buffer -> buffer.Snapshot.LineCount
        | None -> total

    let graph = new Graph ()
    let phases = Frames.layoutForHost host
    let visible = 25

    let count =
        if defaultArg unbounded false then total
        else if document.IsSome then visible + 100
        else min total (visible + 100)

    let slots =
        graph.Run (fun () ->
            Array.init count (fun slot ->
                {
                    Slot = slot
                    Text = Api.createSignal ""
                    Line = -1
                    Revision = 0L
                }))

    let mutable ordered = slots
    let sourceKeys = Array.create count (-1L, -1L)
    let mutable textWrites = 0L
    let mutable widthReads = 0L
    let items = graph.Run (fun () -> Api.createSignal slots)

    let mounted =
        Mounting.mount graph None (fun errors -> failwithf "%A" errors) container None (fun context ->
            Program.run (Component.render rows items) context.Backend)

    let cache = MeasurementCache (max 1 (count * 2))
    let fonts = Fonts.forHost host
    let mutable fontRevision = 0L
    let fontEvents = fonts.Subscribe (fun _ -> fontRevision <- fontRevision + 1L)
    let mutable first = min 50 (max 0 (total - visible))
    let mutable pinned = false
    let mutable requested = first
    let mutable styleRevision = 0L
    let mutable width, height, scale, wrap = 700, 500, 1., false

    let generatedText line =
        match defaultArg textKind "ordinary" with
        | "long-line" -> $"line {line}: " + String.replicate 512 "abcdef "
        | "bidi" -> $"line {line}: العربية עברית abc 日本語"
        | _ -> $"line {line}: let value = {line}"

    let mutable start = 0

    let clampFirst line =
        max
            0
            (min
                (total
                 - min count (if wrap then 1 else max 1 (height / 20)))
                line)

    let refresh () =
        let nextStart = min (max 0 (total - count)) (max 0 (first - 50))
        let delta = nextStart - start

        match document with
        | Some buffer ->
            let retained = System.Collections.Generic.Dictionary<int64, Row>()

            for row in ordered do
                let identity, _ = sourceKeys[row.Slot]

                if identity >= 0L then
                    retained[identity] <- row

            let selected = Array.create<Row option> count None
            let used = System.Collections.Generic.HashSet<int>()

            for index in 0 .. count - 1 do
                if nextStart + index < total then
                    match retained.TryGetValue (buffer.Snapshot.Line (nextStart + index)).Id with
                    | true, row ->
                        selected[index] <- Some row
                        used.Add row.Slot |> ignore
                    | _ -> ()

            let available =
                System.Collections.Generic.Queue<Row>(
                    ordered
                    |> Array.filter (fun row -> not (used.Contains row.Slot))
                )

            ordered <-
                selected
                |> Array.map (function
                    | Some row -> row
                    | None -> available.Dequeue ())
        | None when delta <> 0 && abs delta < count ->
            let previous = ordered
            ordered <- Array.init count (fun index -> previous[(index + delta + count) % count])
        | None -> ()

        start <- nextStart

        phases.Mutate (fun () ->
            graph.Run (fun () ->
                Api.batch (fun () ->
                    for index in 0 .. ordered.Length - 1 do
                        let row = ordered[index]
                        let line = start + index

                        let sourceKey =
                            match document with
                            | Some buffer when line < total -> let value = buffer.Snapshot.Line line in value.Id, value.Revision
                            | Some _ -> -1L, -1L
                            | None -> int64 line, 0L

                        row.Line <- line

                        if sourceKeys[row.Slot] <> sourceKey then
                            sourceKeys[row.Slot] <- sourceKey
                            row.Revision <- row.Revision + 1L

                            row.Text.Value <-
                                match document with
                                | Some buffer when line < total -> (buffer.Snapshot.Line line).Text
                                | Some _ -> ""
                                | None -> generatedText line

                            textWrites <- textWrites + 1L

                    if delta <> 0 || document.IsSome then
                        items.Value <- ordered)))

        container.scrollTop <- (first - start) * 20

    do
        container.setAttribute ("style", "height:500px;width:700px;overflow:hidden;font:14px monospace;line-height:20px;white-space:pre")
        refresh ()

    let nodesBySlot =
        let nodes = container.querySelectorAll ".editor-line"

        Array.init count (fun i ->
            let node = nodes.item (uint32 i) :?> HTMLElement
            node.style.setProperty ("min-height", "20px", "")
            node)

    let documentEvents =
        document
        |> Option.map (fun buffer ->
            buffer.Changed.Subscribe (fun change ->
                total <- buffer.Snapshot.LineCount

                if
                    pinned
                    && (first >= total
                        || (change.First <= first
                            && change.First + change.Removed > first))
                then
                    pinned <- false
                    host.CancelComposition ()

                if change.First < requested then
                    requested <-
                        if change.First + change.Removed <= requested then
                            requested + change.Added - change.Removed
                        else
                            change.First

                requested <- clampFirst requested

                if not pinned then
                    first <- requested
                    refresh ()))

    member _.TextWrites = textWrites
    member _.WidthReads = widthReads
    member _.Count = count
    member _.First = first
    member _.CacheCount = cache.Count

    member _.Edit(text: string) =
        match document with
        | Some buffer ->
            let line = buffer.Snapshot.Line first

            buffer.Replace (
                { Line = first; Column = 0 },
                {
                    Line = first
                    Column = line.Text.Length
                },
                text
            )
        | None ->
            let row = ordered[first - start]
            let value = $"line {first}: {text}"

            if row.Text.Value <> value then
                phases.Mutate (fun () -> graph.Run (fun () -> Api.batch (fun () -> row.Text.Value <- value)))
                row.Revision <- row.Revision + 1L
                textWrites <- textWrites + 1L

    member _.MoveTo line =
        requested <- clampFirst line

        if not pinned then
            first <- requested
            refresh ()

    member _.PinComposition() =
        pinned <- true

    member _.EndComposition() =
        pinned <- false
        first <- requested
        refresh ()

    member _.Configure(newWidth, newHeight, newScale, newWrap) =
        if
            (width, height, scale, wrap)
            <> (newWidth, newHeight, newScale, newWrap)
        then
            width <- newWidth
            height <- newHeight
            scale <- newScale
            wrap <- newWrap
            styleRevision <- styleRevision + 1L

            phases.Mutate (fun () ->
                container.setAttribute (
                    "style",
                    $"height:{height}px;width:{width}px;overflow:hidden;font:14px monospace;line-height:20px;white-space:"
                    + (if wrap then "pre-wrap" else "pre")
                ))

            requested <- clampFirst requested

            if not pinned then
                first <- requested
                refresh ()

    member _.Measure() =
        cache.Configure
            {
                FontRevision = fontRevision
                DeviceScale = scale
                Wrap = wrap
                Width = width
                Height = height
            }

        let mutable offset = 0.

        phases.Measure (fun () ->
            for index in 0 .. ordered.Length - 1 do
                let row = ordered[index]
                let node = nodesBySlot[row.Slot]

                let key =
                    {
                        Text = $"{row.Slot}:{row.Revision}"
                        Font = "14px monospace"
                        Width = width
                        StyleRevision = styleRevision
                    }

                let extent =
                    cache.Measure (
                        key,
                        fun () ->
                            widthReads <- widthReads + 1L
                            float (if wrap then node.offsetHeight else node.scrollWidth)
                    )

                if index < first - start then
                    offset <- offset + (if wrap then extent else 20.))

        if wrap then
            phases.Mutate (fun () -> container.scrollTop <- int offset)

    interface IDisposable with
        member _.Dispose() =
            documentEvents
            |> Option.iter (fun subscription -> subscription.Dispose ())

            fontEvents.Dispose ()
            fonts.Dispose ()
            mounted.Dispose ()
            graph.Dispose ()
