/// <summary>
/// Writes <c>Generated/Objects.fs</c>: one class per interface of the class set, each implementing the interfaces its
/// base class does not, with every member mapped to an <c>rv_dom_*</c> call, a hand-written body, or a throw.
/// </summary>
module Ranvier.View.WebCore.Generate.Emit

open System
open System.Reflection
open System.Text
open System.Text.Json
open Microsoft.FSharp.Reflection
open Ranvier.View.WebCore.Generate.Types

/// <summary>How a member was emitted.</summary>
type Status =
    | Mapped of functionName: string
    | Hand
    | Unmapped of reason: string

/// <summary>One emitted member: its class, interface, display name and status.</summary>
type Entry =
    {
        Class: string
        Interface: string
        Member: string
        Status: Status
    }

/// <summary>The inputs of a run.</summary>
type Input =
    {
        /// <summary>A representative type from the shared DOM contract assembly.</summary>
        Dom: Type
        /// <summary>The canonical IDL model, including mixin owners and concrete dispatch targets.</summary>
        Model: JsonElement
        Functions: Abi.Function list
        /// <summary>
        /// Hand-written bodies by key: <c>Interface.member</c>, <c>.get</c>/<c>.set</c> for accessors, <c>&lt;&gt;</c> for generic methods.
        /// </summary>
        Hand: Map<string, string>
        /// <summary>Derived contract interfaces that also need native wrapper classes.</summary>
        Extra: string list
        /// <summary>The release kind of each root class.</summary>
        Kinds: Map<string, string>
    }

let private flags =
    BindingFlags.Public
    ||| BindingFlags.Instance
    ||| BindingFlags.DeclaredOnly

let private idl (tsName: string) =
    tsName.Split('`')[0]

/// <summary>The class name of an interface type definition, with its generic parameters.</summary>
let className (t: Type) =
    let baseName = t.Name.Split('`')[0] + "Object"
    baseName + generics (t.GetGenericArguments ())

let private classBase (t: Type) =
    t.Name.Split('`')[0] + "Object"

type private Context =
    {
        Input: Input
        Classes: Map<string, Type>
        Index: Map<string * string * string, Abi.Function list>
    }

let private isInterface (ctx: Context) (t: Type) =
    t.IsInterface || t.IsGenericParameter

/// <summary>The class for a TS type: a class-set interface, or an instance of a generic one.</summary>
let private classFor (ctx: Context) (t: Type) : string option =
    if t.IsGenericType && not t.IsGenericTypeDefinition then
        let definition = t.GetGenericTypeDefinition ()

        if ctx.Classes.ContainsKey definition.Name then
            Some (
                sprintf
                    "%s<%s>"
                    (classBase definition)
                    (t.GetGenericArguments ()
                     |> Array.map name
                     |> String.concat ", ")
            )
        else
            None
    elif ctx.Classes.ContainsKey t.Name then
        Some (classBase t)
    else
        None

let private literal (value: string) =
    match value with
    | "false" -> Some "0"
    | "true" -> Some "1"
    | "\"\"" -> Some "\"\""
    | _ when value.StartsWith "0x" -> Some (string (Convert.ToUInt64 (value, 16)))
    | _ when fst (Double.TryParse value) -> Some value
    | _ -> None

let private numericType (t: Type) =
    match Type.GetTypeCode t with
    | TypeCode.SByte
    | TypeCode.Byte
    | TypeCode.Int16
    | TypeCode.UInt16
    | TypeCode.Int32
    | TypeCode.UInt32
    | TypeCode.Int64
    | TypeCode.UInt64
    | TypeCode.Single
    | TypeCode.Double -> true
    | _ -> false

