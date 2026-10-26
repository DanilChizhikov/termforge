using DTech.Core.Terminal.Sessions;

namespace DTech.Entrypoint.Terminals;

internal sealed class TerminalShutdownService(IHostApplicationLifetime lifetime, TerminalSessionManager sessionManager, ILogger<TerminalShutdownService> logger) : IHostedService
{
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(10);

    private CancellationTokenRegistration _stoppingRegistration;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingRegistration = lifetime.ApplicationStopping.Register(CloseAllSessions);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stoppingRegistration.DisposeAsync();
        await sessionManager.CloseAllAsync(TerminalCloseReasons.ShuttingDown);
    }

    private void CloseAllSessions()
    {
        var openCount = sessionManager.Count;
        var closing = sessionManager.CloseAllAsync(TerminalCloseReasons.ShuttingDown);
        var isClosed = closing.Wait(CloseTimeout);
        if (!isClosed)
        {
            logger.LogWarning($"Terminal sessions did not close within {CloseTimeout.TotalSeconds} seconds of shutdown");
            return;
        }

        logger.LogInformation($"Closed {openCount} terminal sessions on shutdown");
    }
}
