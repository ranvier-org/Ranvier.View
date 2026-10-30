module internal NativeApiConformance.CustomProperties

open System
open Ranvier.Dom

/// <summary>Checks document registration, actual interpolation and readonly property-rule inspection.</summary>
/// <param name="document">A fresh live document; API registrations persist until that document is destroyed.</param>
/// <param name="create">Creates a borrowed registration provider for this document.</param>
/// <param name="computed">Reads live computed values in this document.</param>
/// <param name="disposalName">Reads the provider's observable disposal failure name.</param>
/// <param name="expectedDisposalName">The provider-specific disposal failure name.</param>
let run
    (document: Document)
    (create: unit -> CustomPropertyService)
    (computed: ComputedStyleService)
    (disposalName: exn -> string)
    expectedDisposalName
    =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    use service = create ()

    let length name inherits value =
        { PropertyDefinition.Create (name, inherits) with
            Syntax = "<length>"
            InitialValue = CustomPropertyInitialValue.Value value
        }

    let reject definition expected =
        let mutable name = "accepted"

        try
            service.Register definition
        with CustomPropertyException (error, _) ->
            name <- error

        equal "engine registration error" expected name

    let registered = length "--c12-registered" false "20px"
    equal "default universal syntax" "*" (PropertyDefinition.Create ("--unused", false)).Syntax
    equal "default initial omission" CustomPropertyInitialValue.Omitted (PropertyDefinition.Create ("--unused", false)).InitialValue
    service.Register registered
    reject registered "InvalidModificationError"
    reject { registered with Name = "width" } "SyntaxError"
    reject { registered with Name = null } "SyntaxError"

    reject
        { registered with
            Name = "--c12-bad-syntax"
            Syntax = "<not-a-type>"
        }
        "SyntaxError"

    reject
        { registered with
            Name = "--c12-nul-syntax"
            Syntax = "<length>\u0000junk"
        }
        "SyntaxError"

    reject
        { registered with
            Name = "--c12-null-syntax"
            Syntax = null
        }
        "SyntaxError"

    reject
        { registered with
            Name = "--c12-absent"
            InitialValue = CustomPropertyInitialValue.Omitted
        }
        "SyntaxError"

    reject (length "--c12-empty" false "") "SyntaxError"
    reject (length "--c12-dependent" false "1em") "SyntaxError"
    reject (length "--c12-invalid" false "red") "SyntaxError"
    reject (length "--c12-nul-initial" false "20px\u0000junk") "SyntaxError"
    service.Register (length "--c12-inherit" true "11px")
    service.Register (length "--c12-no-inherit" false "17px")
    service.Register (length "--c12-unicode-\u03c0" false "23px")
    service.Register (PropertyDefinition.Create ("--c12-universal-absent", false))

    service.Register
        { PropertyDefinition.Create ("--c12-universal-empty", false) with
            InitialValue = CustomPropertyInitialValue.Value ""
        }

    service.Register
        { PropertyDefinition.Create ("--c12-present-null", false) with
            Syntax = "<custom-ident>"
            InitialValue = CustomPropertyInitialValue.Value null
        }

    let mutable nullRecordRejected = false

    try
        service.Register Unchecked.defaultof<PropertyDefinition>
    with _ ->
        nullRecordRejected <- true

    equal "null registration record rejected" true nullRecordRejected
    service.Register (length "--c12-after-errors" false "31px")

    let style = document.createElement "style" :?> HTMLStyleElement
    let parent = document.createElement "div" :?> HTMLElement
    parent.id <- "c12-parent"

    let child id =
        let element = document.createElement "div" :?> HTMLElement
        element.id <- id
        parent.appendChild element |> ignore
        element

    let animated = child "c12-animated"
    let plain = child "c12-plain"
    let inherited = child "c12-inherited"
    let isolated = child "c12-isolated"
    let initial = child "c12-initial"
    let ruleValue = child "c12-rule-value"
    let absent = child "c12-absent"
    let empty = child "c12-empty"
    let presentNull = child "c12-present-null"
    let afterErrors = child "c12-after-errors"
    let priority = child "c12-priority"
    priority.style.setProperty ("--c12-registered", "40px", "important")

    style.textContent <-
        "@property --c12-from-rule {syntax:'<length>';inherits:false;initial-value:29px}"
        + "@property --c12-universal-rule {syntax:'*';inherits:true}"
        + "@keyframes c12-motion {from {--c12-registered:20px;--c12-unregistered:20px} to {--c12-registered:100px;--c12-unregistered:100px}}"
        + "#c12-animated,#c12-plain {animation:c12-motion 100s linear -50s 1 normal both paused}"
        + "#c12-animated,#c12-priority {width:var(--c12-registered)} #c12-plain {width:var(--c12-unregistered)}"
        + "#c12-parent {--c12-inherit:83px;--c12-no-inherit:83px}"
        + "#c12-inherited {width:var(--c12-inherit)} #c12-isolated {width:var(--c12-no-inherit)}"
        + "#c12-initial {width:var(--c12-unicode-\u03c0)} #c12-rule-value {width:var(--c12-from-rule)}"
        + "#c12-after-errors {width:var(--c12-after-errors)}"
        + "#c12-absent {width:var(--c12-universal-absent,21px)} #c12-empty {width:var(--c12-universal-empty,21px)}"

    document.head.appendChild style |> ignore
    document.body.appendChild parent |> ignore

    let value element property =
        (computed.Get (element, None)).getPropertyValue property

    try
        equal "registered paused midpoint interpolates" "60px" (value animated "width")
        equal "unregistered midpoint remains discrete" "100px" (value plain "width")
        equal "registered inheritance" "83px" (value inherited "width")
        equal "noninherited registered initial" "17px" (value isolated "width")
        equal "Unicode registration initial" "23px" (value initial "width")
        equal "registration errors preserve later success" "31px" (value afterErrors "width")
        equal "omitted universal initial uses variable fallback" "21px" (value absent "width")
        equal "present empty universal value is empty" "" (value empty "--c12-universal-empty")
        equal "present empty does not select fallback" false (value empty "width" = "21px")
        equal "present null becomes WebIDL text null" "null" (value presentNull "--c12-present-null")
        equal "initial important priority" "important" (priority.style.getPropertyPriority "--c12-registered")
        equal "initial important result" "40px" (value priority "width")
        let previousValue = priority.style.getPropertyValue "--c12-registered"
        let previousPriority = priority.style.getPropertyPriority "--c12-registered"

        try
            priority.style.setProperty ("--c12-registered", "80px", "")
            equal "temporary specified registration value" "80px" (priority.style.getPropertyValue "--c12-registered")
            equal "temporary inline registration value" "80px" (value priority "width")
            equal "temporary inline priority" "" (priority.style.getPropertyPriority "--c12-registered")
        finally
            priority.style.setProperty ("--c12-registered", previousValue, previousPriority)

        equal "original inline value restored" previousValue (priority.style.getPropertyValue "--c12-registered")
        equal "original priority restored" previousPriority (priority.style.getPropertyPriority "--c12-registered")
        equal "restored actual registered result" "40px" (value priority "width")

        let sheet = style.sheet
        let rule = sheet.cssRules.item 0u :?> CSSPropertyRule
        let universal = sheet.cssRules.item 1u :?> CSSPropertyRule
        equal "property rule identity" true (Object.ReferenceEquals (rule, sheet.cssRules.item 0u))
        equal "property rule parent sheet" true (Object.ReferenceEquals (sheet, rule.parentStyleSheet))
        equal "property rule name" "--c12-from-rule" rule.name
        equal "property rule syntax" "<length>" rule.syntax
        equal "property rule inherits" false rule.inherits
        equal "property rule initial value" "29px" rule.initialValue
        equal "property rule actual initial result" "29px" (value ruleValue "width")
        equal "universal rule syntax" "*" universal.syntax
        equal "universal rule inherits" true universal.inherits
        equal "absent rule initial value stays null" true (isNull universal.initialValue)
        let serialized = rule.cssText
        equal "serialized property descriptor present" true (serialized.Contains "29px")
        sheet.deleteRule 0u
        equal "retained property rule name" "--c12-from-rule" rule.name
        equal "retained property rule initial" "29px" rule.initialValue
        equal "retained complete serialization" serialized rule.cssText
        equal "removed rule parent cleared" true (isNull rule.parentStyleSheet)

        service.Dispose ()
        let mutable disposed = "accepted"

        try
            service.Register (length "--c12-after-dispose" false "47px")
        with error ->
            disposed <- disposalName error

        equal "provider disposal rejects subsequent registration" expectedDisposalName disposed
        use next = create ()
        let mutable duplicate = "accepted"

        try
            next.Register registered
        with CustomPropertyException (name, _) ->
            duplicate <- name

        equal "provider disposal keeps document registration" "InvalidModificationError" duplicate
        equal "provider disposal keeps actual interpolation" "60px" (value animated "width")
        checks
    finally
        document.body.removeChild parent |> ignore
        document.head.removeChild style |> ignore
