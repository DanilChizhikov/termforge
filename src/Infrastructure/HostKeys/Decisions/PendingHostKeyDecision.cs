using DTech.Core.Connections;

namespace DTech.HostKeys.Decisions;

public sealed class PendingHostKeyDecision(Guid id, HostKeyChallenge challenge, DateTimeOffset expiresAt)
{
    private readonly TaskCompletionSource<HostKeyPromptOutcome> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Guid Id { get; } = id;

    public HostKeyChallenge Challenge { get; } = challenge;

    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    internal Task<HostKeyPromptOutcome> Completion => _completion.Task;

    internal bool TryComplete(HostKeyPromptOutcome outcome)
    {
        return _completion.TrySetResult(outcome);
    }
}
