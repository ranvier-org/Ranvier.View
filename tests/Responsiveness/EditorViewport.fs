module Responsiveness.EditorViewport

open System
open Ranvier
open Ranvier.Dom
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore
open Responsiveness.ViewportModel

type private Row = { Slot: int; Text: Signal<string> }

let private row =
    Component.define (fun _ (item: unit -> Row) -> item ()) (fun t ->
        t.element (HtmlTag "div") [| t.Attrs.className "editor-line" |] [| t.text (t.from (fun (row: Row) -> row.Text)) |])

let private rows =
    Component.define (fun _ (items: Signal<Row array>) -> items) (fun t ->
        t.element (HtmlTag "div") [||] [| t.each (fun (row: Row) -> row.Slot) (fun items -> items.Value) row |])

type EditorViewport(host: WebCoreHost, container: HTMLElement, total: int, ?unbounded: bool, ?textKind: string) =
    do
        if total < 1 then
            invalidArg "total" "positive line count required"

    let graph = new Graph ()
    let phases = Frames.layoutForHost host
    let visible = 25

    let count =
        if defaultArg unbounded false then
            total
        else
            min total (visible + 100)

    let slots =
        graph.Run (fun () ->
            Array.init count (fun slot ->
                {
                    Slot = slot
                    Text = Api.createSignal ""
                }))

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

    let text line =
        match defaultArg textKind "ordinary" with
        | "long-line" -> $"line {line}: " + String.replicate 512 "abcdef "
        | "bidi" -> $"line {line}: العربية עברית abc 日本語"
        | _ -> $"line {line}: let value = {line}"

    let mutable start = 0

    let clampFirst line =
        max 0 (min (total - min count (max 1 (height / 20))) line)

    let refresh () =
        start <- min (max 0 (total - count)) (max 0 (first - 50))

        phases.Mutate (fun () ->
            graph.Run (fun () ->
                Api.batch (fun () ->
                    for index in 0 .. slots.Length - 1 do
                        slots[index].Text.Value <- text (start + index))))

        container.scrollTop <- (first - start) * 20

    do
        container.setAttribute ("style", "height:500px;width:700px;overflow:hidden;font:14px monospace;line-height:20px;white-space:pre")
        refresh ()

    member _.Count = count
    member _.First = first
    member _.CacheCount = cache.Count

    member _.Edit(text: string) =
        phases.Mutate (fun () -> graph.Run (fun () -> Api.batch (fun () -> slots[first - start].Text.Value <- $"line {first}: {text}")))

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

        let nodes = container.querySelectorAll ".editor-line"

        phases.Measure (fun () ->
            for index in 0 .. int nodes.length - 1 do
                let node = nodes.item (uint32 index) :?> HTMLElement

                let key =
                    {
                        Text = node.textContent
                        Font = "14px monospace"
                        Width = width
                        StyleRevision = styleRevision
                    }

                cache.Measure (key, fun () -> float node.scrollWidth)
                |> ignore)

    interface IDisposable with
        member _.Dispose() =
            fontEvents.Dispose ()
            fonts.Dispose ()
            mounted.Dispose ()
            graph.Dispose ()
