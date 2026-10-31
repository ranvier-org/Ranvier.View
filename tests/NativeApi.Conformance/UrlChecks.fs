module internal NativeApiConformance.UrlChecks

open System
open Ranvier.Dom

/// <summary>The URL parser whose measured differences from the other engine the fixture expects.</summary>
type Parser =
    /// <summary>WebKit's WTF URLParser, which follows the WHATWG URL standard in the qualified cases.</summary>
    | WebKit
    /// <summary>Chromium 156's GURL-based parser.</summary>
    | Chromium

// Each case: input, base, then the href on WebKit and on Chromium; None is a TypeError. The cases come from the
// Ranvier.WebCore H13 fixture (tests/fixtures/url-cases.json); the four that differ are qualified there.
let private parseCases: (string * string option * string option * string option) list =
    let same input baseUrl href =
        input, baseUrl, href, href

    [
        same "http://example.com/a/b/../c/./d" None (Some "http://example.com/a/c/d")
        same "../x?y#z" (Some "http://h.test/a/b/c") (Some "http://h.test/a/x?y#z")
        same "../../../../x" (Some "http://h.test/a/b/c") (Some "http://h.test/x")
        same "//other.test/p" (Some "https://h.test/a") (Some "https://other.test/p")
        same "?q=1" (Some "http://h.test/a/b?old#f") (Some "http://h.test/a/b?q=1")
        same "" (Some "http://h.test/a/b?q#f") (Some "http://h.test/a/b?q")
        same "g;x?y#s" (Some "http://a/b/c/d;p?q") (Some "http://a/b/c/g;x?y#s")
        same "https:other" (Some "https://h.test/a/b") (Some "https://h.test/a/other")
        same "http:" (Some "https://h.test/a") None
        same "HTTP://EXAMPLE.COM:80/" None (Some "http://example.com/")
        same "http://example.com:065535/" None (Some "http://example.com:65535/")
        same "http://example.com:65536/" None None
        same "http://[0:0:0:0:0:0:0:1]/" None (Some "http://[::1]/")
        same "http://[::127.0.0.1]/" None (Some "http://[::7f00:1]/")
        same "http://192.168.0.257/" None None
        same "http://0x7f.1/" None (Some "http://127.0.0.1/")
        same "http://2130706433/" None (Some "http://127.0.0.1/")
        same "http://münchen.de/" None (Some "http://xn--mnchen-3ya.de/")
        same "http://EXAMPLE.ＣＯＭ/" None (Some "http://example.com/")
        "http://ex ample.com/", None, None, Some "http://ex%20ample.com/"
        same "http://a<b.test/" None None
        same "http://example.com/a b/ü?q=a b&ü#f g" None (Some "http://example.com/a%20b/%C3%BC?q=a%20b&%C3%BC#f%20g")
        same "http://example.com/%zz/%41?%zz#%zz" None (Some "http://example.com/%zz/%41?%zz#%zz")
        "http://example.com/\"<>`{}|^", None, Some "http://example.com/%22%3C%3E%60%7B%7D|%5E", Some "http://example.com/%22%3C%3E%60%7B%7D%7C%5E"
        same "http://us@er:p@ss@host/" None (Some "http://us%40er:p%40ss@host/")
        same "file:///C:/Windows/../x" None (Some "file:///C:/x")
        "file:c:\\foo\\bar", None, Some "file:///c:/foo/bar", Some "file:///C:/foo/bar"
        same "\\\\server\\share\\f" (Some "file:///C:/") (Some "file://server/share/f")
        "file://localhost/etc/x", None, Some "file:///etc/x", Some "file://localhost/etc/x"
        same "mailto:Someone@Example.com" None (Some "mailto:Someone@Example.com")
        same "data:text/plain,hello world" None (Some "data:text/plain,hello world")
        same "non-special://HOST/x" None (Some "non-special://HOST/x")
        same "foo://" None (Some "foo://")
        same "sc://ñ/" None (Some "sc://%C3%B1/")
        same "  http://trim.test/  " None (Some "http://trim.test/")
        same "http://a.test/\t\nb" None (Some "http://a.test/b")
        same "http:/\\\\a.test/b" None (Some "http://a.test/b")
        same "http://a.test/a/%2e./b" None (Some "http://a.test/b")
        same "relative" None None
        same "https://x.test/\u0000" None (Some "https://x.test/")
        same "https://x.test/😀?😀" None (Some "https://x.test/%F0%9F%98%80?%F0%9F%98%80")
    ]

