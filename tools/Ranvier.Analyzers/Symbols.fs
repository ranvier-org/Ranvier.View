/// Full names of the symbols the rules recognise, and the reference gates of the rule sets.
module Ranvier.Analyzers.Symbols

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols

/// The full name of the entity that declares a member or, for an extension member, the entity it extends; empty for a
/// local value.
let enclosing (m: FSharpMemberOrFunctionOrValue) : string =
    match m.ApparentEnclosingEntity with
    | Some entity -> defaultArg entity.TryFullName ""
    | None -> ""

/// Whether `m` is the member or function `name` of `entity`.
let isMember (entity: string) (name: string) (m: FSharpMemberOrFunctionOrValue) : bool =
    m.LogicalName = name && enclosing m = entity

/// The name a message gives `m`: the type's name for a constructor, the CE builder keyword for a `.Run` on `Ranvier.CE`,
/// the display name otherwise.
let display (m: FSharpMemberOrFunctionOrValue) : string =
    match enclosing m, m.LogicalName with
    | "Ranvier.CE.MemoBuilder", "Run" -> "memo"
    | "Ranvier.CE.EffectBuilder", "Run" -> "effect"
    | "Ranvier.CE.AsyncMemoBuilder", "Run" -> "asyncMemo"
    | "Ranvier.CE.StreamBuilder", "Run" -> "stream"
    | "Ranvier.CE.EditableBuilder", "Run" -> "editable"
    | "Ranvier.CE.DraftBuilder", "Run" -> "draft"
    | "Ranvier.CE.BoundaryBuilder", "Run" -> "boundary"
    | "Ranvier.CE.RowBuilder", "Run" -> "row"
    | _ ->
        match m.ApparentEnclosingEntity with
        | Some entity when m.IsConstructor -> entity.DisplayName
        | _ -> m.DisplayName

[<Literal>]
let Operators = "Microsoft.FSharp.Core.Operators"

[<Literal>]
let StringType = "System.String"

[<Literal>]
let PipeRight = "op_PipeRight"

[<Literal>]
let PipeLeft = "op_PipeLeft"

/// Whether `m` is the pipe operator `name` (`PipeRight` or `PipeLeft`).
let isPipe (name: string) (m: FSharpMemberOrFunctionOrValue) : bool =
    m.LogicalName = name && enclosing m = Operators

[<Literal>]
let CompilerGenerated = "System.Runtime.CompilerServices.CompilerGeneratedAttribute"

/// The case-name prefix of a tester property: `IsQueue` tests `Queue`.
[<Literal>]
let TesterProperty = "Is"

/// The prefix of a generated union case tester's getter: `get_IsQueue` tests `Queue`.
[<Literal>]
let TesterPrefix = "get_" + TesterProperty

/// The names of the equality, hashing and comparison members the compiler generates for a type.
let generatedComparisons = set [ "CompareTo"; "Equals"; "GetHashCode" ]

/// The path that source code names a module, namespace or type by: its namespace, then the display names of its
/// enclosing modules and types. A module compiled with the `Module` suffix keeps its source name.
let rec pathOf (entity: FSharpEntity) : string =
    if entity.IsNamespace then
        match entity.AccessPath with
        | null
        | ""
        | "global" -> entity.DisplayName
        | access -> access + "." + entity.DisplayName
    else
        match entity.DeclaringEntity with
        | Some parent -> pathOf parent + "." + entity.DisplayName
        | None ->
            match entity.Namespace with
            | Some space when space <> "" -> space + "." + entity.DisplayName
            | _ -> entity.DisplayName

/// The source path of the module, namespace or type that declares `entity`; empty at the global level.
let containerOf (entity: FSharpEntity) : string =
    match entity.DeclaringEntity with
    | Some parent -> pathOf parent
    | None -> defaultArg entity.Namespace ""

let private referencedFiles (options: AnalyzerProjectOptions) : string seq =
    options.OtherOptions
    |> Seq.choose (fun option ->
        if option.StartsWith ("-r:", StringComparison.Ordinal) then
            Some (Path.GetFileName (option.Substring 3))
        elif option.StartsWith ("--reference:", StringComparison.Ordinal) then
            Some (Path.GetFileName (option.Substring 12))
        else
            None)

/// Whether the project's `-r:` options name the assembly file `assembly`.
let references (assembly: string) (options: AnalyzerProjectOptions) : bool =
    referencedFiles options
    |> Seq.exists (fun file -> String.Equals (file, assembly, StringComparison.OrdinalIgnoreCase))

/// The assembly files whose reference enables a rule set.
module Assemblies =
    [<Literal>]
    let Ranvier = "Ranvier.dll"

    [<Literal>]
    let View = "Ranvier.View.dll"

    [<Literal>]
    let Dom = "Ranvier.View.Dom.dll"

    [<Literal>]
    let Fable = "Fable.Core.dll"

