using DTech.Core.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal sealed class SshClientConnector(SshHopConnector hopConnector, IHostKeyTrustPrompt hostKeyTrustPrompt, SshConnectionHealthOptions healthOptions)
{
    private const string LoopbackAddress = "127.0.0.1";
    private const uint AnyFreePort = 0;

    public Task<SshResult<SshConnection<TClient>>> ConnectAsync<TClient>(SshConnectionTarget target, Func<ConnectionInfo, TClient> createClient, CancellationToken cancellationToken)
        where TClient : BaseClient
    {
        var hop = new SshHop<TClient>(target, target.Endpoint, createClient);
        return ConnectAsync(hop, cancellationToken);
    }

    private async Task<SshResult<SshConnection<TClient>>> ConnectAsync<TClient>(SshHop<TClient> hop, CancellationToken cancellationToken)
        where TClient : BaseClient
    {
        var target = hop.Target;
        SshTunnel? tunnel = null;
        SshHopAttempt<TClient>? attempt = null;
        var transferred = false;
        try
        {
            var dialEndpoint = target.Endpoint;
            if (target.JumpHost is not null)
            {
                var tunnelResult = await OpenTunnelAsync(target, cancellationToken);
                if (!tunnelResult.IsSuccess)
                {
                    return SshResult<SshConnection<TClient>>.Failure(tunnelResult.Error!);
                }

                tunnel = tunnelResult.Value;
                dialEndpoint = tunnel.LocalEndpoint;
            }

            hop = hop with { DialEndpoint = dialEndpoint };
            attempt = await ConnectVerifiedHopAsync(hop, cancellationToken);
            if (attempt.Client is null)
            {
                return SshResult<SshConnection<TClient>>.Failure(attempt.Error!);
            }

            var dependencies = new List<IDisposable>(attempt.OwnedResources);
            if (tunnel is not null)
            {
                dependencies.Add(tunnel);
            }

            var options = healthOptions with { ProbeEnabled = hop.ProbeConnection };
            var connection = new SshConnection<TClient>(attempt.Client, dependencies, options);
            var connected = SshResult<SshConnection<TClient>>.Success(connection);
            transferred = true;
            return connected;
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Connecting to {target.Endpoint}");
            return SshResult<SshConnection<TClient>>.Failure(error);
        }
        finally
        {
            if (!transferred)
            {
                try
                {
                    var resources = attempt?.OwnedResources ?? [];
                    SshResourceCleanup.Dispose(attempt?.Client, resources);
                }
                finally
                {
                    tunnel?.Dispose();
                }
            }
        }
    }

    private async Task<SshHopAttempt<TClient>> ConnectVerifiedHopAsync<TClient>(SshHop<TClient> hop, CancellationToken cancellationToken)
        where TClient : BaseClient
    {
        var firstAttempt = await hopConnector.ConnectAsync(hop, null, cancellationToken);
        var challenge = firstAttempt.Challenge;
        if (challenge is null)
        {
            return firstAttempt;
        }

        HostKeyPromptOutcome outcome;
        try
        {
            outcome = await hostKeyTrustPrompt.AskAsync(challenge, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var cancelledError = new SshError(SshErrorKind.Cancelled, $"Connecting to {hop.Target.Endpoint} was cancelled while waiting for a host key decision");
            return SshHopAttempt<TClient>.Failed(cancelledError);
        }
        catch (Exception exception)
        {
            var promptError = HostKeyErrors.VerifierFailed(hop.Target.Endpoint, challenge.Request.HostKey, $"host key prompt failed: {exception.Message}");
            return SshHopAttempt<TClient>.Failed(promptError);
        }

        if (outcome != HostKeyPromptOutcome.Accepted)
        {
            var notAcceptedError = HostKeyErrors.NotAccepted(challenge, outcome);
            return SshHopAttempt<TClient>.Failed(notAcceptedError);
        }

        var approvedHostKey = challenge.Request.HostKey;
        var secondAttempt = await hopConnector.ConnectAsync(hop, approvedHostKey, cancellationToken);
        if (secondAttempt.Challenge is not null)
        {
            var changedError = HostKeyErrors.ChangedDuringConfirmation(challenge);
            return SshHopAttempt<TClient>.Failed(changedError);
        }

        return secondAttempt;
    }

    private async Task<SshResult<SshTunnel>> OpenTunnelAsync(SshConnectionTarget target, CancellationToken cancellationToken)
    {
        var jumpHost = target.JumpHost!;
        var hop = new SshHop<SshClient>(jumpHost, jumpHost.Endpoint, CreateSshClient) { ProbeConnection = false };
        var jumpConnection = await ConnectAsync(hop, cancellationToken);
        if (!jumpConnection.IsSuccess)
        {
            var jumpError = jumpConnection.Error!;
            var error = jumpError with { Message = $"Jump host {jumpHost.Endpoint}: {jumpError.Message}" };
            return SshResult<SshTunnel>.Failure(error);
        }

        ForwardedPortLocal? forwardedPort = null;
        var transferred = false;
        try
        {
            forwardedPort = new ForwardedPortLocal(LoopbackAddress, AnyFreePort, target.Endpoint.Host, (uint)target.Endpoint.Port);
            jumpConnection.Value.Client.AddForwardedPort(forwardedPort);
            forwardedPort.Start();
            var tunnel = new SshTunnel(jumpConnection.Value, forwardedPort);
            var opened = SshResult<SshTunnel>.Success(tunnel);
            transferred = true;
            return opened;
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Forwarding through jump host {jumpHost.Endpoint} to {target.Endpoint}");
            return SshResult<SshTunnel>.Failure(error);
        }
        finally
        {
            if (!transferred)
            {
                try
                {
                    forwardedPort?.Dispose();
                }
                finally
                {
                    jumpConnection.Value.Dispose();
                }
            }
        }
    }

    private static SshClient CreateSshClient(ConnectionInfo connectionInfo)
    {
        return new SshClient(connectionInfo);
    }
}
