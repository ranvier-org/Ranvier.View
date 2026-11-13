module HmrCounter.Program

[<EntryPoint>]
let main argv =
    Ranvier.View.WebCore.Hmr.Dev.Run (argv, "Ranvier HMR", 640, 480, App.app, ())
