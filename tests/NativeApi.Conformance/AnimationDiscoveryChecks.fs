module internal NativeApiConformance.AnimationDiscoveryChecks

open Ranvier.Dom

let private Css =
    ".wd{position:absolute;width:40px;height:40px}"
    + "@keyframes wd-fade{from{opacity:0}to{opacity:1}}@keyframes wd-spin{from{transform:rotate(0deg)}to{transform:rotate(90deg)}}"
    + "#wd-css{left:0;top:0;animation:wd-fade 100s linear}#wd-tr{left:50px;top:0;transition:opacity 100s linear}"
    + "#wd-tree{position:absolute;left:100px;top:0}#wd-c2{animation:wd-spin 100s linear}"

let private frame (properties: (string * string) list) =
    let value = Keyframe ()

    value.properties <-
        properties
        |> List.map (fun (name, text) ->
            let property = KeyframeProperty ()
            property.name <- name
            property.value <- text
            property)
        |> List.toArray

    value

/// <summary>
/// A05: document, element and subtree getAnimations over a CSS animation, a CSS transition and script animations,
/// with each animation's interface and CSS metadata, id, effect target and timeline, identity across the lists and
/// cancellation. A04: commitStyles at the midpoint keeps the committed values after cancel and leaves unrelated
/// inline declarations and priorities. Returns the (discovery, commit) assertion counts.
/// </summary>
let run (document: Document) (styles: unit -> ComputedStyleService) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if actual <> expected then
            failwith (
                label
                + ": expected "
                + string (box expected)
                + ", got "
                + string (box actual)
            )

    let same label (expected: obj) (actual: obj) =
        checks <- checks + 1

        if not (obj.ReferenceEquals (expected, actual)) then
            failwith (label + ": expected the same object")

    let element parent id =
        let value = document.createElement "div" :?> HTMLElement
        value.id <- id
        value.className <- "wd"
        (parent: Node).appendChild value |> ignore
        value

    let style = document.createElement "style"
    style.textContent <- Css
    document.head.appendChild style |> ignore
    let css = element document.body "wd-css"
    let tr = element document.body "wd-tr"
    let tree = document.createElement "div" :?> HTMLElement
    tree.id <- "wd-tree"
    document.body.appendChild tree |> ignore
    let c1 = element tree "wd-c1"
    let c2 = element tree "wd-c2"
    let computed = styles ()

    let targetId (animation: WebAnimation) =
        let effect = unbox<KeyframeEffect> animation.effect

        if isNull effect || isNull effect.target then
            ""
        else
            (unbox<HTMLElement> effect.target).id

    let mutable commitChecks = 0

    try
        (computed.Get (tr, None)).getPropertyValue "opacity"
        |> ignore

        tr.style.setProperty ("opacity", "0.2")

        let options id =
            let value = KeyframeAnimationOptions ()
            value.duration <- UnrestrictedDoubleOrCSSNumericValueOrDOMString.UnrestrictedDouble 100000.0
            value.id <- id
            value

        let scripted =
            c1.animate ([| frame [ "opacity", "0" ]; frame [ "opacity", "1" ] |], options "scripted")

        let second =
            css.animate (
                [|
                    frame [ "transform", "translateX(0px)" ]
                    frame [ "transform", "translateX(10px)" ]
                |],
                options "second"
            )

        let all = document.getAnimations ()
        equal "document count" 5 all.Length
        equal "transition first" "opacity" (unbox<CSSTransition> all[0]).transitionProperty
        equal "transition target" "wd-tr" (targetId all[0])
        equal "css animation name" "wd-fade" (unbox<CSSAnimation> all[1]).animationName
        equal "css animation target" "wd-css" (targetId all[1])
        equal "second css animation" "wd-spin" (unbox<CSSAnimation> all[2]).animationName
        equal "scripted id" "scripted" all[3].id
        equal "scripted target" "wd-c1" (targetId all[3])
        equal "second scripted id" "second" all[4].id
        equal "css animation id" "" all[1].id

        for index, animation in Array.indexed all do
            same ("timeline " + string index) (box document.timeline) (box animation.timeline)

        same "scripted is listed" (box scripted) (box all[3])
        same "second is listed" (box second) (box all[4])
        equal "tree" 0 (tree.getAnimations ()).Length
        let subtreeOptions = GetAnimationsOptions ()
        subtreeOptions.subtree <- true
        let subtree = tree.getAnimations subtreeOptions
        equal "tree subtree" [ "wd-c2"; "wd-c1" ] (subtree |> Array.map targetId |> Array.toList)
        same "subtree CSS animation is listed" (box all[2]) (box subtree[0])
        let own = css.getAnimations ()

        equal
            "css element"
            [ "wd-fade"; "second" ]
            (own
             |> Array.map (fun a ->
                 if a.id = "" then
                     (unbox<CSSAnimation> a).animationName
                 else
                     a.id)
             |> Array.toList)

        same "element lists the document's object" (box all[1]) (box own[0])
        equal "transition element" 1 (tr.getAnimations ()).Length
        let again = document.getAnimations ()

        for index, animation in Array.indexed all do
            same ("document again " + string index) (box animation) (box again[index])

        all[1].cancel()
        equal "css cancelled" AnimationPlayState.Idle all[1].playState

        equal
            "css cancelled remaining"
            [ "second" ]
            (css.getAnimations ()
             |> Array.map (fun a -> a.id)
             |> Array.toList)

        scripted.cancel ()

        equal
            "scripted cancelled tree"
            [ "wd-c2" ]
            (tree.getAnimations subtreeOptions
             |> Array.map targetId
             |> Array.toList)

        equal "scripted cancelled document" 3 (document.getAnimations ()).Length
        all[0].cancel()
        second.cancel ()

        equal
            "after all cancelled"
            [ "wd-spin" ]
            (document.getAnimations ()
             |> Array.map (fun a -> (unbox<CSSAnimation> a).animationName)
             |> Array.toList)

        all[2].cancel()

        // A04: commitStyles.
        let discovery = checks
        checks <- 0
        let target = element document.body "wd-commit"
        target.style.setProperty ("color", "rgb(1, 2, 3)", "important")
        target.style.setProperty ("margin-top", "0px")

        let committed =
            target.animate (
                [|
                    frame [ "opacity", "0"; "transform", "translateX(0px)" ]
                    frame [ "opacity", "1"; "transform", "translateX(100px)" ]
                |],
                1000.0
            )

        committed.pause ()
        committed.currentTime <- Some (DoubleOrCSSNumericValue.Double 500.0)
        committed.commitStyles ()
        committed.cancel ()
        equal "committed opacity" "0.5" (target.style.getPropertyValue "opacity")
        equal "committed transform" "translateX(50px)" (target.style.getPropertyValue "transform")
        equal "committed opacity priority" "" (target.style.getPropertyPriority "opacity")
        equal "unrelated color" "rgb(1, 2, 3)" (target.style.getPropertyValue "color")
        equal "unrelated color priority" "important" (target.style.getPropertyPriority "color")
        equal "unrelated margin" "0px" (target.style.getPropertyValue "margin-top")
        equal "computed opacity after cancel" "0.5" ((computed.Get (target, None)).getPropertyValue "opacity")
        equal "cancelled" AnimationPlayState.Idle committed.playState
        let detached = document.createElement "div"

        let failed =
            try
                (detached.animate ([| frame [ "opacity", "0" ] |], 1000.0)).commitStyles()
                false
            with _ ->
                true

        equal "detached commitStyles fails" true failed
        target.remove ()
        commitChecks <- checks
        checks <- discovery
    finally
        for node in [ css :> Node; tr; tree; style ] do
            node.parentNode.removeChild node |> ignore

    checks, commitChecks
