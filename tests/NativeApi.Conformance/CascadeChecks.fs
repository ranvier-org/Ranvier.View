module internal NativeApiConformance.CascadeChecks

open Ranvier.Dom

/// <summary>The engine whose cascade diagnostics the fixture expects.</summary>
type Engine =
    /// <summary>The native host: contributors, origins, layers, positions and the winner.</summary>
    | Native
    /// <summary>Chromium: computed values only; cascade provenance is Unsupported.</summary>
    | Chromium

let private Css =
    "@layer cx-one, cx-two;"
    + "@property --cx-reg{syntax:'<integer>';inherits:false;initial-value:0}"
    + "#cx-spec{order:1}.cx-s0.cx-s1{order:2}.cx-s0{order:3}"
    + ".cx-src{order:4}"
    + ":where(#cx-wh){order:7}.cx-wh{order:6}"
    + ".cx-imp{order:8 !important}#cx-imp{order:9}"
    + "@layer cx-two{#cx-lay{order:10}}@layer cx-one{#cx-lay{order:11}}.cx-lay{order:12}"
    + "@layer cx-one{#cx-limp{order:13 !important}}@layer cx-two{#cx-limp{order:14 !important}}#cx-limp{order:15 !important}"
    + "@layer cx-two{#cx-anon{order:16}}@layer{#cx-anon{order:17}}"
    + "@layer cx-one{@layer inner{#cx-nest{order:32}}#cx-nest{order:33}}"
    + "#cx-inl{order:18}#cx-inl2{order:20 !important}"
    + ".cx-uap{margin-top:3px}"
    + "#cx-sh{margin:7px 1px}"
    + "#cx-vt{writing-mode:vertical-rl;margin-top:70px;margin-inline-start:71px}"
    + ".cx-cv{--cx-x:42}#cx-cv{--cx-x:41}"
    + "#cx-rn{--cx-reg:44}"
    + "#cx-ch1{order:50}#cx-ch2{order:inherit}#cx-ch3{order:inherit}"
    + "#cx-u1{tab-size:5}#cx-u2{tab-size:unset}"
    + "#cx-var{--cx-o:60;order:var(--cx-o)}"
    + "@media (min-width: 1px){@supports (display: grid){#cx-cond{order:61}}}"
    + "@media (max-width: 1px){#cx-cond{order:99}}"
    + ".cx-src{order:5}"

/// <summary>An expected non-user-agent declaration.</summary>
type private Expected =
    {
        Origin: CascadeOrigin
        Value: string
        Winner: bool
        Important: bool
        Layer: string option
        Depth: int
        Declared: string option
        Shorthand: string option
        Variable: bool
        Conditions: string list
    }

let private author value =
    {
        Origin = CascadeOrigin.Author
        Value = value
        Winner = false
        Important = false
        Layer = None
        Depth = 0
        Declared = None
        Shorthand = None
        Variable = false
        Conditions = []
    }

let private wins (e: Expected) = { e with Winner = true }
let private important (e: Expected) = { e with Important = true }
let private layer name (e: Expected) = { e with Layer = Some name }
let private depth n (e: Expected) = { e with Depth = n }

/// <summary>A case: element, property, the computed value every engine reports, whether user-agent declarations lead,
/// and the expected declarations after them.</summary>
type private Case =
    {
        Name: string
        Element: string
        Property: string
        Computed: Choice<float, string>
        UserAgent: bool
        Declarations: Expected list
    }

let private case name element property (computed: float) declarations =
    {
        Name = name
        Element = element
        Property = property
        Computed = Choice1Of2 computed
        UserAgent = false
        Declarations = declarations
    }

