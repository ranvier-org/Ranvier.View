module internal NativeApiConformance.R07Wait

open System
open System.Diagnostics
open System.Threading.Tasks

let run (timeoutMilliseconds: int64) (cycle: unit -> unit) (task: Task<'T>) : 'T =
    let clock = Stopwatch.StartNew ()

    while not task.IsCompleted
          && clock.ElapsedMilliseconds < timeoutMilliseconds do
        cycle ()

    if not task.IsCompleted then
        raise (TimeoutException "The canvas export fixture did not complete before its deadline")

    task.GetAwaiter().GetResult()
