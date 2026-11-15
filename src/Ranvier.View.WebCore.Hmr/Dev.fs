namespace Ranvier.View.WebCore.Hmr

// The dev host: `dotnet run -- dev [-hmr alc|fsi|fine] [-nochecker] [-bench scenario.json]`.
// Owns the SDL window and WebCore page for the whole session; reloads swap code underneath them.
// For tests: `-hidden` keeps the window off screen, `-software` uses the software renderer, and `-sources <dir>`
// reads the project from a copy in another folder.

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
        /// Further files an edit writes or a revert restores with `file`: (file, find, replace).
        more: (string * string * string) list
    }

type private StepResult =
    {
        Name: string
        Ok: bool
        Detail: string
        Report: Report option
    }

type internal DevHost
    (argv: string array, title: string, width: int, height: int, root: TemplateComponent, propsOf: Window -> obj, ownsProps: bool, callerFile: string)
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

    // `-sources <dir>` reads the project from another folder than the one it was built in: a test runs the host
    // against a copy of its fixture.
    let project =
        match arg "-sources" with
        | Some dir -> Delivery.loadProject (Path.Combine (Path.GetFullPath dir, "Program.fs"))
        | None -> Delivery.loadProject callerFile

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
            Mounting.mount graph None (List.iter window.Host.Report) container None (fun context ->
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

    /// Reports a change the host leaves unapplied until the app restarts.
    let restart (report: Report) (why: string) =
        let r =
            { report with
                Path = "restart"
                Detail = why
            }

        ui.Enqueue (fun () -> awaitingPresent <- Some r)

    /// Evaluates a file's lets and nested modules in a shadow module that opens the module already in use.
    let shadowLoad
        (f: string)
        (moduleName: string)
        (decls: Delivery.Declaration list)
        (aliasOf: Delivery.Declaration -> string option)
        (nestedOf: Delivery.Declaration -> Delivery.Nested)
        (abbreviations: (string * string) list)
        (live: bool)
        =
        generation <- generation + 1
        let shadowDir = Path.Combine (project.Dir, "obj", "hmr", "fine")
        Directory.CreateDirectory shadowDir |> ignore

        let shadow =
            Path.Combine (shadowDir, $"{Path.GetFileNameWithoutExtension f}__hmr{generation}.fs")

        File.WriteAllText (shadow, Delivery.shadowModule f moduleName generation decls aliasOf nestedOf abbreviations)
        let result = fsi.Value.Load shadow

        match result with
        | Ok e when live ->
            for (k, _) in e.Components do
                componentKeys.Add k |> ignore

            for d in decls do
                let evaluated =
                    match d.Kind with
                    | "let" ->
                        d.IsValue
                        && not (isNull d.Name)
                        && (aliasOf d).IsNone
                    | "module" -> nestedOf d = Delivery.Reevaluate
                    | _ -> false

                if evaluated then
                    definedIn[struct (f, d.Name)] <- $"{moduleName}__hmr{generation}"

            latestModule[moduleName] <- $"{moduleName}__hmr{generation}"
        | _ -> ()

        result

    /// The module that last evaluated a top-level value or nested module of a file.
    let liveModule (f: string) (moduleName: string) (name: string) =
        match definedIn.TryGetValue (struct (f, name)) with
        | true, m -> m
        | _ -> moduleName

    /// The live module of a public, immutable module-level value, which then keeps the object it holds (a store, a
    /// cache) across a reload. None for any other let, and for a value among `touched`: the declarations that
    /// changed or that use a change, by full name.
    let aliasOf (f: string) (moduleName: string) (touched: HashSet<string>) (d: Delivery.Declaration) =
        if
            d.IsValue
            && d.IsPublic
            && not d.IsMutable
            && not (isNull d.Name)
            && not (componentKeys.Contains (moduleName + "." + d.Name))
            && not (touched.Contains (moduleName + "." + d.Name))
        then
            Some (liveModule f moduleName d.Name)
        else
            None

    /// A nested module of lets is evaluated again when a declaration inside it is among `touched`, and when it is
    /// private: the shadow module cannot reach the existing one. Any other public module is abbreviated.
    let nestedOf (f: string) (moduleName: string) (touched: HashSet<string>) (d: Delivery.Declaration) =
        let prefix = $"{moduleName}.{d.Name}."

        if
            Delivery.isLetsOnly d
            && (not d.IsPublic
                || touched
                   |> Seq.exists (fun name -> name.StartsWith prefix))
        then
            Delivery.Reevaluate
        elif d.IsPublic then
            Delivery.Abbreviate (liveModule f moduleName d.Name)
        else
            Delivery.Omit

    let untouched = HashSet<string>()

    /// The FSI baseline of one file, the first time it is touched: fingerprints of its mounted code as FSI
    /// compiles it, so its first edit compares FSI output with FSI output (reloaded code always sits in another
    /// assembly than the types it uses, so its IL never matches the app's own compile). It aliases as a reload
    /// with no changed value does, or its closures' IL differs from the first edit's (an alias getter in place of
    /// a static field).
    let baselineOf (f: string) (text: string) =
        if baselined.Add f then
            match Delivery.declarations f text with
            | Some (moduleName, decls) ->
                match shadowLoad f moduleName decls (aliasOf f moduleName untouched) (nestedOf f moduleName untouched) [] false with
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

    /// Evaluates one file: its lets and nested modules of lets in a shadow module, or the whole file when `whole`.
    /// `touched` holds the full names of the file's declarations that changed or that use a change.
    let evaluate (f: string) (text: string) (touched: HashSet<string>) (whole: bool) =
        match (if whole then None else Delivery.declarations f text) with
        | Some (moduleName, decls) ->
            let alias = aliasOf f moduleName touched
            let nested = nestedOf f moduleName touched

            // A name from an earlier file means that file's latest generation, in an edited file as in a dependent.
            let abbreviations =
                [
                    for KeyValue (m, latest) in latestModule do
                        if m <> moduleName && latest <> m then
                            yield (m, latest)
                ]

            let kept =
                decls
                |> List.filter (fun d -> d.Kind = "let" && (alias d).IsSome)
                |> List.map _.Name

            let note =
                if kept.IsEmpty then
                    ""
                else
                    $" (kept {String.Join (',', kept)})"

            match shadowLoad f moduleName decls alias nested abbreviations true with
            | Error _ when not abbreviations.IsEmpty ->
                // A shadow module holds lets only, so a name qualified with a reloaded module (one of its types, say)
                // is missing from it.
                let unabbreviated = shadowLoad f moduleName decls alias nested [] true

                unabbreviated,
                "lets-only eval"
                + note
                + " (names qualified with a reloaded module bind to the build)"
            | result -> result, "lets-only eval" + note
        | _ ->
            let result = fsi.Value.Load f

            match result, Delivery.declarations f text with
            | Ok e, Some (moduleName, decls) ->
                for (k, _) in e.Components do
                    componentKeys.Add k |> ignore

                for d in decls do
                    if
                        (d.Kind = "let" && d.IsValue && not (isNull d.Name))
                        || d.Kind = "module"
                    then
                        definedIn[struct (f, d.Name)] <- moduleName

                latestModule[moduleName] <- moduleName
            | _ -> ()

            result, "whole-file eval (types changed)"

    /// Declaration stamps decide what changed. The checker gate runs first: its errors block the reload, and its
    /// uses decide which declarations, in any file, depend on a change and are evaluated again with it. IL
    /// fingerprints (FSI to FSI) decide which closures inside a patched component rebind. Components that neither
    /// changed nor depend on a change are not touched at all.
    let deliverFine (report: Report) (files: string list) (drift: bool) =
        let notes = ResizeArray<string>()
        let texts = Dictionary<string, string>()
        let fresh = Dictionary<string, Stamps.Stamp array>()
        let changedKeys = Dictionary<string, string list>()
        let changedSet = HashSet<struct (string * string)>()

        let textOf (f: string) =
            match texts.TryGetValue f with
            | true, text -> text
            | _ ->
                let text = File.ReadAllText f
                texts[f] <- text
                text

        for f in files do
            let stamps = Stamps.ofText stampContext.Value f (textOf f)
            let keys = Stamps.changed appliedStamps[f] stamps
            fresh[f] <- stamps
            changedKeys[f] <- keys

            if keys.IsEmpty then
                // Only text between declarations changed (comments, blank lines): nothing to evaluate.
                notes.Add $"{Path.GetFileName f}: no declaration changed (stamps)"

            for k in keys do
                changedSet.Add (struct (Path.GetFileName f, k))
                |> ignore

        let checking = useChecker && gateReady
        let check = Stopwatch.StartNew ()

        // The checker holds the text of every delivered file, a file saved back to its applied text included: the
        // errors of an edit that was undone must not outlive it.
        if checking && changedSet.Count = 0 then
            for f in files do
                gate.Value.SetText (f, textOf f)

        // An edit can break a file below it, so every file from the first edited one on is read for errors.
        let errors =
            if checking && changedSet.Count > 0 then
                let first =
                    files
                    |> List.map (fun f -> List.findIndex ((=) f) project.Sources)
                    |> List.min

                [
                    for f in project.Sources |> List.skip first do
                        if List.contains f files then
                            yield! (gate.Value.Check (f, textOf f)).Errors
                        elif f <> project.Entry then
                            yield! gate.Value.Errors f
                ]
                |> List.distinct
            else
                []

        let checkMs = if checking then check.Elapsed.TotalMilliseconds else 0.0

        if not errors.IsEmpty then
            fail { report with CheckMs = checkMs } errors
        else
            let parsed = Dictionary<string, (string * Delivery.Declaration list) option>()

            let parse (f: string) =
                match parsed.TryGetValue f with
                | true, p -> p
                | _ ->
                    let p = Delivery.declarations f (textOf f)
                    parsed[f] <- p
                    p

            // Declarations a shadow module cannot evaluate again: any kind but a let or an open, and a let inside a
            // nested module that holds more than lets.
            let structural (f: string) (keys: string list) =
                match parse f with
                | None -> []
                | Some (moduleName, decls) ->
                    keys
                    |> List.filter (fun k ->
                        not (k.EndsWith ":Value" || k.EndsWith ":Open")
                        || decls
                           |> List.exists (fun d ->
                               d.Kind = "module"
                               && not (Delivery.isLetsOnly d)
                               && k.StartsWith $"{moduleName}.{d.Name}."))

            // A file evaluated whole redefines every declaration it holds, so each of them counts as changed.
            let whole = HashSet<string>()

            for f in files do
                if
                    not changedKeys[f].IsEmpty
                    && ((parse f).IsNone
                        || not (structural f changedKeys[f]).IsEmpty)
                then
                    whole.Add f |> ignore

                    for stamp in fresh[f] do
                        changedSet.Add (struct (Path.GetFileName f, stamp.Key))
                        |> ignore

            let isComponent (stampKey: string) =
                stampKey.EndsWith ":Value"
                && componentKeys.Contains (stampKey.Substring (0, stampKey.Length - 6))

            // Dependents through types and values (not through components, which are found by key).
            let unread = HashSet<string>()

            let dependents =
                if checking && changedSet.Count > 0 then
                    gate.Value.Dependents (changedSet, isComponent, unread)
                else
                    HashSet ()

            for f in unread do
                if f <> project.Entry then
                    notes.Add
                        $"{Path.GetFileName f}: not checked by declaration, so what it uses of this edit is unknown and it was not evaluated again"

            let plan =
                [
                    for f in project.Sources do
                        if f <> project.Entry then
                            let own =
                                match changedKeys.TryGetValue f with
                                | true, keys -> keys
                                | _ -> []

                            let uses =
                                [
                                    for (struct (file, k)) in dependents do
                                        if file = Path.GetFileName f then
                                            yield k
                                ]

                            if not (own.IsEmpty && uses.IsEmpty) then
                                yield f, own, uses
                ]

            // A dependent in a file with several modules is evaluated with its whole file.
            for (f, _, _) in plan do
                if (parse f).IsNone then
                    whole.Add f |> ignore

            let shortKey (k: string) =
                k.Substring (k.LastIndexOf '.' + 1)

            let stale =
                [
                    for (f, _, uses) in plan do
                        if not (whole.Contains f) then
                            match structural f uses with
                            | [] -> ()
                            | keys -> yield $"{Path.GetFileName f}: {String.Join (',', keys |> List.map shortKey)}"
                ]

            if not stale.IsEmpty then
                let edited =
                    files
                    |> List.filter (fun f -> not changedKeys[f].IsEmpty)
                    |> List.map Path.GetFileName

                let users = String.Join ("; ", stale)
                let sources = String.Join (',', edited)

                restart
                    { report with CheckMs = checkMs }
                    ($"restart needed: {users} cannot be evaluated again in place, "
                     + $"and the edit to {sources} redefines what they use; the edit was not applied")
            else
                let mutable baselineMs = 0.0
                let loads = ResizeArray<string * Result<Delivery.Evaluated, string list> * bool>()
                let mutable failure: string list = []

                for (f, own, uses) in plan do
                    if failure.IsEmpty then
                        if lazyBaseline && not drift then
                            match baselineOf f applied[f] with
                            | Some ms -> baselineMs <- baselineMs + ms
                            | None -> ()

                        let touched =
                            HashSet<string>(
                                own @ uses
                                |> List.map (fun k -> k.Substring (0, k.LastIndexOf ':'))
                            )

                        let load, how = evaluate f (textOf f) touched (whole.Contains f)

                        let names (keys: string list) =
                            String.Join (',', keys |> List.map shortKey |> List.truncate 4)

                        let what =
                            match own, uses with
                            | [], _ -> $"dependent {names uses}"
                            | _, [] -> $"changed {names own}"
                            | _ -> $"changed {names own}, dependent {names uses}"

                        notes.Add $"{Path.GetFileName f}: {what}; {how}"
                        loads.Add ((f, load, whole.Contains f))

                        match load with
                        | Error messages ->
                            let specific =
                                messages
                                |> List.filter (fun m ->
                                    m
                                    <> "Operation could not be completed due to earlier error")

                            failure <-
                                $"{Path.GetFileName f} passed the check and FSI did not evaluate it ({how})"
                                :: (if specific.IsEmpty then messages else specific)
                        | Ok _ -> ()

                if not failure.IsEmpty then
                    // Every file of this batch stays unapplied, so the next save delivers them again.
                    fail { report with CheckMs = checkMs } failure
                else
                    for f in files do
                        applied[f] <- textOf f
                        appliedStamps[f] <- fresh[f]

                    if loads.Count = 0 then
                        post
                            { report with
                                CheckMs = checkMs
                                Detail = String.Join ("; ", notes)
                            }
                            (fun () -> "unchanged", "")
                    else
                        let shouldPatch (file: string) (componentKey: string) =
                            not checking
                            || (let key = struct (Path.GetFileName file, componentKey + ":Value")
                                changedSet.Contains key || dependents.Contains key)

                        // A module-level value lives in a static field, so evaluating it again leaves the IL of the
                        // closures that read it unchanged: the components that use it are rebound.
                        let forced =
                            if checking then
                                let values = HashSet<struct (string * string)>()

                                for (f, _, _) in plan do
                                    match parse f with
                                    | Some (moduleName, decls) ->
                                        for name in Delivery.valueNames moduleName decls do
                                            let key = struct (Path.GetFileName f, name + ":Value")

                                            if
                                                (changedSet.Contains key || dependents.Contains key)
                                                && not (componentKeys.Contains name)
                                            then
                                                values.Add key |> ignore
                                    | None -> ()

                                if values.Count = 0 then
                                    HashSet ()
                                else
                                    gate.Value.Dependents (values, (fun _ -> false), HashSet ())
                            else
                                HashSet ()

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

    let processBatch (files: string list) (startedAt: int64) =
        let detected = Time.now ()

        let read (f: string) =
            let rec attempt n =
                try
                    File.ReadAllText f
                with :? IOException when n > 0 ->
                    Thread.Sleep 5
                    attempt (n - 1)

            attempt 20

        let changed =
            files
            |> List.filter (fun f ->
                match lastText.TryGetValue f with
                | true, t when t = read f -> false
                | _ -> true)

        if not changed.IsEmpty then
            for f in changed do
                lastText[f] <- read f

            let entryChanged = List.contains project.Entry changed

            // Fine delivers the saved files and every file whose text differs from the text last applied, so a file
            // whose reload failed goes again with the save that repairs it.
            let deliver =
                match strategy with
                | Fine ->
                    project.Sources
                    |> List.filter (fun f ->
                        f <> project.Entry
                        && (List.contains f changed || read f <> applied[f]))
                | _ ->
                    changed
                    |> List.filter (fun f -> f <> project.Entry)

            seq <- seq + 1

            let report =
                {
                    Seq = seq
                    Strategy = (sprintf "%A" strategy).ToLowerInvariant()
                    Files =
                        String.Join (
                            ",",
                            (if deliver.IsEmpty then changed else deliver)
                            |> List.map Path.GetFileName
                        )
                    Path = ""
                    Detail = ""
                    DetectMs = Time.ms startedAt detected
                    CheckMs = 0.0
                    DeliverMs = 0.0
                    ApplyMs = 0.0
                    TotalMs = 0.0
                    Started = startedAt
                }

            let entryNote =
                $"restart needed: {Path.GetFileName project.Entry} is the entry file, which the host does not reload"

            if deliver.IsEmpty then
                // Only the entry file was saved.
                restart report entryNote
            else
                if entryChanged then
                    log entryNote

                try
                    match strategy with
                    | Alc -> deliverAlc report
                    | Fsi -> deliverFsi report deliver
                    | Fine -> deliverFine report deliver false
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
                            match shadowLoad f moduleName decls (aliasOf f moduleName untouched) (nestedOf f moduleName untouched) [] false with
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

            deliverFine report driftFiles true

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

        if List.contains full project.Sources then
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
                            more =
                                match e.TryGetProperty "more" with
                                | true, items when items.ValueKind = JsonValueKind.Array ->
                                    [
                                        for item in items.EnumerateArray () do
                                            let part (n: string) =
                                                match item.TryGetProperty n with
                                                | true, v when v.ValueKind = JsonValueKind.String -> v.GetString ()
                                                | _ -> null

                                            yield part "file", part "find", part "replace"
                                    ]
                                | _ -> []
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

                    let others =
                        [
                            for (name, find, replace) in step.more do
                                let other = Path.Combine (project.Dir, name)

                                if not (originals.ContainsKey other) then
                                    originals[other] <- File.ReadAllText other

                                let text = File.ReadAllText other

                                let fit (s: string) =
                                    if text.Contains "\r\n" then
                                        s.Replace("\r\n", "\n").Replace("\n", "\r\n")
                                    else
                                        s

                                if step.op = "revert" then
                                    yield other, originals[other]
                                elif text.Contains (fit find) then
                                    yield other, text.Replace (fit find, fit replace)
                                else
                                    failwith $"bench: '{find}' not found in {name}"
                        ]

                    let since = lock reports (fun () -> reports.Count |> fun _ -> seq)
                    File.WriteAllText (file, next)

                    for (other, text) in others do
                        File.WriteAllText (other, text)

                    let report = waitReport since

                    // `contains` on an edit names text the reload's report must carry.
                    let told (r: Report) =
                        String.IsNullOrEmpty step.contains
                        || r.Detail.Contains step.contains

                    let ok, detail =
                        match report with
                        | Some r when r.Path = "error" && step.error -> told r, $"expected error: {r.Detail}"
                        | Some r when r.Path <> "error" -> not step.error && told r, $"{r.Path}: {r.Detail}"
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
            Window.Create (
                title,
                width,
                height,
                hidden = Array.contains "-hidden" argv,
                ?renderer =
                    (if Array.contains "-software" argv then
                         Some "software"
                     else
                         None)
            )

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

        // The tree's GPU surfaces went with it. Resources the props hold were built on the window, so they go next,
        // and the window last: a process that exits with both alive can stall in the driver's teardown.
        if ownsProps then
            match props with
            | :? IDisposable as resources -> resources.Dispose ()
            | _ -> ()

        (window :> IDisposable).Dispose()
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
        (
            argv: string array,
            title: string,
            width: int,
            height: int,
            root: Component<'props>,
            propsOf: Window -> 'props,
            ownsProps: bool,
            callerFile: string
        ) : int =
        if Array.contains "dev" argv then
            DevHost(argv, title, width, height, root, (fun window -> box (propsOf window)), ownsProps, callerFile).Run()
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
            let props = propsOf window

            let mounted =
                Mounting.mount graph None (List.iter window.Host.Report) container None (fun context ->
                    Program.run (Component.render root props) context.Backend)

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

            // As in the dev host: the props' resources go before the window they were built on.
            if ownsProps then
                match box props with
                | :? IDisposable as resources -> resources.Dispose ()
                | _ -> ()

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
        Dev.Start (argv, title, width, height, root, (fun _ -> props), false, callerFile)

    /// As Run, with the root's props built from the window: a GPU context created here lives as long as the
    /// window and survives every reload. Props that implement IDisposable belong to the host, which disposes them
    /// after the tree unmounts and before the window.
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
        Dev.Start (argv, title, width, height, root, propsOf, true, callerFile)
