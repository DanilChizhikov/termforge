using DTech.Core.Connections;

namespace DTech.HostKeys.Decisions;

public sealed class PendingHostKeyDecision(Guid id, HostKeyChallenge challenge, DateTimeOffset expiresAt)
{
    private readonly TaskCompletionSource<HostKeyPromptOutcome> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _gate = new();
    private bool _claimed;

    public Guid Id { get; } = id;

    public HostKeyChallenge Challenge { get; } = challenge;

    public DateTimeOffset ExpiresAt { get; } = expiresAt;

    internal Task<HostKeyPromptOutcome> Completion => _completion.Task;

    internal bool TryClaim(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_claimed || _completion.Task.IsCompleted)
            {
                return false;
            }

            if (now >= ExpiresAt)
            {
                _completion.TrySetResult(HostKeyPromptOutcome.TimedOut);
                return false;
            }

            _claimed = true;
            return true;
        }
    }

    internal void Expire()
    {
        lock (_gate)
        {
            if (!_claimed)
            {
                _completion.TrySetResult(HostKeyPromptOutcome.TimedOut);
            }
        }
    }

    internal void Cancel(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_claimed)
            {
                _completion.TrySetCanceled(cancellationToken);
            }
        }
    }

    internal bool TryComplete(HostKeyPromptOutcome outcome)
    {
        return _completion.TrySetResult(outcome);
    }
}
