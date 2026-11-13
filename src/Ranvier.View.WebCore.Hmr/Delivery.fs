/// Getting new code into the running process: an in-process compile into a collectible load context,
/// an FSI session, the declaration splitter for shadow modules, and the Ranvier.FSharp.Checker gate.
module Ranvier.View.WebCore.Hmr.Delivery

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.InteropServices
open System.Runtime.Loader
open System.Text
open System.Xml.Linq
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open Ranvier.View

// ---------------------------------------------------------------------------------------------------------------
// Project

type Project =
    {
        Fsproj: string
        Dir: string
        /// Compile order, full paths.
        Sources: string list
        /// The file holding [<EntryPoint>]; never reloaded.
        Entry: string
        /// Managed, non-framework assemblies the app is built from (its own assembly first).
        References: string list
        /// The running runtime's implementation assemblies.
        Framework: string list
        Name: string
    }

let private excluded (name: string) =
    [
        "FSharp.Compiler"
        "Rfs."
        "Ionide"
        "Microsoft.Build"
        "NuGet"
        "FSharp.DependencyManager"
        "Microsoft.CodeAnalysis"
        "Fantomas"
    ]
    |> List.exists name.StartsWith

let private isManaged (path: string) =
    try
        AssemblyName.GetAssemblyName path |> ignore
        true
    with _ ->
        false

let loadProject (callerFile: string) : Project =
    let rec findProject (dir: DirectoryInfo) =
        if isNull dir then
            failwith "No .fsproj found above the entry file"
        else
            match dir.GetFiles "*.fsproj" with
            | [||] -> findProject dir.Parent
            | files -> files[0].FullName

    let fsproj = findProject (FileInfo(callerFile).Directory)
    let dir = Path.GetDirectoryName fsproj
    let doc = XDocument.Load fsproj

    let sources =
        doc.Descendants ()
        |> Seq.filter (fun e ->
            e.Name.LocalName = "Compile"
            && not (isNull (e.Attribute (XName.Get "Include"))))
        |> Seq.map (fun e -> Path.GetFullPath (Path.Combine (dir, e.Attribute(XName.Get "Include").Value)))
        |> Seq.toList

    let entry =
        sources
        |> List.tryFindBack (fun f -> File.ReadAllText(f).Contains "EntryPoint")
        |> Option.defaultValue (List.last sources)

    let runtimeDir = RuntimeEnvironment.GetRuntimeDirectory ()
    let entryAssembly = Assembly.GetEntryAssembly ()
    let seen = HashSet<string>()
    let refs = ResizeArray<string> [ entryAssembly.Location ]

    let rec visit (a: Assembly) =
        for r in a.GetReferencedAssemblies () do
            if seen.Add r.Name && not (excluded r.Name) then
                match
                    (try
                        Assembly.Load r
                     with _ ->
                         null)
                with
                | null -> ()
                | ra when String.IsNullOrEmpty ra.Location -> ()
                | ra when ra.Location.StartsWith (runtimeDir, StringComparison.OrdinalIgnoreCase) -> ()
                | ra ->
                    refs.Add ra.Location

                    if not (r.Name.StartsWith "Ranvier.View.WebCore.Hmr") then
                        visit ra

    visit entryAssembly

    let framework =
        Directory.GetFiles (runtimeDir, "*.dll")
        |> Array.filter (fun f ->
            let n = Path.GetFileName f

            (n.StartsWith "System."
             || n.StartsWith "Microsoft."
             || n = "netstandard.dll"
             || n = "mscorlib.dll")
            && not (n.Contains "Native")
            && isManaged f)
        |> Array.toList

    {
        Fsproj = fsproj
        Dir = dir
        Sources = sources
        Entry = entry
        References = List.ofSeq refs |> List.distinct
        Framework = framework
        Name = entryAssembly.GetName().Name
    }

let private errorsOf (diagnostics: FSharpDiagnostic array) =
    diagnostics
    |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Error)
    |> Array.map (fun d -> $"{Path.GetFileName d.FileName}({d.StartLine},{d.StartColumn}): {d.Message}")
    |> Array.toList

