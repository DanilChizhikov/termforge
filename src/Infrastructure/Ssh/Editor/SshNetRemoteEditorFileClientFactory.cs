using DTech.Core.Connections;
using DTech.Core.Editor;
using DTech.Ssh.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Editor;

internal sealed class SshNetRemoteEditorFileClientFactory(SshClientConnector connector) : IRemoteEditorFileClientFactory
{
    public async Task<SshResult<IRemoteEditorFileClient>> ConnectAsync(SshConnectionTarget target, CancellationToken cancellationToken)
    {
        var connection = await connector.ConnectAsync(target, CreateClient, cancellationToken);
        if (!connection.IsSuccess)
        {
            return SshResult<IRemoteEditorFileClient>.Failure(connection.Error!);
        }

        var client = new SshNetRemoteEditorFileClient(connection.Value);
        return SshResult<IRemoteEditorFileClient>.Success(client);
    }

    private static SftpClient CreateClient(ConnectionInfo connectionInfo)
    {
        return new SftpClient(connectionInfo);
    }
}
