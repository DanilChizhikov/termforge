using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Ssh.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Commands;

internal sealed class SshNetRemoteCommandRunnerFactory(SshClientConnector connector) : IRemoteCommandRunnerFactory
{
    public async Task<SshResult<IRemoteCommandRunner>> ConnectAsync(SshConnectionTarget target, CancellationToken cancellationToken)
    {
        var connection = await connector.ConnectAsync(target, CreateClient, cancellationToken);
        if (!connection.IsSuccess)
        {
            return SshResult<IRemoteCommandRunner>.Failure(connection.Error!);
        }

        var runner = new SshNetRemoteCommandRunner(connection.Value);
        return SshResult<IRemoteCommandRunner>.Success(runner);
    }

    private static SshClient CreateClient(ConnectionInfo connectionInfo)
    {
        return new SshClient(connectionInfo);
    }
}
