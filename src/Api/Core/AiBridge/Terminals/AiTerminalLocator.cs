using DTech.Core.Terminal.Sessions;

namespace DTech.Core.AiBridge.Terminals;

public sealed class AiTerminalLocator(TerminalSessionManager sessionManager, AiServerAccessPolicy accessPolicy)
{
    public async Task<AiBridgeResult<IReadOnlyList<TerminalSessionDescriptor>>> ListAsync(CancellationToken cancellationToken)
    {
        var accessible = await accessPolicy.ListAccessibleAsync(cancellationToken);
        if (!accessible.IsSuccess)
        {
            return AiBridgeResult<IReadOnlyList<TerminalSessionDescriptor>>.Failure(accessible.Error);
        }

        var connectionIds = new HashSet<Guid>();
        for (var i = 0; i < accessible.Value.Count; i++)
        {
            connectionIds.Add(accessible.Value[i].Id);
        }

        var all = sessionManager.ListAll();
        var terminals = new List<TerminalSessionDescriptor>();
        for (var i = 0; i < all.Count; i++)
        {
            if (connectionIds.Contains(all[i].ConnectionId))
            {
                terminals.Add(all[i]);
            }
        }

        return AiBridgeResult<IReadOnlyList<TerminalSessionDescriptor>>.Success(terminals);
    }

    public async Task<AiBridgeResult<AiTerminalTarget>> FindAsync(string? terminalId, bool requiresWrite, CancellationToken cancellationToken)
    {
        var session = FindOpenSession(terminalId);
        if (session is null)
        {
            var missing = new AiBridgeError(AiBridgeErrorCode.NotFound, $"Terminal {terminalId} is not open. Call list_terminals to see the open terminals.");
            return AiBridgeResult<AiTerminalTarget>.Failure(missing);
        }

        var connectionId = session.Descriptor.ConnectionId;
        var access = requiresWrite
            ? await accessPolicy.CheckWritableAsync(connectionId, cancellationToken)
            : await accessPolicy.CheckAsync(connectionId, cancellationToken);
        if (!access.IsSuccess)
        {
            return AiBridgeResult<AiTerminalTarget>.Failure(access.Error);
        }

        var target = new AiTerminalTarget(session, access.Value);
        return AiBridgeResult<AiTerminalTarget>.Success(target);
    }

    private ManagedTerminalSession? FindOpenSession(string? terminalId)
    {
        if (string.IsNullOrWhiteSpace(terminalId))
        {
            return null;
        }

        var sessionId = new TerminalSessionId(terminalId);
        var session = sessionManager.FindAnyOwner(sessionId);
        return session is null || session.IsClosed ? null : session;
    }
}
