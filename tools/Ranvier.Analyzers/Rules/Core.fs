/// RAN010-RAN018: core graph and computation-expression rules.
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

                if
                    i >= textLine.Length
                    || (i + 1 < textLine.Length
                        && textLine[i] = '/'
                        && textLine[i + 1] = '/')
                then
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
            | CeKind.Boundary, false -> Some (callee, false)
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

let private isBatchOrTaskDelay (m: FSharpMemberOrFunctionOrValue) : bool =
    isTransparent m && not (isUntrack m)

/// Whether `path` executes inside an untracked closed Phase-2 body (split `*On`, static `map`/`map2`/`bind*`, or the
/// continuation after a reactive CE `let!`) without entering a nested tracked body, `untrack`, or user closure.
let private inUntrackedComputePhase (input: Input) (path: Step list) : bool =
    let rec go (lambdas: int) (inReactiveCeCont: bool) (steps: Step list) : bool =
        match steps with
        | [] -> false
        | Step.Lambda (_, true) :: rest -> go lambdas inReactiveCeCont rest
        | Step.Lambda (_, false) :: Step.Argument (callee, _, _) :: _ when isUntrack callee -> false
        | Step.Lambda (_, false) :: Step.Argument (callee, _, _) :: rest when isBatchOrTaskDelay callee -> go lambdas inReactiveCeCont rest
        | Step.Lambda (_, false) :: Step.Argument (callee, index, count) :: rest when lambdas = 0 && isCeStepLambda callee index count ->
            let enteredReactive =
                index = 1
                && count = 2
                && ceBindKind callee = Some CeBindKind.Reactive

            go 0 (inReactiveCeCont || enteredReactive) rest
        | Step.Lambda (_, false) :: rest -> go (lambdas + 1) inReactiveCeCont rest
        | Step.Argument (callee, 0, 2) :: rest when isCeHeaderFor callee ->
            if lambdas = 0 then
                let outer =
                    match ceBuilder callee with
                    | Some kind -> skipCeHeaderWrapper kind rest
                    | None -> rest

                go 0 inReactiveCeCont outer
            else
                false
        | Step.Argument (callee, 0, 1) :: _ when (isCeRun callee).IsSome -> lambdas = 0 && inReactiveCeCont
        | Step.Argument (callee, index, count) :: rest ->
            if (ceBuilder callee).IsSome then
                if lambdas = 0 then go 0 inReactiveCeCont rest else false
            else
                match untrackedPhaseLayers callee index with
                | Some layers -> lambdas = layers
                | None ->
                    if trackedBody callee index count then false
                    elif lambdas = 0 then go 0 inReactiveCeCont rest
                    else false
        | _ :: rest -> go lambdas inReactiveCeCont rest

    go 0 false path && not (afterAwait input path)

let private untrackedPhaseReads (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            match site.Receiver with
            | Some receiver when
                isReactiveRead site.Callee
                && inUntrackedComputePhase input site.Path
                ->
                let read = text input site.Range
                let source = text input receiver.Range

                message
                    "RAN016"
                    Severity.Warning
                    site.Range
                    $"`{read}` runs inside an untracked compute phase, so changes to `{source}` never wake this computation and a pending read fails it. Bind `{source}` with `let!`/`and!` in the tracked phase, or use `{source}.Peek` / `untrack` for an intentional snapshot."
                    []
            | _ -> ()
    ]

let ran016: Rule =
    {
        Code = "RAN016"
        Title = "Reactive read inside an untracked compute phase"
        Gate = Symbols.Assemblies.Ranvier
        Run = untrackedPhaseReads
    }

let private isRan017Builder (kind: CeKind) : bool =
    match kind with
    | CeKind.Memo
    | CeKind.Effect
    | CeKind.AsyncMemo
    | CeKind.Stream
    | CeKind.Editable
    | CeKind.Draft
    | CeKind.Row -> true
    | CeKind.Boundary -> false

let rec private leftmostMergeSource (expr: FSharpExpr) : FSharpExpr =
    match expr with
    | Coerce (_, inner) -> leftmostMergeSource inner
    | Call (_, m, _, _, [ left; _ ]) when isMergeSources m -> leftmostMergeSource left
    | _ -> expr

let rec private exprEndPos (expr: FSharpExpr) : pos =
    let mutable best = expr.Range.End

    for sub in expr.ImmediateSubExpressions do
        let p = exprEndPos sub

        if Position.posGt p best then
            best <- p

    best

