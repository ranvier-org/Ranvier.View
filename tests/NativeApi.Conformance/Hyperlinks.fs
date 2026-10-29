module internal NativeApiConformance.Hyperlinks

open Ranvier.Dom

/// <summary>Checks Area reflection and parsed URL component mutations.</summary>
/// <param name="document">The owning live engine document; fixture nodes remain detached.</param>
let run (document: Document) =
    let mutable checks = 0

    let equal label expected actual =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected {expected}, got {actual}"

    let area = document.createElement "area" :?> HTMLAreaElement
    equal "area tag" "AREA" area.tagName
    area.alt <- "Region λ"
    equal "area alt" "Region λ" area.alt
    equal "area alt attribute" "Region λ" (area.getAttribute "alt")
    area.setAttribute ("alt", "Region β")
    equal "area alt reverse" "Region β" area.alt
    area.coords <- "0,0,40,20"
    equal "area coords" "0,0,40,20" area.coords
    equal "area coords attribute" "0,0,40,20" (area.getAttribute "coords")
    area.setAttribute ("coords", "1,2,3")
    equal "area coords reverse" "1,2,3" area.coords
    area.shape <- "circle"
    equal "area shape" "circle" area.shape
    equal "area shape attribute" "circle" (area.getAttribute "shape")
    area.setAttribute ("shape", "rect")
    equal "area shape reverse" "rect" area.shape
    area.download <- "fixture.txt"
    equal "area download" "fixture.txt" area.download
    equal "area download attribute" "fixture.txt" (area.getAttribute "download")
    area.setAttribute ("download", "next.txt")
    equal "area download reverse" "next.txt" area.download
    area.target <- "_blank"
    equal "area target" "_blank" area.target
    equal "area target attribute" "_blank" (area.getAttribute "target")
    area.setAttribute ("target", "_self")
    equal "area target reverse" "_self" area.target
    area.referrerPolicy <- "origin"
    equal "area policy" "origin" area.referrerPolicy
    equal "area policy attribute" "origin" (area.getAttribute "referrerpolicy")
    area.setAttribute ("referrerpolicy", "no-referrer")
    equal "area policy reverse" "no-referrer" area.referrerPolicy
    area.noHref <- true
    equal "area noHref" true area.noHref
    equal "area noHref attribute" "" (area.getAttribute "nohref")
    area.noHref <- false
    equal "area noHref cleared" false (area.hasAttribute "nohref")
    area.rel <- "noopener"
    equal "area rel attribute" "noopener" (area.getAttribute "rel")
    let tokens = area.relList
    equal "area rel token" true (tokens.contains "noopener")
    equal "area rel token identity" true (obj.ReferenceEquals (tokens, area.relList))
    tokens.add [| "noreferrer" |]
    equal "area token mutation" "noopener noreferrer" area.rel
    area.setAttribute ("rel", "external")
    equal "area live token reverse" true (tokens.contains "external")
    equal "area token replacement" false (tokens.contains "noopener")
    let original = "https://user:secret@example.invalid:8443/path?before=1#old"
    area.href <- original
    equal "area href" original area.href
    equal "area href attribute" original (area.getAttribute "href")
    equal "area origin" "https://example.invalid:8443" area.origin
    equal "area protocol" "https:" area.protocol
    equal "area username" "user" area.username
    equal "area password" "secret" area.password
    equal "area host" "example.invalid:8443" area.host
    equal "area hostname" "example.invalid" area.hostname
    equal "area port" "8443" area.port
    equal "area pathname" "/path" area.pathname
    equal "area search" "?before=1" area.search
    equal "area hash" "#old" area.hash
    area.protocol <- "http:"
    equal "area protocol mutation" "http:" area.protocol
    area.username <- "next"
    equal "area username mutation" "next" area.username
    area.password <- "token"
    equal "area password mutation" "token" area.password
    area.host <- "changed.invalid:9000"
    equal "area host mutation" "changed.invalid:9000" area.host
    equal "area host changes hostname" "changed.invalid" area.hostname
    equal "area host changes port" "9000" area.port
    area.hostname <- "final.invalid"
    equal "area hostname mutation" "final.invalid:9000" area.host
    area.port <- "8081"
    equal "area port mutation" "final.invalid:8081" area.host
    area.pathname <- "/two words"
    equal "area pathname encoding" "/two%20words" area.pathname
    area.search <- "?after=2"
    equal "area search mutation" "?after=2" area.search
    area.hash <- "#last"
    equal "area hash mutation" "#last" area.hash
    let finalUrl = "http://next:token@final.invalid:8081/two%20words?after=2#last"
    equal "area accumulated URL" finalUrl area.href
    equal "area accumulated attribute" finalUrl (area.getAttribute "href")
    equal "area changed origin" "http://final.invalid:8081" area.origin
    area.setAttribute ("href", "https://reverse.invalid/final")
    equal "area href reverse" "https://reverse.invalid/final" area.href
    equal "area reverse hostname" "reverse.invalid" area.hostname
    equal "area reverse clears credentials" "" area.username
    equal "area reverse clears port" "" area.port
    equal "area reverse clears query" "" area.search
    equal "area reverse clears fragment" "" area.hash
    checks
