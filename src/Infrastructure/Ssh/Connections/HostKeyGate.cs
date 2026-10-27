using DTech.Core.Connections;
using Renci.SshNet.Common;

namespace DTech.Ssh.Connections;

internal sealed class HostKeyGate(IHostKeyVerifier verifier, SshEndpoint endpoint, HostKey? approvedHostKey)
{
    public HostKey? RejectedHostKey { get; private set; }

    public HostKeyVerification? Verification { get; private set; }

    public string? VerifierFailure { get; private set; }

    public void OnHostKeyReceived(object? sender, HostKeyEventArgs eventArgs)
    {
        var hostKey = ReadHostKey(eventArgs);
        if (hostKey is null)
        {
            eventArgs.CanTrust = false;
            return;
        }

        eventArgs.CanTrust = IsTrusted(hostKey);
        RejectedHostKey = eventArgs.CanTrust ? null : hostKey;
    }

    private HostKey? ReadHostKey(HostKeyEventArgs eventArgs)
    {
        try
        {
            return HostKey.FromPublicKeyBlob(eventArgs.HostKey);
        }
        catch (FormatException exception)
        {
            VerifierFailure = $"Host key presented by {endpoint} cannot be parsed: {exception.Message}";
            return null;
        }
    }

    private bool IsTrusted(HostKey hostKey)
    {
        if (hostKey == approvedHostKey)
        {
            return true;
        }

        var request = new HostKeyVerificationRequest(endpoint, hostKey);
        Verification = Verify(request);
        return Verification?.IsTrusted == true;
    }

    private HostKeyVerification? Verify(HostKeyVerificationRequest request)
    {
        try
        {
            var verification = verifier.Verify(request);
            if (verification is null)
            {
                VerifierFailure = "Host key verifier returned no result";
            }

            return verification;
        }
        catch (Exception exception)
        {
            VerifierFailure = $"Host key verifier failed: {exception.Message}";
            return null;
        }
    }
}