/// <summary>The F# expression passing the TS parameter <c>v</c> of type <c>ts</c> as the C parameter <c>p</c>.</summary>
let private input (ctx: Context) (fn: Abi.Function) (qualified: string) (v: string) (ts: Type) (p: Abi.Parameter) : Result<string, string> =
    let inner, nullable =
        match optionOf ts with
        | Some inner -> inner, true
        | None -> ts, false

    let scalar (convert: string -> string option) =
        match convert "x" with
        | None -> Error $"argument {p.Name}: {name ts} as {p.FSharp}"
        | Some _ when not nullable -> Ok (convert v).Value
        | Some converted ->
            let typed (value: string) =
                match p.FSharp with
                | "uint32" -> value + "u"
                | "uint16" -> value + "us"
                | "int64" -> value + "L"
                | "uint64" -> value + "UL"
                | "float" when not (value.Contains ".") -> value + ".0"
                | "float32" ->
                    (if value.Contains "." then value else value + ".0")
                    + "f"
                | _ -> value

            match fn.Defaults.TryFind p.Name |> Option.bind literal with
            | Some fallback -> Ok $"(match {v} with Some x -> {converted} | None -> {typed fallback})"
            | None -> Error $"argument {p.Name}: optional without an IDL default"

    let numeric (cast: string) (x: string) =
        if numericType inner then Some $"{cast} {x}" else None

    let argumentIndex =
        fn.Inputs
        |> List.findIndex (fun candidate -> candidate.Name = p.Name)

    let argument = (Metadata.items "arguments" fn.Descriptor)[argumentIndex]
    let mapping = argument.GetProperty "mapping"
    let mappingKind = Metadata.text "kind" mapping

    match p.FSharp with
    | "byte[]" when
        inner = typeof<string>
        && Metadata.hasText "stringInputs" "utf8-lengths" fn.Descriptor
        ->
        if not nullable then
            Ok $"scope.Bytes({v})"
        else
            match fn.Defaults.TryFind p.Name |> Option.bind literal with
            | Some fallback -> Ok $"scope.Bytes(defaultArg {v} {fallback})"
            | None -> Ok $"(match {v} with Some x -> scope.Bytes(x) | None -> null)"
    | "int" when mappingKind = "enum" ->
        Input.encode ctx.Input.Model mapping inner (if nullable then "item" else v)
        |> Result.bind (fun expression ->
            if not nullable then
                Ok expression
            else
                match fn.Defaults.TryFind p.Name with
                | Some raw ->
                    let values =
                        ctx.Input.Model.GetProperty("enumerations").GetProperty(Metadata.text "enum" mapping).EnumerateArray()
                        |> Seq.map _.GetString()
                        |> Seq.toArray

                    match
                        values
                        |> Array.tryFindIndex ((=) (raw.Trim ('"')))
                    with
                    | Some index -> Ok $"(match {v} with Some item -> {expression} | None -> {index})"
                    | None -> Error $"Unknown enum default {raw} for {qualified}"
                | None -> Error $"Optional enum {p.Name} has no IDL default")
    | "int[]" when mappingKind = "enum" && nullable ->
        Input.encode ctx.Input.Model mapping inner "item"
        |> Result.map (fun expression -> $"(match {v} with None -> null | Some item -> [| {expression} |])")
    | "string" when inner = typeof<string> ->
        Ok (
            if nullable then
                match fn.Defaults.TryFind p.Name with
                | Some raw when raw.StartsWith "\"" ->
                    let fallback = sprintf "%A" (JsonSerializer.Deserialize<string> raw)
                    $"Option.defaultValue {fallback} {v}"
                | _ -> $"Option.toObj {v}"
            elif mapping.GetProperty("nullable").GetBoolean() then
                v
            else
                $"(if isNull {v} then \"\" else {v})"
        )
    | "int" ->
        scalar (fun x ->
            if inner = typeof<bool> then
                Some $"(if {x} then 1 else 0)"
            else
                numeric "int" x)
    | "uint32" -> scalar (numeric "uint32")
    | "uint16" -> scalar (numeric "uint16")
    | "int16" -> scalar (numeric "int16")
    | "int64" -> scalar (numeric "int64")
    | "uint64" -> scalar (numeric "uint64")
    | "float" -> scalar (numeric "float")
    | "float32" -> scalar (numeric "float32")
    | "nativeint" when p.C.StartsWith "Rv" && isInterface ctx inner ->
        let transformInput =
            List.contains
                fn.Interface
                [
                    "CSSTransformValue"
                    "CSSTransformComponent"
                    "CSSTranslate"
                    "CSSRotate"
                    "CSSScale"
                    "CSSSkew"
                    "CSSSkewX"
                    "CSSSkewY"
                    "CSSPerspective"
                    "CSSMatrixComponent"
                ]

        let svgInput = fn.Interface.StartsWith ("SVG", System.StringComparison.Ordinal)

        let reference value =
            if svgInput then
                let kind =
                    if mappingKind = "node" then
                        "Kind.Node"
                    else
                        ctx.Input.Kinds[Metadata.publicInterfaceName ctx.Input.Model (Metadata.text "root" mapping)]

                let allowNull = mapping.GetProperty("nullable").GetBoolean()
                $"Interop.svgHandle this.Session {kind} {allowNull.ToString().ToLowerInvariant()} (box ({value}))"
            elif transformInput then
                match Metadata.text "root" mapping with
                | "CSSStyleValue" -> $"Interop.valueHandle this.Session (box ({value}))"
                | "CSSTransformComponent" -> $"Interop.transformHandle this.Session Kind.TransformComponent (box ({value}))"
                | "DOMMatrixReadOnly" -> $"Interop.transformHandle this.Session Kind.Matrix (box ({value}))"
                | _ -> $"Interop.handle (box ({value}))"
            else
                $"Interop.handle (box ({value}))"

        Ok (
            if nullable then
                if transformInput || svgInput then
                    let expression = reference "value"
                    $"(match {v} with None -> 0n | Some value -> {expression})"
                else
                    $"Interop.handleOr {v}"
            else if transformInput || svgInput then
                reference v
            else
                $"Interop.handle (box {v})"
        )
    | "int[]" when nullable && inner = typeof<bool> -> Ok $"(match {v} with Some x -> [| (if x then 1 else 0) |] | None -> null)"
    | "int[]" when nullable && numericType inner -> Ok $"(match {v} with Some x -> [| int x |] | None -> null)"
    | "uint32[]" when nullable && numericType inner -> Ok $"(match {v} with Some x -> [| uint32 x |] | None -> null)"
    | "float[]" when nullable && numericType inner -> Ok $"(match {v} with Some x -> [| float x |] | None -> null)"
    | container when
        container.EndsWith "[]"
        && container.StartsWith "RvDom"
        ->
        Input.encode ctx.Input.Model mapping inner (if nullable then "item" else v)
        |> Result.map (fun expression ->
            if
                fn.Interface.StartsWith ("SVG", System.StringComparison.Ordinal)
                && mappingKind = "dict"
            then
                if nullable then
                    $"(match {v} with None -> null | Some item when isNull (box item) -> null | Some item -> [| {expression} |])"
                else
                    $"(if isNull (box {v}) then null else [| {expression} |])"
            elif nullable then
                $"(match {v} with None -> null | Some item -> [| {expression} |])"
            else
                $"[| {expression} |]")
    | _ -> Error $"argument {p.Name}: {name ts} as {p.C}"

