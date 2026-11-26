/// RAN010-RAN015: core graph traps.
module Ranvier.Analyzers.Rules.Core

open System.Collections.Generic
open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text
open Ranvier.Analyzers
open Ranvier.Analyzers.Index
open Ranvier.Analyzers.Rule
open Ranvier.Analyzers.Symbols.Core

/// Where the node is created and what the failure is, then the fix, for a pure context of `kind`.
let private failure (kind: Pure) (context: string) (owning: string option) : string * string =
    match kind, owning with
    | Pure.Memo, Some "owning" ->
        $"`{context}`'s body, which fails every read of the memo",
        $"Add `owning` to `{context}` for a memo that owns the nodes its body creates, or create the node outside the memo."
    | Pure.Memo, Some owner ->
        $"`{context}`'s body, which fails every read of the memo",
        $"Use `{owner}` for a memo that owns the nodes its body creates, or create the node outside the memo."
    | Pure.Memo, None ->
        $"`{context}`'s body, which fails every read of the memo", "Pass `true` as the owning argument, or create the node outside the memo."
    | Pure.MemoRead, _ -> $"`{context}`'s read, which fails every read of the memo", "Create the node outside the memo."
    | Pure.Async, Some "owning" ->
        $"`{context}`'s compute, which fails every read of the async value",
        $"Add `owning` to `{context}` for an async value that owns the nodes its body creates up to the first await, or create the node outside the async value."
    | Pure.Async, Some owner ->
        $"`{context}`'s compute, which fails every read of the async value",
        $"Use `{owner}` for an async value that owns the nodes its body creates, or create the node outside the async value."
    | Pure.Async, None ->
        $"`{context}`'s compute, which fails every read of the async value",
        "Pass `true` as the owning argument, or create the node outside the async value."
    | Pure.AsyncRead, _ -> $"`{context}`'s read, which fails every read of the async value", "Create the node outside the async value."
    | Pure.EffectOn, _ -> $"`{context}`'s compute, which fails the effect", "Create the node in act, which owns it."
    | Pure.Map, _ ->
        "a projection's map, which fails the row's reads",
        "Use createProjectionWith, or createIndexProjectionWith, and create the node in the factory, which runs once per key."
    | Pure.Mapping, _ -> $"`{context}`'s mapping, which fails every read of the derived memo", "Create the node outside the mapping."
    | Pure.Stream, _ -> $"`{context}`'s body, which fails the stream", "Create the node outside the stream."
    | Pure.Timed, _ -> $"`{context}`'s reader, which fails the timed node", "Create the node outside the timed reader."
    | Pure.Reader, _ -> "a projection row's reader, which fails the row's reads", "Move the creation into the factory body, which runs once per key."
    | Pure.LookupF, _ -> "a lookup's f, which fails the key's reads", "Create the node outside the lookup."
    | Pure.LookupAffected, _ -> "a lookup's affected, which fails every live key", "Create the node outside the lookup."
    | Pure.Seed, _ -> $"`{context}`'s seed, which fails every read of the editable", "Create the node outside the editable."
    | Pure.Aggregate, _ -> "a projection aggregate's add, subtract or folder, which fails the aggregate", "Create the node outside the aggregate."

/// The range of `name` at the start of the call at `range`, when the call's text begins with a qualified `name`.
let private calleeName (input: Input) (range: range) (name: string) : range option =
    let callText = text input range
    let at = callText.IndexOf name

    if
        at >= 0
        && callText.Substring (0, at)
           |> Seq.forall (fun c ->
               System.Char.IsLetterOrDigit c
               || c = '.'
               || c = '_')
    then
        let start = Position.mkPos range.StartLine (range.StartColumn + at)
        Some (Range.mkRange range.FileName start (Position.mkPos range.StartLine (range.StartColumn + at + name.Length)))
    else
        None

