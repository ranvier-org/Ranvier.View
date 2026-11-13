namespace Ranvier.View.WebCore.Hmr

// The dev host: `dotnet run -- dev [-hmr alc|fsi|fine] [-nochecker] [-bench scenario.json]`.
// Owns the SDL window and WebCore page for the whole session; reloads swap code underneath them.

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading
open Ranvier
open Ranvier.Dom
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Sdl

type Strategy =
    | Alc
    | Fsi
    | Fine

type Report =
    {
        Seq: int
        Strategy: string
        Files: string
        /// How the change was applied: remount, patch, unchanged, fallback, error, skipped.
        Path: string
        Detail: string
        DetectMs: float
        CheckMs: float
        DeliverMs: float
        mutable ApplyMs: float
        mutable TotalMs: float
        /// Stopwatch timestamp of the first change seen in this batch.
        Started: int64
    }

module private Time =
    let ms (from: int64) (until: int64) =
        float (until - from) * 1000.0
        / float Stopwatch.Frequency

    let now () =
        Stopwatch.GetTimestamp ()

type private Step =
    {
        op: string
        name: string
        selector: string
        text: string
        contains: string
        attr: string
        file: string
        find: string
        replace: string
        find2: string
        replace2: string
        times: int
        value: int
        error: bool
    }

type private StepResult =
    {
        Name: string
        Ok: bool
        Detail: string
        Report: Report option
    }

