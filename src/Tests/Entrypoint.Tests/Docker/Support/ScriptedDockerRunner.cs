using System.Collections.Concurrent;
using DTech.Core.Connections;
using DTech.Core.Diagnostics;

namespace DTech.Entrypoint.Tests.Docker.Support;

internal sealed class ScriptedDockerRunner : IRemoteCommandRunner, IRemoteCommandRunnerFactory
{
    private readonly ConcurrentQueue<(string Fragment, RemoteCommandResult Result)> _responses = new();
    private readonly ConcurrentQueue<string> _executed = new();

    public IReadOnlyList<string> ExecutedCommands => _executed.ToList();

    public ScriptedDockerRunner Respond(string fragment, int exitCode, string standardOutput, string standardError = "")
    {
        var result = new RemoteCommandResult(exitCode, standardOutput, standardError);
        _responses.Enqueue((fragment, result));
        return this;
    }

    public Task<SshResult<IRemoteCommandRunner>> ConnectAsync(SshConnectionTarget target, CancellationToken cancellationToken)
    {
        var connected = SshResult<IRemoteCommandRunner>.Success(this);
        return Task.FromResult(connected);
    }

    public Task<SshResult<RemoteCommandResult>> RunAsync(RemoteCommandRequest request, CancellationToken cancellationToken)
    {
        _executed.Enqueue(request.Command);
        foreach (var response in _responses)
        {
            if (request.Command.Contains(response.Fragment, StringComparison.Ordinal))
            {
                var matched = SshResult<RemoteCommandResult>.Success(response.Result);
                return Task.FromResult(matched);
            }
        }

        var notFoundResult = new RemoteCommandResult(127, string.Empty, "sh: not found");
        var notFound = SshResult<RemoteCommandResult>.Success(notFoundResult);
        return Task.FromResult(notFound);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
