using DTech.Core.Connections;
using DTech.Core.Editor.Sudo;
using DTech.Ssh.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Shell;

internal sealed class SshNetRemoteShellCommandRunnerFactory(SshClientConnector connector) : IRemoteShellCommandRunnerFactory
{
    public async Task<SshResult<IRemoteShellCommandRunner>> ConnectAsync(SshConnectionTarget target, CancellationToken cancellationToken)
    {
        var connection = await connector.ConnectAsync(target, CreateClient, cancellationToken);
        if (!connection.IsSuccess)
        {
            return SshResult<IRemoteShellCommandRunner>.Failure(connection.Error!);
        }

        var runner = new SshNetRemoteShellCommandRunner(connection.Value);
        return SshResult<IRemoteShellCommandRunner>.Success(runner);
    }

    private static SshClient CreateClient(ConnectionInfo connectionInfo)
    {
        return new SshClient(connectionInfo);
    }
}