/// Skips the `Delay` and `Run` steps wrapping the `For(header, _)` argument when exiting a CE header expression.
let rec private skipCeHeaderWrapper (kind: CeKind) (steps: Step list) : Step list =
    match steps with
    | Step.Lambda (_, false) :: Step.Argument (delay, 0, 1) :: Step.Argument (run, 0, 1) :: tail when
        ceBuilder delay = Some kind
        && delay.LogicalName = "Delay"
        && isCeRun run = Some kind
        ->
        tail
    | Step.Lambda (_, true) :: tail -> skipCeHeaderWrapper kind tail
    | _ -> steps

[<NoEquality; NoComparison>]
type private ScopeContext =
    | CeScope of callee: FSharpMemberOrFunctionOrValue * runPath: Step list * kind: CeKind * phase1: bool
    | PureFnScope of callee: FSharpMemberOrFunctionOrValue * purity: PureContext
    | OwnerFnScope of callee: FSharpMemberOrFunctionOrValue
    | NoScope

/// The innermost pure context, owner body, or `Ranvier.CE` block whose body directly holds `path`.
let private enclosingScope (path: Step list) : ScopeContext =
    let rec go (lambdas: int) (phase1: bool option) (steps: Step list) =
        match steps with
        | [] -> NoScope
        | Step.Lambda (_, true) :: rest -> go lambdas phase1 rest
        | Step.Lambda (_, false) :: Step.Argument (callee, _, _) :: rest when isTransparent callee -> go lambdas phase1 rest
        | Step.Lambda (_, false) :: Step.Argument (callee, index, count) :: rest when lambdas = 0 && isCeStepLambda callee index count ->
            let nextPhase1 =
                match phase1, ceBindKind callee, index, count with
                | None, Some CeBindKind.Reactive, 1, 2 -> Some false
                | _ -> phase1

            go 0 nextPhase1 rest
        | Step.Lambda (_, false) :: rest -> go (lambdas + 1) phase1 rest
        | Step.Argument (callee, 0, 2) :: rest when isCeHeaderFor callee ->
            if lambdas = 0 then
                let outer =
                    match ceBuilder callee with
                    | Some kind -> skipCeHeaderWrapper kind rest
                    | None -> rest

                go 0 None outer
            else
                NoScope
        | Step.Argument (callee, 0, 1) :: rest ->
            match isCeRun callee with
            | Some kind ->
                if lambdas = 0 then
                    CeScope (callee, rest, kind, defaultArg phase1 false)
                else
                    NoScope
            | None -> checkOther callee 0 1 lambdas phase1 rest
        | Step.Argument (callee, index, count) :: rest -> checkOther callee index count lambdas phase1 rest
        | _ :: rest -> go lambdas phase1 rest

    and checkOther callee index count lambdas phase1 rest =
        if (ceBuilder callee).IsSome then
            if lambdas > 0 then
                NoScope
            else
                let nextPhase1 =
                    match phase1, ceBindKind callee, index, count with
                    | None, Some CeBindKind.Reactive, 0, 2 -> Some true
                    | _ -> phase1

                go 0 nextPhase1 rest
        else
            match pureContext callee index count with
            | Some purity ->
                if purity.Layers = lambdas then
                    PureFnScope (callee, purity)
                else
                    NoScope
            | None ->
                match ownerBody callee index count with
                | Some layers -> if layers = lambdas then OwnerFnScope callee else NoScope
                | None when lambdas = 0 -> go 0 phase1 rest
                | None -> NoScope

    go 0 None path

