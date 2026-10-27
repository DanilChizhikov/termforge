namespace DTech.Entrypoint.Lifecycle;

internal sealed class GracefulShutdownHostedService(IHostApplicationLifetime lifetime, GracefulShutdown shutdown, ILogger<GracefulShutdownHostedService> logger) : IHostedService
{
    private static readonly TimeSpan WaitMargin = TimeSpan.FromSeconds(1);

    private CancellationTokenRegistration _stoppingRegistration;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingRegistration = lifetime.ApplicationStopping.Register(RunOnStopping);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stoppingRegistration.DisposeAsync();
    }

    private void RunOnStopping()
    {
        var running = shutdown.RunAsync();
        var waitLimit = shutdown.Timeout + WaitMargin;
        var finished = running.Wait(waitLimit);
        if (!finished)
        {
            logger.LogWarning($"Graceful shutdown did not finish within {waitLimit.TotalSeconds} seconds");
        }
    }
}
