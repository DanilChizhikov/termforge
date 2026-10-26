using DTech.Core.Connections;

namespace DTech.Core.Diagnostics;

public interface IRemoteCommandRunner : IAsyncDisposable
{
    Task<SshResult<RemoteCommandResult>> RunAsync(RemoteCommandRequest request, CancellationToken cancellationToken);
}