// ---------------------------------------------------------------------------------------------------------------
// P1: in-process compile of the whole project into a collectible AssemblyLoadContext

type private ReloadContext(name: string) =
    inherit AssemblyLoadContext(name, isCollectible = true)
    // Everything except the reloaded assembly itself resolves from the default context, so Ranvier types are shared.
    override _.Load(_: AssemblyName) = null

type Compiled =
    {
        Components: (string * TemplateComponent) list
        CompileMs: float
        LoadMs: float
    }

type AlcDelivery(project: Project) =
    let checker = FSharpChecker.Create ()
    let outDir = Path.Combine (project.Dir, "obj", "hmr", "alc")
    let mutable generation = 0
    let mutable current: AssemblyLoadContext = null

    do Directory.CreateDirectory outDir |> ignore

    member _.Compile() : Result<Compiled, string list> =
        generation <- generation + 1
        let out = Path.Combine (outDir, $"{project.Name}.g{generation}.dll")
        let sw = Stopwatch.StartNew ()

        let argv =
            [|
                yield "fsc.exe"
                yield "-o:" + out
                yield "--target:library"
                yield "--noframework"
                yield "--targetprofile:netcore"
                yield "--optimize-"
                yield "--debug:portable"
                yield "--nowin32manifest"
                yield "--nocopyfsharpcore"
                yield "--nowarn:3370"
                yield "--define:DEBUG"
                for r in project.Framework do
                    yield "-r:" + r
                for r in project.References do
                    yield "-r:" + r
                for s in project.Sources do
                    if s <> project.Entry then
                        yield s
            |]

        let diagnostics, failure = checker.Compile argv |> Async.RunSynchronously
        let compileMs = sw.Elapsed.TotalMilliseconds

        match failure, errorsOf diagnostics with
        | None, [] ->
            sw.Restart ()
            let context = new ReloadContext $"hmr-g{generation}"
            use stream = new MemoryStream (File.ReadAllBytes out)
            let assembly = context.LoadFromStream stream
            let components = Reflect.componentsOf (Reflect.safeTypes assembly)

            if not (isNull current) then
                current.Unload ()

            current <- context

            Ok
                {
                    Components = components
                    CompileMs = compileMs
                    LoadMs = sw.Elapsed.TotalMilliseconds
                }
        | Some e, errors -> Error (e.Message :: errors)
        | None, errors -> Error errors

// ---------------------------------------------------------------------------------------------------------------
// P2/P3: an FSI session that references the running app

type Evaluated =
    {
        Components: (string * TemplateComponent) list
        EvalMs: float
    }

type FsiDelivery(project: Project) =
    let out = StringBuilder ()
    let err = StringBuilder ()

    let session =
        lazy
            (let config = FsiEvaluationSession.GetDefaultConfiguration ()

             let argv =
                 [|
                     yield "fsi.exe"
                     yield "--noninteractive"
                     yield "--nologo"
                     yield "--gui-"
                     yield "--multiemit+"
                     yield "--optimize-"
                     yield "--define:DEBUG"
                     yield "--nowarn:3370"
                     for r in project.References do
                         yield "-r:" + r
                 |]

             FsiEvaluationSession.Create (config, argv, new StringReader "", new StringWriter (out), new StringWriter (err), collectible = false))

    let gate = obj ()

    /// The first evaluation compiles FSI itself; do it before the first edit.
    member _.Warm() =
        lock gate (fun () ->
            let sw = Stopwatch.StartNew ()

            session.Value.EvalInteractionNonThrowing "let __hmrWarm = 1 + 1"
            |> ignore

            sw.Elapsed.TotalMilliseconds)

    member _.Load(path: string) : Result<Evaluated, string list> =
        lock gate (fun () ->
            let sw = Stopwatch.StartNew ()

            let before =
                HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies (), HashIdentity.Reference)

            let result, diagnostics =
                session.Value.EvalInteractionNonThrowing ($"#load @\"{path}\"")

            let ms = sw.Elapsed.TotalMilliseconds

            match result, errorsOf diagnostics with
            | Choice1Of2 _, [] ->
                let fresh =
                    AppDomain.CurrentDomain.GetAssemblies ()
                    |> Array.filter (fun a -> not (before.Contains a))

                let components =
                    fresh
                    |> Seq.collect Reflect.safeTypes
                    |> Reflect.componentsOf

                Ok { Components = components; EvalMs = ms }
            | Choice2Of2 e, errors -> Error (e.Message :: errors)
            | _, errors -> Error errors)

