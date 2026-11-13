module HmrWgpu.Program

open Ranvier.Wgpu.Sdl
open HmrWgpu.Gpu

[<EntryPoint>]
let main argv =
    // The context is built once per window and survives every reload.
    Ranvier.View.WebCore.Hmr.Dev.RunWith (
        argv,
        "Ranvier.Wgpu under HMR",
        640,
        480,
        Scene.scene,
        fun window ->
            {
                Window = window
                Context = WgpuSdlSurface.createContextSized 480 320 window
            }
    )
