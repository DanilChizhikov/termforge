using DTech.Core.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal sealed class SshTunnel(SshConnection<SshClient> jumpConnection, ForwardedPortLocal forwardedPort) : IDisposable
{
    public SshEndpoint LocalEndpoint { get; } = new(forwardedPort.BoundHost, (int)forwardedPort.BoundPort);

    public void Dispose()
    {
        forwardedPort.Dispose();
        jumpConnection.Dispose();
    }
}
