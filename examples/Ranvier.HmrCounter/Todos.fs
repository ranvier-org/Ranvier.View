module HmrCounter.Todos

open Ranvier
open Ranvier.Dom
open Ranvier.View

type Todo = { Id: int; Text: string }

type TodosState =
    {
        Items: Signal<Todo array>
        Draft: Signal<string>
        NextId: Signal<int>
    }

let todoRow: Component<unit -> Todo> =
    Component.define (fun _ (read: unit -> Todo) -> read) (fun t ->
        t.li [| t.Attrs.attribute ("class", "todo") |] [| t.text (fun read -> "- " + (read ()).Text) |])

/// HMR_SEED_TODOS=n seeds n rows (the bench's scale runs).
let private seed () =
    match System.Int32.TryParse (System.Environment.GetEnvironmentVariable "HMR_SEED_TODOS") with
    | true, n when n > 1 ->
        Array.init n (fun i ->
            {
                Id = i + 1
                Text = if i = 0 then "Try hot reload" else $"Seeded todo {i + 1}"
            })
    | _ -> [| { Id = 1; Text = "Try hot reload" } |]

let private add (s: TodosState) =
    if s.Draft.Value <> "" then
        s.Items.Value <-
            Array.append
                s.Items.Value
                [|
                    {
                        Id = s.NextId.Value
                        Text = s.Draft.Value
                    }
                |]

        s.NextId.Value <- s.NextId.Value + 1

let todos: Component<unit> =
    Component.define
        (fun _ () ->
            let initial = seed ()

            {
                Items = Api.createSignal initial
                Draft = Api.createSignal ""
                NextId = Api.createSignal (initial.Length + 1)
            })
        (fun t ->
            t.div
                [| t.Attrs.attribute ("id", "todos") |]
                [|
                    t.h2 [||] [| t.text "Todos" |]
                    t.input
                        [|
                            t.Attrs.attribute ("id", "todo-input")
                            t.Attrs.attribute ("placeholder", "New todo")
                            t.Events.on ("input", fun s (e: Event) -> s.Draft.Value <- (unbox<HTMLInputElement> e.target).value)
                        |]
                    t.button
                        [|
                            t.Attrs.attribute ("id", "todo-add")
                            t.Events.on ("click", fun s _ -> add s)
                        |]
                        [| t.text "Add" |]
                    t.ul [| t.Attrs.attribute ("id", "todo-list") |] [| t.each (fun (x: Todo) -> x.Id) (fun s -> s.Items.Value) todoRow |]
                |])
