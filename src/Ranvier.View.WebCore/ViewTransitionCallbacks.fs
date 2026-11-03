namespace Ranvier.View.WebCore

open System.Runtime.InteropServices
open RanvierWebCoreAbi

/// <summary>Native update callbacks whose context is released after the engine drops its last reference.</summary>
module internal ViewTransitionCallbacks =
    type private State(session: Session, update: unit -> unit) =
        member _.Session = session
        member _.Update = update

    let private run =
        Native.ViewTransitionUpdate (fun context ->
            let state = GCHandle.FromIntPtr(context).Target :?> State
            let session = state.Session
            session.ScriptDepth <- session.ScriptDepth + 1

            try
                try
                    state.Update ()
                    0
                with error ->
                    session.ListenerErrors.Add error
                    1
            finally
                session.ScriptDepth <- session.ScriptDepth - 1

                if
                    session.ScriptDepth = 0
                    && not (isNull session.AfterListener)
                then
                    session.AfterListener.Invoke ())

    let private release =
        Native.CustomEffectRelease (fun context -> GCHandle.FromIntPtr(context).Free())

    let private runPointer =
        Marshal.GetFunctionPointerForDelegate<Native.ViewTransitionUpdate> run

    let private releasePointer =
        Marshal.GetFunctionPointerForDelegate<Native.CustomEffectRelease> release

    /// <summary>Returns a caller-owned callback reference for the session's current document.</summary>
    let create (session: Session) (update: unit -> unit) =
        let context = GCHandle.Alloc (State (session, update))

        let handle =
            Native.rv_view_transition_update_callback_create (session.Host, runPointer, GCHandle.ToIntPtr context, releasePointer)

        if handle = 0n then
            context.Free ()
            invalidOp "rv_view_transition_update_callback_create returned NULL"

        handle