// Each case: href, setter, value, then the resulting href on WebKit and on Chromium.
let private setterCases: (string * string * string * string * string) list =
    let same href setter value result =
        href, setter, value, result, result

    [
        same "https://a.test/p?q#f" "protocol" "http" "http://a.test/p?q#f"
        same "https://a.test/p" "protocol" "mailto" "https://a.test/p"
        "mailto:x@y", "protocol", "https", "mailto:x@y", "https://x@y/"
        same "http://a.test/" "username" "us er@" "http://us%20er%40@a.test/"
        same "http://a.test/" "password" "p:w" "http://:p%3Aw@a.test/"
        same "http://a.test/" "host" "b.test:99" "http://b.test:99/"
        same "http://a.test:81/" "hostname" "c.test:82" "http://a.test:81/"
        same "http://a.test/" "port" "8080abc" "http://a.test:8080/"
        same "http://a.test:81/" "port" "70000" "http://a.test:81/"
        same "http://a.test/" "pathname" "/x y/../z" "http://a.test/z"
        same "http://a.test/" "search" "a b=c" "http://a.test/?a%20b=c"
        "http://a.test/", "search", "?", "http://a.test/?", "http://a.test/"
        same "http://a.test/" "hash" "f g" "http://a.test/#f%20g"
    ]

