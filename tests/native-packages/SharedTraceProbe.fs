module OfficialTraceProbe

open System
open System.Reflection
open Ranvier.View.WebCore

let verify () =
    let traced =
        Boolean.Parse (Environment.GetEnvironmentVariable "COHORT_EXPECT_TRACE")

    for assembly in
        [
            typeof<Ranvier.Graph>.Assembly
            typeof<Ranvier.Dom.Document>.Assembly
            typeof<Ranvier.View.Template.TemplateBuilder<unit>>.Assembly
            typeof<Ranvier.View.Dom.Mounting.Mounted>.Assembly
            typeof<WebCoreHost>.Assembly
        ] do
        let actual =
            assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            |> Seq.exists (fun attribute ->
                attribute.Key = "RanvierTrace"
                && attribute.Value = "true")

        if actual <> traced then
            failwith $"Trace cohort mismatch: {assembly.FullName}"

    printfn $"OFFICIAL_TRACE_METADATA PASS expected={traced} assemblies=5"
