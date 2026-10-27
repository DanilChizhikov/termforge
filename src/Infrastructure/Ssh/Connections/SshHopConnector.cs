using DTech.Core.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal sealed class SshHopConnector(SshAuthenticationFactory authenticationFactory, IHostKeyVerifier hostKeyVerifier)
{
    public async Task<SshHopAttempt<TClient>> ConnectAsync<TClient>(SshHop<TClient> hop, HostKey? approvedHostKey, CancellationToken cancellationToken)
        where TClient : BaseClient
    {
        var target = hop.Target;
        var authentication = authenticationFactory.Create(target);
        if (!authentication.IsSuccess)
        {
            return SshHopAttempt<TClient>.Failed(authentication.Error!);
        }

        var ownedResources = authentication.Value.OwnedResources;
        var connectionInfo = new ConnectionInfo(hop.DialEndpoint.Host, hop.DialEndpoint.Port, target.UserName, authentication.Value.Method);
        var client = hop.CreateClient(connectionInfo);
        var hostKeyGate = new HostKeyGate(hostKeyVerifier, target.Endpoint, approvedHostKey);
        client.HostKeyReceived += hostKeyGate.OnHostKeyReceived;
        try
        {
            await client.ConnectAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            client.Dispose();
            DisposeAll(ownedResources);
            return CreateFailedAttempt<TClient>(target, hostKeyGate, exception);
        }

        return SshHopAttempt<TClient>.Connected(client, ownedResources);
    }

    private static SshHopAttempt<TClient> CreateFailedAttempt<TClient>(SshConnectionTarget target, HostKeyGate hostKeyGate, Exception exception)
        where TClient : BaseClient
    {
        var rejectedHostKey = hostKeyGate.RejectedHostKey;
        if (hostKeyGate.VerifierFailure is not null)
        {
            var verifierError = HostKeyErrors.VerifierFailed(target.Endpoint, rejectedHostKey, hostKeyGate.VerifierFailure);
            return SshHopAttempt<TClient>.Failed(verifierError);
        }

        var verification = hostKeyGate.Verification;
        if (rejectedHostKey is null || verification is null)
        {
            var connectError = SshExceptionMapper.Map(exception, $"Connecting to {target.Endpoint}");
            return SshHopAttempt<TClient>.Failed(connectError);
        }

        var request = new HostKeyVerificationRequest(target.Endpoint, rejectedHostKey);
        if (verification.Status == HostKeyStatus.Revoked)
        {
            var revokedError = HostKeyErrors.Revoked(request);
            return SshHopAttempt<TClient>.Failed(revokedError);
        }

        var challenge = new HostKeyChallenge(request, verification);
        return SshHopAttempt<TClient>.AwaitingDecision(challenge);
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