let rec private isPatternProjection (locals: FSharpMemberOrFunctionOrValue list) (expr: FSharpExpr) : bool =
    match expr with
    | Value value -> locals |> List.contains value
    | AddressOf inner
    | Coerce (_, inner)
    | TupleGet (_, _, inner)
    | FSharpFieldGet (Some inner, _, _)
    | UnionCaseGet (inner, _, _, _) -> isPatternProjection locals inner
    | _ -> false

/// Strips compiler-generated irrefutable pattern-destructuring `Let` bindings bound before `s1End` from `paramA`.
let private stripPatternBinds
    (s1End: pos)
    (paramA: FSharpMemberOrFunctionOrValue)
    (body: FSharpExpr)
    : FSharpExpr * FSharpMemberOrFunctionOrValue list * FSharpMemberOrFunctionOrValue list =
    let rec go (allLocals: FSharpMemberOrFunctionOrValue list) (namedVars: FSharpMemberOrFunctionOrValue list) (expr: FSharpExpr) =
        match expr with
        | Let ((value, rhs, _), rest) when
            (value.IsCompilerGenerated
             || Position.posLt value.DeclarationLocation.Start s1End)
            && isPatternProjection allLocals rhs
            ->
            let nextNamed =
                if value.IsCompilerGenerated then
                    namedVars
                else
                    value :: namedVars

            go (value :: allLocals) nextNamed rest
        | _ -> expr, allLocals, namedVars

    go [ paramA ] [] body

let private codeLimitBeforeLineComment (lineText: string) (minCol: int) (maxCol: int) : int =
    let limit = min maxCol lineText.Length
    let mutable i = max 0 minCol
    let mutable inString = false
    let mutable blockDepth = 0
    let mutable stopAt = limit

    while i < limit && stopAt = limit do
        let c = lineText[i]

        if inString then
            if c = '\\' && i + 1 < limit then
                i <- i + 2
            elif c = '"' then
                inString <- false
                i <- i + 1
            else
                i <- i + 1
        elif blockDepth > 0 then
            if c = '(' && i + 1 < limit && lineText[i + 1] = '*' then
                blockDepth <- blockDepth + 1
                i <- i + 2
            elif c = '*' && i + 1 < limit && lineText[i + 1] = ')' then
                blockDepth <- blockDepth - 1
                i <- i + 2
            else
                i <- i + 1
        elif c = '"' then
            inString <- true
            i <- i + 1
        elif c = '(' && i + 1 < limit && lineText[i + 1] = '*' then
            blockDepth <- 1
            i <- i + 2
        elif c = '/' && i + 1 < limit && lineText[i + 1] = '/' then
            stopAt <- i
        else
            i <- i + 1

    stopAt

let private isIdentChar (c: char) : bool =
    System.Char.IsLetterOrDigit c
    || c = '_'
    || c = '\''
    || c = '!'

/// Finds the `let!` keyword range immediately preceding `targetPos` (the pattern of the inner bind), skipping comments
/// and whitespace back to `stopPos`. Rejects `match!`, `do!`, `use!`, or anything without a preceding `let!`.
let private letBangRangeBefore (input: Input) (fileName: string) (stopPos: pos) (targetPos: pos) : range option =
    let minLine = max 1 stopPos.Line

    let rec scanBack line col blockDepth =
        if line < minLine then
            None
        else
            let textLine = input.Source.GetLineString (line - 1)
            let mutable i = min col textLine.Length
            let mutable depth = blockDepth
            let mutable found: range option = None
            let mutable stopped = false

            while i > 0 && not stopped do
                if depth > 0 then
                    if
                        i >= 2
                        && textLine[i - 2] = '*'
                        && textLine[i - 1] = ')'
                    then
                        depth <- depth + 1
                        i <- i - 2
                    elif
                        i >= 2
                        && textLine[i - 2] = '('
                        && textLine[i - 1] = '*'
                    then
                        depth <- depth - 1
                        i <- i - 2
                    else
                        i <- i - 1
                else
                    let c = textLine[i - 1]

                    if c = ' ' || c = '\t' || c = '\r' then
                        i <- i - 1
                    elif
                        i >= 2
                        && textLine[i - 2] = '*'
                        && textLine[i - 1] = ')'
                    then
                        depth <- 1
                        i <- i - 2
                    elif
                        i >= 4
                        && textLine.Substring (i - 4, 4) = "let!"
                        && (i = 4 || not (isIdentChar textLine[i - 5]))
                    then
                        let startPos = Position.mkPos line (i - 4)
                        let endPos = Position.mkPos line i
                        found <- Some (Range.mkRange fileName startPos endPos)
                        stopped <- true
                    else
                        stopped <- true

            if found.IsSome then
                found
            elif not stopped && line - 1 >= minLine then
                let prevLine = line - 1
                let prevText = input.Source.GetLineString (prevLine - 1)

                let prevLimit =
                    if depth = 0 then
                        let minCol = if prevLine = stopPos.Line then stopPos.Column else 0
                        codeLimitBeforeLineComment prevText minCol prevText.Length
                    else
                        prevText.Length

                scanBack prevLine prevLimit depth
            else
                None

    scanBack targetPos.Line targetPos.Column 0

