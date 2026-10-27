using System.Text;
using DTech.Core.Connections;
using DTech.Core.Editor.Sudo;

namespace DTech.Core.Tests.Editor.Sudo;

internal sealed class ScriptedShellRunner : IRemoteShellCommandRunner
{
    private readonly List<ScriptedReply> _replies = [];

    public List<RecordedShellCommand> Commands { get; } = [];

    public bool Disposed { get; private set; }

    public void Reply(Func<string, bool> matches, RemoteShellCommandResult result)
    {
        var reply = new ScriptedReply(matches, result);
        _replies.Add(reply);
    }

    public void ReplySuccess(Func<string, bool> matches, string standardOutput)
    {
        var output = Encoding.UTF8.GetBytes(standardOutput);
        var result = new RemoteShellCommandResult(0, output, string.Empty);
        Reply(matches, result);
    }

    public void ReplyFailure(Func<string, bool> matches, string standardError)
    {
        var result = new RemoteShellCommandResult(1, [], standardError);
        Reply(matches, result);
    }

    public Task<SshResult<RemoteShellCommandResult>> RunAsync(RemoteShellCommand command, CancellationToken cancellationToken)
    {
        var input = command.StandardInput.ToArray();
        var recorded = new RecordedShellCommand(command.Text, input);
        Commands.Add(recorded);
        for (var i = 0; i < _replies.Count; i++)
        {
            var reply = _replies[i];
            if (reply.Matches(command.Text))
            {
                var matched = SshResult<RemoteShellCommandResult>.Success(reply.Result);
                return Task.FromResult(matched);
            }
        }

        var empty = new RemoteShellCommandResult(0, [], string.Empty);
        var fallback = SshResult<RemoteShellCommandResult>.Success(empty);
        return Task.FromResult(fallback);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private sealed record ScriptedReply(Func<string, bool> Matches, RemoteShellCommandResult Result);
}

internal sealed record RecordedShellCommand(string Text, byte[] StandardInput)
{
    public string InputText => Encoding.UTF8.GetString(StandardInput);
}