let private zero (fsharp: string) =
    match fsharp with
    | "int&" -> "0"
    | "nativeint&" -> "0n"
    | "float&" -> "0.0"
    | "uint32&" -> "0u"
    | "int16&" -> "0s"
    | "uint16&" -> "0us"
    | "float32&" -> "0.0f"
    | "int64&" -> "0L"
    | "uint64&" -> "0UL"
    | other when other.EndsWith "&" -> $"Unchecked.defaultof<NativeDom.{other.TrimEnd '&'}>"
    | other -> failwith $"Unsupported output parameter type {other}"

let private snake (value: string) =
    Text.RegularExpressions.Regex.Replace (value, "([a-z0-9])([A-Z])", "$1_$2")
    |> fun text -> Text.RegularExpressions.Regex.Replace(text, "([A-Z])([A-Z][a-z])", "$1_$2").ToLowerInvariant()

let private releaseContainer (ctx: Context) (mapping: JsonElement) =
    let ctype = Metadata.text "ctype" mapping

    Metadata.items "containers" ctx.Input.Model
    |> Array.find (fun item -> Metadata.text "ctype" (item.GetProperty "mapping") = ctype)
    |> Metadata.text "releaseFunction"

let private runtimeQueries (model: JsonElement) =
    let mutable queries = Unchecked.defaultof<JsonElement>

    if model.TryGetProperty ("runtimeTypeQueries", &queries) then
        queries.EnumerateArray () |> Seq.toArray
    else
        [||]

let private wrappedObject (ctx: Context) (mapping: JsonElement) (managed: Type) (value: string) (owner: string) =
    match classFor ctx managed with
    | None -> Error $"returns {name managed}, which has no class"
    | Some cls ->
        let root = Metadata.text "root" mapping
        let publicRoot = Metadata.publicInterfaceName ctx.Input.Model root

        let query =
            runtimeQueries ctx.Input.Model
            |> Array.tryFind (fun row -> Metadata.text "root" row = root)

        if
            query.IsSome
            || (root.StartsWith ("SVG", System.StringComparison.Ordinal)
                && ctx.Input.Kinds.ContainsKey publicRoot)
            || List.contains
                root
                [
                    "CSSRuleList"
                    "StyleSheetList"
                    "MediaList"
                    "DOMTokenList"
                    "DOMRectList"
                    "DOMRectReadOnly"
                    "DOMQuad"
                    "CSSOMVariableReferenceValue"
                    "Blob"
                    "FileList"
                    "DOMURL"
                    "URLSearchParams"
                    "URLPattern"
                ]
        then
            let kind = ctx.Input.Kinds[publicRoot]

            let create =
                if query.IsSome then
                    $"Classes.{root}(session, handle)"
                else
                    $"({cls}(session, handle) :> NativeObject)"

            let create =
                if
                    root = "StylePropertyMapReadOnly"
                    || root = "DOMMatrixReadOnly"
                then
                    $"(let document = Interop.contextDocument this in let target = {create} in target.BindDocumentContext(document); target)"
                else
                    create

            Ok
                $"(unbox<{name managed}> (this.Session.Object({value}, {kind}, {owner}, System.Func<Session, nativeint, NativeObject>(fun session handle -> {create}))))"
        else
            Ok $"(this.Session.Track ({cls}(this.Session, {value})) :> {name managed})"

