using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.Workspace;

public sealed class WorkspaceSaveResult
{
    private WorkspaceSaveResult(WorkspaceStateError? error)
    {
        Error = error;
    }

    public static WorkspaceSaveResult Saved { get; } = new(null);

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public WorkspaceStateError? Error { get; }

    public static WorkspaceSaveResult Rejected(WorkspaceStateError error)
    {
        return new WorkspaceSaveResult(error);
    }
}