// ---------------------------------------------------------------------------------------------------------------
// P3: declaration-scoped evaluation

type Declaration =
    {
        Kind: string
        StartLine: int
        EndLine: int
        Text: string
        /// The bound name of a single-binding let; null otherwise.
        Name: string
        /// `let name = expr` (a value), as opposed to `let name args = ...` (a function).
        IsValue: bool
        IsPublic: bool
        IsMutable: bool
    }

let rec private headOf (p: SynPat) =
    match p with
    | SynPat.Named (ident = SynIdent (id, _); accessibility = access) -> Some (id.idText, access.IsNone, true)
    | SynPat.Typed (pat = inner)
    | SynPat.Paren (pat = inner)
    | SynPat.Attrib (pat = inner) -> headOf inner
    | SynPat.LongIdent (longDotId = SynLongIdent (id = ids); argPats = SynArgPats.Pats []; accessibility = access) ->
        Some ((List.last ids).idText, access.IsNone, true)
    | SynPat.LongIdent (longDotId = SynLongIdent (id = ids); accessibility = access) -> Some ((List.last ids).idText, access.IsNone, false)
    | _ -> None

let private parser = lazy (FSharpChecker.Create ())

/// Top-level declarations of a file with a single top-level module, or None for any other shape.
let declarations (path: string) (text: string) : (string * Declaration list) option =
    let options =
        { FSharpParsingOptions.Default with
            SourceFiles = [| path |]
        }

    let parsed =
        parser.Value.ParseFile (path, SourceText.ofString text, options)
        |> Async.RunSynchronously

    let lines = text.Replace("\r\n", "\n").Split '\n'

    match parsed.ParseTree with
    | ParsedInput.ImplFile (ParsedImplFileInput (
        contents = [ SynModuleOrNamespace (longId = lid; kind = SynModuleOrNamespaceKind.NamedModule; decls = decls) ])) when
        not parsed.ParseHadErrors
        ->
        let name =
            lid
            |> List.map (fun i -> i.idText)
            |> String.concat "."

        let decls =
            decls
            |> List.map (fun d ->
                let kind =
                    match d with
                    | SynModuleDecl.Let _ -> "let"
                    | SynModuleDecl.Open _ -> "open"
                    | SynModuleDecl.HashDirective _ -> "directive"
                    | _ -> "type"

                let name, isPublic, isValue, isMutable =
                    match d with
                    | SynModuleDecl.Let (bindings = [ SynBinding (accessibility = access; isMutable = m; headPat = pat) ]) ->
                        match headOf pat with
                        | Some (n, pub, value) -> n, pub && access.IsNone, value, m
                        | None -> null, false, false, m
                    | _ -> null, false, false, false

                let r = d.Range

                {
                    Kind = kind
                    StartLine = r.StartLine
                    EndLine = r.EndLine
                    Text = String.Join ("\n", lines[r.StartLine - 1 .. r.EndLine - 1])
                    Name = name
                    IsValue = isValue
                    IsPublic = isPublic
                    IsMutable = isMutable
                })

        Some (name, decls)
    | _ -> None

