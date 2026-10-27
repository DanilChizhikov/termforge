using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.Workspace;

public sealed class WorkspaceStateReadResult
{
    private WorkspaceStateReadResult(WorkspaceState? state, WorkspaceRestoreWarning? warning)
    {
        State = state;
        Warning = warning;
    }

    [MemberNotNullWhen(true, nameof(State))]
    [MemberNotNullWhen(false, nameof(Warning))]
    public bool IsSuccess => Warning is null;

    public WorkspaceState? State { get; }

    public WorkspaceRestoreWarning? Warning { get; }

    public static WorkspaceStateReadResult Success(WorkspaceState state)
    {
        return new WorkspaceStateReadResult(state, null);
    }

    public static WorkspaceStateReadResult Failure(WorkspaceRestoreWarning warning)
    {
        return new WorkspaceStateReadResult(null, warning);
    }
}