let private cePureContext (callee: FSharpMemberOrFunctionOrValue) (kind: CeKind) (phase1: bool) : PureContext option =
    let ctx pureKind owning =
        Some
            {
                Kind = pureKind
                Layers = 0
                Owning = owning
            }

    match kind, phase1 with
    | CeKind.Memo, true -> ctx Pure.MemoRead None
    | CeKind.Memo, false ->
        match memoRunMode callee with
        | MemoRunMode.Owning
        | MemoRunMode.Tail -> None
        | MemoRunMode.Option -> ctx Pure.MemoRead None
        | MemoRunMode.Pure -> ctx Pure.Memo (Some "owning")
    | CeKind.Effect, true -> ctx Pure.EffectOn None
    | CeKind.Effect, false -> None
    | CeKind.AsyncMemo, true -> ctx Pure.AsyncRead None
    | CeKind.AsyncMemo, false ->
        match asyncRunMode callee with
        | AsyncRunMode.Owning -> None
        | AsyncRunMode.Pure -> ctx Pure.Async (Some "owning")
    | CeKind.Stream, _ -> ctx Pure.Stream None
    | (CeKind.Editable | CeKind.Draft), _ -> ctx Pure.Seed None
    | CeKind.Boundary, _ -> None
    | CeKind.Row, _ -> ctx Pure.Reader None

/// Scans forward from `startLine, startColumn` across `input.Source` to find the opening `{` of a CE block.
let private findCeOpenBrace (input: Input) (startLine: int) (startColumn: int) : (int * int) option =
    let lineCount = input.Source.GetLineCount ()

    let rec scanLine line col =
        if line > lineCount then
            None
        else
            let textLine = input.Source.GetLineString (line - 1)
            let mutable i = col

            while i < textLine.Length
                  && (textLine[i] = ' ' || textLine[i] = '\t') do
                i <- i + 1

            if i >= textLine.Length then scanLine (line + 1) 0
            elif textLine[i] = '{' then Some (line, i)
            else None

    scanLine startLine startColumn

let private hasBangBind (text: string) : bool =
    let rec loop i =
        if i >= text.Length then
            false
        elif System.Char.IsLetter text[i] then
            let mutable j = i

            while j < text.Length
                  && (System.Char.IsLetterOrDigit text[j]
                      || text[j] = '_') do
                j <- j + 1

            let word = text.Substring (i, j - i)

            if
                j < text.Length
                && text[j] = '!'
                && (word = "let"
                    || word = "use"
                    || word = "do"
                    || word = "match")
            then
                true
            else
                loop j
        else
            loop (i + 1)

    loop 0

/// Builds an insertion fix adding `owning` to the header of the CE `.Run` call at `runSite`, when unambiguous.
let private owningHeaderFix (input: Input) (runSite: CallSite) : Fix list =
    match findCeOpenBrace input runSite.Range.EndLine runSite.Range.EndColumn with
    | None -> []
    | Some (braceLine, braceCol) ->
        let lineCount = input.Source.GetLineCount ()

        let rec findFirstToken line col =
            if line > lineCount then
                None
            else
                let textLine = input.Source.GetLineString (line - 1)
                let mutable i = col

                while i < textLine.Length
                      && (textLine[i] = ' ' || textLine[i] = '\t') do
                    i <- i + 1

                if i >= textLine.Length then
                    findFirstToken (line + 1) 0
                else
                    Some (line, i, textLine)

        match findFirstToken braceLine (braceCol + 1) with
        | None -> []
        | Some (firstLine, firstCol, firstLineText) ->
            let insertPos = Position.mkPos firstLine firstCol
            let zeroRange = Range.mkRange runSite.Range.FileName insertPos insertPos

            if firstLine > braceLine then
                let indent = firstLineText.Substring (0, firstCol)
                [ replace zeroRange "" ("owning\n" + indent) ]
            else
                let bodyEndLine =
                    match runSite.Arguments with
                    | Call (_, _, _, _, [ arg ]) :: _ -> arg.Range.EndLine
                    | arg :: _ -> arg.Range.EndLine
                    | [] -> firstLine

                if bodyEndLine > braceLine then
                    let indent = System.String (' ', firstCol)
                    [ replace zeroRange "" ("owning\n" + indent) ]
                elif not (hasBangBind (firstLineText.Substring firstCol)) then
                    [ replace zeroRange "" "owning; " ]
                else
                    []

