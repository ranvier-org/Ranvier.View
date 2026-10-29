module internal NativeApiConformance.FontTransitionRules

open System
open Ranvier.Dom

let private execute (document: Document) action =
    let style = document.createElement "style" :?> HTMLStyleElement
    document.body.appendChild style |> ignore

    try
        action style.sheet
    finally
        document.body.removeChild style |> ignore

/// <summary>Checks parsed font descriptors, feature-family edits and immutable palette-rule replacement.</summary>
/// <param name="document">The live document; inserted stylesheets are removed before returning.</param>
/// <returns>The assertion count and whether the font-width descriptor alias is available.</returns>
let fonts (document: Document) =
    execute document (fun sheet ->
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

        let contains label (part: string) (text: string) =
            equal label true (text.Contains part)

        let insert css =
            let index = sheet.insertRule (css, sheet.cssRules.length)
            sheet.cssRules.item index

        let face =
            insert
                """@font-face { font-family: Fixture; src: local("Fixture"); font-style: italic; font-weight: 600; font-stretch: condensed; unicode-range: U+0-FF; font-display: swap; size-adjust: 90%; ascent-override: 80%; descent-override: 10%; line-gap-override: 5%; font-feature-settings: "kern" 1; }"""
            :?> CSSFontFaceRule

        let descriptors = face.style
        equal "font descriptor identity" true (Object.ReferenceEquals (descriptors, face.style))
        equal "font descriptor parent" true (Object.ReferenceEquals (face, descriptors.parentRule))
        equal "font family" "Fixture" descriptors.fontFamily
        equal "font style" "italic" descriptors.fontStyle
        equal "font weight" "600" descriptors.fontWeight
        equal "font stretch" "condensed" descriptors.fontStretch
        let fontWidthAlias = not (isNull descriptors.fontWidth)

        if fontWidthAlias then
            equal "font width alias" "condensed" descriptors.fontWidth

        equal "unicode range" "U+0-FF" (descriptors.unicodeRange.ToUpperInvariant ())
        equal "font display" "swap" descriptors.fontDisplay
        equal "size adjust" "90%" descriptors.sizeAdjust
        equal "ascent override" "80%" descriptors.ascentOverride
        equal "descent override" "10%" descriptors.descentOverride
        equal "line gap override" "5%" descriptors.lineGapOverride
        contains "font source retained" "Fixture" descriptors.src
        contains "font feature tag retained" "kern" descriptors.fontFeatureSettings
        equal "font style alias" descriptors.fontStyle descriptors.``font-style``
        equal "font stretch alias" descriptors.fontStretch descriptors.``font-stretch``

        if fontWidthAlias then
            equal "font width CSS alias" descriptors.fontWidth descriptors.``font-width``

        equal "unicode range alias" descriptors.unicodeRange descriptors.``unicode-range``
        equal "font display alias" descriptors.fontDisplay descriptors.``font-display``
        equal "ascent override alias" descriptors.ascentOverride descriptors.``ascent-override``
        equal "descent override alias" descriptors.descentOverride descriptors.``descent-override``
        equal "line gap override alias" descriptors.lineGapOverride descriptors.``line-gap-override``
        equal "font feature settings alias" descriptors.fontFeatureSettings descriptors.``font-feature-settings``
        descriptors.``font-family`` <- "ChangedFixture"
        equal "font family alias mutation" "ChangedFixture" descriptors.fontFamily
        descriptors.fontWeight <- "700"
        equal "font weight alias mutation" "700" descriptors.``font-weight``
        descriptors.sizeAdjust <- "95%"
        equal "size adjust alias mutation" "95%" descriptors.``size-adjust``

        descriptors.removeProperty "font-display"
        |> ignore

        equal "font descriptor removal" "" descriptors.fontDisplay

        let features =
            insert "@font-feature-values Fixture { @styleset { decorative: 1 2; } @stylistic { alternate: 3; } }" :?> CSSFontFeatureValuesRule

        equal "feature rule family" "Fixture" features.fontFamily
        contains "feature definitions serialized" "decorative" features.cssText
        contains "feature values serialized" "1 2" features.cssText
        contains "stylistic definition serialized" "alternate" features.cssText
        let featureDefinitions = features.cssText.Substring (features.cssText.IndexOf '{')
        features.fontFamily <- "ChangedFixture"
        equal "feature family mutation" "ChangedFixture" features.fontFamily
        contains "feature serialized family mutation" "ChangedFixture" features.cssText
        equal "feature family mutation preserves complete definitions" featureDefinitions (features.cssText.Substring (features.cssText.IndexOf '{'))

        let palette =
            insert "@font-palette-values --fixture { font-family: Fixture; base-palette: 1; override-colors: 0 red, 2 blue; }"
            :?> CSSFontPaletteValuesRule

        equal "palette rule name" "--fixture" palette.name
        equal "palette family" "Fixture" palette.fontFamily
        equal "palette base" "1" palette.basePalette

        equal
            "palette colors preserve indices, values and order"
            true
            (palette.overrideColors = "0 rgb(255, 0, 0), 2 rgb(0, 0, 255)"
             || palette.overrideColors = "0 red, 2 blue")

        equal "palette rule identity" true (Object.ReferenceEquals (palette, sheet.cssRules.item 2u))
        equal "palette parent sheet" true (Object.ReferenceEquals (sheet, palette.parentStyleSheet))
        sheet.deleteRule 2u
        equal "detached palette retains name" "--fixture" palette.name
        equal "detached palette retains base" "1" palette.basePalette

        let replacement =
            insert "@font-palette-values --fixture { font-family: ChangedFixture; base-palette: dark; override-colors: 0 green; }"
            :?> CSSFontPaletteValuesRule

        equal "replacement palette family" "ChangedFixture" replacement.fontFamily
        equal "replacement palette base" "dark" replacement.basePalette

        equal
            "replacement palette color"
            true
            (replacement.overrideColors = "0 rgb(0, 128, 0)"
             || replacement.overrideColors = "0 green")

        equal "replacement palette is independent" false (Object.ReferenceEquals (palette, replacement))
        equal "retained palette is unchanged" "Fixture" palette.fontFamily
        equal "retained descriptor after other rule replacement" "700" descriptors.fontWeight

        let emptyPalette =
            insert "@font-palette-values --empty { font-family: Fixture; }" :?> CSSFontPaletteValuesRule

        equal "absent palette base" "" emptyPalette.basePalette
        equal "absent palette colors" "" emptyPalette.overrideColors
        checks, fontWidthAlias)

