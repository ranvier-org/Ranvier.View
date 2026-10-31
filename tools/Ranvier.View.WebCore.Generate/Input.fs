module internal Ranvier.View.WebCore.Generate.Input

open System
open System.Text.Json
open Microsoft.FSharp.Reflection
open Ranvier.View.WebCore.Generate.Types

let private numeric (ctype: string) =
    match ctype with
    | "int32_t" -> "int"
    | "uint32_t" -> "uint32"
    | "int16_t" -> "int16"
    | "uint16_t" -> "uint16"
    | "int64_t" -> "int64"
    | "uint64_t" -> "uint64"
    | "float" -> "float32"
    | "double" -> "float"
    | other -> raise (NotSupportedException $"Numeric input transport {other}")

let private accepts (model: JsonElement) (assembly: Reflection.Assembly) (mapping: JsonElement) (t: Type) =
    match Metadata.text "kind" mapping with
    | "bool" -> t = typeof<bool>
    | "string" -> t = typeof<string>
    | "number" -> t.IsPrimitive && t <> typeof<bool>
    | "node"
    | "event"
    | "object"
    | "dict" ->
        let expected =
            assembly.GetType (
                "Ranvier.Dom."
                + Metadata.publicInterfaceName model (Metadata.text "interface" mapping)
            )

        not (isNull expected)
        && expected.IsAssignableFrom t
    | "enum" -> t.Name = Metadata.text "enum" mapping
    | "sequence" -> t.IsArray
    | _ -> false

let rec private build (model: JsonElement) (mapping: JsonElement) (t: Type) (value: string) : string =
    let kind = Metadata.text "kind" mapping

    match kind with
    | "bool" -> $"(if {value} then 1 else 0)"
    | "number" ->
        let cast = numeric (Metadata.text "ctype" mapping)
        $"{cast} ({value})"
    | "string" ->
        let mutable transport = Unchecked.defaultof<JsonElement>

        if
            mapping.TryGetProperty ("stringTransport", &transport)
            && transport.GetString () = "utf8-lengths"
        then
            $"scope.TextSpan({value})"
        else
            $"scope.Text({value})"
    | "node"
    | "event"
    | "object" ->
        let root = Metadata.text "root" mapping

        if
            root = "CSSStyleValue"
            || root = "CSSOMVariableReferenceValue"
        then
            $"Interop.valueHandle this.Session (box ({value}))"
        else
            $"Interop.handle (box ({value}))"
    | "enum" ->
        let branches =
            FSharpType.GetUnionCases t
            |> Array.mapi (fun index case -> $"| {name t}.{ident case.Name} -> {index}")
            |> String.concat " "

        $"(match {value} with {branches})"
    | "dict" ->
        let native =
            "NativeDom.RvDom"
            + Metadata.text "interface" mapping

        let geometryDictionary =
            List.contains (Metadata.text "interface" mapping) [ "DOMPointInit"; "DOMMatrixInit" ]

        let sourceValue = if geometryDictionary then "dictionary" else value

        let assignments =
            model.GetProperty("dictionaries").GetProperty(Metadata.text "interface" mapping).EnumerateArray()
            |> Seq.map (fun field ->
                let fieldName = Metadata.text "name" field
                let property = t.GetProperty fieldName

                if isNull property then
                    raise (NotSupportedException $"Dictionary field {t.Name}.{fieldName}")

                let fieldType = property.PropertyType
                let source = $"({sourceValue}).{ident property.Name}"
                let fieldMapping = field.GetProperty "mapping"
                let ctype = Metadata.text "ctype" field

                let expression =
                    if List.contains (Metadata.text "kind" fieldMapping) [ "node"; "event"; "object" ] then
                        match optionOf fieldType with
                        | Some inner ->
                            let encoded = build model fieldMapping inner "item"
                            $"(match {source} with None -> 0n | Some item -> {encoded})"
                        | None -> build model fieldMapping fieldType source
                    elif ctype.EndsWith "*" && ctype <> "const char*" then
                        match optionOf fieldType with
                        | Some inner ->
                            let encoded = build model fieldMapping inner "item"
                            $"(match {source} with None -> 0n | Some item -> scope.Pin([| {encoded} |]))"
                        | None -> $"scope.Pin([| {build model fieldMapping fieldType source} |])"
                    else
                        build model fieldMapping fieldType source

                let nativeField = Metadata.text "field" field |> ident
                $"result.{nativeField} <- {expression}")
            |> String.concat "; "

        let assignments = if assignments = "" then "" else assignments + "; "

        let defaults =
            if geometryDictionary then
                $"let dictionary = if isNull (box ({value})) then {name t}() else {value} in "
            else
                ""

        $"({defaults}let mutable result = Unchecked.defaultof<{native}> in result.size <- uint32 (System.Runtime.InteropServices.Marshal.SizeOf<{native}>()); {assignments}result)"
    | "sequence" when t.IsArray ->
        let native = "NativeDom." + Metadata.text "ctype" mapping
        let element = (Metadata.items "subtypes" mapping)[0]
        let converted = build model element (t.GetElementType ()) "item"
        $"(let values = ({value}) |> Array.map (fun item -> {converted}) in let mutable result = Unchecked.defaultof<{native}> in result.present <- 1; result.count <- values.Length; result.items <- scope.Pin(values); result)"
    | "union" ->
        let native = "NativeDom." + Metadata.text "ctype" mapping
        let arms = Metadata.items "subtypes" mapping

        let armValue index arm fieldType source =
            let converted = build model arm fieldType source

            let converted =
                if Metadata.text "kind" arm = "dict" then
                    $"scope.Pin([| {converted} |])"
                else
                    converted

            $"(let mutable result = Unchecked.defaultof<{native}> in result.present <- 1; result.tag <- {index + 1}; result.arm{index} <- {converted}; result)"

        if
            t.Name = Metadata.text "label" mapping
            && FSharpType.IsUnion t
        then
            let cases = FSharpType.GetUnionCases t

            if cases.Length <> arms.Length then
                raise (NotSupportedException $"Union input arm count for {t.Name}")

            let branches =
                Array.zip cases arms
                |> Array.mapi (fun index (case, arm) ->
                    let fields = case.GetFields ()
                    let fieldType = fields[0].PropertyType
                    let expression = armValue index arm fieldType "item"
                    $"| {name t}.{ident case.Name} item -> {expression}")
                |> String.concat " "

            $"(match {value} with {branches})"
        else
            match
                arms
                |> Array.tryFindIndex (fun arm -> accepts model t.Assembly arm t)
            with
            | Some index -> armValue index arms[index] t value
            | None ->
                let label = Metadata.text "label" mapping
                raise (NotSupportedException $"Union {label} does not accept {name t}")
    | _ -> raise (NotSupportedException $"Input transport {kind} as {name t}")

let encode model mapping t value =
    try
        Ok (build model mapping t value)
    with :? NotSupportedException as error ->
        Error error.Message