let private innermostCallAround (calls: CallSite array) (callee: FSharpMemberOrFunctionOrValue) (innerRange: range) =
    calls
    |> Array.filter (fun candidate ->
        candidate.Callee = callee
        && Range.rangeContainsRange candidate.Range innerRange)
    |> Array.sortBy (fun candidate ->
        candidate.Range.EndLine
        - candidate.Range.StartLine,
        candidate.Range.EndColumn
        - candidate.Range.StartColumn)
    |> Array.tryHead

let private ownedInPure (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            if isCreator site.Callee then
                let purityOpt =
                    match enclosingScope site.Path with
                    | PureFnScope (callee, purity) -> Some (callee, purity, None)
                    | CeScope (callee, runPath, kind, phase1) ->
                        cePureContext callee kind phase1
                        |> Option.map (fun purity -> callee, purity, Some runPath)
                    | OwnerFnScope _
                    | NoScope -> None

                match purityOpt with
                | Some (callee, purity, ceRunPath) ->
                    let contextName = Symbols.display callee
                    let where, fix = failure purity.Kind contextName purity.Owning

                    let fixes =
                        match purity.Owning, ceRunPath with
                        | Some "owning", Some runPath ->
                            let runSiteOpt =
                                input.Index.Calls
                                |> Array.tryFind (fun candidate ->
                                    candidate.Callee = callee
                                    && obj.ReferenceEquals (candidate.Path, runPath))

                            match runSiteOpt with
                            | Some runSite -> owningHeaderFix input runSite
                            | None -> []
                        | Some owner, None ->
                            match
                                innermostCallAround input.Index.Calls callee site.Range
                                |> Option.bind (fun s -> calleeName input s.Range callee.LogicalName)
                            with
                            | Some nameRange -> [ replace nameRange callee.LogicalName owner ]
                            | None -> []
                        | _ -> []

                    message "RAN010" Severity.Error site.Range $"`{Symbols.display site.Callee}` creates an owned node inside {where}. {fix}" fixes
                | None -> ()
    ]

let ran010: Rule =
    {
        Code = "RAN010"
        Title = "Owned node created in a pure derivation"
        Gate = Symbols.Assemblies.Ranvier
        Run = ownedInPure
    }

/// Whether the computation `expr` awaits: a suspending bind on its own statements, or in the lambdas of its control
/// members. Stops before nested computation-expression `.Run` calls.
let rec private containsAwait (expr: FSharpExpr) : bool =
    match expr with
    | Call (_, m, _, _, _) when isAwaitBind m -> true
    | Call (_, m, _, _, arguments) when isControl m && (isCeRun m).IsNone -> List.exists containsAwait arguments
    | Lambda (_, body)
    | Let (_, body)
    | Sequential (_, body)
    | Coerce (_, body) -> containsAwait body
    | IfThenElse (_, whenTrue, whenFalse) -> containsAwait whenTrue || containsAwait whenFalse
    | DecisionTree (_, targets) -> targets |> List.exists (snd >> containsAwait)
    | TryWith (body, _, _, _, handler, _, _) -> containsAwait body || containsAwait handler
    | TryFinally (body, cleanup, _, _) -> containsAwait body || containsAwait cleanup
    | _ -> false

/// Whether argument `index` of `callee` is a tracked body: a pure context, an owner body, or `untrack`'s body.
let private trackedBody (callee: FSharpMemberOrFunctionOrValue) (index: int) (count: int) : bool =
    (pureContext callee index count).IsSome
    || (ownerBody callee index count).IsSome
    || isUntrack callee

