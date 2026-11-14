/// <summary>The template benchmark row of <c>FrameworkPerf</c> written with the template sugar. Kept apart from <c>FrameworkPerf</c>; bundles of the explicit fixtures hold no sugar code.</summary>
module FrameworkSugarPerf

open global.Ranvier
open Ranvier.Dom
open Ranvier.View
open Ranvier.View.Dsl
open FrameworkPerf

let private perfRowSugar =
    Component.template
        (fun _ (props: PerfProps) ->
            {
                Index = props.Index
                Source = props.Source
                Disabled = props.Disabled
                Local = Api.createSignal 0
            })
        (div (div.id (fun r -> "row-" + string r.Index), div.className "panel", div.title (from (fun r -> r.Source))) {
            input (input.props.value (from (fun r -> r.Source)))

            button (button.props.disabled (from (fun r -> r.Disabled)), button.onClick (fun r _ -> r.Local.Value <- r.Local.Value + 1)) {
                fun r -> string r.Local.Value
            }
        })

/// <summary>Mounts equivalent rows as template component instances written with the template sugar.</summary>
let mountTemplateSugar (host: HTMLElement) width =
    mountCore
        (fun width source disabled ->
            Program.fragment (
                Array.init width (fun index ->
                    Template.Component.render
                        perfRowSugar
                        {
                            Index = index + 1
                            Source = source
                            Disabled = disabled
                        })
            ))
        host
        width
