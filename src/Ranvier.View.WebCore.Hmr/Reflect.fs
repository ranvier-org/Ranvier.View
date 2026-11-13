/// Reflection over F# modules and Ranvier.View internals (dev mode only; JIT, not AOT).
module Ranvier.View.WebCore.Hmr.Reflect

open System
open System.Reflection
open Ranvier.View

let private statics =
    BindingFlags.Static
    ||| BindingFlags.Public
    ||| BindingFlags.NonPublic

/// FSI emits modules under an interaction prefix (FSI_0004.); compiled assemblies do not.
let private shadowSuffix = Text.RegularExpressions.Regex @"__hmr\d+"

/// Strips what differs between compilations of the same source: the FSI interaction prefix and the
/// shadow-module suffix of a declaration-scoped reload.
let normalizeTypeName (name: string) =
    let name = shadowSuffix.Replace (name.Replace ('+', '.'), "")

    if name.StartsWith "FSI_" then
        match name.IndexOf '.' with
        | -1 -> name
        | i -> name.Substring (i + 1)
    else
        name

let private isModule (t: Type) =
    t.IsAbstract
    && t.IsSealed
    && not t.IsGenericTypeDefinition

/// Module-level component values: "Namespace.Module.name" -> definition.
let componentsOf (types: Type seq) : (string * TemplateComponent) list =
    [
        for t in types do
            if isModule t then
                for p in t.GetProperties statics do
                    if
                        typeof<TemplateComponent>.IsAssignableFrom p.PropertyType
                        && p.GetIndexParameters().Length = 0
                    then
                        match
                            (try
                                p.GetValue null
                             with _ ->
                                 null)
                        with
                        | :? TemplateComponent as c -> yield (normalizeTypeName t.FullName + "." + p.Name), c
                        | _ -> ()
    ]

let safeTypes (a: Assembly) : Type seq =
    try
        a.GetTypes () :> _
    with :? ReflectionTypeLoadException as e ->
        e.Types |> Seq.filter (fun t -> not (isNull t))

/// Builds another definition of the same Component<'props> type from a setup and a root (internal constructor).
let rebuild (like: TemplateComponent) (setup: Func<Setup, obj, obj>) (root: TemplateNode) : TemplateComponent =
    let ctor =
        like
            .GetType()
            .GetConstructors(
                BindingFlags.Instance
                ||| BindingFlags.NonPublic
                ||| BindingFlags.Public
            )
        |> Array.find (fun c -> c.GetParameters().Length = 2)

    ctor.Invoke [| box setup; box root |] :?> TemplateComponent

let view (definition: TemplateComponent) (props: obj) : Program.View =
    Program.View (fun backend -> backend.Template (definition, props))