/// Whether `path` runs after an await inside the compute of an async value's creator: in a suspending bind's
/// continuation, or after a `Combine` or loop whose body awaits. A read inside a nested tracked computation-expression
/// node, tracked body, or `untrack` is outside the async value's tracking.
let private afterAwait (input: Input) (path: Step list) : bool =
    // The `Combine` call at `outer`, the path of the call itself, holds the read in its second argument.
    let awaitsBefore (combine: FSharpMemberOrFunctionOrValue) (outer: Step list) =
        input.Index.Calls
        |> Array.exists (fun call ->
            call.Callee = combine
            && obj.ReferenceEquals (call.Path, outer)
            && call.Arguments.Length = 2
            && containsAwait call.Arguments[0])

    // The loop call at `outer` holds the read, and an await in the loop precedes the read's next run.
    let loopAwaits (loop: FSharpMemberOrFunctionOrValue) (outer: Step list) =
        input.Index.Calls
        |> Array.exists (fun call ->
            call.Callee = loop
            && obj.ReferenceEquals (call.Path, outer)
            && List.exists containsAwait call.Arguments)

    let rec go (awaited: bool) (steps: Step list) =
        match steps with
        | [] -> false
        | Step.Lambda (_, false) :: (Step.Argument (callee, index, count) :: _ as rest) when isAwaitBind callee && index = count - 1 -> go true rest
        | Step.Argument (callee, 0, 1) :: _ when
            (match isCeRun callee with
             | Some kind -> kind <> CeKind.AsyncMemo
             | None -> false)
            ->
            false
        | Step.Lambda (_, false) :: Step.Argument (callee, index, count) :: _ when
            asyncComputeIndex callee count <> Some index
            && trackedBody callee index count
            ->
            false
        | Step.Argument (callee, 1, 2) :: rest when isCombine callee -> go (awaited || awaitsBefore callee rest) rest
        | Step.Argument (callee, _, _) :: rest when isLoop callee -> go (awaited || loopAwaits callee rest) rest
        | Step.Argument (callee, index, count) :: rest ->
            match asyncComputeIndex callee count with
            | Some compute when compute = index -> awaited
            | _ -> go awaited rest
        | _ :: rest -> go awaited rest

    go false path

let rec private stripImmediateLambdas (path: Step list) : Step list =
    match path with
    | Step.Lambda (_, true) :: rest -> stripImmediateLambdas rest
    | _ -> path

let rec private referencesBoundSource (target: FSharpMemberOrFunctionOrValue) (expr: FSharpExpr) : bool =
    match expr with
    | Value value -> value = target
    | Coerce (_, inner)
    | Let (_, inner) -> referencesBoundSource target inner
    | Call (_, pipe, _, _, [ arg; _ ]) when Symbols.isPipe Symbols.PipeRight pipe -> referencesBoundSource target arg
    | Call (_, pipe, _, _, [ _; arg ]) when Symbols.isPipe Symbols.PipeLeft pipe -> referencesBoundSource target arg
    | Call (_, m, _, _, args) when isMergeSources m || isWithComparer m -> List.exists (referencesBoundSource target) args
    | NewObject (ctor, _, args) when Symbols.enclosing ctor = "Ranvier.Internal.ComparedReactive`1" -> List.exists (referencesBoundSource target) args
    | _ -> false

/// Whether `inner` (the relative path from an inner read or reactive `let!` back to the `let` binding) remains within
/// the same owner body run without entering a nested computation expression or non-transparent user lambda.
let rec private sameOwnerRun (inner: Step list) : bool =
    match inner with
    | [] -> true
    | Step.Lambda (_, true) :: rest -> sameOwnerRun rest
    | Step.Lambda (_, false) :: Step.Argument (callee, _, _) :: rest when isTransparent callee -> sameOwnerRun rest
    | Step.Lambda (_, false) :: Step.Argument (callee, index, count) :: rest when isCeStepLambda callee index count -> sameOwnerRun rest
    | Step.Lambda (_, false) :: _ -> false
    | Step.Argument (callee, 0, 1) :: _ when (isCeRun callee).IsSome -> false
    | _ :: rest -> sameOwnerRun rest