let private Cases =
    [
        case "specificity" "cx-spec" "order" 1.0 [ author "3"; author "2"; wins (author "1") ]
        case "source order" "cx-src" "order" 5.0 [ author "4"; wins (author "5") ]
        case "specificity over source order" "cx-wh" "order" 6.0 [ author "7"; wins (author "6") ]
        case "important over specificity" "cx-imp" "order" 8.0 [ wins (important (author "8")); author "9" ]
        case "unlayered over layers" "cx-lay" "order" 12.0 [ layer "cx-one" (author "11"); layer "cx-two" (author "10"); wins (author "12") ]
        case
            "important reverses layer order"
            "cx-limp"
            "order"
            13.0
            [
                wins (important (layer "cx-one" (author "13")))
                important (layer "cx-two" (author "14"))
                important (author "15")
            ]
        case "anonymous layer after named" "cx-anon" "order" 17.0 [ layer "cx-two" (author "16"); wins (layer "" (author "17")) ]
        case "sublayer before its parent" "cx-nest" "order" 33.0 [ layer "cx-one.inner" (author "32"); wins (layer "cx-one" (author "33")) ]
        case
            "inline over author"
            "cx-inl"
            "order"
            19.0
            [
                author "18"
                wins
                    { author "19" with
                        Origin = CascadeOrigin.Inline
                    }
            ]
        case
            "author important over inline"
            "cx-inl2"
            "order"
            20.0
            [
                wins (important (author "20"))
                { author "21" with
                    Origin = CascadeOrigin.Inline
                }
            ]
        { (case "author over user agent" "cx-uap" "margin-top" 3.0 [ wins (author "3px") ]) with
            UserAgent = true
        }
        case
            "presentational hint"
            "cx-hint"
            "width"
            30.0
            [
                wins
                    { author "30px" with
                        Origin = CascadeOrigin.PresentationalHint
                    }
            ]
        case "shorthand longhand" "cx-sh" "margin-top" 7.0 [ wins { author "7px" with Shorthand = Some "margin" } ]
        case
            "vertical logical over physical"
            "cx-vt"
            "margin-top"
            71.0
            [
                author "70px"
                wins
                    { author "71px" with
                        Declared = Some "margin-inline-start"
                    }
            ]
        case "custom property" "cx-cv" "--cx-x" 41.0 [ author "42"; wins (author "41") ]
        case "unregistered custom property inherits" "cx-cvkid" "--cx-x" 41.0 [ depth 1 (author "42"); wins (depth 1 (author "41")) ]
        case "registered inherits false" "cx-rnkid" "--cx-reg" 0.0 []
        case "inherit chain" "cx-ch3" "order" 50.0 [ wins (author "inherit"); wins (depth 1 (author "inherit")); wins (depth 2 (author "50")) ]
        case "unset inherited chain" "cx-u3" "tab-size" 5.0 [ wins (depth 1 (author "unset")); wins (depth 2 (author "5")) ]
        case "no declaration" "cx-plain" "order" 0.0 []
        case "var reference" "cx-var" "order" 60.0 [ wins { author "var(--cx-o)" with Variable = true } ]
        case "media and supports" "cx-cond" "order" 61.0 [ wins { author "61" with Conditions = [ "@media"; "@supports" ] } ]
    ]

