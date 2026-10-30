namespace DTech.Core.AiBridge.Approvals;

public sealed class PendingAiApproval(Guid id, AiActionDescription action, DateTimeOffset expiresAt)
{
    private readonly TaskCompletionSource<AiApprovalOutcome> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Guid Id { get; } = id;

    public AiActionDescription Action { get; } = action;

    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    internal Task<AiApprovalOutcome> Completion => _completion.Task;

    internal bool TryComplete(AiApprovalOutcome outcome)
    {
        return _completion.TrySetResult(outcome);
    }
}
