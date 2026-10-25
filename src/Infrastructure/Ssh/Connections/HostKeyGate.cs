using DTech.Core.Connections;
using Renci.SshNet.Common;

namespace DTech.Ssh.Connections;

internal sealed class HostKeyGate(IHostKeyVerifier verifier, SshEndpoint endpoint)
{
    public const string Sha256FingerprintPrefix = "SHA256:";

    public HostKey? RejectedHostKey { get; private set; }

    public string? VerifierFailure { get; private set; }

    public void OnHostKeyReceived(object? sender, HostKeyEventArgs eventArgs)
    {
        var fingerprint = $"{Sha256FingerprintPrefix}{eventArgs.FingerPrintSHA256}";
        var hostKey = new HostKey(eventArgs.HostKeyName, fingerprint);
        var request = new HostKeyVerificationRequest(endpoint, hostKey);
        var decision = Decide(request);
        eventArgs.CanTrust = decision == HostKeyTrustDecision.Trust;
        RejectedHostKey = eventArgs.CanTrust ? null : hostKey;
    }

    private HostKeyTrustDecision Decide(HostKeyVerificationRequest request)
    {
        try
        {
            return verifier.Verify(request);
        }
        catch (Exception exception)
        {
            VerifierFailure = $"Host key verifier failed: {exception.Message}";
            return HostKeyTrustDecision.Reject;
        }
    }
}
