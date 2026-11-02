using DTech.Core.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal sealed class SshHopConnector(SshAuthenticationFactory authenticationFactory, IHostKeyVerifier hostKeyVerifier, SshConnectionHealthOptions healthOptions)
{
    public async Task<SshHopAttempt<TClient>> ConnectAsync<TClient>(SshHop<TClient> hop, HostKey? approvedHostKey, CancellationToken cancellationToken)
        where TClient : BaseClient
    {
        var target = hop.Target;
        var authentication = await authenticationFactory.CreateAsync(target, cancellationToken);
        if (!authentication.IsSuccess)
        {
            return SshHopAttempt<TClient>.Failed(authentication.Error!);
        }

        var ownedResources = authentication.Value.OwnedResources;
        TClient? client = null;
        HostKeyGate? hostKeyGate = null;
        var transferred = false;
        try
        {
            var connectionInfo = new ConnectionInfo(hop.DialEndpoint.Host, hop.DialEndpoint.Port, target.UserName, authentication.Value.Method);
            try
            {
                var knownKeys = approvedHostKey is null ? hostKeyVerifier.GetKnownHostKeys(target.Endpoint) : [approvedHostKey];
                HostKeyAlgorithmPreference.Apply(connectionInfo, knownKeys);
            }
            catch (Exception exception)
            {
                var verifierError = HostKeyErrors.VerifierFailed(target.Endpoint, approvedHostKey, exception.Message);
                return SshHopAttempt<TClient>.Failed(verifierError);
            }

            client = hop.CreateClient(connectionInfo);
            client.KeepAliveInterval = healthOptions.Interval;
            hostKeyGate = new HostKeyGate(hostKeyVerifier, target.Endpoint, approvedHostKey);
            client.HostKeyReceived += hostKeyGate.OnHostKeyReceived;
            await client.ConnectAsync(cancellationToken);
            var connected = SshHopAttempt<TClient>.Connected(client, ownedResources);
            transferred = true;
            return connected;
        }
        catch (Exception exception)
        {
            if (hostKeyGate is not null)
            {
                return CreateFailedAttempt<TClient>(target, hostKeyGate, exception);
            }

            var connectError = SshExceptionMapper.Map(exception, $"Connecting to {target.Endpoint}");
            return SshHopAttempt<TClient>.Failed(connectError);
        }
        finally
        {
            if (!transferred)
            {
                SshResourceCleanup.Dispose(client, ownedResources);
            }
        }
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
}
