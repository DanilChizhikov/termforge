using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Ssh.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Commands;

internal sealed class SshNetRemoteCommandStreamFactory(SshClientConnector connector) : IRemoteCommandStreamFactory
{
    public async Task<SshResult<IRemoteCommandStream>> StartAsync(SshConnectionTarget target, string command, CancellationToken cancellationToken)
    {
        var connection = await connector.ConnectAsync(target, CreateClient, cancellationToken);
        if (!connection.IsSuccess)
        {
            return SshResult<IRemoteCommandStream>.Failure(connection.Error!);
        }

        try
        {
            var sshCommand = connection.Value.Client.CreateCommand(command);
            var stream = new SshNetRemoteCommandStream(connection.Value, sshCommand);
            return SshResult<IRemoteCommandStream>.Success(stream);
        }
        catch (Exception exception)
        {
            connection.Value.Dispose();
            var error = SshExceptionMapper.Map(exception, "Starting remote command");
            return SshResult<IRemoteCommandStream>.Failure(error);
        }
    }

    private static SshClient CreateClient(ConnectionInfo connectionInfo)
    {
        return new SshClient(connectionInfo);
    }
}