let private recreated (input: Input) : Message list =
    // The paths of the `let`s whose bound expression creates an async value, keyed by the bound value.
    let asyncLets = Dictionary<FSharpMemberOrFunctionOrValue, ResizeArray<Step list>>()

    for call in input.Index.Calls do
        if isAsyncCreator call.Callee then
            match stripImmediateLambdas call.Path with
            | Step.Bound value :: rest ->
                let list =
                    match asyncLets.TryGetValue value with
                    | true, existing -> existing
                    | false, _ ->
                        let created = ResizeArray<Step list>()
                        asyncLets[value] <- created
                        created

                list.Add rest
            | _ -> ()

    let isCreatedAsync (site: LetSite) =
        match asyncLets.TryGetValue site.Value with
        | true, paths ->
            paths
            |> Seq.exists (fun rest -> obj.ReferenceEquals (rest, site.Path))
        | false, _ -> false

    let enclosingOwner (site: LetSite) : (FSharpMemberOrFunctionOrValue * bool) option =
        match enclosingScope site.Path with
        | OwnerFnScope callee ->
            let isAsyncOwner =
                (asyncComputeIndex
                    callee
                    (site.Path
                     |> List.pick (function
                         | Step.Argument (m, _, c) when m = callee -> Some c
                         | _ -> None)))
                    .IsSome

            if isAsyncOwner && afterAwait input site.Path then
                None
            else
                Some (callee, isAsyncOwner)
        | CeScope (callee, _, kind, phase1) ->
            match kind, phase1 with
            | CeKind.Memo, false when
                (match memoRunMode callee with
                 | MemoRunMode.Owning
                 | MemoRunMode.Tail -> true
                 | _ -> false)
                ->
                Some (callee, false)
            | CeKind.Effect, false -> Some (callee, false)
            | CeKind.Boundary, _ -> Some (callee, false)
            | CeKind.AsyncMemo, false when
                asyncRunMode callee = AsyncRunMode.Owning
                && not (afterAwait input site.Path)
                ->
                Some (callee, true)
            | _ -> None
        | PureFnScope _
        | NoScope -> None

    let candidates =
        [
            for site in input.Index.Lets do
                if isCreatedAsync site then
                    match enclosingOwner site with
                    | Some (callee, isAsyncOwner) -> site, callee, isAsyncOwner
                    | None -> ()
        ]

    if candidates.IsEmpty then
        []
    else
        let readCalls =
            input.Index.Calls
            |> Array.filter (fun call ->
                isReactiveRead call.Callee
                || ceBindKind call.Callee = Some CeBindKind.Reactive)

        [
            for site, callee, isAsyncOwner in candidates do
                let readInBody =
                    readCalls
                    |> Array.exists (fun call ->
                        let targetsSite =
                            if isReactiveRead call.Callee then
                                match call.Receiver with
                                | Some (Value value) -> value = site.Value
                                | _ -> false
                            else
                                match call.Arguments with
                                | src :: _ -> referencesBoundSource site.Value src
                                | [] -> false

                        targetsSite
                        && (match Index.within site.Path call.Path with
                            | Some inner ->
                                sameOwnerRun inner
                                && not (isAsyncOwner && afterAwait input call.Path)
                            | None -> false))

                if readInBody then
                    let name = site.Value.DisplayName
                    let ownerName = Symbols.display callee

                    message
                        "RAN011"
                        Severity.Warning
                        site.Value.DeclarationLocation
                        $"`{name}` is created inside `{ownerName}`'s body and read there; each run of the body disposes `{name}` and starts a new flight. Create `{name}` outside `{ownerName}` and read it inside."
                        []
        ]

let ran011: Rule =
    {
        Code = "RAN011"
        Title = "Async value created and read in the owner that replaces it"
        Gate = Symbols.Assemblies.Ranvier
        Run = recreated
    }

let private untrackedReads (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            match site.Receiver with
            | Some receiver when
                isReactiveRead site.Callee
                && afterAwait input site.Path
                ->
                let read = text input site.Range
                let source = text input receiver.Range

                message
                    "RAN012"
                    Severity.Warning
                    site.Range
                    $"`{read}` is read after the first `let!`/`do!` of this async memo; a read after an await that suspends is untracked, and a change to `{source}` starts no flight. Read it before the first await and bind it to a local."
                    []
            | _ -> ()
    ]

