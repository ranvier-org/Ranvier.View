/// Declaration stamps: what the fingerprinting dev compiler (tools/Ranvier.View.WebCore.Hmr.Fsc) embeds in the
/// assembly, and what the dev host computes again for each reload. Both compile this one file, so both sides
/// compute identical values. Declaration boundaries and keys come from the fickle FCS declaration splitter.
/// The hash is FNV-1a over the declaration's text: stable across processes, unlike string.GetHashCode.
module Ranvier.View.WebCore.Hmr.Stamps

open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Declarations
open FSharp.Compiler.Text

type Stamp =
    {
        /// "Module.Path.name:Kind" (#ordinal when a name repeats in the same module).
        Key: string
        Hash: string
        StartLine: int
        EndLine: int
    }

/// The metadata key prefix of a stamp: hmr:<file name>:<declaration key>.
let prefix = "hmr:"
let compilerKey = "hmr-compiler"

let keyText (k: DeclarationKey) =
    let path = String.Join (".", k.ModulePath)
    let ordinal = if k.Ordinal > 0 then $"#{k.Ordinal}" else ""
    $"{path}.{k.Name}:{k.Kind}{ordinal}"

let fnv (text: string) =
    let mutable h = 0xcbf29ce484222325UL

    for c in text.Replace ("\r\n", "\n") do
        h <- (h ^^^ uint64 c) * 0x100000001b3UL

    h.ToString "x16"

let private checker = lazy (FSharpChecker.Create ())

/// A declaration-splitting context for a project, from its compiler command line (options and sources).
/// Split only accepts parses made in such a context; building it reads the project's references once.
let context (projectFile: string) (args: string array) : ProjectContext =
    let isSource (a: string) =
        not (a.StartsWith "-")
        && (a.EndsWith (".fs", StringComparison.OrdinalIgnoreCase)
            || a.EndsWith (".fsi", StringComparison.OrdinalIgnoreCase))

    // The shape Rfs.Projects.ProjectSpec.toFcsOptions builds: sources apart from the other options.
    let options: FSharpProjectOptions =
        {
            ProjectFileName = projectFile
            ProjectId = None
            SourceFiles =
                args
                |> Array.filter isSource
                |> Array.map Path.GetFullPath
            OtherOptions = args |> Array.filter (isSource >> not)
            ReferencedProjects = [||]
            IsIncompleteTypeCheckEnvironment = false
            UseScriptResolutionRules = false
            LoadTime = DateTime.Now
            UnresolvedReferences = None
            OriginalLoadReferences = []
            Stamp = None
        }

    DeclarationChecker.CreateProject options
    |> Async.RunSynchronously

/// The stamps of a file's top-level declarations.
let ofText (ctx: ProjectContext) (path: string) (text: string) : Stamp array =
    let parsed = DeclarationChecker.Parse (ctx, path, text)

    DeclarationChecker.Split parsed
    |> Array.map (fun d ->
        {
            Key = keyText d.Key
            Hash = fnv d.Text
            StartLine = d.Range.StartLine
            EndLine = d.Range.EndLine
        })

let ofFile (ctx: ProjectContext) (path: string) =
    ofText ctx path (File.ReadAllText path)

/// Changed, added and removed declaration keys between two stamp sets of one file.
let changed (before: Stamp array) (after: Stamp array) : string list =
    let b =
        before
        |> Array.map (fun s -> s.Key, s.Hash)
        |> Map.ofArray

    let a =
        after
        |> Array.map (fun s -> s.Key, s.Hash)
        |> Map.ofArray

    [
        for KeyValue (k, h) in a do
            if Map.tryFind k b <> Some h then
                yield k
        for KeyValue (k, _) in b do
            if not (a.ContainsKey k) then
                yield k
    ]