/// A shadow module holding only a file's opens and let declarations, opening the existing module so the new
/// closures bind to the types already in use. Line directives keep closure names and diagnostics on the
/// original lines.
let shadowModule
    (path: string)
    (moduleName: string)
    (generation: int)
    (decls: Declaration list)
    (aliasOf: Declaration -> string option)
    (abbreviations: (string * string) list)
    =
    let sb = StringBuilder ()
    let source = path.Replace ('\\', '/')

    sb.Append ($"module {moduleName}__hmr{generation}\n")
    |> ignore

    for d in decls |> List.filter (fun d -> d.Kind = "open") do
        sb.Append($"# {d.StartLine} \"{source}\"\n").Append(d.Text).Append('\n')
        |> ignore

    sb.Append ($"open {moduleName}\n") |> ignore

    // A dependent re-evaluated after a module it uses was reloaded: its names for that module mean the latest
    // generation. Qualified uses go through the abbreviation; an `open` of it is repeated for the latest one.
    for (original, latest) in abbreviations do
        let short = original.Substring (original.LastIndexOf '.' + 1)

        sb.Append ($"module {short} = {latest}\n")
        |> ignore

        if
            decls
            |> List.exists (fun d ->
                d.Kind = "open"
                && (d.Text.Trim () = $"open {original}"
                    || d.Text.Trim () = $"open {short}"))
        then
            sb.Append ($"open {latest}\n") |> ignore

    for d in decls |> List.filter (fun d -> d.Kind = "let") do
        match aliasOf d with
        | Some definedIn ->
            // An unchanged module-level value keeps its identity (a store, a cache): bind the live one.
            sb.Append($"# {d.StartLine} \"{source}\"\n").Append($"let {d.Name} = {definedIn}.{d.Name}\n")
            |> ignore
        | None ->
            sb.Append($"# {d.StartLine} \"{source}\"\n").Append(d.Text).Append('\n')
            |> ignore

    sb.ToString ()

// ---------------------------------------------------------------------------------------------------------------
// P3: the Ranvier.FSharp.Checker gate (declaration-incremental type checking)

/// The compiler command line the checker and the stamp context share: options, then sources.
let compilerArgs (project: Project) : string list =
    [
        yield "--noframework"
        yield "--targetprofile:netcore"
        yield "--target:exe"
        yield "--define:DEBUG"
        yield
            "-o:"
            + Path.Combine (project.Dir, "obj", "hmr", "checker", project.Name + ".dll")
        for r in project.Framework do
            yield "-r:" + r
        for r in project.References |> List.skip 1 do
            yield "-r:" + r
    ]

type GateResult =
    {
        Errors: string list
        /// Declarations whose check re-ran, as the checker describes them.
        Rechecked: string list
        /// Whether the file was checked by declaration. A file-level check publishes no declarations, so an empty
        /// Rechecked then says nothing.
        DeclarationMode: bool
        CheckMs: float
    }

/// What a declaration's check marked it as using: TypedUses (public; uses through types) and References (internal
/// in the fickle FCS: the module values it names), read reflectively here.
let private referencesProperty =
    typeof<FSharp.Compiler.Declarations.DeclarationResult>
        .GetProperty(
            "References",
            BindingFlags.Instance
            ||| BindingFlags.Public
            ||| BindingFlags.NonPublic
        )

/// (file, declaration key) of each use. A use in another file has no key on its DeclarationRef; References then
/// gives the value's module path and name, which is its key.
let usesOf (r: FSharp.Compiler.Declarations.DeclarationResult) : (string * string option) seq =
    seq {
        for u in r.TypedUses do
            yield u.File, u.Key |> Option.map Stamps.keyText

        if not (isNull referencesProperty) then
            match referencesProperty.GetValue r with
            | :? Array as items ->
                for item in items do
                    match item with
                    | :? (FSharp.Compiler.Declarations.DeclarationRef * string list * string) as entry ->
                        let (d, path, name) = entry

                        match d.Key with
                        | Some k -> yield d.File, Some (Stamps.keyText k)
                        | None -> yield d.File, Some (String.Join (".", path) + "." + name + ":Value")
                    | _ -> ()
            | _ -> ()
    }