/// <summary>Checks starting-style child editing and view-transition rule metadata without executing a transition.</summary>
/// <param name="document">The live document; inserted stylesheets are removed before returning.</param>
let transitions (document: Document) =
    execute document (fun sheet ->
        let mutable checks = 0

        let equal label expected actual =
            checks <- checks + 1

            if expected <> actual then
                failwith $"{label}: expected {expected}, got {actual}"

        let insert css =
            let index = sheet.insertRule (css, sheet.cssRules.length)
            sheet.cssRules.item index

        let starting =
            insert "@starting-style { .entry { opacity: 0; } }" :?> CSSStartingStyleRule

        equal "starting style identity" true (Object.ReferenceEquals (starting, sheet.cssRules.item 0u))
        equal "starting style parent" true (Object.ReferenceEquals (sheet, starting.parentStyleSheet))
        equal "starting child count" 1u starting.cssRules.length
        let child = starting.cssRules.item 0u :?> CSSStyleRule
        equal "starting child parent" true (Object.ReferenceEquals (starting, child.parentRule))
        equal "starting child sheet" true (Object.ReferenceEquals (sheet, child.parentStyleSheet))
        equal "starting child selector" ".entry" child.selectorText
        equal "starting child declaration" "0" (child.style.getPropertyValue "opacity")
        child.style.setProperty ("opacity", "0.25", "")
        equal "starting child edit" "0.25" (child.style.getPropertyValue "opacity")
        equal "starting child insert index" 1u (starting.insertRule (".second { opacity: 0.5; }", 1u))
        equal "starting child list is live" 2u starting.cssRules.length
        starting.deleteRule 1u
        equal "starting child deletion" 1u starting.cssRules.length

        let transition =
            insert "@view-transition { navigation: auto; types: entry reverse; }" :?> CSSViewTransitionRule

        equal "transition navigation" "auto" transition.navigation
        equal "transition types preserve order" [ "entry"; "reverse" ] (Array.toList transition.types)
        equal "transition rule identity" true (Object.ReferenceEquals (transition, sheet.cssRules.item 1u))
        equal "transition parent sheet" true (Object.ReferenceEquals (sheet, transition.parentStyleSheet))
        sheet.deleteRule 1u
        equal "detached transition navigation" "auto" transition.navigation
        equal "detached transition types" [ "entry"; "reverse" ] (Array.toList transition.types)

        let replacement =
            insert "@view-transition { navigation: none; types: replacement; }" :?> CSSViewTransitionRule

        equal "replacement transition navigation" "none" replacement.navigation
        equal "replacement transition types" [ "replacement" ] (Array.toList replacement.types)
        equal "replacement transition identity" false (Object.ReferenceEquals (transition, replacement))
        equal "retained transition stays unchanged" "auto" transition.navigation
        let emptyTransition = insert "@view-transition {}" :?> CSSViewTransitionRule
        equal "absent transition navigation" "" emptyTransition.navigation
        equal "empty transition types" [] (Array.toList emptyTransition.types)
        checks)
