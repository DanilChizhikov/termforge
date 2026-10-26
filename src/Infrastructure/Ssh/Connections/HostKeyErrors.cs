using DTech.Core.Connections;

namespace DTech.Ssh.Connections;

internal static class HostKeyErrors
{
    public static SshError VerifierFailed(SshEndpoint endpoint, HostKey? presentedHostKey, string reason)
    {
        var keyDescription = presentedHostKey is null ? "Host key" : $"Host key {presentedHostKey}";
        var message = $"{keyDescription} of {endpoint} was rejected: {reason}";
        return new SshError(SshErrorKind.HostKeyRejected, message)
        {
            PresentedHostKey = presentedHostKey,
        };
    }

    public static SshError Revoked(HostKeyVerificationRequest request)
    {
        var message = $"Host key {request.HostKey} of {request.Endpoint} was rejected: it is marked as revoked in known_hosts";
        return new SshError(SshErrorKind.HostKeyRejected, message)
        {
            PresentedHostKey = request.HostKey,
        };
    }

    public static SshError NotAccepted(HostKeyChallenge challenge, HostKeyPromptOutcome outcome)
    {
        var request = challenge.Request;
        var reason = outcome == HostKeyPromptOutcome.TimedOut
            ? "no decision was made in time, so the connection failed closed"
            : "it was not accepted";
        if (challenge.Verification.Status == HostKeyStatus.Changed)
        {
            var knownKeys = string.Join(", ", challenge.Verification.KnownHostKeys);
            var changedMessage = $"WARNING: host key of {request.Endpoint} has changed. Presented {request.HostKey}, known {knownKeys}. The connection was blocked because {reason}. This can mean someone is intercepting the connection";
            return new SshError(SshErrorKind.HostKeyChanged, changedMessage)
            {
                PresentedHostKey = request.HostKey,
            };
        }

        var unknownMessage = $"Host key {request.HostKey} of {request.Endpoint} is not known and was rejected: {reason}";
        return new SshError(SshErrorKind.HostKeyRejected, unknownMessage)
        {
            PresentedHostKey = request.HostKey,
        };
    }

    public static SshError ChangedDuringConfirmation(HostKeyChallenge challenge)
    {
        var request = challenge.Request;
        var message = $"Host key of {request.Endpoint} differs from the approved key {request.HostKey}; the connection was rejected";
        return new SshError(SshErrorKind.HostKeyRejected, message)
        {
            PresentedHostKey = request.HostKey,
        };
    }
}
