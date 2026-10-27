namespace DTech.Core.Terminal.Sessions;

public interface ITerminalLauncher
{
    Task<TerminalResult<ManagedTerminalSession>> LaunchAsync(TerminalLaunchRequest request, CancellationToken cancellationToken);
}
