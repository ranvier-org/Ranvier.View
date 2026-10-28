module RanvierSharedDomTypes

open System
open System.Reflection

/// <summary>Resolves an HTML or event interface to its shared DOM contract.</summary>
let contract (assembly: Assembly) (native: Type) =
    let name =
        match native.Name.Split('`')[0] with
        | "NodeListOf" -> "NodeList"
        | "HTMLCollectionOf" -> "HTMLCollection"
        | name -> name

    assembly.GetType ("Ranvier.Dom." + name, true)

/// <summary>Formats the shared contract's F# value type for generated authoring signatures.</summary>
let rec render (assembly: Assembly) (native: Type) =
    let primitive =
        [
            typeof<string>, "string"
            typeof<bool>, "bool"
            typeof<float>, "float"
            typeof<float32>, "float32"
            typeof<int16>, "int16"
            typeof<uint16>, "uint16"
            typeof<int32>, "int"
            typeof<uint32>, "uint32"
            typeof<int64>, "int64"
            typeof<uint64>, "uint64"
            typeof<byte>, "byte"
            typeof<sbyte>, "sbyte"
        ]

    match
        primitive
        |> List.tryFind (fun (candidate, _) -> native = candidate)
    with
    | Some (_, name) -> name
    | None when
        native.Assembly = assembly
        && not native.IsGenericType
        ->
        "Dom." + native.Name
    | None when
        native.IsGenericType
        && native.GetGenericTypeDefinition () = typedefof<option<_>>
        ->
        render assembly (native.GetGenericArguments()[0])
        + " option"
    | _ -> invalidOp $"Unsupported shared DOM value shape: {native}"

/// <summary>Projects browser-only values to shared contracts, scalar strings or opaque objects.</summary>
let rec browserValue (assembly: Assembly) (native: Type) =
    if
        native.IsGenericType
        && native.GetGenericTypeDefinition () = typedefof<option<_>>
    then
        let inner = native.GetGenericArguments()[0]
        let projected = browserValue assembly inner

        if inner.IsValueType then
            projected + " option"
        else
            projected
    else
        let name =
            match native.Name.Split('`')[0] with
            | "NodeListOf" -> "NodeList"
            | "HTMLCollectionOf" -> "HTMLCollection"
            | name -> name

        let shared = assembly.GetType ("Ranvier.Dom." + name)

        if not (isNull shared) then
            render assembly shared
        elif
            native.GetCustomAttributesData ()
            |> Seq.exists (fun attribute -> attribute.AttributeType.Name = "StringEnumAttribute")
        then
            "string"
        else
            try
                render assembly native
            with :? InvalidOperationException ->
                "obj"
