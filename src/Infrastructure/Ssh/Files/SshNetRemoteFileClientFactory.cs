using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Ssh.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Files;

internal sealed class SshNetRemoteFileClientFactory(SshClientConnector connector) : IRemoteFileClientFactory
{
    public async Task<SshResult<IRemoteFileClient>> ConnectAsync(SshConnectionTarget target, CancellationToken cancellationToken)
    {
        var connection = await connector.ConnectAsync(target, CreateClient, cancellationToken);
        if (!connection.IsSuccess)
        {
            return SshResult<IRemoteFileClient>.Failure(connection.Error!);
        }

        var client = new SshNetRemoteFileClient(connection.Value);
        return SshResult<IRemoteFileClient>.Success(client);
    }

    private static SftpClient CreateClient(ConnectionInfo connectionInfo)
    {
        return new SftpClient(connectionInfo);
    }
}
