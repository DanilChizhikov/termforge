using DTech.Core.Connections;
using DTech.Core.Diagnostics;

namespace DTech.Core.Tests.Diagnostics.Support;

internal sealed class ScriptedCommandRunner : IRemoteCommandRunner
{
    private readonly List<(string Fragment, SshResult<RemoteCommandResult> Result)> _responses = [];

    public List<string> ExecutedCommands { get; } = [];

    public bool Disposed { get; private set; }

    public ScriptedCommandRunner Respond(string commandFragment, int exitCode, string standardOutput = "", string standardError = "")
    {
        var result = new RemoteCommandResult(exitCode, standardOutput, standardError);
        _responses.Add((commandFragment, SshResult<RemoteCommandResult>.Success(result)));
        return this;
    }

    public ScriptedCommandRunner Fail(string commandFragment, SshErrorKind kind, string message)
    {
        var error = new SshError(kind, message);
        _responses.Add((commandFragment, SshResult<RemoteCommandResult>.Failure(error)));
        return this;
    }

    public Task<SshResult<RemoteCommandResult>> RunAsync(RemoteCommandRequest request, CancellationToken cancellationToken)
    {
        ExecutedCommands.Add(request.Command);
        for (var i = 0; i < _responses.Count; i++)
        {
            var response = _responses[i];
            if (request.Command.Contains(response.Fragment, StringComparison.Ordinal))
            {
                return Task.FromResult(response.Result);
            }
        }

        var notFound = new RemoteCommandResult(127, string.Empty, "sh: not found");
        return Task.FromResult(SshResult<RemoteCommandResult>.Success(notFound));
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
