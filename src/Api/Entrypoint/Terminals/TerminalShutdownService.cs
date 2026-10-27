using DTech.Core.Terminal.Sessions;
using DTech.Entrypoint.Lifecycle;

namespace DTech.Entrypoint.Terminals;

internal sealed class TerminalShutdownService(TerminalSessionManager sessionManager, ILogger<TerminalShutdownService> logger) : IHostedService, IShutdownStep
{
    public ShutdownStage Stage => ShutdownStage.CloseSessions;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await sessionManager.CloseAllAsync(TerminalCloseReasons.ShuttingDown);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var openCount = sessionManager.Count;
        var closing = sessionManager.CloseAllAsync(TerminalCloseReasons.ShuttingDown);
        await closing.WaitAsync(cancellationToken);
        logger.LogInformation($"Closed {openCount} terminal sessions on shutdown");
    }
}