let private letRhsText (input: Input) (afterPattern: range) (expr: FSharpExpr) : string =
    match expr with
    | Let _ ->
        let afterPatternPos = afterPattern.End
        let endPos = exprEndPos expr
        let maxLine = min endPos.Line (input.Source.GetLineCount ())

        let rec findEq line col =
            if line > maxLine then
                None
            else
                let lineText = input.Source.GetLineString (line - 1)

                let limit =
                    if line = maxLine then
                        min endPos.Column lineText.Length
                    else
                        lineText.Length

                let mutable i = col
                let mutable found = -1

                while i < limit && found < 0 do
                    if lineText[i] = '=' then found <- i else i <- i + 1

                if found >= 0 then
                    Some (line, found + 1)
                else
                    findEq (line + 1) 0

        let rec findStart line col =
            if line > maxLine then
                None
            else
                let lineText = input.Source.GetLineString (line - 1)

                let limit =
                    if line = maxLine then
                        min endPos.Column lineText.Length
                    else
                        lineText.Length

                let mutable i = col

                while i < limit
                      && (lineText[i] = ' ' || lineText[i] = '\t') do
                    i <- i + 1

                if i < limit then
                    Some (Position.mkPos line i)
                else
                    findStart (line + 1) 0

        match
            findEq afterPatternPos.Line afterPatternPos.Column
            |> Option.bind (fun (line, col) -> findStart line col)
        with
        | Some startPos when Position.posLt startPos endPos -> text input (Range.mkRange afterPattern.FileName startPos endPos)
        | _ -> text input expr.Range
    | _ -> text input expr.Range

let private independentSequentialBinds (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            if
                site.Callee.LogicalName = "Bind"
                && ceBindKind site.Callee = Some CeBindKind.Reactive
                && (match ceBuilder site.Callee with
                    | Some kind -> isRan017Builder kind
                    | None -> false)
            then
                match site.Arguments with
                | [ s1; Lambda (paramA, bodyA) ] ->
                    let s1End = exprEndPos s1
                    let stripped, patternLocals, namedVars = stripPatternBinds s1End paramA bodyA

                    match stripped with
                    | Call (_, innerCallee, _, _, [ s2; Lambda (paramB, _) ]) when
                        ceBuilder innerCallee = ceBuilder site.Callee
                        && (innerCallee.LogicalName = "Bind"
                            || innerCallee.LogicalName = "BindReturn")
                        && ceBindKind innerCallee = Some CeBindKind.Reactive
                        && (patternLocals
                            |> List.forall (fun local -> (Index.uses local s2).IsEmpty))
                        ->
                        match letBangRangeBefore input paramB.DeclarationLocation.FileName s1End paramB.DeclarationLocation.Start with
                        | Some letBangRange ->
                            let sourceExpr = leftmostMergeSource s2
                            let s2Text = letRhsText input paramB.DeclarationLocation sourceExpr

                            let paramDisplay =
                                match namedVars with
                                | [] -> "_"
                                | [ single ] -> single.DisplayName
                                | many ->
                                    many
                                    |> List.sortBy (fun v -> v.DeclarationLocation.StartLine, v.DeclarationLocation.StartColumn)
                                    |> List.map _.DisplayName
                                    |> String.concat ", "

                            message
                                "RAN017"
                                Severity.Hint
                                letBangRange
                                $"`{s2Text}` does not depend on `{paramDisplay}`; sequential `let!` forces dynamic tracking and skips static `map2`/`*On` key cutoff. Write `and!` to bind both sources statically."
                                [ replace letBangRange "let!" "and!" ]
                        | None -> ()
                    | _ -> ()
                | _ -> ()
    ]

