using DTech.Core.Connections;

namespace DTech.Core.Files;

public interface IRemoteFileClientFactory
{
    Task<SshResult<IRemoteFileClient>> ConnectAsync(SshConnectionTarget target, CancellationToken cancellationToken);
}
