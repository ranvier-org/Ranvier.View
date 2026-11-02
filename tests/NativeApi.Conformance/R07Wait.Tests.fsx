#load "R07Wait.fs"

open System
open System.Threading.Tasks
open NativeApiConformance

let pending = TaskCompletionSource<int>()
let mutable timedOut = false

try
    R07Wait.run 0L ignore pending.Task |> ignore
with :? TimeoutException ->
    timedOut <- true

if not timedOut then
    failwith "an unfinished task must time out without waiting for its result"

let ready = Task.FromResult 40

if R07Wait.run 0L ignore ready <> 40 then
    failwith "a completed task must return its value"

let afterCycle = TaskCompletionSource<int>()

let pump () =
    afterCycle.TrySetResult 7 |> ignore

if R07Wait.run 1000L pump afterCycle.Task <> 7 then
    failwith "the task must complete while the host is pumped"

let fault = Task.FromException<int>(InvalidOperationException "provider failed")
let mutable observedFault = false

try
    R07Wait.run 0L ignore fault |> ignore
with :? InvalidOperationException as error when error.Message = "provider failed" ->
    observedFault <- true

if not observedFault then
    failwith "a completed task must retain its provider exception"

printfn "R07 wait controls passed"
