module Responsiveness.DocumentModel

open System
open System.Collections.Immutable

type Position = { Line: int; Column: int }

type Line =
    {
        Id: int64
        Revision: int64
        Text: string
        Ending: string
    }

type Change =
    { First: int; Removed: int; Added: int }

type Snapshot =
    private
        {
            Generation: Guid
            Version: int64
            NextId: int64
            Lines: ImmutableList<Line>
        }

    member this.DocumentId = this.Generation
    member this.Revision = this.Version
    member this.LineCount = this.Lines.Count

    member this.Line(index) =
        this.Lines[index]

let private splitLines (text: string) =
    let result = ResizeArray<string * string>()
    let mutable start = 0
    let mutable index = 0

    while index < text.Length do
        if text[index] = '\r' || text[index] = '\n' then
            let size =
                if
                    text[index] = '\r'
                    && index + 1 < text.Length
                    && text[index + 1] = '\n'
                then
                    2
                else
                    1

            result.Add (text.Substring (start, index - start), text.Substring (index, size))
            index <- index + size
            start <- index
        else
            index <- index + 1

    result.Add (text.Substring start, "")
    result.ToArray ()

let create text =
    if isNull text then
        nullArg "text"

    let lines =
        splitLines text
        |> Array.mapi (fun i (value, ending) ->
            {
                Id = int64 i
                Revision = 0L
                Text = value
                Ending = ending
            })

    {
        Generation = Guid.NewGuid ()
        Version = 0L
        NextId = int64 lines.Length
        Lines = ImmutableList.CreateRange lines
    }

let private validate (snapshot: Snapshot) position =
    if
        position.Line < 0
        || position.Line >= snapshot.LineCount
    then
        invalidArg "position" "line outside document"

    let text = snapshot.Line(position.Line).Text

    if
        position.Column < 0
        || position.Column > text.Length
    then
        invalidArg "position" "column outside line"

    if
        position.Column > 0
        && position.Column < text.Length
        && Char.IsHighSurrogate text[position.Column - 1]
        && Char.IsLowSurrogate text[position.Column]
    then
        invalidArg "position" "position splits a surrogate pair"

let read (snapshot: Snapshot) start finish =
    validate snapshot start
    validate snapshot finish

    if (start.Line, start.Column) > (finish.Line, finish.Column) then
        invalidArg "finish" "range is reversed"

    let result = Text.StringBuilder ()

    for index in start.Line .. finish.Line do
        let line = snapshot.Line index
        let first = if index = start.Line then start.Column else 0

        let last =
            if index = finish.Line then
                finish.Column
            else
                line.Text.Length

        result.Append (line.Text, first, last - first)
        |> ignore

        if index < finish.Line then
            result.Append line.Ending |> ignore

    result.ToString ()

let replace (snapshot: Snapshot) start finish inserted =
    validate snapshot start
    validate snapshot finish

    if isNull inserted then
        nullArg "inserted"

    if (start.Line, start.Column) > (finish.Line, finish.Column) then
        invalidArg "finish" "range is reversed"

    let first = snapshot.Line start.Line
    let last = snapshot.Line finish.Line

    let parts =
        splitLines (
            first.Text.Substring (0, start.Column)
            + inserted
            + last.Text.Substring (finish.Column)
        )

    let version = snapshot.Version + 1L
    let mutable next = snapshot.NextId

    let replacement =
        parts
        |> Array.mapi (fun i (text, ending) ->
            let identity =
                if i = 0 then
                    first.Id
                else
                    let value = next in
                    next <- next + 1L
                    value

            {
                Id = identity
                Revision = version
                Text = text
                Ending = if i = parts.Length - 1 then last.Ending else ending
            })

    let removed = finish.Line - start.Line + 1

    { snapshot with
        Version = version
        NextId = next
        Lines = snapshot.Lines.RemoveRange(start.Line, removed).InsertRange(start.Line, replacement)
    },
    {
        First = start.Line
        Removed = removed
        Added = replacement.Length
    }

type DocumentBuffer(text: string) =
    let mutable snapshot = create text
    let mutable undo: (Snapshot * Change) list = []
    let changed = Event<Change>()
    member _.Snapshot = snapshot
    member _.Changed = changed.Publish

    member _.Replace(start, finish, text) =
        let next, change = replace snapshot start finish text
        undo <- (snapshot, change) :: (undo |> List.truncate 63)
        snapshot <- next
        changed.Trigger change

    member _.Undo() =
        match undo with
        | (previous, change) :: rest ->
            // An undo restores text, but must not make an old worker revision current again.
            snapshot <-
                { previous with
                    Version = snapshot.Version + 1L
                    NextId = max previous.NextId snapshot.NextId
                }

            undo <- rest

            changed.Trigger
                {
                    First = change.First
                    Removed = change.Added
                    Added = change.Removed
                }

            true
        | [] -> false