type internal DevHost(argv: string array, title: string, width: int, height: int, root: TemplateComponent, propsOf: Window -> obj, callerFile: string)
    =
    let arg name =
        match Array.tryFindIndex ((=) name) argv with
        | Some i when i + 1 < argv.Length -> Some argv[i + 1]
        | _ -> None

    let strategy =
        match arg "-hmr" with
        | Some "alc" -> Alc
        | Some "fsi" -> Fsi
        | _ -> Fine

    let useChecker = not (Array.contains "-nochecker" argv)

    /// Declaration stamps the fingerprinting dev compiler embedded: file name -> stamps. Empty for an fsc build.
    let embedded =
        Reflection.Assembly.GetEntryAssembly().GetCustomAttributes(typeof<Reflection.AssemblyMetadataAttribute>, false)
        |> Seq.cast<Reflection.AssemblyMetadataAttribute>
        |> Seq.filter (fun a -> a.Key.StartsWith Stamps.prefix)
        |> Seq.map (fun a ->
            let rest = a.Key.Substring Stamps.prefix.Length
            let i = rest.IndexOf ':'
            let parts = a.Value.Split '|'

            rest.Substring (0, i),
            {
                Stamps.Key = rest.Substring (i + 1)
                Stamps.Hash = parts[0]
                Stamps.StartLine = int parts[1]
                Stamps.EndLine = int parts[2]
            })
        |> Seq.groupBy fst
        |> Seq.map (fun (file, xs) -> file, xs |> Seq.map snd |> Seq.toArray)
        |> dict

    let stampedBuild =
        Reflection.Assembly.GetEntryAssembly().GetCustomAttributes(typeof<Reflection.AssemblyMetadataAttribute>, false)
        |> Seq.cast<Reflection.AssemblyMetadataAttribute>
        |> Seq.exists (fun a -> a.Key = Stamps.compilerKey && a.Value = "fickle")

    // A build compiled by the same compiler as FSI needs no FSI baseline; `-baseline` forces one anyway.
    let skipBaseline =
        (stampedBuild
         && not (Array.contains "-baseline" argv))
        || Array.contains "-nobaseline" argv

    let started = Time.now ()
    let project = Delivery.loadProject callerFile
    let registry = Remount.Registry ()
    let remounter = Remount.Remounter registry
    let engine = Fine.Engine registry
    let ui = ConcurrentQueue<unit -> unit>()
    let work = new BlockingCollection<string list * int64> ()
    let pending = ConcurrentDictionary<string, int64>()
    let lastText = Dictionary<string, string>()
    /// The text of each file as last applied successfully: type-change decisions compare against it.
    let applied = Dictionary<string, string>()
    let mutable gateReady = false
    let mutable driftFiles: string list = []
    /// Worker thread: component keys seen, and the module that last evaluated each module-level value.
    let componentKeys = HashSet<string>()
    let definedIn = Dictionary<struct (string * string), string>()
    /// Worker thread: the stamps of each file as last applied, the module each module name last evaluated into,
    /// and the files whose FSI baseline exists.
    let appliedStamps = Dictionary<string, Stamps.Stamp array>()
    let latestModule = Dictionary<string, string>()
    let baselined = HashSet<string>()
    let lazyBaseline = not (Array.contains "-nolazybaseline" argv)
    let reports = ResizeArray<Report>()
    let reportArrived = new AutoResetEvent false
    let mutable seq = 0
    let mutable ready = false
    let mutable window: Window = Unchecked.defaultof<_>
    let mutable graph: Graph = Unchecked.defaultof<_>
    let mutable mounted: Mounting.Mounted option = None
    let mutable container: Element = Unchecked.defaultof<_>
    let mutable awaitingPresent: Report option = None
    let mutable rootKey = ""
    /// The root's props, built from the window once it exists (a GPU context lives as long as the window).
    let mutable props: obj = null
    let mutable generation = 0

    let alc = lazy (Delivery.AlcDelivery project)
    let fsi = lazy (Delivery.FsiDelivery project)
    let gate = lazy (Delivery.CheckerGate project)

    let stampContext =
        lazy (Stamps.context project.Fsproj (Array.ofList (Delivery.compilerArgs project @ project.Sources)))

    let log (s: string) =
        Console.WriteLine ("[hmr] " + s)

    // -----------------------------------------------------------------------------------------------------------
    // Mounting

    let unmount () =
        match mounted with
        | Some m ->
            graph.Run (fun () -> m.Dispose ())

            window.Host.Document.body.removeChild container
            |> ignore

            mounted <- None
        | None -> ()

    let mountDefinition (definition: TemplateComponent) =
        let doc = window.Host.Document
        container <- doc.createElement "div"
        doc.body.appendChild container |> ignore

        let m =
            Mounting.mount graph None (fun errors -> raise (AggregateException (errors: exn seq))) container None (fun context ->
                Program.run (Reflect.view definition props) context.Backend)

        mounted <- Some m

    let remount () =
        let sw = Stopwatch.StartNew ()

        let definition =
            match strategy with
            | Fine ->
                engine.Reset ()
                graph.Run (fun () -> engine.Instrument ((registry.Latest rootKey).Value, rootKey))
            | _ -> remounter.Prepare rootKey

        unmount ()
        mountDefinition definition

        let outcome =
            if strategy = Fine then
                engine.Outcome
            else
                remounter.Outcome

        sw.Elapsed.TotalMilliseconds, $"state: {outcome.Copied} signals kept ({outcome.Converted} via JSON, {outcome.Skipped.Count} lost)"

    // -----------------------------------------------------------------------------------------------------------
    // Reload pipeline (worker thread), applied on the window thread

    let publish (report: Report) =
        lock reports (fun () -> reports.Add report)

        log (
            $"#{report.Seq} {report.Strategy} {report.Files}: {report.Path} | detect {report.DetectMs:F0} check {report.CheckMs:F0} "
            + $"deliver {report.DeliverMs:F0} apply {report.ApplyMs:F1} total {report.TotalMs:F0} ms | {report.Detail}"
        )

        window.Title <- $"{title} | HMR {report.Strategy}: {report.Path} in {report.TotalMs:F0} ms"
        reportArrived.Set () |> ignore

    let post (report: Report) (apply: unit -> string * string) =
        ui.Enqueue (fun () ->
            let sw = Stopwatch.StartNew ()

            let path, detail =
                try
                    apply ()
                with e ->
                    "error", e.ToString ()

            report.ApplyMs <- sw.Elapsed.TotalMilliseconds

            awaitingPresent <-
                Some
                    { report with
                        Path = path
                        Detail = report.Detail + " " + detail
                    })

    let fail (report: Report) (errors: string list) =
        let r =
            { report with
                Path = "error"
                Detail = String.Join (" | ", errors |> List.truncate 3)
            }

        ui.Enqueue (fun () -> awaitingPresent <- Some r)

    let register (components: (string * TemplateComponent) list) =
        for (k, d) in components do
            registry.Register (k, d)

    let deliverAlc (report: Report) =
        match alc.Value.Compile () with
        | Ok c ->
            post
                { report with
                    DeliverMs = c.CompileMs + c.LoadMs
                    Detail = $"compile {c.CompileMs:F0} load {c.LoadMs:F0} ms;"
                }
                (fun () ->
                    register c.Components
                    let ms, state = remount ()
                    "remount", state)
        | Error errors -> fail report errors

    let deliverFsi (report: Report) (files: string list) =
        let sw = Stopwatch.StartNew ()

        let results = files |> List.map fsi.Value.Load

        match
            results
            |> List.choose (function
                | Error e -> Some e
                | _ -> None)
        with
        | [] ->
            let components =
                results
                |> List.collect (function
                    | Ok e -> e.Components
                    | _ -> [])

            post
                { report with
                    DeliverMs = sw.Elapsed.TotalMilliseconds
                    Detail = $"eval {components.Length} components;"
                }
                (fun () ->
                    register components
                    let _, state = remount ()
                    "remount", state)
        | errors -> fail report (List.concat errors)

    /// Evaluates a file's lets in a shadow module that opens the module already in use.
    let shadowLoad
        (f: string)
        (moduleName: string)
        (decls: Delivery.Declaration list)
        (aliasOf: Delivery.Declaration -> string option)
        (abbreviations: (string * string) list)
        (live: bool)
        =
        generation <- generation + 1
        let shadowDir = Path.Combine (project.Dir, "obj", "hmr", "fine")
        Directory.CreateDirectory shadowDir |> ignore

        let shadow =
            Path.Combine (shadowDir, $"{Path.GetFileNameWithoutExtension f}__hmr{generation}.fs")

        File.WriteAllText (shadow, Delivery.shadowModule f moduleName generation decls aliasOf abbreviations)
        let result = fsi.Value.Load shadow

        match result with
        | Ok e when live ->
            for (k, _) in e.Components do
                componentKeys.Add k |> ignore

            for d in decls do
                if
                    d.Kind = "let"
                    && d.IsValue
                    && not (isNull d.Name)
                    && (aliasOf d).IsNone
                then
                    definedIn[struct (f, d.Name)] <- $"{moduleName}__hmr{generation}"

            latestModule[moduleName] <- $"{moduleName}__hmr{generation}"
        | _ -> ()

        result

    /// The aliasing a reload applies to a file with no changed value: a baseline must compile the same shape, or its
    /// closures' IL differs from the first edit's (an alias getter in place of a static field).
    let unchangedAlias (f: string) (moduleName: string) (d: Delivery.Declaration) =
        if
            d.IsValue
            && d.IsPublic
            && not d.IsMutable
            && not (isNull d.Name)
            && not (componentKeys.Contains (moduleName + "." + d.Name))
        then
            match definedIn.TryGetValue (struct (f, d.Name)) with
            | true, m -> Some m
            | _ -> Some moduleName
        else
            None

    /// The FSI baseline of one file, the first time it is touched: fingerprints of its mounted code as FSI
    /// compiles it, so its first edit compares FSI output with FSI output (reloaded code always sits in another
    /// assembly than the types it uses, so its IL never matches the app's own compile).
    let baselineOf (f: string) (text: string) =
        if baselined.Add f then
            match Delivery.declarations f text with
            | Some (moduleName, decls) ->
                match shadowLoad f moduleName decls (unchangedAlias f moduleName) [] false with
                | Ok e ->
                    use done' = new ManualResetEventSlim false

                    ui.Enqueue (fun () ->
                        try
                            for (k, d) in e.Components do
                                registry.Alias (k, d)

                            graph.Run (fun () ->
                                for (k, d) in e.Components do
                                    engine.Rebaseline (k, d) |> ignore)
                        finally
                            done'.Set ())

                    done'.Wait ()
                    Some e.EvalMs
                | Error _ -> None
            | None -> None
        else
            None

    /// Evaluates one file: its lets in a shadow module when only values and opens changed, the whole file otherwise.
    let evaluate (f: string) (text: string) (changedKeys: string list) (abbreviate: bool) =
        let structural =
            changedKeys
            |> List.exists (fun k -> not (k.EndsWith ":Value" || k.EndsWith ":Open"))

        match Delivery.declarations f text with
        | Some (moduleName, decls) when not structural ->
            let changedNames =
                changedKeys
                |> Seq.map (fun k -> k.Substring (0, k.LastIndexOf ':'))
                |> HashSet

            let aliasOf (d: Delivery.Declaration) =
                if
                    d.IsValue
                    && d.IsPublic
                    && not d.IsMutable
                    && not (isNull d.Name)
                    && not (componentKeys.Contains (moduleName + "." + d.Name))
                    && not (changedNames.Contains (moduleName + "." + d.Name))
                then
                    match definedIn.TryGetValue (struct (f, d.Name)) with
                    | true, m -> Some m
                    | _ -> Some moduleName
                else
                    None

            let abbreviations =
                if abbreviate then
                    [
                        for KeyValue (m, latest) in latestModule do
                            if m <> moduleName && latest <> m then
                                yield (m, latest)
                    ]
                else
                    []

            let aliased =
                decls
                |> List.filter (fun d -> d.Kind = "let" && (aliasOf d).IsSome)
                |> List.map _.Name

            let note =
                if aliased.IsEmpty then
                    ""
                else
                    $" (kept {String.Join (',', aliased)})"

            let result =
                match shadowLoad f moduleName decls aliasOf abbreviations true with
                | Error _ when not abbreviations.IsEmpty -> shadowLoad f moduleName decls aliasOf [] true
                | r -> r

            result, "lets-only eval" + note, false
        | parsed ->
            let result = fsi.Value.Load f

            match result, parsed with
            | Ok e, Some (moduleName, decls) ->
                for (k, _) in e.Components do
                    componentKeys.Add k |> ignore

                for d in decls do
                    if d.Kind = "let" && d.IsValue && not (isNull d.Name) then
                        definedIn[struct (f, d.Name)] <- moduleName

                latestModule[moduleName] <- moduleName
            | _ -> ()

            result, "whole-file eval (types changed)", true

    /// Declaration stamps decide what changed; the checker's TypedUses decide which dependents (in any file) must
    /// be re-evaluated and patched; IL fingerprints (FSI to FSI) decide which closures inside a changed component
    /// rebind. Components that neither changed nor depend on a change are not touched at all.
    let deliverFine (report: Report) (files: string list) (previous: Map<string, string>) (drift: bool) =
        let mutable checkMs = 0.0
        let mutable baselineMs = 0.0
        let notes = ResizeArray<string>()
        let loads = ResizeArray<string * Result<Delivery.Evaluated, string list> * bool>()
        let mutable blocked = false
        let accepted = ResizeArray<string * string * Stamps.Stamp array>()
        let changedSet = HashSet<struct (string * string)>()

        for f in files do
            let text = File.ReadAllText f
            let fresh = Stamps.ofText stampContext.Value f text
            let changedKeys = Stamps.changed appliedStamps[f] fresh

            if changedKeys.IsEmpty then
                // Only text between declarations changed (comments, blank lines): nothing to evaluate.
                notes.Add $"{Path.GetFileName f}: no declaration changed (stamps)"
                accepted.Add ((f, text, fresh))
            else
                for k in changedKeys do
                    changedSet.Add (struct (Path.GetFileName f, k))
                    |> ignore

                // The checker gate runs beside the evaluation; its verdict decides whether the evaluation is used.
                let verdict =
                    if useChecker && gateReady then
                        Some (Tasks.Task.Run (fun () -> gate.Value.Check (f, text)))
                    else
                        None

                if lazyBaseline && not drift then
                    match baselineOf f previous[f] with
                    | Some ms -> baselineMs <- baselineMs + ms
                    | None -> ()

                let load, how, wholeFile = evaluate f text changedKeys false

                let keys =
                    changedKeys
                    |> List.map (fun k -> k.Substring (k.LastIndexOf '.' + 1))
                    |> List.truncate 4

                let what = $"{Path.GetFileName f}: changed {String.Join (',', keys)}; {how}"

                match verdict |> Option.map _.Result with
                | Some g when not g.Errors.IsEmpty ->
                    checkMs <- checkMs + g.CheckMs
                    blocked <- true
                    fail { report with CheckMs = checkMs } g.Errors
                | Some g ->
                    checkMs <- checkMs + g.CheckMs
                    notes.Add what
                    loads.Add ((f, load, wholeFile))

                    if Result.isOk load then
                        accepted.Add ((f, text, fresh))
                | None ->
                    notes.Add what
                    loads.Add ((f, load, wholeFile))

                    if Result.isOk load then
                        accepted.Add ((f, text, fresh))

        // Dependents through types and values (not through components, which are found by key).
        let dependents =
            if
                blocked
                || not (useChecker && gateReady)
                || changedSet.Count = 0
            then
                None
            else
                let isComponent (stampKey: string) =
                    stampKey.EndsWith ":Value"
                    && componentKeys.Contains (stampKey.Substring (0, stampKey.Length - 6))

                Some (gate.Value.Dependents (changedSet, isComponent))

        let forced =
            match dependents with
            | Some _ ->
                let values = HashSet<struct (string * string)>()

                for f in files do
                    match Delivery.declarations f (File.ReadAllText f) with
                    | Some (moduleName, decls) ->
                        for d in decls do
                            let key = struct (Path.GetFileName f, $"{moduleName}.{d.Name}:Value")

                            if
                                d.IsValue
                                && not (isNull d.Name)
                                && changedSet.Contains key
                                && not (componentKeys.Contains $"{moduleName}.{d.Name}")
                            then
                                values.Add key |> ignore
                    | None -> ()

                if values.Count = 0 then
                    HashSet ()
                else
                    gate.Value.Dependents (values, (fun _ -> false))
            | None -> HashSet ()

        match dependents with
        | Some deps when
            loads
            |> Seq.forall (fun (_, l, _) -> Result.isOk l)
            ->
            let names =
                deps
                |> Seq.map (fun (struct (file, _)) -> file)
                |> HashSet

            for d in project.Sources do
                if
                    d <> project.Entry
                    && not (List.contains d files)
                    && names.Contains (Path.GetFileName d)
                then
                    if lazyBaseline && not drift then
                        match baselineOf d applied[d] with
                        | Some ms -> baselineMs <- baselineMs + ms
                        | None -> ()

                    let load, how, wholeFile = evaluate d (File.ReadAllText d) [] true

                    let keys =
                        deps
                        |> Seq.filter (fun (struct (file, _)) -> file = Path.GetFileName d)
                        |> Seq.map (fun (struct (_, k)) -> k.Substring (k.LastIndexOf '.' + 1))

                    notes.Add $"{Path.GetFileName d}: dependent {String.Join (',', keys)}; {how}"
                    loads.Add ((d, load, wholeFile))
        | _ -> ()

        if not blocked then
            for (f, text, stamps) in accepted do
                applied[f] <- text
                appliedStamps[f] <- stamps

        let shouldPatch (file: string) (componentKey: string) =
            match dependents with
            | None -> true
            | Some deps ->
                let key = struct (Path.GetFileName file, componentKey + ":Value")
                changedSet.Contains key || deps.Contains key

        if not blocked then
            match
                loads
                |> Seq.choose (fun (_, l, _) ->
                    match l with
                    | Error e -> Some e
                    | _ -> None)
                |> Seq.toList
            with
            | [] when loads.Count = 0 ->
                post
                    { report with
                        CheckMs = checkMs
                        Detail = String.Join ("; ", notes)
                    }
                    (fun () -> "unchanged", "")
            | [] ->
                let all =
                    loads
                    |> Seq.collect (fun (f, l, whole) ->
                        match l with
                        | Ok e ->
                            e.Components
                            |> List.map (fun (k, d) -> f, k, d, whole)
                        | _ -> [])
                    |> Seq.toList

                let patchable =
                    all
                    |> List.filter (fun (f, k, _, _) -> shouldPatch f k)

                let skipped = all.Length - patchable.Length

                let deliverMs =
                    loads
                    |> Seq.sumBy (fun (_, l, _) ->
                        match l with
                        | Ok e -> e.EvalMs
                        | _ -> 0.0)

                let baselineNote =
                    if baselineMs > 0.0 then
                        $" first-touch baseline {baselineMs:F0} ms;"
                    else
                        ""

                post
                    { report with
                        CheckMs = checkMs
                        DeliverMs = deliverMs + baselineMs
                        Detail = String.Join ("; ", notes) + ";" + baselineNote
                    }
                    (fun () ->
                        // Every new definition's key is known (child lookups); only patched ones become the latest.
                        for (_, k, d, _) in all do
                            registry.Alias (k, d)

                        for (_, k, d, _) in patchable do
                            registry.Register (k, d)
                            engine.MarkReloaded k

                        let rebind (f: string) (k: string) =
                            forced.Contains (struct (Path.GetFileName f, k + ":Value"))

                        let results =
                            graph.Run (fun () ->
                                patchable
                                |> List.map (fun (f, k, d, whole) -> k, engine.Patch (k, d, whole || rebind f k)))

                        let untouched =
                            if skipped > 0 then
                                $"; {skipped} unchanged components not touched"
                            else
                                ""

                        match
                            results
                            |> List.tryPick (fun (_, r) ->
                                match r with
                                | Fine.Structural why -> Some why
                                | _ -> None)
                        with
                        | Some why ->
                            let _, state = remount ()
                            "fallback-remount", $"{why}; {state}"
                        | None ->
                            let patched =
                                results
                                |> List.choose (fun (k, r) ->
                                    match r with
                                    | Fine.Patched (s, f, h, rebound, bridged) ->
                                        Some
                                            $"{k.Substring (k.LastIndexOf '.' + 1)}: {s} static, {f} reader, {h} handler changed; {rebound} rebound, {bridged} bridged"
                                    | _ -> None)

                            if patched.IsEmpty then
                                "unchanged", untouched.TrimStart (';', ' ')
                            else
                                "patch", String.Join ("; ", patched) + untouched)
            | errors -> fail { report with CheckMs = checkMs } (List.concat errors)

    let processBatch (files: string list) (startedAt: int64) =
        let detected = Time.now ()

        let changed =
            files
            |> List.filter (fun f ->
                let text =
                    let rec read n =
                        try
                            File.ReadAllText f
                        with :? IOException when n > 0 ->
                            Thread.Sleep 5
                            read (n - 1)

                    read 20

                match lastText.TryGetValue f with
                | true, t when t = text -> false
                | _ -> true)

        if not changed.IsEmpty then
            let previous =
                changed
                |> List.map (fun f -> f, applied[f])
                |> Map.ofList

            for f in changed do
                lastText[f] <- File.ReadAllText f

            seq <- seq + 1

            let report =
                {
                    Seq = seq
                    Strategy = (sprintf "%A" strategy).ToLowerInvariant()
                    Files = String.Join (",", changed |> List.map Path.GetFileName)
                    Path = ""
                    Detail = ""
                    DetectMs = Time.ms startedAt detected
                    CheckMs = 0.0
                    DeliverMs = 0.0
                    ApplyMs = 0.0
                    TotalMs = 0.0
                    Started = startedAt
                }

            try
                match strategy with
                | Alc -> deliverAlc report
                | Fsi -> deliverFsi report changed
                | Fine -> deliverFine report changed previous false
            with e ->
                fail report [ e.ToString () ]

    let warm () =
        let sw = Stopwatch.StartNew ()
        let parts = ResizeArray<string>()

        match strategy with
        | Alc ->
            match alc.Value.Compile () with
            | Ok c -> parts.Add $"first compile {c.CompileMs:F0} ms"
            | Error e -> parts.Add $"first compile failed: {e}"
        | Fsi
        | Fine ->
            parts.Add $"fsi warm {fsi.Value.Warm ():F0} ms"

            if strategy = Fine && skipBaseline then
                let why =
                    if stampedBuild then
                        "stamped fickle build"
                    else
                        "-nobaseline"

                parts.Add $"no FSI baseline ({why}, {embedded.Count} stamped files)"
            elif strategy = Fine then
                // FSI baseline: fingerprints of the mounted code as FSI compiles it, so the first edit of each
                // file compares FSI output with FSI output.
                let baseline = Stopwatch.StartNew ()
                let mutable rebased = 0

                for f in project.Sources do
                    if f <> project.Entry then
                        match Delivery.declarations f (File.ReadAllText f) with
                        | Some (moduleName, decls) ->
                            match shadowLoad f moduleName decls (unchangedAlias f moduleName) [] false with
                            | Ok e ->
                                baselined.Add f |> ignore
                                use done' = new ManualResetEventSlim false

                                ui.Enqueue (fun () ->
                                    try
                                        for (k, d) in e.Components do
                                            registry.Alias (k, d)

                                        graph.Run (fun () ->
                                            for (k, d) in e.Components do
                                                if engine.Rebaseline (k, d) then
                                                    rebased <- rebased + 1)
                                    finally
                                        done'.Set ())

                                done'.Wait ()
                            | Error e -> parts.Add $"baseline of {Path.GetFileName f} failed: {List.head e}"
                        | None -> ()

                parts.Add $"fsi baseline {rebased} components {baseline.Elapsed.TotalMilliseconds:F0} ms"

            if strategy = Fine && useChecker then
                try
                    parts.Add $"checker ready {gate.Value.Start ():F0} ms"
                    gateReady <- true
                with e ->
                    parts.Add $"checker failed ({e.Message}); continuing without it"

            if strategy = Fine then
                // Stamps of the running code: embedded by the dev compiler, or computed from the sources it started from.
                let st = Stopwatch.StartNew ()
                let drifted = ResizeArray<string>()

                for f in project.Sources do
                    let onDisk = Stamps.ofFile stampContext.Value f

                    match embedded.TryGetValue (Path.GetFileName f) with
                    | true, stamps ->
                        appliedStamps[f] <- stamps

                        if
                            f <> project.Entry
                            && not (Stamps.changed stamps onDisk).IsEmpty
                        then
                            drifted.Add f
                    | _ -> appliedStamps[f] <- onDisk

                parts.Add $"stamps {st.Elapsed.TotalMilliseconds:F0} ms"

                if drifted.Count > 0 then
                    // The binary is older than its sources: hot reload the difference before the first edit.
                    parts.Add $"{drifted.Count} files newer than the build"
                    driftFiles <- List.ofSeq drifted

        let summary = String.Join (", ", parts)
        log $"ready for edits after {sw.Elapsed.TotalMilliseconds:F0} ms of warm-up ({summary})"
        ready <- true

    let workerLoop () =
        warm ()

        if not driftFiles.IsEmpty then
            seq <- seq + 1

            let report =
                {
                    Seq = seq
                    Strategy = "fine"
                    Files =
                        String.Join (",", driftFiles |> List.map Path.GetFileName)
                        + " (drift)"
                    Path = ""
                    Detail = ""
                    DetectMs = 0.0
                    CheckMs = 0.0
                    DeliverMs = 0.0
                    ApplyMs = 0.0
                    TotalMs = 0.0
                    Started = Time.now ()
                }

            deliverFine
                report
                driftFiles
                (driftFiles
                 |> List.map (fun f -> f, applied[f])
                 |> Map.ofList)
                true

        for (files, startedAt) in work.GetConsumingEnumerable () do
            processBatch files startedAt

    // -----------------------------------------------------------------------------------------------------------
    // Watching

    let flush =
        new Timer (fun _ ->
            let files =
                pending.Keys
                |> Seq.toList
                |> List.sortBy (fun f -> List.findIndex ((=) f) project.Sources)

            if not files.IsEmpty then
                let first =
                    files
                    |> List.map (fun f ->
                        match pending.TryRemove f with
                        | true, t -> t
                        | _ -> Int64.MaxValue)
                    |> List.min

                work.Add ((files, first)))

    let onChange (path: string) =
        let full = Path.GetFullPath path

        if
            List.contains full project.Sources
            && full <> project.Entry
        then
            pending.TryAdd (full, Time.now ()) |> ignore
            flush.Change (10, Timeout.Infinite) |> ignore

    // -----------------------------------------------------------------------------------------------------------
    // Bench: scripted edits against the live window

    let onUi (f: unit -> 'T) : 'T =
        let result = TaskCompletionSourceSlim<'T>()

        ui.Enqueue (fun () ->
            try
                result.Set (Ok (f ()))
            with e ->
                result.Set (Error e))

        result.Wait ()

    let runBench (path: string) =
        use doc = JsonDocument.Parse (File.ReadAllText path)

        let steps =
            [|
                for e in doc.RootElement.EnumerateArray () do
                    let str (n: string) =
                        match e.TryGetProperty n with
                        | true, v when v.ValueKind = JsonValueKind.String -> v.GetString ()
                        | _ -> null

                    let num (n: string) =
                        match e.TryGetProperty n with
                        | true, v when v.ValueKind = JsonValueKind.Number -> v.GetInt32 ()
                        | _ -> 0

                    let flag (n: string) =
                        match e.TryGetProperty n with
                        | true, v -> v.ValueKind = JsonValueKind.True
                        | _ -> false

                    yield
                        {
                            op = str "op"
                            name = str "name"
                            selector = str "selector"
                            text = str "text"
                            contains = str "contains"
                            attr = str "attr"
                            file = str "file"
                            find = str "find"
                            replace = str "replace"
                            find2 = str "find2"
                            replace2 = str "replace2"
                            times = num "times"
                            value = num "value"
                            error = flag "error"
                        }
            |]

        let originals = Dictionary<string, string>()
        let results = ResizeArray<StepResult>()

        while not ready do
            Thread.Sleep 50

        let query (selector: string) =
            window.Host.Document.querySelector selector

        let waitReport (since: int) =
            let deadline = Stopwatch.StartNew ()
            let mutable found = None

            while found.IsNone
                  && deadline.Elapsed.TotalSeconds < 60.0 do
                reportArrived.WaitOne 200 |> ignore

                found <-
                    lock reports (fun () ->
                        reports
                        |> Seq.tryFind (fun r ->
                            r.Seq > since && r.TotalMs > 0.0
                            || r.Seq > since && r.Path = "error"))

            found

        for step in steps do
            try
                match step.op with
                | "click" ->
                    for _ in 1 .. max 1 step.times do
                        onUi (fun () -> (unbox<HTMLElement>(query step.selector)).click())
                        Thread.Sleep 30

                    results.Add
                        {
                            Name = $"click {step.selector} x{max 1 step.times}"
                            Ok = true
                            Detail = ""
                            Report = None
                        }
                | "edit"
                | "revert" ->
                    let file = Path.Combine (project.Dir, step.file)

                    if not (originals.ContainsKey file) then
                        originals[file] <- File.ReadAllText file

                    let current = File.ReadAllText file
                    // Scenarios are written with \n; a source checked out with CRLF is matched in its own line endings.
                    let eol (text: string) =
                        if isNull text || not (current.Contains "\r\n") then
                            text
                        else
                            text.Replace("\r\n", "\n").Replace("\n", "\r\n")

                    let step =
                        { step with
                            find = eol step.find
                            replace = eol step.replace
                            find2 = eol step.find2
                            replace2 = eol step.replace2
                        }

                    let next =
                        if step.op = "revert" then
                            originals[file]
                        elif
                            current.Contains step.find
                            && (isNull step.find2 || current.Contains step.find2)
                        then
                            let once = current.Replace (step.find, step.replace)

                            if isNull step.find2 then
                                once
                            else
                                once.Replace (step.find2, step.replace2)
                        else
                            failwith $"bench: '{step.find}' not found in {step.file}"

                    let since = lock reports (fun () -> reports.Count |> fun _ -> seq)
                    File.WriteAllText (file, next)
                    let report = waitReport since

                    let ok, detail =
                        match report with
                        | Some r when r.Path = "error" && step.error -> true, $"expected error: {r.Detail}"
                        | Some r when r.Path <> "error" -> not step.error, $"{r.Path}: {r.Detail}"
                        | Some r -> false, r.Detail
                        | None -> false, "timed out waiting for reload"

                    results.Add
                        {
                            Name = step.name
                            Ok = ok
                            Detail = detail
                            Report = report
                        }
                | "expect" ->
                    let deadline = Stopwatch.StartNew ()
                    let mutable ok = false
                    let mutable seen = ""

                    while not ok && deadline.Elapsed.TotalSeconds < 3.0 do
                        seen <-
                            onUi (fun () ->
                                match query step.selector with
                                | null -> "<missing>"
                                | e when not (String.IsNullOrEmpty step.attr) -> e.getAttribute step.attr
                                | e -> e.textContent)

                        ok <-
                            (not (String.IsNullOrEmpty step.text)
                             && seen = step.text)
                            || (not (String.IsNullOrEmpty step.contains)
                                && not (isNull seen)
                                && seen.Contains step.contains)
                            || (String.IsNullOrEmpty step.text
                                && String.IsNullOrEmpty step.contains
                                && seen <> "<missing>")

                        if not ok then
                            Thread.Sleep 30

                    results.Add
                        {
                            Name = $"expect {step.selector} {step.text}{step.contains}"
                            Ok = ok
                            Detail = $"saw '{seen}'"
                            Report = None
                        }
                | "pixel" ->
                    // The presented frame at the centre of an element: which of r, g, b dominates.
                    let deadline = Stopwatch.StartNew ()
                    let mutable ok = false
                    let mutable seen = ""

                    while not ok && deadline.Elapsed.TotalSeconds < 4.0 do
                        let struct (r, g, b) =
                            onUi (fun () ->
                                let rect = (query step.selector).getBoundingClientRect()
                                // attr "corner": the bottom-left background, clear of overlays; otherwise the centre.
                                let corner = step.attr = "corner"

                                let x =
                                    if corner then
                                        int rect.x + 6
                                    else
                                        int (rect.x + rect.width / 2.0)

                                let y =
                                    if corner then
                                        int (rect.y + rect.height) - 6
                                    else
                                        int (rect.y + rect.height / 2.0)
                                // Window.ReadPixels is internal to the presenter; the bench reads it reflectively.
                                let read =
                                    typeof<Window>
                                        .GetMethod(
                                            "ReadPixels",
                                            Reflection.BindingFlags.Instance
                                            ||| Reflection.BindingFlags.Public
                                            ||| Reflection.BindingFlags.NonPublic
                                        )

                                let struct (w, _, pixels) =
                                    read.Invoke (window, [||]) :?> struct (int * int * byte array)

                                let i = (y * w + x) * 4
                                struct (pixels[i], pixels[i + 1], pixels[i + 2]))

                        let dominant =
                            if r >= g && r >= b then "r"
                            elif g >= b then "g"
                            else "b"

                        seen <- $"rgb({r},{g},{b}) dominant {dominant}"
                        ok <- dominant = step.text

                        if not ok then
                            Thread.Sleep 50

                    results.Add
                        {
                            Name = $"pixel {step.selector} dominant {step.text}"
                            Ok = ok
                            Detail = seen
                            Report = None
                        }
                | "probe" ->
                    // AppDomain data written by the app's own probes (shared by every code generation).
                    let seen =
                        match AppDomain.CurrentDomain.GetData step.name with
                        | :? int as n -> n
                        | _ -> 0

                    results.Add
                        {
                            Name = $"probe {step.name} = {step.value}"
                            Ok = (seen = step.value)
                            Detail = $"saw {seen}"
                            Report = None
                        }
                | other -> failwith $"bench: unknown op {other}"
            with e ->
                results.Add
                    {
                        Name = $"{step.op} {step.name}"
                        Ok = false
                        Detail = e.Message
                        Report = None
                    }

        for KeyValue (file, text) in originals do
            File.WriteAllText (file, text)

        let strategyName = (sprintf "%A" strategy).ToLowerInvariant()
        let suffix = if useChecker || strategy <> Fine then "" else "-nochecker"
        let scenario = Path.GetFileNameWithoutExtension path

        let seeded =
            match Environment.GetEnvironmentVariable "HMR_SEED_TODOS" with
            | null
            | "" -> ""
            | n -> $"-{n}rows"

        let outFile =
            Path.Combine (Path.GetDirectoryName path, "results", $"{scenario}{seeded}-{strategyName}{suffix}-{DateTime.Now:HHmmss}.json")

        Directory.CreateDirectory (Path.GetDirectoryName outFile)
        |> ignore

        let rows =
            results
            |> Seq.map (fun r ->
                {|
                    step = r.Name
                    ok = r.Ok
                    detail = r.Detail
                    path = r.Report |> Option.map _.Path |> Option.toObj
                    detectMs =
                        r.Report
                        |> Option.map _.DetectMs
                        |> Option.toNullable
                    checkMs =
                        r.Report
                        |> Option.map _.CheckMs
                        |> Option.toNullable
                    deliverMs =
                        r.Report
                        |> Option.map _.DeliverMs
                        |> Option.toNullable
                    applyMs =
                        r.Report
                        |> Option.map _.ApplyMs
                        |> Option.toNullable
                    totalMs =
                        r.Report
                        |> Option.map _.TotalMs
                        |> Option.toNullable
                |})

        File.WriteAllText (outFile, JsonSerializer.Serialize (rows, JsonSerializerOptions (WriteIndented = true)))
        log $"bench: {results |> Seq.filter _.Ok |> Seq.length}/{results.Count} steps passed -> {outFile}"
        Thread.Sleep 300
        ui.Enqueue (fun () -> window.Close ())

    // -----------------------------------------------------------------------------------------------------------

    member _.Run() : int =
        let entryTypes = Reflect.safeTypes (Reflection.Assembly.GetEntryAssembly ())

        let initial = Reflect.componentsOf entryTypes
        register initial

        for (k, _) in initial do
            componentKeys.Add k |> ignore

        rootKey <-
            match
                initial
                |> List.tryFind (fun (_, d) -> obj.ReferenceEquals (d, root))
            with
            | Some (k, _) -> k
            | None ->
                registry.Register ("<root>", root)
                "<root>"

        for f in project.Sources do
            lastText[f] <- File.ReadAllText f
            applied[f] <- lastText[f]

        log
            $"dev host: strategy {strategy}, project {Path.GetFileName project.Fsproj}, {project.Sources.Length} sources, {initial.Length} components, root {rootKey}"

        window <-
            if Array.contains "-software" argv then
                Window.Create (title, width, height, renderer = "software")
            else
                Window.create (title, width, height)

        graph <- new Graph ()
        props <- propsOf window
        let ms, _ = remount ()
        let mutable firstFrame = true

        use watcher = new FileSystemWatcher (project.Dir, "*.fs")
        watcher.IncludeSubdirectories <- false

        watcher.NotifyFilter <-
            NotifyFilters.LastWrite
            ||| NotifyFilters.FileName
            ||| NotifyFilters.Size

        watcher.Changed.Add (fun e -> onChange e.FullPath)
        watcher.Created.Add (fun e -> onChange e.FullPath)
        watcher.Renamed.Add (fun e -> onChange e.FullPath)
        watcher.EnableRaisingEvents <- true

        let worker =
            Thread ((fun () -> workerLoop ()), IsBackground = true, Name = "hmr-worker")

        worker.Start ()

        match arg "-bench" with
        | Some path -> Thread((fun () -> runBench (Path.GetFullPath path)), IsBackground = true, Name = "hmr-bench").Start()
        | None -> ()

        let mutable item = Unchecked.defaultof<unit -> unit>

        // Watchdog: reports a window thread that stops iterating, and what it was doing.
        let beat = ref (Time.now ())
        let phase = ref "step"
        let running = ref true

        Thread(
            (fun () ->
                while running.Value do
                    Thread.Sleep 1000
                    let stalled = Time.ms beat.Value (Time.now ())

                    if running.Value && stalled > 3000.0 then
                        log $"WATCHDOG: window thread stalled {stalled:F0} ms in '{phase.Value}'"),
            IsBackground = true,
            Name = "hmr-watchdog"
        )
            .Start()

        while window.Step (timeout = 8) do
            beat.Value <- Time.now ()

            if firstFrame then
                firstFrame <- false
                log $"first frame {Time.ms started (Time.now ()):F0} ms after host start (mount {ms:F1} ms)"

            match awaitingPresent with
            | Some r ->
                awaitingPresent <- None
                r.TotalMs <- Time.ms r.Started (Time.now ())
                phase.Value <- "publish"
                publish r
            | None -> ()

            phase.Value <- "ui work"

            while ui.TryDequeue &item do
                item ()
                beat.Value <- Time.now ()

            phase.Value <- "step"

        running.Value <- false
        unmount ()
        work.CompleteAdding ()
        0

and private TaskCompletionSourceSlim<'T>() =
    let gate = new ManualResetEventSlim false
    let mutable value: Result<'T, exn> = Unchecked.defaultof<_>

    member _.Set(v: Result<'T, exn>) =
        value <- v
        gate.Set ()

    member _.Wait() : 'T =
        gate.Wait ()

        match value with
        | Ok v -> v
        | Error e -> raise (AggregateException e)

[<AbstractClass; Sealed>]
type Dev =
    static member private Start
        (argv: string array, title: string, width: int, height: int, root: Component<'props>, propsOf: Window -> 'props, callerFile: string)
        : int =
        if Array.contains "dev" argv then
            DevHost(argv, title, width, height, root, (fun window -> box (propsOf window)), callerFile).Run()
        else
            // The plain app: what `Window.run` does, plus optional timing output for the comparison runs.
            let started = Stopwatch.GetTimestamp ()

            let elapsed () =
                float (Stopwatch.GetTimestamp () - started)
                * 1000.0
                / float Stopwatch.Frequency

            let exitAfter =
                match Double.TryParse (Environment.GetEnvironmentVariable "RANVIER_HMR_EXIT_AFTER_MS") with
                | true, v -> v
                | _ -> -1.0

            use window = Window.create (title, width, height)
            let graph = new Graph ()
            let container = window.Host.Document.createElement "div"

            window.Host.Document.body.appendChild container
            |> ignore

            let beforeMount = elapsed ()

            let mounted =
                Mounting.mount graph None (fun errors -> raise (AggregateException (errors: exn seq))) container None (fun context ->
                    Program.run (Component.render root (propsOf window)) context.Backend)

            Console.WriteLine ($"[app] mount {elapsed () - beforeMount:F1} ms")
            let mutable first = true

            while window.Step () do
                if first then
                    first <- false
                    Console.WriteLine ($"[app] first frame {elapsed ():F0} ms after Dev.Run")

                if exitAfter > 0.0 && elapsed () > exitAfter then
                    window.Close ()

            graph.Run (fun () -> mounted.Dispose ())
            graph.Dispose ()
            0

    /// Runs a native Ranvier app. With `dev` among the arguments, runs it under the hot reload host instead.
    static member Run
        (
            argv: string array,
            title: string,
            width: int,
            height: int,
            root: Component<'props>,
            props: 'props,
            [<CallerFilePath; Optional; DefaultParameterValue("")>] callerFile: string
        ) : int =
        Dev.Start (argv, title, width, height, root, (fun _ -> props), callerFile)

    /// As Run, with the root's props built from the window: a GPU context created here lives as long as the
    /// window and survives every reload.
    static member RunWith
        (
            argv: string array,
            title: string,
            width: int,
            height: int,
            root: Component<'props>,
            propsOf: Window -> 'props,
            [<CallerFilePath; Optional; DefaultParameterValue("")>] callerFile: string
        ) : int =
        Dev.Start (argv, title, width, height, root, propsOf, callerFile)
