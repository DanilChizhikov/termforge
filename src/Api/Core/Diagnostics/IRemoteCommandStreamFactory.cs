using DTech.Core.Connections;

namespace DTech.Core.Diagnostics;

public interface IRemoteCommandStreamFactory
{
    Task<SshResult<IRemoteCommandStream>> StartAsync(SshConnectionTarget target, string command, CancellationToken cancellationToken);
}
