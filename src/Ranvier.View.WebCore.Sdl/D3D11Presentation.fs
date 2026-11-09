namespace Ranvier.View.WebCore.Sdl

open System
open System.Runtime.InteropServices
open System.Threading
open SDL

module private D3D11Native =
    [<Literal>]
    let FormatRgba8 = 28u

    [<Literal>]
    let DeviceRemoved = 0x887A0005

    [<Literal>]
    let DeviceHung = 0x887A0006

    [<Literal>]
    let DeviceReset = 0x887A0007

    [<Literal>]
    let DriverInternalError = 0x887A0020

    [<Literal>]
    let InvalidCall = 0x887A0001

    [<Literal>]
    let Occluded = 0x087A0001

    let dxgiDeviceId = Guid "77db970f-6276-48ba-ba28-070143b4392c"
    let dxgiFactory2Id = Guid "50c83a1c-e072-4c48-87b0-3630fa36a6d0"
    let texture2DId = Guid "6f15aaf2-d208-4e89-9ab4-489535d34f9c"

    [<Struct; StructLayout(LayoutKind.Sequential); NoEquality; NoComparison>]
    type SwapChainDesc =
        {
            Width: uint32
            Height: uint32
            Format: uint32
            Stereo: int
            SampleCount: uint32
            SampleQuality: uint32
            BufferUsage: uint32
            BufferCount: uint32
            Scaling: int
            SwapEffect: int
            AlphaMode: int
            Flags: uint32
        }

        // A record's generated ToString formats with %+A, which native AOT rejects.
        override _.ToString() =
            "SwapChainDesc"

    [<Struct; StructLayout(LayoutKind.Sequential); NoEquality; NoComparison>]
    type TextureDesc =
        {
            Width: uint32
            Height: uint32
            MipLevels: uint32
            ArraySize: uint32
            Format: uint32
            SampleCount: uint32
            SampleQuality: uint32
            Usage: int
            BindFlags: uint32
            CpuAccessFlags: uint32
            MiscFlags: uint32
        }

        // A record's generated ToString formats with %+A, which native AOT rejects.
        override _.ToString() =
            "TextureDesc"

    [<Struct; StructLayout(LayoutKind.Sequential); NoEquality; NoComparison>]
    type Box =
        {
            Left: uint32
            Top: uint32
            Front: uint32
            Right: uint32
            Bottom: uint32
            Back: uint32
        }

        // A record's generated ToString formats with %+A, which native AOT rejects.
        override _.ToString() = "Box"

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type Mapped =
        val mutable Data: nativeint
        val mutable RowPitch: uint32
        val mutable DepthPitch: uint32

    type GetAdapter = delegate of nativeint * byref<nativeint> -> int
    type SetFrameLatency = delegate of nativeint * uint32 -> int
    type GetObject = delegate of nativeint * byref<Guid> * byref<nativeint> -> int
    type GetBuffer = delegate of nativeint * uint32 * byref<Guid> * byref<nativeint> -> int
    type CreateSwapChain = delegate of nativeint * nativeint * nativeint * byref<SwapChainDesc> * nativeint * nativeint * byref<nativeint> -> int
    type MakeWindowAssociation = delegate of nativeint * nativeint * uint32 -> int
    type CreateTexture = delegate of nativeint * byref<TextureDesc> * nativeint * byref<nativeint> -> int
    type CreateTargetView = delegate of nativeint * nativeint * nativeint * byref<nativeint> -> int
    type Present = delegate of nativeint * uint32 * uint32 -> int
    type ResizeBuffers = delegate of nativeint * uint32 * uint32 * uint32 * uint32 * uint32 -> int
    type ContextCall = delegate of nativeint -> unit
    type CopyResource = delegate of nativeint * nativeint * nativeint -> unit
    type CopyRegion = delegate of nativeint * nativeint * uint32 * uint32 * uint32 * uint32 * nativeint * uint32 * byref<Box> -> unit
    type UpdateResource = delegate of nativeint * nativeint * uint32 * nativeint * nativeint * uint32 * uint32 -> unit
    type ClearView = delegate of nativeint * nativeint * nativeint -> unit
    type Map = delegate of nativeint * nativeint * uint32 * int * uint32 * byref<Mapped> -> int
    type Unmap = delegate of nativeint * nativeint * uint32 -> unit

    [<DllImport("d3d11.dll")>]
    extern int D3D11CreateDevice(
        nativeint adapter,
        int driverType,
        nativeint software,
        uint32 flags,
        nativeint featureLevels,
        uint32 featureLevelCount,
        uint32 sdkVersion,
        nativeint& device,
        int& featureLevel,
        nativeint& context
    )

    /// <summary>The COM method at <c>slot</c> in the vtable of <c>this</c>.</summary>
    let inline comMethod<'Method when 'Method :> Delegate> (this: nativeint) (slot: int) : 'Method =
        Marshal.GetDelegateForFunctionPointer<'Method>(Marshal.ReadIntPtr (Marshal.ReadIntPtr this, slot * IntPtr.Size))

    let drop (value: nativeint) =
        if value <> 0n then
            Marshal.Release value |> ignore

