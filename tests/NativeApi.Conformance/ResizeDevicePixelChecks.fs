module internal NativeApiConformance.ResizeDevicePixelChecks

open System
open System.Collections.Generic
open Ranvier.Dom

let private snapshot name (entry: ResizeObserverEntry) =
    let sizes (values: ResizeObserverSize array) =
        values |> Array.collect (fun size -> [| size.InlineSize; size.BlockSize |])

    let rect = entry.ContentRect
    name, entry.Target.id,
        Array.concat [| [| rect.X; rect.Y; rect.Width; rect.Height |]; sizes entry.ContentBoxSize; sizes entry.BorderBoxSize; sizes entry.DevicePixelContentBoxSize |]

/// <summary>Runs the eight measured device-pixel cases, or verifies rejection by a capability-false runtime.</summary>
let run (document: Document) (create: unit -> ResizeObservationService) (update: unit -> Async<unit>) scale supported =
    async {
        let mutable checks = 0
        let expect label condition =
            checks <- checks + 1
            if not condition then failwith label

        use capability = create ()
        expect "device-pixel support matches the independently queried host capability"
            (capability.Supports ResizeObserverBox.DevicePixelContentBox = supported)

        for case in ResizeDevicePixelData.cases scale do
            let root = document.createElement "div" :?> HTMLElement
            root.style.cssText <- "position:absolute;left:0;top:0"
            let html = ResizeDevicePixelData.markup
            let body = html.Substring(html.IndexOf("<body>") + 6)
            let body = body.Substring(0, body.IndexOf("</body>"))
            let style = html.Substring(html.IndexOf("<style>") + 7)
            let style = style.Substring(0, style.IndexOf("</style>"))
            root.innerHTML <- "<style>" + style + "</style>" + body
            document.body.appendChild root |> ignore

            let elements = Dictionary<string, Element>()
            for id in [ "a"; "v"; "z"; "f"; "bb"; "inl"; "hid"; "svg"; "r"; "outer"; "mid"; "deep" ] do
                elements.Add(id, root.querySelector("#" + id))

            use service = create ()
            let observers = Dictionary<string, ResizeObserver>()
            let log = List<string>()
            let entries = List<ResizeDevicePixelData.Entry>()
            let mutable rejected = 0

            try
                for step in case.Steps do
                    match step[0] with
                    | "element" ->
                        let element = document.createElement "div"
                        element.id <- step[1]
                        element.setAttribute("style", step[2])
                        elements.Add(step[1], element)
                    | "append" ->
                        let parent = if step.Length = 3 then elements[step[2]] else root :> Element
                        parent.appendChild elements[step[1]] |> ignore
                    | "attribute" -> elements[step[1]].setAttribute(step[2], step[3])
                    | "style" ->
                        let element = elements[step[1]]
                        let previous = element.getAttribute "style"
                        let previous = if isNull previous then "" else previous
                        element.setAttribute("style", previous + ";" + step[2] + ":" + step[3])
                    | "create" ->
                        let name = step[1]
                        observers.Add(name, service.Create(fun delivered ->
                            log.Add(name + ":" + String.Join(",", delivered |> Array.map(fun entry -> entry.Target.id)))
                            for entry in delivered do entries.Add(snapshot name entry)))
                    | "observe" ->
                        let box =
                            match step[3] with
                            | "content-box" -> ResizeObserverBox.ContentBox
                            | "border-box" -> ResizeObserverBox.BorderBox
                            | "device-pixel-content-box" -> ResizeObserverBox.DevicePixelContentBox
                            | value -> failwith ("Unknown resize box " + value)
                        if box = ResizeObserverBox.DevicePixelContentBox && not supported then
                            let mutable threw = false
                            try observers[step[1]].Observe(elements[step[2]], box)
                            with _ -> threw <- true
                            expect (case.Name + " rejects unsupported device-pixel observation") threw
                            rejected <- rejected + 1
                        else
                            observers[step[1]].Observe(elements[step[2]], box)
                    | "update" ->
                        do! update ()
                        log.Add "|"
                    | value -> failwith ("Unknown resize step " + value)

                if supported then
                    expect (case.Name + " delivery log at scale " + string scale + ": " + String.Join(" | ", log))
                        (log.ToArray() = case.Log)
                    expect (case.Name + " entry count") (entries.Count = case.Entries.Length)
                    for index in 0 .. case.Entries.Length - 1 do
                        let eo, et, expected = case.Entries[index]
                        let ao, at, actual = entries[index]
                        expect (case.Name + " entry " + string index + " at scale " + string scale + ": " + String.Join(",", actual))
                            (eo = ao && et = at && expected.Length = actual.Length
                             && Array.forall2 (fun e a -> abs(e - a) < 0.000001) expected actual)
                else
                    expect (case.Name + " exercises the unsupported box") (rejected > 0)
                    for _, _, actual in entries do
                        expect (case.Name + " baseline returns empty device-pixel sizes") (actual.Length = 8)
            finally
                service.Dispose ()
                root.remove ()

        return checks
    }

/// <summary>Checks actual host scale changes notify only the device-pixel observer while CSS dimensions stay fixed.</summary>
let runScaleChanges (document: Document) (create: unit -> ResizeObservationService) (update: unit -> Async<unit>) (setScale: float -> unit) =
    async {
        let element = document.createElement "div" :?> HTMLElement
        element.style.cssText <- "position:absolute;left:0;top:0;width:100.25px;height:33.25px"
        document.body.appendChild element |> ignore
        use service = create ()
        let content = List<ResizeObserverEntry>()
        let device = List<ResizeObserverEntry>()
        use c = service.Create(fun entries -> content.AddRange entries)
        use d = service.Create(fun entries -> device.AddRange entries)
        try
            setScale 1.
            c.Observe(element, ResizeObserverBox.ContentBox)
            d.Observe(element, ResizeObserverBox.DevicePixelContentBox)
            do! update ()
            setScale 1.5
            do! update ()
            setScale 2.
            do! update ()
            if content.Count <> 1 || device.Count <> 3 then
                failwith "A real device-scale change must notify only the device-pixel observer"
            let expected = [| 100., 33.; 150., 50.; 201., 67. |]
            for index in 0 .. 2 do
                let entry = device[index]
                let size = entry.DevicePixelContentBoxSize
                let inlineSize, blockSize = expected[index]
                if entry.ContentRect.Width <> 100.25 || entry.ContentRect.Height <> 33.25
                   || size.Length <> 1 || size[0].InlineSize <> inlineSize || size[0].BlockSize <> blockSize then
                    failwith ("Unexpected entry after real device-scale change " + string index)
            return 4
        finally
            element.remove ()
    }
