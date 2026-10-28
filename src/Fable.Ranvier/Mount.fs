namespace Fable.Ranvier

open System
open global.Ranvier.Dom
open global.Ranvier

type DomMount internal (owner: Owner, queue: DomQueue) =
    member _.Flush() =
        if not owner.IsDisposed then
            queue.Flush ()

    member _.Errors = queue.Errors

    member _.Dispose() =
        owner.Dispose ()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

module Mount =
    let mountWith (options: DomOptions) (graph: Graph) (host: Node) (factory: unit -> Node) : DomMount =
        let queue = DomQueue graph

        let context =
            {
                Graph = graph
                Options = options
                Queue = queue
            }

        graph.Run (fun () ->
            let mutable createdOwner: Owner option = None

            try
                let owner =
                    createRoot (fun owner ->
                        createdOwner <- Some owner
                        onCleanup queue.Dispose
                        let root = DomContext.run (Some context) factory

                        if root.nodeType = 11us then
                            raise (
                                ArgumentException ("A mount requires one persistent root node; DocumentFragment roots are unsupported.", "factory")
                            )

                        onCleanup (fun () ->
                            let parent = root.parentNode

                            if not (isNull parent) then
                                parent.removeChild root |> ignore)

                        host.appendChild root |> ignore
                        owner)

                new DomMount (owner, queue)
            with _ ->
                queue.Dispose ()

                createdOwner
                |> Option.iter (fun owner -> owner.Dispose ())

                reraise ())

    let mount (graph: Graph) (host: Node) (factory: unit -> Node) : IDisposable =
        mountWith DomOptions.Default graph host factory :> IDisposable
