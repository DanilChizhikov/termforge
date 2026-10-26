using DTech.Core.SavedConnections;

namespace DTech.Core.Terminal.Sessions;

public sealed class TerminalLauncher(ISavedConnectionRepository repository, ConnectionTargetFactory targetFactory, TerminalSessionManager sessionManager)
{
    public async Task<TerminalResult<ManagedTerminalSession>> LaunchAsync(TerminalLaunchRequest request, CancellationToken cancellationToken)
    {
        var all = await repository.ListAsync(cancellationToken);
        var connection = JumpHostGuard.Find(all, request.ConnectionId);
        if (connection is null)
        {
            var notFound = new TerminalError(TerminalErrorCode.NotFound, $"Saved connection {request.ConnectionId} was not found.");
            return TerminalResult<ManagedTerminalSession>.Failure(notFound);
        }

        var target = await targetFactory.CreateAsync(connection, all, cancellationToken);
        if (!target.IsSuccess)
        {
            var unavailable = new TerminalError(TerminalErrorCode.CredentialsUnavailable, target.Error.Message);
            return TerminalResult<ManagedTerminalSession>.Failure(unavailable);
        }

        var server = new TerminalServer(connection.Id, connection.Details.Name, target.Value);
        var openRequest = new TerminalOpenRequest(request.OwnerKey, server, request.InitialSize);
        var opened = await sessionManager.OpenAsync(openRequest, cancellationToken);
        if (opened.IsSuccess)
        {
            await repository.RecordConnectedAsync(connection.Id, opened.Value.Descriptor.OpenedAt, cancellationToken);
        }

        return opened;
    }
}
