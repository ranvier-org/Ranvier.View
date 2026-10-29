/// <summary>Reads the versioned WebKit contract and native dispatch model shipped with the interop package.</summary>
module internal Ranvier.View.WebCore.Generate.Metadata

open System
open System.Reflection
open System.Text.Json

/// <summary>Reads a string field; JSON null produces a null reference.</summary>
let text (name: string) (value: JsonElement) =
    value.GetProperty(name).GetString()

/// <summary>Tests an optional string field without requiring legacy models to declare it.</summary>
let hasText (name: string) (expected: string) (value: JsonElement) =
    let mutable field = Unchecked.defaultof<JsonElement>

    value.ValueKind = JsonValueKind.Object
    && value.TryGetProperty (name, &field)
    && field.ValueKind = JsonValueKind.String
    && field.GetString () = expected

/// <summary>Reads an array field into values owned by the model.</summary>
let items (name: string) (value: JsonElement) =
    value.GetProperty(name).EnumerateArray()
    |> Seq.toArray

/// <summary>Resolves a native IDL interface name to its public shared contract name.</summary>
let publicInterfaceName (model: JsonElement) (idlName: string) =
    let mutable contracts = Unchecked.defaultof<JsonElement>

    if model.TryGetProperty ("contractInterfaces", &contracts) then
        contracts.EnumerateArray ()
        |> Seq.tryFind (fun row -> text "idlName" row = idlName)
        |> Option.map (text "name")
        |> Option.defaultValue idlName
    else
        idlName

/// <summary>Reads an independent model value; rejects unsupported schema versions.</summary>
let read (path: string) =
    use parsed = JsonDocument.Parse (System.IO.File.ReadAllText path)
    let root = parsed.RootElement.Clone ()

    if root.GetProperty("schemaVersion").GetInt32() <> 1 then
        invalidArg "path" "Unsupported DOM metadata schema"

    root

/// <summary>Native exports for a member whose checked target interface accepts the concrete wrapper.</summary>
let candidates (root: JsonElement) (assembly: Assembly) (concrete: Type) (owner: string) (memberName: string) (kind: string) =
    items "functions" root
    |> Array.filter (fun fn ->
        text "contractOwner" fn = owner
        && text "member" fn = memberName
        && text "kind" fn = kind)
    |> Array.filter (fun fn ->
        let target =
            assembly.GetType (
                "Ranvier.Dom."
                + publicInterfaceName root (text "interface" fn)
            )

        not (isNull target)
        && target.IsAssignableFrom concrete)
