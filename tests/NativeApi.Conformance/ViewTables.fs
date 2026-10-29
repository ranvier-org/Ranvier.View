module internal NativeApiConformance.ViewTables

open Ranvier
open Ranvier.Dom
open Ranvier.View
open Ranvier.View.Dom

let private definition =
    Component.define (fun _ (_: unit) -> ()) (fun t ->
        t.table
            [| t.Attrs.attribute ("id", "native-api-view-table") |]
            [|
                t.caption [||] [| t.text "View caption" |]
                t.colgroup [||] [| t.col [| t.Attrs.attribute ("span", "2") |] |]
                t.thead [||] [| t.tr [||] [| t.th [||] [| t.text "Header" |] |] |]
                t.tbody [||] [| t.tr [||] [| t.td [||] [| t.text "Cell" |] |] |]
                t.tfoot [||] [| t.tr [||] [| t.td [||] [| t.text "Footer" |] |] |]
            |])

/// <summary>Checks table View tags, native interface casts, live editing and mount disposal.</summary>
/// <param name="document">The live engine document; the mount container is removed before returning.</param>
let run (document: Document) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let container = document.createElement "div" :?> HTMLElement
    document.body.appendChild container |> ignore
    use graph = new Graph ()
    let mutable mounted: Mounting.Mounted option = None

    try
        let result =
            Mounting.mount graph None (fun errors -> failwith $"View table mount failed: {errors}") container None (fun context ->
                Program.run (Component.render definition ()) context.Backend)

        mounted <- Some result
        let table = container.querySelector "table" :?> HTMLTableElement
        equal "View table tag" "TABLE" table.tagName
        equal "View table id" "native-api-view-table" table.id
        equal "View caption" "View caption" table.caption.textContent
        equal "View caption tag" "CAPTION" table.caption.tagName
        equal "View head tag" "THEAD" table.tHead.tagName
        equal "View foot tag" "TFOOT" table.tFoot.tagName
        equal "View body count" 1u table.tBodies.length
        equal "View table row count" 3u table.rows.length
        let header = table.tHead.rows.item 0u :?> HTMLTableRowElement
        let body = table.tBodies.item 0u :?> HTMLTableSectionElement
        let row = body.rows.item 0u :?> HTMLTableRowElement
        let footer = table.tFoot.rows.item 0u :?> HTMLTableRowElement
        equal "View header index" 0 header.rowIndex
        equal "View body index" 1 row.rowIndex
        equal "View footer index" 2 footer.rowIndex
        equal "View section row index" 0 row.sectionRowIndex
        let heading = header.cells.item 0u :?> HTMLTableCellElement
        let cell = row.cells.item 0u :?> HTMLTableCellElement
        equal "View header cell tag" "TH" heading.tagName
        equal "View cell tag" "TD" cell.tagName
        equal "View cell text" "Cell" cell.textContent
        cell.colSpan <- 2u
        cell.rowSpan <- 3u
        equal "View cell column span" 2u cell.colSpan
        equal "View cell column attribute" "2" (cell.getAttribute "colspan")
        equal "View cell row span" 3u cell.rowSpan
        equal "View cell row attribute" "3" (cell.getAttribute "rowspan")
        let colgroup = table.querySelector "colgroup" :?> HTMLTableColElement
        let col = colgroup.querySelector "col" :?> HTMLTableColElement
        equal "View colgroup tag" "COLGROUP" colgroup.tagName
        equal "View column tag" "COL" col.tagName
        equal "View column span" 2u col.span
        let rows = table.rows
        let inserted = body.insertRow ()
        inserted.insertCell().textContent <- "Inserted"
        equal "View live row insertion" 4u rows.length
        equal "View inserted row index" 2 inserted.rowIndex
        equal "View moved footer index" 3 footer.rowIndex
        body.deleteRow (-1)
        equal "View live row deletion" 3u rows.length
        equal "View restored footer index" 2 footer.rowIndex
        result.Dispose ()
        equal "View mount disposal" true (isNull (box (container.querySelector "table")))
        equal "View disposal removes markers" 0u container.childNodes.length
        mounted <- None
    finally
        mounted
        |> Option.iter (fun current -> current.Dispose ())

        document.body.removeChild container |> ignore

    checks
