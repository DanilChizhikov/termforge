namespace DTech.Core.Connections;

public interface IHostKeyTrustPrompt
{
    Task<HostKeyPromptOutcome> AskAsync(HostKeyChallenge challenge, CancellationToken cancellationToken);
}
