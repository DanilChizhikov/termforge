using DTech.Core.SavedConnections;
using DTech.Core.Terminal.Sessions;

namespace DTech.Core.Reconnect;

public sealed class ReconnectService(ISavedConnectionRepository repository, ITerminalLauncher launcher, ReconnectScheduler scheduler)
{
    public async Task<ReconnectOutcome> ReconnectAsync(TerminalLaunchRequest request, CancellationToken cancellationToken)
    {
        var connection = await repository.FindAsync(request.ConnectionId, cancellationToken);
        if (connection is null)
        {
            var notFound = new TerminalError(TerminalErrorCode.NotFound, $"Saved connection {request.ConnectionId} was not found.");
            return ReconnectOutcome.Failed(notFound, 0);
        }

        var policy = connection.Details.ReconnectPolicy;
        if (policy.Mode == ReconnectMode.Off)
        {
            var disabled = new TerminalError(TerminalErrorCode.ReconnectDisabled, $"Reconnect is turned off for '{connection.Details.Name}'.");
            return ReconnectOutcome.Failed(disabled, 0);
        }

        Task<TerminalResult<ManagedTerminalSession>> OpenFreshShell(CancellationToken attemptToken)
        {
            return launcher.LaunchAsync(request, attemptToken);
        }

        return await scheduler.RunAsync(policy, OpenFreshShell, cancellationToken);
    }
}