let rec private decoded (ctx: Context) (mapping: JsonElement) (managed: Type) (value: string) (owner: string) : Result<string, string> =
    let kind = Metadata.text "kind" mapping
    let plain = optionOf managed |> Option.defaultValue managed

    let wrap expression =
        if optionOf managed |> Option.isSome then
            $"(if {value}.present = 0 then None else Some ({expression}))"
        else
            expression

    match kind with
    | "void" when plain = typeof<unit> -> Ok "()"
    | "bool" -> Ok $"({value} <> 0)"
    | "number" -> Ok $"{name plain} ({value})"
    | "string" ->
        let mutable transport = Unchecked.defaultof<JsonElement>

        if
            mapping.TryGetProperty ("stringTransport", &transport)
            && transport.GetString () = "utf8-lengths"
        then
            Ok $"Interop.spanText ({value})"
        else
            Ok $"System.Runtime.InteropServices.Marshal.PtrToStringUTF8({value})"
    | "enum" when FSharpType.IsUnion plain ->
        let cases = FSharpType.GetUnionCases plain

        let branches =
            cases
            |> Array.mapi (fun index case -> $"| {index} -> {name plain}.{ident case.Name}")
            |> String.concat " "

        Ok $"(match int ({value}) with {branches} | _ -> invalidOp \"Unknown {plain.Name} value from WebCore\")"
    | "node"
    | "event" ->
        let retain, session =
            if kind = "node" then
                "rv_node_retain", "Node"
            else
                "rv_event_retain", "Event"

        Ok $"(let handle = {value} in Native.{retain} handle |> ignore; unbox<{name plain}> (this.Session.{session} handle))"
    | "object" ->
        let root = Metadata.text "root" mapping |> snake
        let stableOwner = if owner = "this" then "this" else "valueOwner"

        wrappedObject ctx mapping plain "handle" stableOwner
        |> Result.map (fun wrapped ->
            let capture =
                if owner = "this" then
                    ""
                else
                    $"let valueOwner = {owner} in "

            let expression =
                $"({capture}let handle = {value} in NativeDom.rv_dom_{root}_retain handle |> ignore; {wrapped})"

            if mapping.GetProperty("nullable").GetBoolean() then
                $"(if {value} = 0n then Unchecked.defaultof<{name plain}> else {expression})"
            else
                expression)
    | "sequence" when plain.IsArray ->
        let element = plain.GetElementType ()
        let nullableSequence = mapping.GetProperty("nullable").GetBoolean()
        let mapping = (Metadata.items "subtypes" mapping)[0]

        let nativeType =
            match Metadata.text "kind" mapping with
            | "node"
            | "event"
            | "object" -> "nativeint"
            | "string" ->
                let mutable transport = Unchecked.defaultof<JsonElement>

                if
                    mapping.TryGetProperty ("stringTransport", &transport)
                    && transport.GetString () = "utf8-lengths"
                then
                    "NativeDom.RvDomUtf8StringSpan"
                else
                    "nativeint"
            | "union"
            | "sequence" -> "NativeDom." + Metadata.text "ctype" mapping
            | "bool"
            | "enum" -> "int"
            | _ -> name element

        decoded ctx mapping element "item" owner
        |> Result.map (fun expression ->
            let items =
                $"(Array.init {value}.count (fun index -> let item = System.Runtime.InteropServices.Marshal.PtrToStructure<{nativeType}>({value}.items + nativeint (index * System.Runtime.InteropServices.Marshal.SizeOf<{nativeType}>())) in {expression}))"

            // present 0 is the IDL null of a nullable sequence.
            if nullableSequence then
                $"(if {value}.present = 0 then null else {items})"
            else
                items)
    | "union" when FSharpType.IsUnion plain ->
        let cases = FSharpType.GetUnionCases plain
        let arms = Metadata.items "subtypes" mapping

        if cases.Length <> arms.Length then
            Error $"Union arm count differs for {name plain}"
        else
            let branches =
                Array.zip cases arms
                |> Array.mapi (fun index (case, arm) ->
                    let fields = case.GetFields ()

                    if fields.Length <> 1 then
                        Error $"Union arm {case.Name} lacks a single payload"
                    else
                        decoded ctx arm fields[0].PropertyType $"{value}.arm{index}" owner
                        |> Result.map (fun expression -> $"| {index + 1} -> {name plain}.{ident case.Name} ({expression})"))

            match
                branches
                |> Array.tryPick (function
                    | Error reason -> Some reason
                    | _ -> None)
            with
            | Some reason -> Error reason
            | None ->
                let branches =
                    branches
                    |> Array.choose (function
                        | Ok text -> Some text
                        | _ -> None)
                    |> String.concat " "

                Ok (wrap $"(match {value}.tag with {branches} | _ -> invalidOp \"Unknown {plain.Name} union tag from WebCore\")")
    | _ -> Error $"Unsupported owned result {kind} as {name managed}"