/// <summary>
/// A D3D11 device and flip-model swap chain on a window, with a thread that copies a frame to the back buffer and
/// presents it. A present that blocks holds only that thread.
/// </summary>
/// <remarks>
/// The device and its context belong to the present thread while <c>Presenting</c> is true and to the caller's thread
/// otherwise. All members except <c>Presenting</c> run on the thread that owns the window; members that use the device
/// require <c>Presenting</c> to be false, which <c>WaitIdle</c> guarantees.
/// </remarks>
[<Sealed>]
type internal D3D11SwapChain private (hwnd: nativeint, wakeEvent: uint32) =
    let mutable device = 0n
    let mutable context = 0n
    let mutable swapChain = 0n
    let mutable backBuffer = 0n
    let mutable targetView = 0n
    let mutable backWidth = 0
    let mutable backHeight = 0
    let mutable wantedWidth = 0
    let mutable wantedHeight = 0
    let mutable cpuTexture = 0n
    let mutable cpuWidth = 0
    let mutable cpuHeight = 0
    let mutable lost = false
    let mutable disposed = false
    let mutable presentCall: D3D11Native.Present = null
    let mutable copyResource: D3D11Native.CopyResource = null
    let mutable copyRegion: D3D11Native.CopyRegion = null
    let mutable clearView: D3D11Native.ClearView = null
    let mutable flush: D3D11Native.ContextCall = null
    // Opaque black, pinned for ClearRenderTargetView across every frame whose size differs from the back buffer.
    let black = GCHandle.Alloc ([| 0.0f; 0.0f; 0.0f; 1.0f |], GCHandleType.Pinned)
    let go = new SemaphoreSlim (0)
    let idle = new ManualResetEventSlim (true)
    let mutable stopping = false
    let mutable source = 0n
    let mutable sourceWidth = 0
    let mutable sourceHeight = 0
    let mutable visible = true
    let mutable refreshMs = 16
    let mutable presentResult = 0
    let mutable failure: exn = null
    let mutable presentWrapper: (unit -> int) -> int = Unchecked.defaultof<_>
    let mutable worker: Thread = null

    let releaseBuffers () =
        D3D11Native.drop targetView
        D3D11Native.drop backBuffer
        targetView <- 0n
        backBuffer <- 0n

        if context <> 0n then
            // Slots 110 and 111: ClearState and Flush. The swap chain's buffers stay referenced until both have run.
            (D3D11Native.comMethod<D3D11Native.ContextCall> context 110).Invoke context
            (D3D11Native.comMethod<D3D11Native.ContextCall> context 111).Invoke context

    let createBuffers () =
        let mutable id = D3D11Native.texture2DId

        let buffer =
            let mutable value = 0n

            if
                (D3D11Native.comMethod<D3D11Native.GetBuffer> swapChain 9).Invoke(swapChain, 0u, &id, &value)
                >= 0
            then
                value
            else
                0n

        backBuffer <- buffer

        if buffer = 0n then
            false
        else
            let mutable view = 0n

            let created =
                (D3D11Native.comMethod<D3D11Native.CreateTargetView> device 9).Invoke(device, buffer, 0n, &view)
                >= 0

            targetView <- view
            created

    let teardown () =
        releaseBuffers ()
        D3D11Native.drop cpuTexture
        cpuTexture <- 0n
        cpuWidth <- 0
        cpuHeight <- 0
        D3D11Native.drop swapChain
        swapChain <- 0n
        D3D11Native.drop context
        context <- 0n
        D3D11Native.drop device
        device <- 0n

    let createSwapChain () =
        let mutable dxgiDevice = 0n
        let mutable adapter = 0n
        let mutable factory = 0n

        try
            let mutable deviceId = D3D11Native.dxgiDeviceId
            let mutable factoryId = D3D11Native.dxgiFactory2Id

            if
                Marshal.QueryInterface (device, &deviceId, &dxgiDevice) < 0
                // IDXGIDevice1::SetMaximumFrameLatency. A longer queue returns from Present early and moves the wait
                // for the GPU to the shared texture's keyed mutex, on the window's thread.
                || (D3D11Native.comMethod<D3D11Native.SetFrameLatency> dxgiDevice 12).Invoke(dxgiDevice, 1u) < 0
                || (D3D11Native.comMethod<D3D11Native.GetAdapter> dxgiDevice 7).Invoke(dxgiDevice, &adapter) < 0
                || (D3D11Native.comMethod<D3D11Native.GetObject> adapter 6).Invoke(adapter, &factoryId, &factory) < 0
            then
                false
            else
                let mutable desc: D3D11Native.SwapChainDesc =
                    {
                        Width = uint32 wantedWidth
                        Height = uint32 wantedHeight
                        Format = D3D11Native.FormatRgba8
                        Stereo = 0
                        SampleCount = 1u
                        SampleQuality = 0u
                        // DXGI_USAGE_RENDER_TARGET_OUTPUT.
                        BufferUsage = 0x20u
                        BufferCount = 2u
                        // DXGI_SCALING_NONE, DXGI_SWAP_EFFECT_FLIP_DISCARD and DXGI_ALPHA_MODE_IGNORE.
                        Scaling = 1
                        SwapEffect = 4
                        AlphaMode = 3
                        Flags = 0u
                    }

                let mutable created = 0n

                if (D3D11Native.comMethod<D3D11Native.CreateSwapChain> factory 15).Invoke(factory, device, hwnd, &desc, 0n, 0n, &created) < 0 then
                    false
                else
                    swapChain <- created
                    // DXGI_MWA_NO_WINDOW_CHANGES: SDL owns the window's messages.
                    (D3D11Native.comMethod<D3D11Native.MakeWindowAssociation> factory 8).Invoke(factory, hwnd, 1u)
                    |> ignore

                    true
        finally
            D3D11Native.drop factory
            D3D11Native.drop adapter
            D3D11Native.drop dxgiDevice

    let build () =
        let mutable created = 0n
        let mutable immediate = 0n
        let mutable level = 0

        // D3D_DRIVER_TYPE_HARDWARE on the default adapter, as WebCore's producer device uses; D3D11_SDK_VERSION is 7.
        if D3D11Native.D3D11CreateDevice (0n, 1, 0n, 0u, 0n, 0u, 7u, &created, &level, &immediate) < 0 then
            false
        else
            device <- created
            context <- immediate

            if createSwapChain () && createBuffers () then
                backWidth <- wantedWidth
                backHeight <- wantedHeight
                presentCall <- D3D11Native.comMethod<D3D11Native.Present> swapChain 8
                copyRegion <- D3D11Native.comMethod<D3D11Native.CopyRegion> context 46
                copyResource <- D3D11Native.comMethod<D3D11Native.CopyResource> context 47
                clearView <- D3D11Native.comMethod<D3D11Native.ClearView> context 50
                flush <- D3D11Native.comMethod<D3D11Native.ContextCall> context 111
                true
            else
                teardown ()
                false

    let copyToBackBuffer () =
        if
            sourceWidth = backWidth
            && sourceHeight = backHeight
        then
            copyResource.Invoke (context, backBuffer, source)
        else
            let mutable area: D3D11Native.Box =
                {
                    Left = 0u
                    Top = 0u
                    Front = 0u
                    Right = uint32 (min sourceWidth backWidth)
                    Bottom = uint32 (min sourceHeight backHeight)
                    Back = 1u
                }

            clearView.Invoke (context, targetView, black.AddrOfPinnedObject ())
            copyRegion.Invoke (context, backBuffer, 0u, 0u, 0u, 0u, source, 0u, &area)

    let showFrame () =
        if visible then
            presentCall.Invoke (swapChain, 1u, 0u)
        else
            // A hidden or minimized window has no vertical blank to wait for.
            flush.Invoke context
            Thread.Sleep refreshMs
            0

    let presentLoop () =
        go.Wait ()

        while not (Volatile.Read &stopping) do
            try
                copyToBackBuffer ()

                let wrapper = presentWrapper

                let result =
                    if isNull (box wrapper) then
                        showFrame ()
                    else
                        wrapper showFrame

                if result = D3D11Native.Occluded then
                    Thread.Sleep refreshMs

                presentResult <- result
            with error ->
                failure <- error

            idle.Set ()
            let mutable wake = SDL_Event ()
            wake.``type`` <- wakeEvent
            SDL3.SDL_PushEvent &&wake |> ignore
            go.Wait ()

    /// <summary>A swap chain on the window, or <c>None</c> where D3D11 hardware or flip-model presentation is unavailable.</summary>
    static member TryCreate(hwnd: nativeint, width: int, height: int) =
        let chain = new D3D11SwapChain (hwnd, SDL3.SDL_RegisterEvents 1)

        if chain.Start (width, height) then
            Some chain
        else
            (chain :> IDisposable).Dispose()
            None

    member private _.Start(width: int, height: int) =
        wantedWidth <- max 1 width
        wantedHeight <- max 1 height

        if build () then
            worker <- Thread ((fun () -> presentLoop ()), IsBackground = true, Name = "Ranvier present")
            worker.Start ()
            true
        else
            false

    /// <summary>The device for <c>rv_host_acquire_d3d11_frame</c>; it changes after a lost device is rebuilt.</summary>
    member _.Device = device

    /// <summary>Whether the present thread owns the device.</summary>
    member _.Presenting = not idle.IsSet

    /// <summary>Replaces the present call; a test wraps it to block or to report a failure.</summary>
    member _.SetPresentWrapper(wrapper: (unit -> int) -> int) =
        presentWrapper <- wrapper

    /// <summary>Sets the back buffer size that the next <c>Prepare</c> applies.</summary>
    member _.Resize(width: int, height: int) =
        wantedWidth <- width
        wantedHeight <- height

    /// <summary>Sets how later frames reach the window: a hidden one sleeps for <c>interval</c> milliseconds in place of a present.</summary>
    member _.Pace(isVisible: bool, interval: int) =
        visible <- isVisible
        refreshMs <- interval

    /// <summary>
    /// Takes the finished present's outcome: true when it lost the device. Raises the present thread's failure. False
    /// while a present is running.
    /// </summary>
    member _.Settle() =
        if idle.IsSet then
            if not (isNull failure) then
                let error = failure
                failure <- null
                raise (InvalidOperationException ("The present thread failed: " + error.Message, error))

            let result = presentResult
            presentResult <- 0

            match result with
            | D3D11Native.DeviceRemoved
            | D3D11Native.DeviceHung
            | D3D11Native.DeviceReset
            | D3D11Native.DriverInternalError
            | D3D11Native.InvalidCall ->
                lost <- true
                true
            | _ -> false
        else
            false

    /// <summary>Waits for a running present while pumping SDL, because DXGI can send this thread's window a message during one.</summary>
    member _.WaitIdle() =
        while not (idle.Wait 1) do
            SDL3.SDL_PumpEvents ()

    /// <summary>Applies a resize and rebuilds a lost device; false leaves nothing to present to, for at least this frame.</summary>
    member _.Prepare() =
        if lost then
            teardown ()
            lost <- not (build ())

        if
            not lost
            && wantedWidth > 0
            && wantedHeight > 0
            && (wantedWidth <> backWidth
                || wantedHeight <> backHeight)
        then
            releaseBuffers ()

            // Zero count and DXGI_FORMAT_UNKNOWN keep the swap chain's buffer count and format.
            if
                (D3D11Native.comMethod<D3D11Native.ResizeBuffers> swapChain 13).Invoke(swapChain, 0u, uint32 wantedWidth, uint32 wantedHeight, 0u, 0u)
                >= 0
                && createBuffers ()
            then
                backWidth <- wantedWidth
                backHeight <- wantedHeight
            else
                lost <- true

        // A device that cannot be rebuilt yet is retried at the display's rate, not as fast as the loop turns.
        if lost then
            Thread.Sleep refreshMs

        not lost

    /// <summary>Starts copying <c>texture</c> to the back buffer and presenting it on the present thread.</summary>
    member _.Submit(texture: nativeint, width: int, height: int) =
        source <- texture
        sourceWidth <- width
        sourceHeight <- height
        idle.Reset ()
        go.Release () |> ignore

    /// <summary>The texture holding <c>pixels</c>, tightly packed RGBA at the given size.</summary>
    member _.UploadCpu(pixels: byte array, width: int, height: int) =
        if
            cpuTexture = 0n
            || cpuWidth <> width
            || cpuHeight <> height
        then
            D3D11Native.drop cpuTexture
            cpuTexture <- 0n

            let mutable desc: D3D11Native.TextureDesc =
                {
                    Width = uint32 width
                    Height = uint32 height
                    MipLevels = 1u
                    ArraySize = 1u
                    Format = D3D11Native.FormatRgba8
                    SampleCount = 1u
                    SampleQuality = 0u
                    Usage = 0
                    BindFlags = 0u
                    CpuAccessFlags = 0u
                    MiscFlags = 0u
                }

            let mutable created = 0n

            if (D3D11Native.comMethod<D3D11Native.CreateTexture> device 5).Invoke(device, &desc, 0n, &created) < 0 then
                invalidOp "ID3D11Device::CreateTexture2D failed"

            cpuTexture <- created
            cpuWidth <- width
            cpuHeight <- height

        let pin = GCHandle.Alloc (pixels, GCHandleType.Pinned)

        try
            (D3D11Native.comMethod<D3D11Native.UpdateResource> context 48)
                .Invoke(context, cpuTexture, 0u, 0n, pin.AddrOfPinnedObject (), uint32 (width * 4), 0u)
        finally
            pin.Free ()

        cpuTexture

    /// <summary>The tightly packed RGBA bytes of <c>texture</c>, top down, on the thread that owns the device.</summary>
    member _.Read(texture: nativeint, width: int, height: int) =
        let mutable desc: D3D11Native.TextureDesc =
            {
                Width = uint32 width
                Height = uint32 height
                MipLevels = 1u
                ArraySize = 1u
                Format = D3D11Native.FormatRgba8
                SampleCount = 1u
                SampleQuality = 0u
                // D3D11_USAGE_STAGING with D3D11_CPU_ACCESS_READ.
                Usage = 3
                BindFlags = 0u
                CpuAccessFlags = 0x20000u
                MiscFlags = 0u
            }

        let mutable staging = 0n

        if (D3D11Native.comMethod<D3D11Native.CreateTexture> device 5).Invoke(device, &desc, 0n, &staging) < 0 then
            invalidOp "ID3D11Device::CreateTexture2D failed"

        try
            copyResource.Invoke (context, staging, texture)
            let mutable mapped = D3D11Native.Mapped ()

            // D3D11_MAP_READ.
            if (D3D11Native.comMethod<D3D11Native.Map> context 14).Invoke(context, staging, 0u, 1, 0u, &mapped) < 0 then
                invalidOp "ID3D11DeviceContext::Map failed"

            try
                let bytes = Array.zeroCreate<byte>(width * height * 4)

                for row in 0 .. height - 1 do
                    Marshal.Copy (
                        mapped.Data
                        + nativeint (row * int mapped.RowPitch),
                        bytes,
                        row * width * 4,
                        width * 4
                    )

                bytes
            finally
                (D3D11Native.comMethod<D3D11Native.Unmap> context 15).Invoke(context, staging, 0u)
        finally
            D3D11Native.drop staging

    interface IDisposable with
        member this.Dispose() =
            if not disposed then
                disposed <- true
                this.WaitIdle ()

                if not (isNull worker) then
                    Volatile.Write (&stopping, true)
                    go.Release () |> ignore
                    worker.Join ()

                teardown ()
                black.Free ()
                go.Dispose ()
                idle.Dispose ()