type CheckerGate(project: Project) =
    let rechecked = ConcurrentQueue<string * string>()
    let mutable graph: Rfs.Checker.ProjectGraph = Unchecked.defaultof<_>
    let mutable host: Rfs.Checker.GraphHost = null

    member _.Start() : float =
        let sw = Stopwatch.StartNew ()

        let spec: Rfs.Projects.ProjectSpec =
            {
                ProjectFile = project.Fsproj
                SourceFiles = project.Sources
                OtherOptions = compilerArgs project
                ReferencedProjects = []
                ReferencedFrameworks = []
                TargetFramework = "net10.0"
                GeneratedFiles = []
                Items = []
            }

        host <- Rfs.Checker.GraphHost.Start "ranvier-hmr"
        graph <- Rfs.Checker.ProjectGraph.Create(host, spec).GetAwaiter().GetResult()

        graph.Checked.Subscribe (fun (key, file) -> rechecked.Enqueue (Rfs.Checker.ProjectGraph.DescribeKey key, file))
        |> ignore

        for f in project.Sources do
            graph.Promote f

        // Promotion builds each file by declaration in the background; until it finishes, the file is checked whole.
        for f in project.Sources do
            graph.WhenPromoted(f, Threading.CancellationToken.None).GetAwaiter().GetResult()
            |> ignore

            graph.Diagnostics(f).GetAwaiter().GetResult()
            |> ignore

        sw.Elapsed.TotalMilliseconds

    /// Declarations (file name, stamp key) that use any of `changed` through their checked contents, to a
    /// fixpoint. A use of a declaration `stops` says is a component does not propagate: components are found by key
    /// at mount, so what uses one needs no re-evaluation.
    member _.Dependents(changed: HashSet<struct (string * string)>, stops: string -> bool) : HashSet<struct (string * string)> =
        let byFile =
            project.Sources
            |> List.map (fun f ->
                f,
                (try
                    graph.Results(f).GetAwaiter().GetResult()
                 with _ ->
                     null))

        let found = HashSet<struct (string * string)>()

        let frontier =
            HashSet<struct (string * string)>(
                changed
                |> Seq.filter (fun (struct (_, k)) -> not (stops k))
            )

        if Environment.GetEnvironmentVariable "RANVIER_HMR_DEBUG_DEPS" = "1" then
            Console.WriteLine (
                "[deps] changed: "
                + String.Join (
                    ", ",
                    changed
                    |> Seq.map (fun (struct (f, k)) -> f + ":" + k)
                )
            )

            for (f, results) in byFile do
                if isNull results then
                    Console.WriteLine $"[deps] {Path.GetFileName f}: no results"
                else
                    for KeyValue (k, r) in results do
                        let uses =
                            usesOf r
                            |> Seq.map (fun (file, key) -> Path.GetFileName file + ":" + defaultArg key "?")
                            |> Seq.truncate 8

                        Console.WriteLine (
                            $"[deps] {Path.GetFileName f}:{Stamps.keyText k} uses "
                            + String.Join (", ", uses)
                        )

        let mutable grew = true

        while grew do
            grew <- false

            for (f, results) in byFile do
                if not (isNull results) then
                    for KeyValue (k, r) in results do
                        let me = struct (Path.GetFileName f, Stamps.keyText k)

                        if
                            not (changed.Contains me)
                            && not (found.Contains me)
                        then
                            let uses =
                                usesOf r
                                |> Seq.exists (fun (file, key) ->
                                    match key with
                                    | Some k -> frontier.Contains (struct (Path.GetFileName file, k))
                                    | None -> false)

                            if uses then
                                found.Add me |> ignore

                                if not (stops (Stamps.keyText k)) then
                                    frontier.Add me |> ignore

                                grew <- true

        found

    member _.Check(file: string, text: string) : GateResult =
        let sw = Stopwatch.StartNew ()
        rechecked.Clear ()
        graph.SetText (file, text) |> ignore

        let struct (_, diagnostics) =
            graph.SettledDiagnostics(file).GetAwaiter().GetResult()

        let mode = graph.Mode(file).GetAwaiter().GetResult()

        {
            Errors = errorsOf diagnostics
            Rechecked =
                rechecked
                |> Seq.map fst
                |> Seq.distinct
                |> Seq.toList
            DeclarationMode = (mode = Rfs.Checker.FileMode.Declarations)
            CheckMs = sw.Elapsed.TotalMilliseconds
        }
