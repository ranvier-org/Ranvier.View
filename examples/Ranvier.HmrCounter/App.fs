module HmrCounter.App

open Ranvier.View

let app: Component<unit> =
    Component.define (fun _ () -> ()) (fun t ->
        t.div
            [|
                t.Attrs.attribute ("style", "font-family:sans-serif;padding:16px;display:flex;flex-direction:column;gap:12px")
            |]
            [|
                t.h1 [| t.Attrs.attribute ("id", "app-title") |] [| t.text "Ranvier HMR" |]
                t.div
                    [| t.Attrs.attribute ("id", "badges") |]
                    [|
                        t.render Shared.badge (fun _ -> "alpha")
                        t.render Shared.badge (fun _ -> "beta")
                    |]
                t.render Counter.counter (fun _ -> "main")
                t.render Todos.todos (fun _ -> ())
                t.render Edge.edge (fun _ -> ())
            |])