let ran012: Rule =
    {
        Code = "RAN012"
        Title = "Reactive read after the first await of an async memo"
        Gate = Symbols.Assemblies.Ranvier
        Run = untrackedReads
    }

/// Whether the expression at `path` runs in a delegate handed to a thread starter, with no `Graph.Dispatch` between.
let private offThread (path: Step list) : bool =
    let rec go (insideLambda: bool) (steps: Step list) =
        match steps with
        | [] -> false
        | Step.Lambda (_, false) :: rest -> go true rest
        | Step.Argument (callee, _, _) :: _ when isDispatch callee -> false
        | Step.Argument (callee, _, _) :: _ when insideLambda && threadStarter callee -> true
        | _ :: rest -> go insideLambda rest

    go false path

/// The value a graph write targets: the signal or graph receiver, or `Signal.update`'s signal.
let private writeTarget (site: CallSite) : FSharpMemberOrFunctionOrValue option =
    match site.Receiver, site.Arguments with
    | Some (Value target), _ -> Some target
    | None, Value target :: _ -> Some target
    | _ -> None

let private offThreadWrites (input: Input) : Message list =
    let starters =
        input.Index.Calls
        |> Array.filter (fun call -> threadStarter call.Callee)

    let within (outer: range) (inner: range) =
        Range.rangeContainsRange outer inner

    let innermostStarter (site: CallSite) =
        starters
        |> Array.filter (fun starter -> within starter.Range site.Range)
        |> Array.sortBy (fun starter ->
            starter.Range.EndLine - starter.Range.StartLine,
            starter.Range.EndColumn
            - starter.Range.StartColumn)
        |> Array.tryHead

    // A graph that the innermost starter's delegate constructs, a node it constructs over that graph, and a write in
    // that graph's `Run`, belong to the delegate's thread.
    let ownedByThread (site: CallSite) =
        match innermostStarter site, writeTarget site with
        | Some starter, Some target ->
            let lets =
                input.Index.Lets
                |> Array.filter (fun binding -> within starter.Range binding.Value.DeclarationLocation)

            let graphs =
                lets
                |> Array.choose (fun binding ->
                    match binding.Bound with
                    | NewObject (constructor, _, _) when isGraphConstructor constructor -> Some binding.Value
                    | _ -> None)

            let overGraph (expr: FSharpExpr) =
                match expr with
                | NewObject (constructor, _, Value graph :: _) when isNodeConstructor constructor -> graphs |> Array.contains graph
                | Call (Some (Value graph), m, _, _, _)
                | Call (None, m, _, _, Value graph :: _) when isGraphRun m -> graphs |> Array.contains graph
                | _ -> false

            let insideRun =
                input.Index.Calls
                |> Array.exists (fun call ->
                    isGraphRun call.Callee
                    && within call.Range site.Range
                    && (match call.Receiver, call.Arguments with
                        | Some (Value graph), _
                        | None, Value graph :: _ -> graphs |> Array.contains graph
                        | _ -> false))

            graphs |> Array.contains target
            || insideRun
            || lets
               |> Array.exists (fun binding -> binding.Value = target && overGraph binding.Bound)
        | _ -> false

    [
        for site in input.Index.Calls do
            if
                isGraphWrite site.Callee
                && offThread site.Path
                && not (ownedByThread site)
            then
                message
                    "RAN013"
                    Severity.Warning
                    site.Range
                    $"`{text input site.Range}` runs on a thread-pool thread; a write off the graph's owning thread raises under `ThreadAffinity.Guarded`. Hand the write to `graph.Dispatch (fun () -> ...)`."
                    []
    ]

let ran013: Rule =
    {
        Code = "RAN013"
        Title = "Signal write or Pump on a thread-pool callback"
        Gate = Symbols.Assemblies.Ranvier
        Run = offThreadWrites
    }

let private cancelPreviousMessage =
    "This async memo's body ignores its CancellationToken, so `CancelPrevious` cancels nothing and acts as `KeepLatest`. Pass the token to the awaited work, or use `KeepLatest`."

