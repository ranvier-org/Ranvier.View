/// <summary>The story page without its Motion parts: the server render of the story must equal this twin's.</summary>
module StoryTwin

open global.Ranvier
open Ranvier.View
open Ranvier.View.Dsl
open Story

let private percent (value: float) =
    sprintf "%.0f%%" (value * 100.)

let view: Component<Story -> unit> =
    Component.define (fun _ (_: Story -> unit) -> create ()) (fun t ->
        frame
            t
            (article (article.className "story") {
                header (header.className "hero") {
                    p (p.className "eyebrow") { "Rendered on .NET · hydrated in the browser" }
                    h1 (h1.className "headline") { "Motion, owned by the view." }

                    p (p.className "lede") {
                        "Scroll down. Each section below runs an anime.js timeline scrubbed by the scroll, and writes how far it has come into a signal."
                    }

                    div (div.className "cue", div.aria ("hidden", "true")) { "↓" }
                }

                nav (nav.className "rail", nav.aria ("label", "Progress")) {
                    for index in 0 .. chapters.Length - 1 do
                        div (
                            div.className (fun (s: Story) ->
                                if s.Chapters[index].Seen.Value then
                                    "rail-item seen"
                                else
                                    "rail-item"),
                            div.attribute ("style", fun (s: Story) -> Some (sprintf "--p:%.3f" s.Chapters[index].Progress.Value))
                        ) {
                            span (span.className "rail-mark") { chapters[index].Mark }
                            span (span.className "rail-title") { chapters[index].Title }
                            span (span.className "rail-value") { fun (s: Story) -> percent s.Chapters[index].Progress.Value }
                        }
                }

                for index in 0 .. chapters.Length - 1 do
                    section (section.className (if index % 2 = 0 then "chapter" else "chapter flipped"), section.data ("chapter", string index)) {
                        div (div.className "chapter-copy") {
                            span (span.className "chapter-mark") { chapters[index].Mark }
                            h2 () { chapters[index].Title }
                            p () { chapters[index].Text }
                        }

                        div (div.className "chapter-art", div.aria ("hidden", "true")) {
                            div (div.className "orb") { () }
                            div (div.className "ring") { () }
                        }
                    }

                section (section.className "gallery") {
                    h2 () { "Twelve parts, one stagger" }
                    p (p.className "gallery-note") { fun (s: Story) -> "Gallery " + percent s.Gallery.Progress.Value }

                    div (div.className "tiles") {
                        for label in tiles do
                            div (div.className "tile") { label }
                    }
                }

                section (section.className "more") {
                    h2 () { "Four more, created on the client" }

                    button (button.className "primary more-toggle", button.onClick (fun (s: Story) _ -> s.More.Value <- not s.More.Value)) {
                        fun (s: Story) ->
                            if s.More.Value then
                                "Hide the extras"
                            else
                                "Show the extras"
                    }

                    show
                        (fun (s: Story) -> s.More.Value)
                        (div (div.className "tiles extras") {
                            forEachFixed id (fun (_: Story) -> extras) (div (div.className "tile extra") { fun (label: string) -> label })
                        })
                }

                footer (footer.className "story-end") { "Built with Ranvier and anime.js." }
            }))