/// The F#-to-JavaScript compiler a project declares in its tool manifest.
module Compilers =
    open System.Collections.Concurrent
    open System.Text.Json

    [<Literal>]
    let Fable = "fable"

    [<Literal>]
    let Fickl = "fickl"

    /// A declared compiler: its command, its version without a prerelease label, and whether it has one.
    type Declared =
        {
            Command: string
            Version: Version
            Prerelease: bool
        }

    /// The first Fable release whose precompile declares the generated union testers.
    let testersPrecompileFrom = Version (5, 22, 0)

    /// Whether `declared` is a Fable release before `testersPrecompileFrom`; a prerelease of it is before it.
    let precedesTesterFix (declared: Declared) : bool =
        declared.Command = Fable
        && (declared.Version < testersPrecompileFrom
            || (declared.Version = testersPrecompileFrom
                && declared.Prerelease))

    let private parse (text: string) : (Version * bool) option =
        let core = text.Split('-', '+').[0]

        match Version.TryParse core with
        | true, version -> Some (version, text.Contains '-')
        | _ -> None

    let private options =
        JsonDocumentOptions (CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)

    /// A manifest's declared compiler, and whether the manifest is a root.
    let private read (file: string) : Declared option * bool =
        try
            use document = JsonDocument.Parse (File.ReadAllText file, options)
            let root = document.RootElement

            let isRoot =
                root.EnumerateObject ()
                |> Seq.exists (fun property ->
                    String.Equals (property.Name, "isRoot", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind = JsonValueKind.True)

            let tools =
                root.EnumerateObject ()
                |> Seq.tryFind (fun property -> String.Equals (property.Name, "tools", StringComparison.OrdinalIgnoreCase))

            let declared =
                match tools with
                | Some tools when tools.Value.ValueKind = JsonValueKind.Object ->
                    [ Fickl; Fable ]
                    |> List.tryPick (fun command ->
                        tools.Value.EnumerateObject ()
                        |> Seq.tryFind (fun tool -> String.Equals (tool.Name, command, StringComparison.OrdinalIgnoreCase))
                        |> Option.bind (fun tool ->
                            tool.Value.EnumerateObject ()
                            |> Seq.tryFind (fun property -> String.Equals (property.Name, "version", StringComparison.OrdinalIgnoreCase))
                            |> Option.bind (fun version -> parse (version.Value.GetString ())))
                        |> Option.map (fun (version, prerelease) ->
                            {
                                Command = command
                                Version = version
                                Prerelease = prerelease
                            }))
                | _ -> None

            declared, isRoot
        with _ ->
            None, false

    // Each manifest's reading, keyed by its path and valid while its last write time is unchanged.
    let private readings =
        ConcurrentDictionary<string, DateTime * (Declared option * bool)>()

    let private manifestOf (file: string) : Declared option * bool =
        let written = File.GetLastWriteTimeUtc file

        match readings.TryGetValue file with
        | true, (stamp, reading) when stamp = written -> reading
        | _ ->
            let reading = read file
            readings[file] <- (written, reading)
            reading

    /// The compiler, `fable` or `fickl`, that the tool manifests above `projectFile` declare: the nearest manifest that
    /// declares one, searching up to the first root manifest. An edited or added manifest takes effect on the next call.
    let declared (projectFile: string) : Declared option =
        let rec up (directory: string) =
            if String.IsNullOrEmpty directory then
                None
            else
                let manifest =
                    [
                        Path.Combine (directory, ".config", "dotnet-tools.json")
                        Path.Combine (directory, "dotnet-tools.json")
                    ]
                    |> List.tryFind File.Exists

                match manifest |> Option.map manifestOf with
                | Some (Some found, _) -> Some found
                | Some (None, true) -> None
                | _ -> up (Path.GetDirectoryName directory)

        if String.IsNullOrEmpty projectFile then
            None
        else
            up (Path.GetDirectoryName (Path.GetFullPath projectFile))

    /// The compile symbol that opts a project into the Fable rules: `RanvierFableAnalyzers=true` defines it.
    [<Literal>]
    let OptInDefine = "RANVIER_FABLE_ANALYZERS"

    /// Whether the project's compiler options define `OptInDefine`.
    let optedIn (options: AnalyzerProjectOptions) : bool =
        options.OtherOptions
        |> Seq.exists (fun option ->
            option = "--define:" + OptInDefine
            || option = "-d:" + OptInDefine)

    let private readsFable (file: string) : bool =
        try
            Xml.Linq.XDocument.Load(file).Descendants()
            |> Seq.exists (fun element ->
                element.Name.LocalName = "PackageReference"
                && (match element.Attribute (Xml.Linq.XName.Get "Include") with
                    | null -> false
                    | reference -> reference.Value.StartsWith ("Fable.", StringComparison.OrdinalIgnoreCase)))
        with _ ->
            false

    // Each project file's reading, keyed by its path and valid while its last write time is unchanged.
    let private projectReadings = ConcurrentDictionary<string, DateTime * bool>()

    let private referencesFable (file: string) : bool =
        let written = File.GetLastWriteTimeUtc file

        match projectReadings.TryGetValue file with
        | true, (stamp, reading) when stamp = written -> reading
        | _ ->
            let reading = readsFable file
            projectReadings[file] <- (written, reading)
            reading

    /// The project files of the project that compiles `source`: the options' project file when it exists, otherwise
    /// the project files of the nearest directory above `source` that holds one.
    let private projectFiles (options: AnalyzerProjectOptions) (source: string) : string list =
        if File.Exists options.ProjectFileName then
            [ options.ProjectFileName ]
        else
            let rec up (directory: string) =
                if String.IsNullOrEmpty directory then
                    []
                else
                    match
                        Directory.GetFiles (directory, "*.fsproj")
                        |> List.ofArray
                    with
                    | [] -> up (Path.GetDirectoryName directory)
                    | found -> found

            try
                up (Path.GetDirectoryName (Path.GetFullPath source))
            with _ ->
                []

    /// The Fable signals of a project: the compiler its tool manifests declare, and whether anything says that Fable
    /// compiles it.
    type Signal =
        {
            Declared: Declared option
            CompilesWithFable: bool
        }

    /// The Fable signals of the project that compiles `source`. Fable compiles the project when a tool manifest
    /// declares `fable` or `fickl`, the project file references a `Fable.*` package, or the options define
    /// `OptInDefine`.
    let signal (options: AnalyzerProjectOptions) (source: string) : Signal =
        let anchor =
            if File.Exists options.ProjectFileName then
                options.ProjectFileName
            else
                source

        let declared = declared anchor

        {
            Declared = declared
            CompilesWithFable =
                declared.IsSome
                || optedIn options
                || projectFiles options source
                   |> List.exists referencesFable
        }

/// The core node types, their creators and their pure contexts.
module Core =
    [<Literal>]
    let Api = "Ranvier.Api"

    let private valueTypes =
        set
            [
                "Ranvier.Signal`1"
                "Ranvier.AsyncSource`1"
                "Ranvier.Memo`1"
                "Ranvier.AsyncMemo`1"
                "Ranvier.StreamMemo`1"
                "Ranvier.Timed`1"
                "Ranvier.Boundary`1"
                "Ranvier.Editable`1"
            ]

    let private keyedTypes = set [ "Ranvier.Projection`2"; "Ranvier.Lookup`2" ]

    /// Whether `m` is a tracked read: `Value`, `TryValue` or `Status` of a node, `Get`, `TryGet` or `Keys` of a
    /// projection or lookup. `Peek` is untracked.
    let isReactiveRead (m: FSharpMemberOrFunctionOrValue) : bool =
        let entity = enclosing m

        (valueTypes.Contains entity
         && (m.LogicalName = "get_Value"
             || m.LogicalName = "get_TryValue"
             || m.LogicalName = "get_Status"))
        || (keyedTypes.Contains entity
            && (m.LogicalName = "Get"
                || m.LogicalName = "TryGet"
                || m.LogicalName = "get_Keys"))

    /// The `Ranvier.CE` computation-expression builder kind that declares or extends `m`.
    [<RequireQualifiedAccess>]
    type CeKind =
        | Memo
        | Effect
        | AsyncMemo
        | Stream
        | Editable
        | Draft
        | Boundary
        | Row

    /// The ownership/mode variant selected by a `MemoBuilder.Run` overload.
    [<RequireQualifiedAccess>]
    type MemoRunMode =
        | Pure
        | Owning
        | Option
        | Tail

    /// The ownership variant selected by an `AsyncMemoBuilder.Run` overload.
    [<RequireQualifiedAccess>]
    type AsyncRunMode =
        | Pure
        | Owning

    /// What kind of computation-expression `Bind` or `BindReturn` overload a call selects.
    [<RequireQualifiedAccess>]
    type CeBindKind =
        | Reactive
        | TaskAwait
        | ContextToken

    let private ceBuilders: Map<string, CeKind> =
        Map.ofList
            [
                "Ranvier.CE.MemoBuilder", CeKind.Memo
                "Ranvier.CE.EffectBuilder", CeKind.Effect
                "Ranvier.CE.AsyncMemoBuilder", CeKind.AsyncMemo
                "Ranvier.CE.StreamBuilder", CeKind.Stream
                "Ranvier.CE.EditableBuilder", CeKind.Editable
                "Ranvier.CE.DraftBuilder", CeKind.Draft
                "Ranvier.CE.BoundaryBuilder", CeKind.Boundary
                "Ranvier.CE.RowBuilder", CeKind.Row
            ]

    /// The `Ranvier.CE` builder kind of `m`, when `m` is a member or extension member of a `Ranvier.CE` builder.
    let ceBuilder (m: FSharpMemberOrFunctionOrValue) : CeKind option =
        ceBuilders |> Map.tryFind (enclosing m)

    /// The `Ranvier.CE` builder kind when `m` is a `.Run` method of that builder.
    let isCeRun (m: FSharpMemberOrFunctionOrValue) : CeKind option =
        if m.LogicalName = "Run" then ceBuilder m else None

    let private typeFullName (t: FSharpType) : string option =
        if t.HasTypeDefinition then
            t.TypeDefinition.TryFullName
        else
            None

    let private param0Type (m: FSharpMemberOrFunctionOrValue) : FSharpType option =
        if
            m.CurriedParameterGroups.Count > 0
            && m.CurriedParameterGroups[0].Count > 0
        then
            Some (m.CurriedParameterGroups[0].[0].Type)
        else
            None

    let private param0RangeType (m: FSharpMemberOrFunctionOrValue) : FSharpType option =
        param0Type m
        |> Option.bind (fun t ->
            if t.IsFunctionType && t.GenericArguments.Count = 2 then
                Some t.GenericArguments[1]
            else
                None)

    /// The `MemoRunMode` encoded in the parameter type of `MemoBuilder.Run`.
    let memoRunMode (m: FSharpMemberOrFunctionOrValue) : MemoRunMode =
        match param0RangeType m with
        | Some t ->
            match typeFullName t with
            | Some ("Ranvier.Internal.MemoTail`2" | "Ranvier.Internal.ConfiguredMemoTail`4") -> MemoRunMode.Tail
            | Some "Ranvier.Internal.ConfiguredMemo`5" when t.GenericArguments.Count > 0 ->
                match typeFullName t.GenericArguments[0] with
                | Some "Ranvier.Internal.OwningMode" -> MemoRunMode.Owning
                | Some "Ranvier.Internal.OptionMode" -> MemoRunMode.Option
                | _ -> MemoRunMode.Pure
            | _ -> MemoRunMode.Pure
        | None -> MemoRunMode.Pure

    /// The `AsyncRunMode` encoded in the parameter type of `AsyncMemoBuilder.Run`.
    let asyncRunMode (m: FSharpMemberOrFunctionOrValue) : AsyncRunMode =
        match param0RangeType m with
        | Some t when
            typeFullName t = Some "Ranvier.Internal.ConfiguredAsync`5"
            && t.GenericArguments.Count > 0
            ->
            match typeFullName t.GenericArguments[0] with
            | Some "Ranvier.Internal.OwningMode" -> AsyncRunMode.Owning
            | _ -> AsyncRunMode.Pure
        | _ -> AsyncRunMode.Pure

    let private headerTypes =
        set
            [
                "Ranvier.Internal.MemoHeader`2"
                "Ranvier.Internal.EffectHeader`1"
                "Ranvier.Internal.AsyncHeader`2"
                "Ranvier.Internal.StreamHeader`1"
                "Ranvier.Internal.BoundaryHeader`3"
            ]

    /// Whether `m` is the header-carrying `For` overload of a `Ranvier.CE` builder.
    let isCeHeaderFor (m: FSharpMemberOrFunctionOrValue) : bool =
        (ceBuilder m).IsSome
        && m.LogicalName = "For"
        && (match param0Type m |> Option.bind typeFullName with
            | Some name -> headerTypes.Contains name
            | None -> false)

    let private taskAwaitTypes =
        set
            [
                "System.Threading.Tasks.Task"
                "System.Threading.Tasks.Task`1"
                "System.Threading.Tasks.ValueTask"
                "System.Threading.Tasks.ValueTask`1"
            ]

    let private contextTokenTypes =
        set
            [
                "Ranvier.Internal.MemoPreviousToken"
                "Ranvier.Internal.MemoPreviousOrToken`1"
                "Ranvier.Internal.EditablePreviousToken"
                "Ranvier.Internal.EditablePreviousOrToken`1"
                "Ranvier.Internal.DraftPreviousToken"
                "Ranvier.Internal.DraftPreviousOrToken`1"
                "Ranvier.Internal.AsyncPreviousToken"
                "Ranvier.Internal.AsyncCancellationToken"
                "Ranvier.Internal.StreamCancellationToken"
            ]

    /// The bind kind of `m` when `m` is `Bind` or `BindReturn` on a `Ranvier.CE` builder.
    let ceBindKind (m: FSharpMemberOrFunctionOrValue) : CeBindKind option =
        if
            (ceBuilder m).IsSome
            && (m.LogicalName = "Bind"
                || m.LogicalName = "BindReturn")
        then
            match param0Type m |> Option.bind typeFullName with
            | Some name when taskAwaitTypes.Contains name -> Some CeBindKind.TaskAwait
            | Some name when contextTokenTypes.Contains name -> Some CeBindKind.ContextToken
            | _ -> Some CeBindKind.Reactive
        else
            None

    /// Whether `m` is `Bind` or `BindReturn` binding `asyncMemo.cancellationToken`.
    let isCeCancellationBind (m: FSharpMemberOrFunctionOrValue) : bool =
        ceBuilder m = Some CeKind.AsyncMemo
        && (m.LogicalName = "Bind"
            || m.LogicalName = "BindReturn")
        && (param0Type m |> Option.bind typeFullName) = Some "Ranvier.Internal.AsyncCancellationToken"

    /// Whether `m` is `AsyncMemoBuilder.Policy`.
    let isCePolicyCall (m: FSharpMemberOrFunctionOrValue) : bool =
        ceBuilder m = Some CeKind.AsyncMemo
        && m.LogicalName = "Policy"

    /// Whether `Step.Lambda` immediately inside argument `index` of `count` of `m` is a `Ranvier.CE` control/continuation
    /// thunk rather than a user callback.
    let isCeStepLambda (m: FSharpMemberOrFunctionOrValue) (index: int) (count: int) : bool =
        if (ceBuilder m).IsNone then
            false
        else
            match m.LogicalName, index, count with
            | "Delay", 0, 1
            | ("For" | "Combine" | "Using" | "Bind" | "BindReturn"), 1, 2
            | ("While" | "TryWith" | "TryFinally"), (0 | 1), 2 -> true
            | _ -> false

    /// Whether `m` is `MergeSources` on a `Ranvier.CE` builder.
    let isMergeSources (m: FSharpMemberOrFunctionOrValue) : bool =
        (ceBuilder m).IsSome
        && m.LogicalName = "MergeSources"

    /// Whether `m` is `Ranvier.CE.Builders.withComparer`.
    let isWithComparer (m: FSharpMemberOrFunctionOrValue) : bool =
        enclosing m = "Ranvier.CE.Builders"
        && m.LogicalName = "withComparer"

    let private creatorFunctions =
        set
            [
                "createMemo"
                "createMemoWithComparer"
                "createMemoWith"
                "createOwningMemoWithComparer"
                "createMemoOn"
                "createMemoOnWithComparer"
                "createMemoWithOn"
                "createOwningMemoOnWithComparer"
                "createEffect"
                "createEffectNode"
                "createEffectOn"
                "createEffectOnWithComparer"
                "createAsync"
                "createAsyncWith"
                "createAsyncWithPolicy"
                "createOwningAsyncWithPolicy"
                "createAsyncOn"
                "createAsyncWithOn"
                "createAsyncOnWithPolicy"
                "createOwningAsyncOnWithPolicy"
                "createAsyncSource"
                "createStream"
                "createStreamFold"
                "createStreamOn"
                "createStreamFoldOn"
                "createSuspense"
                "createSuspenseWithComparer"
                "createErrorBoundary"
                "createErrorBoundaryWithComparer"
                "createBoundary"
                "createBoundaryWithComparer"
                "createRoot"
                "createProjection"
                "createProjectionWith"
                "createProjectionWithOptions"
                "createProjectionOnce"
                "createIndexProjection"
                "createIndexProjectionWith"
                "createLookup"
                "createSelector"
                "createOptionMemo"
                "createEditable"
                "createDraft"
                "onCleanup"
            ]

    let private creatorModules: Map<string, Set<string>> =
        let combinators = set [ "map"; "map2"; "zip"; "bind" ]

        Map.ofList
            [
                Api, creatorFunctions
                "Ranvier.TimedCombinators",
                set
                    [
                        "debounce"
                        "debounceWith"
                        "throttle"
                        "throttleWith"
                        "throttleFirst"
                        "throttleFirstWith"
                        "throttleLast"
                        "throttleLastWith"
                    ]
                "Ranvier.SignalModule", combinators
                "Ranvier.MemoModule", combinators
                "Ranvier.AsyncMemoModule", Set.add "bindWithPolicy" combinators
                "Ranvier.AsyncSourceModule", combinators
                "Ranvier.StreamMemoModule", Set.add "bindFold" combinators
                "Ranvier.ProjectionModule",
                set
                    [
                        "filter"
                        "choose"
                        "map"
                        "mapWith"
                        "sortBy"
                        "sortByDescending"
                        "take"
                        "skip"
                        "sub"
                        "groupBy"
                        "partition"
                        "distinctBy"
                        "foldGroup"
                        "fold"
                        "foldGroupBy"
                        "sumBy"
                        "countBy"
                        "exists"
                        "forall"
                        "length"
                        "isEmpty"
                        "tryHead"
                        "tryLast"
                        "tryItem"
                        "head"
                        "last"
                        "item"
                        "tryFind"
                        "tryFindKey"
                        "tryPick"
                        "find"
                        "findKey"
                        "pick"
                        "tryAverageBy"
                        "averageBy"
                        "tryMinBy"
                        "tryMaxBy"
                        "minBy"
                        "maxBy"
                    ]
                "Ranvier.GroupingModule", set [ "aggregate" ]
            ]

    let private creatorTypes =
        set [ "Ranvier.Memo`1"; "Ranvier.Effect"; "Ranvier.AsyncMemo`1" ]

    /// Whether a call of `m` creates an owned node.
    let isCreator (m: FSharpMemberOrFunctionOrValue) : bool =
        (creatorModules
         |> Map.tryFind (enclosing m)
         |> Option.exists (fun names -> names.Contains m.LogicalName))
        || (m.IsConstructor
            && creatorTypes.Contains (enclosing m))
        || (match isCeRun m with
            | Some kind -> kind <> CeKind.Row
            | None -> false)

    /// Whether the lambda argument of a call of `m` runs in the caller's run: `untrack`, `batch`, and the `Delay` of a
    /// task expression.
    let isTransparent (m: FSharpMemberOrFunctionOrValue) : bool =
        (enclosing m = Api
         && (m.LogicalName = "untrack"
             || m.LogicalName = "batch"))
        || (m.LogicalName = "Delay"
            && enclosing m = "Microsoft.FSharp.Control.TaskBuilderBase")

    /// How a pure context fails when its body creates an owned node.
    [<RequireQualifiedAccess>]
    type Pure =
        | Memo
        | MemoRead
        | Async
        | AsyncRead
        | EffectOn
        | Map
        | Mapping
        | Stream
        | Timed
        | Reader
        | LookupF
        | LookupAffected
        | Seed
        | Aggregate

    /// A lambda argument that runs as a pure derivation: its kind, the lambdas before its body, and the owning
    /// function that replaces the callee.
    type PureContext =
        {
            Kind: Pure
            Layers: int
            Owning: string option
        }

    let private pureFunctions: Map<string * string * int, PureContext> =
        let context kind layers owning =
            {
                Kind = kind
                Layers = layers
                Owning = owning
            }

        Map.ofList
            [
                (Api, "createMemo", 0), context Pure.Memo 1 (Some "createMemoWith")
                (Api, "createMemoWithComparer", 1), context Pure.Memo 1 (Some "createOwningMemoWithComparer")
                (Api, "createMemoOn", 0), context Pure.MemoRead 1 None
                (Api, "createMemoOn", 1), context Pure.Memo 1 (Some "createMemoWithOn")
                (Api, "createMemoOnWithComparer", 1), context Pure.MemoRead 1 None
                (Api, "createMemoOnWithComparer", 2), context Pure.Memo 1 (Some "createOwningMemoOnWithComparer")
                (Api, "createMemoWithOn", 0), context Pure.MemoRead 1 None
                (Api, "createOwningMemoOnWithComparer", 1), context Pure.MemoRead 1 None
                (Api, "createAsync", 0), context Pure.Async 2 (Some "createAsyncWith")
                (Api, "createAsyncWithPolicy", 1), context Pure.Async 2 (Some "createOwningAsyncWithPolicy")
                (Api, "createAsyncOn", 0), context Pure.AsyncRead 1 None
                (Api, "createAsyncOn", 1), context Pure.Async 3 (Some "createAsyncWithOn")
                (Api, "createAsyncOnWithPolicy", 1), context Pure.AsyncRead 1 None
                (Api, "createAsyncOnWithPolicy", 2), context Pure.Async 3 (Some "createOwningAsyncOnWithPolicy")
                (Api, "createAsyncWithOn", 0), context Pure.AsyncRead 1 None
                (Api, "createOwningAsyncOnWithPolicy", 1), context Pure.AsyncRead 1 None
                (Api, "createEffectOn", 0), context Pure.EffectOn 1 None
                (Api, "createEffectOnWithComparer", 1), context Pure.EffectOn 1 None
                (Api, "createStream", 0), context Pure.Stream 1 None
                (Api, "createStreamFold", 2), context Pure.Stream 1 None
                (Api, "createStreamOn", 0), context Pure.Stream 1 None
                (Api, "createStreamOn", 1), context Pure.Stream 2 None
                (Api, "createStreamFoldOn", 2), context Pure.Stream 1 None
                (Api, "createStreamFoldOn", 3), context Pure.Stream 2 None
                (Api, "createProjection", 1), context Pure.Map 1 None
                (Api, "createIndexProjection", 0), context Pure.Map 1 None
                (Api, "createProjectionWith", 1), context Pure.Reader 2 None
                (Api, "createProjectionWithOptions", 2), context Pure.Reader 2 None
                (Api, "createIndexProjectionWith", 0), context Pure.Reader 2 None
                (Api, "createLookup", 0), context Pure.LookupF 2 None
                (Api, "createLookup", 1), context Pure.LookupAffected 2 None
                (Api, "createEditable", 0), context Pure.Seed 1 None
                (Api, "createDraft", 0), context Pure.Seed 1 None
                ("Ranvier.TimedCombinators", "debounce", 1), context Pure.Timed 1 None
                ("Ranvier.TimedCombinators", "debounceWith", 2), context Pure.Timed 1 None
                ("Ranvier.TimedCombinators", "throttle", 1), context Pure.Timed 1 None
                ("Ranvier.TimedCombinators", "throttleWith", 2), context Pure.Timed 1 None
                ("Ranvier.TimedCombinators", "throttleFirst", 1), context Pure.Timed 1 None
                ("Ranvier.TimedCombinators", "throttleFirstWith", 2), context Pure.Timed 1 None
                ("Ranvier.TimedCombinators", "throttleLast", 1), context Pure.Timed 1 None
                ("Ranvier.TimedCombinators", "throttleLastWith", 2), context Pure.Timed 1 None
                ("Ranvier.SignalModule", "map", 0), context Pure.Mapping 1 None
                ("Ranvier.SignalModule", "map2", 0), context Pure.Mapping 2 None
                ("Ranvier.MemoModule", "map", 0), context Pure.Mapping 1 None
                ("Ranvier.MemoModule", "map2", 0), context Pure.Mapping 2 None
                ("Ranvier.AsyncMemoModule", "map", 0), context Pure.Mapping 1 None
                ("Ranvier.AsyncMemoModule", "map2", 0), context Pure.Mapping 2 None
                ("Ranvier.AsyncMemoModule", "bind", 0), context Pure.AsyncRead 2 None
                ("Ranvier.AsyncMemoModule", "bindWithPolicy", 1), context Pure.AsyncRead 2 None
                ("Ranvier.AsyncSourceModule", "map", 0), context Pure.Mapping 1 None
                ("Ranvier.AsyncSourceModule", "map2", 0), context Pure.Mapping 2 None
                ("Ranvier.AsyncSourceModule", "bind", 0), context Pure.AsyncRead 2 None
                ("Ranvier.StreamMemoModule", "map", 0), context Pure.Mapping 1 None
                ("Ranvier.StreamMemoModule", "map2", 0), context Pure.Mapping 2 None
                ("Ranvier.StreamMemoModule", "bind", 0), context Pure.Stream 2 None
                ("Ranvier.StreamMemoModule", "bindFold", 2), context Pure.Stream 2 None
                ("Ranvier.ProjectionModule", "map", 0), context Pure.Map 1 None
                ("Ranvier.ProjectionModule", "foldGroup", 0), context Pure.Aggregate 2 None
                ("Ranvier.ProjectionModule", "foldGroup", 1), context Pure.Aggregate 2 None
                ("Ranvier.ProjectionModule", "fold", 0), context Pure.Aggregate 2 None
                ("Ranvier.ProjectionModule", "foldGroupBy", 1), context Pure.Aggregate 2 None
                ("Ranvier.ProjectionModule", "foldGroupBy", 2), context Pure.Aggregate 2 None
            ]

    let private owningConstructor (m: FSharpMemberOrFunctionOrValue) =
        m.CurriedParameterGroups
        |> Seq.concat
        |> Seq.exists (fun parameter ->
            parameter.Type.HasTypeDefinition
            && parameter.Type.TypeDefinition.TryFullName = Some "System.Boolean")

    /// The pure context that argument `index` of `count` arguments of a call of `m` is.
    let pureContext (m: FSharpMemberOrFunctionOrValue) (index: int) (count: int) : PureContext option =
        if m.IsConstructor then
            match enclosing m with
            | "Ranvier.Memo`1" when index = count - 1 && not (owningConstructor m) ->
                Some
                    {
                        Kind = Pure.Memo
                        Layers = 1
                        Owning = None
                    }
            | "Ranvier.AsyncMemo`1" when index = count - 1 && not (owningConstructor m) ->
                Some
                    {
                        Kind = Pure.Async
                        Layers = 2
                        Owning = None
                    }
            | _ -> None
        else
            pureFunctions
            |> Map.tryFind (enclosing m, m.LogicalName, index)

    /// The index of the compute argument of an async value's creator, when `m` is one; `count` is the call's
    /// argument count.
    let asyncComputeIndex (m: FSharpMemberOrFunctionOrValue) (count: int) : int option =
        if
            m.IsConstructor
            && enclosing m = "Ranvier.AsyncMemo`1"
        then
            Some (count - 1)
        elif isCeRun m = Some CeKind.AsyncMemo && count = 1 then
            Some 0
        else
            match enclosing m, m.LogicalName with
            | Api, ("createAsync" | "createAsyncWith") -> Some 0
            | Api, ("createAsyncWithPolicy" | "createOwningAsyncWithPolicy" | "createAsyncOn" | "createAsyncWithOn") -> Some 1
            | Api, ("createAsyncOnWithPolicy" | "createOwningAsyncOnWithPolicy") -> Some 2
            | "Ranvier.AsyncMemoModule", "bind" -> Some 0
            | "Ranvier.AsyncMemoModule", "bindWithPolicy" -> Some 1
            | "Ranvier.AsyncSourceModule", "bind" -> Some 0
            | _ -> None

    let private ownerFunctions: Map<string * string * int, int> =
        Map.ofList
            [
                (Api, "createSuspense", 1), 1
                (Api, "createSuspenseWithComparer", 2), 1
                (Api, "createErrorBoundary", 1), 1
                (Api, "createErrorBoundaryWithComparer", 2), 1
                (Api, "createBoundary", 2), 1
                (Api, "createBoundaryWithComparer", 3), 1
                (Api, "createMemoWith", 0), 1
                (Api, "createOwningMemoWithComparer", 1), 1
                (Api, "createMemoWithOn", 1), 1
                (Api, "createOwningMemoOnWithComparer", 2), 1
                (Api, "createEffect", 0), 1
                (Api, "createEffectNode", 0), 1
                (Api, "createAsyncWith", 0), 2
                (Api, "createAsyncWithOn", 1), 3
                (Api, "createOwningAsyncWithPolicy", 1), 2
                (Api, "createOwningAsyncOnWithPolicy", 2), 3
                ("Ranvier.SignalModule", "bind", 0), 1
                ("Ranvier.MemoModule", "bind", 0), 1
            ]

    /// The lambdas before the body when argument `index` of `count` arguments of a call of `m` is an owner body: a
    /// body that disposes the nodes it created before each re-run.
    let ownerBody (m: FSharpMemberOrFunctionOrValue) (index: int) (count: int) : int option =
        if
            m.IsConstructor
            && enclosing m = "Ranvier.Effect"
            && index = count - 1
        then
            Some 1
        else
            ownerFunctions
            |> Map.tryFind (enclosing m, m.LogicalName, index)

    /// Whether a call of `m` creates an async value: an async memo, an async source or a stream.
    let isAsyncCreator (m: FSharpMemberOrFunctionOrValue) : bool =
        (m.IsConstructor
         && enclosing m = "Ranvier.AsyncMemo`1")
        || (enclosing m = Api
            && (m.LogicalName.StartsWith ("createAsync", StringComparison.Ordinal)
                || m.LogicalName.StartsWith ("createStream", StringComparison.Ordinal)
                || m.LogicalName = "createOwningAsyncWithPolicy"
                || m.LogicalName = "createOwningAsyncOnWithPolicy"))
        || (enclosing m = "Ranvier.AsyncMemoModule"
            && (m.LogicalName = "bind"
                || m.LogicalName = "bindWithPolicy"))
        || (enclosing m = "Ranvier.AsyncSourceModule"
            && m.LogicalName = "bind")
        || (enclosing m = "Ranvier.StreamMemoModule"
            && (m.LogicalName = "bind"
                || m.LogicalName = "bindFold"))
        || (match isCeRun m with
            | Some (CeKind.AsyncMemo | CeKind.Stream) -> true
            | _ -> false)

    /// Whether a call of `m` writes a signal or pumps the graph: work the graph's owning thread must do.
    let isGraphWrite (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember "Ranvier.Signal`1" "set_Value" m
        || isMember "Ranvier.SignalModule" "update" m
        || isMember "Ranvier.Graph" "Pump" m

    /// Whether `m` is `Graph.Dispatch`, which runs its work on the owning thread.
    let isDispatch (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember "Ranvier.Graph" "Dispatch" m

    /// Whether a call of `m` runs its delegate argument on a thread-pool or new thread.
    let threadStarter (m: FSharpMemberOrFunctionOrValue) : bool =
        match enclosing m, m.LogicalName with
        | "System.Threading.Tasks.Task", "Run"
        | "System.Threading.Tasks.TaskFactory", "StartNew"
        | "System.Threading.ThreadPool", "QueueUserWorkItem"
        | "System.Threading.ThreadPool", "UnsafeQueueUserWorkItem"
        | "System.Threading.Thread", ".ctor"
        | "System.Threading.Tasks.Parallel", "For"
        | "System.Threading.Tasks.Parallel", "ForEach"
        | "System.Threading.Tasks.Parallel", "Invoke"
        | "System.Threading.Tasks.Task", "ContinueWith"
        | "System.Threading.Tasks.Task`1", "ContinueWith"
        | "System.Threading.Timer", ".ctor" -> true
        | _ -> false

    /// Whether `m` is a computation-expression bind, whose last argument is the continuation after an await:
    /// `Bind`, `BindReturn`, `Bind2` and the like.
    let isBind (m: FSharpMemberOrFunctionOrValue) : bool =
        m.LogicalName.StartsWith ("Bind", StringComparison.Ordinal)

    /// Whether `m` is an awaited bind whose continuation suspends on async work (`Bind*` outside `Ranvier.CE`, or
    /// `Task`/`ValueTask` binds on `AsyncMemoBuilder`).
    let isAwaitBind (m: FSharpMemberOrFunctionOrValue) : bool =
        match ceBuilder m with
        | Some CeKind.AsyncMemo -> ceBindKind m = Some CeBindKind.TaskAwait
        | Some _ -> false
        | None -> isBind m

    /// Whether `m` is a computation-expression `Combine`, whose second argument runs after its first.
    let isCombine (m: FSharpMemberOrFunctionOrValue) : bool =
        m.LogicalName = "Combine"

    /// Whether `m` is a computation-expression loop, `For` over a sequence or `While`, whose body runs again after its
    /// own awaits. Header `For` wrappers of `Ranvier.CE` builders are excluded.
    let isLoop (m: FSharpMemberOrFunctionOrValue) : bool =
        (m.LogicalName = "For" && not (isCeHeaderFor m))
        || m.LogicalName = "While"

    /// Whether `m` is a computation-expression member that runs its lambda arguments as part of the computation:
    /// `Delay`, `Combine`, a loop, `TryWith`, `TryFinally` or `Using`.
    let isControl (m: FSharpMemberOrFunctionOrValue) : bool =
        match m.LogicalName with
        | "Delay"
        | "Combine"
        | "For"
        | "While"
        | "TryWith"
        | "TryFinally"
        | "Using" -> true
        | _ -> false

    /// Whether `m` is `Graph.Run`, which runs its body with the graph active.
    let isGraphRun (m: FSharpMemberOrFunctionOrValue) : bool =
        m.DisplayName = "Run"
        && (m.ApparentEnclosingEntity
            |> Option.bind _.TryFullName)
            =
            Some "Ranvier.Graph"

    /// Whether `m` creates an async value with a flight policy: `createAsyncWithPolicy`, `createAsyncOnWithPolicy`, or
    /// their owning forms.
    let isPolicyCreator (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Api "createAsyncWithPolicy" m
        || isMember Api "createOwningAsyncWithPolicy" m
        || isMember Api "createAsyncOnWithPolicy" m
        || isMember Api "createOwningAsyncOnWithPolicy" m

    /// Whether `case` is `FlightPolicy.CancelPrevious`, which cancels a superseded flight's token.
    let isCancelPrevious (case: FSharpUnionCase) : bool =
        case.Name = "CancelPrevious"
        && case.ReturnType.HasTypeDefinition
        && case.ReturnType.TypeDefinition.TryFullName = Some "Ranvier.FlightPolicy"

    /// Whether `m` constructs a graph.
    let isGraphConstructor (m: FSharpMemberOrFunctionOrValue) : bool =
        m.IsConstructor && enclosing m = "Ranvier.Graph"

    /// Whether `m` constructs a node over the graph it takes first: a signal, an async source or a memo.
    let isNodeConstructor (m: FSharpMemberOrFunctionOrValue) : bool =
        m.IsConstructor
        && (match enclosing m with
            | "Ranvier.Signal`1"
            | "Ranvier.AsyncSource`1"
            | "Ranvier.Memo`1"
            | "Ranvier.AsyncMemo`1" -> true
            | _ -> false)

    /// Whether `m` is `untrack`.
    let isUntrack (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Api "untrack" m

    let private untrackedPhaseFunctions: Map<string * string * int, int> =
        Map.ofList
            [
                (Api, "createMemoOn", 1), 1
                (Api, "createMemoOnWithComparer", 2), 1
                (Api, "createMemoWithOn", 1), 1
                (Api, "createOwningMemoOnWithComparer", 2), 1
                (Api, "createEffectOn", 1), 1
                (Api, "createEffectOnWithComparer", 2), 1
                (Api, "createAsyncOn", 1), 3
                (Api, "createAsyncWithOn", 1), 3
                (Api, "createAsyncOnWithPolicy", 2), 3
                (Api, "createOwningAsyncOnWithPolicy", 2), 3
                (Api, "createStreamOn", 1), 2
                (Api, "createStreamFoldOn", 3), 2
                ("Ranvier.SignalModule", "map", 0), 1
                ("Ranvier.SignalModule", "map2", 0), 2
                ("Ranvier.SignalModule", "bind", 0), 1
                ("Ranvier.MemoModule", "map", 0), 1
                ("Ranvier.MemoModule", "map2", 0), 2
                ("Ranvier.MemoModule", "bind", 0), 1
                ("Ranvier.AsyncMemoModule", "map", 0), 1
                ("Ranvier.AsyncMemoModule", "map2", 0), 2
                ("Ranvier.AsyncMemoModule", "bind", 0), 2
                ("Ranvier.AsyncMemoModule", "bindWithPolicy", 1), 2
                ("Ranvier.AsyncSourceModule", "map", 0), 1
                ("Ranvier.AsyncSourceModule", "map2", 0), 2
                ("Ranvier.AsyncSourceModule", "bind", 0), 2
                ("Ranvier.StreamMemoModule", "map", 0), 1
                ("Ranvier.StreamMemoModule", "map2", 0), 2
                ("Ranvier.StreamMemoModule", "bind", 0), 2
                ("Ranvier.StreamMemoModule", "bindFold", 2), 2
            ]

    /// The lambda count before the body when argument `index` of `m` is an untracked closed Phase-2 callback.
    let untrackedPhaseLayers (m: FSharpMemberOrFunctionOrValue) (index: int) : int option =
        untrackedPhaseFunctions
        |> Map.tryFind (enclosing m, m.LogicalName, index)

    /// The lambda count from the row-factory argument `index` of `m` to its `item: unit -> 'T` reader parameter.
    let projectionFactoryLambdaCount (m: FSharpMemberOrFunctionOrValue) (index: int) : int option =
        match enclosing m, m.LogicalName, index with
        | Api, "createProjectionWith", 1
        | Api, "createProjectionWithOptions", 2
        | Api, "createIndexProjectionWith", 0 -> Some 1
        | "Ranvier.ProjectionModule", "mapWith", 0 -> Some 2
        | _ -> None

/// The view layer's builders, sugar and definitions.
module View =
    /// The namespace of the view layer's templates, sugar and server rendering.
    [<Literal>]
    let Root = "Ranvier.View"

    /// The namespace of the DOM renderer: mounting, hydration and scheduling.
    [<Literal>]
    let Dom = Root + ".Dom"

    /// The namespaces under `Root` outside the view layer's rule surface.
    let private outside =
        [ Root + ".Anime."; Root + ".Twin."; Root + ".Testing."; Root + ".Checks." ]

    open FSharp.Compiler.Symbols.FSharpExprPatterns

    /// Raises `InvalidOperationException` when the HTML parser restructures `tag` under `ancestors` (nearest first, lower
    /// case).
    let checkNesting (ancestors: string list) (tag: string) : unit =
        Ranvier.View.ContentModel.check ancestors tag

    [<Literal>]
    let Builder = Root + ".Template.TemplateBuilder`1"

    [<Literal>]
    let Attributes = Root + ".Template.AttributeBuilder`1"

    [<Literal>]
    let Properties = Root + ".Template.PropertyBuilder`1"

    [<Literal>]
    let Hooks = Root + ".Template.HookBuilder`1"

    [<Literal>]
    let Unsafe = Root + ".Template.UnsafeBuilder`1"

    [<Literal>]
    let Component = Root + ".Template.Component"

    [<Literal>]
    let ComponentType = Root + ".Template.Component`1"

    [<Literal>]
    let Sugar = Root + ".Sugar.Builders"

    [<Literal>]
    let SugarComponent = Root + ".Sugar.Builders.Component"

    [<Literal>]
    let Element = Root + ".Sugar.Builders.Element`2"

    [<Literal>]
    let Elements = Root + ".Sugar.Elements"

    [<Literal>]
    let SugarSvgElements = Root + ".Sugar.SvgElements"

    [<Literal>]
    let Source = Root + ".Template.Source`2"

    [<Literal>]
    let Hydration = Dom + ".Hydration"

    [<Literal>]
    let Microtask = Dom + ".Microtask"

    [<Literal>]
    let CommitMode = Dom + ".Scheduling.CommitMode"

    [<Literal>]
    let ComponentTag = Root + ".Sugar.Builders.ComponentTag`2"

    [<Literal>]
    let Dsl = Root + ".Dsl.Builders"

    [<Literal>]
    let DslComponent = Root + ".Dsl.Builders.Component"

    [<Literal>]
    let DslPart = Root + ".Dsl.Builders.Part"

    [<Literal>]
    let DslTagBase = Root + ".Dsl.Builders.TagBase`2"

    [<Literal>]
    let DslComponentTag = Root + ".Dsl.Builders.ComponentTag`2"

    [<Literal>]
    let DslPropsPrefix = Root + ".Dsl.Props."

    [<Literal>]
    let DslElements = Root + ".Dsl.Elements"

    [<Literal>]
    let DslElement = Root + ".Dsl.Elements.Element`2"

    [<Literal>]
    let DslTagScope = Root + ".Dsl.Elements.TagScope`2"

    [<Literal>]
    let DslHtmlElement = Root + ".Dsl.Elements.HtmlElement`2"

    [<Literal>]
    let DslSvgElements = Root + ".Dsl.SvgElements"

    [<Literal>]
    let HtmlTemplateGroupsPrefix = Root + ".HtmlTemplate.Groups."

    /// Whether `m` belongs to the view layer, by its enclosing entity's namespace.
    let isViewMember (m: FSharpMemberOrFunctionOrValue) : bool =
        let entity = enclosing m

        entity.StartsWith (Root + ".", StringComparison.Ordinal)
        && not (
            outside
            |> List.exists (fun prefix -> entity.StartsWith (prefix, StringComparison.Ordinal))
        )

    /// Whether argument `index` of a call of `m` is read per region read: the arms of `matchWith` and `branch`, the
    /// key and items of `each`, `forEach` and `forEachFixed`.
    let isRegionLambda (m: FSharpMemberOrFunctionOrValue) (index: int) : bool =
        let entity = enclosing m

        (entity = Builder || entity = Sugar || entity = Dsl)
        && (match m.LogicalName, index with
            | ("matchWith" | "branch"), 1 -> true
            | ("each" | "forEach" | "forEachFixed"), (0 | 1) -> true
            | _ -> false)

    /// Whether a call of `m` creates a component definition.
    let isDefinitionCreator (m: FSharpMemberOrFunctionOrValue) =
        match enclosing m, m.LogicalName with
        | Component, "define"
        | (SugarComponent | DslComponent), ("template" | "ofMembers" | "ofClass")
        | (Sugar | Dsl), ("armOf" | "show" | "showElse") -> true
        | _ -> false

    [<Literal>]
    let UnsafeHtmlPart = "Unsafe.html"

    [<Literal>]
    let WidgetPart = "Hooks.widget"

    /// The raw-HTML flag and display name of the content part that `expr` is.
    let rec contentPart (expr: FSharpExpr) : (bool * string) option =
        match expr with
        | Coerce (_, inner) -> contentPart inner
        | Call (_, m, _, _, _) when enclosing m = Unsafe && m.LogicalName = "html" -> Some (true, UnsafeHtmlPart)
        | Call (_, m, _, _, _) when enclosing m = Hooks && m.LogicalName = "widget" -> Some (false, WidgetPart)
        | Call (_, m, _, _, _) when
            enclosing m = DslTagBase
            && m.LogicalName = "unsafeHtml"
            ->
            Some (true, "unsafeHtml")
        | Call (_, m, _, _, _) when
            enclosing m = DslTagBase
            && m.LogicalName = "widget"
            ->
            Some (false, "widget")
        | _ -> None

    /// The void HTML elements.
    let voidTags =
        set
            [
                "area"
                "base"
                "br"
                "col"
                "embed"
                "hr"
                "img"
                "input"
                "link"
                "meta"
                "source"
                "track"
                "wbr"
            ]

    /// The raw-text HTML elements.
    let rawTextTags =
        set
            [
                "script"
                "style"
                "iframe"
                "noscript"
                "xmp"
                "noembed"
                "noframes"
                "plaintext"
            ]

    /// Whether `case` is the queued commit mode, which hydration refuses.
    let isQueuedCommits (case: FSharpUnionCase) : bool =
        case.Name = "QueuedCommits"
        && case.ReturnType.HasTypeDefinition
        && case.ReturnType.TypeDefinition.TryFullName = Some CommitMode

    /// Whether `m` returns a queued commit mode: `Microtask.mode`.
    let isQueuedModeFunction (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Microtask "mode" m

    /// Whether `m` is `Hydration.hydrateWith`, whose first argument is the commit mode.
    let isHydrateWith (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Hydration "hydrateWith" m

    /// The parameter name of a builder member's static value overload, beside its `read` overload.
    [<Literal>]
    let ValueParameter = "value"

    /// The parameter name of a builder member's reader overload.
    [<Literal>]
    let ReaderParameter = "read"

    /// Whether `m` yields or builds a text node: the element CE's `Yield` or the builder's `text`.
    let isTextMember (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Element "Yield" m
        || isMember DslElement "Yield" m
        || isMember Builder "text" m

    /// Whether `m` is a child of an element CE: its `Yield` or `YieldFrom`.
    let isChildYield (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Element "Yield" m
        || isMember Element "YieldFrom" m
        || isMember DslElement "Yield" m
        || isMember DslElement "YieldFrom" m

    /// Whether `m` is the sugar's or DSL's `renderOf`, whose props are fixed when the template is built.
    let isRenderOf (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Sugar "renderOf" m
        || isMember Dsl "renderOf" m

    /// Whether `m` defines a component from a setup function: `Component.define` or `Component.template`.
    let isSetupDefinition (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Component "define" m
        || isMember SugarComponent "template" m
        || isMember DslComponent "template" m

    /// Whether `m` inlines a child template with a props reader: `render` on the DSL, sugar or builder.
    let isRender (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Sugar "render" m
        || isMember Dsl "render" m
        || isMember Builder "render" m

    /// Whether `m` renders a keyed list: the builder's `each`, or the DSL/sugar's `each`, `forEach` and `forEachFixed`.
    let isKeyed (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Builder "each" m
        || isMember Sugar "each" m
        || isMember Sugar "forEach" m
        || isMember Sugar "forEachFixed" m
        || isMember Dsl "each" m
        || isMember Dsl "forEach" m
        || isMember Dsl "forEachFixed" m

    /// Whether `m` is the element CE's `Delay`, or the body argument of its `For`: code that runs when the template is
    /// built, with the CE.
    let isElementBuildLambda (m: FSharpMemberOrFunctionOrValue) (index: int) : bool =
        isMember Element "Delay" m
        || isMember DslElement "Delay" m
        || (isMember Element "For" m && index = 1)
        || (isMember DslElement "For" m && index = 1)

    /// Whether argument `index` of the element CE's member `m` is CE structure built once: `Run`, `Combine`, or the
    /// sequence of `For`.
    let isElementStructure (m: FSharpMemberOrFunctionOrValue) (index: int) : bool =
        isMember Element "Run" m
        || isMember Element "Combine" m
        || isMember DslElement "Run" m
        || isMember DslElement "Combine" m
        || (isMember Element "For" m && index = 0)
        || (isMember DslElement "For" m && index = 0)

    /// Whether `m` constructs a component tag: a type whose base types include `ComponentTag`.
    let isComponentTagConstructor (m: FSharpMemberOrFunctionOrValue) : bool =
        let rec inherits (t: FSharpType option) (depth: int) =
            match t with
            | Some t when depth < 8 && t.HasTypeDefinition ->
                let fullName = t.TypeDefinition.TryFullName

                fullName = Some ComponentTag
                || fullName = Some DslComponentTag
                || inherits t.TypeDefinition.BaseType (depth + 1)
            | _ -> false

        m.IsConstructor
        && (match m.ApparentEnclosingEntity with
            | Some entity -> inherits entity.BaseType 0
            | None -> false)

    /// Whether `m` is `forEach`, whose rows follow the current item.
    let isForEach (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Sugar "forEach" m
        || isMember Dsl "forEach" m

    /// Whether `entity` declares attribute part builders (`Attrs` or DSL tag classes / scopes).
    let isAttributePartEntity (entity: string) : bool =
        entity = Attributes
        || entity = DslTagBase
        || entity = DslTagScope
        || entity = DslHtmlElement
        || entity.StartsWith (DslElements + ".", StringComparison.Ordinal)
        || (entity.StartsWith (HtmlTemplateGroupsPrefix, StringComparison.Ordinal)
            && entity.Contains ("Attributes", StringComparison.Ordinal))

    /// Whether `entity` declares property part builders (`Props` or DSL `.props` scopes).
    let isPropertyPartEntity (entity: string) : bool =
        entity = Properties
        || entity.StartsWith (DslPropsPrefix, StringComparison.Ordinal)
        || (entity.StartsWith (HtmlTemplateGroupsPrefix, StringComparison.Ordinal)
            && entity.Contains ("Properties", StringComparison.Ordinal))

    /// Whether `m` is `Part.batch` or `<tag>.batch`.
    let isBatchPartCall (m: FSharpMemberOrFunctionOrValue) : bool =
        m.LogicalName = "batch"
        && (let entity = enclosing m
            entity = DslPart || entity = DslTagBase)

/// The calls that make a list key unstable.
module Keys =
    /// Whether a call of `m` in a key function gives a new key on every pass.
    let impureCall (m: FSharpMemberOrFunctionOrValue) =
        match enclosing m, m.LogicalName with
        | "System.Guid", "NewGuid"
        | "System.DateTime", ("get_Now" | "get_UtcNow")
        | "System.Environment", ("get_TickCount" | "get_TickCount64")
        | "System.Object", ".ctor"
        | "Microsoft.FSharp.Core.FSharpRef`1", ("set_Value" | "set_contents")
        | Operators, (":=" | "op_ColonEquals" | "incr" | "decr") -> true
        | ("System.Random" | "System.Diagnostics.Stopwatch"), _ -> true
        | _ -> false

    /// Whether a call of `m` in a key function computes the item's position.
    let positionalCall (m: FSharpMemberOrFunctionOrValue) =
        match enclosing m, m.LogicalName with
        | ("Microsoft.FSharp.Collections.ArrayModule" | "Microsoft.FSharp.Collections.ListModule" | "Microsoft.FSharp.Collections.SeqModule"),
          ("findIndex" | "tryFindIndex")
        | ("System.Array" | "System.Collections.Generic.List`1" | "System.Collections.Generic.IList`1"), "IndexOf" -> true
        | _ -> false

    /// Whether `m` is FSharp.Core's `id`.
    let isIdentity (m: FSharpMemberOrFunctionOrValue) =
        isMember Operators "id" m

    /// Whether `m` overrides `Object.Equals` or implements an equality interface's `Equals`.
    let isEqualsOverride (m: FSharpMemberOrFunctionOrValue) =
        m.LogicalName = "Equals"
        && m.IsOverrideOrExplicitInterfaceImplementation
