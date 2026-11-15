/// The later file of the fixture: values and a component that depend on Lib.fs and on each other.
module HmrFixture.App

open Ranvier.View
open HmrFixture.Lib

module private Home =
    let page =
        {
            Title = "Home"
            Body = fun () -> shout "body"
        }

/// A public value computed from a value of another file.
let caption = "caption " + suffix

let private extra =
    {
        Title = "Extra"
        Body = fun () -> "extra"
    }

/// A value of a type of another file.
let tone = Plain

/// A public value computed from a nested module and a private value of this file.
let pages = publish [| Home.page; extra |]

let app: Component<unit> =
    Component.define (fun _ () -> ()) (fun t ->
        t.div
            [||]
            [|
                t.div [| t.Attrs.attribute ("id", "caption") |] [| t.text (fun _ -> caption) |]
                t.div [| t.Attrs.attribute ("id", "home") |] [| t.text (fun _ -> pages[0].Title) |]
                t.div [| t.Attrs.attribute ("id", "extra") |] [| t.text (fun _ -> pages[1].Title) |]
                t.div [| t.Attrs.attribute ("id", "body") |] [| t.text (fun _ -> pages[0].Body ()) |]
                t.div [| t.Attrs.attribute ("id", "tone") |] [| t.text (fun _ -> string tone) |]
            |])
