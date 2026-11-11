module Ranvier.View.WebCore.Tests.DocumentModelTests

open Expecto
open Responsiveness.DocumentModel

[<Tests>]
let tests =
    testList
        "Editor document"
        [
            testCase "snapshots preserve Unicode endings and line identities" (fun () ->
                let original = create "a\r\nb😀\nc"

                let edited, delta =
                    replace original { Line = 1; Column = 1 } { Line = 1; Column = 3 } "日本\nx"

                Expect.equal (read original { Line = 0; Column = 0 } { Line = 2; Column = 1 }) "a\r\nb😀\nc" "old snapshot immutable"
                Expect.equal (read edited { Line = 0; Column = 0 } { Line = 3; Column = 1 }) "a\r\nb日本\nx\nc" "multiline replacement"
                Expect.equal (edited.Line 3).Id (original.Line 2).Id "untouched suffix identity"
                Expect.equal delta { First = 1; Removed = 1; Added = 2 } "changed range"

                Expect.throws
                    (fun () ->
                        replace original { Line = 1; Column = 2 } { Line = 1; Column = 3 } ""
                        |> ignore)
                    "reject split surrogate")
            testCase "undo never reuses a semantic revision" (fun () ->
                let buffer = DocumentBuffer "abc"
                buffer.Replace ({ Line = 0; Column = 1 }, { Line = 0; Column = 2 }, "x")
                let revision = buffer.Snapshot.Revision
                Expect.isTrue (buffer.Undo ()) "undo available"
                Expect.equal (buffer.Snapshot.Line 0).Text "abc" "restored"
                Expect.isGreaterThan buffer.Snapshot.Revision revision "old results remain stale")
            testCase "random edits match a string reference" (fun () ->
                let random = System.Random 42
                let mutable reference = ""
                let mutable snapshot = create reference

                for _ in 1..500 do
                    let start = random.Next (reference.Length + 1)
                    let finish = random.Next (start, reference.Length + 1)
                    let inserted = string (char (int 'a' + random.Next 26))

                    let next, _ =
                        replace snapshot { Line = 0; Column = start } { Line = 0; Column = finish } inserted

                    reference <-
                        reference.Substring (0, start)
                        + inserted
                        + reference.Substring (finish)

                    snapshot <- next
                    Expect.equal (snapshot.Line 0).Text reference "reference edit")
        ]
