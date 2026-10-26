namespace DTech.Core.Files.Search;

public sealed class RemoteFindLauncher(IRemoteCommandRunnerOpener runnerOpener, RemoteFindAvailability availability)
{
    public async Task<RemoteFindLaunch> LaunchAsync(Guid connectionId, FileSearchQuery query, CancellationToken cancellationToken)
    {
        var opened = await runnerOpener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            var connectionFailure = $"remote command execution failed ({opened.Error.Message})";
            return RemoteFindLaunch.Unavailable(connectionFailure);
        }

        var runner = opened.Value;
        var unavailableReason = await availability.FindUnavailableReasonAsync(connectionId, runner, cancellationToken);
        if (unavailableReason is not null)
        {
            await runner.DisposeAsync();
            return RemoteFindLaunch.Unavailable(unavailableReason);
        }

        var execution = new RemoteFindSearchExecution(runner, query);
        return RemoteFindLaunch.Started(execution);
    }
}
