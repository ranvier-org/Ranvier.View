module Docs.Site

open System.IO
open Feliz.ViewEngine
open Nacara.Core
open Nacara.Plugins
open Nacara.Theme

let versions = [ SiteVersion.root "1.0" ]

let theme =
    Theme.defaults
    |> Theme.navbar
        [
            NavbarSection ("Guide", "guide", "/guide/introduction/")
            NavbarSection ("Views", "view", "/view/")
            NavbarSection ("Server", "server", "/server/")
            NavbarSection ("Analyzers", "analyzers", "/analyzers/")
            NavbarSection ("Examples", "examples", "/examples/")
        ]
    |> Theme.navbarEnd
        [
            NavbarDynamicWidget (Versions.switcher (Versions.versions versions Versions.defaults))
            NavbarIcon ("GitHub", "https://github.com/ranvier-org/Ranvier.View", Icons.github)
        ]
    |> Theme.editUrl "https://github.com/ranvier-org/Ranvier.View/edit/main/docs"
    |> Theme.footer (Html.p [ Html.text "Built with Nacara" ])

/// <summary>Copies the built browser examples into the documentation output under examples/.</summary>
let private browserExamples =
    Site.plugin
        { new IPlugin with
            member _.Name = "browser-examples"

            member _.Configure registry =
                [
                    "Ranvier.Motion", "motion"
                    "Fable.Ranvier.Playground", "playground"
                    "Ranvier.Hydration", "hydration"
                ]
                |> List.fold
                    (fun assets (project, route) ->
                        let dist =
                            Path.Combine (__SOURCE_DIRECTORY__, $"../examples/{project}/dist")
                            |> Path.GetFullPath

                        assets
                        |> Registry.asset (Asset.CopyDirectory (AbsolutePath.create dist, RelativePath.create $"examples/{route}")))
                    registry
        }

let site =
    Site.create "Ranvier.View"
    |> Site.baseUrl "/Ranvier.View/"
    |> Site.origin "https://ranvier-org.github.io"
    |> Site.output "output"
    |> Site.staticFiles "static"
    |> Markdown.register
    |> TreeSitter.register
    |> Literate.register
    |> Sitemap.register
    |> LinkValidator.register
    |> browserExamples
    |> LightningCss.register
    |> Esbuild.register
    |> Nuglify.minifyHtml
    |> Versions.register versions
    |> GitHubPages.register
    |> Theme.register theme
    |> Site.collection (Theme.docs theme "content")

[<EntryPoint>]
let main argv =
    Nacara.run site argv
