module HmrWgpu.Scene

open System
open System.Numerics
open Ranvier
open Ranvier.Dom
open Ranvier.View
open Ranvier.Wgpu
open Ranvier.Wgpu.Sdl
open HmrWgpu.Gpu

type SceneState =
    {
        Angle: Signal<float32>
        Renders: Signal<int>
        Surface: Signal<WgpuSdlSurface option>
        Gpu: GpuProps
    }

/// A module-level value read by setup's render effect.
let backdrop = "#05070b"

let scene: Component<GpuProps> =
    Component.define
        (fun context (gpu: GpuProps) ->
            let angle = Api.createSignal 30.0f
            let renders = Api.createSignal 0
            let surface: Signal<WgpuSdlSurface option> = Api.createSignal None
            // GPU resources belong to this setup run and go with its scope.
            let mesh =
                gpu.Context
                |> WgpuContext.uploadMesh (Primitives.unitIcosphere 3)

            let batch = gpu.Context |> WgpuContext.createOpaqueBatch mesh
            batch.SetSingle (Instance3D.sphere Vector3.Zero 1.6f (RgbaColor.fromHex "#0ea5e9") 0.35f 1u InstanceFlags.None)
            let batches = [| batch |]

            context.OnCleanup (fun () ->
                (batch :> IDisposable).Dispose()
                (mesh :> IDisposable).Dispose())

            Api.createEffect (fun () ->
                match surface.Value with
                | Some target ->
                    ScenePass.create (Camera3D.orbit Vector3.Zero angle.Value 20.0f 5.0f 45.0f)
                    |> ScenePass.withClear (RgbaColor.fromHex backdrop)
                    |> ScenePass.withBatches batches
                    |> WgpuSdlSurface.render target
                    |> ignore

                    renders.Value <- renders.Peek + 1
                | None -> ())

            {
                Angle = angle
                Renders = renders
                Surface = surface
                Gpu = gpu
            })
        (fun t ->
            t.div
                [|
                    t.Attrs.attribute ("id", "scene")
                    t.Attrs.attribute ("style", "position:relative;width:480px;font-family:sans-serif")
                |]
                [|
                    t.canvas
                        [|
                            t.Attrs.attribute ("id", "viewport")
                            t.Attrs.attribute ("style", "display:block;width:480px;height:320px")
                            t.Hooks.mounted (fun s (el: HTMLCanvasElement) ->
                                el.width <- 480u
                                el.height <- 320u
                                s.Surface.Value <- Some (WgpuSdlSurface.bind s.Gpu.Window s.Gpu.Context el)

                                fun () ->
                                    s.Surface.Peek
                                    |> Option.iter WgpuSdlSurface.dispose

                                    s.Surface.Value <- None)
                        |]
                        [||]
                    t.div
                        [|
                            t.Attrs.attribute ("id", "overlay")
                            t.Attrs.attribute ("style", "position:absolute;left:12px;top:8px;color:#fff")
                        |]
                        [|
                            t.h3
                                [|
                                    t.Attrs.attribute ("id", "scene-title")
                                    t.Attrs.attribute ("style", "margin:0")
                                |]
                                [| t.text "Ranvier.Wgpu under HMR" |]
                            t.span [| t.Attrs.attribute ("id", "scene-angle") |] [| t.text (fun s -> $"angle {s.Angle.Value}") |]
                        |]
                    t.button
                        [|
                            t.Attrs.attribute ("id", "rotate")
                            t.Events.on ("click", fun s _ -> s.Angle.Value <- s.Angle.Value + 15.0f)
                        |]
                        [| t.text "rotate" |]
                    t.span [| t.Attrs.attribute ("id", "renders") |] [| t.text (fun s -> $"renders {s.Renders.Value}") |]
                |])
