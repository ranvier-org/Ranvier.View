/// <summary>The generated <c>rv_dom_*</c> functions, read from <c>RanvierWebCoreDom.h</c> and <c>NativeDom.fs</c>.</summary>
module Ranvier.View.WebCore.Generate.Abi

open System.Text.RegularExpressions
open System.Text.Json
open System

/// <summary>A parameter: its C type from the header, its name, and its F# type in <c>NativeDom.fs</c>.</summary>
type Parameter =
    {
        C: string
        Name: string
        FSharp: string
    }

/// <summary>
/// An <c>rv_dom_*</c> function. <c>Kind</c> is <c>get</c>, <c>set</c> or <c>op</c>; <c>Interface</c> and <c>Member</c> are
/// the IDL names from the header comment, and <c>Defaults</c> maps a parameter to its IDL default.
/// </summary>
type Function =
    {
        Name: string
        Interface: string
        /// <summary>The shared interface or mixin declaring the member.</summary>
        ContractOwner: string
        Member: string
        Kind: string
        Parameters: Parameter list
        Defaults: Map<string, string>
        /// <summary>The canonical IDL and transport mapping; undefined for the legacy header reader.</summary>
        Descriptor: JsonElement
    }

    /// <summary>The parameters after <c>target</c> that carry IDL arguments.</summary>
    member this.Inputs =
        let mutable transport = Unchecked.defaultof<JsonElement>

        let lengthDelimited =
            this.Descriptor.ValueKind = JsonValueKind.Object
            && this.Descriptor.TryGetProperty ("stringInputs", &transport)
            && transport.GetString () = "utf8-lengths"

        let contextParameter =
            if Metadata.hasText "contextReceiver" "Document" this.Descriptor then
                Some (Metadata.text "contextParameter" this.Descriptor)
            else
                None

        this.Parameters
        |> List.tail
        |> List.filter (fun p ->
            match p.Name with
            | "result"
            | "present"
            | "buffer"
            | "capacity"
            | "length" -> false
            | name when contextParameter = Some name -> false
            | _ ->
                not (
                    lengthDelimited
                    && p.Name.EndsWith "_length"
                    && (this.Parameters
                        |> List.exists (fun candidate ->
                            candidate.Name + "_length" = p.Name
                            && candidate.FSharp = "byte[]"))
                ))

    /// <summary>The result parameters: <c>result</c>, <c>present</c>, or the string triple.</summary>
    member this.Outputs =
        this.Parameters
        |> List.filter (fun p ->
            match p.Name with
            | "result"
            | "present"
            | "buffer" -> true
            | _ -> false)

let private externs (nativeDom: string) : Map<string, (string * string) list> =
    let rx = Regex @"extern \S+ (rv_dom_\w+)\((.*)\)"

    [
        for m in rx.Matches nativeDom do
            let parameters =
                m.Groups[2].Value.Split ','
                |> Array.toList
                |> List.map (fun part ->
                    let part = Regex.Replace(part, @"\[<[^>]*>\]", "").Trim()
                    let at = part.LastIndexOf ' '
                    part.Substring(0, at).Trim(), part.Substring(at + 1).Trim('`'))

            m.Groups[1].Value, parameters
    ]
    |> Map.ofList

/// <summary>Every commented <c>rv_dom_*</c> function of the header, joined with its F# declaration.</summary>
let read (header: string) (nativeDom: string) : Function list =
    let declared = externs nativeDom

    let rx =
        Regex (@"/\*\s*(?<c>(?:(?!\*/).)*?)\*/\s*RV_EXPORT\s+int32_t\s+(?<n>rv_dom_\w+)\((?<p>[^)]*)\);", RegexOptions.Singleline)

    [
        for m in rx.Matches header do
            let comment = Regex.Replace(m.Groups["c"].Value, @"\s*\n\s*\*?\s*", " ").Trim()

            let head = Regex.Match (comment, @"^(?<i>\w+)\.(?<m>\w+)(?<rest>.*)$")
            let name = m.Groups["n"].Value

            if head.Success && declared.ContainsKey name then
                let rest = head.Groups["rest"].Value

                let kind =
                    if rest.StartsWith " getter" then "get"
                    elif rest.StartsWith " setter" then "set"
                    else "op"

                let fsharp = declared[name]

                let parameters =
                    m.Groups["p"].Value.Split ','
                    |> Array.toList
                    |> List.mapi (fun i part ->
                        let mm = Regex.Match (part.Trim (), @"^(?<t>.*?[\s\*])(?<n>\w+)$")

                        {
                            C = Regex.Replace (mm.Groups["t"].Value.Trim(), @"\s+\*", "*")
                            Name = mm.Groups["n"].Value
                            FSharp = fst fsharp[i]
                        })

                let defaults =
                    [
                        for d in Regex.Matches (comment, @"(\w+) defaults to (.+?) in IDL") -> d.Groups[1].Value, d.Groups[2].Value
                    ]
                    |> Map.ofList

                {
                    Name = name
                    Interface = head.Groups["i"].Value
                    ContractOwner = head.Groups["i"].Value
                    Member = head.Groups["m"].Value
                    Kind = kind
                    Parameters = parameters
                    Defaults = defaults
                    Descriptor = Unchecked.defaultof<_>
                }
    ]

