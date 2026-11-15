/// The earlier file of the fixture: what App.fs uses from another file.
module HmrFixture.Lib

open System

type Page = { Title: string; Body: unit -> string }

type Tone =
    | Plain
    | Bold

/// A function called from App.fs.
let shout (s: string) =
    s.ToUpperInvariant ()

/// A value read by a module-level value of App.fs.
let suffix = "one"

/// Records the published titles in AppDomain data, which every generation of this module shares.
let publish (pages: Page array) =
    AppDomain.CurrentDomain.SetData ("hmr.fixture.published", String.Join ("|", pages |> Array.map _.Title))
    pages
