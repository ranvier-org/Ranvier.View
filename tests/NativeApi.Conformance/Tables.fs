module internal NativeApiConformance.Tables

open Ranvier.Dom

/// <summary>Checks live table editing, reflected properties and invalid indices on the supplied engine document.</summary>
let run (document: Document) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let table = document.createElement ("table") :?> HTMLTableElement
    let rows = table.rows
    equal "initial rows" 0u rows.length
    let body = table.createTBody ()
    let bodyRows = body.rows
    let first = body.insertRow ()
    let cells = first.cells
    equal "live table rows" 1u rows.length
    equal "live section rows" 1u bodyRows.length
    equal "row index" 0 first.rowIndex
    equal "section row index" 0 first.sectionRowIndex
    equal "initial cells" 0u cells.length
    let cell = first.insertCell () :?> HTMLTableCellElement
    equal "live cells" 1u cells.length
    equal "cell index" 0 cell.cellIndex
    cell.colSpan <- 3u
    cell.rowSpan <- 2u
    equal "column span" 3u cell.colSpan
    equal "row span" 2u cell.rowSpan
    let second = table.insertRow ()
    equal "append default" 1 second.rowIndex
    equal "live append" 2u rows.length
    let inserted = table.insertRow (0)
    equal "insert index" 0 inserted.rowIndex
    equal "existing row moves" 1 first.rowIndex
    table.deleteRow (0)
    equal "removed row index" -1 inserted.rowIndex
    equal "existing row restored" 0 first.rowIndex
    first.insertCell (0) |> ignore
    equal "inserted cell moves index" 1 cell.cellIndex
    first.deleteCell (0)
    equal "cell index restored" 0 cell.cellIndex
    first.deleteCell (-1)
    equal "live empty cells" 0u cells.length
    table.createCaption().textContent <- "Table title"
    equal "caption text" "Table title" table.caption.textContent
    table.deleteCaption ()
    equal "caption removed" true (isNull (box table.caption))
    let head = table.createTHead ()
    let foot = table.createTFoot ()
    equal "head tag" "THEAD" head.tagName
    equal "foot tag" "TFOOT" foot.tagName
    table.deleteTHead ()
    table.deleteTFoot ()
    equal "head removed" true (isNull (box table.tHead))
    equal "foot removed" true (isNull (box table.tFoot))
    let mutable rejected = false

    try
        table.insertRow (999) |> ignore
    with _ ->
        rejected <- true

    equal "invalid row rejected" true rejected
    equal "invalid row preserves collection" 2u rows.length
    body.deleteRow (-1)
    equal "section deletion updates table" 1u rows.length
    table.deleteRow (-1)
    equal "all rows removed" 0u rows.length
    let col = document.createElement ("col") :?> HTMLTableColElement
    col.span <- 4u
    equal "column reflection" 4u col.span
    checks