/// <summary>
/// Checks URL parsing, URL.parse and URL.canParse, setters, URLSearchParams operations, URL.searchParams identity and
/// liveness, and URLPattern construction, test and exec on one provider; the result is the assertion count.
/// </summary>
/// <param name="errorName">The DOM exception name of a provider error, such as <c>TypeError</c>.</param>
let run (create: unit -> UrlService) (errorName: exn -> string) (parser: Parser) =
    let mutable checks = 0

    let equal label (expected: 'T) (actual: 'T) =
        checks <- checks + 1

        if expected <> actual then
            failwith $"{label}: expected %A{expected}, got %A{actual}"

    let fails label (expected: string) (action: unit -> unit) =
        checks <- checks + 1

        let name =
            try
                action ()
                None
            with error ->
                Some (errorName error)

        if name <> Some expected then
            failwith $"{label}: expected {expected}, got %A{name}"

    let throws label (action: unit -> unit) =
        checks <- checks + 1

        let raised =
            try
                action ()
                false
            with _ ->
                true

        if not raised then
            failwith $"{label}: expected an exception"

    let pick webkit chromium =
        match parser with
        | WebKit -> webkit
        | Chromium -> chromium

    use service = create ()

    for input, baseUrl, webkit, chromium in parseCases do
        let expected = pick webkit chromium
        let label = $"URL {input} against %A{baseUrl}"
        equal (label + ": canParse") expected.IsSome (service.CanParse (input, baseUrl))

        equal
            (label + ": parse")
            expected
            (service.Parse (input, baseUrl)
             |> Option.map (fun url -> url.href))

        match expected with
        | Some href -> equal (label + ": constructor") href (service.Create(input, baseUrl).href)
        | None -> fails (label + ": constructor") "TypeError" (fun () -> service.Create (input, baseUrl) |> ignore)

    let url =
        service.Create ("https://user:pass@sub.example.com:8443/a/b?x=1&y=2#frag", None)

    equal
        "URL components"
        [
            "https://sub.example.com:8443"
            "https:"
            "user"
            "pass"
            "sub.example.com:8443"
            "sub.example.com"
            "8443"
            "/a/b"
            "?x=1&y=2"
            "#frag"
        ]
        [
            url.origin
            url.protocol
            url.username
            url.password
            url.host
            url.hostname
            url.port
            url.pathname
            url.search
            url.hash
        ]

    equal "URL.toJSON" url.href (url.toJSON ())

    for href, setter, value, webkit, chromium in setterCases do
        let target = service.Create (href, None)

        match setter with
        | "protocol" -> target.protocol <- value
        | "username" -> target.username <- value
        | "password" -> target.password <- value
        | "host" -> target.host <- value
        | "hostname" -> target.hostname <- value
        | "port" -> target.port <- value
        | "pathname" -> target.pathname <- value
        | "search" -> target.search <- value
        | _ -> target.hash <- value

        equal $"{href} {setter} = {value}" (pick webkit chromium) target.href

    fails "href = an invalid URL" "TypeError" (fun () -> url.href <- "not a url")
    equal "an invalid href leaves the URL" "https://user:pass@sub.example.com:8443/a/b?x=1&y=2#frag" url.href

    // URLSearchParams.
    let query =
        service.CreateSearchParams (UrlSearchParamsInit.Query "?a=1&b=x+y&a=%E2%82%AC&=e&n")

    equal "query entries" [| "a", "1"; "b", "x y"; "a", "€"; "", "e"; "n", "" |] (service.Entries query)
    equal "query size" 5u query.size
    equal "get" "1" (query.get "a")
    equal "get missing" null (query.get "missing")
    equal "getAll" [| "1"; "€" |] (query.getAll "a")
    equal "has" true (query.has "b")
    equal "has value" true (query.has ("a", "€"))
    equal "has other value" false (query.has ("a", "2"))
    query.append ("s p", "a&b=c")
    query.delete ("a", "1")
    equal "append and delete a pair" "b=x+y&a=%E2%82%AC&=e&n=&s+p=a%26b%3Dc" (query.toString ())
    query.set ("a", "z")
    query.sort ()
    equal "set and sort" "=e&a=z&b=x+y&n=&s+p=a%26b%3Dc" (query.toString ())
    query.delete "a"
    equal "delete every value" [| "", "e"; "b", "x y"; "n", ""; "s p", "a&b=c" |] (service.Entries query)

    let pairs =
        service.CreateSearchParams (UrlSearchParamsInit.Pairs [| "z", "1"; "a", "x y"; "z", "0"; "\U0001F600", "e"; "\uFFFF", "f" |])

    pairs.sort ()
    equal "pairs sort by code units, stably" [| "a", "x y"; "z", "1"; "z", "0"; "\U0001F600", "e"; "\uFFFF", "f" |] (service.Entries pairs)
    equal "pairs serialize" "a=x+y&z=1&z=0&%F0%9F%98%80=e&%EF%BF%BF=f" (pairs.toString ())

    // U+0000 survives every string transport.
    let nul = service.CreateSearchParams (UrlSearchParamsInit.Query "a=x%00y&a=%00")
    equal "get keeps U+0000" "x\u0000y" (nul.get "a")
    equal "getAll keeps U+0000" [| "x\u0000y"; "\u0000" |] (nul.getAll "a")

    let nulPairs =
        service.CreateSearchParams (UrlSearchParamsInit.Pairs [| "k\u0000", "v\u0000w" |])

    equal "pairs keep U+0000" [| "k\u0000", "v\u0000w" |] (service.Entries nulPairs)
    equal "U+0000 serializes" "k%00=v%00w" (nulPairs.toString ())

    let linked = service.Create ("http://a.test/p?x=1&y=2#f", None)
    let parameters = linked.searchParams
    let again = linked.searchParams
    equal "searchParams is one object" true (obj.ReferenceEquals (parameters, again))
    parameters.append ("z", "3 4")
    equal "searchParams writes the URL" "http://a.test/p?x=1&y=2&z=3+4#f" linked.href
    linked.search <- "?q=w+v"
    equal "the URL writes searchParams" "w v" (parameters.get "q")
    parameters.delete "q"
    equal "deleting the last pair empties the query" ("http://a.test/p#f", "") (linked.href, linked.search)

    // URLPattern.
    let init (configure: URLPatternInit -> unit) =
        let value = URLPatternInit ()
        configure value
        USVStringOrURLPatternInit.URLPatternInit value

    let text value =
        USVStringOrURLPatternInit.USVString value

    let books =
        service.CreatePattern (text "/books/:id(\\d+)", Some "https://example.com", false)

    equal
        "pattern components"
        [ "https"; "*"; "*"; "example.com"; ""; "/books/:id(\\d+)"; "*"; "*" ]
        [
            books.protocol
            books.username
            books.password
            books.hostname
            books.port
            books.pathname
            books.search
            books.hash
        ]

    equal "hasRegExpGroups" true books.hasRegExpGroups
    equal "test" true (books.test "https://example.com/books/42")
    equal "test a mismatch" false (books.test "https://example.com/books/x")
    equal "test against a base" true (books.test ("/books/7", "https://example.com"))
    equal "exec a mismatch" None (service.Exec (books, Some (text "https://example.com/books/x"), None))

    match service.Exec (books, Some (text "https://example.com/books/42?q#h"), None) with
    | Some result ->
        equal "exec inputs" [| text "https://example.com/books/42?q#h" |] result.Inputs

        equal
            "exec pathname"
            {
                Input = "/books/42"
                Groups = [| "id", Some "42" |]
            }
            result.Pathname

        equal "exec hostname" { Input = "example.com"; Groups = [||] } result.Hostname

        equal
            "exec search"
            {
                Input = "q"
                Groups = [| "0", Some "q" |]
            }
            result.Search

        equal "exec port" { Input = ""; Groups = [||] } result.Port
    | None -> failwith "exec matched nothing"

    let optional =
        service.CreatePattern (init (fun i -> i.pathname <- Some "/:a/:b?"), None, false)

    match service.Exec (optional, Some (init (fun i -> i.pathname <- Some "/1")), None) with
    | Some result ->
        equal "an unmatched optional group" [| "a", Some "1"; "b", None |] result.Pathname.Groups

        equal
            "exec init input"
            (Some "/1")
            (match result.Inputs with
             | [| USVStringOrURLPatternInit.URLPatternInit i |] -> i.pathname
             | _ -> None)
    | None -> failwith "optional pattern matched nothing"

    // The engine lists groups in pattern order; a JavaScript object lists integer-like keys first.
    let mixed =
        service.CreatePattern (init (fun i -> i.pathname <- Some "/:name/(\\d+)"), None, false)

    match service.Exec (mixed, Some (init (fun i -> i.pathname <- Some "/x/7")), None) with
    | Some result -> equal "group order" (pick [| "name", Some "x"; "0", Some "7" |] [| "0", Some "7"; "name", Some "x" |]) result.Pathname.Groups
    | None -> failwith "mixed pattern matched nothing"

    let anyUrl = service.CreatePattern (init ignore, None, false)
    equal "an empty init matches an absent input" true (service.Exec (anyUrl, None, None)).IsSome

    let caseless =
        service.CreatePattern (init (fun i -> i.pathname <- Some "/Case/:x"), None, true)

    equal "ignoreCase" true (caseless.test (URLPatternInit (pathname = Some "/CASE/y")))

    let cased =
        service.CreatePattern (init (fun i -> i.pathname <- Some "/Case/:x"), None, false)

    equal "case sensitive" false (cased.test (URLPatternInit (pathname = Some "/CASE/y")))

    fails "a relative pattern without a base" "TypeError" (fun () ->
        service.CreatePattern (text "/relative/:x", None, false)
        |> ignore)

    fails "an unbalanced group" "TypeError" (fun () ->
        service.CreatePattern (init (fun i -> i.pathname <- Some "/(\\d+"), None, false)
        |> ignore)

    fails "a duplicate group name" "TypeError" (fun () ->
        service.CreatePattern (init (fun i -> i.pathname <- Some "/:a/:a"), None, false)
        |> ignore)

    fails "an invalid base" "TypeError" (fun () ->
        service.CreatePattern (text "/x", Some "not a url", false)
        |> ignore)

    // Arguments.
    throws "a null URL" (fun () -> service.Create (null, None) |> ignore)

    equal
        "a Some null base parses as None does"
        (service.Parse ("x", None)
         |> Option.map (fun url -> url.href))
        (service.Parse ("x", Some null)
         |> Option.map (fun url -> url.href))

    equal
        "a Some null base creates as None does"
        (service.Create ("https://a.test/p", None)).href
        (service.Create ("https://a.test/p", Some null)).href

    throws "a null pair" (fun () ->
        service.CreateSearchParams (UrlSearchParamsInit.Pairs [| "a", null |])
        |> ignore)

    throws "a null pattern input" (fun () ->
        service.CreatePattern (text null, None, false)
        |> ignore)

    throws "a null pattern" (fun () -> service.Exec (null, None, None) |> ignore)

    service.Dispose ()
    throws "a disposed service" (fun () -> service.CanParse ("x", None) |> ignore)
    equal "objects outlive the service" "http://a.test/p#f" linked.href
    checks
