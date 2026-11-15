module HmrFixture.Program

[<EntryPoint>]
let main argv =
    Ranvier.View.WebCore.Hmr.Dev.Run (argv, "Ranvier HMR fixture", 320, 240, App.app, ())