/// <summary>The body of a mapped member, or why it cannot be mapped.</summary>
let private body (ctx: Context) (fn: Abi.Function) (qualified: string) (parameters: (string * Type) list) (returns: Type) =
    let arguments = Metadata.items "arguments" fn.Descriptor

    let inputs =
        fn.Inputs
        |> List.mapi (fun index p ->
            let argumentName = Metadata.text "name" arguments[index]

            match
                parameters
                |> List.tryFind (fun (value, _) -> value.Trim ('`') = argumentName)
            with
            | Some (value, t) -> input ctx fn qualified value t p
            | None when p.FSharp.EndsWith "[]" -> Ok "null"
            | None when p.FSharp = "string" -> Ok "null"
            | None ->
                match fn.Defaults.TryFind p.Name |> Option.bind literal with
                | Some value -> Ok $"{p.FSharp} ({value})"
                | None -> Error $"Omitted argument {p.Name} has no transport default")

    match
        inputs
        |> List.tryPick (function
            | Error e -> Some e
            | Ok _ -> None)
    with
    | Some reason -> Error reason
    | None ->
        let prepared =
            List.zip fn.Inputs inputs
            |> List.mapi (fun index (parameter, expression) ->
                let expression =
                    match expression with
                    | Ok value -> value
                    | Error _ -> ""

                if
                    parameter.FSharp = "byte[]"
                    && Metadata.hasText "stringInputs" "utf8-lengths" fn.Descriptor
                then
                    let local = $"nativeString{index}"

                    let length =
                        if expression = "null" then
                            "-1"
                        elif expression.EndsWith "| None -> null)" then
                            $"(if isNull {local} then -1 else {local}.Length)"
                        else
                            local + ".Length"

                    let binding =
                        if expression = "null" then
                            $"let {local}: byte[] = null"
                        else
                            $"let {local} = {expression}"

                    [ binding ], [ local; length ]
                else
                    [], [ expression ])

        let preparation = prepared |> List.collect fst

        let args =
            "Interop.handle (box this)"
            :: ((if Metadata.hasText "contextReceiver" "Document" fn.Descriptor then
                     [ "Interop.contextHandle this" ]
                 else
                     [])
                @ (prepared |> List.collect snd))

        let valueOwner =
            if List.contains fn.Interface [ "StylePropertyMap"; "StylePropertyMapReadOnly" ] then
                "Interop.contextDocument this"
            else
                "this"

        let call (outs: string list) =
            sprintf "NativeDom.%s (%s)" fn.Name (String.concat ", " (args @ outs))

        let checkCall outs =
            sprintf "Interop.check \"%s\" (%s)" qualified (call outs)

        let inner, nullable =
            match optionOf returns with
            | Some inner -> inner, true
            | None -> returns, false

        let result =
            match fn.Outputs with
            | [] when returns = typeof<Void> || returns = typeof<unit> -> Ok [ checkCall [] ]
            | [] -> Error $"returns {name returns} with no result"
            | [ buffer ] when buffer.Name = "buffer" && inner = typeof<string> ->
                let text =
                    sprintf "Interop.text \"%s\" (fun buffer capacity length -> %s)" qualified (call [ "buffer"; "capacity"; "length" ])

                Ok
                    [
                        (if nullable then
                             $"Option.ofObj ({text})"
                         elif fn.Descriptor.GetProperty("returns").GetProperty("nullable").GetBoolean() then
                             text
                         else
                             $"Interop.nonNull ({text})")
                    ]
            | result :: rest when result.Name = "result" ->
                let present = rest |> List.exists (fun p -> p.Name = "present")
                let declare = [ sprintf "let mutable result = %s" (zero result.FSharp) ]

                let declare =
                    if present then
                        declare @ [ "let mutable present = 0" ]
                    else
                        declare

                let outs = if present then [ "&result"; "&present" ] else [ "&result" ]

                let finish (value: string) =
                    Ok (declare @ [ checkCall outs; value ])

                let scalar (value: string) =
                    if present && nullable then
                        finish $"(if present = 0 then None else Some ({value}))"
                    elif present || nullable then
                        Error $"returns {name returns} for a nullable {result.C}"
                    else
                        finish value

                let resultMapping = fn.Descriptor.GetProperty "returns"
                let resultKind = Metadata.text "kind" resultMapping

                match result.FSharp with
                | _ when resultKind = "union" || resultKind = "sequence" ->
                    decoded ctx resultMapping returns "result" valueOwner
                    |> Result.map (fun expression ->
                        declare
                        @ [
                            "try"
                            "    " + checkCall outs
                            "    " + expression
                            "finally"
                            $"    NativeDom.{releaseContainer ctx resultMapping}(&result)"
                        ])
                | "int&" when resultKind = "enum" ->
                    decoded ctx resultMapping inner "result" valueOwner
                    |> Result.bind scalar
                | "nativeint&" when result.C = "RvNode**" || result.C = "RvEvent**" ->
                    if not (isInterface ctx inner) then
                        Error $"returns {name returns} for {result.C}"
                    else
                        let wrapped =
                            if result.C = "RvNode**" then
                                "this.Session.Node result"
                            else
                                "this.Session.Event result"

                        if nullable then
                            finish $"Interop.optionOf<{name inner}> ({wrapped})"
                        else
                            finish $"unbox<{name inner}> ({wrapped})"
                | "nativeint&" ->
                    wrappedObject ctx resultMapping inner "result" "this"
                    |> Result.bind (fun created ->
                        if nullable then
                            finish $"(if result = 0n then None else Some {created})"
                        elif resultMapping.GetProperty("nullable").GetBoolean() then
                            finish $"(if result = 0n then Unchecked.defaultof<{name inner}> else {created})"
                        else
                            finish created)
                | "int&" when inner = typeof<bool> -> scalar "result <> 0"
                | ("int&" | "uint32&" | "float&" | "int16&" | "uint16&" | "float32&" | "int64&" | "uint64&") when numericType inner ->
                    scalar $"{name inner} result"
                | _ -> Error $"returns {name returns} for {result.C}"
            | _ -> Error "unrecognised results"

        result
        |> Result.map (fun lines ->
            let lines = preparation @ lines

            if
                (fn.Interface.StartsWith ("SVG", System.StringComparison.Ordinal)
                 && fn.Interface <> "SVGElement"
                 && fn.Interface <> "SVGStyleElement")
                || List.contains
                    fn.Interface
                    [
                        "CSSTransformValue"
                        "CSSTransformComponent"
                        "CSSTranslate"
                        "CSSRotate"
                        "CSSScale"
                        "CSSSkew"
                        "CSSSkewX"
                        "CSSSkewY"
                        "CSSPerspective"
                        "CSSMatrixComponent"
                    ]
            then
                [ "try" ]
                @ (lines |> List.map (fun line -> "    " + line))
                @ [ "finally"; "    System.GC.KeepAlive this" ]
                @ (parameters
                   |> List.map (fun (value, _) -> $"    System.GC.KeepAlive (box {value})"))
            else
                lines)

/// <summary>The ABI functions of a member, searched in the declaring interface, then the class and its bases.</summary>
let private candidates (ctx: Context) (chain: Type list) (iface: Type) (memberName: string) (kind: string) =
    let concrete = List.head chain

    ctx.Input.Functions
    |> List.filter (fun fn ->
        fn.ContractOwner = iface.Name
        && fn.Member = memberName
        && fn.Kind = kind
        && (let target =
                ctx.Input.Dom.Assembly.GetType (
                    "Ranvier.Dom."
                    + Metadata.publicInterfaceName ctx.Input.Model fn.Interface
                )

            not (isNull target)
            && target.IsAssignableFrom concrete))