let rec private cancelPreviousHeaderRange (expr: FSharpExpr) : range option =
    match expr with
    | Call (_, m, _, _, [ _; NewUnionCase (_, case, []) ]) when isCePolicyCall m && isCancelPrevious case -> Some expr.Range
    | Call (_, _, _, _, args) -> List.tryPick cancelPreviousHeaderRange args
    | _ -> None

let rec private referencesValue (target: FSharpMemberOrFunctionOrValue) (expr: FSharpExpr) : bool =
    match expr with
    | Value value -> value = target
    | Coerce (_, inner) -> referencesValue target inner
    | _ -> false

/// Collects `(lamParam, continuationBody)` pairs for `let! ... = asyncMemo.cancellationToken` binds inside `expr`,
/// stopping before nested `AsyncMemoBuilder.Run` blocks.
let private cancellationBindsIn (expr: FSharpExpr) : (FSharpMemberOrFunctionOrValue * FSharpExpr) list =
    let results = ResizeArray<FSharpMemberOrFunctionOrValue * FSharpExpr>()

    let rec walk (e: FSharpExpr) =
        match e with
        | Call (_, m, _, _, _) when isCeRun m = Some CeKind.AsyncMemo -> ()
        | Call (receiver, m, _, _, ([ _; Lambda (lamParam, contBody) ] as args)) when isCeCancellationBind m ->
            results.Add (lamParam, contBody)
            Option.iter walk receiver
            List.iter walk args
        | _ ->
            for inner in e.ImmediateSubExpressions do
                walk inner

    walk expr
    List.ofSeq results

let private ignoredTokenBindingRange (lamParam: FSharpMemberOrFunctionOrValue) (contBody: FSharpExpr) : range option =
    match contBody with
    | Let ((boundVal, rhs, _), rest) when
        not boundVal.IsCompilerGenerated
        && referencesValue lamParam rhs
        ->
        if
            boundVal.DisplayName.StartsWith ("_", System.StringComparison.Ordinal)
            || (Index.uses boundVal rest).IsEmpty
        then
            Some boundVal.DeclarationLocation
        else
            None
    | _ ->
        if
            lamParam.DisplayName.StartsWith ("_", System.StringComparison.Ordinal)
            || (Index.uses lamParam contBody).IsEmpty
        then
            Some lamParam.DeclarationLocation
        else
            None

let private ignoredTokens (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            if isPolicyCreator site.Callee then
                match site.Arguments with
                | [ NewUnionCase (_, case, []); Lambda (_, Lambda (token, body)) ]
                | [ NewUnionCase (_, case, []); _; Lambda (_, Lambda (_, Lambda (token, body))) ] when
                    isCancelPrevious case
                    && (token.DisplayName.StartsWith ("_", System.StringComparison.Ordinal)
                        || (Index.uses token body).IsEmpty)
                    ->
                    message "RAN015" Severity.Hint token.DeclarationLocation cancelPreviousMessage []
                | _ -> ()
            elif
                ceBuilder site.Callee = Some CeKind.AsyncMemo
                && isCeHeaderFor site.Callee
            then
                match site.Arguments with
                | [ headerExpr; bodyExpr ] ->
                    match cancelPreviousHeaderRange headerExpr with
                    | Some policyRange ->
                        match cancellationBindsIn bodyExpr with
                        | [] -> message "RAN015" Severity.Hint policyRange cancelPreviousMessage []
                        | binds ->
                            for lamParam, contBody in binds do
                                match ignoredTokenBindingRange lamParam contBody with
                                | Some tokenRange -> message "RAN015" Severity.Hint tokenRange cancelPreviousMessage []
                                | None -> ()
                    | None -> ()
                | _ -> ()
    ]

let ran015: Rule =
    {
        Code = "RAN015"
        Title = "CancelPrevious with a body that ignores its token"
        Gate = Symbols.Assemblies.Ranvier
        Run = ignoredTokens
    }
