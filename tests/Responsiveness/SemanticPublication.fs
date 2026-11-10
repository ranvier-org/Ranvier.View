module Responsiveness.SemanticPublication

open System

/// <summary>Worker result identity; DocumentId is a fresh open-document generation.</summary>
type Revision =
    {
        DocumentId: Guid
        TextRevision: int64
        ProjectRevision: int64
        RequestId: int64
    }

type Result =
    {
        Revision: Revision
        VisibleColor: string
    }

// Call on the owner at delivery, even when the worker supports cancellation.
let isCurrent (current: Revision) (result: Result) =
    current = result.Revision