let private resolve (ctx: Context) chain iface memberName kind (parameters: (string * Type) list) returns qualified =
    match candidates ctx chain iface memberName kind with
    | [] -> Error "no ABI function"
    | fns ->
        match
            fns
            |> List.filter (fun f ->
                let arguments = Metadata.items "arguments" f.Descriptor

                let supplied argument =
                    parameters
                    |> List.exists (fun (value, _) -> value.Trim ('`') = Metadata.text "name" argument)

                (arguments
                 |> Array.forall (fun argument ->
                     supplied argument
                     || argument.GetProperty("optional").GetBoolean()))
                && (parameters
                    |> List.forall (fun (value, _) ->
                        arguments
                        |> Array.exists (fun argument -> value.Trim ('`') = Metadata.text "name" argument))))
        with
        | [] -> Error "no ABI overload of this arity"
        | matching ->
            let attempts =
                matching
                |> List.map (fun fn -> fn, body ctx fn qualified parameters returns)

            match
                attempts
                |> List.tryFind (fun (_, r) -> Result.isOk r)
            with
            | Some (fn, Ok lines) -> Ok (fn.Name, lines)
            | _ ->
                match attempts with
                | (_, Error reason) :: _ -> Error reason
                | _ -> Error "no ABI function"

type private Emitter(ctx: Context, out: StringBuilder, entries: ResizeArray<Entry>) =
    member _.Line(indent: int, text: string) =
        out.Append(String (' ', indent * 4)).AppendLine text
        |> ignore

    member this.Body(indent: int, lines: string list) =
        for line in lines do
            this.Line (indent, line)

    /// <summary>True while the class being written is a node class, whose members a harness can intercept.</summary>
    member val Intercepts = false with get, set

    /// <summary>
    /// The body run through the session's interceptions of <c>name</c> while it has any: the arguments are rebound from
    /// the array the interceptions pass on.
    /// </summary>
    member this.Intercepted(name: string, parameters: (string * Type) list, returns: Type, lines: string list) : string list =
        let unitResult = returns = typeof<Void> || returns = typeof<unit>

        let boxed =
            parameters
            |> List.map (fun (v, _) -> $"box {v}")
            |> String.concat "; "

        [
            "if this.Session.Intercepting then"
            $"    Interop.intercept this \"{name}\" [| {boxed} |] (fun args ->"
            yield!
                parameters
                |> List.mapi (fun i (v, t) -> $"        let {v}: {Types.name t} = unbox args[{i}]")
            "        let result ="
            yield!
                lines
                |> List.map (fun line -> "            " + line)
            "        box result)"
            (if unitResult then
                 "    |> ignore"
             else
                 $"    |> unbox<{Types.name returns}>")
            "else"
            yield! lines |> List.map (fun line -> "    " + line)
        ]

    /// <summary>A member body: hand-written, mapped or a throw, recorded in the report.</summary>
    member this.Resolve
        (cls: string, chain, iface: Type, memberName, kind, handKey: string, parameters, returns, intercepted: string option)
        : string list =
        let qualified = $"{idl iface.Name}.{memberName}"

        let wrap lines =
            match intercepted with
            | Some name when this.Intercepts -> this.Intercepted (name, parameters, returns, lines)
            | _ -> lines

        let record status =
            entries.Add
                {
                    Class = cls
                    Interface = iface.Name
                    Member =
                        (if kind = "op" then
                             memberName
                         else
                             $"{memberName} ({kind}ter)")
                    Status = status
                }

        match ctx.Input.Hand.TryFind handKey with
        | Some hand ->
            record Hand

            let hand =
                parameters
                |> List.indexed
                |> List.fold (fun (text: string) (index, (variable, _)) -> text.Replace ("{" + string index + "}", variable)) hand

            wrap [ hand ]
        | None ->
            match resolve ctx chain iface memberName kind parameters returns qualified with
            | Ok (fn, lines) ->
                record (Mapped fn)

                let lines =
                    if
                        lines
                        |> List.exists (fun line -> line.Contains "scope.")
                    then
                        "use scope = new InputScope()" :: lines
                    else
                        lines

                wrap lines
            | Error reason ->
                record (Unmapped reason)
                [ $"Interop.unsupported \"{idl iface.Name}.{memberName}\"" ]

    member this.Interface(cls: string, chain: Type list, iface: Type) =
        let methods =
            iface.GetMethods flags
            |> Array.filter (fun m -> not m.IsSpecialName)

        let properties = iface.GetProperties flags

        let ifaceName =
            name (
                if iface.IsGenericTypeDefinition then
                    iface.MakeGenericType (iface.GetGenericArguments ())
                else
                    iface
            )

        if methods.Length = 0 && properties.Length = 0 then
            this.Line (1, $"interface {ifaceName}")
        else
            this.Line (1, $"interface {ifaceName} with")
            let prefix = iface.Name.Split('`')[0]

            for p in properties do
                let indexes = p.GetIndexParameters ()
                let t = name p.PropertyType
                this.Line (2, $"member this.{ident p.Name}")

                let indexArgs, indexParams, memberName, getKind =
                    if indexes.Length = 1 then
                        $"(index: {name indexes[0].ParameterType})", [ "index", indexes[0].ParameterType ], "item", "op"
                    else
                        "()", [], p.Name, "get"

                if p.CanRead then
                    this.Line (3, $"with get {indexArgs} : {t} =")

                    let intercepted = if indexes.Length = 1 then None else Some $"get_{p.Name}"

                    this.Body (
                        4,
                        this.Resolve (cls, chain, iface, memberName, getKind, $"{prefix}.{p.Name}.get", indexParams, p.PropertyType, intercepted)
                    )

                if p.CanWrite then
                    let setArgs =
                        if indexes.Length = 1 then
                            $"(index: {name indexes[0].ParameterType}) (value: {t})"
                        else
                            $"(value: {t})"

                    let keyword = if p.CanRead then "and" else "with"
                    this.Line (3, $"{keyword} set {setArgs} =")

                    let lines =
                        if indexes.Length = 1 then
                            entries.Add
                                {
                                    Class = cls
                                    Interface = iface.Name
                                    Member = "Item (setter)"
                                    Status = Unmapped "indexed setter"
                                }

                            [ $"Interop.unsupported \"{prefix}.Item\"" ]
                        else
                            this.Resolve (
                                cls,
                                chain,
                                iface,
                                p.Name,
                                "set",
                                $"{prefix}.{p.Name}.set",
                                [ "value", p.PropertyType ],
                                typeof<Void>,
                                Some $"set_{p.Name}"
                            )

                    this.Body (4, lines)

            for m in methods do
                let parameters = m.GetParameters ()

                let declared =
                    parameters
                    |> Array.mapi (fun i p ->
                        let pname = if String.IsNullOrEmpty p.Name then $"arg{i}" else p.Name

                        if p.IsDefined (typeof<OptionalArgumentAttribute>) then
                            $"?{ident pname}: {name (optionOf p.ParameterType).Value}"
                        elif p.IsDefined (typeof<ParamArrayAttribute>) then
                            $"[<System.ParamArray>] {ident pname}: {name p.ParameterType}"
                        else
                            $"{ident pname}: {name p.ParameterType}")
                    |> String.concat ", "

                let gens =
                    if m.IsGenericMethodDefinition then
                        generics (m.GetGenericArguments ())
                    else
                        ""

                this.Line (2, $"member this.{ident m.Name}{gens}({declared}) : {name m.ReturnType} =")

                let values =
                    parameters
                    |> Array.mapi (fun i p -> ident (if String.IsNullOrEmpty p.Name then $"arg{i}" else p.Name), p.ParameterType)
                    |> List.ofArray

                let key =
                    $"{prefix}.{m.Name}"
                    + (if m.IsGenericMethodDefinition then "<>" else "")

                let lines =
                    if
                        m.IsGenericMethodDefinition
                        && m.ReturnType.IsGenericParameter |> not
                        && m.GetGenericArguments ()
                           |> Array.exists (fun g -> g.GetGenericParameterConstraints().Length = 0)
                        && not (ctx.Input.Hand.ContainsKey key)
                    then
                        entries.Add
                            {
                                Class = cls
                                Interface = iface.Name
                                Member = m.Name + "<>"
                                Status = Unmapped "unconstrained generic overload"
                            }

                        [ $"Interop.unsupported \"{prefix}.{m.Name}\"" ]
                    else
                        this.Resolve (cls, chain, iface, m.Name, "op", key, values, m.ReturnType, Some m.Name)

                this.Body (3, lines)

