namespace Ranvier.View.WebCore

open System
open System.Text

/// <summary>Availability of a bounded control context for a native input method.</summary>
type ImeContextState =
    | Absent = 0
    | Available = 1
    | Unavailable = 2
    | Secure = 3

/// <summary>Text with a UTF-16 document origin and window-relative UTF-16 cursor and anchor.</summary>
/// <remarks>The token belongs to this host's latest successful read and expires when editing context changes.</remarks>
type ImeContextSnapshot =
    {
        Text: string
        Origin: int
        Cursor: int
        Anchor: int
        State: ImeContextState
        Token: uint64
    }

    override this.ToString() = this.Text

/// <summary>Bounded native input-method context for supported text controls.</summary>
module ImeContext =
    /// <summary>Reads at most maxUnits UTF-16 units; unsupported structure or composition reports Unavailable.</summary>
    /// <param name="host">The active host, accessed on its owner thread.</param>
    /// <param name="maxUnits">Between 1 and 4096 UTF-16 units, including both selection ends.</param>
    /// <returns>ValueNone when the native runtime lacks the capability.</returns>
    let read (host: WebCoreHost) maxUnits =
        host.EnsureActive ()

        if maxUnits < 1 || maxUnits > 4096 then
            invalidArg (nameof maxUnits) "Expected 1..4096 UTF-16 units"

        if not (WebCoreHost.Has "ime-surrounding-window-v1") then
            ValueNone
        else
            let bytes = Array.zeroCreate<byte>(maxUnits * 3)
            let length = [| 0 |]
            let mutable origin, cursor, anchor, state = 0, 0, 0, 0
            let mutable token = 0UL

            OptionalNative.rv_host_ime_surrounding_window (
                host.Handle,
                maxUnits,
                bytes,
                bytes.Length,
                length,
                &origin,
                &cursor,
                &anchor,
                &state,
                &token
            )
            |> Interop.check "ime surrounding window"

            ValueSome
                {
                    Text =
                        if state = 1 then
                            Encoding.UTF8.GetString (bytes, 0, length[0])
                        else
                            ""
                    Origin = origin
                    Cursor = cursor
                    Anchor = anchor
                    State = enum<ImeContextState> state
                    Token = token
                }

    /// <summary>Deletes a window-relative UTF-16 range; stale or unavailable contexts return false.</summary>
    /// <remarks>Deletion uses the existing undoable edit transaction and may take time proportional to control size.</remarks>
    let tryDelete (host: WebCoreHost) token start finish =
        host.EnsureActive ()

        if not (WebCoreHost.Has "ime-surrounding-window-v1") then
            false
        else
            let status =
                OptionalNative.rv_host_ime_delete_surrounding_window (host.Handle, token, start, finish)

            if status = -10 || status = -9 then
                false
            else
                Interop.check "ime window deletion" status
                status > 0
