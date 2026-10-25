using DTech.Core.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal sealed class SshClientConnector(SshAuthenticationFactory authenticationFactory, IHostKeyVerifier hostKeyVerifier)
{
    private const string LoopbackAddress = "127.0.0.1";
    private const uint AnyFreePort = 0;

    public async Task<SshResult<SshConnection<TClient>>> ConnectAsync<TClient>(SshConnectionTarget target, Func<ConnectionInfo, TClient> createClient, CancellationToken cancellationToken)
        where TClient : BaseClient
    {
        var dependencies = new List<IDisposable>();
        var dialEndpoint = target.Endpoint;
        if (target.JumpHost is not null)
        {
            var tunnel = await OpenTunnelAsync(target, cancellationToken);
            if (!tunnel.IsSuccess)
            {
                return SshResult<SshConnection<TClient>>.Failure(tunnel.Error!);
            }

            dependencies.Add(tunnel.Value);
            dialEndpoint = tunnel.Value.LocalEndpoint;
        }

        var authentication = authenticationFactory.Create(target);
        if (!authentication.IsSuccess)
        {
            DisposeAll(dependencies);
            return SshResult<SshConnection<TClient>>.Failure(authentication.Error!);
        }

        dependencies.InsertRange(0, authentication.Value.OwnedResources);
        var connectionInfo = new ConnectionInfo(dialEndpoint.Host, dialEndpoint.Port, target.UserName, authentication.Value.Method);
        var client = createClient(connectionInfo);
        var hostKeyGate = new HostKeyGate(hostKeyVerifier, target.Endpoint);
        client.HostKeyReceived += hostKeyGate.OnHostKeyReceived;
        try
        {
            await client.ConnectAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            client.Dispose();
            DisposeAll(dependencies);
            var error = CreateConnectError(target, hostKeyGate, exception);
            return SshResult<SshConnection<TClient>>.Failure(error);
        }

        var connection = new SshConnection<TClient>(client, dependencies);
        return SshResult<SshConnection<TClient>>.Success(connection);
    }

    private async Task<SshResult<SshTunnel>> OpenTunnelAsync(SshConnectionTarget target, CancellationToken cancellationToken)
    {
        var jumpHost = target.JumpHost!;
        var jumpConnection = await ConnectAsync(jumpHost, CreateSshClient, cancellationToken);
        if (!jumpConnection.IsSuccess)
        {
            var jumpError = jumpConnection.Error!;
            var error = jumpError with { Message = $"Jump host {jumpHost.Endpoint}: {jumpError.Message}" };
            return SshResult<SshTunnel>.Failure(error);
        }

        var forwardedPort = new ForwardedPortLocal(LoopbackAddress, AnyFreePort, target.Endpoint.Host, (uint)target.Endpoint.Port);
        try
        {
            jumpConnection.Value.Client.AddForwardedPort(forwardedPort);
            forwardedPort.Start();
        }
        catch (Exception exception)
        {
            forwardedPort.Dispose();
            jumpConnection.Value.Dispose();
            var error = SshExceptionMapper.Map(exception, $"Forwarding through jump host {jumpHost.Endpoint} to {target.Endpoint}");
            return SshResult<SshTunnel>.Failure(error);
        }

        var tunnel = new SshTunnel(jumpConnection.Value, forwardedPort);
        return SshResult<SshTunnel>.Success(tunnel);
    }

    private static SshClient CreateSshClient(ConnectionInfo connectionInfo)
    {
        return new SshClient(connectionInfo);
    }

    private static SshError CreateConnectError(SshConnectionTarget target, HostKeyGate hostKeyGate, Exception exception)
    {
        var rejectedHostKey = hostKeyGate.RejectedHostKey;
        if (rejectedHostKey is null)
        {
            return SshExceptionMapper.Map(exception, $"Connecting to {target.Endpoint}");
        }

        var reason = hostKeyGate.VerifierFailure ?? "not trusted by the host key verifier";
        var message = $"Host key {rejectedHostKey.Algorithm} {rejectedHostKey.Sha256Fingerprint} of {target.Endpoint} was rejected: {reason}";
        return new SshError(SshErrorKind.HostKeyRejected, message)
        {
            PresentedHostKey = rejectedHostKey,
        };
    }

    private static void DisposeAll(IReadOnlyList<IDisposable> resources)
    {
        for (var i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            resource.Dispose();
        }
    }
}
