module internal NativeApiConformance.CounterStyleRules

open Ranvier.Dom

/// <summary>Checks counter rule edits against actual inside-marker layout and independently rendered literal references.</summary>
/// <param name="document">A live page document on its owning engine thread.</param>
/// <param name="flush">Updates layout before marker and reference geometry is read.</param>
/// <param name="mutateDetached">Includes the extra detached setter only for the isolated crash regression.</param>
let private runCore (document: Document) (flush: unit -> unit) mutateDetached =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let style = document.createElement "style" :?> HTMLStyleElement

    style.textContent <-
        "@counter-style c19-fallback { system: cyclic; symbols: \"FallbackWide\"; suffix: \"\"; }"
        + "@counter-style c19-fixed { system: fixed 1; symbols: \"ii\" \"WWWW\"; prefix: \"\"; suffix: \"\"; fallback: c19-fallback; }"
        + "@counter-style c19-numeric { system: numeric; symbols: \"i\" \"W\"; prefix: \"\"; suffix: \"\"; range: -10 10; }"
        + "@counter-style c19-additive { system: additive; additive-symbols: 5 \"WWWW\", 1 \"i\"; prefix: \"\"; suffix: \"\"; }"

    let container = document.createElement "div" :?> HTMLElement
    container.style.cssText <- "font:32px/40px serif;white-space:pre;font-kerning:none;font-variant-ligatures:none"
    let list = document.createElement "ol" :?> HTMLOListElement
    list.style.cssText <- "margin:0;padding:0"
    let item = document.createElement "li" :?> HTMLLIElement
    item.style.cssText <- "margin:0;padding:0;list-style-position:inside;list-style-type:c19-fixed"
    let payload = document.createElement "span" :?> HTMLElement
    payload.style.cssText <- "display:inline-block"
    payload.textContent <- "X"
    let reference = document.createElement "div" :?> HTMLElement
    let referenceMarker = document.createElement "span" :?> HTMLElement
    referenceMarker.style.cssText <- "display:inline-block;white-space:pre"
    let referencePayload = document.createElement "span" :?> HTMLElement
    referencePayload.style.cssText <- "display:inline-block"
    referencePayload.textContent <- "X"
    reference.appendChild referenceMarker |> ignore
    reference.appendChild referencePayload |> ignore
    item.appendChild payload |> ignore
    list.appendChild item |> ignore
    container.appendChild list |> ignore
    container.appendChild reference |> ignore
    document.head.appendChild style |> ignore
    document.body.appendChild container |> ignore

    let referenceAdvance text =
        referenceMarker.textContent <- text
        flush ()

        referencePayload.getBoundingClientRect().left
        - reference.getBoundingClientRect().left

    let marker label name value expected =
        item.style.setProperty ("list-style-type", name)
        item.value <- value
        let expectedAdvance = referenceAdvance expected

        let actualAdvance =
            payload.getBoundingClientRect().left
            - item.getBoundingClientRect().left

        equal (label + " positive marker advance") true (expectedAdvance > 0.)

        equal
            (label
             + $" rendered marker {expected} (advance {actualAdvance}, reference {expectedAdvance})")
            true
            (abs (actualAdvance - expectedAdvance) < 0.75)

    try
        let sheet = style.sheet
        equal "counter rule count" 4u sheet.cssRules.length
        let fallback = sheet.cssRules.item 0u :?> CSSCounterStyleRule
        let fixedRule = sheet.cssRules.item 1u :?> CSSCounterStyleRule
        let numeric = sheet.cssRules.item 2u :?> CSSCounterStyleRule
        let additive = sheet.cssRules.item 3u :?> CSSCounterStyleRule
        equal "counter rule name" "c19-fixed" fixedRule.name
        equal "counter rule system" "fixed 1" fixedRule.system
        equal "counter rule symbols" "\"ii\" \"WWWW\"" fixedRule.symbols
        equal "counter fallback name" "c19-fallback" fixedRule.fallback
        equal "counter parent sheet" true (obj.ReferenceEquals (sheet, fixedRule.parentStyleSheet))
        equal "top-level counter has no parent rule" true (isNull fixedRule.parentRule)
        equal "counter rule identity" true (obj.ReferenceEquals (fixedRule, sheet.cssRules.item 1u))
        equal "reference distinguishes equal-length narrow/wide glyphs" true (abs (referenceAdvance "iii" - referenceAdvance "WWW") > 2.)
        marker "initial fixed first" fixedRule.name 1 "ii"
        marker "initial fixed second" fixedRule.name 2 "WWWW"
        fixedRule.symbols <- "\"WWW\" \"i\""
        equal "edited symbols getter" "\"WWW\" \"i\"" fixedRule.symbols
        marker "edited fixed first" fixedRule.name 1 "WWW"
        marker "edited fixed second" fixedRule.name 2 "i"
        fixedRule.system <- "fixed 2"
        equal "edited fixed start getter" "fixed 2" fixedRule.system
        marker "edited fixed start" fixedRule.name 2 "WWW"
        marker "below fixed start uses fallback" fixedRule.name 1 "FallbackWide"
        fixedRule.prefix <- "\"[\""
        fixedRule.suffix <- "\"]\""
        equal "edited prefix getter" "\"[\"" fixedRule.prefix
        equal "edited suffix getter" "\"]\"" fixedRule.suffix
        marker "prefix and suffix rendered" fixedRule.name 2 "[WWW]"
        fixedRule.range <- "3 3"
        equal "edited range getter" "3 3" fixedRule.range
        marker "excluded range uses fallback" fixedRule.name 2 "[FallbackWide]"
        marker "included range uses symbol" fixedRule.name 3 "[i]"
        fixedRule.fallback <- "decimal"
        equal "edited fallback getter" "decimal" fixedRule.fallback
        marker "edited fallback rendered" fixedRule.name 2 "[2]"
        fixedRule.range <- "1 4"
        fixedRule.name <- "c19-renamed"
        equal "renamed counter getter" "c19-renamed" fixedRule.name
        marker "renamed counter renders" fixedRule.name 2 "[WWW]"
        marker "numeric binary two" numeric.name 2 "Wi"
        marker "numeric binary four" numeric.name 4 "Wii"
        numeric.negative <- "\"NEG\" \"END\""
        equal "edited negative getter" "\"NEG\" \"END\"" numeric.negative
        marker "negative formatting rendered" numeric.name -3 "NEGWWEND"
        numeric.pad <- "5 \"p\""
        equal "edited pad getter" "5 \"p\"" numeric.pad
        marker "padding rendered" numeric.name 3 "pppWW"
        marker "additive six" additive.name 6 "WWWWi"
        additive.additiveSymbols <- "5 \"ii\", 1 \"WWWW\""
        equal "edited additive symbols getter" "5 \"ii\", 1 \"WWWW\"" additive.additiveSymbols
        marker "edited additive six" additive.name 6 "iiWWWW"
        marker "edited additive ten" additive.name 10 "iiii"
        let preserved = fixedRule.cssText
        fixedRule.symbols <- "("
        fixedRule.range <- "invalid"
        fixedRule.name <- "123"
        equal "invalid descriptors preserve complete rule" preserved fixedRule.cssText
        marker "invalid descriptors preserve rendering" fixedRule.name 2 "[WWW]"
        fixedRule.cssText <- "@counter-style c19-ignored { system: cyclic; symbols: \"X\"; }"
        equal "inherited cssText setter is pinned no-op" preserved fixedRule.cssText
        let beforeSpeak = fixedRule.speakAs

        let speakAs =
            if isNull beforeSpeak then
                "unavailable"
            else
                fixedRule.speakAs <- "numbers"
                fixedRule.speakAs

        fallback.symbols <- "\"ChangedFallback\""
        fixedRule.fallback <- fallback.name
        marker "fallback rule edit renders" fixedRule.name 1 "[ChangedFallback]"
        sheet.deleteRule 1u
        equal "counter removal updates list" 3u sheet.cssRules.length
        equal "removed counter parent cleared" true (isNull fixedRule.parentStyleSheet)
        equal "removed counter name retained" "c19-renamed" fixedRule.name

        if mutateDetached then
            fixedRule.prefix <- "\"Detached\""
            equal "detached counter edit" "\"Detached\"" fixedRule.prefix

        checks, speakAs
    finally
        document.body.removeChild container |> ignore
        document.head.removeChild style |> ignore

/// <summary>Verifies attached counter edits, rendered markers and retained metadata after removal.</summary>
/// <param name="document">A live page document on its owning engine thread.</param>
/// <param name="flush">Updates layout before marker and reference geometry is read.</param>
let run document flush =
    runCore document flush false

/// <summary>Reproduces the browser crash when a valid descriptor is changed on a deleted counter rule.</summary>
/// <param name="document">An isolated page document used only for the crash regression.</param>
/// <param name="flush">Updates layout before marker and reference geometry is read.</param>
let reproduceDetachedMutation document flush =
    runCore document flush true