/// <summary>
/// Builds the cascade fixture and checks, on every engine, the computed value of each case and the service's
/// validation; on the native engine also the declarations, origins, layers, positions and winner of each case, and on
/// Chromium that the cascade reads as Unsupported. The result is the assertion count.
/// </summary>
let run (create: unit -> DiagnosticsService) (styles: ComputedStyleService) (document: Document) (engine: Engine) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith $"{label}: expected {expected}, got {actual}"

    // Providers raise ArgumentNullException, ArgumentException and ObjectDisposedException; Fable erases the types.
    let raises label (action: unit -> unit) =
        checks <- checks + 1

        let raised =
            try
                action ()
                false
            with _ ->
                true

        if not raised then
            failwith $"{label}: expected an exception"

    let matches (expected: Choice<float, string>) (actual: string) =
        match expected with
        | Choice1Of2 number ->
            let text = actual.Trim ()

            let digits =
                text
                |> Seq.takeWhile (fun c -> System.Char.IsDigit c || c = '.' || c = '-')
                |> Seq.map string
                |> String.concat ""

            digits.Length > 0 && float digits = number
        | Choice2Of2 keyword -> actual.Trim () = keyword

    let created = ResizeArray<Element> ()

    let element (tag: string) (id: string) (parent: Element) =
        let value = document.createElement tag
        value.id <- id
        parent.appendChild value |> ignore
        value

    let body = document.body :> Element
    let style = element "style" "cx-style" body
    style.textContent <- Css
    created.Add style

    let top tag id =
        let value = element tag id body
        created.Add value
        value

    let classed tag id (name: string) =
        let value = top tag id
        value.className <- name
        value

    classed "div" "cx-spec" "cx-s0 cx-s1" |> ignore
    classed "div" "cx-src" "cx-src" |> ignore
    classed "div" "cx-wh" "cx-wh" |> ignore
    classed "div" "cx-imp" "cx-imp" |> ignore
    classed "div" "cx-lay" "cx-lay" |> ignore

    for id in [ "cx-limp"; "cx-anon"; "cx-nest"; "cx-sh"; "cx-plain"; "cx-var"; "cx-cond" ] do
        top "div" id |> ignore

    (top "div" "cx-inl").setAttribute ("style", "order:19")
    (top "div" "cx-inl2").setAttribute ("style", "order:21")
    (classed "p" "cx-uap" "cx-uap").textContent <- "P"
    (top "hr" "cx-hint").setAttribute ("width", "30")
    (top "div" "cx-vt").textContent <- "V"
    element "div" "cx-cvkid" (classed "div" "cx-cv" "cx-cv") |> ignore
    element "div" "cx-rnkid" (top "div" "cx-rn") |> ignore
    element "div" "cx-ch3" (element "div" "cx-ch2" (top "div" "cx-ch1")) |> ignore
    element "div" "cx-u3" (element "div" "cx-u2" (top "div" "cx-u1")) |> ignore
    let detached = document.createElement "div"

    let byId (id: string) = document.getElementById id

    let service = create ()

    try
        // Computed values agree with the fixture on every engine.
        for item in Cases do
            let value = (styles.Get (byId item.Element, None)).getPropertyValue item.Property
            equal (item.Name + " computed") true (matches item.Computed value)

        let spec = byId "cx-spec"
        equal "cascade disabled" (Error DiagnosticsUnavailable.Disabled) (service.Cascade (spec, "order") |> Result.map ignore)
        raises "null property" (fun () -> service.Cascade (spec, null) |> ignore)
        raises "empty property" (fun () -> service.Cascade (spec, "") |> ignore)
        raises "null element" (fun () -> service.Cascade (Unchecked.defaultof<Element>, "order") |> ignore)
        service.Enable ()

        match engine with
        | Chromium ->
            equal "cascade unsupported" true (service.Features.Cascade <> DiagnosticsAvailability.Supported)

            for item in Cases do
                equal (item.Name + " unsupported") true (service.Cascade (byId item.Element, item.Property) |> Result.isError)
        | Native ->
            equal "cascade supported" DiagnosticsAvailability.Supported service.Features.Cascade
            equal "shorthand" (Error DiagnosticsUnavailable.UnsupportedProperty) (service.Cascade (spec, "margin") |> Result.map ignore)
            equal "unknown property" (Error DiagnosticsUnavailable.UnsupportedProperty) (service.Cascade (spec, "cx-nope") |> Result.map ignore)
            equal "detached element" (Error DiagnosticsUnavailable.NotRendered) (service.Cascade (detached, "order") |> Result.map ignore)

            for item in Cases do
                match service.Cascade (byId item.Element, item.Property) with
                | Error reason -> failwith $"{item.Name}: {reason}"
                | Ok cascade ->
                    equal (item.Name + " property") item.Property cascade.Property

                    let userAgent =
                        cascade.Declarations
                        |> Array.takeWhile (fun d -> d.Origin = CascadeOrigin.UserAgent)

                    let rest = cascade.Declarations[userAgent.Length ..]
                    equal (item.Name + " user-agent entries") item.UserAgent (userAgent.Length > 0)
                    equal (item.Name + " count") (List.length item.Declarations) rest.Length

                    for actual, expected in Seq.zip rest item.Declarations do
                        let label = item.Name + " " + expected.Value
                        equal (label + " origin") expected.Origin actual.Origin
                        equal (label + " value") expected.Value actual.Value
                        equal (label + " winner") expected.Winner actual.IsWinner
                        equal (label + " important") expected.Important actual.Important
                        equal (label + " layer") expected.Layer (actual.Layer |> Option.map _.Name)
                        equal (label + " depth") expected.Depth actual.Depth
                        equal (label + " declared") (defaultArg expected.Declared item.Property) actual.Property
                        equal (label + " shorthand") expected.Shorthand actual.Shorthand
                        equal (label + " var()") expected.Variable actual.HasVariableReference

                        equal
                            (label + " conditions")
                            expected.Conditions
                            (actual.Conditions
                             |> Array.map (fun c -> c.Substring (0, c.IndexOf ' '))
                             |> Array.toList)

                        let ruled = actual.Origin = CascadeOrigin.Author
                        equal (label + " position") ruled actual.Position.IsSome
                        equal (label + " selector") ruled (actual.MatchedSelector.Length > 0)

                    // Layer orders follow the layers' declaration, and source order follows rule positions.
                    let orders = rest |> Array.choose (fun d -> d.Layer |> Option.map _.Order)
                    equal (item.Name + " layer orders") (Array.sort orders) orders

                    if item.Name = "source order" then
                        equal "later rule position" true (rest[0].Position.Value.Rule < rest[1].Position.Value.Rule)

                    match cascade.Winner with
                    | Some winner when winner.Origin <> CascadeOrigin.UserAgent && not winner.HasVariableReference ->
                        equal (item.Name + " winner value") true (matches item.Computed winner.Value)
                    | _ -> ()

            match service.Cascade (byId "cx-spec", "order"), service.Cascade (byId "cx-spec", "order") with
            | Ok first, Ok second -> equal "repeat queries agree" first second
            | _ -> failwith "repeat queries"

        service.Disable ()
        equal "disabled again" (Error DiagnosticsUnavailable.Disabled) (service.Cascade (spec, "order") |> Result.map ignore)
        service.Dispose ()
        raises "disposed" (fun () -> service.Cascade (spec, "order") |> ignore)
        checks
    finally
        service.Dispose ()

        for value in created do
            value.remove ()
