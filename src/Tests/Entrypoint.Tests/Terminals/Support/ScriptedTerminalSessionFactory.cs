using System.Collections.Concurrent;
using DTech.Core.Connections;
using DTech.Core.Terminal;

namespace DTech.Entrypoint.Tests.Terminals.Support;

internal sealed class ScriptedTerminalSessionFactory : ITerminalSessionFactory
{
    private readonly ConcurrentQueue<ScriptedTerminalSession> _sessions = new();
    private readonly ConcurrentQueue<TerminalSessionRequest> _requests = new();

    public IReadOnlyList<ScriptedTerminalSession> Sessions => _sessions.ToList();

    public IReadOnlyList<TerminalSessionRequest> Requests => _requests.ToList();

    public Task<SshResult<ITerminalSession>> OpenAsync(TerminalSessionRequest request, CancellationToken cancellationToken)
    {
        var session = new ScriptedTerminalSession();
        _requests.Enqueue(request);
        _sessions.Enqueue(session);
        var opened = SshResult<ITerminalSession>.Success(session);
        return Task.FromResult(opened);
    }
}
