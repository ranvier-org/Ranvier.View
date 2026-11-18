// Randomized program generator + F# runner for Ranvier.View differential conformance.
#load "../conformance/Harness.fsx"

open System
open System.Collections.Generic
open System.IO
open Harness

type Rng(seed: int) =
    let mutable state =
        uint64 (if seed = 0 then 1 else seed)
        * 6364136223846793005UL
        + 1UL

    member _.NextU64() =
        let mutable x = state
        x <- x ^^^ (x >>> 12)
        x <- x ^^^ (x <<< 25)
        x <- x ^^^ (x >>> 27)
        state <- x
        x * 2685821657736338717UL

    member this.Next(bound: int) =
        if bound <= 1 then
            0
        else
            int (this.NextU64 () % uint64 bound)

    member this.Bool() =
        this.Next 2 = 1

    member this.Shuffle(xs: 'a array) =
        let a = Array.copy xs

        for i = a.Length - 1 downto 1 do
            let j = this.Next (i + 1)
            let tmp = a[i]
            a[i] <- a[j]
            a[j] <- tmp

        a

let fmtIntListSexp (xs: seq<int>) =
    "("
    + String.Join (" ", xs |> Seq.map string)
    + ")"

let genLayout (rng: Rng) : string =
    let mutable fresh = 10
    let initLen = rng.Next 6

    let initRows =
        [
            for _ in 1..initLen ->
                fresh <- fresh + 1
                fresh
        ]

    let mutable cur = initRows
    let leaving = ResizeArray<int>()
    let hasExit = HashSet<int>()
    let mutable rfTickets = 0
    let steps = 2 + rng.Next 7
    let cmds = ResizeArray<string>()

    for _ in 1..steps do
        match rng.Next 6 with
        | 0 when not cur.IsEmpty ->
            let r = cur[rng.Next cur.Length]
            hasExit.Add r |> ignore
            cmds.Add (sprintf "(reg-exit %d)" r)
        | 1 when leaving.Count > 0 ->
            let idx = rng.Next leaving.Count
            let r = leaving[idx]
            leaving.RemoveAt idx
            cmds.Add (sprintf "(fin-exit %d)" r)
        | 2 ->
            rfTickets <- rfTickets + 1
            cmds.Add "(rf-before)"
        | 3 when rfTickets > 0 ->
            if rng.Bool () then
                cmds.Add (sprintf "(rf-post %d)" (rng.Next rfTickets))
            else
                cmds.Add (sprintf "(rf-cancel %d)" (rng.Next rfTickets))
        | _ ->
            let next =
                match rng.Next 4 with
                | 0 -> []
                | 1 when cur.Length >= 2 ->
                    // Outer swap candidate
                    let arr = List.toArray cur
                    let tmp = arr[0]
                    arr[0] <- arr[arr.Length - 1]
                    arr[arr.Length - 1] <- tmp
                    Array.toList arr
                | _ ->
                    let surviving =
                        cur
                        |> List.filter (fun _ -> rng.Next 4 <> 0)
                        |> List.toArray
                        |> rng.Shuffle
                        |> Array.toList

                    let addedCount = rng.Next 3

                    let added =
                        [
                            for _ in 1..addedCount ->
                                fresh <- fresh + 1
                                fresh
                        ]

                    let splitAt = rng.Next (surviving.Length + 1)
                    let pre, suf = List.splitAt splitAt surviving
                    pre @ added @ suf

            for r in cur do
                if not (List.contains r next) && hasExit.Remove r then
                    leaving.Add r

            cur <- next
            cmds.Add (sprintf "(reconcile %s)" (fmtIntListSexp next))

    sprintf "(layout %s %s)" (fmtIntListSexp initRows) (String.Join (" ", cmds))

let rec genSsrTree (rng: Rng) (depth: int) (nextId: int ref) : string =
    if depth >= 3 || (depth > 0 && rng.Bool ()) then
        nextId.Value <- nextId.Value + 1
        sprintf "(node %d)" nextId.Value
    else
        let tag = 1 + rng.Next 4
        let n = rng.Next 4
        let children = [ for _ in 1..n -> genSsrTree rng (depth + 1) nextId ]

        if children.IsEmpty then
            sprintf "(reg %d)" tag
        else
            sprintf "(reg %d %s)" tag (String.Join (" ", children))

let genSsr (rng: Rng) : string =
    let idRef = ref 0
    // Always start with a region at root so logicalNext scans nested brackets
    let tag = 1 + rng.Next 4
    let n = rng.Next 4
    let children = [ for _ in 1..n -> genSsrTree rng 1 idRef ]

    let root =
        if children.IsEmpty then
            sprintf "(reg %d)" tag
        else
            sprintf "(reg %d %s)" tag (String.Join (" ", children))

    let sufLen = rng.Next 4

    let suf =
        [
            for _ in 1..sufLen ->
                idRef.Value <- idRef.Value + 1
                idRef.Value
        ]

    sprintf "(ssr %s %s)" root (fmtIntListSexp suf)

let rec genHydOps (rng: Rng) (depth: int) : string list =
    let n = 1 + rng.Next 3

    [
        for _ in 1..n ->
            if depth < 2 && rng.Next 3 = 0 then
                let sub = genHydOps rng (depth + 1)
                sprintf "(scope %s)" (String.Join (" ", sub))
            else
                let delta = (rng.Next 41) - 20
                sprintf "(add %d)" delta
    ]

let genCursor (rng: Rng) : string =
    let s0 = rng.Next 100
    let commit = if rng.Next 3 = 0 then 1 else 0
    let ops = genHydOps rng 0
    sprintf "(cursor %d %d %s)" s0 commit (String.Join (" ", ops))

let genBoundary (rng: Rng) : string =
    let tracks = if rng.Next 5 = 0 then 0 else 1
    let mutable slots = 0
    let mutable maxGen = 0
    let cmds = ResizeArray<string>()
    let n = 3 + rng.Next 10

    for _ in 1..n do
        match rng.Next 7 with
        | 0
        | 1 ->
            slots <- slots + 1
            cmds.Add "(expect)"
        | 2 when slots > 0 -> cmds.Add (sprintf "(step %d val)" (rng.Next slots))
        | 3 when slots > 0 -> cmds.Add (sprintf "(step %d pending)" (rng.Next slots))
        | 4 when slots > 0 ->
            maxGen <- maxGen + 1
            cmds.Add (sprintf "(step %d err)" (rng.Next slots))
        | 5 when slots > 0 -> cmds.Add (sprintf "(dispose %d)" (rng.Next slots))
        | 6 ->
            match rng.Next 3 with
            | 0 -> cmds.Add "(end-setup)"
            | 1 -> cmds.Add "(reveal)"
            | _ ->
                let g = rng.Next (maxGen + 2)

                if g > 0 then
                    slots <- 0

                cmds.Add (sprintf "(retry %d)" g)
        | _ -> cmds.Add "(end-setup)"

    sprintf "(boundary %d %s)" tracks (String.Join (" ", cmds))

let genPlacement (rng: Rng) : string =
    let parentPlaced = if rng.Bool () then 1 else 0
    let mutable depth = 0
    let mutable nextHook = 1
    let cmds = ResizeArray<string>()
    let n = 4 + rng.Next 10

    for _ in 1..n do
        match rng.Next 7 with
        | 0
        | 1 ->
            depth <- depth + 1
            cmds.Add "(enter)"
        | 2 when depth > 0 ->
            depth <- depth - 1
            cmds.Add "(leave)"
        | 3
        | 4 ->
            let h = nextHook
            nextHook <- nextHook + 1
            cmds.Add (sprintf "(add %d)" h)
        | 5 ->
            let h = nextHook
            nextHook <- nextHook + 1
            cmds.Add (sprintf "(rel-child %d)" h)
        | _ ->
            if rng.Next 3 = 0 then
                cmds.Add "(discard)"
            else
                cmds.Add "(place)"

    if rng.Bool () then
        cmds.Add "(place)"

    sprintf "(placement %d %s)" parentPlaced (String.Join (" ", cmds))

let rec genLtNode (rng: Rng) (depth: int) (nextId: int ref) (nextCl: int ref) : string =
    nextId.Value <- nextId.Value + 1
    let id = nextId.Value
    let clCount = rng.Next 3

    let cls =
        [
            for _ in 1..clCount ->
                nextCl.Value <- nextCl.Value + 1
                nextCl.Value
        ]

    let childCount = if depth >= 2 then 0 else rng.Next 3
    let children = [ for _ in 1..childCount -> genLtNode rng (depth + 1) nextId nextCl ]

    if children.IsEmpty then
        sprintf "(lt %d %s)" id (fmtIntListSexp cls)
    else
        sprintf "(lt %d %s %s)" id (fmtIntListSexp cls) (String.Join (" ", children))

let genLifetime (rng: Rng) : string =
    let idRef = ref 0
    let clRef = ref 0
    sprintf "(lifetime %s)" (genLtNode rng 0 idRef clRef)

let genWindow (rng: Rng) : string =
    let start = rng.Next 5
    let len = 1 + rng.Next 12
    let ivCount = rng.Next 4
    let mutable cursor = start
    let ivs = ResizeArray<string>()

    for _ in 1..ivCount do
        if cursor < start + len then
            let s = cursor + rng.Next 2
            let f = min (start + len) (s + rng.Next 4)

            if s <= f then
                ivs.Add (sprintf "(%d %d)" s f)
                cursor <- f

    if ivs.Count = 0 then
        sprintf "(window %d %d)" start len
    else
        sprintf "(window %d %d %s)" start len (String.Join (" ", ivs))

let genQueue (rng: Rng) : string =
    let mutable slotCount = 0
    let mutable nextAct = 1
    let mutable ticketUpper = 0
    let cmds = ResizeArray<string>()
    let n = 4 + rng.Next 10

    for _ in 1..n do
        match rng.Next 8 with
        | 0
        | 1 ->
            let sc = rng.Next 4
            let hd = if rng.Bool () then 1 else 0
            slotCount <- slotCount + 1
            cmds.Add (sprintf "(reserve %d %d)" sc hd)
        | 2
        | 3
        | 4 when slotCount > 0 ->
            let idx = rng.Next slotCount
            let act = nextAct
            nextAct <- nextAct + 1
            ticketUpper <- ticketUpper + 1
            cmds.Add (sprintf "(enqueue %d %d)" idx act)
        | 5 when slotCount > 0 -> cmds.Add (sprintf "(cancel %d)" (rng.Next slotCount))
        | 6 ->
            let mode = if rng.Bool () then "collect" else "failfast"

            let failing =
                [
                    for a in 1 .. nextAct - 1 do
                        if rng.Next 4 = 0 then
                            a
                ]

            if rng.Next 3 = 0 && slotCount > 0 && nextAct > 1 then
                let trig = 1 + rng.Next (nextAct - 1)
                let tgt = rng.Next slotCount
                let na = nextAct
                nextAct <- nextAct + 1
                ticketUpper <- ticketUpper + 1

                cmds.Add (sprintf "(flush-reenq %s %s ((%d %d %d)))" mode (fmtIntListSexp failing) trig tgt na)
            elif rng.Bool () && ticketUpper > 0 then
                cmds.Add (sprintf "(ticket %d %s %s)" (rng.Next ticketUpper) mode (fmtIntListSexp failing))
            else
                cmds.Add (sprintf "(flush %s %s)" mode (fmtIntListSexp failing))
        | _ ->
            if rng.Next 5 = 0 then
                cmds.Add "(dispose)"
            elif slotCount > 0 then
                let idx = rng.Next slotCount
                let act = nextAct
                nextAct <- nextAct + 1
                ticketUpper <- ticketUpper + 1
                cmds.Add (sprintf "(enqueue %d %d)" idx act)
            else
                slotCount <- slotCount + 1
                cmds.Add "(reserve 1 1)"

    sprintf "(queue %s)" (String.Join (" ", cmds))

let genOwq (rng: Rng) : string =
    let cap = 1 + rng.Next 4
    let mutable nextItem = 1
    let cmds = ResizeArray<string>()
    let n = 3 + rng.Next 9

    for _ in 1..n do
        match rng.Next 6 with
        | 0
        | 1 ->
            let it = nextItem
            nextItem <- nextItem + 1
            cmds.Add (sprintf "(enq %d %d)" it cap)
        | 2 -> cmds.Add "(deq)"
        | 3
        | 4 -> cmds.Add (sprintf "(wake %d)" (if rng.Bool () then 1 else 0))
        | _ ->
            if rng.Next 4 = 0 then
                cmds.Add "(stop)"
            else
                cmds.Add "(deq)"

    sprintf "(owq %s)" (String.Join (" ", cmds))

let genFence (rng: Rng) : string =
    let cmds = ResizeArray<string>()
    let n = 2 + rng.Next 8

    for _ in 1..n do
        match rng.Next 5 with
        | 0
        | 1 -> cmds.Add "(try not-ready)"
        | 2 -> cmds.Add "(try ready)"
        | 3 -> cmds.Add "(try gpu-fail)"
        | _ -> cmds.Add "(reset)"

    sprintf "(fence %s)" (String.Join (" ", cmds))

let families: (string * (Rng -> string)) array =
    [|
        "layout", genLayout
        "ssr", genSsr
        "cursor", genCursor
        "boundary", genBoundary
        "placement", genPlacement
        "lifetime", genLifetime
        "window", genWindow
        "queue", genQueue
        "owq", genOwq
        "fence", genFence
    |]

let argv = fsi.CommandLineArgs[1..]
let count = if argv.Length >= 1 then int argv[0] else 400
let seed = if argv.Length >= 2 then int argv[1] else 1
let workDir = if argv.Length >= 3 then argv[2] else "."

let rng = Rng seed
let programs = Array.zeroCreate<string> count
let counts = Dictionary<string, int>()

for name, _ in families do
    counts[name] <- 0

for i = 0 to count - 1 do
    let name, gen = families[i % families.Length]
    counts[name] <- counts[name] + 1
    programs[i] <- gen rng

File.WriteAllLines (Path.Combine (workDir, "programs.txt"), programs)

do
    use writer = new StreamWriter (Path.Combine (workDir, "fsharp.txt"), false)

    for i = 0 to count - 1 do
        let obs = evalSexpFSharp (parseSexp programs[i])
        writer.WriteLine (sprintf "%d\tok\t%s" i obs)

let summary =
    families
    |> Array.map (fun (k, _) -> sprintf "%s=%d" k counts[k])
    |> String.concat " "

Console.WriteLine (sprintf "generated %d programs (seed %d): %s" count seed summary)