let private scalarNames =
    Map.ofList
        [
            "System.SByte", "sbyte"
            "System.Byte", "byte"
            "System.Int16", "int16"
            "System.UInt16", "uint16"
            "System.Int32", "int"
            "System.UInt32", "uint32"
            "System.Int64", "int64"
            "System.UInt64", "uint64"
            "System.Single", "float32"
            "System.Double", "float"
            "System.IntPtr", "nativeint"
            "System.String", "string"
        ]

let rec private nativeParameter (parameterType: Type) =
    if parameterType.IsByRef then
        nativeParameter (parameterType.GetElementType ())
        + "&"
    elif parameterType.IsArray then
        nativeParameter (parameterType.GetElementType ())
        + "[]"
    else
        scalarNames.TryFind parameterType.FullName
        |> Option.defaultValue parameterType.Name

let private constantName (value: string) =
    Regex.Replace (value, "([a-z0-9])([A-Z])", "$1_$2")
    |> fun text -> Regex.Replace(text, "([A-Z])([A-Z][a-z])", "$1_$2").ToUpperInvariant()

let private validateUnionTags (nativeDom: Type) (model: JsonElement) =
    for container in Metadata.items "containers" model do
        let mapping = container.GetProperty "mapping"

        if Metadata.text "kind" mapping = "union" then
            let prefix =
                "RV_DOM_"
                + constantName ((Metadata.text "ctype" mapping).Substring("RvDom".Length))

            for index, arm in Metadata.items "subtypes" mapping |> Array.indexed do
                let suffix =
                    match Metadata.text "kind" arm with
                    | "void" -> "VOID"
                    | "bool" -> "BOOL"
                    | "number" -> "NUMBER"
                    | "string" -> "STRING"
                    | "bytes" -> Metadata.text "label" arm |> constantName
                    | "sequence" -> constantName ((Metadata.text "ctype" arm).Substring("RvDom".Length))
                    | _ ->
                        // The native header names an arm by its interface, enumeration or label (domtypes.union_arm_name).
                        [ "interface"; "enum"; "label"; "kind" ]
                        |> List.pick (fun name ->
                            match arm.TryGetProperty name with
                            | true, value when value.ValueKind = JsonValueKind.String -> Some (value.GetString ())
                            | _ -> None)
                        |> constantName

                let fieldName = prefix + "_" + suffix
                let field = nativeDom.GetField fieldName

                if
                    isNull field
                    || not field.IsLiteral
                    || unbox<int>(field.GetRawConstantValue ())
                       <> index + 1
                then
                    invalidArg "nativeDom" $"Canonical managed union discriminant differs for {fieldName}"

/// <summary>Reads native declarations and their IDL mappings from the canonical transport model.</summary>
let readModel (nativeDom: Type) (model: JsonElement) : Function list =
    validateUnionTags nativeDom model

    [
        for fn in Metadata.items "functions" model do
            let functionName = Metadata.text "function" fn
            let declaration = nativeDom.GetMethod functionName

            if isNull declaration then
                invalidArg "nativeDom" $"Canonical managed interop lacks {functionName}"

            let managed = declaration.GetParameters ()
            let native = Metadata.items "parameters" fn

            if managed.Length <> native.Length then
                invalidArg "nativeDom" $"Managed/native parameter count differs for {functionName}"

            let parameters =
                [
                    for index, parameter in Array.indexed native do
                        let ctype = Metadata.text "ctype" parameter

                        {
                            C = ctype
                            Name = Metadata.text "name" parameter
                            FSharp = nativeParameter managed[index].ParameterType
                        }
                ]

            // The parameter of each IDL argument: after target, without a checked context and the byte length that
            // follows each UTF-8 string.
            let contextParameter =
                if Metadata.hasText "contextReceiver" "Document" fn then
                    Some (Metadata.text "contextParameter" fn)
                else
                    None

            let argumentParameters =
                parameters
                |> List.pairwise
                |> List.filter (fun (previous, parameter) ->
                    Some parameter.Name <> contextParameter
                    && not (
                        previous.C = "const uint8_t*"
                        && parameter.Name = previous.Name + "_length"
                    ))
                |> List.map snd

            let defaults =
                [
                    for index, argument in Metadata.items "arguments" fn |> Array.indexed do
                        let fallback = argument.GetProperty "default"

                        if fallback.ValueKind <> JsonValueKind.Null then
                            argumentParameters[index].Name, fallback.GetString ()
                ]
                |> Map.ofList

            {
                Name = Metadata.text "function" fn
                Interface = Metadata.text "interface" fn
                ContractOwner = Metadata.text "contractOwner" fn
                Member = Metadata.text "member" fn
                Kind =
                    match Metadata.text "kind" fn with
                    | "getter" -> "get"
                    | "setter" -> "set"
                    | _ -> "op"
                Parameters = parameters
                Defaults = defaults
                Descriptor = fn
            }
    ]
