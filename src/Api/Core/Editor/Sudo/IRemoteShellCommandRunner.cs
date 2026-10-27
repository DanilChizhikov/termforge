using DTech.Core.Connections;

namespace DTech.Core.Editor.Sudo;

public interface IRemoteShellCommandRunner : IAsyncDisposable
{
    Task<SshResult<RemoteShellCommandResult>> RunAsync(RemoteShellCommand command, CancellationToken cancellationToken);
}