/// <summary>The class set: in-scope interfaces, their extras, and the class each one inherits.</summary>
let private classSet (input: Input) =
    let lookup (n: string) =
        input.Dom.Assembly.GetType (
            "Ranvier.Dom."
            + Metadata.publicInterfaceName input.Model n
        )
        |> Option.ofObj

    let names =
        [
            for f in input.Functions -> f.Interface
            yield! input.Extra
        ]
        |> List.distinct

    names
    |> List.choose lookup
    |> List.filter (fun t -> t.IsInterface)
    |> List.map (fun t -> t.Name, t)
    |> Map.ofList

let private baseOf (classes: Map<string, Type>) (t: Type) : Type option =
    t.GetInterfaces ()
    |> Array.filter (fun i -> classes.ContainsKey i.Name && not i.IsGenericType)
    |> Array.sortByDescending (fun i -> i.GetInterfaces().Length, i.Name)
    |> Array.tryHead

/// <summary>Writes the generated file and returns it with the report entries.</summary>
let run (input: Input) (tagMap: (string * string * string) list) : string * Entry list =
    let classes = classSet input

    let ctx =
        {
            Input = input
            Classes = classes
            Index =
                input.Functions
                |> List.groupBy (fun f -> f.ContractOwner, f.Member, f.Kind)
                |> Map.ofList
        }

    let out = StringBuilder ()
    let entries = ResizeArray<Entry>()
    let emitter = Emitter (ctx, out, entries)

    let ordered =
        classes
        |> Map.toList
        |> List.map snd
        |> List.sortBy (fun t -> t.GetInterfaces().Length, t.Name)

    // An interface with its own release kind is a root class: its handle is released as that kind, not as its base's.
    let rec chainOf (t: Type) =
        match baseOf classes t with
        | Some _ when input.Kinds.ContainsKey t.Name -> []
        | Some b -> b :: chainOf b
        | None -> []

    let mutable first = true

    for t in ordered do
        let chain = chainOf t
        let cls = className t
        out.AppendLine () |> ignore

        out.AppendLine (sprintf "%s %s(session: Session, handle: nativeint) =" (if first then "type internal" else "and internal") cls)
        |> ignore

        first <- false

        match chain with
        | b :: _ -> emitter.Line (1, $"inherit {classBase b}(session, handle)")
        | [] ->
            match input.Kinds.TryFind (t.Name) with
            | Some kind -> emitter.Line (1, $"inherit NativeObject(session, handle, {kind})")
            | None -> failwith $"{t.Name} is a root class without a release kind"

        let covered =
            match chain with
            | b :: _ -> set [ yield b.Name; for i in b.GetInterfaces () -> i.Name ]
            | [] -> Set.empty

        let explicitly =
            t
            :: [
                for i in t.GetInterfaces () do
                    let redeclaredProperty =
                        t.GetProperties flags
                        |> Array.exists (fun property ->
                            i.GetProperties flags
                            |> Array.exists (fun inherited ->
                                property.Name = inherited.Name
                                && property.PropertyType = inherited.PropertyType
                                && (property.GetIndexParameters ()
                                    |> Array.map (fun p -> p.ParameterType))
                                    =
                                    (inherited.GetIndexParameters ()
                                     |> Array.map (fun p -> p.ParameterType))))

                    if
                        not (covered.Contains i.Name)
                        || redeclaredProperty
                    then
                        i
            ]

        // F# requires an interface of the base class to be implemented again when two of the class's own interfaces
        // include it.
        let rec withShared (set: Type list) =
            let shared =
                t.GetInterfaces ()
                |> Array.filter (fun j ->
                    covered.Contains j.Name
                    && not (set |> List.contains j)
                    && (set
                        |> List.filter (fun i -> i.GetInterfaces () |> Array.contains j)
                        |> List.length)
                       >= 2)
                |> List.ofArray

            if shared.IsEmpty then set else withShared (set @ shared)

        let implemented =
            withShared explicitly
            |> List.sortBy (fun i -> i.GetInterfaces().Length, i.Name)

        let root =
            match List.tryLast chain with
            | Some root -> root
            | None -> t

        emitter.Intercepts <- input.Kinds.TryFind root.Name = Some "Kind.Node"

        for iface in implemented do
            emitter.Interface (cls, t :: chain, iface)

    // HTML node names are upper-case; SVG names retain the catalogue's case.
    out.AppendLine () |> ignore

    out.AppendLine "/// <summary>Wraps an element using its namespace and tag from the IDL tag catalogues.</summary>"
    |> ignore

    out.AppendLine "and [<AbstractClass; Sealed>] internal Classes ="
    |> ignore

    for query in runtimeQueries input.Model do
        let root = Metadata.text "root" query
        let nativeQuery = Metadata.text "function" query
        let feature = Metadata.text "feature" query
        emitter.Line (1, $"static member {root}(session: Session, handle: nativeint) : NativeObject =")
        emitter.Line (2, $"if Native.rv_abi_has \"{feature}\" = 0 then Interop.unsupported<unit> \"{root} runtime interface\"")

        emitter.Line (
            2,
            $"let interfaceName = Interop.text \"{root} runtime interface\" (fun buffer capacity length -> NativeDom.{nativeQuery}(handle, buffer, capacity, length))"
        )

        if root = "StyleSheet" then
            emitter.Line (2, "let mutable ownerNode = 0n")
            emitter.Line (2, "Interop.check \"StyleSheet.ownerNode\" (NativeDom.rv_dom_style_sheet_get_owner_node(handle, &ownerNode))")
            emitter.Line (2, "let owner = session.Node ownerNode")

        emitter.Line (2, "let wrapped: NativeObject =")
        emitter.Line (3, "match interfaceName with")

        for interfaceName in query.GetProperty("interfaces").EnumerateArray() do
            let interfaceName = interfaceName.GetString ()
            let publicName = Metadata.publicInterfaceName input.Model interfaceName

            if not (classes.ContainsKey publicName) then
                invalidOp $"Native runtime interface {interfaceName} has no managed wrapper"

            emitter.Line (3, $"| \"{interfaceName}\" -> {publicName}Object(session, handle)")

        emitter.Line (3, $"| name -> invalidOp (\"Unknown {root} runtime interface: \" + name)")

        if root = "StyleSheet" then
            emitter.Line (2, "wrapped.KeepAlive owner")

        emitter.Line (2, "wrapped")
        out.AppendLine () |> ignore

    emitter.Line (1, "/// <summary>The wrapper class matching the element's namespace and tag.</summary>")
    emitter.Line (1, "static member Element(session: Session, handle: nativeint, tag: string) : NativeObject =")

    emitter.Line (
        2,
        "let ns = Interop.text \"Element.namespaceURI\" (fun buffer capacity length -> NativeDom.rv_dom_element_get_namespace_uri(handle, buffer, capacity, length))"
    )

    emitter.Line (2, "let tag = if ns = \"http://www.w3.org/1999/xhtml\" then tag.ToUpperInvariant() else tag")
    emitter.Line (2, "match ns, tag with")

    for ns, tag, iface in tagMap do
        if classes.ContainsKey iface then
            let tag =
                if ns = "http://www.w3.org/1999/xhtml" then
                    tag.ToUpperInvariant ()
                else
                    tag

            emitter.Line (2, $"| \"{ns}\", \"{tag}\" -> {iface}Object (session, handle)")

    emitter.Line (2, "| \"http://www.w3.org/1999/xhtml\", _ -> HTMLElementObject (session, handle)")
    emitter.Line (2, "| \"http://www.w3.org/2000/svg\", _ -> SVGElementObject (session, handle)")

    // The canonical tag table lists HTML and SVG tags only; MathMLMathElement is the one typed MathML tag interface.
    if
        classes.ContainsKey "MathMLMathElement"
        && not (
            tagMap
            |> List.exists (fun (ns, tag, _) ->
                ns = "http://www.w3.org/1998/Math/MathML"
                && tag = "math")
        )
    then
        emitter.Line (2, "| \"http://www.w3.org/1998/Math/MathML\", \"math\" -> MathMLMathElementObject (session, handle)")

    if classes.ContainsKey "MathMLElement" then
        emitter.Line (2, "| \"http://www.w3.org/1998/Math/MathML\", _ -> MathMLElementObject (session, handle)")

    emitter.Line (2, "| _ -> ElementObject (session, handle)")

    out.ToString (), List.ofSeq entries