let ran017: Rule =
    {
        Code = "RAN017"
        Title = "Independent sequential let! in a reactive computation expression"
        Gate = Symbols.Assemblies.Ranvier
        Run = independentSequentialBinds
    }

let private isRan018Builder (kind: CeKind) : bool =
    match kind with
    | CeKind.Memo
    | CeKind.Editable
    | CeKind.Draft
    | CeKind.AsyncMemo
    | CeKind.Stream
    | CeKind.Row -> true
    | CeKind.Effect
    | CeKind.Boundary -> false

/// The innermost enclosing `.Run` path when `steps` sits directly in a CE block (jumping across headers via `skipCeHeaderWrapper`).
let rec private enclosingBindRunPath (steps: Step list) : Step list option =
    match steps with
    | [] -> None
    | Step.Lambda (_, true) :: rest -> enclosingBindRunPath rest
    | Step.Argument (callee, 0, 2) :: rest when isCeHeaderFor callee ->
        let outer =
            match ceBuilder callee with
            | Some kind -> skipCeHeaderWrapper kind rest
            | None -> rest

        enclosingBindRunPath outer
    | Step.Argument (callee, 0, 1) :: rest when (isCeRun callee).IsSome -> Some rest
    | _ :: rest -> enclosingBindRunPath rest

/// The innermost enclosing `.Run` `(callee, kind, runPath)` when `path` evaluates directly inside that CE's body at
/// `lambdaCount = 0`, outside `untrack` and nested tracked/untracked callback bodies.
let private enclosingDirectCeRun (path: Step list) : (FSharpMemberOrFunctionOrValue * CeKind * Step list) option =
    let rec go (lambdas: int) (steps: Step list) =
        match steps with
        | [] -> None
        | Step.Lambda (_, true) :: rest -> go lambdas rest
        | Step.Lambda (_, false) :: Step.Argument (callee, _, _) :: _ when isUntrack callee -> None
        | Step.Lambda (_, false) :: Step.Argument (callee, _, _) :: rest when isBatchOrTaskDelay callee -> go lambdas rest
        | Step.Lambda (_, false) :: Step.Argument (callee, index, count) :: rest when lambdas = 0 && isCeStepLambda callee index count -> go 0 rest
        | Step.Lambda (_, false) :: _ -> None
        | Step.Argument (callee, 0, 2) :: rest when isCeHeaderFor callee ->
            if lambdas = 0 then
                let outer =
                    match ceBuilder callee with
                    | Some kind -> skipCeHeaderWrapper kind rest
                    | None -> rest

                go 0 outer
            else
                None
        | Step.Argument (callee, 0, 1) :: rest ->
            match isCeRun callee with
            | Some kind -> if lambdas = 0 then Some (callee, kind, rest) else None
            | None -> checkNonCe callee 0 1 lambdas rest
        | Step.Argument (callee, index, count) :: rest ->
            if (ceBuilder callee).IsSome then
                if lambdas = 0 then go 0 rest else None
            else
                checkNonCe callee index count lambdas rest
        | _ :: rest -> go lambdas rest

    and checkNonCe callee index count lambdas rest =
        if
            (untrackedPhaseLayers callee index).IsSome
            || trackedBody callee index count
        then
            None
        elif lambdas = 0 then
            go 0 rest
        else
            None

    go 0 path

/// The `item: unit -> 'T` parameter of the enclosing projection row factory when `rowRunPath` is the factory's row reader.
let private enclosingProjectionItem (rowRunPath: Step list) : FSharpMemberOrFunctionOrValue option =
    let rec stripPrefix =
        function
        | Step.Lambda (_, true) :: rest
        | Step.Bound _ :: rest -> stripPrefix rest
        | rest -> rest

    match stripPrefix rowRunPath with
    | Step.Lambda (Some item, false) :: Step.Argument (callee, index, _) :: _ when projectionFactoryLambdaCount callee index = Some 1 -> Some item
    | Step.Lambda (Some item, false) :: Step.Lambda (_, false) :: Step.Argument (callee, index, _) :: _ when
        projectionFactoryLambdaCount callee index = Some 2
        ->
        Some item
    | _ -> None

