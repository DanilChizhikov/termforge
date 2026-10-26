using DTech.Core.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal sealed record SshHopAttempt<TClient>(TClient? Client, IReadOnlyList<IDisposable> OwnedResources, SshError? Error)
    where TClient : BaseClient
{
    public HostKeyChallenge? Challenge { get; init; }

    public static SshHopAttempt<TClient> Connected(TClient client, IReadOnlyList<IDisposable> ownedResources)
    {
        return new SshHopAttempt<TClient>(client, ownedResources, null);
    }

    public static SshHopAttempt<TClient> Failed(SshError error)
    {
        return new SshHopAttempt<TClient>(null, [], error);
    }

    public static SshHopAttempt<TClient> AwaitingDecision(HostKeyChallenge challenge)
    {
        var error = HostKeyErrors.NotAccepted(challenge, HostKeyPromptOutcome.Rejected);
        return new SshHopAttempt<TClient>(null, [], error)
        {
            Challenge = challenge,
        };
    }
}
