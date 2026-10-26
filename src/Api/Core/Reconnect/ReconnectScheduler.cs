using DTech.Core.Terminal.Sessions;

namespace DTech.Core.Reconnect;

public sealed class ReconnectScheduler(TimeProvider timeProvider)
{
    public async Task<ReconnectOutcome> RunAsync(ReconnectPolicy policy, Func<CancellationToken, Task<TerminalResult<ManagedTerminalSession>>> openShell, CancellationToken cancellationToken)
    {
        var isAutomatic = policy.Mode == ReconnectMode.Automatic;
        var attemptLimit = isAutomatic ? policy.MaxAttempts : 1;
        TerminalError? lastError = null;
        for (var attempt = 1; attempt <= attemptLimit; attempt++)
        {
            if (isAutomatic)
            {
                var delay = ReconnectBackoff.DelayBefore(policy, attempt);
                await Task.Delay(delay, timeProvider, cancellationToken);
            }

            var opened = await openShell(cancellationToken);
            if (opened.IsSuccess)
            {
                return ReconnectOutcome.Opened(opened.Value, attempt);
            }

            lastError = opened.Error;
            if (!ReconnectErrorClassifier.IsTransient(lastError))
            {
                return ReconnectOutcome.Failed(lastError, attempt);
            }
        }

        var message = $"Could not reconnect after {attemptLimit} attempt(s): {lastError!.Message}";
        var exhausted = new TerminalError(TerminalErrorCode.ReconnectFailed, message)
        {
            SshErrorKind = lastError.SshErrorKind,
        };
        return ReconnectOutcome.Failed(exhausted, attemptLimit);
    }
}