/// Collects direct `(callRange, fnRange)` calls to the row `item ()` reader in `expr`, stopping before user lambdas,
/// `untrack`, nested tracked/untracked callbacks, CE headers, and nested CE `.Run` blocks.
let private directRowItemCalls (item: FSharpMemberOrFunctionOrValue) (expr: FSharpExpr) : (range * range) list =
    let results = ResizeArray<range * range>()

    let rec walkStepLambda (arg: FSharpExpr) =
        match arg with
        | Lambda (_, body) -> walk body
        | _ -> walk arg

    and walk (e: FSharpExpr) =
        match e with
        | Lambda _ -> ()
        | Call (_, callee, _, _, _) when (isCeRun callee).IsSome || isUntrack callee -> ()
        | Call (receiver, callee, _, _, [ body ]) when isCeHeaderFor callee ->
            Option.iter walk receiver
            walkStepLambda body
        | Call (None, pipe, _, _, [ arg; fn ]) when Symbols.isPipe Symbols.PipeRight pipe ->
            walk arg
            walkAppliedLambda fn
        | Call (None, pipe, _, _, [ fn; arg ]) when Symbols.isPipe Symbols.PipeLeft pipe ->
            walk arg
            walkAppliedLambda fn
        | Call (receiver, callee, _, _, args) ->
            Option.iter walk receiver

            args
            |> List.iteri (fun i arg ->
                if
                    not (trackedBody callee i args.Length)
                    && (untrackedPhaseLayers callee i).IsNone
                then
                    if
                        isCeStepLambda callee i args.Length
                        || isBatchOrTaskDelay callee
                    then
                        walkStepLambda arg
                    else
                        walk arg)
        | Application (Value fn, _, args) when fn = item && args.Length = 1 ->
            let fnRange =
                match e with
                | Application (fnExpr, _, _) -> fnExpr.Range
                | _ -> e.Range

            results.Add (e.Range, fnRange)
            List.iter walk args
        | Application (fn, _, args) ->
            walkAppliedLambda fn
            List.iter walk args
        | ObjectExpr (_, baseCall, _, _) -> walk baseCall
        | _ ->
            for sub in e.ImmediateSubExpressions do
                walk sub

    and walkAppliedLambda fn =
        match fn with
        | Lambda (_, body) -> walkAppliedLambda body
        | _ -> walk fn

    walk expr
    List.ofSeq results

let private directReadsWithoutBind (input: Input) : Message list =
    let candidateRuns =
        input.Index.Calls
        |> Array.filter (fun site ->
            match isCeRun site.Callee with
            | Some kind -> isRan018Builder kind
            | None -> false)

    if Array.isEmpty candidateRuns then
        []
    else
        let boundRunPaths = HashSet<Step list>(HashIdentity.Reference)

        for site in input.Index.Calls do
            if ceBindKind site.Callee = Some CeBindKind.Reactive then
                match enclosingBindRunPath site.Path with
                | Some runPath -> boundRunPaths.Add runPath |> ignore
                | None -> ()

        [
            for site in input.Index.Calls do
                match site.Receiver with
                | Some receiver when isReactiveRead site.Callee ->
                    match enclosingDirectCeRun site.Path with
                    | Some (runCallee, kind, runPath) when
                        isRan018Builder kind
                        && not (boundRunPaths.Contains runPath)
                        && not (afterAwait input site.Path)
                        ->
                        let read = text input site.Range
                        let source = text input receiver.Range
                        let builderName = Symbols.display runCallee

                        message
                            "RAN018"
                            Severity.Hint
                            site.Range
                            $"`{read}` is read directly inside `{builderName} {{ ... }}` with no `let!` bindings, which skips static source fusion, `.Graph` resolution, and closed-phase tracking. Bind `{source}` with `let!`."
                            []
                    | _ -> ()
                | _ -> ()

            for rowRun in candidateRuns do
                if
                    isCeRun rowRun.Callee = Some CeKind.Row
                    && not (boundRunPaths.Contains rowRun.Path)
                then
                    match enclosingProjectionItem rowRun.Path, rowRun.Arguments with
                    | Some item, [ bodyExpr ] ->
                        for callRange, fnRange in directRowItemCalls item bodyExpr do
                            let read = text input callRange
                            let source = text input fnRange

                            message
                                "RAN018"
                                Severity.Hint
                                callRange
                                $"`{read}` is read directly inside `row {{ ... }}` with no `let!` bindings, which skips static source fusion, `.Graph` resolution, and closed-phase tracking. Bind `{source}` with `let!`."
                                []
                    | _ -> ()
        ]
        |> List.sortBy (fun m -> m.Range.StartLine, m.Range.StartColumn)

let ran018: Rule =
    {
        Code = "RAN018"
        Title = "Reactive read in a computation expression with no let! bindings"
        Gate = Symbols.Assemblies.Ranvier
        Run = directReadsWithoutBind
    }
