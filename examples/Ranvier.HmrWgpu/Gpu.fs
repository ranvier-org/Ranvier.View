/// The root's props. They live in a file that reloads do not redefine, so every new setup receives the original
/// props object and the GPU context built for the window.
module HmrWgpu.Gpu

open System
open Ranvier.View.WebCore.Sdl
open Ranvier.Wgpu

type GpuProps =
    {
        Window: Window
        Context: WgpuContext
    }

    // Props built by Dev.RunWith belong to the host, which disposes them after the tree and before the window.
    interface IDisposable with
        member this.Dispose() =
            WgpuContext.dispose this.Context
