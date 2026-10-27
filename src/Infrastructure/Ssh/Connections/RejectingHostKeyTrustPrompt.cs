using DTech.Core.Connections;

namespace DTech.Ssh.Connections;

internal sealed class RejectingHostKeyTrustPrompt : IHostKeyTrustPrompt
{
    public Task<HostKeyPromptOutcome> AskAsync(HostKeyChallenge challenge, CancellationToken cancellationToken)
    {
        return Task.FromResult(HostKeyPromptOutcome.Rejected);
    }
}
