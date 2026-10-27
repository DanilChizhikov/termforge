namespace DTech.Core.Editor.Sudo;

public sealed class FileAccessReader(RemoteShellConnector connector, EditorPathResolver paths, FileAccessCommands commands) : IFileAccessReader
{
    private const int MaxOutputBytes = 4 * 1024;
    private const string Action = "Checking file access";

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

    public async Task<EditorResult<RemoteFileAccess>> ReadAsync(FileAccessRequest request, CancellationToken cancellationToken)
    {
        if (request.OwnerId < 0)
        {
            var invalid = EditorError.Invalid("The owner id must not be negative.");
            return EditorResult<RemoteFileAccess>.Failure(invalid);
        }

        var location = paths.Resolve(request.Path);
        if (!location.IsSuccess)
        {
            return EditorResult<RemoteFileAccess>.Failure(location.Error);
        }

        var connected = await connector.ConnectAsync(request.ConnectionId, cancellationToken);
        if (!connected.IsSuccess)
        {
            return EditorResult<RemoteFileAccess>.Failure(connected.Error);
        }

        await using var runner = connected.Value.Runner;
        var text = commands.Probe(request.OwnerId, location.Value);
        var command = new RemoteShellCommand(text, ProbeTimeout, MaxOutputBytes);
        var executed = await runner.RunAsync(command, cancellationToken);
        if (!executed.IsSuccess)
        {
            var failure = EditorErrorMapper.FromSsh(executed.Error!, location.Value.Path);
            return EditorResult<RemoteFileAccess>.Failure(failure);
        }

        var output = executed.Value.StandardOutputText;
        var access = executed.Value.Succeeded ? FileAccessOutputParser.Parse(output, request.OwnerId) : null;
        if (access is null)
        {
            var described = ShellCommandFailures.Describe(executed.Value, Action);
            var unreadable = new EditorError(EditorErrorCode.Failed, described.Message);
            return EditorResult<RemoteFileAccess>.Failure(unreadable);
        }

        return EditorResult<RemoteFileAccess>.Success(access);
    }
}
