module Ranvier.View.Dom.Microtask

open Ranvier.View
open Ranvier.View.Dom.Scheduling

let private post callback =
    DomRuntime.queueMicrotask callback

let create graph failure =
    Queue (graph, failure, post)

let mode failure =
    QueuedCommits (failure, post)
